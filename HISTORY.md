# Blazing.Extensions.Http History

## V2.1.0 - 16 April 2026

### New Features

**Resumable Downloads**

- New `DownloadAsync` extension method with built-in cancel/resume support via HTTP Range Requests (RFC 7233)
- When a download is cancelled mid-stream, `DownloadAsync` returns a `DownloadResult.Cancelled` carrying a `ResumeToken`
- Pass the `ResumeToken` back to `DownloadAsync` to resume from the exact byte offset — the server validator (`ETag` or `Last-Modified`) is used with `If-Range` to prevent corrupt resumes
- `GetAsync` methods now return `Task<GetResult>` instead of `Task`

**New Result Models**

- `ResultBase` — abstract base record with `IsSuccess`, `StatusCode`, `ErrorMessage`, and `Exception`
- `GetResult` — result type for `GetAsync` operations; factory methods `Ok(statusCode)` and `Failed(statusCode, error)`
- `DownloadResult` — result type for `DownloadAsync` operations; adds `Cancelled(resumeToken)` for mid-stream cancellation
- `ResumeToken` — captures `Url`, `BytesWritten`, `ETag`, and `LastModified` needed to issue a Range request

### Updated Samples

- **ConsoleExample** — updated to use `DownloadAsync` with the new result-based API
- **WinFormsExample** — full cancel/resume UI with per-download Resume button; corrected active/failed statistics counters after cancel and resume
- **WpfExample** — full cancel/resume UI with per-download Resume button; corrected global CTS refresh after "Stop All" and active/failed statistics counters

### Tests

- Reorganised test project into `UnitTests/`, `IntegrationTests/`, and `Fixtures/` folders
- Added `GetResultTests`, `DownloadResultTests`, `ResumeTokenTests` unit test classes
- Added `HttpClientExtensionTests` integration tests covering 200 OK, 404, 206 Partial Content, Range header validation, range-not-supported fallback, and mid-stream cancellation
- **255 passing tests** across net8.0, net9.0, and net10.0

### Breaking Changes

- `GetAsync` now returns `Task<GetResult>` instead of `Task` — callers must check `result.IsSuccess` instead of catching exceptions

---

### V2.0.0 - 17 November 2025

- **.NET 10.0 Support** - Full compatibility with .NET 10.0
- **Test Coverage** - Added unit tests for core functionalities

## V1.0.0 (.Net 8.0+) - 7 October, 2025

### Initial Release

**Core Features**

- Advanced HttpClient extension methods with real-time progress reporting
- High-precision latency tracking and Time To First Byte (TTFB) measurements
- Comprehensive transfer statistics with multiple unit representations
- Support for .NET 8.0 and .NET 9.0 with central package management

**HTTP Operations**

- `GetAsync` methods for file downloads with progress monitoring
- `PostAsync` methods for file uploads with multipart/form-data support
- Custom headers support for authenticated and specialized requests
- Configurable buffer sizes and reporting intervals for performance optimization

**Progress Reporting**

- `TransferState` model with detailed transfer statistics
- Real-time progress percentage calculations
- Current, average, and maximum transfer rate tracking
- Remaining time estimation and elapsed time reporting
- Byte and bit unit formatting (B, KiB, MiB, GiB, TiB and bit, Kibit, Mibit, Gibit, Tibit)

**Latency Tracking**

- `LatencyTracker` with nanosecond precision timing
- Per-packet latency measurements with statistical analysis
- Time To First Byte (TTFB) tracking for network performance
- Minimum, maximum, and average latency calculations

**Sample Applications**

- **ConsoleExample**: Interactive console application with parallel download support
- **WinFormsExample**: Visual download manager with dependency injection integration
- **WpfExample**: MVVM-based WPF application using CommunityToolkit.Mvvm

**Technical Infrastructure**

- Multi-targeting support for .NET 8.0 and .NET 9.0
- Central package management with Directory.Build.props and Directory.Packages.props
- NuGet package configuration with comprehensive metadata
- Modern project structure with .sln file format
- MIT License and complete documentation

**Performance Optimizations**

- Memory-efficient streaming operations
- Thread-safe progress reporting
- Configurable buffer sizes for different connection speeds
- Optional latency tracking for memory-constrained environments

### Breaking Changes

None - Initial release.

### Dependencies

- Microsoft.Extensions.DependencyInjection 9.0.0
- Microsoft.SourceLink.GitHub 8.0.0 (development only)

### Repository Information

- GitHub: https://github.com/gragra33/blazing.extensions.http
- License: MIT
- Author: Graeme Grant
