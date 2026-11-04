using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Models;

public class HostViewModel
{
    /// <param name="availableVersions">Null when the channel document does not publish this host at all.</param>
    public HostViewModel(HostStatus status, string displayName, List<PluginVersionInfo>? availableVersions)
    {
        Host = status.Host;
        DisplayName = displayName;
        IsPublished = availableVersions is not null;
        InstalledVersion = status.InstalledVersion;
        AvailableVersions = availableVersions ?? [];

        foreach (var version in AvailableVersions)
            version.IsInstalled = version.Version == InstalledVersion;
    }

    public PluginHost Host { get; }
    public string DisplayName { get; }
    public string? InstalledVersion { get; }
    public bool IsPluginInstalled => InstalledVersion is not null;
    public List<PluginVersionInfo> AvailableVersions { get; }
    public bool IsNotPublished => !IsPublished;
    public bool IsPublished { get; }
    public bool HasNoVersions => IsPublished && AvailableVersions.Count == 0;
}
