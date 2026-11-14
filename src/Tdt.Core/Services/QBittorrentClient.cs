using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Tdt.Core.Services;

/// <summary>
/// qBittorrent WebUI API v2 client.
/// </summary>
/// <remarks>
/// Authentication follows the same precedence as the previous implementation:
/// an API key wins over everything, otherwise a captured session cookie is
/// sent unless the server reported <c>bypass_local_auth</c>. Cookie handling is
/// manual so the owner must build the <see cref="HttpClient"/> without an
/// automatic cookie handler; see <c>CreateDefaultClient</c>.
/// </remarks>
public sealed class QBittorrentClient : IQBittorrentClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private string? _cookie;
    private string? _apiKey;
    private bool _bypassLocalAuth;

    public QBittorrentClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public bool HasApiKey => !string.IsNullOrEmpty(_apiKey);

    /// <summary>
    /// Builds the client the app should use: a shared handler that does not
    /// manage cookies, because the client sends the session cookie itself.
    /// </summary>
    public static HttpClient CreateDefaultClient(DelegatingHandler? handler = null)
    {
        HttpMessageHandler transport = new SocketsHttpHandler
        {
            UseCookies = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (handler is not null)
        {
            handler.InnerHandler = transport;
            transport = handler;
        }

        return new HttpClient(transport, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(100),
        };
    }

    public void SetApiKey(string? apiKey)
    {
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    public async Task LoginAsync(
        QBittorrentConnection connection,
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (HasApiKey)
        {
            throw new InvalidOperationException(
                "Cannot call login when API key authentication is set");
        }

        var baseUrl = connection.BaseUrl;
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v2/auth/login")
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("username", username ?? string.Empty),
                new KeyValuePair<string, string>("password", password ?? string.Empty),
            ]),
        };
        ApplyHeaders(request, baseUrl);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // qBittorrent 5.2 answers 204 to a successful form login.
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NoContent))
        {
            throw new QBittorrentException(
                $"qBittorrent login failed ({(int)response.StatusCode})");
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var session = cookies.FirstOrDefault();
            if (!string.IsNullOrEmpty(session))
            {
                _cookie = session.Split(';')[0];
            }
        }
    }

    public async Task AddTorrentAsync(
        QBittorrentConnection connection,
        byte[] torrentBytes,
        string? savePath = null,
        bool paused = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(torrentBytes);

        var baseUrl = connection.BaseUrl;
        using var content = new MultipartFormDataContent();
        var torrentContent = new ByteArrayContent(torrentBytes);
        torrentContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-bittorrent");
        content.Add(torrentContent, "torrents", "upload.torrent");

        if (!string.IsNullOrEmpty(savePath))
        {
            content.Add(new StringContent(savePath), "savepath");
        }

        content.Add(new StringContent(paused ? "true" : "false"), "paused");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/v2/torrents/add")
        {
            Content = content,
        };
        ApplyHeaders(request, baseUrl);

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureOk(response, "Failed to add torrent");
    }

    public async Task<IReadOnlyList<QBittorrentTorrent>> GetTorrentsAsync(
        QBittorrentConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = CreateRequest(HttpMethod.Get, connection, "/api/v2/torrents/info");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureOk(response, "Failed to get torrents");

        var torrents = await DeserializeAsync<List<QBittorrentTorrent>>(response, cancellationToken)
            .ConfigureAwait(false);
        return torrents ?? [];
    }

    public async Task<IReadOnlyList<QBittorrentFileEntry>> GetFilesAsync(
        QBittorrentConnection connection,
        string hash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        using var request = CreateRequest(
            HttpMethod.Get,
            connection,
            $"/api/v2/torrents/files?hash={Uri.EscapeDataString(hash)}");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureOk(response, "Failed to get torrent files");

        var files = await DeserializeAsync<List<QBittorrentFileEntry>>(response, cancellationToken)
            .ConfigureAwait(false);
        return files ?? [];
    }

    public async Task SetFilePriorityAsync(
        QBittorrentConnection connection,
        string hash,
        IReadOnlyCollection<int> fileIndexes,
        int priority,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        ArgumentNullException.ThrowIfNull(fileIndexes);

        var ids = string.Join('|', fileIndexes.Select(index => index.ToString(CultureInfo.InvariantCulture)));
        using var request = CreateFormRequest(
            connection,
            "/api/v2/torrents/filePrio",
            [
                new KeyValuePair<string, string>("hash", hash),
                new KeyValuePair<string, string>("id", ids),
                new KeyValuePair<string, string>("priority", priority.ToString(CultureInfo.InvariantCulture)),
            ]);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureOk(response, "Failed to set file priority");
    }

    public Task StartAsync(
        QBittorrentConnection connection,
        string hashes,
        CancellationToken cancellationToken = default)
    {
        return SendHashesAsync(connection, "/api/v2/torrents/start", hashes, "Failed to start torrent(s)", cancellationToken);
    }

    public Task StopAsync(
        QBittorrentConnection connection,
        string hashes,
        CancellationToken cancellationToken = default)
    {
        return SendHashesAsync(connection, "/api/v2/torrents/stop", hashes, "Failed to stop torrent(s)", cancellationToken);
    }

    public async Task<bool> GetBypassLocalAuthAsync(
        QBittorrentConnection connection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var request = CreateRequest(HttpMethod.Get, connection, "/api/v2/app/preferences");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureOk(response, "Failed to get preferences");

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("bypass_local_auth", out var value))
        {
            _bypassLocalAuth = value.ValueKind == JsonValueKind.True;
        }

        return _bypassLocalAuth;
    }

    private async Task SendHashesAsync(
        QBittorrentConnection connection,
        string path,
        string hashes,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashes);

        using var request = CreateFormRequest(
            connection,
            path,
            [new KeyValuePair<string, string>("hashes", hashes)]);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        EnsureOk(response, failureMessage);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        QBittorrentConnection connection,
        string path)
    {
        var baseUrl = connection.BaseUrl;
        var request = new HttpRequestMessage(method, $"{baseUrl}{path}");
        ApplyHeaders(request, baseUrl);
        return request;
    }

    private HttpRequestMessage CreateFormRequest(
        QBittorrentConnection connection,
        string path,
        IEnumerable<KeyValuePair<string, string>> fields)
    {
        var request = CreateRequest(HttpMethod.Post, connection, path);
        request.Content = new FormUrlEncodedContent(fields);
        return request;
    }

    private void ApplyHeaders(HttpRequestMessage request, string baseUrl)
    {
        request.Headers.TryAddWithoutValidation("Referer", baseUrl);

        if (!string.IsNullOrEmpty(_apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        else if (!_bypassLocalAuth && !string.IsNullOrEmpty(_cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", _cookie);
        }
    }

    private static void EnsureOk(HttpResponseMessage response, string message)
    {
        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new QBittorrentException($"{message} ({(int)response.StatusCode})");
        }
    }

    private static async Task<T?> DeserializeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new QBittorrentException("qBittorrent returned an unexpected response", ex);
        }
    }
}
