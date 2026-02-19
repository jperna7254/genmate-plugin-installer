namespace GenMate.PluginInstaller.Services;

public interface IPluginInstallService
{
    Task InstallAsync(string downloadUrl, IProgress<int> progress, CancellationToken ct = default);
    Task UninstallAsync();
}
