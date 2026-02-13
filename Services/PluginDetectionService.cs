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
            return doc.Root?.Attribute("AppVersion")?.Value;
        }
        catch
        {
            return null;
        }
    }
}
