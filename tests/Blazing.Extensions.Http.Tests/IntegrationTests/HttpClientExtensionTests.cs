using Blazing.Extensions.Http.Tests.Fixtures;

namespace Blazing.Extensions.Http.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class HttpClientExtensionTests
{
    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond)
        => new HttpClient(new FakeHttpHandler(respond));

    private static Progress<TransferState> NoopProgress()
        => new Progress<TransferState>();

    // ── GetAsync tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_With200Response_ReturnsSuccess()
    {
        // Arrange
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Hello World")
        });
        using var dest = new MemoryStream();

        // Act
        var result = await client.GetAsync(new Uri("http://test.example.com/file"), dest, NoopProgress());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_With404Response_ReturnsFailure()
    {
        // Arrange
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Resource not found"),
            ReasonPhrase = "Not Found"
        });
        using var dest = new MemoryStream();

        // Act
        var result = await client.GetAsync(new Uri("http://test.example.com/missing"), dest, NoopProgress());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAsync_With200Response_WritesContentToStream()
    {
        // Arrange
        const string content = "streamed content";
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        });
        using var dest = new MemoryStream();

        // Act
        var result = await client.GetAsync(new Uri("http://test.example.com/file"), dest, NoopProgress());

        // Assert
        result.IsSuccess.Should().BeTrue();
        dest.Length.Should().BeGreaterThan(0);
    }

    // ── DownloadAsync tests ───────────────────────────────────────────────────

    [Fact]
    public async Task DownloadAsync_With200Response_ReturnsSuccess()
    {
        // Arrange
        const string content = "file content";
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content)
        });
        using var dest = new MemoryStream();

        // Act
        var result = await client.DownloadAsync(new Uri("http://test.example.com/file"), dest, NoopProgress());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ResumeToken.Should().BeNull();
        dest.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task DownloadAsync_With404Response_ReturnsFailure()
    {
        // Arrange
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not found"),
            ReasonPhrase = "Not Found"
        });
        using var dest = new MemoryStream();

        // Act
        var result = await client.DownloadAsync(new Uri("http://test.example.com/missing"), dest, NoopProgress());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
        result.ResumeToken.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DownloadAsync_WithResumeToken_SendsRangeHeader()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StringContent("remaining content")
            };
        });
        using var dest = new MemoryStream();
        var resumeToken = new ResumeToken(new Uri("http://test.example.com/file"), 512L, ETag: "\"abc\"");

        // Act
        var result = await client.DownloadAsync(
            new Uri("http://test.example.com/file"),
            dest,
            NoopProgress(),
            resumeToken: resumeToken);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Range.Should().NotBeNull();
        capturedRequest.Headers.Range!.Ranges.Should().ContainSingle();
        capturedRequest.Headers.Range.Ranges.Single().From.Should().Be(512L);
        capturedRequest.Headers.Range.Ranges.Single().To.Should().BeNull();
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAsync_ServerReturns200WhenRangeSent_ReturnsRangeNotSupported()
    {
        // Arrange — server ignores Range header and sends 200 (not 206)
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("full content from start")
        });
        using var dest = new MemoryStream();
        var resumeToken = new ResumeToken(new Uri("http://test.example.com/file"), 100L);

        // Act
        var result = await client.DownloadAsync(
            new Uri("http://test.example.com/file"),
            dest,
            NoopProgress(),
            resumeToken: resumeToken);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        result.ResumeToken.Should().BeNull();
        result.ErrorMessage.Should().Contain("Range");
    }

    [Fact]
    public async Task DownloadAsync_With206PartialContent_ReturnsSuccess()
    {
        // Arrange
        const string content = "resumed content";
        var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new StringContent(content)
        });
        using var dest = new MemoryStream();
        var resumeToken = new ResumeToken(new Uri("http://test.example.com/file"), 100L);

        // Act
        var result = await client.DownloadAsync(
            new Uri("http://test.example.com/file"),
            dest,
            NoopProgress(),
            resumeToken: resumeToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        result.ResumeToken.Should().BeNull();
    }

    [Fact]
    public async Task DownloadAsync_CancelledMidStream_ReturnsResumeTokenWithBytesWritten()
    {
        // Arrange — CancellationStream delivers 256 bytes then deterministically cancels the token
        using var cts = new CancellationTokenSource();
        var initialData = new byte[256];
        Random.Shared.NextBytes(initialData);

        var cancellationStream = new CancellationStream(initialData, cts);
        var client = CreateClient(_ =>
        {
            var content = new StreamContent(cancellationStream);
            content.Headers.ContentLength = 10_000; // report a larger total so bytesWritten < total
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var dest = new MemoryStream();

        // Act — CancellationStream cancels cts after 256 bytes; no timing dependency
        var result = await client.DownloadAsync(
            new Uri("http://test.example.com/large-file"),
            dest,
            NoopProgress(),
            bufferSize: 512,
            cancellationToken: cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().BeNull();
        result.ResumeToken.Should().NotBeNull();
        result.ResumeToken!.BytesWritten.Should().Be(256);
    }

    [Fact]
    public async Task DownloadAsync_WithIfRangeHeader_SendsETagInIfRange()
    {
        // Arrange
        const string etag = "\"version1\"";
        HttpRequestMessage? capturedRequest = null;
        var client = CreateClient(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new StringContent("partial")
            };
        });
        using var dest = new MemoryStream();
        var resumeToken = new ResumeToken(new Uri("http://test.example.com/file"), 100L, ETag: etag);

        // Act
        await client.DownloadAsync(
            new Uri("http://test.example.com/file"),
            dest,
            NoopProgress(),
            resumeToken: resumeToken);

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.TryGetValues("If-Range", out var values).Should().BeTrue();
        values.Should().ContainSingle(v => v == etag);
    }
}
