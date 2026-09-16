using System.IO.Compression;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Tests;

public class PluginHostServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gm-hosts-" + Guid.NewGuid().ToString("N"));
    private readonly FakeHostMachine _machine = new();
    private readonly PluginHost _autoCad;
    private readonly PluginHost _bricsCad;
    private readonly string _sharedUserData;
    private readonly PluginHostService _service;

    public PluginHostServiceTests()
    {
        var programData = Path.Combine(_root, "ProgramData");
        _autoCad = PluginHost.AutoCad(programData);
        _bricsCad = PluginHost.BricsCad(programData);
        _sharedUserData = Path.Combine(_root, "LocalAppData", "GenMate");
        _service = new PluginHostService([_autoCad, _bricsCad], _sharedUserData, _machine);
    }

    [Fact]
    public void Installing_for_BricsCAD_places_the_bundle_in_the_Bricsys_plugin_folder_and_registers_the_installed_dll()
    {
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.2.0"));

        var dll = Path.Combine(_root, "ProgramData", "Bricsys", "ApplicationPlugins",
            "GenMate.bricscad.bundle", "Contents", "GenMate.Plugin.Brics24.dll");
        Assert.True(File.Exists(dll));

        var entry = Assert.Single(_machine.DemandLoads);
        Assert.Equal(@"SOFTWARE\Bricsys\ObjectDRX\V24x64\Applications\GenMate", entry.Key);
        Assert.Equal(dll, entry.Value);
    }

    [Fact]
    public void Installing_for_AutoCAD_registers_nothing()
    {
        _service.InstallFromZip(_autoCad, AutoCadZip("5.2.0"));

        Assert.Equal("5.2.0", _service.GetInstalledVersion(_autoCad));
        Assert.Empty(_machine.DemandLoads);
    }

    [Fact]
    public void The_installed_version_of_each_host_is_read_from_its_own_bundle()
    {
        _service.InstallFromZip(_autoCad, AutoCadZip("5.1.0"));
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.2.0"));

        Assert.Equal("5.1.0", _service.GetInstalledVersion(_autoCad));
        Assert.Equal("5.2.0", _service.GetInstalledVersion(_bricsCad));
    }

    [Fact]
    public void Nothing_is_installed_until_a_bundle_is()
    {
        Assert.Null(_service.GetInstalledVersion(_bricsCad));
    }

    [Fact]
    public void Installing_a_newer_release_replaces_the_bundle_in_place()
    {
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.1.0", extraFile: "Contents/Removed.dll"));

        _service.InstallFromZip(_bricsCad, BricsCadZip("5.2.0"));

        Assert.Equal("5.2.0", _service.GetInstalledVersion(_bricsCad));
        Assert.False(File.Exists(Path.Combine(_bricsCad.BundlePath, "Contents", "Removed.dll")));
        Assert.Single(_machine.DemandLoads);
    }

    [Fact]
    public void Uninstalling_BricsCAD_removes_its_files_and_registration_and_leaves_AutoCAD_untouched()
    {
        _service.InstallFromZip(_autoCad, AutoCadZip("5.1.0"));
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.1.0"));

        _service.Uninstall(_bricsCad);

        Assert.False(Directory.Exists(_bricsCad.BundlePath));
        Assert.Empty(_machine.DemandLoads);
        Assert.Equal("5.1.0", _service.GetInstalledVersion(_autoCad));
    }

    [Fact]
    public void Uninstalling_AutoCAD_leaves_BricsCAD_untouched()
    {
        _service.InstallFromZip(_autoCad, AutoCadZip("5.1.0"));
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.1.0"));

        _service.Uninstall(_autoCad);

        Assert.False(Directory.Exists(_autoCad.BundlePath));
        Assert.Equal("5.1.0", _service.GetInstalledVersion(_bricsCad));
        Assert.Single(_machine.DemandLoads);
    }

    [Fact]
    public void Uninstalling_a_host_with_nothing_installed_succeeds()
    {
        _service.Uninstall(_bricsCad);

        Assert.Empty(_machine.DemandLoads);
    }

    [Fact]
    public void A_zip_without_the_hosts_bundle_folder_is_refused_and_the_existing_install_is_kept()
    {
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.1.0"));

        var ex = Assert.Throws<InvalidDataException>(() => _service.InstallFromZip(_bricsCad, AutoCadZip("5.2.0")));

        Assert.Contains("GenMate.bricscad.bundle", ex.Message);
        Assert.Equal("5.1.0", _service.GetInstalledVersion(_bricsCad));
        Assert.Equal(["GenMate.bricscad.bundle"], EntriesOf(_bricsCad.PluginsDirectory));
    }

    [Fact]
    public void A_refused_registration_leaves_the_existing_install_whole()
    {
        _service.InstallFromZip(_bricsCad, BricsCadZip("5.1.0"));
        _machine.FailRegister = true;

        Assert.Throws<UnauthorizedAccessException>(() => _service.InstallFromZip(_bricsCad, BricsCadZip("5.2.0")));

        Assert.Equal("5.1.0", _service.GetInstalledVersion(_bricsCad));
        Assert.True(File.Exists(Path.Combine(_bricsCad.BundlePath, "Contents", "GenMate.Plugin.Brics24.dll")));
        Assert.Equal(["GenMate.bricscad.bundle"], EntriesOf(_bricsCad.PluginsDirectory));
    }

    [Fact]
    public void A_BricsCAD_zip_without_the_adapter_is_refused_before_anything_is_registered()
    {
        var zip = BuildZip("GenMate.bricscad.bundle", "5.2.0", includeBricsAdapter: false);

        var ex = Assert.Throws<InvalidDataException>(() => _service.InstallFromZip(_bricsCad, zip));

        Assert.Contains("GenMate.Plugin.Brics24.dll", ex.Message);
        Assert.Null(_service.GetInstalledVersion(_bricsCad));
        Assert.Empty(_machine.DemandLoads);
    }

    [Fact]
    public void Shared_user_data_is_cleared_when_no_other_host_has_GenMate_installed()
    {
        WriteSharedUserData();

        _service.InstallFromZip(_bricsCad, BricsCadZip("5.2.0"));

        Assert.False(Directory.Exists(_sharedUserData));
    }

    [Fact]
    public void Shared_user_data_is_kept_while_another_host_still_has_GenMate_installed()
    {
        _service.InstallFromZip(_autoCad, AutoCadZip("5.1.0"));
        WriteSharedUserData();

        _service.InstallFromZip(_bricsCad, BricsCadZip("5.2.0"));
        _service.Uninstall(_bricsCad);

        Assert.True(Directory.Exists(_sharedUserData));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void A_host_is_offered_when_its_application_is_installed_or_GenMate_already_is(
        bool applicationInstalled, bool pluginInstalled, bool offered)
    {
        _machine.InstalledApplications.Clear();
        if (applicationInstalled)
            _machine.InstalledApplications.Add(CadHosts.BricsCad);
        if (pluginInstalled)
            _service.InstallFromZip(_bricsCad, BricsCadZip("5.1.0"));

        var status = _service.GetStatus(_bricsCad);

        Assert.Equal(applicationInstalled, status.ApplicationInstalled);
        Assert.Equal(pluginInstalled ? "5.1.0" : null, status.InstalledVersion);
        Assert.Equal(offered, status.IsOffered);
    }

    private string AutoCadZip(string version) => BuildZip("GenMate.bundle", version, includeBricsAdapter: false);

    private string BricsCadZip(string version, string? extraFile = null) =>
        BuildZip("GenMate.bricscad.bundle", version, includeBricsAdapter: true, extraFile);

    // The shape the plugin's release workflow publishes: Compress-Archive of the bundle folder, so
    // the folder itself is the zip's single root entry.
    private string BuildZip(string bundleFolder, string version, bool includeBricsAdapter, string? extraFile = null)
    {
        var source = Path.Combine(_root, "src-" + Guid.NewGuid().ToString("N"));
        var bundle = Path.Combine(source, bundleFolder);
        Directory.CreateDirectory(Path.Combine(bundle, "Contents"));
        File.WriteAllText(Path.Combine(bundle, "PackageContents.xml"),
            $"""<?xml version="1.0" encoding="utf-8"?><ApplicationPackage AppVersion="{version}" />""");
        File.WriteAllText(Path.Combine(bundle, "Contents", "GenMate.Plugin.Core.dll"), "core");
        if (includeBricsAdapter)
            File.WriteAllText(Path.Combine(bundle, "Contents", "GenMate.Plugin.Brics24.dll"), "adapter");
        if (extraFile is not null)
            File.WriteAllText(Path.Combine(bundle, extraFile), "extra");

        var zip = Path.Combine(_root, $"{bundleFolder}-v{version}-{Guid.NewGuid():N}.zip");
        ZipFile.CreateFromDirectory(source, zip);
        return zip;
    }

    private void WriteSharedUserData()
    {
        Directory.CreateDirectory(_sharedUserData);
        File.WriteAllText(Path.Combine(_sharedUserData, "settings.json"), "{}");
    }

    private static string[] EntriesOf(string directory) => Directory
        .GetFileSystemEntries(directory)
        .Select(Path.GetFileName)
        .OfType<string>()
        .Order(StringComparer.Ordinal)
        .ToArray();

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
