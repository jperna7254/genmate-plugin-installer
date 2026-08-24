namespace GenMate.PluginInstaller.Core.Channel;

/// <summary>
/// The release-layout contract this installer reads at launch, so that a change to asset names,
/// tag shape or repository is an edit to a published file rather than a forced re-download by
/// every customer who already has the installer.
/// </summary>
public sealed class ChannelDocument
{
    // A document that declares any other schema is discarded whole rather than read field by field:
    // schema is bumped precisely when the meaning of a field changed, so a partial read of an
    // unknown schema is a read of fields that no longer mean what this build thinks they mean.
    public const int SupportedSchema = 2;

    public required InstallerChannel Installer { get; init; }

    public required PluginChannel Plugin { get; init; }

    /// <summary>
    /// The layout this build shipped with, used whenever <c>channel.json</c> is unreachable or
    /// unreadable, so the installer still works offline against the release names it knows.
    /// </summary>
    public static ChannelDocument Fallback { get; } = new()
    {
        Installer = new InstallerChannel
        {
            Repo = "jperna7254/genmate-plugin-installer",
            AssetPattern = "GenMate.PluginInstaller*.exe",
            MinimumVersion = null
        },
        Plugin = new PluginChannel
        {
            Repo = "jperna7254/genmate-plugin-releases",
            Hosts = new Dictionary<string, HostChannel>
            {
                [CadHosts.AutoCad] = new()
                {
                    DisplayName = "AutoCAD",
                    BundleAsset = "GenMate.bundle-v{version}.zip",
                    ManifestAsset = "GenMate.bundle-v{version}.manifest.json",
                    SignatureAsset = "GenMate.bundle-v{version}.manifest.p7s",
                    MinimumVersion = null
                }
            }
        }
    };
}

public sealed class InstallerChannel
{
    public required string Repo { get; init; }

    /// <summary>Glob matched against the release's asset names to find the installer executable.</summary>
    public required string AssetPattern { get; init; }

    // The hard floor of iuv1 §2.5 - the version below which an installer must update before it can
    // continue. Nothing gates on it yet; that dialog is not built. It is read and logged so a
    // below-floor build is visible in diagnostics the day the floor is first set.
    public Version? MinimumVersion { get; init; }
}

public sealed class PluginChannel
{
    public required string Repo { get; init; }

    /// <summary>
    /// Keyed by CAD host id. Hosts this build has no code for are dropped at parse time, so a
    /// future host added to the document is invisible to older installers rather than breaking
    /// them - a host can never be introduced by a file edit alone, because registering a plugin
    /// with a CAD application is code.
    /// </summary>
    public required IReadOnlyDictionary<string, HostChannel> Hosts { get; init; }
}

public sealed class HostChannel
{
    public required string DisplayName { get; init; }

    // Anchored "{version}" templates rather than the name prefixes this installer used before:
    // "GenMate.bundle-" happens not to match "GenMate.bricscad.bundle-..." only by luck of naming,
    // and a second host makes that luck load-bearing.
    public required string BundleAsset { get; init; }
    public required string ManifestAsset { get; init; }
    public required string SignatureAsset { get; init; }

    /// <summary>An editorial floor, raised for reasons unrelated to whether a release verifies.</summary>
    public Version? MinimumVersion { get; init; }

    public string ResolveBundleAsset(string version) => Resolve(BundleAsset, version);

    public string ResolveManifestAsset(string version) => Resolve(ManifestAsset, version);

    public string ResolveSignatureAsset(string version) => Resolve(SignatureAsset, version);

    private static string Resolve(string template, string version) =>
        template.Replace("{version}", version, StringComparison.Ordinal);
}

public static class CadHosts
{
    public const string AutoCad = "autocad";

    /// <summary>The hosts this build can actually detect, install into and register with.</summary>
    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(StringComparer.Ordinal) { AutoCad };
}
