# Resume After Cancellation - Plan

## Overview

This feature adds **resumable download support** to `Blazing.Extensions.Http` across the core library and all three sample applications (WPF, WinForms, Console). Currently, when a download is cancelled — by the user, a timeout, or a network event — the partially-downloaded file is discarded and any subsequent retry must restart from byte 0. This wastes bandwidth, adds latency, and degrades user experience for large files.

The implementation uses **HTTP Range Requests (RFC 7233)**. When a download is cancelled the library captures a `ResumeToken` containing the URL, the absolute number of bytes already written, and the server's `ETag` / `Last-Modified` validator. On resume, a new request is sent with `Range: bytes=<offset>-` and `If-Range: <etag>`, the destination stream is seeked to the resume point, and `TransferState` is pre-seeded so that progress percentage, remaining-time, and remaining-size calculations all reflect the **full** download — not just the resumed portion.

**Goals:**

- Enable seamless cancel → resume across all three sample UIs
- Introduce a shared `ResultBase` abstract record carrying `IsSuccess`, `HttpStatusCode?`, `ErrorMessage`, and `Exception` for both result types
- **Breaking change**: `GetAsync` returns `GetResult` (inherits `ResultBase`) instead of `Task` — callers get status codes and error messages without try/catch
- Expose `DownloadAsync` returning `DownloadResult` (also inherits `ResultBase`) with a `ResumeToken` on cancellation
- Pre-seed `TransferState` so existing calc methods work correctly after a resume with zero modification

**Success Criteria:**

- Start 4 parallel downloads → cancel one individually → Resume button/prompt appears → click/confirm Resume → download continues from saved byte offset (verified by the `Content-Range` response header)
- Cancelling again after a partial resume yields a new `ResumeToken` (chainable cancel → resume)
- Servers that don't support `Range:` are detected via `result.StatusCode == HttpStatusCode.OK` — no separate boolean flag needed
- `GetResult` and `DownloadResult` both expose `IsSuccess`, `HttpStatusCode?`, and `string? ErrorMessage` through `ResultBase`
- All three sample apps updated to consume `GetResult` from their existing `GetAsync` calls

---

## Architecture

### Technology Stack

- **.NET 10.0 / C# 14** — nullable reference types, `LangVersion: latest`, primary constructors
- **`System.Net.Http`** — `HttpRequestMessage` + `SendAsync` for per-request Range headers; `HttpCompletionOption.ResponseHeadersRead` for streaming
- **`System.Diagnostics.Stopwatch`** — nanosecond-resolution latency tracking (unchanged)
- **WPF Sample** — `CommunityToolkit.Mvvm`, `ObservableObject`, `RelayCommand`, Blazing DI auto-register
- **WinForms Sample** — code-behind `Control` pattern, custom event `ResumeRequested`
- **Console Sample** — `Console.CancelKeyPress`, interactive y/n resume prompt

### Design Patterns

- **`ResultBase` abstract record** — shared base for all HTTP operation results: `bool IsSuccess`, `HttpStatusCode? StatusCode`, `string? ErrorMessage`, `Exception? Exception`. Both `GetResult` and `DownloadResult` inherit it, enabling polymorphic handling and consistent logging across the library
- **`HttpStatusCode?` is nullable** — cancellations and network-level failures never produce an HTTP response, so `StatusCode` is `null` in those cases; always set on server responses (200, 206, 404, etc.)
- **`Exception?` for deep diagnostics** — HTTP-status failures (404, 429, etc.) set only `ErrorMessage` (no exception thrown); network-level failures and cancellations also capture the original `Exception` so callers can log stack traces, inspect inner exceptions (`SocketException`, etc.), or re-wrap. `ErrorMessage` is always a human-readable string; `Exception` is the raw throwable
- **Nothing escapes** — both `InternalGetAsync` and `InternalDownloadAsync` catch all exceptions and return a `Failed`/`Cancelled` result; no `throw` in any error path. The `catch (Exception ex)` that previously re-threw now returns `DownloadResult.Failed(null, ex.Message, ex)`
- **`ServerRangeNotSupported` eliminated** — callers detect this scenario via `result.StatusCode == HttpStatusCode.OK` when they sent a range request; no separate boolean flag needed
- **Breaking change: `GetAsync` → `GetResult`** — `GetAsync` and `InternalGetAsync` are updated to return `Task<GetResult>` (no exception thrown on HTTP error); `GetResult.IsSuccess` and `GetResult.StatusCode` replace the `HttpRequestException` pattern
- **`DownloadAsync` returns `DownloadResult`** — catches `OperationCanceledException` internally; returns `DownloadResult` with `ResumeToken` on cancel, or `IsSuccess = false` with `StatusCode` on HTTP error
- **Static factories** — both result types expose `Ok(HttpStatusCode)`, `Failed(HttpStatusCode?, string, Exception? = null)`, and `DownloadResult` additionally exposes `Cancelled(ResumeToken, OperationCanceledException?)` for clarity at call sites
- **Polly / resilience** — Polly belongs **outside** this library, configured at the `IHttpClientFactory` level via `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler()`). Since both methods accept `HttpClient`, any resilience pipeline on the named client is applied transparently. **Important:** do not attach retry to the download client — Polly retry would restart from byte 0, directly conflicting with the `ResumeToken` resume mechanism. Recommended pattern: two named clients — `"BlazingGet"` (with retry/circuit-breaker) and `"BlazingDownload"` (no retry; let `ResumeToken` handle resilience)
- **Per-request headers** — uses `HttpRequestMessage` + `SendAsync` to attach `Range` and `If-Range` headers, avoiding the race condition of mutating `DefaultRequestHeaders` in concurrent downloads
- **Pre-seeded `TransferState`** — `Start(totalBytes, startOffset)` sets `Total.Transferred = startOffset` so all three existing calc methods (`CalcProgressPercentage`, `CalcRemainingSize`, `CalcEstimatedRemainingTime`) work for resumed downloads without modification
- **WPF MVVM** — `IsCancelled` observable property on `DownloadItemViewModel` is distinct from `IsError`; `CanResume` drives button `Visibility` via `BooleanToVisibilityConverter`; `ResumeDownloadCommand(DownloadItemViewModel)` in `MainViewModel` dispatches the resume
- **WinForms event** — `DownloadProgressControl` exposes `event EventHandler<ResumeToken> ResumeRequested`; `MainForm` subscribes and dispatches the resume `Task`
- **Console loop** — `Ctrl+C` hooks cancel all downloads; post-run prompt "Resume cancelled downloads? (y/n)" feeds into `ResumeDownloadsAsync`

### Key Components

| Component                                              | Responsibility                                                                                                                                                                |
| ------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ResultBase`                                           | Abstract record: `bool IsSuccess`, `HttpStatusCode? StatusCode`, `string? ErrorMessage`, `Exception? Exception` — inherited by both result types                              |
| `GetResult`                                            | Sealed record inheriting `ResultBase`; returned by all `GetAsync` overloads; static factories `Ok(statusCode)`, `Failed(statusCode, error, exception?)`                       |
| `DownloadResult`                                       | Sealed record inheriting `ResultBase`; adds `ResumeToken?`; static factories `Ok(statusCode)`, `Failed(statusCode, error, exception?)`, `Cancelled(resumeToken, exception?)`  |
| `ResumeToken`                                          | Immutable record carrying `Uri Url`, `long BytesWritten`, `string? ETag`, `DateTimeOffset? LastModified`                                                                      |
| `TransferState.Start(totalBytes, startOffset)`         | Pre-seeds `Total.Transferred` so all existing calc methods work on resumes                                                                                                    |
| `HttpClientExtension.InternalGetAsync`                 | Updated to return `Task<GetResult>`; catches HTTP errors and cancellation; never throws                                                                                       |
| `HttpClientExtension.GetAsync` (×4 overloads)          | **Breaking change** — now return `Task<GetResult>`                                                                                                                            |
| `HttpClientExtension.InternalDownloadAsync`            | Core loop: `HttpRequestMessage` + `SendAsync`, Range header, 206 vs 200 detection via `StatusCode`, absolute `bytesWritten` tracking, cancellation → `Cancelled(resumeToken)` |
| `HttpClientExtension.DownloadAsync` (×4 overloads)     | Public surface; delegates to `InternalDownloadAsync`; returns `Task<DownloadResult>`                                                                                          |
| WPF `DownloadService.DownloadFileAsync` (new overload) | Opens file with `FileMode.OpenOrCreate`, seeks to `BytesWritten`, calls `DownloadAsync`                                                                                       |
| WPF `DownloadItemViewModel`                            | Adds `ResumeToken?`, `IsCancelled`, `CanResume`, `MarkCancelled()`, `ResetForResume()`                                                                                        |
| WPF `MainViewModel.ResumeDownloadCommand`              | Creates new linked CTS, calls service resumable overload, updates VM state                                                                                                    |
| WinForms `DownloadProgressControl`                     | Adds orange `_resumeButton`, `ResumeRequested` event, `MarkCancelled()`, `ResetForResume()`, `DestinationPath`                                                                |
| WinForms `MainForm`                                    | Subscribes `ResumeRequested`, creates new CTS, dispatches resume `Task`                                                                                                       |
| Console `FileTransferHelper`                           | Threads `CancellationToken`, switches to `DownloadAsync`, returns `Task<DownloadResult[]>`, adds `ResumeDownloadsAsync`                                                       |
| Console `Program`                                      | `CancelKeyPress` → CTS cancel, post-run resume prompt loop                                                                                                                    |
| Console `Progress`                                     | Adds `MarkCancelled()` and `MarkResuming()` console-row writers                                                                                                               |

### Data Flow — Library Core

```
Caller
  └─▶ HttpClientExtension.GetAsync(client, url, destStream, progress, ...)
        └─▶ InternalGetAsync
              ├─ client.GetAsync(url, ResponseHeadersRead, ct)
              ├─ On HTTP error   → return GetResult.Failed(statusCode, errorContent)
              ├─ On cancel       → return GetResult.Failed(statusCode: null, "Cancelled")
              ├─ Read loop
              └─ On success      → return GetResult.Ok(200)

