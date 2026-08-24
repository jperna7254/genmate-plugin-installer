using System.Text.RegularExpressions;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>Matches a release asset name against a channel document's glob.</summary>
public static class AssetPattern
{
    public static bool Matches(string pattern, string assetName)
    {
        var regex = "^" + string.Join(".*", pattern.Split('*').Select(Regex.Escape)) + "$";

        // NonBacktracking because the pattern is a field-editable value from channel.json and this
        // runs on the launch path: a clumsy multi-wildcard glob against a non-matching asset name
        // backtracks super-linearly, and a synchronous match cannot be cancelled by the check
        // timeout, so the launch window would simply never open.
        return Regex.IsMatch(assetName, regex, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
    }
}
