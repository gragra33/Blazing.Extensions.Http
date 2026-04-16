namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Captures the state needed to resume a previously cancelled download using HTTP Range Requests (RFC 7233).
/// Pass this token to <c>DownloadAsync</c> on <see cref="HttpClientExtension"/> to continue from the saved byte offset.
/// </summary>
/// <param name="Url">The URL of the resource being downloaded.</param>
/// <param name="BytesWritten">The absolute number of bytes already written to the destination stream.</param>
/// <param name="ETag">
/// The <c>ETag</c> validator from the original response, used with the <c>If-Range</c> request header.
/// <c>null</c> when the server did not return an <c>ETag</c>.
/// </param>
/// <param name="LastModified">
/// The <c>Last-Modified</c> validator from the original response, used as a fallback <c>If-Range</c> value
/// when <paramref name="ETag"/> is unavailable.
/// <c>null</c> when the server did not return a <c>Last-Modified</c> header.
/// </param>
public sealed record ResumeToken(
    Uri Url,
    long BytesWritten,
    string? ETag = null,
    DateTimeOffset? LastModified = null);
