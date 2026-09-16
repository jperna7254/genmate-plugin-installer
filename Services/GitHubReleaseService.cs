using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Hosts;
using GenMate.PluginInstaller.Core.SelfUpdate;
using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

// Cross-repo contract with the plugin releases repository. None of it is visible in that repo, so a
// change made there silently breaks every installed copy of this app in the field:
//   - the release tag must be "v{version}";
//   - the release must be published, not a draft and not a prerelease;
//   - it must carry the bundle asset the channel document names for each host; a release without
//     one still appears in that host's version list, marked as having no package, and cannot be
//     installed for it;
//   - that zip's layout is PluginHost's contract, whose PackageContents.xml carries the installed
//     version in its AppVersion attribute;
//   - that AppVersion value must equal the tag with its leading "v" removed, character for
//     character, because HostViewModel compares the two with exact string equality.
//     A "v1.2.3" tag shipping AppVersion="1.2.3.0" satisfies every clause above and installs
//     fine, yet the app then reports 1.2.3.0 installed while offering 1.2.3 as if it were not.
// The repository and the asset names come from channel.json, so renaming an asset is an edit to
// that document rather than a forced re-download by every customer. The other clauses are not in
// the document - the tag shape, the zip's root folder and the AppVersion equality are compiled in,
// and changing any of them still breaks every fielded installer that predates the change. Change
// those additively: match the old shape as well as the new, and drop the old form only once no
// fielded installer matters.
public class GitHubReleaseService : IVersionService
{
    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "GenMate-PluginInstaller" },
            { "Accept", "application/vnd.github+json" }
        }
    };

    public async Task<IReadOnlyDictionary<string, List<PluginVersionInfo>>> GetAvailableVersionsAsync(
        PluginChannel channel)
    {
        List<GitHubRelease>? releases;
        try
        {
            releases = await HttpClient.GetFromJsonAsync<List<GitHubRelease>>(
                $"https://api.github.com/repos/{channel.Repo}/releases");
        }
        catch
        {
            releases = null;
        }

        var published = (releases ?? [])
            .Where(r => !r.Draft && !r.Prerelease)
            .Select(r => new { Release = r, Version = r.TagName.TrimStart('v') })
            .ToList();

        return channel.Hosts.ToDictionary(
            entry => entry.Key,
            entry => published
                .Where(r => ReleaseVersion.IsAtOrAboveFloor(r.Version, entry.Value.MinimumVersion))
                .Select(r => new PluginVersionInfo
                {
                    Version = r.Version,
                    ReleaseDate = r.Release.PublishedAt,
                    DownloadUrl = FindBundleUrl(r.Release, entry.Key, entry.Value, r.Version)
                })
                .OrderByDescending(v => v.ReleaseDate)
                .ToList());
    }

    private static string? FindBundleUrl(GitHubRelease release, string hostId, HostChannel host, string version)
    {
        var assets = release.Assets ?? [];
        var name = BundleAsset.Find(assets.Select(a => a.Name), hostId, host, version);
        return assets.FirstOrDefault(a => a.Name == name)?.BrowserDownloadUrl;
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
