# Blazing.Extensions.Http History

## V1.0.0 (.Net 8.0+) (7 October, 2025)

### Initial Release

**Core Features**

-   Advanced HttpClient extension methods with real-time progress reporting
-   High-precision latency tracking and Time To First Byte (TTFB) measurements
-   Comprehensive transfer statistics with multiple unit representations
-   Support for .NET 8.0 and .NET 9.0 with central package management

**HTTP Operations**

-   `GetAsync` methods for file downloads with progress monitoring
-   `PostAsync` methods for file uploads with multipart/form-data support
-   Custom headers support for authenticated and specialized requests
-   Configurable buffer sizes and reporting intervals for performance optimization

**Progress Reporting**

-   `TransferState` model with detailed transfer statistics
-   Real-time progress percentage calculations
-   Current, average, and maximum transfer rate tracking
-   Remaining time estimation and elapsed time reporting
-   Byte and bit unit formatting (B, KiB, MiB, GiB, TiB and bit, Kibit, Mibit, Gibit, Tibit)

**Latency Tracking**

-   `LatencyTracker` with nanosecond precision timing
-   Per-packet latency measurements with statistical analysis
-   Time To First Byte (TTFB) tracking for network performance
-   Minimum, maximum, and average latency calculations

**Sample Applications**

-   **ConsoleExample**: Interactive console application with parallel download support
-   **WinFormsExample**: Visual download manager with dependency injection integration
-   **WpfExample**: MVVM-based WPF application using CommunityToolkit.Mvvm

**Technical Infrastructure**

-   Multi-targeting support for .NET 8.0 and .NET 9.0
-   Central package management with Directory.Build.props and Directory.Packages.props
-   NuGet package configuration with comprehensive metadata
-   Modern project structure with .sln file format
-   MIT License and complete documentation

**Performance Optimizations**

-   Memory-efficient streaming operations
-   Thread-safe progress reporting
-   Configurable buffer sizes for different connection speeds
-   Optional latency tracking for memory-constrained environments

### Breaking Changes

None - Initial release.

### Dependencies

-   Microsoft.Extensions.DependencyInjection 9.0.0
-   Microsoft.SourceLink.GitHub 8.0.0 (development only)

### Repository Information

-   GitHub: https://github.com/gragra33/blazing.extensions.http
-   License: MIT
-   Author: Graeme Grant
