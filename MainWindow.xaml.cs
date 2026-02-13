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

    private string? _installedVersion;
    private bool _isPluginInstalled;
    private List<PluginVersionInfo> _availableVersions = [];

    public MainWindow()
    {
        _detectionService = new PluginDetectionService();
        _versionService = new HardcodedVersionService();

        InitializeComponent();
        DataContext = this;
        LoadData();
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

    private void LoadData()
    {
        InstalledVersion = _detectionService.GetInstalledVersion();
        IsPluginInstalled = InstalledVersion is not null;

        var versions = _versionService.GetAvailableVersions();
        foreach (var version in versions)
            version.IsInstalled = version.Version == InstalledVersion;

        AvailableVersions = versions;
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: PluginVersionInfo version })
        {
            MessageBox.Show(
                $"Install v{version.Version} — not implemented yet.",
                "Install Plugin",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Uninstall — not implemented yet.",
            "Uninstall Plugin",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