Caller
  └─▶ HttpClientExtension.DownloadAsync(client, url, destStream, progress, resumeToken?, ...)
        └─▶ InternalDownloadAsync
              ├─ Build HttpRequestMessage
              ├─ If resumeToken != null → add Range: bytes=N- + If-Range: headers
              ├─ client.SendAsync(request, ResponseHeadersRead, ct)
              ├─ Capture etag + lastModified from response headers
              ├─ If resumeToken != null && StatusCode == 200
              │     └─▶ return DownloadResult.Failed(200, "Server does not support Range requests")
              ├─ If !IsSuccessStatusCode → return DownloadResult.Failed(statusCode, errorContent)
              ├─ If 206 → startOffset = resumeToken.BytesWritten
              ├─ transferState.Start(totalBytes, startOffset)   ← pre-seeded
              ├─ Read loop: bytesWritten += bytesRead (never reset)
              │             position    += bytesRead (reset per interval)
              ├─ On OperationCanceledException
              │     └─▶ return DownloadResult.Cancelled(new ResumeToken(url, bytesWritten, etag))
              └─ On success → return DownloadResult.Ok(206 or 200)
```

### Data Flow — WPF Sample

```
User clicks Cancel (DownloadItemViewModel.CancelDownloadCommand)
  └─▶ individualCts.Cancel()
        └─▶ DownloadAsync returns DownloadResult.Cancelled(resumeToken)
              └─▶ MainViewModel.DownloadFileAsync catches result
                    └─▶ downloadVm.MarkCancelled(result.ResumeToken)
                          └─▶ IsCancelled=true, CanResume=true, Resume button visible

User clicks Resume (MainViewModel.ResumeDownloadCommand)
  └─▶ Creates new linked CTS
        └─▶ vm.ResetForResume() + vm.SetCancellationTokenSource(newCts)
              └─▶ DownloadService.DownloadFileAsync(url, path, progress, latency, vm.ResumeToken, ct)
                    └─▶ FileMode.OpenOrCreate + Seek(BytesWritten)
                          └─▶ client.DownloadAsync(..., resumeToken, ...)
```

### Integration Points

- **HTTP protocol**: `Range: bytes=N-` (open-ended range), `If-Range: <etag>` conditional, `206 Partial Content` vs `200 OK` detection via `result.StatusCode`
- **File I/O**: `FileMode.OpenOrCreate` + `Stream.Seek(offset, SeekOrigin.Begin)` on resume; `File.Create` on fresh download; partial file **not deleted** on cancellation
- **Breaking change scope**: All four `GetAsync` overloads and `InternalGetAsync` change return type from `Task` to `Task<GetResult>`; all sample call sites updated accordingly
- **`TransferState.Start()`**: Backward-compatible via `startOffset = 0` default; existing one-arg callers unaffected

---

## Folder & Files

### Directory Structure

```
Blazing.Extensions.Http/
├── docs/
│   └── resume-update-plan.md               ← this document
└── src/
    ├── Blazing.Extensions.Http/
    │   ├── HttpClientExtension.cs           ← add InternalDownloadAsync + DownloadAsync ×4
    │   └── Models/
    │       ├── ResultBase.cs               ← NEW
    │       ├── GetResult.cs                ← NEW
    │       ├── ResumeToken.cs              ← NEW
    │       ├── DownloadResult.cs           ← NEW
    │       └── TransferState.cs             ← update Start() + add StartOffset
    └── samples/
        ├── WpfExample/
        │   ├── Services/
        │   │   └── DownloadService.cs       ← new resumable overload
        │   ├── ViewModels/
        │   │   ├── DownloadItemViewModel.cs ← ResumeToken?, IsCancelled, CanResume, MarkCancelled()
        │   │   └── MainViewModel.cs         ← ResumeDownloadCommand, destination path persistence
        │   └── Views/
        │       └── MainWindow.xaml          ← Resume button, status badge differentiation
        ├── WinFormsExample/
        │   ├── DownloadService.cs           ← new resumable overload
        │   ├── DownloadProgressControl.cs   ← ResumeRequested event, orange resume button, MarkCancelled()
        │   └── MainForm.cs                  ← ResumeRequested handler, destination path persistence
        └── ConsoleExample/
            ├── FileTransferHelper.cs        ← CancellationToken wiring, DownloadAsync, ResumeDownloadsAsync
            ├── Program.cs                   ← CancelKeyPress hook, resume prompt loop
            └── Progress.cs                  ← MarkCancelled(), MarkResuming()
