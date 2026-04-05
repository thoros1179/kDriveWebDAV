using System.Xml.Linq;
using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Props;
using NWebDav.Server.Stores;
using kDriveWebDav.KDrive;
using kDriveWebDav.KDrive.Models;

namespace kDriveWebDav.WebDav;

/// <summary>
/// Represents a kDrive directory as a WebDAV store collection.
/// </summary>
internal sealed class KDriveCollection : IStoreCollection
{
    private readonly KDriveApiClient _client;
    private readonly ILockingManager _lockingManager;

    public KDriveCollection(
        KDriveApiClient client,
        KDriveFile? directory,
        long directoryId,
        string name,
        ILockingManager lockingManager)
    {
        _client = client;
        _lockingManager = lockingManager;
        DirectoryId = directoryId;
        Name = name;

        CreatedDate = directory != null
            ? directory.CreatedDate.UtcDateTime
            : DateTime.UtcNow;
        LastModifiedDate = directory != null
            ? directory.LastModifiedDate.UtcDateTime
            : DateTime.UtcNow;

        PropertyManager = BuildPropertyManager();
    }

    /// <summary>The kDrive ID of this directory.</summary>
    internal long DirectoryId { get; }

    internal DateTime CreatedDate { get; }
    internal DateTime LastModifiedDate { get; }

    // ---- IStoreItem ----

    public string Name { get; }
    public string UniqueKey => $"kdrive-dir-{DirectoryId}";
    public IPropertyManager PropertyManager { get; }
    public ILockingManager LockingManager => _lockingManager;
    public InfiniteDepthMode InfiniteDepthMode => InfiniteDepthMode.Rejected;

    // ---- Stream operations (collections have no body) ----

    public Task<Stream?> GetReadableStreamAsync(IHttpContext httpContext)
        => Task.FromResult<Stream?>(null);

    public Task<DavStatusCode> UploadFromStreamAsync(IHttpContext httpContext, Stream inputStream)
        => Task.FromResult(DavStatusCode.Conflict);

    // ---- Collection operations ----

    public async Task<IStoreItem?> GetItemAsync(string name, IHttpContext httpContext)
    {
        var children = await _client.GetChildFilesAsync(DirectoryId).ConfigureAwait(false);
        var file = children.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (file == null) return null;
        return WrapFile(file);
    }

    public async Task<IEnumerable<IStoreItem>> GetItemsAsync(IHttpContext httpContext)
    {
        var children = await _client.GetChildFilesAsync(DirectoryId).ConfigureAwait(false);
        return children.Select(WrapFile);
    }

