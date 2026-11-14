using System.Net;
using System.Text;

namespace Tdt.Core.Tests.Support;

/// <summary>A request as observed by <see cref="StubHttpMessageHandler"/>.</summary>
internal sealed record RecordedRequest(
    HttpMethod Method,
    Uri RequestUri,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> ContentHeaders,
    string? Body)
{
    public string Path => RequestUri.AbsolutePath;

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records the request and answers
/// with a caller supplied response.
/// </summary>
/// <remarks>
/// The request content is read inside <see cref="SendAsync"/> because the
/// client disposes the message once the call returns.
/// </remarks>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<RecordedRequest, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<RecordedRequest> Requests { get; } = [];

    public RecordedRequest LastRequest => Requests[^1];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var headers = request.Headers.ToDictionary(
            header => header.Key,
            header => string.Join(",", header.Value),
            StringComparer.OrdinalIgnoreCase);

        var contentHeaders = request.Content is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : request.Content.Headers.ToDictionary(
                header => header.Key,
                header => string.Join(",", header.Value),
                StringComparer.OrdinalIgnoreCase);

        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var recorded = new RecordedRequest(
            request.Method,
            request.RequestUri!,
            headers,
            contentHeaders,
            body);

        Requests.Add(recorded);
        return _responder(recorded);
    }
}

internal static class StubResponses
{
    public static HttpResponseMessage Json(object? body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json"),
        };
    }

    public static HttpResponseMessage Raw(string body, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    public static HttpResponseMessage Empty(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty),
        };

        foreach (var (name, value) in headers)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        return response;
    }
}
