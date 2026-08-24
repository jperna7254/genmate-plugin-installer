using GenMate.PluginInstaller.Core.Channel;
using GenMate.PluginInstaller.Core.Diagnostics;
using GenMate.PluginInstaller.Core.SelfUpdate;

namespace GenMate.PluginInstaller.Tests;

/// <summary>
/// A real temporary directory behind the update's file operations, so the happy path exercises
/// genuine renames, with hooks to provoke the failures Windows only produces under conditions a
/// test cannot arrange.
/// </summary>
internal sealed class FakeUpdateEnvironment : IUpdateEnvironment, IDisposable
{
    public const string CurrentImage = "current-installer";

    public FakeUpdateEnvironment(string executableName = "GenMate.PluginInstaller.exe")
    {
        Root = Path.Combine(Path.GetTempPath(), "gm-selfupdate-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Root);
        CurrentExecutablePath = Path.Combine(Root, executableName);
        File.WriteAllText(CurrentExecutablePath, CurrentImage);
    }

    public string Root { get; }

    public string CurrentExecutablePath { get; }

    public Func<string, string, bool>? FailMoveWhen { get; set; }

    public Func<string, bool>? FailDeleteWhen { get; set; }

    public bool FailLaunch { get; set; }

    public List<string> Launched { get; } = [];

    public bool FileExists(string path) => File.Exists(path);

    public void MoveFile(string sourcePath, string destinationPath)
    {
        if (FailMoveWhen?.Invoke(sourcePath, destinationPath) == true)
            throw new IOException($"refused to move {Path.GetFileName(sourcePath)}");

        File.Move(sourcePath, destinationPath);
    }

    public void DeleteFile(string path)
    {
        if (FailDeleteWhen?.Invoke(path) == true)
            throw new UnauthorizedAccessException($"refused to delete {Path.GetFileName(path)}");

        File.Delete(path);
    }

    public IReadOnlyList<string> ListFiles(string directory, string searchPattern) =>
        System.IO.Directory.Exists(directory) ? System.IO.Directory.GetFiles(directory, searchPattern) : [];

    public void Launch(string executablePath)
    {
        if (FailLaunch)
            throw new InvalidOperationException("refused to launch");

        Launched.Add(executablePath);
    }

    public string WithSuffix(string suffix) => CurrentExecutablePath + suffix;

    public string ReadImage(string path) => File.ReadAllText(path);

    public string[] FileNames() => System.IO.Directory
        .GetFiles(Root)
        .Select(Path.GetFileName)
        .OfType<string>()
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal sealed class FakeReleaseSource : IInstallerReleaseSource
{
    public InstallerRelease? Latest { get; set; }

    public Exception? GetLatestThrows { get; set; }

    public Exception? DownloadThrows { get; set; }

    public string DownloadedImage { get; set; } = "updated-installer";

    public int GetLatestCalls { get; private set; }

    public int DownloadCalls { get; private set; }

    public Task<InstallerRelease?> GetLatestAsync(InstallerChannel channel, CancellationToken ct)
    {
        GetLatestCalls++;
        return GetLatestThrows is not null
            ? Task.FromException<InstallerRelease?>(GetLatestThrows)
            : Task.FromResult(Latest);
    }

    public async Task DownloadAsync(
        InstallerRelease release, string destinationPath, IProgress<int>? progress, CancellationToken ct)
    {
        DownloadCalls++;
        await File.WriteAllTextAsync(destinationPath, DownloadedImage, ct);
        progress?.Report(100);

        if (DownloadThrows is not null)
            throw DownloadThrows;
    }
}

internal sealed class RecordingLog : IUpdateLog
{
    public List<string> Lines { get; } = [];

    public void Write(string message) => Lines.Add(message);

    public void Write(string message, Exception exception) => Lines.Add($"{message}: {exception.Message}");

    public bool Mentions(string fragment) =>
        Lines.Any(line => line.Contains(fragment, StringComparison.OrdinalIgnoreCase));
}

internal sealed class RejectingVerifier : IInstallerVerifier
{
    public bool IsTrusted(string installerPath, out string? reason)
    {
        reason = "signature did not match the pinned identity";
        return false;
    }
}

internal sealed class ThrowingVerifier : IInstallerVerifier
{
    public bool IsTrusted(string installerPath, out string? reason) =>
        throw new InvalidOperationException("verifier exploded");
}