```

### File Responsibilities

| File                                             | Change       | Purpose                                                                                                                                      |
| ------------------------------------------------ | ------------ | -------------------------------------------------------------------------------------------------------------------------------------------- |
| `Models/ResultBase.cs`                           | **New**      | Abstract record: `bool IsSuccess`, `HttpStatusCode? StatusCode`, `string? ErrorMessage`, `Exception? Exception`                              |
| `Models/GetResult.cs`                            | **New**      | Sealed record inheriting `ResultBase`; static factories `Ok(statusCode)`, `Failed(statusCode, error)`                                        |
| `Models/ResumeToken.cs`                          | **New**      | Immutable record: `Uri Url`, `long BytesWritten`, `string? ETag`, `DateTimeOffset? LastModified`                                             |
| `Models/DownloadResult.cs`                       | **New**      | Sealed record inheriting `ResultBase`; adds `ResumeToken?`; static factories `Ok`, `Failed`, `Cancelled`                                     |
| `Models/TransferState.cs`                        | **Modified** | Add `StartOffset` property; update `Start()` to accept `startOffset = 0` and pre-seed `Total.Transferred`                                    |
| `HttpClientExtension.cs`                         | **Modified** | **Breaking**: `GetAsync` × 4 return `Task<GetResult>`; `InternalGetAsync` updated; add `InternalDownloadAsync` + 4 `DownloadAsync` overloads |
| `WpfExample/Services/DownloadService.cs`         | **Modified** | New `Task<DownloadResult>` overload accepting `ResumeToken?`; opens file correctly for resume                                                |
| `WpfExample/ViewModels/DownloadItemViewModel.cs` | **Modified** | Add `ResumeToken?`, `IsCancelled`, `CanResume`, `DestinationPath`, `MarkCancelled()`, `ResetForResume()`                                     |
| `WpfExample/ViewModels/MainViewModel.cs`         | **Modified** | Add `ResumeDownloadCommand`, `_destinationPaths` list, resume dispatch; `MarkError` → `MarkCancelled` on cancel                              |
| `WpfExample/Views/MainWindow.xaml`               | **Modified** | Resume button per download item; `IsError`/`IsCancelled`/`IsComplete` status badges                                                          |
| `WinFormsExample/DownloadService.cs`             | **Modified** | Same resumable overload as WPF                                                                                                               |
| `WinFormsExample/DownloadProgressControl.cs`     | **Modified** | Orange `_resumeButton`, `ResumeRequested` event, `MarkCancelled()`, `ResetForResume()`, `DestinationPath`                                    |
| `WinFormsExample/MainForm.cs`                    | **Modified** | `ResumeRequested` subscription, `_destinationPaths`, resume task dispatch; no file delete on cancel                                          |
| `ConsoleExample/FileTransferHelper.cs`           | **Modified** | Thread CT, switch to `DownloadAsync`, return `Task<DownloadResult[]>`, add `ResumeDownloadsAsync`                                            |
| `ConsoleExample/Program.cs`                      | **Modified** | `CancelKeyPress` hook, CTS, resume prompt loop, per-file status summary                                                                      |
| `ConsoleExample/Progress.cs`                     | **Modified** | `MarkCancelled()` and `MarkResuming()` at fixed cursor row                                                                                   |

### Naming Conventions

- New models follow existing `sealed record` pattern in the `Models/` folder; `ResultBase` is the shared abstract base — no interface needed
- `GetResult` and `DownloadResult` use static factory methods (`Ok`, `Failed`, `Cancelled`) — no public constructors
- New `DownloadAsync` overloads mirror existing `GetAsync` overload set (Uri + string, with/without headers dict)
- WPF: `MarkCancelled` / `ResetForResume` parallel existing `MarkComplete` / `MarkError`
- WinForms: `ResumeRequested` event follows .NET `EventHandler<TEventArgs>` convention
- Console: `MarkCancelled()` / `MarkResuming()` parallel existing `Report()` / `CompactReport()` on `Progress`

---

## Code Snippets

### ResultBase.cs

```csharp
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
```

### GetResult.cs

```csharp
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
    public static GetResult Ok(HttpStatusCode statusCode = HttpStatusCode.OK)
        => new() { IsSuccess = true, StatusCode = statusCode };

    /// <summary>Returns a failed result with the given <paramref name="statusCode"/> and <paramref name="error"/> message.</summary>
    /// <param name="statusCode">The HTTP status code, or <c>null</c> when no response was received.</param>
    /// <param name="error">A human-readable description of the failure (safe for display).</param>
    /// <param name="exception">The original exception, if the failure originated from a thrown exception; otherwise <c>null</c>.</param>
    public static GetResult Failed(HttpStatusCode? statusCode, string error, Exception? exception = null)
        => new() { IsSuccess = false, StatusCode = statusCode, ErrorMessage = error, Exception = exception };
}
```

### Scenario Matrix

| Scenario                                           | `IsSuccess` | `StatusCode` | `ErrorMessage`                             | `Exception`                  | `ResumeToken` |
| -------------------------------------------------- | ----------- | ------------ | ------------------------------------------ | ---------------------------- | ------------- |
| GET 200 OK, complete                               | `true`      | `200`        | `null`                                     | `null`                       | —             |
| Download 206 Partial Content, resumed              | `true`      | `206`        | `null`                                     | `null`                       | `null`        |
| Download 200 OK when range sent (no Range support) | `false`     | `200`        | `"Server does not support Range requests"` | `null`                       | `null`        |
| Cancelled mid-download                             | `false`     | `null`       | `null`                                     | `OperationCanceledException` | _(set)_       |
| 404 Not Found                                      | `false`     | `404`        | `"Not Found: ..."`                         | `null`                       | `null`        |
| 403 Forbidden                                      | `false`     | `403`        | `"Forbidden: ..."`                         | `null`                       | `null`        |
| 429 Too Many Requests                              | `false`     | `429`        | `"Too Many Requests: ..."`                 | `null`                       | `null`        |
| Network exception (no response)                    | `false`     | `null`       | `"Connection refused"`                     | `SocketException` (or inner) | `null`        |

---

### ResumeToken.cs

```csharp
namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Captures the state needed to resume a previously cancelled download.
/// </summary>
/// <param name="Url">The URL of the resource being downloaded.</param>
/// <param name="BytesWritten">The absolute number of bytes already written to the destination stream.</param>
/// <param name="ETag">The ETag validator from the original response, used with If-Range.</param>
/// <param name="LastModified">The Last-Modified validator, used when ETag is unavailable.</param>
public sealed record ResumeToken(
    Uri Url,
    long BytesWritten,
    string? ETag = null,
    DateTimeOffset? LastModified = null);
```

### DownloadResult.cs

```csharp
using System.Net;

namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents the outcome of a <see cref="HttpClientExtension"/> <c>DownloadAsync</c> operation.
/// Inherits <see cref="ResultBase"/> so callers never need try/catch on HTTP errors or cancellation.
/// When the download is cancelled mid-stream, <see cref="ResumeToken"/> is populated.
/// </summary>
public sealed record DownloadResult : ResultBase
{
    /// <summary>
    /// Gets the token required to resume this download, or <c>null</c> when the
    /// download completed or failed without a resumable cancellation.
    /// </summary>
    public ResumeToken? ResumeToken { get; private init; }

    private DownloadResult() { }

    /// <summary>Returns a successful download result.</summary>
    /// <param name="statusCode">The HTTP status code received (typically 200 or 206).</param>
    public static DownloadResult Ok(HttpStatusCode statusCode = HttpStatusCode.OK)
        => new() { IsSuccess = true, StatusCode = statusCode };

    /// <summary>Returns a failed download result.</summary>
    /// <param name="statusCode">The HTTP status code, or <c>null</c> when no response was received.</param>
    /// <param name="error">A human-readable description of the failure (safe for display).</param>
    /// <param name="exception">The original exception, if the failure originated from a thrown exception; otherwise <c>null</c>.</param>
    public static DownloadResult Failed(HttpStatusCode? statusCode, string error, Exception? exception = null)
        => new() { IsSuccess = false, StatusCode = statusCode, ErrorMessage = error, Exception = exception };

    /// <summary>
    /// Returns a cancelled result carrying a <see cref="Models.ResumeToken"/> so the
    /// caller can offer to resume. <see cref="ResultBase.StatusCode"/> is <c>null</c> because
    /// cancellation occurs before or during streaming — not at the HTTP response level.
    /// </summary>
    /// <param name="resumeToken">The resume token capturing bytes written and server validators.</param>
    /// <param name="exception">The <see cref="OperationCanceledException"/> that triggered cancellation, for diagnostic logging.</param>
    public static DownloadResult Cancelled(ResumeToken resumeToken, OperationCanceledException? exception = null)
        => new() { IsSuccess = false, ResumeToken = resumeToken, Exception = exception };
}
```

### TransferState.cs — updated Start()

```csharp
/// <summary>
/// Gets the byte offset from which this transfer started (non-zero on a resume).
/// </summary>
public long StartOffset { get; private set; }

/// <summary>
/// Marks the start of the transfer, sets the total bytes, and optionally pre-seeds
/// <see cref="Transfer.Transferred"/> to <paramref name="startOffset"/> so that all
/// progress calculations reflect the full download when resuming.
/// </summary>
public void Start(long? totalBytes, long startOffset = 0)
{
    StartTime = DateTimeOffset.Now;
    TotalBytes = totalBytes ?? 0D;
    StartOffset = startOffset;
    if (startOffset > 0)
    {
        Total.Transferred = startOffset;
    }
}
```

### HttpClientExtension.cs — InternalDownloadAsync (core)

```csharp
private static async Task<DownloadResult> InternalDownloadAsync(
    HttpClient client,
    Uri url,
    Stream destStream,
    IProgress<TransferState> progress,
    int interval,
    int bufferSize,
    LatencyTracker? latencyTracker,
    ResumeToken? resumeToken,
    CancellationToken cancellationToken)
{
    HttpResponseMessage? response = null;
    string? etag = null;
    DateTimeOffset? lastModified = null;
    long startOffset = resumeToken?.BytesWritten ?? 0L;

    try
    {
        long requestStartTicks = Stopwatch.GetTimestamp();
        using HttpRequestMessage request = new(HttpMethod.Get, url);

        if (resumeToken != null)
        {
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeToken.BytesWritten, null);
            string? ifRange = resumeToken.ETag ?? resumeToken.LastModified?.ToString("R");
            if (ifRange != null)
                request.Headers.TryAddWithoutValidation("If-Range", ifRange);
        }

        response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        // Capture validators for future resume tokens
        etag = response.Headers.ETag?.Tag;
        lastModified = response.Content.Headers.LastModified;

        // If we requested a range but got 200 OK, the server doesn't support Range requests
        if (resumeToken != null && response.StatusCode == System.Net.HttpStatusCode.OK)
            return DownloadResult.Failed(response.StatusCode, "Server does not support Range requests");

        if (!response.IsSuccessStatusCode)
        {
            string errorContent = string.Empty;
            try { errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (HttpRequestException) { }
            return DownloadResult.Failed(response.StatusCode, $"{response.ReasonPhrase}: {errorContent}");
        }

        long? length = response.Content.Headers.ContentLength;
        long? totalBytes = response.Content.Headers.ContentRange?.Length ?? length;

        Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        int position = 0;
        int bytesRead;
        long bytesWritten = startOffset;  // absolute; never reset
        byte[] buffer = new byte[bufferSize];

        TransferState transferState = new();
        transferState.Start(totalBytes ?? 0, startOffset);

        Stopwatch stopwatch = Stopwatch.StartNew();
        long lastReport = 0;
        long lastPacketTicks = requestStartTicks;
        double tickFrequency = 1_000_000_000.0 / Stopwatch.Frequency;

        while ((bytesRead = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            long nowTicks = Stopwatch.GetTimestamp();
            if (latencyTracker != null)
            {
                double packetNs = (nowTicks - lastPacketTicks) * tickFrequency;
                latencyTracker.UpdatePacketLatency(packetNs);
                transferState.Latency = latencyTracker;
            }
            lastPacketTicks = nowTicks;

            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            position += bytesRead;
            bytesWritten += bytesRead;

            long now = stopwatch.ElapsedMilliseconds;
            if (now - lastReport >= interval)
            {
                lastReport = now;
                progress.Report(transferState.Update(position));
                position = 0;
            }
        }

        transferState.Stop();
        progress.Report(transferState.Update(position));
        return DownloadResult.Ok(response.StatusCode);
    }
    catch (OperationCanceledException ex)
    {
        // Return a resume token — StatusCode is null (no HTTP response involved in cancellation)
        return DownloadResult.Cancelled(new ResumeToken(url, bytesWritten, etag, lastModified), ex);
    }
    catch (Exception ex)
    {
        response?.Dispose();
        // Never re-throw — return a Failed result so callers never need try/catch
        return DownloadResult.Failed(null, ex.Message, ex);
    }
}
```

### HttpClientExtension.cs — public DownloadAsync overloads

```csharp
/// <summary>Downloads with resumable cancellation support. Returns a <see cref="DownloadResult"/> instead of throwing on cancellation.</summary>
public static async Task<DownloadResult> DownloadAsync(
    this HttpClient client,
    Uri url,
    Stream destStream,
    IProgress<TransferState> progress,
    ResumeToken? resumeToken = null,
    int interval = 100,
    int bufferSize = 512,
    LatencyTracker? latencyTracker = null,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(client);
    ArgumentNullException.ThrowIfNull(destStream);
    ArgumentNullException.ThrowIfNull(progress);
    return await InternalDownloadAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, resumeToken, cancellationToken).ConfigureAwait(false);
}

