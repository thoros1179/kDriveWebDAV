using System.Text.Json.Serialization;

namespace kDriveWebDav.KDrive.Models;

/// <summary>
/// Represents a file or directory in kDrive.
/// </summary>
public sealed class KDriveFile
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;   // "file" or "dir"

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("created_at")]
    public long? CreatedAt { get; set; }   // Unix timestamp (seconds), may be null

    [JsonPropertyName("added_at")]
    public long? AddedAt { get; set; }     // Unix timestamp (seconds), fallback for CreatedAt

    [JsonPropertyName("last_modified_at")]
    public long LastModifiedAt { get; set; }  // Unix timestamp (seconds)

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("parent_id")]
    public long? ParentId { get; set; }

    [JsonPropertyName("is_dir")]
    public bool IsDir => Type == "dir";

    public DateTimeOffset CreatedDate =>
        DateTimeOffset.FromUnixTimeSeconds(CreatedAt ?? AddedAt ?? LastModifiedAt);

    public DateTimeOffset LastModifiedDate =>
        DateTimeOffset.FromUnixTimeSeconds(LastModifiedAt);
}
