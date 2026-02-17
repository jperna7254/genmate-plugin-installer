namespace GenMate.PluginInstaller.Models;

public class PluginVersionInfo
{
    public required string Version { get; init; }
    public required DateTime ReleaseDate { get; init; }
    public required string Description { get; init; }
    public bool IsInstalled { get; set; }
}
