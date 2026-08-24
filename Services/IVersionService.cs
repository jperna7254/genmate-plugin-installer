using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

public interface IVersionService
{
    Task<List<PluginVersionInfo>> GetAvailableVersionsAsync(PluginChannel channel);
}
