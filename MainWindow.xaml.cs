using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Diagnostics;
using GenMate.PluginInstaller.Core.SelfUpdate;
using GenMate.PluginInstaller.Models;
using GenMate.PluginInstaller.Services;

namespace GenMate.PluginInstaller;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    // Every request made through this client is already bounded by its own linked
    // CancellationTokenSource - ChannelDocumentReader's 10s fetch, the update check's 15s, the
    // download's 20 minutes. Those budgets were each chosen for what the user is waiting on, and an
    // infinite client timeout leaves them the single visible answer to how long anything may take,
    // rather than sharing that answer with a default whose interaction with a streamed body read is
    // subtle enough that two readings of it have disagreed. Anything added here must bring its own
    // token; a request without one would wait forever.
    private static readonly HttpClient UpdateHttpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders =
        {
            { "User-Agent", "GenMate-PluginInstaller" },
            { "Accept", "application/vnd.github+json" }
        }
    };

    private readonly IPluginDetectionService _detectionService;
    private readonly IVersionService _versionService;
    private readonly IPluginInstallService _installService;
    private readonly IAutoCADDetectionService _autoCADDetectionService;
    private readonly ChannelDocumentReader _channelReader;
    private readonly SelfUpdateService _selfUpdateService;

    private ChannelDocument _channel = ChannelDocument.Fallback;

    private string? _installedVersion;
    private bool _isPluginInstalled;
    private List<PluginVersionInfo> _availableVersions = [];
    private bool _isBusy;
    private int _downloadProgress;
    private string? _statusMessage;

    public MainWindow()
    {
        var log = FileUpdateLog.Default();
        _detectionService = new PluginDetectionService();
        _versionService = new GitHubReleaseService();
        _installService = new PluginInstallService();
        _autoCADDetectionService = new AutoCADDetectionService();
        _channelReader = new ChannelDocumentReader(UpdateHttpClient, log);
        _selfUpdateService = new SelfUpdateService(
            Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0, 0),
            new GitHubInstallerReleaseSource(UpdateHttpClient),
            // The one line to change when a code-signing certificate exists; the class it names
            // carries what the replacement must prove and why there is nothing here yet.
            new AcceptUnsignedInstallerVerifier(),
            new LocalUpdateEnvironment(),
            log);

        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await StartAsync();
    }

    public string? InstalledVersion
    {
        get => _installedVersion;
        set { _installedVersion = value; OnPropertyChanged(); }
    }

    public bool IsPluginInstalled
    {
        get => _isPluginInstalled;
        set { _isPluginInstalled = value; OnPropertyChanged(); }
    }

    public List<PluginVersionInfo> AvailableVersions
    {
        get => _availableVersions;
        set { _availableVersions = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
    }

    public bool IsNotBusy => !IsBusy;

    public int DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(); }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    // The update runs before the version list is fetched, so that everything most likely to need
    // fixing in the field - which releases are offered, how a bundle is verified, what a CAD
    // install looks like - sits behind the one thing that can replace itself.
    private async Task StartAsync()
    {
        if (await TryUpdateSelfAsync())
            return;

        await LoadDataAsync();
    }

    private async Task<bool> TryUpdateSelfAsync()
    {
        IsBusy = true;
        DownloadProgress = 0;
        StatusMessage = "Checking for updates...";

        try
        {
            _selfUpdateService.CleanUpPreviousUpdate();
            _channel = await _channelReader.ReadAsync();

            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                StatusMessage = "Updating GenMate Installer...";
            });

            if (await _selfUpdateService.TryUpdateAsync(_channel.Installer, progress) !=
                SelfUpdateOutcome.RelaunchStarted)
            {
                return false;
            }

            StatusMessage = "Restarting...";
            Application.Current.Shutdown();
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDataAsync()
    {
        InstalledVersion = _detectionService.GetInstalledVersion();
        IsPluginInstalled = InstalledVersion is not null;

        var versions = await _versionService.GetAvailableVersionsAsync(_channel.Plugin);
        foreach (var version in versions)
            version.IsInstalled = version.Version == InstalledVersion;

        AvailableVersions = versions;
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PluginVersionInfo version })
            return;

        if (version.DownloadUrl is null)
        {
            MessageBox.Show(
                "No download available for this version.",
                "Install Plugin",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_autoCADDetectionService.IsAutoCADRunning())
        {
            MessageBox.Show(
                "Please close AutoCAD before installing the plugin.",
                "AutoCAD Is Running",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var action = InstalledVersion is not null
            ? $"replace v{InstalledVersion} with v{version.Version}"
            : $"install v{version.Version}";

        var result = MessageBox.Show(
            $"Are you sure you want to {action}?",
            "Confirm Install",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        DownloadProgress = 0;
        StatusMessage = "Downloading...";

        try
        {
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                StatusMessage = $"Downloading... {p}%";
            });

            await _installService.InstallAsync(version.DownloadUrl, progress);

            StatusMessage = "Installation complete!";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Installation failed: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusMessage = "Installation failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (_autoCADDetectionService.IsAutoCADRunning())
        {
            MessageBox.Show(
                "Please close AutoCAD before uninstalling the plugin.",
                "AutoCAD Is Running",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to uninstall v{InstalledVersion}?",
            "Confirm Uninstall",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        DownloadProgress = 0;
        StatusMessage = "Uninstalling...";

        try
        {
            await _installService.UninstallAsync();

            StatusMessage = "Uninstall complete!";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Uninstall failed: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusMessage = "Uninstall failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