    public async Task<StoreItemResult> CreateItemAsync(
        string name, bool overwrite, IHttpContext httpContext)
    {
        // We create a placeholder file by uploading an empty stream.
        // The actual content will be PUT afterwards.
        try
        {
            var children = await _client.GetChildFilesAsync(DirectoryId).ConfigureAwait(false);
            var existing = children.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null && !overwrite)
                return new StoreItemResult(DavStatusCode.PreconditionFailed);

            var uploaded = await _client.UploadFileAsync(
                DirectoryId, name, Stream.Null, 0)
                .ConfigureAwait(false);

            if (uploaded == null)
                return new StoreItemResult(DavStatusCode.InternalServerError);

            var doc = new KDriveDocument(_client, uploaded, _lockingManager);
            return new StoreItemResult(
                existing != null ? DavStatusCode.NoContent : DavStatusCode.Created, doc);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error creating item '{name}' in '{Name}': {ex.Message}");
            return new StoreItemResult(DavStatusCode.InternalServerError);
        }
    }

    public async Task<StoreCollectionResult> CreateCollectionAsync(
        string name, bool overwrite, IHttpContext httpContext)
    {
        try
        {
            var children = await _client.GetChildFilesAsync(DirectoryId).ConfigureAwait(false);
            var existing = children.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null && !overwrite)
                return new StoreCollectionResult((DavStatusCode)405); // 405 Method Not Allowed – RFC 4918 §9.3.1

            var created = await _client.CreateDirectoryAsync(DirectoryId, name)
                .ConfigureAwait(false);

            if (created == null)
                return new StoreCollectionResult(DavStatusCode.InternalServerError);

            var col = new KDriveCollection(_client, created, created.Id, created.Name, _lockingManager);
            return new StoreCollectionResult(DavStatusCode.Created, col);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error creating collection '{name}' in '{Name}': {ex.Message}");
            return new StoreCollectionResult(DavStatusCode.InternalServerError);
        }
    }

    public bool SupportsFastMove(
        IStoreCollection destination,
        string destinationName,
        bool overwrite,
        IHttpContext httpContext)
        => destination is KDriveCollection;

    public async Task<StoreItemResult> MoveItemAsync(
        string sourceName,
        IStoreCollection destination,
        string destinationName,
        bool overwrite,
        IHttpContext httpContext)
    {
        if (destination is not KDriveCollection destCol)
            return new StoreItemResult(DavStatusCode.NotImplemented);

        try
        {
            var children = await _client.GetChildFilesAsync(DirectoryId).ConfigureAwait(false);
            var file = children.FirstOrDefault(f => f.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));

            if (file == null)
                return new StoreItemResult(DavStatusCode.NotFound);

            // Move to destination directory
            KDriveFile? moved;
            if (destCol.DirectoryId != DirectoryId)
                moved = await _client.MoveAsync(file.Id, destCol.DirectoryId).ConfigureAwait(false);
            else
                moved = file;

            // Rename if needed
            if (moved != null && !destinationName.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
                moved = await _client.RenameAsync(moved.Id, destinationName).ConfigureAwait(false);

            if (moved == null)
                return new StoreItemResult(DavStatusCode.InternalServerError);

            return new StoreItemResult(DavStatusCode.Ok, WrapFile(moved));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error moving '{sourceName}' to '{destinationName}': {ex.Message}");
            return new StoreItemResult(DavStatusCode.InternalServerError);
        }
    }

    public async Task<DavStatusCode> DeleteItemAsync(string name, IHttpContext httpContext)
    {
        try
        {
            var children = await _client.GetChildFilesAsync(DirectoryId).ConfigureAwait(false);
            var file = children.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (file == null)
                return DavStatusCode.NotFound;

            await _client.DeleteAsync(file.Id).ConfigureAwait(false);
            return DavStatusCode.Ok;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error deleting '{name}' from '{Name}': {ex.Message}");
            return DavStatusCode.InternalServerError;
        }
    }

    public async Task<StoreItemResult> CopyAsync(
        IStoreCollection destination,
        string name,
        bool overwrite,
        IHttpContext httpContext)
    {
        if (destination is not KDriveCollection destCol)
            return new StoreItemResult(DavStatusCode.NotImplemented);

        try
        {
            var copied = await _client.CopyAsync(DirectoryId, destCol.DirectoryId)
                .ConfigureAwait(false);

            if (copied == null)
                return new StoreItemResult(DavStatusCode.InternalServerError);

            if (!name.Equals(Name, StringComparison.OrdinalIgnoreCase))
                await _client.RenameAsync(copied.Id, name).ConfigureAwait(false);

            return new StoreItemResult(DavStatusCode.Created);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error copying '{Name}' to '{name}': {ex.Message}");
            return new StoreItemResult(DavStatusCode.InternalServerError);
        }
    }

    // ------------------------------------------------------------------ //

    private IStoreItem WrapFile(KDriveFile file)
    {
        if (file.IsDir)
            return new KDriveCollection(_client, file, file.Id, file.Name, _lockingManager);
        return new KDriveDocument(_client, file, _lockingManager);
    }

    private IPropertyManager BuildPropertyManager()
        => new PropertyManager<KDriveCollection>(new DavProperty<KDriveCollection>[]
        {
            new DavDisplayName<KDriveCollection>
            {
                Getter = (_, item) => item.Name,
            },
            new DavGetLastModified<KDriveCollection>
            {
                Getter = (_, item) => item.LastModifiedDate,
            },
            new DavCreationDate<KDriveCollection>
            {
                Getter = (_, item) => item.CreatedDate,
            },
            new DavGetResourceType<KDriveCollection>
            {
                Getter = (_, _) => new[] { new XElement(WebDavNamespaces.DavNs + "collection") },
            },
            new DavGetContentType<KDriveCollection>
            {
                Getter = (_, _) => "httpd/unix-directory",
            },
            new DavGetContentLength<KDriveCollection>
            {
                Getter = (_, _) => 0L,
            },
            new DavLockDiscoveryDefault<KDriveCollection>(),
            new DavSupportedLockDefault<KDriveCollection>(),
        });
}
