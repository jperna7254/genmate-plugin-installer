using System.IO.Compression;
using System.Xml.Linq;

namespace GenMate.PluginInstaller.Core.Hosts;

public sealed class PluginHostService
{
    private readonly IReadOnlyList<PluginHost> _hosts;
    private readonly string _sharedUserDataDirectory;
    private readonly IHostMachine _machine;

    /// <param name="hosts">Every host GenMate can be installed for, so that removing one can see the others.</param>
    /// <param name="sharedUserDataDirectory">
    /// %LOCALAPPDATA%\GenMate - the plugin's credentials, settings and caches. Every host's plugin
    /// reads the same folder, which is why it is only cleared when no other host still has GenMate.
    /// </param>
    public PluginHostService(IReadOnlyList<PluginHost> hosts, string sharedUserDataDirectory, IHostMachine machine)
    {
        _hosts = hosts;
        _sharedUserDataDirectory = sharedUserDataDirectory;
        _machine = machine;
    }

    public HostStatus GetStatus(PluginHost host) =>
        new(host, _machine.IsApplicationInstalled(host), GetInstalledVersion(host));

    public string? GetInstalledVersion(PluginHost host)
    {
        try
        {
            if (!File.Exists(host.ManifestPath))
                return null;

            return XDocument.Load(host.ManifestPath).Root?.Attribute("AppVersion")?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <exception cref="InvalidDataException">
    /// The zip is not a bundle for this host. The existing install is left exactly as it was.
    /// </exception>
    public void InstallFromZip(PluginHost host, string zipPath)
    {
        // Staged beside the bundle rather than in %TEMP%, so the final step is a rename on one volume.
        // The name carries no ".bundle" suffix, so AutoCAD does not load a half-extracted stage.
        var staging = Path.Combine(host.PluginsDirectory, ".GenMate-staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(zipPath, staging);

            var stagedBundle = Path.Combine(staging, host.BundleFolderName);
            if (!File.Exists(Path.Combine(stagedBundle, PluginHost.ManifestFileName)))
                throw new InvalidDataException(
                    $"The downloaded package is not a GenMate package for {host.ApplicationName}: it has no {host.BundleFolderName} folder.");

            if (host.DemandLoad is { } demandLoad && !File.Exists(demandLoad.ResolveAssembly(stagedBundle)))
                throw new InvalidDataException(
                    $"The downloaded package for {host.ApplicationName} is incomplete: it has no {demandLoad.AssemblyPath[^1]}.");

            // Registered before the old bundle is touched, because the entry names the final path
            // rather than the files: a refused registry write then leaves the existing install whole,
            // instead of a bundle that shows as installed but that BricsCAD never loads.
            if (host.DemandLoad is { } registration)
                _machine.RegisterDemandLoad(registration, registration.ResolveAssembly(host.BundlePath));

            if (Directory.Exists(host.BundlePath))
                Directory.Delete(host.BundlePath, true);

            ClearSharedUserDataUnlessInUseBesides(host);

            Directory.Move(stagedBundle, host.BundlePath);
        }
        finally
        {
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
    }

    public void Uninstall(PluginHost host)
    {
        // Registration first: a leftover bundle nobody loads is inert, but an entry pointing at a
        // half-deleted bundle is an error every time the application starts.
        if (host.DemandLoad is { } registration)
            _machine.UnregisterDemandLoad(registration);

        if (Directory.Exists(host.BundlePath))
            Directory.Delete(host.BundlePath, true);

        ClearSharedUserDataUnlessInUseBesides(host);
    }

    private void ClearSharedUserDataUnlessInUseBesides(PluginHost host)
    {
        if (_hosts.Any(other => other.Id != host.Id && Directory.Exists(other.BundlePath)))
            return;

        if (Directory.Exists(_sharedUserDataDirectory))
            Directory.Delete(_sharedUserDataDirectory, true);
    }
}
