using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Models;

public class HostViewModel
{
    public HostViewModel(HostStatus status, string displayName, List<PluginVersionInfo> availableVersions)
    {
        Host = status.Host;
        DisplayName = displayName;
        InstalledVersion = status.InstalledVersion;
        AvailableVersions = availableVersions;

        foreach (var version in availableVersions)
            version.IsInstalled = version.Version == InstalledVersion;
    }

    public PluginHost Host { get; }
    public string DisplayName { get; }
    public string? InstalledVersion { get; }
    public bool IsPluginInstalled => InstalledVersion is not null;
    public List<PluginVersionInfo> AvailableVersions { get; }
    public bool HasNoVersions => AvailableVersions.Count == 0;
}
