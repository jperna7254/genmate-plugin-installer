namespace GenMate.PluginInstaller.Core;

/// <summary>
/// Where the installer keeps the little that has to outlive its own replacement - the update log
/// and the record of what the last update aimed at.
/// </summary>
/// <remarks>
/// Deliberately %LOCALAPPDATA%\GenMate.PluginInstaller and not the %LOCALAPPDATA%\GenMate that
/// <c>PluginInstallService</c> deletes wholesale on every install and uninstall: state that
/// disappears when the user installs a plugin cannot explain a failed install, and cannot stop an
/// update from being retried forever.
/// </remarks>
public static class InstallerDataDirectory
{
    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GenMate.PluginInstaller");
}
