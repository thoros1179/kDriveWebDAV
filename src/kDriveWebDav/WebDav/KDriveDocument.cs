using NWebDav.Server;
using NWebDav.Server.Http;
using NWebDav.Server.Locking;
using NWebDav.Server.Props;
using NWebDav.Server.Stores;
using kDriveWebDav.KDrive;
using kDriveWebDav.KDrive.Models;

namespace kDriveWebDav.WebDav;

/// <summary>
/// Represents a single kDrive file as a WebDAV store item.
/// </summary>
internal sealed class KDriveDocument : IStoreItem
{
    private readonly KDriveApiClient _client;

    public KDriveDocument(KDriveApiClient client, KDriveFile file, ILockingManager lockingManager)
    {
        _client = client;
        File = file;
        LockingManager = lockingManager;
        PropertyManager = BuildPropertyManager();
    }

    internal KDriveFile File { get; }

    // ---- IStoreItem ----

    public string Name => File.Name;
    public string UniqueKey => $"kdrive-file-{File.Id}";
    public IPropertyManager PropertyManager { get; }
    public ILockingManager LockingManager { get; }

    public async Task<Stream?> GetReadableStreamAsync(IHttpContext httpContext)
    {
        try
        {
            return await _client.DownloadFileAsync(File.Id).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error downloading file '{File.Name}': {ex.Message}");
            return null;
        }
    }

    public async Task<DavStatusCode> UploadFromStreamAsync(IHttpContext httpContext, Stream inputStream)
    {
        try
        {
            if (File.ParentId == null)
                return DavStatusCode.Conflict;

            long? contentLength = null;
            if (httpContext.Request.GetHeaderValue("Content-Length") is string cl
                && long.TryParse(cl, out var len))
                contentLength = len;

            await _client.UploadFileAsync(
                File.ParentId.Value,
                File.Name,
                inputStream,
                contentLength)
                .ConfigureAwait(false);

            return DavStatusCode.Ok;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[kDriveWebDav] Error uploading file '{File.Name}': {ex.Message}");
            return DavStatusCode.InternalServerError;
        }
    }

    public async Task<StoreItemResult> CopyAsync(
        IStoreCollection destination,
        string name,
        bool overwrite,
        IHttpContext httpContext)
    {
        if (destination is KDriveCollection destCol)
        {
            try
            {
                var copied = await _client.CopyAsync(File.Id, destCol.DirectoryId)
                    .ConfigureAwait(false);

                if (copied != null && name != File.Name)
                    await _client.RenameAsync(copied.Id, name).ConfigureAwait(false);

                return new StoreItemResult(DavStatusCode.Created);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[kDriveWebDav] Error copying file '{File.Name}': {ex.Message}");
                return new StoreItemResult(DavStatusCode.InternalServerError);
            }
        }

        return new StoreItemResult(DavStatusCode.NotImplemented);
    }

    // ------------------------------------------------------------------ //

    private IPropertyManager BuildPropertyManager()
        => new PropertyManager<KDriveDocument>(new DavProperty<KDriveDocument>[]
        {
            new DavDisplayName<KDriveDocument>
            {
                Getter = (_, item) => item.File.Name,
            },
            new DavGetContentLength<KDriveDocument>
            {
                Getter = (_, item) => item.File.Size,
            },
            new DavGetContentType<KDriveDocument>
            {
                Getter = (_, item) => item.File.MimeType ?? "application/octet-stream",
            },
            new DavGetEtag<KDriveDocument>
            {
                Getter = (_, item) => $"\"{item.File.Id}-{item.File.LastModifiedAt}\"",
            },
            new DavGetLastModified<KDriveDocument>
            {
                Getter = (_, item) => item.File.LastModifiedDate.UtcDateTime,
            },
            new DavCreationDate<KDriveDocument>
            {
                Getter = (_, item) => item.File.CreatedDate.UtcDateTime,
            },
            new DavGetResourceType<KDriveDocument>
            {
                Getter = (_, _) => null,
            },
            new DavLockDiscoveryDefault<KDriveDocument>(),
            new DavSupportedLockDefault<KDriveDocument>(),
        });
}
