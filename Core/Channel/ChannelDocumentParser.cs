using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenMate.PluginInstaller.Core.Channel;

public static class ChannelDocumentParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Reads a channel document, or reports failure. A document that is malformed, declares a
    /// schema other than <see cref="ChannelDocument.SupportedSchema"/>, or omits a field this
    /// build needs is rejected whole rather than merged with the fallback: a half-applied layout
    /// points some names at the new release shape and some at the old, which is neither layout.
    /// </summary>
    public static bool TryParse(string json, out ChannelDocument document, out string? failure)
    {
        document = ChannelDocument.Fallback;

        ChannelJson? raw;
        try
        {
            raw = JsonSerializer.Deserialize<ChannelJson>(json, Options);
        }
        catch (JsonException ex)
        {
            failure = $"not valid JSON: {ex.Message}";
            return false;
        }

        if (raw is null)
        {
            failure = "document was null";
            return false;
        }

        if (raw.Schema != ChannelDocument.SupportedSchema)
        {
            failure = $"schema {raw.Schema} is not the schema {ChannelDocument.SupportedSchema} this build reads";
            return false;
        }

        if (raw.Installer is null || string.IsNullOrWhiteSpace(raw.Installer.Repo) ||
            string.IsNullOrWhiteSpace(raw.Installer.AssetPattern))
        {
            failure = "installer block is missing repo or assetPattern";
            return false;
        }

        if (raw.Plugin is null || string.IsNullOrWhiteSpace(raw.Plugin.Repo) || raw.Plugin.Hosts is null)
        {
            failure = "plugin block is missing repo or hosts";
            return false;
        }

        var hosts = new Dictionary<string, HostChannel>(StringComparer.Ordinal);
        foreach (var (id, host) in raw.Plugin.Hosts)
        {
            if (!CadHosts.Known.Contains(id))
                continue;

            if (host is null || string.IsNullOrWhiteSpace(host.DisplayName) ||
                string.IsNullOrWhiteSpace(host.BundleAsset) ||
                string.IsNullOrWhiteSpace(host.ManifestAsset) ||
                string.IsNullOrWhiteSpace(host.SignatureAsset))
            {
                failure = $"host '{id}' is missing one of displayName, bundleAsset, manifestAsset, signatureAsset";
                return false;
            }

            if (!TryParseFloor(host.MinimumVersion, out var hostFloor))
            {
                failure = $"host '{id}' has an unparseable minimumVersion '{host.MinimumVersion}'";
                return false;
            }

            hosts[id] = new HostChannel
            {
                DisplayName = host.DisplayName,
                BundleAsset = host.BundleAsset,
                ManifestAsset = host.ManifestAsset,
                SignatureAsset = host.SignatureAsset,
                MinimumVersion = hostFloor
            };
        }

        if (hosts.Count == 0)
        {
            failure = "no host in the document is one this build has code for";
            return false;
        }

        if (!TryParseFloor(raw.Installer.MinimumVersion, out var installerFloor))
        {
            failure = $"installer has an unparseable minimumVersion '{raw.Installer.MinimumVersion}'";
            return false;
        }

        document = new ChannelDocument
        {
            Installer = new InstallerChannel
            {
                Repo = raw.Installer.Repo,
                AssetPattern = raw.Installer.AssetPattern,
                MinimumVersion = installerFloor
            },
            Plugin = new PluginChannel
            {
                Repo = raw.Plugin.Repo,
                Hosts = hosts
            }
        };

        failure = null;
        return true;
    }

    private static bool TryParseFloor(string? value, out Version? floor)
    {
        floor = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!Version.TryParse(value, out var parsed))
            return false;

        floor = parsed;
        return true;
    }

    private sealed class ChannelJson
    {
        [JsonPropertyName("schema")]
        public int Schema { get; init; }

        [JsonPropertyName("installer")]
        public InstallerJson? Installer { get; init; }

        [JsonPropertyName("plugin")]
        public PluginJson? Plugin { get; init; }
    }

    private sealed class InstallerJson
    {
        [JsonPropertyName("repo")]
        public string? Repo { get; init; }

        [JsonPropertyName("assetPattern")]
        public string? AssetPattern { get; init; }

        [JsonPropertyName("minimumVersion")]
        public string? MinimumVersion { get; init; }
    }

    private sealed class PluginJson
    {
        [JsonPropertyName("repo")]
        public string? Repo { get; init; }

        [JsonPropertyName("hosts")]
        public Dictionary<string, HostJson?>? Hosts { get; init; }
    }

    private sealed class HostJson
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("bundleAsset")]
        public string? BundleAsset { get; init; }

        [JsonPropertyName("manifestAsset")]
        public string? ManifestAsset { get; init; }

        [JsonPropertyName("signatureAsset")]
        public string? SignatureAsset { get; init; }

        [JsonPropertyName("minimumVersion")]
        public string? MinimumVersion { get; init; }
    }
}
