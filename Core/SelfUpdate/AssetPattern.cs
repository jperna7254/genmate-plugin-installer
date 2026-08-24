using System.Text.RegularExpressions;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>Matches a release asset name against a channel document's glob.</summary>
public static class AssetPattern
{
    public static bool Matches(string pattern, string assetName)
    {
        var regex = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";
        return Regex.IsMatch(assetName, regex, RegexOptions.IgnoreCase);
    }
}
