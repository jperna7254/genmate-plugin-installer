using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

public interface IVersionService
{
    List<PluginVersionInfo> GetAvailableVersions();
}
