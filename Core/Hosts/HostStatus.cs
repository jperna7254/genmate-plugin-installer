namespace GenMate.PluginInstaller.Core.Hosts;

public sealed record HostStatus(PluginHost Host, bool ApplicationInstalled, string? InstalledVersion)
{
    // An install whose application has since been removed is still offered, so it can be uninstalled.
    public bool IsOffered => ApplicationInstalled || InstalledVersion is not null;
}
