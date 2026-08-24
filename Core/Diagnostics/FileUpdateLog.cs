using System.Globalization;

namespace GenMate.PluginInstaller.Core.Diagnostics;

public sealed class FileUpdateLog : IUpdateLog
{
    private const long MaxBytes = 256 * 1024;

    private readonly string _path;
    private readonly Lock _gate = new();

    public FileUpdateLog(string path)
    {
        _path = path;
    }

    public static FileUpdateLog Default() =>
        new(Path.Combine(InstallerDataDirectory.Path, "update.log"));

    public void Write(string message) => Append(message);

    public void Write(string message, Exception exception) => Append($"{message}: {exception}");

    // Logging is the last thing that may take the app down: every failure path in the updater
    // reports through here, so a full disk or a locked file must be swallowed rather than raised.
    private void Append(string line)
    {
        try
        {
            lock (_gate)
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                    File.Delete(_path);

                var stamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
                File.AppendAllText(_path, $"[{stamp}] {line}{Environment.NewLine}");
            }
        }
        catch
        {
            // ignored
        }
    }
}
