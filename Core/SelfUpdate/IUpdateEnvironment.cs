namespace GenMate.PluginInstaller.Core.SelfUpdate;

/// <summary>
/// The file and process operations the swap needs, behind an interface so that the rename failures
/// the swap must survive can be provoked in a test - on Windows they only happen under conditions
/// (a locked image, a denied ACL) a test cannot arrange, and on Linux they never happen at all.
/// </summary>
public interface IUpdateEnvironment
{
    string CurrentExecutablePath { get; }

    bool FileExists(string path);

    void MoveFile(string sourcePath, string destinationPath);

    void DeleteFile(string path);

    IReadOnlyList<string> ListFiles(string directory, string searchPattern);

    void Launch(string executablePath);

    /// <summary>
    /// The version the last completed swap put in place, or null if none is recorded. Held outside
    /// the executable's own directory because it has to survive the executable being replaced.
    /// </summary>
    Version? ReadLastAppliedTarget();

    void WriteLastAppliedTarget(Version version);
}
