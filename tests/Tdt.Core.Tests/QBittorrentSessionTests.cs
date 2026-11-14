using System.Net;
using Tdt.Core.Models;
using Tdt.Core.Services;
using Tdt.Core.Tests.Support;

namespace Tdt.Core.Tests;

public sealed class QBittorrentSessionTests
{
    private const string Host = "127.0.0.1";
    private const int Port = 8080;

    private static (QBittorrentClient Client, StubHttpMessageHandler Handler) Create(
        Func<RecordedRequest, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        return (new QBittorrentClient(new HttpClient(handler, disposeHandler: false)), handler);
    }

    private static QBittorrentSettings Settings(string apiKey = "") => new()
    {
        Host = Host,
        Port = Port,
        Username = "admin",
        Password = "secret",
        ApiKey = apiKey,
    };

    [Fact]
    public async Task UsesTheApiKeyWithoutLoggingIn()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());

        await QBittorrentSession.ConnectAsync(client, Settings(apiKey: "qbt_key"));

        Assert.True(client.HasApiKey);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task LogsInWithFormCredentialsAndLearnsBypassState()
    {
        var (client, handler) = Create(request => request.Path.EndsWith("/auth/login", StringComparison.Ordinal)
            ? StubResponses.Empty(HttpStatusCode.NoContent, ("Set-Cookie", "SID=abc; path=/"))
            : StubResponses.Json(new Dictionary<string, object> { ["bypass_local_auth"] = false }));

        await QBittorrentSession.ConnectAsync(client, Settings());

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("/api/v2/auth/login", handler.Requests[0].Path);
        Assert.Equal("/api/v2/app/preferences", handler.Requests[1].Path);
    }

    [Fact]
    public async Task RejectsIncompleteSettingsBeforeAnyRequest()
    {
        var (client, handler) = Create(_ => StubResponses.Empty());

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => QBittorrentSession.ConnectAsync(client, new QBittorrentSettings()));

        Assert.Contains("host", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RejectsAnImpossiblePort()
    {
        var (client, _) = Create(_ => StubResponses.Empty());
        var settings = Settings();
        settings.Port = 70000;

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => QBittorrentSession.ConnectAsync(client, settings));

        Assert.Contains("port", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PropagatesARejectedLogin()
    {
        var (client, _) = Create(_ => StubResponses.Empty(HttpStatusCode.Forbidden));

        var error = await Assert.ThrowsAsync<QBittorrentException>(
            () => QBittorrentSession.ConnectAsync(client, Settings()));

        Assert.Contains("403", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IgnoresAServerWithoutAPreferencesEndpoint()
    {
        var (client, _) = Create(request => request.Path.EndsWith("/auth/login", StringComparison.Ordinal)
            ? StubResponses.Empty(HttpStatusCode.NoContent)
            : StubResponses.Empty(HttpStatusCode.NotFound));

        await QBittorrentSession.ConnectAsync(client, Settings());
    }

    [Fact]
    public async Task TestReturnsTheTorrentCount()
    {
        var (client, _) = Create(request =>
        {
            if (request.Path.EndsWith("/auth/login", StringComparison.Ordinal))
            {
                return StubResponses.Empty(HttpStatusCode.NoContent);
            }

            if (request.Path.EndsWith("/app/preferences", StringComparison.Ordinal))
            {
                return StubResponses.Json(new Dictionary<string, object>());
            }

            return StubResponses.Raw("""[{"name":"a","hash":"1"},{"name":"b","hash":"2"}]""");
        });

        var count = await QBittorrentSession.TestAsync(client, Settings());

        Assert.Equal(2, count);
    }
}
