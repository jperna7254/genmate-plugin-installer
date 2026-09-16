using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Tests;

public class BundleAssetTests
{
    private static readonly HostChannel AutoCad = new()
    {
        DisplayName = "AutoCAD",
        BundleAsset = "GenMate.bundle-v{version}.zip",
        ManifestAsset = "GenMate.bundle-v{version}.manifest.json",
        SignatureAsset = "GenMate.bundle-v{version}.manifest.p7s"
    };

    private static readonly HostChannel BricsCad = new()
    {
        DisplayName = "BricsCAD V24",
        BundleAsset = "GenMate.bricscad.bundle-v{version}.zip",
        ManifestAsset = "GenMate.bricscad.bundle-v{version}.manifest.json",
        SignatureAsset = "GenMate.bricscad.bundle-v{version}.manifest.p7s"
    };

    [Fact]
    public void Each_host_picks_its_own_zip_from_a_release_carrying_both()
    {
        string[] assets = ["GenMate.bundle-v5.2.0.zip", "GenMate.bricscad.bundle-v5.2.0.zip"];

        Assert.Equal("GenMate.bundle-v5.2.0.zip", BundleAsset.Find(assets, CadHosts.AutoCad, AutoCad, "5.2.0"));
        Assert.Equal("GenMate.bricscad.bundle-v5.2.0.zip", BundleAsset.Find(assets, CadHosts.BricsCad, BricsCad, "5.2.0"));
    }

    [Fact]
    public void A_release_with_no_BricsCAD_zip_offers_BricsCAD_nothing_rather_than_the_AutoCAD_zip()
    {
        string[] assets = ["GenMate.bundle-v5.1.0.zip"];

        Assert.Null(BundleAsset.Find(assets, CadHosts.BricsCad, BricsCad, "5.1.0"));
    }

    [Fact]
    public void An_AutoCAD_release_published_under_an_older_name_still_offers_its_zip()
    {
        string[] assets = ["GenMate.bundle-2.2.0.zip"];

        Assert.Equal("GenMate.bundle-2.2.0.zip", BundleAsset.Find(assets, CadHosts.AutoCad, AutoCad, "2.2.0"));
    }
}
