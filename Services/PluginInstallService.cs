using System.IO;
using System.Net.Http;
using GenMate.PluginInstaller.Core.Hosts;

namespace GenMate.PluginInstaller.Services;

public class PluginInstallService : IPluginInstallService
{
    private readonly PluginHostService _hostService;

    private static readonly HttpClient HttpClient = new()
    {
        DefaultRequestHeaders =
        {
            { "User-Agent", "GenMate-PluginInstaller" }
        }
    };

    public PluginInstallService(PluginHostService hostService)
    {
        _hostService = hostService;
    }

    public async Task InstallAsync(PluginHost host, string downloadUrl, IProgress<int> progress, CancellationToken ct = default)
    {
        string? tempFile = null;
        try
        {
            tempFile = Path.GetTempFileName();
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            await using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
            await using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
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
            }

            await Task.Run(() => _hostService.InstallFromZip(host, tempFile), ct);
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    public Task UninstallAsync(PluginHost host) => Task.Run(() => _hostService.Uninstall(host));
}
