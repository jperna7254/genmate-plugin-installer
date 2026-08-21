using System.IO;
using System.Xml.Linq;

namespace GenMate.PluginInstaller.Services;

public class PluginDetectionService : IPluginDetectionService
{
    private const string BundlePath =
        @"C:\ProgramData\Autodesk\ApplicationPlugins\GenMate.bundle\PackageContents.xml";

    public string? GetInstalledVersion()
    {
        try
        {
            if (!File.Exists(BundlePath))
                return null;

            var doc = XDocument.Load(BundlePath);
            // AppVersion on the PackageContents.xml root is fixed by the cross-repo contract on GitHubReleaseService.
            return doc.Root?.Attribute("AppVersion")?.Value;
        }
        catch
        {
            return null;
        }
    }
}
