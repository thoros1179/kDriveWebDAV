using System.Net;
using System.Text.Json;
using kDriveWebDav.KDrive;
using kDriveWebDav.KDrive.Models;

namespace kDriveWebDav.Tests.KDrive;

/// <summary>
/// Unit tests for <see cref="KDriveApiClient"/> using a fake HTTP handler.
/// All tests exercise the URL construction and JSON (de)serialisation logic
/// without making real network calls.
/// </summary>
public sealed class KDriveApiClientTests
{
    // Shared JSON serialiser that matches what the server would return.
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string FileJson(long id, string name, string type = "file", long size = 0,
        long lastModified = 1_700_000_000, string? mimeType = null) =>
        JsonSerializer.Serialize(new
        {
            id,
            name,
            type,
            size,
            last_modified_at = lastModified,
            mime_type        = mimeType,
        });

    private static string ListJson(IEnumerable<object> items, bool success = true,
        int page = 1, int pages = 1, int total = 0)
    {
        var data = items.ToList();
        return JsonSerializer.Serialize(new
        {
            result = success ? "success" : "error",
            data,
            page,
            pages,
            total = total == 0 ? data.Count : total,
        });
    }

    private static string SingleJson(object item, bool success = true) =>
        JsonSerializer.Serialize(new { result = success ? "success" : "error", data = item });

    // ------------------------------------------------------------------ GetChildFilesAsync