// string overload — delegates to Uri overload
public static async Task<DownloadResult> DownloadAsync(
    this HttpClient client,
    string url,
    Stream destStream,
    IProgress<TransferState> progress,
    ResumeToken? resumeToken = null,
    int interval = 100,
    int bufferSize = 512,
    LatencyTracker? latencyTracker = null,
    CancellationToken cancellationToken = default)
    => await DownloadAsync(client, new Uri(url), destStream, progress, resumeToken, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);

// Uri + headers overload
public static async Task<DownloadResult> DownloadAsync(
    this HttpClient client,
    Uri url,
    Stream destStream,
    IProgress<TransferState> progress,
    ResumeToken? resumeToken,
    int interval,
    int bufferSize,
    LatencyTracker? latencyTracker,
    IDictionary<string, string>? headers,
    CancellationToken cancellationToken = default)
{
    ArgumentNullException.ThrowIfNull(client);
    ArgumentNullException.ThrowIfNull(destStream);
    ArgumentNullException.ThrowIfNull(progress);
    // headers applied per-request in InternalDownloadAsync when using HttpRequestMessage
    return await InternalDownloadAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, resumeToken, cancellationToken, headers).ConfigureAwait(false);
}

// string + headers overload
public static async Task<DownloadResult> DownloadAsync(
    this HttpClient client,
    string url,
    Stream destStream,
    IProgress<TransferState> progress,
    ResumeToken? resumeToken,
    int interval,
    int bufferSize,
    LatencyTracker? latencyTracker,
    IDictionary<string, string>? headers,
    CancellationToken cancellationToken = default)
    => await DownloadAsync(client, new Uri(url), destStream, progress, resumeToken, interval, bufferSize, latencyTracker, headers, cancellationToken).ConfigureAwait(false);
```

### WPF — DownloadItemViewModel additions

```csharp
[ObservableProperty]
private bool _isCancelled;

[ObservableProperty]
private bool _canResume;

public ResumeToken? ResumeToken { get; private set; }
public string? DestinationPath { get; set; }

public void MarkCancelled(ResumeToken? resumeToken)
{
    IsCancelled = true;
    IsError = false;
    CanCancel = false;
    ResumeToken = resumeToken;
    CanResume = resumeToken != null;
}

public void ResetForResume()
{
    IsCancelled = false;
    CanResume = false;
    ResumeToken = null;
    ProgressPercentage = 0;
    ProgressText = "0%";
}
```

### WPF — MainViewModel.ResumeDownloadCommand

```csharp
[RelayCommand]
private async Task ResumeDownloadAsync(DownloadItemViewModel vm)
{
    if (vm.ResumeToken is null || vm.DestinationPath is null)
        return;

    int index = Downloads.IndexOf(vm);
    var individualCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellationTokenSource!.Token);

    // Replace the old (cancelled/disposed) CTS
    _individualCancellationTokenSources[index].Dispose();
    _individualCancellationTokenSources[index] = individualCts;

    vm.ResetForResume();
    vm.SetCancellationTokenSource(individualCts);

    await ResumeFileAsync(index, vm, individualCts.Token);
}

private async Task ResumeFileAsync(int index, DownloadItemViewModel vm, CancellationToken ct)
{
    var resumeToken = vm.ResumeToken!;
    try
    {
        var latencyTracker = new LatencyTracker();
        var progress = new Progress<TransferState>(state =>
            Application.Current.Dispatcher.Invoke(() =>
            {
                vm.UpdateProgress(state);
                UpdateStatistics(state);
            }));

        DownloadResult result = await _downloadService.DownloadFileAsync(
            resumeToken.Url, vm.DestinationPath!, progress, latencyTracker, resumeToken, ct);

        Application.Current.Dispatcher.Invoke(() =>
        {
            if (result.IsSuccess)
                vm.MarkComplete();
            else if (result.ResumeToken != null)
                vm.MarkCancelled(result.ResumeToken);   // chainable cancel → resume
            else
                vm.MarkError();
        });
    }
    catch (Exception)
    {
        Application.Current.Dispatcher.Invoke(() => vm.MarkError());
    }
}
```

### WinForms — DownloadProgressControl additions

```csharp
// New field
private Button _resumeButton = null!;
public string? DestinationPath { get; set; }
public event EventHandler<ResumeToken>? ResumeRequested;
private ResumeToken? _resumeToken;

// In InitializeComponent — add _resumeButton styled orange, initially hidden
_resumeButton.BackColor = Color.FromArgb(255, 140, 0);
_resumeButton.Text = "Resume";
_resumeButton.Visible = false;
_resumeButton.Click += ResumeButton_Click;

private void ResumeButton_Click(object? sender, EventArgs e)
{
    if (_resumeToken != null)
        ResumeRequested?.Invoke(this, _resumeToken);
}

public void MarkCancelled(ResumeToken? resumeToken)
{
    if (InvokeRequired) { Invoke(() => MarkCancelled(resumeToken)); return; }
    _fileNameLabel.ForeColor = Color.FromArgb(255, 140, 0);
    _cancelButton.Enabled = false;
    _resumeToken = resumeToken;
    _resumeButton.Visible = resumeToken != null;
    _resumeButton.Enabled = resumeToken != null;
}

public void ResetForResume()
{
    if (InvokeRequired) { Invoke(ResetForResume); return; }
    _fileNameLabel.ForeColor = Color.Black;
    _resumeButton.Visible = false;
    _progressBar.Value = 0;
}
```

### Console — FileTransferHelper key changes

```csharp
// RunDownloadAsync now returns results and accepts a CT
public static async Task<DownloadResult[]> RunDownloadAsync(
    IHttpClientFactory httpClientFactory,
    Uri urlPath,
    string[] saveFiles,
    int interval,
    CancellationToken cancellationToken = default)
{
    List<Task<DownloadResult>> tasks = [];
    for (int i = 0; i < saveFiles.Length; i++)
        tasks.Add(FileDownloadAsync(httpClientFactory, urlPath, saveFiles[i], 0, 8 + i, interval, true, cancellationToken));
    return await Task.WhenAll(tasks).ConfigureAwait(false);
}

// New: resume all cancelled results
public static async Task<DownloadResult[]> ResumeDownloadsAsync(
    IHttpClientFactory httpClientFactory,
    DownloadResult[] previousResults,
    string[] saveFiles,
    int interval,
    CancellationToken cancellationToken = default)
{
    List<Task<DownloadResult>> tasks = [];
    for (int i = 0; i < previousResults.Length; i++)
    {
        DownloadResult prev = previousResults[i];
        if (!prev.IsSuccess && prev.ResumeToken != null)
            tasks.Add(FileDownloadAsync(httpClientFactory, prev.ResumeToken.Url, saveFiles[i], 0, 8 + i, interval, true, cancellationToken, prev.ResumeToken));
        else
            tasks.Add(Task.FromResult(prev));  // already complete or non-resumable
    }
    return await Task.WhenAll(tasks).ConfigureAwait(false);
}
```

### Console — Program.cs resume loop

```csharp
using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

