using System.Diagnostics;
using System.IO;
using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Hosts;
using Microsoft.Win32;

namespace GenMate.PluginInstaller.Services;

public class WindowsHostMachine : IHostMachine
{
    public bool IsApplicationInstalled(PluginHost host)
    {
        try
        {
            return host.Id switch
            {
                CadHosts.AutoCad => IsAutoCad2024Installed(),
                CadHosts.BricsCad => IsBricsCadV24Installed(),
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }

    public bool IsApplicationRunning(PluginHost host) =>
        Process.GetProcessesByName(host.ProcessName).Length > 0;

    public void RegisterDemandLoad(DemandLoadRegistration registration, string loaderPath)
    {
        using var key = OpenLocalMachine().CreateSubKey(registration.KeyPath, writable: true);
        key.SetValue("DESCRIPTION", registration.Description, RegistryValueKind.String);
        key.SetValue("LOADER", loaderPath, RegistryValueKind.String);
        // 2 = load when the application starts: the plugin hooks document events at load.
        key.SetValue("LOADCTRLS", 2, RegistryValueKind.DWord);
        key.SetValue("MANAGED", 1, RegistryValueKind.DWord);
    }

    public void UnregisterDemandLoad(DemandLoadRegistration registration) =>
        OpenLocalMachine().DeleteSubKeyTree(registration.KeyPath, throwOnMissingSubKey: false);

    // The 64-bit view explicitly: both applications are 64-bit and read it, whatever this process is.
    private static RegistryKey OpenLocalMachine() =>
        RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);

    // R24.3 is AutoCAD 2024, the release the AutoCAD adapter is built against; each installed
    // product (AutoCAD, or a toolset built on it) adds an ACAD-* key beneath it.
    private static bool IsAutoCad2024Installed()
    {
        using var release = OpenLocalMachine().OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD\R24.3");
        return release?.GetSubKeyNames().Any(n => n.StartsWith("ACAD-", StringComparison.OrdinalIgnoreCase)) == true;
    }

    // Checked down to the executable, because uninstalling BricsCAD can leave its version key behind.
    private static bool IsBricsCadV24Installed()
    {
        using var version = OpenLocalMachine().OpenSubKey(@"SOFTWARE\Bricsys\BricsCAD\V24x64");
        if (version?.GetValue("CURVER") is not string language)
            return false;

        using var install = version.OpenSubKey(language);
        return install?.GetValue("InstallDir") is string directory &&
               File.Exists(Path.Combine(directory, "bricscad.exe"));
    }
}
