using GenMate.PluginInstaller.Core.Channel;

namespace GenMate.PluginInstaller.Core.Hosts;

public static class BundleAsset
{
    /// <summary>The name of the release asset that installs GenMate for a host, or null if the release has none.</summary>
    public static string? Find(IEnumerable<string> assetNames, string hostId, HostChannel host, string version)
    {
        var names = assetNames.ToList();
        var expected = host.ResolveBundleAsset(version);

        var exact = names.FirstOrDefault(n => n.Equals(expected, StringComparison.OrdinalIgnoreCase));
        if (exact is not null || hostId != CadHosts.AutoCad)
            return exact;

        // The legacy prefix is matched so that an AutoCAD release published before the channel
        // document existed still offers a download. AutoCAD only: for any other host the prefix finds
        // the AutoCAD zip, and would install it from a release that carries none for that host.
        return names.FirstOrDefault(n =>
            n.StartsWith("GenMate.bundle-", StringComparison.Ordinal) &&
            n.EndsWith(".zip", StringComparison.Ordinal));
    }
}