    [Fact]
    public async Task GetChildFilesAsync_SinglePage_ReturnsAllFiles()
    {
        var items = new[]
        {
            new { id = 1L, name = "a.txt", type = "file", size = 10L,
                  last_modified_at = 1_700_000_000L, mime_type = (string?)null },
            new { id = 2L, name = "b.txt", type = "file", size = 20L,
                  last_modified_at = 1_700_000_001L, mime_type = (string?)"text/plain" },
        };
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(ListJson(items.Cast<object>())));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 42);

        var result = await client.GetChildFilesAsync(directoryId: 1);

        Assert.Equal(2, result.Count);
        Assert.Equal("a.txt", result[0].Name);
        Assert.Equal("b.txt", result[1].Name);
    }

    [Fact]
    public async Task GetChildFilesAsync_RequestUrl_ContainsDriveIdAndDirectoryId()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(ListJson(Array.Empty<object>())));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 99);

        await client.GetChildFilesAsync(directoryId: 77);

        var url = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("/2/drive/99/files/77/files", url);
    }

    [Fact]
    public async Task GetChildFilesAsync_RequestUrl_IsSortedByNameAscending()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(ListJson(Array.Empty<object>())));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        await client.GetChildFilesAsync(directoryId: 1);

        var url = handler.Requests[0].RequestUri!.Query;
        Assert.Contains("order_by=name", url);
        Assert.Contains("order=asc", url);
    }

    [Fact]
    public async Task GetChildFilesAsync_Paginated_FetchesAllPages()
    {
        const int pageSize = 10;
        // Page 1: full page (10 items) → triggers a second request
        var page1Items = Enumerable.Range(1, pageSize)
            .Select(i => new { id = (long)i, name = $"f{i}.txt", type = "file",
                               size = 0L, last_modified_at = 1_700_000_000L, mime_type = (string?)null })
            .Cast<object>().ToArray();
        // Page 2: partial page (3 items) → stops pagination
        var page2Items = Enumerable.Range(11, 3)
            .Select(i => new { id = (long)i, name = $"f{i}.txt", type = "file",
                               size = 0L, last_modified_at = 1_700_000_000L, mime_type = (string?)null })
            .Cast<object>().ToArray();

        int callCount = 0;
        var handler = new FakeHttpMessageHandler(_ =>
        {
            callCount++;
            return FakeHttpMessageHandler.JsonOkRaw(callCount == 1
                ? ListJson(page1Items)
                : ListJson(page2Items));
        });
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.GetChildFilesAsync(directoryId: 1);

        Assert.Equal(pageSize + 3, result.Count);
        Assert.Equal(2, callCount);
    }

    [Fact]
    public async Task GetChildFilesAsync_WhenApiReturnsError_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(ListJson(Array.Empty<object>(), success: false)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.GetChildFilesAsync(directoryId: 1);

        Assert.Empty(result);
    }

    // ------------------------------------------------------------------ GetRootFilesAsync

    [Fact]
    public async Task GetRootFilesAsync_CallsDirectoryId1()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(ListJson(Array.Empty<object>())));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 5);

        await client.GetRootFilesAsync();

        var url = handler.Requests[0].RequestUri!.ToString();
        Assert.Contains("/files/1/files", url);
    }

    // ------------------------------------------------------------------ GetFileAsync

    [Fact]
    public async Task GetFileAsync_WhenFound_ReturnsFile()
    {
        var fileJson = FileJson(id: 7, name: "doc.pdf");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.GetFileAsync(fileId: 7);

        Assert.NotNull(result);
        Assert.Equal(7, result.Id);
        Assert.Equal("doc.pdf", result.Name);
    }

    [Fact]
    public async Task GetFileAsync_WhenApiReturnsError_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw("""{"result":"error","data":null}"""));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.GetFileAsync(fileId: 99);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetFileAsync_RequestUrl_ContainsFileId()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw("""{"result":"error"}"""));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 3);

        await client.GetFileAsync(fileId: 55);

        Assert.Contains("/files/55", handler.Requests[0].RequestUri!.ToString());
    }

    // ------------------------------------------------------------------ DownloadFileAsync

    [Fact]
    public async Task DownloadFileAsync_ReturnsResponseBodyStream()
    {
        var content = "hello world"u8.ToArray();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content),
        });
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        await using var stream = await client.DownloadFileAsync(fileId: 10);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        Assert.Equal(content, ms.ToArray());
    }

    [Fact]
    public async Task DownloadFileAsync_RequestUrl_ContainsDownloadSegment()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(string.Empty),
        });
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 7);

        await using var _ = await client.DownloadFileAsync(fileId: 42);

        Assert.Contains("/files/42/download", handler.Requests[0].RequestUri!.ToString());
    }

    // ------------------------------------------------------------------ UploadFileAsync

    [Fact]
    public async Task UploadFileAsync_WhenSuccessful_ReturnsUploadedFile()
    {
        var fileJson = FileJson(id: 200, name: "upload.bin");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        using var content = new MemoryStream([1, 2, 3]);
        var result = await client.UploadFileAsync(parentDirectoryId: 5, fileName: "upload.bin",
            content: content, contentLength: 3);

        Assert.NotNull(result);
        Assert.Equal(200, result.Id);
    }

    [Fact]
    public async Task UploadFileAsync_RequestUrl_ContainsFileNameAndDirectoryId()
    {
        var fileJson = FileJson(id: 1, name: "x.txt");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 8);

        await client.UploadFileAsync(parentDirectoryId: 33, fileName: "hello world.txt",
            content: Stream.Null, contentLength: 0);

        var url = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.ToString());
        Assert.Contains("file_name=hello world.txt", url);
        Assert.Contains("directory_id=33", url);
    }

    // ------------------------------------------------------------------ CreateDirectoryAsync

    [Fact]
    public async Task CreateDirectoryAsync_WhenSuccessful_ReturnsDirectory()
    {
        var dirJson = FileJson(id: 50, name: "new-dir", type: "dir");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(dirJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.CreateDirectoryAsync(parentDirectoryId: 10, name: "new-dir");

        Assert.NotNull(result);
        Assert.Equal(50, result.Id);
    }

    [Fact]
    public async Task CreateDirectoryAsync_SendsPostToCorrectUrl()
    {
        var dirJson = FileJson(id: 1, name: "d", type: "dir");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(dirJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 2);

        await client.CreateDirectoryAsync(parentDirectoryId: 15, name: "d");

        var req = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/files/15/directory", req.RequestUri!.ToString());
    }

    // ------------------------------------------------------------------ DeleteAsync

    [Fact]
    public async Task DeleteAsync_SendsDeleteToCorrectUrl()
    {
        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NoContent));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 3);

        await client.DeleteAsync(fileId: 77);

        var req = handler.Requests[0];
        Assert.Equal(HttpMethod.Delete, req.Method);
        Assert.Contains("/files/77", req.RequestUri!.ToString());
    }

    // ------------------------------------------------------------------ RenameAsync

    [Fact]
    public async Task RenameAsync_WhenSuccessful_ReturnsRenamedFile()
    {
        var fileJson = FileJson(id: 10, name: "renamed.txt");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.RenameAsync(fileId: 10, newName: "renamed.txt");

        Assert.NotNull(result);
        Assert.Equal("renamed.txt", result.Name);
    }

    [Fact]
    public async Task RenameAsync_RequestUrl_ContainsRenameSegment()
    {
        var fileJson = FileJson(id: 10, name: "x");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        await client.RenameAsync(fileId: 10, newName: "x");

        Assert.Contains("/files/10/rename", handler.Requests[0].RequestUri!.ToString());
    }

    // ------------------------------------------------------------------ MoveAsync

    [Fact]
    public async Task MoveAsync_WhenSuccessful_ReturnsMovedFile()
    {
        var fileJson = FileJson(id: 5, name: "moved.txt");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        var result = await client.MoveAsync(fileId: 5, destinationDirectoryId: 20);

        Assert.NotNull(result);
        Assert.Equal(5, result.Id);
    }

    [Fact]
    public async Task MoveAsync_RequestUrl_ContainsMoveAndDestination()
    {
        var fileJson = FileJson(id: 5, name: "f");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        await client.MoveAsync(fileId: 5, destinationDirectoryId: 20);

        Assert.Contains("/files/5/move/20", handler.Requests[0].RequestUri!.ToString());
    }

    // ------------------------------------------------------------------ CopyAsync

    [Fact]
    public async Task CopyAsync_SendsPostToCorrectUrl()
    {
        var fileJson = FileJson(id: 6, name: "copy.txt");
        var handler = new FakeHttpMessageHandler(_ =>
            FakeHttpMessageHandler.JsonOkRaw(SingleJson(
                JsonSerializer.Deserialize<object>(fileJson)!)));
        using var client = new KDriveApiClient(handler.MakeHttpClient(), driveId: 1);

        await client.CopyAsync(fileId: 6, destinationDirectoryId: 30);

        var req = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, req.Method);
        Assert.Contains("/files/6/copy", req.RequestUri!.ToString());
    }
}
