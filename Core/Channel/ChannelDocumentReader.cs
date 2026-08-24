using GenMate.PluginInstaller.Core.Diagnostics;

namespace GenMate.PluginInstaller.Core.Channel;

/// <summary>
/// Fetches <c>channel.json</c> from the releases repository, falling back to the layout this build
/// shipped with whenever it cannot be fetched or read.
/// </summary>
/// <remarks>
/// <para>
/// The invariant this class exists under: <b>channel.json may relax presentation, never
/// verification.</b> It is an unsigned file fetched over HTTPS, so anyone able to substitute a
/// release asset can probably substitute it too. That is acceptable only because three facts are
/// compiled into this binary and cannot be influenced by the document:
/// </para>
/// <list type="number">
///   <item>the pinned signing identity is a constant in the binary;</item>
///   <item>a plugin bundle with no valid, identity-pinned signed manifest is never installed;</item>
///   <item>an installer update whose signature does not match the same pin is never applied.</item>
/// </list>
/// <para>
/// Walk the attack with those held: repoint the installer asset at a hostile exe and (3) rejects
/// it; repoint the bundle asset and (2) rejects it; lower a minimumVersion to expose old
/// unverifiable releases and (2) rejects them; add a host and it is ignored, because this build
/// only knows hosts it has code for.
/// </para>
/// <para>
/// So: never add a field here that any verification step reads. The reversal this guards against is
/// one line long - "let channel.json turn verification off for QA" - and
/// <c>ChannelInvariantTests</c> fails when a property that could carry it appears on these types.
/// </para>
/// </remarks>
public sealed class ChannelDocumentReader
{
    public const string DefaultUrl =
        "https://raw.githubusercontent.com/jperna7254/genmate-plugin-releases/main/channel.json";

    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly IUpdateLog _log;
    private readonly string _url;

    public ChannelDocumentReader(HttpClient http, IUpdateLog log, string url = DefaultUrl)
    {
        _http = http;
        _log = log;
        _url = url;
    }

    public async Task<ChannelDocument> ReadAsync(CancellationToken ct = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(FetchTimeout);

            var json = await _http.GetStringAsync(_url, timeout.Token);
            if (ChannelDocumentParser.TryParse(json, out var document, out var failure))
                return document;

            _log.Write($"channel.json rejected ({failure}); using the layout this build shipped with");
        }
        catch (Exception ex)
        {
            _log.Write("channel.json could not be fetched; using the layout this build shipped with", ex);
        }

        return ChannelDocument.Fallback;
    }
}
