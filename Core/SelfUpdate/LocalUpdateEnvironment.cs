using System.Diagnostics;

namespace GenMate.PluginInstaller.Core.SelfUpdate;

public sealed class LocalUpdateEnvironment : IUpdateEnvironment
{
    private readonly string _lastAppliedTargetPath;

    public LocalUpdateEnvironment(string? currentExecutablePath = null, string? lastAppliedTargetPath = null)
    {
        _lastAppliedTargetPath = lastAppliedTargetPath
            ?? Path.Combine(InstallerDataDirectory.Path, "last-applied-update.txt");

        // Environment.ProcessPath, not Assembly.Location: this app ships as a single file, where
        // Location is an empty string rather than the path of the exe the user double-clicked.
        CurrentExecutablePath = currentExecutablePath
            ?? Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the running executable's path.");
    }

    public string CurrentExecutablePath { get; }

    public bool FileExists(string path) => File.Exists(path);

    public void MoveFile(string sourcePath, string destinationPath) => File.Move(sourcePath, destinationPath);

    public void DeleteFile(string path) => File.Delete(path);

    public IReadOnlyList<string> ListFiles(string directory, string searchPattern) =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, searchPattern) : [];

    public Version? ReadLastAppliedTarget() =>
        File.Exists(_lastAppliedTargetPath) && Version.TryParse(File.ReadAllText(_lastAppliedTargetPath).Trim(), out var version)
            ? version
            : null;

    public void WriteLastAppliedTarget(Version version)
    {
        var directory = Path.GetDirectoryName(_lastAppliedTargetPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(_lastAppliedTargetPath, version.ToString());
    }

    public void Launch(string executablePath)
    {
        // UseShellExecute so the child gets its own console-free process and inherits this
        // process's elevation without a second UAC prompt.
        Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty
        });
    }
}
