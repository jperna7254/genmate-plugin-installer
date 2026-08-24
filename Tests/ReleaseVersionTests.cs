using GenMate.PluginInstaller.Core.SelfUpdate;

namespace GenMate.PluginInstaller.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.0.3", "1.0.3.0")]
    [InlineData("1.0.3", "1.0.3.0")]
    [InlineData("v2.1", "2.1.0.0")]
    [InlineData("v1.0.3.4", "1.0.3.4")]
    public void A_release_tag_reads_as_a_four_part_version(string tag, string expected)
    {
        Assert.True(ReleaseVersion.TryParseTag(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData("v1.0.3-rc1")]
    [InlineData("latest")]
    [InlineData("v")]
    [InlineData("")]
    [InlineData(null)]
    public void A_tag_this_build_cannot_order_is_refused(string? tag)
    {
        Assert.False(ReleaseVersion.TryParseTag(tag, out _));
    }

    [Fact]
    public void A_three_part_version_does_not_read_as_older_than_its_four_part_self()
    {
        // Version leaves unspecified components at -1, so 1.0.2 sorts below 1.0.2.0 - which would
        // make an assembly version of 1.0.2.0 see the tag it was built from as an update, forever.
        Assert.True(new Version("1.0.2") < new Version("1.0.2.0"));
        Assert.Equal(ReleaseVersion.Normalize(new Version("1.0.2")), new Version("1.0.2.0"));
    }
}
