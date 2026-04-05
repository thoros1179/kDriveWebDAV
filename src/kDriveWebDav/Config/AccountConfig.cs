namespace kDriveWebDav.Config;

/// <summary>
/// Represents a single kDrive account configuration.
/// </summary>
public sealed class AccountConfig
{
    /// <summary>A unique, human-readable name used as the WebDAV path prefix.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Infomaniak API token (Bearer token) for this account.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>The numeric kDrive ID (drive_id) for this account.</summary>
    public long DriveId { get; set; }
}
