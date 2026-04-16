using System.Net;

namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents the outcome of a <see cref="HttpClientExtension"/> <c>GetAsync</c> operation.
/// Inherits <see cref="ResultBase"/> so callers never need try/catch on HTTP errors or cancellation.
/// </summary>
public sealed record GetResult : ResultBase
{
    private GetResult() { }

    /// <summary>Returns a successful result with the given <paramref name="statusCode"/>.</summary>
    /// <param name="statusCode">The HTTP status code received from the server (typically 200 OK).</param>
    public static GetResult Ok(HttpStatusCode statusCode = HttpStatusCode.OK)
        => new() { IsSuccess = true, StatusCode = statusCode };

    /// <summary>Returns a failed result with the given <paramref name="statusCode"/> and <paramref name="error"/> message.</summary>
    /// <param name="statusCode">The HTTP status code, or <c>null</c> when no response was received (e.g. cancellation, network failure).</param>
    /// <param name="error">A human-readable description of the failure (safe for display).</param>
    /// <param name="exception">The original exception, if the failure originated from a thrown exception; otherwise <c>null</c>.</param>
    public static GetResult Failed(HttpStatusCode? statusCode, string error, Exception? exception = null)
        => new() { IsSuccess = false, StatusCode = statusCode, ErrorMessage = error, Exception = exception };
}
