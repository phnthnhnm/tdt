using System.Net;
using Tdt.Core.Services;
using Tdt.Core.Tests.Support;

namespace Tdt.Core.Tests;

public sealed class QBittorrentClientTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 8080;
    private const string BaseUrl = "http://127.0.0.1:8080";

    private static readonly QBittorrentConnection Connection = new(Host, Port, UseHttps: false);

    private static (QBittorrentClient Client, StubHttpMessageHandler Handler) Create(
        Func<RecordedRequest, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var http = new HttpClient(handler, disposeHandler: false);
        return (new QBittorrentClient(http), handler);
    }

    [Fact]
    public async Task LoginPostsFormCredentialsAndKeepsTheSessionCookie()
    {
        var (client, handler) = Create(request => request.Path.EndsWith("/auth/login", StringComparison.Ordinal)
            ? StubResponses.Empty(
                HttpStatusCode.OK,
                ("Set-Cookie", "SID=abc123; HttpOnly; path=/"))
            : StubResponses.Json(Array.Empty<object>()));

        await client.LoginAsync(Connection, "admin", "secret");

        var login = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Equal("http://127.0.0.1:8080/api/v2/auth/login", login.RequestUri.ToString());
        Assert.Equal(BaseUrl + "/", login.Header("Referer"));
        Assert.Null(login.Header("Cookie"));
        Assert.Equal("username=admin&password=secret", login.Body);

        await client.GetTorrentsAsync(Connection);

        Assert.Equal("SID=abc123", handler.LastRequest.Header("Cookie"));
    }

    [Fact]
    public async Task LoginAcceptsNoContentFromQBittorrent52AndLater()
    {
        var (client, handler) = Create(request => request.Path.EndsWith("/auth/login", StringComparison.Ordinal)
            ? StubResponses.Empty(
                HttpStatusCode.NoContent,
                ("Set-Cookie", "SID=xyz; path=/"))
            : StubResponses.Json(Array.Empty<object>()));

        await client.LoginAsync(Connection, "u", "p");
        await client.GetTorrentsAsync(Connection);

        Assert.Equal("SID=xyz", handler.LastRequest.Header("Cookie"));
    }

    [Fact]
    public async Task LoginReportsTheRejectedStatus()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.Forbidden));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => client.LoginAsync(Connection, "u", "p"));

        Assert.Contains("403", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginIsRefusedWhenAnApiKeyIsConfigured()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());
        client.SetApiKey("qbt_key");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.LoginAsync(Connection, "u", "p"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ApiKeyTakesPrecedenceOverTheSessionCookie()
    {
        var (client, handler) = Create(request => request.Path.EndsWith("/auth/login", StringComparison.Ordinal)
            ? StubResponses.Empty(HttpStatusCode.OK, ("Set-Cookie", "SID=abc123; path=/"))
            : StubResponses.Json(Array.Empty<object>()));

        await client.LoginAsync(Connection, "u", "p");
        client.SetApiKey("qbt_secret");
        await client.GetTorrentsAsync(Connection);

        var request = handler.LastRequest;
        Assert.Equal("Bearer qbt_secret", request.Header("Authorization"));
        Assert.Null(request.Header("Cookie"));
    }

    [Fact]
    public async Task BypassLocalAuthStopsSendingTheCookie()
    {
        var (client, handler) = Create(request =>
        {
            if (request.Path.EndsWith("/auth/login", StringComparison.Ordinal))
            {
                return StubResponses.Empty(HttpStatusCode.OK, ("Set-Cookie", "SID=abc123; path=/"));
            }

            if (request.Path.EndsWith("/app/preferences", StringComparison.Ordinal))
            {
                return StubResponses.Json(new Dictionary<string, object> { ["bypass_local_auth"] = true });
            }

            return StubResponses.Json(Array.Empty<object>());
        });

        await client.LoginAsync(Connection, "u", "p");
        await client.GetBypassLocalAuthAsync(Connection);

        Assert.Equal("SID=abc123", handler.LastRequest.Header("Cookie"));

        await client.GetTorrentsAsync(Connection);

        Assert.Null(handler.LastRequest.Header("Cookie"));
    }

    [Fact]
    public async Task KeepsSendingTheCookieWhenLocalAuthIsRequired()
    {
        var (client, handler) = Create(request =>
        {
            if (request.Path.EndsWith("/auth/login", StringComparison.Ordinal))
            {
                return StubResponses.Empty(HttpStatusCode.OK, ("Set-Cookie", "SID=abc123; path=/"));
            }

            if (request.Path.EndsWith("/app/preferences", StringComparison.Ordinal))
            {
                return StubResponses.Json(new Dictionary<string, object> { ["bypass_local_auth"] = false });
            }

            return StubResponses.Json(Array.Empty<object>());
        });

        await client.LoginAsync(Connection, "u", "p");
        await client.GetBypassLocalAuthAsync(Connection);
        await client.GetTorrentsAsync(Connection);

        Assert.Equal("SID=abc123", handler.LastRequest.Header("Cookie"));
    }

    [Fact]
    public async Task KeepsTheKnownBypassStateWhenTheServerOmitsIt()
    {
        var calls = 0;
        var (client, _) = Create(request =>
        {
            if (request.Path.EndsWith("/app/preferences", StringComparison.Ordinal))
            {
                calls++;
                return calls == 1
                    ? StubResponses.Json(new Dictionary<string, object> { ["bypass_local_auth"] = true })
                    : StubResponses.Json(new Dictionary<string, object>());
            }

            return StubResponses.Json(Array.Empty<object>());
        });

        Assert.True(await client.GetBypassLocalAuthAsync(Connection));
        Assert.True(await client.GetBypassLocalAuthAsync(Connection));
    }

    [Fact]
    public async Task UsesHttpsAndKeepsTheRefererInSync()
    {
        var (client, handler) = Create(_ => StubResponses.Json(Array.Empty<object>()));

        await client.GetTorrentsAsync(new QBittorrentConnection(Host, Port, UseHttps: true));

        Assert.Equal("https://127.0.0.1:8080/api/v2/torrents/info", handler.LastRequest.RequestUri.ToString());
        Assert.Equal("https://127.0.0.1:8080/", handler.LastRequest.Header("Referer"));
    }

    [Fact]
    public async Task DecodesTheTorrentList()
    {
        var (client, _) = Create(_ => StubResponses.Raw("""[{"name":"Release","hash":"abc"}]"""));

        var torrents = await client.GetTorrentsAsync(Connection);

        var torrent = Assert.Single(torrents);
        Assert.Equal("Release", torrent.Name);
        Assert.Equal("abc", torrent.Hash);
    }

    [Fact]
    public async Task ReportsAFailedTorrentList()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.InternalServerError));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => client.GetTorrentsAsync(Connection));

        Assert.Contains("500", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestsFilesForAHash()
    {
        var (client, handler) = Create(_ => StubResponses.Raw("""[{"index":3,"name":"Release/001.cbz"}]"""));

        var files = await client.GetFilesAsync(Connection, "deadbeef");

        Assert.Equal("hash=deadbeef", handler.LastRequest.RequestUri.Query.TrimStart('?'));
        var file = Assert.Single(files);
        Assert.Equal(3, file.Index);
        Assert.Equal("Release/001.cbz", file.Name);
    }

    [Fact]
    public async Task ReportsAFailedFileList()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => client.GetFilesAsync(Connection, "abc"));

        Assert.Contains("404", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadsTheTorrentAsMultipartFormData()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());

        await client.AddTorrentAsync(Connection, [1, 2, 3], savePath: @"D:\Downloads", paused: false);

        var request = handler.LastRequest;
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://127.0.0.1:8080/api/v2/torrents/add", request.RequestUri.ToString());
        Assert.StartsWith("multipart/form-data", request.ContentHeaders["Content-Type"], StringComparison.Ordinal);
        Assert.Contains("upload.torrent", request.Body, StringComparison.Ordinal);
        Assert.Contains("torrents", request.Body, StringComparison.Ordinal);
        Assert.Contains(@"D:\Downloads", request.Body, StringComparison.Ordinal);
        Assert.Contains("false", request.Body, StringComparison.Ordinal);
        Assert.Equal(BaseUrl + "/", request.Header("Referer"));
    }

    [Fact]
    public async Task AddsTorrentsPausedWithoutASavePathByDefault()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());

        await client.AddTorrentAsync(Connection, [1]);

        var body = handler.LastRequest.Body;
        Assert.Contains("paused", body, StringComparison.Ordinal);
        Assert.Contains("true", body, StringComparison.Ordinal);
        Assert.DoesNotContain("savepath", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsARejectedTorrent()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.UnsupportedMediaType));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => client.AddTorrentAsync(Connection, [1]));

        Assert.Contains("415", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostsFilePriorities()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());

        await client.SetFilePriorityAsync(Connection, "abc", [0, 2, 5], priority: 1);

        var request = handler.LastRequest;
        Assert.Equal("/api/v2/torrents/filePrio", request.Path);
        Assert.Equal("hash=abc&id=0%7C2%7C5&priority=1", request.Body);
    }

    [Fact]
    public async Task ReportsRejectedFilePriorities()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.BadRequest));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => client.SetFilePriorityAsync(Connection, "abc", [0], priority: 0));

        Assert.Contains("400", error.Message, StringComparison.Ordinal);
    }
    [Fact]
    public async Task StopsTorrents()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());

        await client.StopAsync(Connection, "abc");

        Assert.Equal("/api/v2/torrents/stop", handler.LastRequest.Path);
        Assert.Equal("hashes=abc", handler.LastRequest.Body);
    }
    [Fact]
    public async Task ReportsAFailedStop()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.Conflict));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => client.StopAsync(Connection, "abc"));

        Assert.Contains("409", error.Message, StringComparison.Ordinal);
    }
}
