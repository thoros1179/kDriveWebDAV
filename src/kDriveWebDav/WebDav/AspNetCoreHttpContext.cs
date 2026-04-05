using NWebDav.Server.Http;
using AspNetHttpContext = Microsoft.AspNetCore.Http.HttpContext;
using AspNetHttpRequest = Microsoft.AspNetCore.Http.HttpRequest;
using AspNetHttpResponse = Microsoft.AspNetCore.Http.HttpResponse;

namespace kDriveWebDav.WebDav;

/// <summary>
/// Wraps ASP.NET Core <see cref="AspNetHttpContext"/> to implement
/// NWebDav's <see cref="IHttpContext"/> abstraction.
/// </summary>
internal sealed class AspNetCoreHttpContext : IHttpContext
{
    private readonly AspNetHttpContext _context;

    public AspNetCoreHttpContext(AspNetHttpContext context)
    {
        _context = context;
        Request = new AspNetCoreHttpRequest(context.Request);
        Response = new AspNetCoreHttpResponse(context.Response);
        Session = new AspNetCoreHttpSession();
    }

    public IHttpRequest Request { get; }
    public IHttpResponse Response { get; }
    public IHttpSession Session { get; }

    public async Task CloseAsync()
    {
        await _context.Response.Body.FlushAsync().ConfigureAwait(false);
    }
}

/// <summary>Adapts ASP.NET Core <see cref="AspNetHttpRequest"/> to <see cref="IHttpRequest"/>.</summary>
internal sealed class AspNetCoreHttpRequest : IHttpRequest
{
    private readonly AspNetHttpRequest _request;

    public AspNetCoreHttpRequest(AspNetHttpRequest request)
    {
        _request = request;
    }

    public string HttpMethod => _request.Method;

    public Uri Url
    {
        get
        {
            var req = _request;
            var scheme = req.Scheme;
            var host = req.Host.Value;
            var pathBase = req.PathBase.Value ?? string.Empty;
            var path = req.Path.Value ?? string.Empty;
            var query = req.QueryString.Value ?? string.Empty;
            return new Uri($"{scheme}://{host}{pathBase}{path}{query}");
        }
    }

    public string RemoteEndPoint =>
        _request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public IEnumerable<string> Headers => _request.Headers.Keys;

    public string? GetHeaderValue(string header) =>
        _request.Headers.TryGetValue(header, out var values) ? values.ToString() : null;

    // RFC 4918 §9.1: a PROPFIND request with no body MUST be treated as allprop.
    // Some clients (e.g. Synology HyperBackup) omit the body entirely, which causes
    // NWebDav to fail with a 500 when trying to parse an empty XML stream.
    public Stream Stream
    {
        get
        {
            if (_request.ContentLength is null or 0)
            {
                const string allProp = """<?xml version="1.0" encoding="utf-8"?><D:propfind xmlns:D="DAV:"><D:allprop/></D:propfind>""";
                return new MemoryStream(System.Text.Encoding.UTF8.GetBytes(allProp));
            }
            return _request.Body;
        }
    }
}

/// <summary>Adapts ASP.NET Core <see cref="AspNetHttpResponse"/> to <see cref="IHttpResponse"/>.</summary>
internal sealed class AspNetCoreHttpResponse : IHttpResponse
{
    private readonly AspNetHttpResponse _response;

    public AspNetCoreHttpResponse(AspNetHttpResponse response)
    {
        _response = response;
    }

    public int Status
    {
        get => _response.StatusCode;
        set => _response.StatusCode = value;
    }

    public string StatusDescription
    {
        get => _response.StatusCode.ToString();
        set { /* ASP.NET Core does not expose reason phrase directly */ }
    }

    public void SetHeaderValue(string header, string value)
    {
        _response.Headers[header] = value;
    }

    public Stream Stream => _response.Body;
}

/// <summary>Minimal <see cref="IHttpSession"/> that returns no principal (no built-in auth required).</summary>
internal sealed class AspNetCoreHttpSession : IHttpSession
{
    public System.Security.Principal.IPrincipal? Principal => null;
}
