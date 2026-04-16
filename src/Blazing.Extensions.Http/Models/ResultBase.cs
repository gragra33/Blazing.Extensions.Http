using System.Net;

namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Abstract base record providing the shared state for all HTTP operation results
/// in Blazing.Extensions.Http. Callers never need try/catch — every operation
/// returns a concrete subtype; inspect <see cref="IsSuccess"/> to branch.
/// </summary>
public abstract record ResultBase
{
    /// <summary>Gets a value indicating whether the HTTP operation succeeded.</summary>
    public bool IsSuccess { get; protected init; }

    /// <summary>
    /// Gets the HTTP status code returned by the server, or <c>null</c> when no
    /// response was received (e.g. cancellation or network-level failure).
    /// </summary>
    public HttpStatusCode? StatusCode { get; protected init; }

    /// <summary>Gets the error message when <see cref="IsSuccess"/> is <c>false</c>; otherwise <c>null</c>.</summary>
    public string? ErrorMessage { get; protected init; }

    /// <summary>
    /// Gets the original exception when <see cref="IsSuccess"/> is <c>false</c> and the failure
    /// originated from a thrown exception (e.g. <see cref="System.Net.Sockets.SocketException"/>,
    /// <see cref="OperationCanceledException"/>).
    /// <c>null</c> for HTTP-status failures (4xx/5xx) where no exception was thrown.
    /// Use this for detailed diagnostics or structured logging; prefer <see cref="ErrorMessage"/> for display.
    /// </summary>
    public Exception? Exception { get; protected init; }

    /// <summary>Initialises a new instance of <see cref="ResultBase"/>.</summary>
    protected ResultBase() { }
}
