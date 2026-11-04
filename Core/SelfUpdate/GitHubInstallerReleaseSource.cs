using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GenMate.PluginInstaller.Core.Channel;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>
/// Reads the installer's own releases feed - a different repository from the plugin releases that
/// <c>GitHubReleaseService</c> lists.
/// </summary>
public sealed class GitHubInstallerReleaseSource : IInstallerReleaseSource
{
    private readonly HttpClient _http;

    public GitHubInstallerReleaseSource(HttpClient http)
    {
        _http = http;
    }

    public async Task<InstallerRelease?> GetLatestAsync(InstallerChannel channel, CancellationToken ct)
    {
        // /releases/latest rather than /releases: GitHub already excludes drafts and prereleases
        // from it, and it is one small response rather than the whole history.
        var url = $"https://api.github.com/repos/{channel.Repo}/releases/latest";
        var release = await _http.GetFromJsonAsync<GitHubRelease>(url, ct);
        if (release is null || !ReleaseVersion.TryParseTag(release.TagName, out var version))
            return null;

        var asset = release.Assets?.FirstOrDefault(a => AssetPattern.Matches(channel.AssetPattern, a.Name));
        if (asset is null || !Uri.TryCreate(asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUrl))
            return null;

        return new InstallerRelease(version, downloadUrl);
    }

    public async Task DownloadAsync(
        InstallerRelease release,
        string destinationPath,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        using var response = await _http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1;
        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(
            destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long bytesRead = 0;
        int read;
        var lastReported = -1;
        while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
            bytesRead += read;

            if (progress is null || totalBytes <= 0)
                continue;

            var percent = (int)(bytesRead * 100 / totalBytes);
            if (percent == lastReported)
                continue;

            lastReported = percent;
            progress.Report(percent);
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset>? Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("browser_download_url")]
        public required string BrowserDownloadUrl { get; init; }
    }
}