DownloadResult[] results = await FileTransferHelper
    .RunDownloadAsync(httpClientFactory, new Uri(url), [saveFile1, saveFile2, saveFile3, saveFile4], reportInterval, cts.Token)
    .ConfigureAwait(false);

while (results.Any(r => !r.IsSuccess && r.ResumeToken != null))
{
    int cancelledCount = results.Count(r => !r.IsSuccess && r.ResumeToken != null);
    Console.SetCursorPosition(0, 13);
    Console.Write($"{cancelledCount} download(s) cancelled. Resume? (y/n): ");
    char answer = Console.ReadKey().KeyChar;
    Console.WriteLine();
    if (answer != 'y') break;

    using CancellationTokenSource resumeCts = new();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; resumeCts.Cancel(); };
    results = await FileTransferHelper
        .ResumeDownloadsAsync(httpClientFactory, results, [saveFile1, saveFile2, saveFile3, saveFile4], reportInterval, resumeCts.Token)
        .ConfigureAwait(false);
}
```

---

## Phases

### Phase 1 — Library: New Models

**Goal:** Add `ResultBase`, `GetResult`, `ResumeToken`, `DownloadResult`, and update `TransferState`.

**Deliverables:**

- `Models/ResultBase.cs` — abstract record: `bool IsSuccess`, `HttpStatusCode? StatusCode`, `string? ErrorMessage`, `Exception? Exception` (with `protected init` accessors)
- `Models/GetResult.cs` — sealed record inheriting `ResultBase`; static factories `Ok(statusCode)`, `Failed(statusCode, error)`
- `Models/ResumeToken.cs` — sealed record with `Uri`, `long`, and two nullable validators
- `Models/DownloadResult.cs` — sealed record inheriting `ResultBase`; adds `ResumeToken?`; static factories `Ok`, `Failed`, `Cancelled`
- `Models/TransferState.cs` — `StartOffset` property + overloaded `Start()` with `startOffset = 0` default (existing one-arg callers unaffected)

**Dependencies:** None

---

### Phase 2 — Library: GetResult + DownloadAsync API

**Goal:** **Breaking change** — update `GetAsync` / `InternalGetAsync` to return `Task<GetResult>`. Add `InternalDownloadAsync` and four `DownloadAsync` overloads.

**Deliverables:**

- `InternalGetAsync` updated to return `Task<GetResult>`; HTTP errors captured as `GetResult.Failed(statusCode, error)` rather than thrown
- All 4 `GetAsync` overloads updated to return `Task<GetResult>` (breaking change)
- `InternalDownloadAsync` private method using `HttpRequestMessage` + `SendAsync`
- Range header construction; 206 vs 200 detection via `response.StatusCode` (no `ServerRangeNotSupported` boolean)
- Absolute `bytesWritten` tracking throughout the read loop
- `OperationCanceledException` caught → `DownloadResult.Cancelled(resumeToken)` returned (never re-thrown)
- 4 public `DownloadAsync` overloads mirroring the updated `GetAsync` surface

**Dependencies:** Phase 1 complete

---

### Phase 3 — WPF Sample

**Goal:** Add cancel → Resume button → resume flow to the WPF download manager.

**Deliverables:**

- `DownloadService.cs` — new `Task<DownloadResult>` resumable overload
- `DownloadItemViewModel.cs` — `IsCancelled`, `CanResume`, `ResumeToken?`, `DestinationPath`, `MarkCancelled()`, `ResetForResume()`
- `MainViewModel.cs` — `ResumeDownloadCommand`, `_destinationPaths` list, `ResumeFileAsync()` private method, `MarkCancelled` call when `result.IsSuccess == false && result.ResumeToken != null`
- `MainWindow.xaml` — Resume button bound to `ResumeDownloadCommand` with `CanResume` visibility; distinct orange badge for Cancelled state

**Dependencies:** Phase 2 complete

---

### Phase 4 — WinForms Sample

**Goal:** Mirror the cancel → resume flow in WinForms using an event-driven approach.

**Deliverables:**

- `DownloadService.cs` — same resumable overload as WPF
- `DownloadProgressControl.cs` — orange `_resumeButton` (hidden until cancelled), `ResumeRequested` event, `MarkCancelled()`, `ResetForResume()`, `DestinationPath` property
- `MainForm.cs` — `_destinationPaths` list, `ResumeRequested` subscription per control, resume task dispatch, `MarkCancelled` when `result.ResumeToken != null`, `MarkError` when `!result.IsSuccess && result.ResumeToken == null`

**Dependencies:** Phase 2 complete

---

### Phase 5 — Console Sample

**Goal:** Enable Ctrl+C → resume prompt → continue loop in the console runner.

**Deliverables:**

- `Progress.cs` — `MarkCancelled()` and `MarkResuming()` methods at fixed console row
- `FileTransferHelper.cs` — `CancellationToken` threaded through all levels, switch to `DownloadAsync`, return `Task<DownloadResult[]>`, new `ResumeDownloadsAsync`
- `Program.cs` — `CancelKeyPress` handler, `using CancellationTokenSource`, per-file result summary, `while` resume loop

**Dependencies:** Phase 2 complete

---

### Phase 6 — Tests

**Goal:** Add unit tests for new models and integration-style tests for resume token round-trips.

**Deliverables:**

- `Models/ResumeTokenTests.cs` — constructor, positional values, equality, null validators
- `Models/GetResultTests.cs` — `IsSuccess`/`StatusCode`/`ErrorMessage` for `Ok` and `Failed` factories; `ResultBase` assignability
- `Models/DownloadResultTests.cs` — `Ok`, `Failed`, `Cancelled` factories; `IsSuccess`/`StatusCode`/`ResumeToken` combinations; `ResultBase` assignability
- `Models/TransferStateTests.cs` — update existing `Start()` tests; add tests for `startOffset` pre-seeding
- `HttpClientExtensionTests.cs` — `GetAsync` 200/404; `DownloadAsync` with mock handler returning 206, 200 (range not supported), and mid-stream cancellation

**Dependencies:** Phase 2 complete

---

## Steps

### Phase 1 — Library: New Models

#### Step 1: Create ResultBase.cs

1. Create `src/Blazing.Extensions.Http/Models/ResultBase.cs`
2. Declare `public abstract record ResultBase` with `bool IsSuccess`, `HttpStatusCode? StatusCode`, `string? ErrorMessage`, `Exception? Exception`
3. Use `protected init` setters so only derived result types can create/update instances through factory methods
4. Add full XML doc comments; note that `StatusCode` is nullable for cancellation/network-failure cases

#### Step 2: Create GetResult.cs

1. Create `src/Blazing.Extensions.Http/Models/GetResult.cs`
2. Declare `public sealed record GetResult : ResultBase`
3. Private parameterless constructor; expose `static GetResult Ok(HttpStatusCode)` and `static GetResult Failed(HttpStatusCode?, string)` factories
4. Add full XML doc comments

#### Step 3: Create ResumeToken.cs

1. Create `src/Blazing.Extensions.Http/Models/ResumeToken.cs`
2. Declare `public sealed record ResumeToken(Uri Url, long BytesWritten, string? ETag = null, DateTimeOffset? LastModified = null)`
3. Add full XML doc comments on the record and each parameter

#### Step 4: Create DownloadResult.cs

1. Create `src/Blazing.Extensions.Http/Models/DownloadResult.cs`
2. Declare `public sealed record DownloadResult : ResultBase` with `ResumeToken?`
3. Private parameterless constructor; expose `Ok(HttpStatusCode)`, `Failed(HttpStatusCode?, string)`, `Cancelled(ResumeToken)` factories
4. Add full XML doc comments; note `Cancelled` leaves `StatusCode = null`

#### Step 5: Update TransferState.cs

1. Open `src/Blazing.Extensions.Http/Models/TransferState.cs`
2. Add `public long StartOffset { get; private set; }` property with XML doc comment
3. Change existing `Start(long? totalBytes)` signature to `Start(long? totalBytes, long startOffset = 0)` — preserves all existing callers
4. Inside `Start()` body: set `StartOffset = startOffset`; when `startOffset > 0`, set `Total.Transferred = startOffset`
5. Verify `CalcProgressPercentage`, `CalcRemainingSize`, `CalcEstimatedRemainingTime` require no changes (they read `Total.Transferred` and `TotalBytes` — both now correct)
6. Run `dotnet build` — confirm zero errors/warnings

---

### Phase 2 — Library: GetResult + DownloadAsync API

#### Step 6: Update InternalGetAsync and GetAsync overloads

1. Open `src/Blazing.Extensions.Http/HttpClientExtension.cs`
2. Change `InternalGetAsync` return type from `Task` to `Task<GetResult>`
3. Replace the `throw new HttpRequestException(...)` with `return GetResult.Failed(response.StatusCode, $"{response.ReasonPhrase}: {errorContent}")`
4. Catch `OperationCanceledException` in `InternalGetAsync` → `return GetResult.Failed(null, "Cancelled")`
5. After the successful read loop, return `GetResult.Ok(response.StatusCode)`
6. Update all 4 public `GetAsync` overloads to return `Task<GetResult>` and propagate the result
7. Update all sample call sites that currently `await GetAsync(...)` with no result capture — they must now inspect `GetResult`

#### Step 7: Add InternalDownloadAsync

1. After the closing brace of updated `InternalGetAsync`, add `private static async Task<DownloadResult> InternalDownloadAsync(...)` with the signature from the Code Snippets section
2. Implement body:
    - Build `HttpRequestMessage` with `HttpMethod.Get`
    - If `resumeToken != null`: set `request.Headers.Range` and `If-Range` header
    - `client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)`
    - Capture `etag` from `response.Headers.ETag?.Tag` and `lastModified` from `response.Content.Headers.LastModified`
    - Check `resumeToken != null && response.StatusCode == 200` → return `DownloadResult.Failed(200, "Server does not support Range requests")`
    - Check `!response.IsSuccessStatusCode` → return `DownloadResult.Failed(response.StatusCode, errorContent)`
    - Resolve `totalBytes` from `ContentRange?.Length ?? ContentLength`
    - Call `transferState.Start(totalBytes, startOffset)` with `startOffset = resumeToken?.BytesWritten ?? 0L`
    - Read loop: write to `destStream`, increment both `position` (reset per interval) and `bytesWritten` (never reset)
    - After loop: `transferState.Stop()`, final progress report, return `DownloadResult.Ok(response.StatusCode)`
    - `catch (OperationCanceledException)` → return `DownloadResult.Cancelled(new ResumeToken(url, bytesWritten, etag, lastModified))` — do NOT re-throw
    - `catch (Exception)` → dispose response, re-throw
3. `bytesWritten` must be declared before the `try` block so the catch block can access it

#### Step 8: Add public DownloadAsync overloads

1. After the last `GetAsync` overload, add the 4 `DownloadAsync` overloads from the Code Snippets section
2. Add XML doc comments matching the updated `GetAsync` style, noting the additional `resumeToken` parameter
3. Validate param nullchecks match `GetAsync` pattern
4. Run `dotnet build` — confirm zero errors/warnings

---

### Phase 3 — WPF Sample

#### Step 9: Update DownloadService

1. Open `src/samples/WpfExample/Services/DownloadService.cs`
2. Add new overload: `Task<DownloadResult> DownloadFileAsync(Uri url, string destinationPath, IProgress<TransferState>, LatencyTracker, ResumeToken? resumeToken, CancellationToken)`
3. When `resumeToken != null`: open file with `new FileStream(destinationPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None)`, then `await fileStream.SeekAsync(resumeToken.BytesWritten, SeekOrigin.Begin)` (or synchronous `Seek` + `SetLength` if already at position)
4. When `resumeToken == null`: keep `File.Create(destinationPath)` behaviour
5. Call `client.DownloadAsync(url, fileStream, progress, resumeToken, interval: 100, bufferSize: 65536, latencyTracker, ct)`
6. Return the `DownloadResult` from the library

#### Step 10: Update DownloadItemViewModel

1. Open `src/samples/WpfExample/ViewModels/DownloadItemViewModel.cs`
2. Add `[ObservableProperty] private bool _isCancelled;`
3. Add `[ObservableProperty] private bool _canResume;`
4. Add `public ResumeToken? ResumeToken { get; private set; }` (not observable — ViewModel reads it, XAML doesn't bind)
5. Add `public string? DestinationPath { get; set; }`
6. Add `MarkCancelled(ResumeToken? resumeToken)` method — sets `IsCancelled = true`, `IsError = false`, `CanCancel = false`, stores token, sets `CanResume`
7. Add `ResetForResume()` method — clears `IsCancelled`, `CanResume`, `ResumeToken`, resets `ProgressPercentage` and `ProgressText`
8. Update any existing `MarkError()` call sites that were handling cancel (should be moved to `MarkCancelled`)

#### Step 11: Update MainViewModel

1. Open `src/samples/WpfExample/ViewModels/MainViewModel.cs`
2. Add `private readonly List<string> _destinationPaths = [];` field
3. In `DownloadFileAsync(int index, string url, CT)`, store destination path to `_destinationPaths[index]` before the download, store `DownloadItemViewModel` in a local and set `downloadVm.DestinationPath`
4. Switch service call to `DownloadAsync` (returns `DownloadResult`); on `result.ResumeToken != null` call `downloadVm.MarkCancelled(result.ResumeToken)`; on `!result.IsSuccess && result.ResumeToken == null` call `downloadVm.MarkError()`
5. Add `[RelayCommand] private async Task ResumeDownloadAsync(DownloadItemViewModel vm)` — creates new linked CTS, stores it in `_individualCancellationTokenSources[index]`, calls `vm.ResetForResume()`, `vm.SetCancellationTokenSource(newCts)`, then `ResumeFileAsync(index, vm, newCts.Token)`
6. Add private `ResumeFileAsync` method (see Code Snippets)

#### Step 12: Update MainWindow.xaml

1. Open `src/samples/WpfExample/Views/MainWindow.xaml`
2. In the `DataTemplate` for each download item, add a Resume `Button` with:
    - `Command="{Binding DataContext.ResumeDownloadCommand, RelativeSource={RelativeSource AncestorType=Window}}"` and `CommandParameter="{Binding}"`
    - `Visibility="{Binding CanResume, Converter={StaticResource BooleanToVisibilityConverter}}"`
    - Orange background styling matching the Cancel button style but distinct
3. Add a "Cancelled" status badge styled in orange (parallel to existing "Error" badge in red), shown when `IsCancelled = true`
4. Ensure `BooleanToVisibilityConverter` is registered in Window resources (likely already present for existing bindings)

---

### Phase 4 — WinForms Sample

#### Step 13: Update WinForms DownloadService

1. Open `src/samples/WinFormsExample/DownloadService.cs`
2. Add the same resumable overload as the WPF `DownloadService` (identical logic)

#### Step 14: Update DownloadProgressControl

1. Open `src/samples/WinFormsExample/DownloadProgressControl.cs`
2. Add `private Button _resumeButton = null!;` field
3. Add `public string? DestinationPath { get; set; }` property
4. Add `public event EventHandler<ResumeToken>? ResumeRequested;`
5. Add `private ResumeToken? _resumeToken;` field
6. In `InitializeComponent()` (or constructor after designer call), create `_resumeButton`: orange background, "Resume" text, anchored next to Cancel button, `Visible = false`
7. Wire `_resumeButton.Click += ResumeButton_Click` where the handler calls `ResumeRequested?.Invoke(this, _resumeToken!)`
8. Add `MarkCancelled(ResumeToken? resumeToken)` — stores token, sets label foreground to orange, disables Cancel button, shows/enables Resume button
9. Add `ResetForResume()` — hides Resume button, resets label foreground, resets progress bar
10. Update `MarkComplete()` and `MarkError()` to hide the Resume button
11. Update `.Designer.cs` / `resx` if the control has a designer file

#### Step 15: Update MainForm

1. Open `src/samples/WinFormsExample/MainForm.cs`
2. Add `private readonly List<string> _destinationPaths = [];` field
3. In `DownloadFileAsync(int, string, CT)`: populate `_destinationPaths[i]`, set `control.DestinationPath`, switch to `DownloadAsync` service overload
4. Switch service call to `DownloadAsync`; when `result.ResumeToken != null` call `control.MarkCancelled(result.ResumeToken)`; when `!result.IsSuccess && result.ResumeToken == null` call `control.MarkError()`
5. After creating each `DownloadProgressControl`, subscribe `control.ResumeRequested += OnResumeRequested`
6. Add `private void OnResumeRequested(object? sender, ResumeToken token)`:
    - Find the control index
    - Create new linked CTS, store in `_individualCancellationTokenSources[index]`
    - Call `(sender as DownloadProgressControl)!.ResetForResume()`
    - Dispatch `Task.Run(() => DownloadResumeAsync(index, control, token, newCts.Token))`
7. Add private `async Task DownloadResumeAsync(...)` — opens file, seeks, calls service resumable overload, updates control state on completion

---

### Phase 5 — Console Sample

#### Step 16: Update Progress.cs

1. Open `src/samples/ConsoleExample/Progress.cs`
2. Add `MarkCancelled(int row)` — moves cursor to `row`, writes orange (or white) "Cancelled" status at a fixed column
3. Add `MarkResuming(int row)` — moves cursor to `row`, writes "Resuming..." status

#### Step 17: Update FileTransferHelper.cs

1. Open `src/samples/ConsoleExample/FileTransferHelper.cs`
2. Thread `CancellationToken cancellationToken = default` through `RunDownloadAsync` → `FileDownloadAsync` → `DownloadFileAsync` (bottom level)
3. Change bottom-level `client.GetAsync(...)` call to `client.DownloadAsync(..., cancellationToken: cancellationToken)` — returns `DownloadResult`
4. Change return types: `DownloadFileAsync` → `Task<DownloadResult>`, `FileDownloadAsync` → `Task<DownloadResult>`, `RunDownloadAsync` → `Task<DownloadResult[]>`
5. On cancellation, call `Progress.MarkCancelled(row)` before returning the result
6. Add `ResumeDownloadsAsync(IHttpClientFactory, DownloadResult[], string[], int, CancellationToken)` — see Code Snippets

#### Step 18: Update Program.cs

1. Open `src/samples/ConsoleExample/Program.cs`
2. In the download menu branch, add `using CancellationTokenSource cts = new();`
3. Hook `Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); }`
4. Switch `await FileTransferHelper.RunDownloadAsync(...)` to capture `DownloadResult[] results`
5. After `RunDownloadAsync` completes, print a per-file summary (Complete / Cancelled / Failed)
6. Implement the `while (results.Any(r => !r.IsSuccess && r.ResumeToken != null))` loop (see Code Snippets)
7. Unhook `CancelKeyPress` handler for the initial CTS after use (or scope carefully) to avoid double-fire

---

### Phase 6 — Tests

#### Step 19: Add ResultBase and GetResult tests

1. Create `tests/Blazing.Extensions.Http.Tests/Models/GetResultTests.cs`
2. Test: `GetResult.Ok(200)` → `IsSuccess=true`, `StatusCode=200`, `ErrorMessage=null`
3. Test: `GetResult.Failed(404, "Not Found")` → `IsSuccess=false`, `StatusCode=404`, `ErrorMessage != null`
4. Test: `GetResult.Failed(null, "Cancelled")` → `StatusCode=null`
5. Test: `GetResult` is assignable to `ResultBase`

#### Step 20: Add ResumeToken tests

1. Create `tests/Blazing.Extensions.Http.Tests/Models/ResumeTokenTests.cs`
2. Test: constructor positional assignment, ETag null default, LastModified null default, record equality

#### Step 21: Add DownloadResult tests

1. Create `tests/Blazing.Extensions.Http.Tests/Models/DownloadResultTests.cs`
2. Test: `DownloadResult.Ok(200)` → `IsSuccess=true`, `StatusCode=200`, `ResumeToken=null`
3. Test: `DownloadResult.Failed(404, "Not Found")` → `IsSuccess=false`, `ResumeToken=null`
4. Test: `DownloadResult.Failed(200, "Server does not support Range requests")` → `StatusCode=200`, `IsSuccess=false`
5. Test: `DownloadResult.Cancelled(token)` → `IsSuccess=false`, `StatusCode=null`, `ResumeToken != null`
6. Test: `DownloadResult` is assignable to `ResultBase`

#### Step 22: Update TransferState tests

1. Open `tests/Blazing.Extensions.Http.Tests/Models/TransferStateTests.cs`
2. Add test: `Start(1000, 0)` → `StartOffset == 0`, `Total.Transferred == 0`
3. Add test: `Start(1000, 400)` → `StartOffset == 400`, `Total.Transferred == 400`, `CalcProgressPercentage() == 40`
4. Add test: `Start(1000, 400)` → `CalcRemainingSize() == 600`
5. Ensure existing one-arg `Start(totalBytes)` tests still pass unchanged

#### Step 23: Add HttpClientExtension tests

1. Create or update `tests/Blazing.Extensions.Http.Tests/HttpClientExtensionTests.cs`
2. Write a `DelegatingHandler` test double that returns 200, 206, 404, or a mid-stream cancellation
3. Test: `GetAsync` 200 → `result.IsSuccess=true`, `result.StatusCode=200`
4. Test: `GetAsync` 404 → `result.IsSuccess=false`, `result.StatusCode=404`, `result.ErrorMessage != null`
5. Test: `DownloadAsync` with `resumeToken` → handler receives `Range: bytes=N-` header
6. Test: Server returns 200 on range request → `result.IsSuccess=false`, `result.StatusCode=200`, `result.ResumeToken=null`
7. Test: Mid-stream cancellation → `result.IsSuccess=false`, `result.StatusCode=null`, `result.ResumeToken.BytesWritten > 0`
8. Test: Successful complete download → `result.IsSuccess=true`, `result.StatusCode=200` or `206`

---

## Checklist

### Phase 1 — Library Models

- [ ] `Models/ResultBase.cs` created as `public abstract record`
- [ ] `ResultBase` has `bool IsSuccess`, `HttpStatusCode? StatusCode`, `string? ErrorMessage`, `Exception? Exception`
- [ ] XML doc comments on `ResultBase` and all members; note nullable `StatusCode` for cancel/network-failure; note `Exception` is null for HTTP-status failures
- [ ] `Models/GetResult.cs` created as `public sealed record : ResultBase`
- [ ] `GetResult` has private parameterless constructor and uses inherited `ResultBase` state via factory methods
- [ ] `GetResult.Ok(HttpStatusCode)` static factory
- [ ] `GetResult.Failed(HttpStatusCode?, string, Exception? = null)` static factory
- [ ] XML doc comments on `GetResult` type and all members
- [ ] `Models/ResumeToken.cs` created as `public sealed record`
- [ ] `ResumeToken` has `Uri Url`, `long BytesWritten`, `string? ETag = null`, `DateTimeOffset? LastModified = null`
- [ ] XML doc comments on `ResumeToken` type and all parameters
- [ ] `Models/DownloadResult.cs` created as `public sealed record : ResultBase`
- [ ] `DownloadResult` has private parameterless constructor and a private `init` `ResumeToken?` property; shared state comes from `ResultBase`
- [ ] `DownloadResult.Ok(HttpStatusCode)` static factory
- [ ] `DownloadResult.Failed(HttpStatusCode?, string, Exception? = null)` static factory
- [ ] `DownloadResult.Cancelled(ResumeToken, OperationCanceledException? = null)` static factory — `StatusCode = null`
- [ ] XML doc comments on `DownloadResult` type and all members
- [ ] `TransferState.StartOffset` property added (`public long`, `private set`)
- [ ] `TransferState.Start()` signature updated to `Start(long? totalBytes, long startOffset = 0)`
- [ ] `Start()` pre-seeds `Total.Transferred = startOffset` when `startOffset > 0`
- [ ] Existing one-arg callers of `Start(totalBytes)` compile without change
- [ ] `CalcProgressPercentage()`, `CalcRemainingSize()`, `CalcEstimatedRemainingTime()` require no modification

### Phase 2 — Library API

- [ ] `InternalGetAsync` return type changed to `Task<GetResult>` (**breaking**)
- [ ] `InternalGetAsync` HTTP error path returns `GetResult.Failed(statusCode, error)` — does not throw
- [ ] `InternalGetAsync` cancellation path returns `GetResult.Failed(null, "Cancelled")` — does not throw
- [ ] `InternalGetAsync` success path returns `GetResult.Ok(response.StatusCode)`
- [ ] All 4 public `GetAsync` overloads return `Task<GetResult>`
- [ ] `InternalDownloadAsync` added as `private static async Task<DownloadResult>`
- [ ] Uses `HttpRequestMessage` + `client.SendAsync` (not `client.GetAsync`)
- [ ] `Range: bytes=N-` header set when `resumeToken != null`
- [ ] `If-Range` header set to `ETag` when available, else `LastModified.ToString("R")`
- [ ] ETag captured from `response.Headers.ETag?.Tag`
- [ ] `LastModified` captured from `response.Content.Headers.LastModified`
- [ ] Range-not-supported: `resumeToken != null && status == 200` → `DownloadResult.Failed(200, "Server does not support Range requests")`
- [ ] `transferState.Start(totalBytes, startOffset)` called with correct `startOffset`
- [ ] `bytesWritten` (absolute, `long`) declared before `try` block so catch can access it
- [ ] `bytesWritten` incremented each read iteration and **never reset**
- [ ] `position` (interval chunk counter, `int`) still reset after each progress report
- [ ] `catch (OperationCanceledException ex)` returns `DownloadResult.Cancelled(new ResumeToken(...), ex)` — does **not** re-throw
- [ ] `catch (Exception ex)` returns `DownloadResult.Failed(null, ex.Message, ex)` — disposes response, does **not** re-throw
- [ ] Nothing escapes either internal method — callers never need `try/catch`
- [ ] 4 public `DownloadAsync` overloads added (Uri + string, with + without headers `IDictionary`)
- [ ] All `DownloadAsync` overloads have full XML doc comments
- [ ] `dotnet build` passes with zero errors

### Phase 3 — WPF Sample

- [ ] `DownloadService` new overload accepts `ResumeToken?` and returns `Task<DownloadResult>`
- [ ] Resume overload opens file with `FileMode.OpenOrCreate` and seeks to `BytesWritten`
- [ ] Fresh download overload still uses `File.Create` (no change to existing overload)
- [ ] `DownloadItemViewModel`: `IsCancelled` observable property added
- [ ] `DownloadItemViewModel`: `CanResume` observable property added
- [ ] `DownloadItemViewModel`: `ResumeToken?` property added (non-observable)
- [ ] `DownloadItemViewModel`: `DestinationPath` property added
- [ ] `DownloadItemViewModel`: `MarkCancelled(ResumeToken?)` method added
- [ ] `DownloadItemViewModel`: `ResetForResume()` method added
- [ ] `MarkCancelled` sets `IsCancelled=true`, `IsError=false`, `CanCancel=false`, `CanResume = token != null`
- [ ] `MainViewModel`: `_destinationPaths` list maintains path per download slot
- [ ] `MainViewModel`: `DownloadFileAsync` uses `DownloadAsync` — captures `DownloadResult`
- [ ] `MainViewModel`: `result.ResumeToken != null` → `MarkCancelled(result.ResumeToken)`; `!result.IsSuccess && result.ResumeToken == null` → `MarkError()`
- [ ] `MainViewModel`: `ResumeDownloadCommand` (`[RelayCommand]`) added
- [ ] `MainViewModel`: `ResumeFileAsync` private method creates new linked CTS, resets VM, calls service
- [ ] `MainWindow.xaml`: Resume button bound to `ResumeDownloadCommand` with `CommandParameter="{Binding}"`
- [ ] Resume button `Visibility` bound to `CanResume` via `BooleanToVisibilityConverter`
- [ ] Cancelled state shown in orange badge (distinct from red Error badge)
- [ ] Partial file is **not deleted** on cancellation

### Phase 4 — WinForms Sample

- [ ] `DownloadService` new overload accepts `ResumeToken?` and returns `Task<DownloadResult>` (identical to WPF)
- [ ] `DownloadProgressControl`: `_resumeButton` field added (orange background, initially hidden)
- [ ] `DownloadProgressControl`: `DestinationPath` property added
- [ ] `DownloadProgressControl`: `ResumeRequested` event (`EventHandler<ResumeToken>`) added
- [ ] `DownloadProgressControl`: `_resumeToken` private field stores the current token
- [ ] `DownloadProgressControl`: `MarkCancelled(ResumeToken?)` stores token, shows Resume button, sets orange label
- [ ] `DownloadProgressControl`: `ResetForResume()` hides Resume button, resets label and progress bar
- [ ] `DownloadProgressControl`: `MarkComplete()` and `MarkError()` hide Resume button
- [ ] `DownloadProgressControl`: `_resumeButton.Click` fires `ResumeRequested` event
- [ ] `DownloadProgressControl`: `InvokeRequired` checks on all methods modifying UI
- [ ] `MainForm`: `_destinationPaths` list added
- [ ] `MainForm`: `control.DestinationPath` set before download begins
- [ ] `MainForm`: `control.ResumeRequested` subscribed for each control after creation
- [ ] `MainForm`: `DownloadFileAsync` uses `DownloadAsync` service overload, captures `DownloadResult`
- [ ] `MainForm`: cancel path calls `control.MarkCancelled(result.ResumeToken)` (not `MarkError`)
- [ ] `MainForm`: `OnResumeRequested` handler creates new CTS, calls `ResetForResume()`, dispatches resume task
- [ ] Partial file is **not deleted** on cancellation

### Phase 5 — Console Sample

- [ ] `Progress.MarkCancelled(int row)` added — writes "Cancelled" at fixed console row
- [ ] `Progress.MarkResuming(int row)` added — writes "Resuming..." at fixed console row
- [ ] `FileTransferHelper.RunDownloadAsync` accepts and threads `CancellationToken`
- [ ] All internal call chain levels accept `CancellationToken` (no `default` silent drops)
- [ ] Bottom-level call uses `client.DownloadAsync(...)` — not `client.GetAsync`
- [ ] `FileTransferHelper.RunDownloadAsync` returns `Task<DownloadResult[]>`
- [ ] `FileTransferHelper.ResumeDownloadsAsync` added — skips already-complete results
- [ ] On cancellation: `Progress.MarkCancelled(row)` called before returning result
- [ ] On resume start: `Progress.MarkResuming(row)` called before the request
- [ ] `Program.cs`: `Console.CancelKeyPress` handler added — sets `e.Cancel = true`, calls `cts.Cancel()`
- [ ] `Program.cs`: `using CancellationTokenSource cts = new()` scoped to download session
- [ ] `Program.cs`: Per-file result summary printed after `RunDownloadAsync` completes
- [ ] `Program.cs`: `while` resume loop runs until all complete or user declines
- [ ] Partial files are **not deleted** on cancellation

### Code Quality

- [ ] XML doc comments on all new public types and members (`/// <summary>`)
- [ ] No `TODO`, `FIXME`, or placeholder comments in committed code
- [ ] No `NotImplementedException`
- [ ] Nullable reference type annotations correct — no `!` overuse
- [ ] C# 14 primary constructors used where applicable (records, simple classes)
- [ ] `ArgumentNullException.ThrowIfNull` used at all public API boundaries (matches existing pattern)
- [ ] No mutation of `client.DefaultRequestHeaders` in any new code path
- [ ] Partial files are never deleted on cancellation in any sample

