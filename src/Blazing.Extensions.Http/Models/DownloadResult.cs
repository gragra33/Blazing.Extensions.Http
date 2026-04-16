using System.Net;

namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents the outcome of a <see cref="HttpClientExtension"/> <c>DownloadAsync</c> operation.
/// Inherits <see cref="ResultBase"/> so callers never need try/catch on HTTP errors or cancellation.
/// When the download is cancelled mid-stream, <see cref="ResumeToken"/> is populated so callers can
/// offer to resume; pass it back to <c>DownloadAsync</c> to continue.
/// </summary>
public sealed record DownloadResult : ResultBase
{
    /// <summary>
    /// Gets the token required to resume this download, or <c>null</c> when the download
    /// completed successfully or failed without a resumable cancellation.
    /// Pass this to <c>DownloadAsync</c> to continue from the saved offset.
    /// </summary>
    public ResumeToken? ResumeToken { get; private init; }

    private DownloadResult() { }

    /// <summary>Returns a successful download result.</summary>
    /// <param name="statusCode">The HTTP status code received (typically 200 OK or 206 Partial Content).</param>
    public static DownloadResult Ok(HttpStatusCode statusCode = HttpStatusCode.OK)
        => new() { IsSuccess = true, StatusCode = statusCode };

    /// <summary>Returns a failed download result.</summary>
    /// <param name="statusCode">The HTTP status code, or <c>null</c> when no response was received (e.g. network failure).</param>
    /// <param name="error">A human-readable description of the failure (safe for display).</param>
    /// <param name="exception">The original exception, if the failure originated from a thrown exception; otherwise <c>null</c>.</param>
    public static DownloadResult Failed(HttpStatusCode? statusCode, string error, Exception? exception = null)
        => new() { IsSuccess = false, StatusCode = statusCode, ErrorMessage = error, Exception = exception };

    /// <summary>
    /// Returns a cancelled result carrying a <see cref="Models.ResumeToken"/> so the caller can offer to resume.
    /// <see cref="ResultBase.StatusCode"/> is <c>null</c> because cancellation occurs during streaming — not at the
    /// HTTP response level.
    /// </summary>
    /// <param name="resumeToken">The resume token capturing bytes written and server validators.</param>
    /// <param name="exception">The <see cref="OperationCanceledException"/> that triggered cancellation, for diagnostic logging.</param>
    public static DownloadResult Cancelled(ResumeToken resumeToken, OperationCanceledException? exception = null)
        => new() { IsSuccess = false, ResumeToken = resumeToken, Exception = exception };
}
