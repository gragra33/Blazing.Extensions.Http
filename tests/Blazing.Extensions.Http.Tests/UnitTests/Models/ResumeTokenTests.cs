namespace Blazing.Extensions.Http.Tests.UnitTests.Models;

[Trait("Category", "Unit")]
public class ResumeTokenTests
{
    private static readonly Uri TestUrl = new("https://example.com/large-file.zip");

    [Fact]
    public void Constructor_AssignsUrlAndBytesWritten()
    {
        // Act
        var token = new ResumeToken(TestUrl, 1024L);

        // Assert
        token.Url.Should().Be(TestUrl);
        token.BytesWritten.Should().Be(1024L);
    }

    [Fact]
    public void Constructor_DefaultETagIsNull()
    {
        // Act
        var token = new ResumeToken(TestUrl, 512L);

        // Assert
        token.ETag.Should().BeNull();
    }

    [Fact]
    public void Constructor_DefaultLastModifiedIsNull()
    {
        // Act
        var token = new ResumeToken(TestUrl, 512L);

        // Assert
        token.LastModified.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithETag_AssignsETag()
    {
        // Arrange
        const string etag = "\"abc123\"";

        // Act
        var token = new ResumeToken(TestUrl, 256L, ETag: etag);

        // Assert
        token.ETag.Should().Be(etag);
        token.LastModified.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithLastModified_AssignsLastModified()
    {
        // Arrange
        var lastModified = new DateTimeOffset(2024, 6, 1, 10, 0, 0, TimeSpan.Zero);

        // Act
        var token = new ResumeToken(TestUrl, 256L, LastModified: lastModified);

        // Assert
        token.LastModified.Should().Be(lastModified);
        token.ETag.Should().BeNull();
    }

    [Fact]
    public void RecordEquality_IdenticalValues_AreEqual()
    {
        // Arrange
        var token1 = new ResumeToken(TestUrl, 1024L, "\"etag1\"");
        var token2 = new ResumeToken(TestUrl, 1024L, "\"etag1\"");

        // Assert
        token1.Should().Be(token2);
    }

    [Fact]
    public void RecordEquality_DifferentBytesWritten_AreNotEqual()
    {
        // Arrange
        var token1 = new ResumeToken(TestUrl, 512L);
        var token2 = new ResumeToken(TestUrl, 1024L);

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void RecordEquality_DifferentETag_AreNotEqual()
    {
        // Arrange
        var token1 = new ResumeToken(TestUrl, 1024L, "\"etag1\"");
        var token2 = new ResumeToken(TestUrl, 1024L, "\"etag2\"");

        // Assert
        token1.Should().NotBe(token2);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1L)]
    [InlineData(long.MaxValue)]
    public void BytesWritten_VariousValues_RoundTrip(long bytes)
    {
        // Act
        var token = new ResumeToken(TestUrl, bytes);

        // Assert
        token.BytesWritten.Should().Be(bytes);
    }
}
