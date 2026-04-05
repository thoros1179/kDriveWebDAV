using System.Text.Json.Serialization;

namespace kDriveWebDav.KDrive.Models;

/// <summary>Generic Infomaniak API response envelope.</summary>
public sealed class ApiResponse<T>
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;  // "success" or "error"

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }

    public bool IsSuccess => Result == "success";
}

/// <summary>Paginated API response envelope.</summary>
public sealed class ApiListResponse<T>
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = [];

    [JsonPropertyName("error")]
    public ApiError? Error { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; } = 1;

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("total")]
    public int Total { get; set; }

    public bool IsSuccess => Result == "success";
}

/// <summary>API error details.</summary>
public sealed class ApiError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}
