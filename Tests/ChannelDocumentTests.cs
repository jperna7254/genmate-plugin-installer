using GenMate.PluginInstaller.Core.Channel;

namespace GenMate.PluginInstaller.Tests;

public class ChannelDocumentTests
{
    private const string TwoHostDocument = """
        {
          "schema": 2,
          "installer": {
            "repo": "jperna7254/genmate-plugin-installer",
            "assetPattern": "GenMate.PluginInstaller*.exe",
            "minimumVersion": "1.1.0"
          },
          "plugin": {
            "repo": "jperna7254/genmate-plugin-releases",
            "hosts": {
              "autocad": {
                "displayName": "AutoCAD",
                "bundleAsset":    "GenMate.bundle-v{version}.zip",
                "manifestAsset":  "GenMate.bundle-v{version}.manifest.json",
                "signatureAsset": "GenMate.bundle-v{version}.manifest.p7s",
                "minimumVersion": null
              },
              "bricscad": {
                "displayName": "BricsCAD V24",
                "bundleAsset":    "GenMate.bricscad.bundle-v{version}.zip",
                "manifestAsset":  "GenMate.bricscad.bundle-v{version}.manifest.json",
                "signatureAsset": "GenMate.bricscad.bundle-v{version}.manifest.p7s",
                "minimumVersion": null
              }
            }
          }
        }
        """;

    [Fact]
    public void A_schema_2_document_is_read()
    {
        Assert.True(ChannelDocumentParser.TryParse(TwoHostDocument, out var document, out var failure));

        Assert.Null(failure);
        Assert.Equal("jperna7254/genmate-plugin-installer", document.Installer.Repo);
        Assert.Equal("GenMate.PluginInstaller*.exe", document.Installer.AssetPattern);
        Assert.Equal(new Version(1, 1, 0), document.Installer.MinimumVersion);
        Assert.Equal("jperna7254/genmate-plugin-releases", document.Plugin.Repo);
    }

    [Fact]
    public void A_host_this_build_has_no_code_for_is_ignored_rather_than_offered()
    {
        ChannelDocumentParser.TryParse(TwoHostDocument, out var document, out _);

        Assert.Equal([CadHosts.AutoCad], document.Plugin.Hosts.Keys.Order().ToArray());
    }

    [Fact]
    public void Asset_names_are_anchored_templates_rather_than_prefixes()
    {
        ChannelDocumentParser.TryParse(TwoHostDocument, out var document, out _);
        var host = document.Plugin.Hosts[CadHosts.AutoCad];

        Assert.Equal("GenMate.bundle-v3.1.0.zip", host.ResolveBundleAsset("3.1.0"));
        Assert.Equal("GenMate.bundle-v3.1.0.manifest.json", host.ResolveManifestAsset("3.1.0"));
        Assert.Equal("GenMate.bundle-v3.1.0.manifest.p7s", host.ResolveSignatureAsset("3.1.0"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void A_document_declaring_another_schema_is_refused_whole(int schema)
    {
        var json = TwoHostDocument.Replace("\"schema\": 2", $"\"schema\": {schema}");

        Assert.False(ChannelDocumentParser.TryParse(json, out var document, out var failure));
        Assert.Contains("schema", failure);
        Assert.Same(ChannelDocument.Fallback, document);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{}")]
    [InlineData("""{"schema": 2, "installer": {"repo": "a/b"}, "plugin": {"repo": "c/d", "hosts": {}}}""")]
    [InlineData("""{"schema": 2, "installer": {"repo": "a/b", "assetPattern": "*.exe"}, "plugin": {"repo": "c/d", "hosts": {"autocad": {"displayName": "AutoCAD"}}}}""")]
    public void An_unusable_document_falls_back_to_the_layout_this_build_shipped_with(string json)
    {
        Assert.False(ChannelDocumentParser.TryParse(json, out var document, out var failure));

        Assert.NotNull(failure);
        Assert.Same(ChannelDocument.Fallback, document);
    }

    [Fact]
    public void An_unparseable_version_floor_is_refused_rather_than_ignored()
    {
        var json = TwoHostDocument.Replace("\"minimumVersion\": \"1.1.0\"", "\"minimumVersion\": \"latest\"");

        Assert.False(ChannelDocumentParser.TryParse(json, out _, out var failure));
        Assert.Contains("minimumVersion", failure);
    }

    [Fact]
    public void The_shipped_fallback_describes_the_layout_the_installer_publishes_today()
    {
        var fallback = ChannelDocument.Fallback;

        Assert.Equal("jperna7254/genmate-plugin-installer", fallback.Installer.Repo);
        Assert.Equal("GenMate.bundle-v3.0.0.zip",
            fallback.Plugin.Hosts[CadHosts.AutoCad].ResolveBundleAsset("3.0.0"));
    }
}