### Build & Tests

- [ ] `dotnet build` passes for library project — zero errors, zero warnings
- [ ] `dotnet build` passes for all three sample projects — zero errors, zero warnings
- [ ] `dotnet build` passes for test project — zero errors, zero warnings
- [ ] `dotnet test` passes — all existing tests green
- [ ] New `ResumeTokenTests` pass
- [ ] New `DownloadResultTests` pass
- [ ] Updated `TransferStateTests` pass (including new `startOffset` scenarios)
- [ ] New `GetResultTests` pass (including `Exception` property is null on HTTP-status failures, non-null on network exceptions)
- [ ] New `DownloadAsync` integration tests pass (206, 200-fallback, mid-stream cancel, network exception)
- [ ] New `GetAsync` integration tests pass (200 success, 404 failure, network exception → `Exception != null`)
- [ ] Polly guidance: two named clients documented in README — `"BlazingGet"` (retry enabled) and `"BlazingDownload"` (no retry)

---

## Progress Updates

### Phase 6 Complete — Tests (all 255 passing)

**Date:** Session 2

**Summary:** Phase 6 tests implemented and all 255 tests pass across `net8.0`, `net9.0`, and `net10.0`. The test project was reorganised from a flat `Models/` folder to an industry-standard three-tier structure.

**Test project structure after reorganisation:**

