using GenMate.PluginInstaller.Core.Channel;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

public interface IInstallerReleaseSource
{
    /// <summary>Returns null when the feed carries no release this build can parse.</summary>
    Task<InstallerRelease?> GetLatestAsync(InstallerChannel channel, CancellationToken ct);

    Task DownloadAsync(InstallerRelease release, string destinationPath, IProgress<int>? progress, CancellationToken ct);
}
