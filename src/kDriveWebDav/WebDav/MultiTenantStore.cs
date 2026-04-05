using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Stores;
using kDriveWebDav.Config;
using kDriveWebDav.KDrive;

namespace kDriveWebDav.WebDav;

/// <summary>
/// The top-level <see cref="IStore"/> implementation.
/// Resolves URIs to the correct per-account kDrive store collection or document,
/// supporting multi-tenancy by using the first path segment as the account name.
/// </summary>
internal sealed class MultiTenantStore : IStore
{
    private readonly IReadOnlyDictionary<string, KDriveApiClient> _clients;
    private readonly ILockingManager _lockingManager;
    private readonly RootCollection _root;

    public MultiTenantStore(
        IEnumerable<AccountConfig> accounts,
        ILockingManager lockingManager)
    {
        _lockingManager = lockingManager;

        _clients = accounts.ToDictionary(
            a => a.Name,
            a => new KDriveApiClient(a.Token, a.DriveId),
            StringComparer.OrdinalIgnoreCase);

        _root = new RootCollection(_clients, _lockingManager);
    }

    // For testing only – accepts pre-built clients.
    internal MultiTenantStore(
        IReadOnlyDictionary<string, KDriveApiClient> clients,
        ILockingManager lockingManager)
    {
        _lockingManager = lockingManager;
        _clients = clients;
        _root = new RootCollection(_clients, _lockingManager);
    }

    /// <summary>
    /// Resolves the URI to the deepest matching <see cref="IStoreItem"/>.
    /// </summary>
    public async Task<IStoreItem?> GetItemAsync(Uri uri, IHttpContext httpContext)
    {
        var segments = GetPathSegments(uri);

        if (segments.Length == 0)
            return _root;

        // First segment is the account name
        var accountName = segments[0];
        if (!_clients.TryGetValue(accountName, out var client))
            return null;

        // Resolve each subsequent segment via the kDrive API
        IStoreCollection current = new KDriveCollection(client, null, 1, accountName, _lockingManager);

        for (var i = 1; i < segments.Length; i++)
        {
            var child = await current.GetItemAsync(segments[i], httpContext).ConfigureAwait(false);
            if (child == null) return null;

            if (i == segments.Length - 1)
                return child;

            if (child is not IStoreCollection childCollection)
                return null;

            current = childCollection;
        }

        return current;
    }

    /// <summary>
    /// Resolves the URI to the deepest matching <see cref="IStoreCollection"/>.
    /// </summary>
    public async Task<IStoreCollection?> GetCollectionAsync(Uri uri, IHttpContext httpContext)
    {
        var item = await GetItemAsync(uri, httpContext).ConfigureAwait(false);
        return item as IStoreCollection;
    }

    // ------------------------------------------------------------------ //

    private static string[] GetPathSegments(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/').TrimEnd('/');
        if (string.IsNullOrEmpty(path))
            return [];

        return path.Split('/', StringSplitOptions.RemoveEmptyEntries)
                   .Select(Uri.UnescapeDataString)
                   .ToArray();
    }
}
