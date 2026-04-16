namespace Blazing.Extensions.Http.Tests.UnitTests.Models;

[Trait("Category", "Unit")]
public class DownloadResultTests
{
    private static readonly Uri TestUrl = new("https://example.com/large-file.zip");

    [Fact]
    public void Ok_WithDefaultStatusCode_ReturnsSuccess()
    {
        // Act
        var result = DownloadResult.Ok();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ResumeToken.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void Ok_With206PartialContent_ReturnsSuccess()
    {
        // Act
        var result = DownloadResult.Ok(HttpStatusCode.PartialContent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        result.ResumeToken.Should().BeNull();
    }

    [Fact]
    public void Failed_WithStatusCodeAndMessage_ReturnsFailure()
    {
        // Act
        var result = DownloadResult.Failed(HttpStatusCode.NotFound, "Not Found: /file");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().Be("Not Found: /file");
        result.ResumeToken.Should().BeNull();
    }

    [Fact]
    public void Failed_WithStatus200WhenRangeRequested_CapturesRangeNotSupported()
    {
        // Act
        var result = DownloadResult.Failed(HttpStatusCode.OK, "Server does not support Range requests");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ErrorMessage.Should().Contain("Range");
        result.ResumeToken.Should().BeNull();
    }

    [Fact]
    public void Failed_WithNullStatusCode_StatusCodeIsNull()
    {
        // Act
        var result = DownloadResult.Failed(null, "Network error");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().BeNull();
        result.ResumeToken.Should().BeNull();
    }

    [Fact]
    public void Failed_WithException_CapturesException()
    {
        // Arrange
        var ex = new HttpRequestException("Connection refused");

        // Act
        var result = DownloadResult.Failed(null, ex.Message, ex);

        // Assert
        result.Exception.Should().BeSameAs(ex);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Cancelled_WithResumeToken_ReturnsTokenAndNullStatusCode()
    {
        // Arrange
        var token = new ResumeToken(TestUrl, 512L);

        // Act
        var result = DownloadResult.Cancelled(token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().BeNull();
        result.ResumeToken.Should().Be(token);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Cancelled_WithException_CapturesException()
    {
        // Arrange
        var token = new ResumeToken(TestUrl, 256L);
        var ex = new OperationCanceledException("user cancelled");

        // Act
        var result = DownloadResult.Cancelled(token, ex);

        // Assert
        result.Exception.Should().BeSameAs(ex);
        result.ResumeToken.Should().Be(token);
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Cancelled_DefaultException_ExceptionIsNull()
    {
        // Arrange
        var token = new ResumeToken(TestUrl, 128L);

        // Act
        var result = DownloadResult.Cancelled(token);

        // Assert
        result.Exception.Should().BeNull();
    }

    [Fact]
    public void DownloadResult_IsAssignableToResultBase()
    {
        // Act
        var result = DownloadResult.Ok();

        // Assert
        result.Should().BeAssignableTo<ResultBase>();
    }

    [Fact]
    public void Cancelled_ResumeTokenPreservesAllFields()
    {
        // Arrange
        var token = new ResumeToken(TestUrl, 1024L, ETag: "\"abc\"");

        // Act
        var result = DownloadResult.Cancelled(token);

        // Assert
        result.ResumeToken!.Url.Should().Be(TestUrl);
        result.ResumeToken.BytesWritten.Should().Be(1024L);
        result.ResumeToken.ETag.Should().Be("\"abc\"");
    }
}