```
tests/Blazing.Extensions.Http.Tests/
├── Fixtures/
│   ├── FakeHttpHandler.cs          ← DelegatingHandler test double for HttpClient
│   └── CancellationStream.cs       ← Deterministic mid-stream cancellation stream
├── UnitTests/
│   └── Models/
│       ├── BitUnitTests.cs         ← migrated from Models/
│       ├── ByteUnitTests.cs        ← migrated from Models/
│       ├── DownloadResultTests.cs  ← new (Step 21)
│       ├── GetResultTests.cs       ← new (Step 19)
│       ├── LatencyTrackerTests.cs  ← migrated from Models/
│       ├── ResumeTokenTests.cs     ← new (Step 20)
│       ├── TransferRateTests.cs    ← migrated from Models/
│       └── TransferStateTests.cs   ← migrated + extended (Step 22)
└── IntegrationTests/
    └── HttpClientExtensionTests.cs ← new (Step 23), 8 integration tests
```

**Test counts (per framework):** 85 unit tests + integration tests = 85 total.

**Key implementation notes:**

- `FakeHttpHandler` routes both `client.GetAsync` (via `InternalGetAsync`) and `client.SendAsync` (via `InternalDownloadAsync`) through a single `SendAsync` override — no separate mocks needed
- `CancellationStream` cancels deterministically: delivers all `initialData` bytes, then calls `CancelAsync()` and awaits `Task.Delay(Infinite, alreadyCancelledToken)` — throws `OperationCanceledException` immediately, no timing dependency
- `CA2000` and `CA5394` suppressed in test `.csproj` `<NoWarn>` (standard practice for test projects — HttpClient/HttpResponseMessage lifetime managed by the framework)
- `CA1849` fixed in `CancellationStream.cs` by replacing `_cts.Cancel()` with `await _cts.CancelAsync()`
- FluentAssertions used throughout (not Shouldly — the project already had FluentAssertions v7.0.0)

**All phases complete.** Build: 0 errors, 0 warnings across all 15 project targets.
