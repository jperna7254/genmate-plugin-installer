using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Services;

public interface IPluginInstallService
{
    Task InstallAsync(PluginHost host, string downloadUrl, IProgress<int> progress, CancellationToken ct = default);
    Task UninstallAsync(PluginHost host);
}
