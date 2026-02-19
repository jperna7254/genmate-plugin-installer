using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace GenMate.PluginInstaller.Services;

public class PluginInstallService : IPluginInstallService
{
    private const string BundlePath = @"C:\ProgramData\Autodesk\ApplicationPlugins\GenMate.bundle";
    private const string ApplicationPluginsPath = @"C:\ProgramData\Autodesk\ApplicationPlugins";

    private static readonly string LocalAppDataGenMate =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenMate");

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "GenMate-PluginInstaller" }
        }
    };

    public async Task InstallAsync(string downloadUrl, IProgress<int> progress, CancellationToken ct = default)
    {
        string? tempFile = null;
        try
        {
            // Download zip to temp file with progress
            tempFile = Path.GetTempFileName();
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long bytesRead = 0;
            int read;
            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;
                if (totalBytes > 0)
                    progress.Report((int)(bytesRead * 100 / totalBytes));
            }

            fileStream.Close();

            // Remove existing bundle
            if (Directory.Exists(BundlePath))
                Directory.Delete(BundlePath, true);

            // Clean local app data
            if (Directory.Exists(LocalAppDataGenMate))
                Directory.Delete(LocalAppDataGenMate, true);

            // Extract zip (contains GenMate.bundle/ root folder)
            Directory.CreateDirectory(ApplicationPluginsPath);
            ZipFile.ExtractToDirectory(tempFile, ApplicationPluginsPath, true);
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    public Task UninstallAsync()
    {
        if (Directory.Exists(BundlePath))
            Directory.Delete(BundlePath, true);

        if (Directory.Exists(LocalAppDataGenMate))
            Directory.Delete(LocalAppDataGenMate, true);

        return Task.CompletedTask;
    }
}
