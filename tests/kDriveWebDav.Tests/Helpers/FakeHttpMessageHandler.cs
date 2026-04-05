using System.Net;
using System.Text;
using System.Text.Json;

namespace kDriveWebDav.Tests;

/// <summary>
/// Minimal fake <see cref="HttpMessageHandler"/> for unit-testing <see cref="kDriveWebDav.KDrive.KDriveApiClient"/>.
/// Records every request so tests can assert on URLs / bodies.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => _responder = responder;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }

    // --------------- factory helpers ---------------

    /// <summary>Creates an <see cref="HttpClient"/> backed by this handler.</summary>
    public HttpClient MakeHttpClient()
        => new(this) { BaseAddress = new Uri("https://api.infomaniak.com") };

    /// <summary>Builds a 200 OK response with a JSON-serialised body.</summary>
    public static HttpResponseMessage JsonOk(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>Builds a 200 OK response with a raw JSON string body.</summary>
    public static HttpResponseMessage JsonOkRaw(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    /// <summary>Builds a 500 Internal Server Error response.</summary>
    public static HttpResponseMessage ServerError()
        => new(HttpStatusCode.InternalServerError);
}
