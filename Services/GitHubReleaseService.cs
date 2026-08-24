using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.SelfUpdate;
using GenMate.PluginInstaller.Models;

namespace GenMate.PluginInstaller.Services;

// Cross-repo contract with the plugin releases repository. None of it is visible in that repo, so a
// change made there silently breaks every installed copy of this app in the field:
//   - the release tag must be "v{version}";
//   - the release must be published, not a draft and not a prerelease;
//   - it must carry the bundle asset the channel document names for the host; a release without
//     one still appears in the version list, with no download, and cannot be installed;
//   - that zip must contain a "GenMate.bundle/" root folder (see PluginInstallService), whose
//     PackageContents.xml carries the installed version in its AppVersion attribute
//     (see PluginDetectionService);
//   - that AppVersion value must equal the tag with its leading "v" removed, character for
//     character, because MainWindow.LoadDataAsync compares the two with exact string equality.
//     A "v1.2.3" tag shipping AppVersion="1.2.3.0" satisfies every clause above and installs
//     fine, yet the app then reports 1.2.3.0 installed while offering 1.2.3 as if it were not.
// The repository and the asset name now come from channel.json, so renaming an asset is an edit to
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

    public async Task<List<PluginVersionInfo>> GetAvailableVersionsAsync(PluginChannel channel)
    {
        if (!channel.Hosts.TryGetValue(CadHosts.AutoCad, out var host))
            return [];

        try
        {
            var releases = await HttpClient.GetFromJsonAsync<List<GitHubRelease>>(
                $"https://api.github.com/repos/{channel.Repo}/releases");
            if (releases is null)
                return [];

            return releases
                .Where(r => !r.Draft && !r.Prerelease)
                .Select(r => new { Release = r, Version = r.TagName.TrimStart('v') })
                .Where(r => ReleaseVersion.IsAtOrAboveFloor(r.Version, host.MinimumVersion))
                .Select(r => new PluginVersionInfo
                {
                    Version = r.Version,
                    ReleaseDate = r.Release.PublishedAt,
                    DownloadUrl = FindBundle(r.Release, host, r.Version)?.BrowserDownloadUrl
                })
                .OrderByDescending(v => v.ReleaseDate)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static GitHubAsset? FindBundle(GitHubRelease release, HostChannel host, string version)
    {
        var expected = host.ResolveBundleAsset(version);

        // The legacy prefix is matched as well as the channel document's anchored name so that a
        // release published before the document existed still offers a download.
        return release.Assets?.FirstOrDefault(a =>
                   a.Name.Equals(expected, StringComparison.OrdinalIgnoreCase))
               ?? release.Assets?.FirstOrDefault(a =>
                   a.Name.StartsWith("GenMate.bundle-", StringComparison.Ordinal) &&
                   a.Name.EndsWith(".zip", StringComparison.Ordinal));
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
