using GenMate.PluginInstaller.Core.Channel;

namespace GenMate.PluginInstaller.Core.Hosts;

/// <summary>
/// Where GenMate lives for one CAD application, and how that application is told to load it.
/// </summary>
/// <remarks>
/// Cross-repo contract with genmate-plugin's release workflow, invisible from that repo: each host's
/// zip has <see cref="BundleFolderName"/> as its single root folder, holding a
/// <see cref="ManifestFileName"/> whose AppVersion is the installed version; the BricsCAD bundle also carries
/// <c>Contents\GenMate.Plugin.Brics24.dll</c>, the assembly its DemandLoad entry points at.
/// </remarks>
public sealed class PluginHost
{
    public const string ManifestFileName = "PackageContents.xml";

    public required string Id { get; init; }

    /// <summary>The application and release this host's adapter targets, for when the channel document does not name the host.</summary>
    public required string ApplicationName { get; init; }

    public required string PluginsDirectory { get; init; }

    public required string BundleFolderName { get; init; }

    /// <summary>The application's process name, whose running copy holds the bundle's files open.</summary>
    public required string ProcessName { get; init; }

    /// <summary>Null for a host that loads the bundle from <see cref="PluginsDirectory"/> by itself.</summary>
    public DemandLoadRegistration? DemandLoad { get; init; }

    public string BundlePath => Path.Combine(PluginsDirectory, BundleFolderName);

    public string ManifestPath => Path.Combine(BundlePath, ManifestFileName);

    public static PluginHost AutoCad(string programData) => new()
    {
        Id = CadHosts.AutoCad,
        ApplicationName = "AutoCAD 2024",
        PluginsDirectory = Path.Combine(programData, "Autodesk", "ApplicationPlugins"),
        BundleFolderName = "GenMate.bundle",
        ProcessName = "acad"
    };

    // BricsCAD V24 ignores PackageContents.xml bundles entirely - Bricsys: the ApplicationPlugins
    // mechanism "is not yet supported by BricsCAD" - so the folder is only where the files are kept,
    // and the registry entry is what makes BricsCAD load them.
    public static PluginHost BricsCad(string programData) => new()
    {
        Id = CadHosts.BricsCad,
        ApplicationName = "BricsCAD V24",
        PluginsDirectory = Path.Combine(programData, "Bricsys", "ApplicationPlugins"),
        BundleFolderName = "GenMate.bricscad.bundle",
        ProcessName = "bricscad",
        DemandLoad = new DemandLoadRegistration
        {
            KeyPath = @"SOFTWARE\Bricsys\ObjectDRX\V24x64\Applications\GenMate",
            AssemblyPath = ["Contents", "GenMate.Plugin.Brics24.dll"],
            Description = "GenMate Plugin for BricsCAD"
        }
    };
}

/// <summary>A registry DemandLoad entry under HKEY_LOCAL_MACHINE that loads a managed assembly at startup.</summary>
public sealed class DemandLoadRegistration
{
    public required string KeyPath { get; init; }

    /// <summary>The loaded assembly's path segments, relative to the bundle folder.</summary>
    public required IReadOnlyList<string> AssemblyPath { get; init; }

    public required string Description { get; init; }

    public string ResolveAssembly(string bundlePath) => Path.Combine([bundlePath, .. AssemblyPath]);
}
