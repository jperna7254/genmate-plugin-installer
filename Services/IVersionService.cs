using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

public interface IVersionService
{
    /// <summary>The releases offered for each host the channel names, keyed by host id.</summary>
    Task<IReadOnlyDictionary<string, List<PluginVersionInfo>>> GetAvailableVersionsAsync(PluginChannel channel);
}
