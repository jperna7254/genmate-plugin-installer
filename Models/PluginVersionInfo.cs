namespace GenMate.PluginInstaller.Models;

public class PluginVersionInfo
{
    public required string Version { get; init; }
    public required DateTimeOffset ReleaseDate { get; init; }
    public required string Description { get; init; }
    public string? DownloadUrl { get; init; }
    public bool IsInstalled { get; set; }
}
