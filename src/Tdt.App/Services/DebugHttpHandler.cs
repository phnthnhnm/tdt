using System.Diagnostics;

namespace Tdt.App.Services;

/// <summary>
/// Logs the method, URL and status of every qBittorrent request in debug
/// builds. Headers are never logged, so cookies and API keys cannot leak into
/// the log.
/// </summary>
public sealed class DebugHttpHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

#if DEBUG
        Debug.WriteLine($"[qbt] {request.Method} {request.RequestUri}");
#endif

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

#if DEBUG
        Debug.WriteLine($"[qbt] {(int)response.StatusCode} {request.RequestUri}");
#endif

        return response;
    }
}
