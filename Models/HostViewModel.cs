using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Models;

public class HostViewModel
{
    public HostViewModel(HostStatus status, List<PluginVersionInfo> availableVersions)
    {
        Host = status.Host;
        InstalledVersion = status.InstalledVersion;
        AvailableVersions = availableVersions;

        foreach (var version in availableVersions)
            version.IsInstalled = version.Version == InstalledVersion;
    }

    public PluginHost Host { get; }
    public string DisplayName => Host.ApplicationName;
    public string? InstalledVersion { get; }
    public bool IsPluginInstalled => InstalledVersion is not null;
    public List<PluginVersionInfo> AvailableVersions { get; }
    public bool HasNoVersions => AvailableVersions.Count == 0;
}
