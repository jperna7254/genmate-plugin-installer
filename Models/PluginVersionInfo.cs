namespace GenMate.PluginInstaller.Models;

public class PluginVersionInfo
{
    public required string Version { get; init; }
    public required DateTimeOffset ReleaseDate { get; init; }
    public string? DownloadUrl { get; init; }
    public bool IsInstalled { get; set; }
    public bool CanInstall => !IsInstalled && DownloadUrl is not null;
    public bool HasNoPackage => !IsInstalled && DownloadUrl is null;
}
