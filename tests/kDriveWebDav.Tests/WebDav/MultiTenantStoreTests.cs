using System.Text.Json;
using kDriveWebDav.KDrive;
using kDriveWebDav.WebDav;
using NWebDav.Server.Locking;
using NWebDav.Server.Stores;

namespace kDriveWebDav.Tests.WebDav;

/// <summary>
/// Unit tests for <see cref="MultiTenantStore"/> path-resolution logic.
/// The kDrive API calls are intercepted by a fake <see cref="FakeHttpMessageHandler"/>
/// that returns empty directory listings, so no real HTTP traffic occurs.
/// </summary>
public sealed class MultiTenantStoreTests
{
    private static readonly ILockingManager s_lockingManager = new InMemoryLockingManager();

    // A handler that always responds with an empty, successful listing.
    private static FakeHttpMessageHandler EmptyListingHandler()
        => new(_ => FakeHttpMessageHandler.JsonOkRaw(
            """{"result":"success","data":[],"pages":1,"page":1,"total":0}"""));

    private static MultiTenantStore MakeStore(params (string name, long driveId)[] accounts)
    {
        var clients = accounts.ToDictionary(
            a => a.name,
            a =>
            {
                var handler = EmptyListingHandler();
                return new KDriveApiClient(handler.MakeHttpClient(), a.driveId);
            },
            StringComparer.OrdinalIgnoreCase);

        return new MultiTenantStore(clients, s_lockingManager);
    }

    private static Uri Uri(string path) => new($"http://localhost{path}");

    // Minimal no-op IHttpContext — the store only passes it through to child collections.
    private static readonly NWebDav.Server.Http.IHttpContext s_ctx =
        new FakeHttpContext();

    // ------------------------------------------------------------------ root

    [Fact]
    public async Task GetItemAsync_RootUri_ReturnsRootCollection()
    {
        var store = MakeStore(("Alice", 1));

        var item = await store.GetItemAsync(Uri("/"), s_ctx);

        Assert.NotNull(item);
        Assert.IsAssignableFrom<IStoreCollection>(item);
    }

    [Fact]
    public async Task GetCollectionAsync_RootUri_ReturnsRootCollection()
    {
        var store = MakeStore(("Alice", 1));

        var col = await store.GetCollectionAsync(Uri("/"), s_ctx);

        Assert.NotNull(col);
    }

    // ------------------------------------------------------------------ account root

    [Fact]
    public async Task GetItemAsync_KnownAccount_ReturnsKDriveCollection()
    {
        var store = MakeStore(("Alice", 10));

        var item = await store.GetItemAsync(Uri("/Alice"), s_ctx);

        Assert.NotNull(item);
        Assert.IsAssignableFrom<IStoreCollection>(item);
    }

    [Fact]
    public async Task GetItemAsync_AccountNameIsCaseInsensitive()
    {
        var store = MakeStore(("alice", 10));

        var item = await store.GetItemAsync(Uri("/ALICE"), s_ctx);

        Assert.NotNull(item);
    }

    [Fact]
    public async Task GetItemAsync_UnknownAccount_ReturnsNull()
    {
        var store = MakeStore(("Alice", 10));

        var item = await store.GetItemAsync(Uri("/Ghost"), s_ctx);

        Assert.Null(item);
    }

    // ------------------------------------------------------------------ nested paths

    [Fact]
    public async Task GetItemAsync_PathBeyondRoot_ReturnsNullWhenDirectoryEmpty()
    {
        // The fake handler returns an empty listing, so no child named "subdir" exists.
        var store = MakeStore(("Alice", 10));

        var item = await store.GetItemAsync(Uri("/Alice/subdir"), s_ctx);

        Assert.Null(item);
    }

    // ------------------------------------------------------------------ URL decoding

    [Fact]
    public async Task GetItemAsync_UrlEncodedAccountName_IsDecodedCorrectly()
    {
        var store = MakeStore(("My Drive", 1));

        // "My%20Drive" should be decoded to "My Drive" and match the account.
        var item = await store.GetItemAsync(Uri("/My%20Drive"), s_ctx);

        Assert.NotNull(item);
    }

    // ------------------------------------------------------------------ root item listing

    [Fact]
    public async Task RootCollection_GetItemsAsync_ReturnsOneCollectionPerAccount()
    {
        var store = MakeStore(("Alice", 1), ("Bob", 2));
        var root = (IStoreCollection)(await store.GetCollectionAsync(Uri("/"), s_ctx))!;

        var items = (await root.GetItemsAsync(s_ctx)).ToList();

        Assert.Equal(2, items.Count);
        var names = items.Select(i => i.Name).OrderBy(n => n).ToList();
        Assert.Equal(["Alice", "Bob"], names);
    }

    // ------------------------------------------------------------------ fake IHttpContext

    private sealed class FakeHttpContext : NWebDav.Server.Http.IHttpContext
    {
        public NWebDav.Server.Http.IHttpRequest  Request  { get; } = new FakeRequest();
        public NWebDav.Server.Http.IHttpResponse Response { get; } = new FakeResponse();
        public NWebDav.Server.Http.IHttpSession  Session  { get; } = new FakeSession();
        public Task CloseAsync() => Task.CompletedTask;
    }

    private sealed class FakeRequest : NWebDav.Server.Http.IHttpRequest
    {
        public string HttpMethod  => "GET";
        public System.Uri Url     => new("http://localhost/");
        public string RemoteEndPoint => "127.0.0.1";
        public IEnumerable<string> Headers => [];
        public string? GetHeaderValue(string header) => null;
        public Stream Stream => Stream.Null;
    }

    private sealed class FakeResponse : NWebDav.Server.Http.IHttpResponse
    {
        public int    Status            { get; set; } = 200;
        public string StatusDescription { get; set; } = "OK";
        public Stream Stream => Stream.Null;
        public void SetHeaderValue(string header, string value) { }
    }

    private sealed class FakeSession : NWebDav.Server.Http.IHttpSession
    {
        public System.Security.Principal.IPrincipal? Principal => null;
    }
}
