using System.Text.RegularExpressions;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

public static partial class ReleaseVersion
{
    /// <summary>
    /// Reads a "v1.2.3" release tag. A tag carrying a prerelease or build suffix is refused rather
    /// than truncated: this build cannot order "1.2.3-rc1" against "1.2.3", and guessing wrong here
    /// means shipping an unfinished installer to every customer.
    /// </summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(tag))
            return false;

        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];

        if (!TagPattern().IsMatch(trimmed) || !Version.TryParse(trimmed, out var parsed))
            return false;

        version = Normalize(parsed);
        return true;
    }

    /// <summary>
    /// Fills in the components a version left unspecified. Version treats them as -1, which orders
    /// 1.0.3 *below* 1.0.3.0 - so an assembly version compared against a three-part release tag
    /// reports an update that does not exist, on every launch, forever.
    /// </summary>
    public static Version Normalize(Version version) => new(
        version.Major,
        version.Minor,
        Math.Max(version.Build, 0),
        Math.Max(version.Revision, 0));

    /// <summary>
    /// Whether a version clears a channel document's floor. Both sides are normalized, because a
    /// floor is written by hand in the document and a version comes from a tag: comparing a
    /// four-part "3.0.0.0" floor against a three-part "3.0.0" tag without normalizing drops the
    /// matching release with no log and no trace in the UI.
    /// </summary>
    public static bool IsAtOrAboveFloor(Version version, Version? floor) =>
        floor is null || Normalize(version) >= Normalize(floor);

    /// <summary>
    /// As <see cref="IsAtOrAboveFloor(Version, Version?)"/>, for a version still in the string form
    /// a release tag carries. A version that will not parse is below every floor rather than above
    /// it: an unreadable version cannot be shown to clear one.
    /// </summary>
    public static bool IsAtOrAboveFloor(string? version, Version? floor) =>
        floor is null || (Version.TryParse(version, out var parsed) && IsAtOrAboveFloor(parsed, floor));

    [GeneratedRegex(@"^\d+(\.\d+){1,3}$")]
    private static partial Regex TagPattern();
}
