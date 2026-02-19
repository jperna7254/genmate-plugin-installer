using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using GenMate.PluginInstaller.Models;
using GenMate.PluginInstaller.Services;

namespace GenMate.PluginInstaller;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly IPluginDetectionService _detectionService;
    private readonly IVersionService _versionService;
    private readonly IPluginInstallService _installService;
    private readonly IAutoCADDetectionService _autoCADDetectionService;

    private string? _installedVersion;
    private bool _isPluginInstalled;
    private List<PluginVersionInfo> _availableVersions = [];
    private bool _isBusy;
    private int _downloadProgress;
    private string? _statusMessage;

    public MainWindow()
    {
        _detectionService = new PluginDetectionService();
        _versionService = new GitHubReleaseService();
        _installService = new PluginInstallService();
        _autoCADDetectionService = new AutoCADDetectionService();

        InitializeComponent();
        DataContext = this;
        Loaded += async (_, _) => await LoadDataAsync();
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

    private async Task LoadDataAsync()
    {
        InstalledVersion = _detectionService.GetInstalledVersion();
        IsPluginInstalled = InstalledVersion is not null;

        var versions = await _versionService.GetAvailableVersionsAsync();
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
