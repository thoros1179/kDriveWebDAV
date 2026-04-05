using System.Xml.Linq;
using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Props;
using NWebDav.Server.Stores;
using kDriveWebDav.Config;
using kDriveWebDav.KDrive;

namespace kDriveWebDav.WebDav;

/// <summary>
/// The virtual root collection that exposes each configured kDrive account
/// as a top-level directory, enabling multi-tenancy.
/// </summary>
internal sealed class RootCollection : IStoreCollection
{
    private readonly IReadOnlyDictionary<string, KDriveApiClient> _clients;
    private readonly ILockingManager _lockingManager;

    public RootCollection(
        IReadOnlyDictionary<string, KDriveApiClient> clients,
        ILockingManager lockingManager)
    {
        _clients = clients;
        _lockingManager = lockingManager;
        PropertyManager = BuildPropertyManager();
    }

    // ---- IStoreItem ----

    public string Name => string.Empty;
    public string UniqueKey => "kdrive-root";
    public IPropertyManager PropertyManager { get; }
    public ILockingManager LockingManager => _lockingManager;
    public InfiniteDepthMode InfiniteDepthMode => InfiniteDepthMode.Rejected;

    public Task<Stream?> GetReadableStreamAsync(IHttpContext httpContext)
        => Task.FromResult<Stream?>(null);

    public Task<DavStatusCode> UploadFromStreamAsync(IHttpContext httpContext, Stream inputStream)
        => Task.FromResult(DavStatusCode.Forbidden);

    // ---- Collection operations ----

    public Task<IStoreItem?> GetItemAsync(string name, IHttpContext httpContext)
    {
        if (_clients.TryGetValue(name, out var client))
        {
            IStoreItem col = new KDriveCollection(client, null, 1, name, _lockingManager);
            return Task.FromResult<IStoreItem?>(col);
        }
        return Task.FromResult<IStoreItem?>(null);
    }

    public Task<IEnumerable<IStoreItem>> GetItemsAsync(IHttpContext httpContext)
    {
        IEnumerable<IStoreItem> items = _clients
            .Select(kv => (IStoreItem)new KDriveCollection(kv.Value, null, 1, kv.Key, _lockingManager))
            .ToList();
        return Task.FromResult(items);
    }

    public Task<StoreItemResult> CreateItemAsync(string name, bool overwrite, IHttpContext httpContext)
        => Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

    public Task<StoreCollectionResult> CreateCollectionAsync(string name, bool overwrite, IHttpContext httpContext)
        => Task.FromResult(new StoreCollectionResult(DavStatusCode.Forbidden));

    public bool SupportsFastMove(IStoreCollection destination, string destinationName, bool overwrite, IHttpContext httpContext)
        => false;

    public Task<StoreItemResult> MoveItemAsync(string sourceName, IStoreCollection destination, string destinationName, bool overwrite, IHttpContext httpContext)
        => Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

    public Task<DavStatusCode> DeleteItemAsync(string name, IHttpContext httpContext)
        => Task.FromResult(DavStatusCode.Forbidden);

    public Task<StoreItemResult> CopyAsync(IStoreCollection destination, string name, bool overwrite, IHttpContext httpContext)
        => Task.FromResult(new StoreItemResult(DavStatusCode.Forbidden));

    // ------------------------------------------------------------------ //

    private IPropertyManager BuildPropertyManager()
        => new PropertyManager<RootCollection>(new DavProperty<RootCollection>[]
        {
            new DavDisplayName<RootCollection>
            {
                Getter = (_, _) => "kDrive WebDAV",
            },
            new DavGetLastModified<RootCollection>
            {
                Getter = (_, _) => DateTime.UtcNow,
            },
            new DavCreationDate<RootCollection>
            {
                Getter = (_, _) => DateTime.UtcNow,
            },
            new DavGetResourceType<RootCollection>
            {
                Getter = (_, _) => new[] { new XElement(WebDavNamespaces.DavNs + "collection") },
            },
            new DavGetContentType<RootCollection>
            {
                Getter = (_, _) => "httpd/unix-directory",
            },
            new DavGetContentLength<RootCollection>
            {
                Getter = (_, _) => 0L,
            },
            new DavLockDiscoveryDefault<RootCollection>(),
            new DavSupportedLockDefault<RootCollection>(),
        });
}
