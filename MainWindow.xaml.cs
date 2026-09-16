using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Diagnostics;
using GenMate.PluginInstaller.Core.Hosts;
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

    private readonly IReadOnlyList<PluginHost> _pluginHosts;
    private readonly IHostMachine _hostMachine;
    private readonly PluginHostService _hostService;
    private readonly IVersionService _versionService;
    private readonly IPluginInstallService _installService;
    private readonly ChannelDocumentReader _channelReader;
    private readonly SelfUpdateService _selfUpdateService;

    private ChannelDocument _channel = ChannelDocument.Fallback;

    private List<HostViewModel> _hosts = [];
    private HostViewModel? _selectedHost;
    private bool _isLoaded;
    private bool _isBusy;
    private int _downloadProgress;
    private string? _statusMessage;

    public MainWindow()
    {
        var log = FileUpdateLog.Default();
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        _pluginHosts = [PluginHost.AutoCad(programData), PluginHost.BricsCad(programData)];
        _hostMachine = new WindowsHostMachine();
        _hostService = new PluginHostService(
            _pluginHosts,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenMate"),
            _hostMachine);
        _versionService = new GitHubReleaseService();
        _installService = new PluginInstallService(_hostService);
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

    // Three parts rather than Version's four, so the label reads as the release tag on GitHub.
    public string RunningVersion { get; } =
        (Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0)).ToString(3);

    public List<HostViewModel> Hosts
    {
        get => _hosts;
        set { _hosts = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasNoHosts)); }
    }

    public HostViewModel? SelectedHost
    {
        get => _selectedHost;
        set { _selectedHost = value; OnPropertyChanged(); }
    }

    // Not shown before the first load, so the window does not claim no CAD application is installed
    // while it is still checking for updates.
    public bool HasNoHosts => _isLoaded && Hosts.Count == 0;

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
        var statuses = _pluginHosts
            .Select(_hostService.GetStatus)
            .Where(status => status.IsOffered)
            .ToList();

        var versions = await _versionService.GetAvailableVersionsAsync(_channel.Plugin);

        var selectedHostId = SelectedHost?.Host.Id;
        _isLoaded = true;
        Hosts = statuses
            .Select(status => new HostViewModel(
                status,
                _channel.Plugin.Hosts.TryGetValue(status.Host.Id, out var channelHost)
                    ? channelHost.DisplayName
                    : status.Host.ApplicationName,
                versions.GetValueOrDefault(status.Host.Id) ?? []))
            .ToList();
        SelectedHost = Hosts.FirstOrDefault(h => h.Host.Id == selectedHostId) ?? Hosts.FirstOrDefault();
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PluginVersionInfo version, Tag: HostViewModel host })
            return;

        if (version.DownloadUrl is null)
        {
            MessageBox.Show(
                $"v{version.Version} has no package for {host.DisplayName}.",
                "Install Plugin",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (_hostMachine.IsApplicationRunning(host.Host))
        {
            MessageBox.Show(
                $"Please close {host.Host.ApplicationName} before installing the plugin.",
                $"{host.Host.ApplicationName} Is Running",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var action = host.InstalledVersion is not null
            ? $"replace v{host.InstalledVersion} with v{version.Version} for {host.DisplayName}"
            : $"install v{version.Version} for {host.DisplayName}";

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

            await _installService.InstallAsync(host.Host, version.DownloadUrl, progress);

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
        if (sender is not FrameworkElement { DataContext: HostViewModel host })
            return;

        if (_hostMachine.IsApplicationRunning(host.Host))
        {
            MessageBox.Show(
                $"Please close {host.Host.ApplicationName} before uninstalling the plugin.",
                $"{host.Host.ApplicationName} Is Running",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to uninstall v{host.InstalledVersion} from {host.DisplayName}?",
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
            await _installService.UninstallAsync(host.Host);

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
