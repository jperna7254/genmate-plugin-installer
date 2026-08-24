using System.Reflection;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.SelfUpdate;

namespace GenMate.PluginInstaller.Tests;

/// <summary>
/// channel.json may relax presentation, never verification. It is an unsigned file fetched over
/// HTTPS, so anyone able to substitute a release asset can probably substitute it too; what makes
/// that acceptable is that nothing it says can make the installer trust something. These tests
/// exist because the reversal is one line long - "let channel.json turn verification off for QA".
/// </summary>
public class ChannelInvariantTests
{
    private static readonly Dictionary<Type, string[]> PermittedProperties = new()
    {
        [typeof(ChannelDocument)] = ["Installer", "Plugin"],
        [typeof(InstallerChannel)] = ["Repo", "AssetPattern", "MinimumVersion"],
        [typeof(PluginChannel)] = ["Repo", "Hosts"],
        [typeof(HostChannel)] = ["DisplayName", "BundleAsset", "ManifestAsset", "SignatureAsset", "MinimumVersion"]
    };

    [Fact]
    public void The_document_carries_names_and_floors_and_nothing_else()
    {
        foreach (var (type, permitted) in PermittedProperties)
        {
            var declared = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name)
                .Order()
                .ToArray();

            Assert.Equal(permitted.Order().ToArray(), declared);
        }
    }

    [Fact]
    public void No_field_of_the_document_is_a_switch()
    {
        // Every legitimate field names something or floors a version. A boolean is the shape a
        // verification bypass would arrive in, so the type system is where it gets stopped.
        foreach (var type in PermittedProperties.Keys)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            Assert.DoesNotContain(
                properties,
                p => p.PropertyType == typeof(bool) || p.PropertyType == typeof(bool?));
        }
    }

    [Fact]
    public void Fields_a_hostile_document_might_add_to_relax_verification_are_dropped()
    {
        const string hostile = """
            {
              "schema": 2,
              "verifySignatures": false,
              "installer": {
                "repo": "attacker/installer",
                "assetPattern": "*.exe",
                "requireSignature": false,
                "trustedThumbprint": "6CF8348E"
              },
              "plugin": {
                "repo": "attacker/releases",
                "hosts": {
                  "autocad": {
                    "displayName": "AutoCAD",
                    "bundleAsset":    "hostile-v{version}.zip",
                    "manifestAsset":  "hostile-v{version}.manifest.json",
                    "signatureAsset": "hostile-v{version}.manifest.p7s",
                    "allowUnsigned": true
                  }
                }
              }
            }
            """;

        Assert.True(ChannelDocumentParser.TryParse(hostile, out var document, out _));

        // The names moved, which is all the document is allowed to do; the fields that tried to
        // relax verification reached no property at all, because there is none for them to reach.
        Assert.Equal("attacker/installer", document.Installer.Repo);
        Assert.Equal("hostile-v3.1.0.zip", document.Plugin.Hosts[CadHosts.AutoCad].ResolveBundleAsset("3.1.0"));
    }

    [Fact]
    public async Task A_repointed_installer_asset_is_still_refused_by_the_compiled_in_verifier()
    {
        using var environment = new FakeUpdateEnvironment();
        var releases = new FakeReleaseSource
        {
            Latest = new InstallerRelease(new Version(9, 9, 9), new Uri("https://attacker.invalid/evil.exe")),
            DownloadedImage = "hostile-installer"
        };
        var log = new RecordingLog();
        var hostileChannel = new InstallerChannel
        {
            Repo = "attacker/installer",
            AssetPattern = "*.exe",
            MinimumVersion = null
        };

        var service = new SelfUpdateService(
            new Version(1, 0, 2), releases, new RejectingVerifier(), environment, log);

        var outcome = await service.TryUpdateAsync(hostileChannel);

        Assert.Equal(SelfUpdateOutcome.NotApplied, outcome);
        Assert.Equal(FakeUpdateEnvironment.CurrentImage, environment.ReadImage(environment.CurrentExecutablePath));
        Assert.Empty(environment.Launched);
    }
}
