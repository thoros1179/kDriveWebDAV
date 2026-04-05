using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using kDriveWebDav.KDrive.Models;

namespace kDriveWebDav.KDrive;

/// <summary>
/// HTTP client that wraps the Infomaniak kDrive REST API (v2).
/// </summary>
public sealed class KDriveApiClient : IDisposable
{
    private const string BaseUrl = "https://api.infomaniak.com";
    private const long RootDirectoryId = 1;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly long _driveId;

    public KDriveApiClient(string token, long driveId)
    {
        _driveId = driveId;
        _http = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestVersion = System.Net.HttpVersion.Version11;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }

    // For testing only – accepts a pre-configured HttpClient.
    internal KDriveApiClient(HttpClient http, long driveId)
    {
        _driveId = driveId;
        _http = http;
    }

    // ------------------------------------------------------------------ //
    //  Directory listing
    // ------------------------------------------------------------------ //

    /// <summary>Lists files in the root directory of the drive.</summary>
    public Task<List<KDriveFile>> GetRootFilesAsync(CancellationToken ct = default)
        => GetChildFilesAsync(RootDirectoryId, ct);

    /// <summary>Lists the direct children of a directory by its ID.</summary>
    public async Task<List<KDriveFile>> GetChildFilesAsync(long directoryId, CancellationToken ct = default)
    {
        var result = new List<KDriveFile>();
        int page = 1;
        int pageSize = 10;

        while (true)
        {
            var response = await _http.GetFromJsonAsync<ApiListResponse<KDriveFile>>(
                $"/2/drive/{_driveId}/files/{directoryId}/files?order_by=name&order=asc&page={page}&per_page={pageSize}",
                s_jsonOptions, ct).ConfigureAwait(false);

            if (response?.IsSuccess != true || response.Data == null || response.Data.Count == 0)
                break;

            result.AddRange(response.Data);

            if (response.Data.Count < pageSize)
                break;

            page++;
        }

        return result;
    }

    /// <summary>Gets metadata for a single file or directory by ID.</summary>
    public async Task<KDriveFile?> GetFileAsync(long fileId, CancellationToken ct = default)
    {
        var response = await _http.GetFromJsonAsync<ApiResponse<KDriveFile>>(
            $"/2/drive/{_driveId}/files/{fileId}",
            s_jsonOptions, ct).ConfigureAwait(false);

        return response?.IsSuccess == true ? response.Data : null;
    }

    // ------------------------------------------------------------------ //
    //  Download
    // ------------------------------------------------------------------ //

    /// <summary>Opens a readable stream for the content of a file.</summary>
    public async Task<Stream> DownloadFileAsync(long fileId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync(
            $"/2/drive/{_driveId}/files/{fileId}/download",
            HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    // ------------------------------------------------------------------ //
    //  Upload
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Uploads a file to the specified parent directory.
    /// Uses the kDrive chunked-upload endpoint.
    /// </summary>
    public async Task<KDriveFile?> UploadFileAsync(
        long parentDirectoryId,
        string fileName,
        Stream content,
        long? contentLength = null,
        CancellationToken ct = default)
    {
        var size = contentLength ?? (content.CanSeek ? content.Length : 0);
        var url = $"/2/drive/{_driveId}/upload" +
                  $"?file_name={Uri.EscapeDataString(fileName)}" +
                  $"&directory_id={parentDirectoryId}" +
                  $"&total_size={size}" +
                  $"&conflict=version";

        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType =
            new MediaTypeHeaderValue("application/octet-stream");

        var response = await _http.PostAsync(url, streamContent, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KDriveFile>>(
            s_jsonOptions, ct).ConfigureAwait(false);

        return result?.IsSuccess == true ? result.Data : null;
    }

    // ------------------------------------------------------------------ //
    //  Create directory
    // ------------------------------------------------------------------ //

    /// <summary>Creates a new sub-directory inside the given parent directory.</summary>
    public async Task<KDriveFile?> CreateDirectoryAsync(
        long parentDirectoryId,
        string name,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { name });
        using var requestContent = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(
            $"/2/drive/{_driveId}/files/{parentDirectoryId}/directory",
            requestContent, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KDriveFile>>(
            s_jsonOptions, ct).ConfigureAwait(false);

        return result?.IsSuccess == true ? result.Data : null;
    }

    // ------------------------------------------------------------------ //
    //  Delete
    // ------------------------------------------------------------------ //

    /// <summary>Moves a file or directory to the trash.</summary>
    public async Task DeleteAsync(long fileId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync(
            $"/2/drive/{_driveId}/files/{fileId}", ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
    }

    // ------------------------------------------------------------------ //
    //  Rename / Move / Copy
    // ------------------------------------------------------------------ //

    /// <summary>Renames a file or directory.</summary>
    public async Task<KDriveFile?> RenameAsync(
        long fileId,
        string newName,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { name = newName });
        using var requestContent = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(
            $"/2/drive/{_driveId}/files/{fileId}/rename",
            requestContent, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KDriveFile>>(
            s_jsonOptions, ct).ConfigureAwait(false);

        return result?.IsSuccess == true ? result.Data : null;
    }

    /// <summary>Moves a file or directory to a different parent directory.</summary>
    public async Task<KDriveFile?> MoveAsync(
        long fileId,
        long destinationDirectoryId,
        CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"/2/drive/{_driveId}/files/{fileId}/move/{destinationDirectoryId}",
            null, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KDriveFile>>(
            s_jsonOptions, ct).ConfigureAwait(false);

        return result?.IsSuccess == true ? result.Data : null;
    }

    /// <summary>Copies a file or directory into the given destination directory.</summary>
    public async Task<KDriveFile?> CopyAsync(
        long fileId,
        long destinationDirectoryId,
        CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { destination_directory_id = destinationDirectoryId });
        using var requestContent = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync(
            $"/2/drive/{_driveId}/files/{fileId}/copy",
            requestContent, ct).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<KDriveFile>>(
            s_jsonOptions, ct).ConfigureAwait(false);

        return result?.IsSuccess == true ? result.Data : null;
    }

    public void Dispose() => _http.Dispose();
}
