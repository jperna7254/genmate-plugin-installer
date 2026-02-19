using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

public class GitHubReleaseService : IVersionService
{
    private const string ReleasesUrl = "https://api.github.com/repos/jperna7254/genmate-plugin-releases/releases";

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "GenMate-PluginInstaller" },
            { "Accept", "application/vnd.github+json" }
        }
    };

    public async Task<List<PluginVersionInfo>> GetAvailableVersionsAsync()
    {
        try
        {
            var releases = await HttpClient.GetFromJsonAsync<List<GitHubRelease>>(ReleasesUrl);
            if (releases is null)
                return [];

            return releases
                .Where(r => !r.Draft && !r.Prerelease)
                .Select(r => new PluginVersionInfo
                {
                    Version = r.TagName.TrimStart('v'),
                    ReleaseDate = r.PublishedAt,
                    DownloadUrl = r.Assets
                        ?.FirstOrDefault(a => a.Name.StartsWith("GenMate.bundle-") && a.Name.EndsWith(".zip"))
                        ?.BrowserDownloadUrl
                })
                .OrderByDescending(v => v.ReleaseDate)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public required string TagName { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; init; }
    }

    private class GitHubAsset
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public required string BrowserDownloadUrl { get; init; }
    }
}
