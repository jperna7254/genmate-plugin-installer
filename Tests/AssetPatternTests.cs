using GenMate.PluginInstaller.Core.SelfUpdate;

namespace GenMate.PluginInstaller.Tests;

public class AssetPatternTests
{
    [Theory]
    [InlineData("GenMate.PluginInstaller*.exe", "GenMate.PluginInstaller.exe", true)]
    [InlineData("GenMate.PluginInstaller*.exe", "GenMate.PluginInstaller-v1.1.0.exe", true)]
    [InlineData("GenMate.PluginInstaller*.exe", "GenMate.PluginInstaller.exe.sig", false)]
    [InlineData("GenMate.PluginInstaller*.exe", "SomethingElse.exe", false)]
    [InlineData("GenMate.bundle-v3.0.0.zip", "GenMate.bricscad.bundle-v3.0.0.zip", false)]
    public void A_pattern_matches_only_what_it_anchors(string pattern, string assetName, bool expected)
    {
        Assert.Equal(expected, AssetPattern.Matches(pattern, assetName));
    }

    [Fact]
    public void Regex_metacharacters_in_a_pattern_are_matched_literally()
    {
        Assert.False(AssetPattern.Matches("GenMate.exe", "GenMateXexe"));
    }
}
