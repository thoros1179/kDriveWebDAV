using kDriveWebDav.KDrive.Models;

namespace kDriveWebDav.Tests.KDrive;

public sealed class KDriveFileTests
{
    // ------------------------------------------------------------------ IsDir

    [Fact]
    public void IsDir_WhenTypeIsDir_ReturnsTrue()
    {
        var file = new KDriveFile { Type = "dir" };
        Assert.True(file.IsDir);
    }

    [Fact]
    public void IsDir_WhenTypeIsFile_ReturnsFalse()
    {
        var file = new KDriveFile { Type = "file" };
        Assert.False(file.IsDir);
    }

    [Fact]
    public void IsDir_WhenTypeIsOther_ReturnsFalse()
    {
        var file = new KDriveFile { Type = "unknown" };
        Assert.False(file.IsDir);
    }

    // ------------------------------------------------------------------ CreatedDate

    [Fact]
    public void CreatedDate_WhenCreatedAtIsSet_UsesCreatedAt()
    {
        var file = new KDriveFile
        {
            CreatedAt      = 1_700_000_000,
            AddedAt        = 1_600_000_000,
            LastModifiedAt = 1_500_000_000,
        };

        var expected = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        Assert.Equal(expected, file.CreatedDate);
    }

    [Fact]
    public void CreatedDate_WhenCreatedAtIsNull_FallsBackToAddedAt()
    {
        var file = new KDriveFile
        {
            CreatedAt      = null,
            AddedAt        = 1_600_000_000,
            LastModifiedAt = 1_500_000_000,
        };

        var expected = DateTimeOffset.FromUnixTimeSeconds(1_600_000_000);
        Assert.Equal(expected, file.CreatedDate);
    }

    [Fact]
    public void CreatedDate_WhenBothCreatedAtAndAddedAtAreNull_FallsBackToLastModifiedAt()
    {
        var file = new KDriveFile
        {
            CreatedAt      = null,
            AddedAt        = null,
            LastModifiedAt = 1_500_000_000,
        };

        var expected = DateTimeOffset.FromUnixTimeSeconds(1_500_000_000);
        Assert.Equal(expected, file.CreatedDate);
    }

    // ------------------------------------------------------------------ LastModifiedDate

    [Fact]
    public void LastModifiedDate_ReturnsDateTimeOffsetFromUnixTimestamp()
    {
        var file = new KDriveFile { LastModifiedAt = 1_700_000_000 };

        var expected = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        Assert.Equal(expected, file.LastModifiedDate);
    }
}

public sealed class ApiResponseTests
{
    // ------------------------------------------------------------------ ApiResponse<T>

    [Fact]
    public void IsSuccess_WhenResultIsSuccess_ReturnsTrue()
    {
        var response = new ApiResponse<string> { Result = "success" };
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public void IsSuccess_WhenResultIsError_ReturnsFalse()
    {
        var response = new ApiResponse<string> { Result = "error" };
        Assert.False(response.IsSuccess);
    }

    [Fact]
    public void IsSuccess_WhenResultIsEmpty_ReturnsFalse()
    {
        var response = new ApiResponse<string> { Result = string.Empty };
        Assert.False(response.IsSuccess);
    }

    // ------------------------------------------------------------------ ApiListResponse<T>

    [Fact]
    public void ApiListResponse_IsSuccess_WhenResultIsSuccess_ReturnsTrue()
    {
        var response = new ApiListResponse<string> { Result = "success" };
        Assert.True(response.IsSuccess);
    }

    [Fact]
    public void ApiListResponse_IsSuccess_WhenResultIsError_ReturnsFalse()
    {
        var response = new ApiListResponse<string> { Result = "error" };
        Assert.False(response.IsSuccess);
    }

    [Fact]
    public void ApiListResponse_DefaultPagination_IsPageOneOfOne()
    {
        var response = new ApiListResponse<string>();
        Assert.Equal(1, response.Page);
        Assert.Equal(1, response.Pages);
    }
}
