using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

public class HardcodedVersionService : IVersionService
{
    public List<PluginVersionInfo> GetAvailableVersions()
    {
        return
        [
            new PluginVersionInfo
            {
                Version = "1.2.0",
                ReleaseDate = new DateTime(2026, 2, 10),
                Description = "Latest release with performance improvements"
            },
            new PluginVersionInfo
            {
                Version = "1.1.0",
                ReleaseDate = new DateTime(2026, 1, 15),
                Description = "Edge validation and sync enhancements"
            },
            new PluginVersionInfo
            {
                Version = "1.0.0",
                ReleaseDate = new DateTime(2025, 12, 1),
                Description = "Initial release"
            }
        ];
    }
}
