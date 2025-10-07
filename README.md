# Blazing.Extensions.Http: Advanced HttpClient Extensions for .NET

High-performance HttpClient extension methods with real-time asynchronous Get/Post progress reporting, latency tracking, and detailed transfer statistics. Built for .NET 8 & 9, this library provides essential functionality for file downloads, uploads, and HTTP operations requiring comprehensive monitoring and performance insights.

## Table of Contents

-   [Quick Start](#quick-start)
    -   [Installation](#installation)
        -   [Package Installation](#package-installation)
        -   [Project Reference](#project-reference)
    -   [Configuration](#configuration)
        -   [Console Applications](#console-applications)
        -   [WPF/WinForms Applications](#wpfwinforms-applications)
    -   [Usage](#usage)
        -   [File Downloads with Progress](#file-downloads-with-progress)
        -   [File Uploads with Progress](#file-uploads-with-progress)
        -   [Custom Headers Support](#custom-headers-support)
        -   [Advanced Configuration](#advanced-configuration)
-   [Key Features](#key-features)
    -   [Real-time Progress Reporting](#real-time-progress-reporting)
    -   [Latency Tracking](#latency-tracking)
    -   [Transfer Statistics](#transfer-statistics)
    -   [Performance Monitoring](#performance-monitoring)
-   [Give a ⭐](#give-a-)
-   [Documentation](#documentation)
    -   [Core Extension Methods](#core-extension-methods)
    -   [Progress Reporting Models](#progress-reporting-models)
    -   [Latency Tracking Models](#latency-tracking-models)
-   [API Reference](#api-reference)
    -   [HttpClient Extensions](#httpclient-extensions)
    -   [Transfer State Models](#transfer-state-models)
-   [Real-World Examples](#real-world-examples)
-   [Sample Applications](#sample-applications)
    -   [ConsoleExample - Complete Implementation](#consoleexample---complete-implementation)
    -   [WinFormsExample - Visual Download Manager](#winformsexample---visual-download-manager)
    -   [WpfExample - MVVM Download Manager](#wpfexample---mvvm-download-manager)
-   [Best Practices](#best-practices)
    -   [Error Handling](#error-handling)
    -   [Memory Management](#memory-management)
    -   [Thread Safety](#thread-safety)
-   [Requirements](#requirements)
-   [Project Structure](#project-structure)
-   [Building](#building)
-   [Contributing](#contributing)
-   [License](#license)
-   [Acknowledgments](#acknowledgments)
-   [History](#history)

## Quick Start

Get started with advanced HTTP operations featuring progress reporting and latency tracking in minutes. This library extends HttpClient with powerful monitoring capabilities perfect for file transfers, API calls, and performance-critical applications.

### Installation

Add the [Blazing.Extensions.Http](https://www.nuget.org/packages/Blazing.Extensions.Http) package to your project.

#### Package Installation

```bash
dotnet add package Blazing.Extensions.Http
```

#### Project Reference

```xml
<PackageReference Include="Blazing.Extensions.Http" Version="1.0.0" />
```

### Configuration

Configure HttpClient using IHttpClientFactory for optimal performance and proper resource management. The library works seamlessly with any HttpClient configuration pattern.

#### Console Applications

Console applications can use a simple IHttpClientFactory setup for HttpClient management.

```csharp
using Microsoft.Extensions.DependencyInjection;
using Blazing.Extensions.Http;
using Blazing.Extensions.Http.Models;

// Initialize HttpClientFactory
IHttpClientFactory InitializeHttpClientFactory()
{
    ServiceCollection builder = new();
    builder.AddHttpClient();
    ServiceProvider serviceProvider = builder.BuildServiceProvider();
    return serviceProvider.GetRequiredService<IHttpClientFactory>();
}

var httpClientFactory = InitializeHttpClientFactory();

// Create progress reporter with detailed transfer information
var progress = new Progress<TransferState>(state =>
{
    double progressPercent = state.CalcProgressPercentage();
    var (speed, unit) = state.Chunk.ByteUnit;
    Console.WriteLine($"Progress: {progressPercent:P0} - {state.Total.Transferred:N0}/{state.TotalBytes:N0} bytes | Speed: {speed:N2} {unit}/s");
});

// Download with progress tracking and latency measurement
var latencyTracker = new LatencyTracker();
using var httpClient = httpClientFactory.CreateClient();
await using var fileStream = File.Create("downloaded-file.exe");
await httpClient.GetAsync("https://example.com/largefile.zip", fileStream, progress, interval: 250, bufferSize: 512, latencyTracker);
```

#### WPF/WinForms Applications

Desktop applications can integrate progress reporting with UI controls for real-time user feedback during file operations.

```csharp
// WinForms/WPF Service Registration
using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register HttpClient with factory
services.AddHttpClient("DownloadClient", client =>
{
    client.Timeout = TimeSpan.FromMinutes(30);
});

// Auto-discover and register all services with AutoRegister attribute
services.Register(typeof(Program).Assembly);

var serviceProvider = services.BuildServiceProvider();

// Download Service Implementation
[AutoRegister(ServiceLifetime.Singleton)]
public class DownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DownloadService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("DownloadClient");
        await using var fileStream = File.Create(destinationPath);

        await client.GetAsync(
            url,
            fileStream,
            progress,
            interval: 100,
            bufferSize: 65536, // 64KB buffer
            latencyTracker,
            cancellationToken);
    }
}

// UI Integration Example
private async void StartDownload_Click(object sender, RoutedEventArgs e)
{
    var progress = new Progress<TransferState>(UpdateProgress);
    var latencyTracker = new LatencyTracker();

    await _downloadService.DownloadFileAsync(
        "https://example.com/file.zip",
        "download.zip",
        progress,
        latencyTracker);
}

private void UpdateProgress(TransferState state)
{
    // Update UI on the main thread
    Dispatcher.Invoke(() =>
    {
        ProgressBar.Value = state.CalcProgressPercentage() * 100;
        StatusLabel.Content = $"{state.Total.Transferred:N0}/{state.TotalBytes:N0} bytes";
        var (speed, unit) = state.Chunk.ByteUnit;
        SpeedLabel.Content = $"{speed:N2} {unit}/s";

        if (state.Latency != null && state.Latency.PacketCount > 0)
        {
            LatencyLabel.Content = $"Latency: {state.Latency.PacketAvgMs:N2} ms";
        }
    });
}
```

### Usage

The library provides intuitive extension methods for HttpClient that enable comprehensive monitoring of HTTP operations with minimal code changes.

#### File Downloads with Progress

Download files with real-time progress reporting, transfer statistics, and latency measurements.

```csharp
using Blazing.Extensions.Http;
using Blazing.Extensions.Http.Models;

// Create detailed progress reporter for comprehensive monitoring
var progress = new Progress<TransferState>(state =>
{
    double percent = state.CalcProgressPercentage();
    var (speed, unit) = state.Chunk.ByteUnit;
    var (bitSpeed, bitUnit) = state.Chunk.BitUnit;
    var remaining = state.CalcEstimatedRemainingTime();

    bool isDownloading = Math.Abs(1 - percent) > 0.001;

    Console.WriteLine($"Progress: {percent:P1} | Speed: {speed:N2} {unit}/s ({bitSpeed:N2} {bitUnit}) | ETA: {remaining.TotalSeconds:N0}s");

    if (!isDownloading)
    {
        Console.WriteLine("Download completed!");
    }
});

// Optional latency tracking for network performance analysis
var latencyTracker = new LatencyTracker();

// Download with comprehensive monitoring
using var client = httpClientFactory.CreateClient();
await using var destination = File.Create("large-file.exe");

await client.GetAsync(
    url: "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
    destStream: destination,
    progress: progress,
    interval: 250,          // Report progress every 250ms
    bufferSize: 512,        // 512-byte buffer
    latencyTracker: latencyTracker
);

// Access final statistics
if (latencyTracker.PacketCount > 0)
{
    Console.WriteLine($"Average latency: {latencyTracker.PacketAvgMs:N2} ms");
    Console.WriteLine($"Time to first byte: {latencyTracker.TimeToFirstByte:N0} ns");
    Console.WriteLine($"Latency range: {latencyTracker.PacketMinMs:N3} - {latencyTracker.PacketMaxMs:N3} ms");
}
```

#### File Uploads with Progress

Upload files with comprehensive progress tracking and latency measurement.

```csharp
// Upload file with detailed progress monitoring
var uploadProgress = new Progress<TransferState>(state =>
{
    double percent = state.CalcProgressPercentage();
    var (currentSpeed, speedUnit) = state.Chunk.ByteUnit;
    var (avgSpeed, avgUnit) = state.Average.ByteUnit;

    Console.WriteLine($"Upload: {percent:P1} | Current: {currentSpeed:N2} {speedUnit}/s | Average: {avgSpeed:N2} {avgUnit}/s");
});

var latencyTracker = new LatencyTracker();

using var client = httpClientFactory.CreateClient();
await client.PostAsync(
    url: "https://api.example.com/upload",
    filePath: @"C:\Documents\presentation.pptx",
    progress: uploadProgress,
    interval: 250,
    bufferSize: 512,        // 512-byte buffer
    latencyTracker: latencyTracker
);
```

#### Custom Headers Support

Add custom headers to download and upload requests for authentication, API keys, or specialized requirements.

```csharp
// Download with custom headers
var headers = new Dictionary<string, string>
{
    ["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    ["X-API-Key"] = "your-api-key-here",
    ["User-Agent"] = "MyApp/1.0.0"
};

using var client = httpClientFactory.CreateClient();
await using var fileStream = File.Create("secured-file.pdf");

await client.GetAsync(
    url: "https://secure-api.example.com/files/document.pdf",
    destStream: fileStream,
    progress: progress,
    interval: 250,
    bufferSize: 512,
    latencyTracker: latencyTracker,
    headers: headers
);

// Upload with custom headers
await client.PostAsync(
    url: "https://api.example.com/files",
    filePath: @"C:\Documents\file.pdf",
    progress: uploadProgress,
    interval: 250,
    bufferSize: 512,
    latencyTracker: latencyTracker,
    headers: headers
);
```

#### Advanced Configuration

Configure advanced scenarios with custom buffer sizes, reporting intervals, and performance optimizations.

```csharp
// High-performance configuration for large files
DateTime lastUpdate = DateTime.Now;
var highPerfProgress = new Progress<TransferState>(state =>
{
    // Update UI every 2 seconds for large transfers
    if (DateTime.Now - lastUpdate > TimeSpan.FromSeconds(2))
    {
        UpdateProgressBar(state);
        lastUpdate = DateTime.Now;
    }
});

await client.GetAsync(
    url: largeFileUrl,
    destStream: fileStream,
    progress: highPerfProgress,
    interval: 2000,         // Report every 2 seconds
    bufferSize: 65536,      // 64KB buffer for high-speed connections
    latencyTracker: new LatencyTracker()
);

// Memory-efficient configuration for constrained environments
await client.GetAsync(
    url: fileUrl,
    destStream: fileStream,
    progress: memoryEfficientProgress,
    interval: 1000,         // Less frequent updates
    bufferSize: 512,        // Smaller 512-byte buffer
    latencyTracker: null    // Skip latency tracking to save memory
);

// Parallel downloads for multiple files (Console Example pattern)
var downloadTasks = new List<Task>();
string[] saveFiles = ["file1.exe", "file2.exe", "file3.exe", "file4.exe"];

for (int i = 0; i < saveFiles.Length; i++)
{
    int fileIndex = i; // Capture for closure
    downloadTasks.Add(Task.Run(async () =>
    {
        var progress = new Progress<TransferState>(state =>
        {
            // Each download gets its own progress reporting
            Console.WriteLine($"File {fileIndex + 1}: {state.CalcProgressPercentage():P1}");
        });

        using var client = httpClientFactory.CreateClient();
        await using var fileStream = File.Create(saveFiles[fileIndex]);
        await client.GetAsync(downloadUrl, fileStream, progress, 250, 512, new LatencyTracker());
    }));
}

await Task.WhenAll(downloadTasks); // Wait for all downloads to finish
```

## Key Features

This library delivers enterprise-grade HTTP operations with comprehensive monitoring capabilities, built specifically for performance-critical applications requiring detailed transfer insights.

### Real-time Progress Reporting

Advanced progress reporting system provides detailed transfer statistics and real-time updates for both downloads and uploads.

**Key Features:**

-   ✅ **Percentage Complete**: Accurate progress calculation based on total bytes
-   ✅ **Transfer Rates**: Current, average, and maximum speed measurements
-   ✅ **Bytes Transferred**: Running totals with formatted byte units (B, KiB, MiB, GiB, TiB)
-   ✅ **Time Estimates**: Remaining time calculation based on current transfer rates
-   ✅ **Bit/Byte Rates**: Comprehensive speed reporting in both bit and byte units
-   ✅ **Customizable Intervals**: Configurable progress reporting frequency
-   ✅ **Memory Efficient**: Minimal overhead during transfer operations

```csharp
// Comprehensive progress reporting
var progress = new Progress<TransferState>(state =>
{
    // Progress percentage
    double percent = state.CalcProgressPercentage();

    // Current transfer rates
    var (chunkSpeed, chunkUnit) = state.Chunk.ByteUnit;
    var (chunkBitSpeed, chunkBitUnit) = state.Chunk.BitUnit;

    // Average and maximum rates
    var (avgSpeed, avgUnit) = state.Average.ByteUnit;
    var (maxSpeed, maxUnit) = state.Maximum.ByteUnit;

    // Time calculations
    TimeSpan elapsed = state.Total.Elapsed;
    TimeSpan remaining = state.CalcEstimatedRemainingTime();

    // Remaining bytes calculation
    var (remainingBytes, remainingUnit) = state.CalcRemainingSize();

    Console.WriteLine($"Progress: {percent:P2}");
    Console.WriteLine($"Current:  {chunkSpeed:N2} {chunkUnit}/s ({chunkBitSpeed:N2} {chunkBitUnit})");
    Console.WriteLine($"Average:  {avgSpeed:N2} {avgUnit}/s");
    Console.WriteLine($"Maximum:  {maxSpeed:N2} {maxUnit}/s");
    Console.WriteLine($"Elapsed:  {elapsed.TotalSeconds:N1}s | Remaining: {remaining.TotalSeconds:N1}s");
    Console.WriteLine($"Left:     {remainingBytes:N2} {remainingUnit}");
});
```

### Latency Tracking

Comprehensive latency measurement system tracks network performance metrics including Time To First Byte (TTFB) and per-packet latency statistics.

**Key Features:**

-   ✅ **Time To First Byte**: TTFB measurement in nanoseconds for accurate network latency
-   ✅ **Per-Packet Latency**: Individual packet timing for detailed performance analysis
-   ✅ **Statistical Analysis**: Minimum, maximum, and average latency calculations
-   ✅ **High-Precision Timing**: Nanosecond-level accuracy using Stopwatch
-   ✅ **Real-time Updates**: Live latency statistics during transfer operations
-   ✅ **Optional Tracking**: Can be enabled/disabled based on performance requirements

```csharp
// Enable comprehensive latency tracking
var latencyTracker = new LatencyTracker();

await client.GetAsync(url, stream, progress, interval: 100, bufferSize: 512, latencyTracker);

// Access latency statistics after transfer
if (latencyTracker.TimeToFirstByte.HasValue)
{
    Console.WriteLine($"Time to First Byte: {latencyTracker.TimeToFirstByte.Value:N0} ns");
}
Console.WriteLine($"Average Packet Latency: {latencyTracker.PacketAvgMs:N3} ms");
Console.WriteLine($"Latency Range: {latencyTracker.PacketMinMs:N3} - {latencyTracker.PacketMaxMs:N3} ms");
Console.WriteLine($"Packets Measured: {latencyTracker.PacketCount}");

// Use in progress reporting for real-time latency display
var progress = new Progress<TransferState>(state =>
{
    if (state.Latency != null && state.Latency.PacketCount > 0 && state.Latency.PacketMinMs >= 0)
    {
        Console.WriteLine($"Current Latency: {state.Latency.PacketAvgMs:N2} ms");
        if (state.Latency.TimeToFirstByte.HasValue)
        {
            Console.WriteLine($"TTFB: {state.Latency.TimeToFirstByte.Value / 1_000_000:N0} ms");
        }
    }
});
```

### Transfer Statistics

Detailed transfer statistics provide comprehensive insights into HTTP operation performance with multiple measurement perspectives.

**Statistical Measurements:**

-   **Current Chunk**: Real-time statistics for the most recent data transfer
-   **Total Transfer**: Cumulative statistics for the entire operation
-   **Average Rates**: Running average of transfer speeds throughout the operation
-   **Maximum Rates**: Peak performance measurements during transfer
-   **Time Analysis**: Elapsed time, estimated completion, and transfer duration

```csharp
// Access comprehensive transfer statistics
var progress = new Progress<TransferState>(state =>
{
    // Current chunk statistics (most recent transfer)
    Console.WriteLine($"Current Chunk: {state.Chunk.Transferred} bytes in {state.Chunk.Elapsed.TotalMilliseconds:N2} ms");

    // Total transfer statistics
    Console.WriteLine($"Total: {state.Total.Transferred:N0}/{state.TotalBytes:N0} bytes in {state.Total.Elapsed.TotalSeconds:N1}s");

    // Average performance over entire transfer
    Console.WriteLine($"Average Rate: {state.Average.ByteUnit.Speed:N2} {state.Average.ByteUnit.Size}/s");

    // Peak performance measurement
    Console.WriteLine($"Peak Rate: {state.Maximum.ByteUnit.Speed:N2} {state.Maximum.ByteUnit.Size}/s");

    // Performance analysis
    double efficiency = state.Average.RawSpeed / state.Maximum.RawSpeed * 100;
    Console.WriteLine($"Transfer Efficiency: {efficiency:N1}%");
});
```

### Performance Monitoring

Built-in performance monitoring capabilities provide insights into network conditions, transfer efficiency, and optimization opportunities.

**Monitoring Features:**

-   **Bandwidth Utilization**: Current vs. maximum throughput analysis
-   **Transfer Consistency**: Variation analysis in transfer rates
-   **Network Performance**: Latency trends and packet timing
-   **Efficiency Metrics**: Performance ratios and optimization indicators

## Give a ⭐

If you like or are using this project to learn or start your solution, please give it a star. Thanks! Also, if you find this library useful, and you're feeling really generous, then please consider [buying me a coffee ☕](https://bmc.link/gragra33).

## Documentation

The library provides comprehensive HTTP extension methods with detailed progress reporting and latency tracking capabilities for any .NET application requiring advanced HTTP monitoring.

### Core Extension Methods

The core extension methods provide powerful HTTP operations with built-in progress reporting and performance monitoring capabilities.

| Method                                                                                                | Description                                          | Use Case                                   |
| ----------------------------------------------------------------------------------------------------- | ---------------------------------------------------- | ------------------------------------------ |
| `GetAsync(url, stream, progress)`                                                                     | Downloads content to stream with progress reporting  | Basic file downloads with progress         |
| `GetAsync(url, stream, progress, interval, bufferSize, latencyTracker)`                               | Advanced download with customizable settings         | High-performance downloads with monitoring |
| `GetAsync(url, stream, progress, interval, bufferSize, latencyTracker, headers, cancellationToken)`   | Download with custom headers and cancellation        | Authenticated downloads with full control  |
| `PostAsync(url, filePath, progress, interval, bufferSize, latencyTracker, cancellationToken)`         | Uploads file using multipart/form-data with progress | File uploads with progress monitoring      |
| `PostAsync(url, content, progress, interval, bufferSize, latencyTracker, headers, cancellationToken)` | Advanced upload with custom content and headers      | Complex upload scenarios with full control |

### Progress Reporting Models

Comprehensive data models provide detailed insights into transfer operations and performance characteristics.

#### TransferState

Central state management for transfer operations with comprehensive statistics and progress tracking.

```csharp
public sealed class TransferState
{
    // Timing information
    public DateTimeOffset StartTime { get; }
    public DateTimeOffset LastCheckTime { get; }
    public DateTimeOffset StopTime { get; }

    // Transfer data
    public double TotalBytes { get; set; }
    public Transfer Total { get; set; }        // Cumulative statistics
    public Transfer Chunk { get; set; }        // Current chunk statistics
    public AverageTransfer Average { get; set; } // Running averages
    public Transfer Maximum { get; set; }      // Peak performance

    // Performance tracking
    public LatencyTracker? Latency { get; set; }

    // Calculation methods
    public double CalcProgressPercentage();
    public (double Bytes, ByteUnit Unit) CalcRemainingSize();
    public TimeSpan CalcEstimatedRemainingTime();
}
```

#### Transfer Rate Models

Detailed transfer rate calculations with multiple unit representations and statistical analysis.

```csharp
// Base transfer rate functionality
public abstract class TransferRateBase
{
    public double RawSpeed { get; protected set; }
    public (double Speed, ByteUnit Size) ByteUnit { get; protected set; }
    public (double Speed, BitUnit Size) BitUnit { get; protected set; }
}

// Individual transfer measurement
public sealed class Transfer : TransferRateBase
{
    public long Transferred { get; set; }
    public TimeSpan Elapsed { get; set; }
    public void CalcRates();
}

// Running average calculator
public sealed class AverageTransfer : TransferRateBase
{
    public int Count { get; set; }
    public void Update(TransferRateBase rate);
}
```

### Latency Tracking Models

High-precision latency measurement and statistical analysis for network performance monitoring.

#### LatencyTracker

Comprehensive latency tracking with nanosecond precision and statistical analysis capabilities.

```csharp
public sealed class LatencyTracker
{
    // Real-time measurements
    public double CurrentPacketMs { get; }

    // Statistical analysis
    public double PacketAvg { get; }           // Average in nanoseconds
    public double PacketAvgMs { get; }         // Average in milliseconds
    public double PacketMinMs { get; }         // Minimum in milliseconds
    public double PacketMaxMs { get; }         // Maximum in milliseconds
    public int PacketCount { get; }            // Number of measurements

    // Network performance
    public double? TimeToFirstByte { get; }    // TTFB in nanoseconds

    // Measurement method
    public void UpdatePacketLatency(double nanoseconds);
}
```

## API Reference

Complete API reference for all extension methods, models, and configuration options available in the library.

### HttpClient Extensions

Extension methods that enhance HttpClient with progress reporting and latency tracking capabilities.

#### Download Methods

```csharp
// Basic download with progress
Task GetAsync(
    this HttpClient client,
    string url,
    Stream destStream,
    IProgress<TransferState> progress,
    int interval = 100,
    int bufferSize = 512,
    LatencyTracker? latencyTracker = null)

// Advanced download with headers
Task GetAsync(
    this HttpClient client,
    string url,
    Stream destStream,
    IProgress<TransferState> progress,
    int interval,
    int bufferSize,
    LatencyTracker? latencyTracker,
    IDictionary<string, string>? headers)
```

**Parameters:**

-   `client`: HttpClient instance to use for the request
-   `url`: URL to download from
-   `destStream`: Destination stream for downloaded content
-   `progress`: Progress reporter for transfer state updates
-   `interval`: Progress reporting interval in milliseconds (default: 100)
-   `bufferSize`: Buffer size for stream operations (default: 512)
-   `latencyTracker`: Optional latency tracker for performance monitoring
-   `headers`: Optional custom headers for the request

#### Upload Methods

```csharp
// File upload with multipart/form-data
Task PostAsync(
    this HttpClient client,
    string url,
    string filePath,
    IProgress<TransferState> progress,
    int interval = 100,
    int bufferSize = 512,
    LatencyTracker? latencyTracker = null,
    CancellationToken cancellationToken = default)

// Advanced upload with custom content
Task PostAsync(
    this HttpClient client,
    string url,
    HttpContent content,
    IProgress<TransferState> progress,
    int interval = 100,
    int bufferSize = 512,
    LatencyTracker? latencyTracker = null,
    IDictionary<string, string>? headers = null,
    CancellationToken cancellationToken = default)
```

### Transfer State Models

Comprehensive models for tracking and reporting transfer progress and performance statistics.

#### Units and Measurements

```csharp
// Byte units for file size and transfer rate display
public enum ByteUnit
{
    B,      // Bytes
    KiB,    // Kibibytes (1024 bytes)
    MiB,    // Mebibytes (1024^2 bytes)
    GiB,    // Gibibytes (1024^3 bytes)
    TiB     // Tebibytes (1024^4 bytes)
}

// Bit units for network speed display
public enum BitUnit
{
    bit,    // Bits
    Kibit,  // Kibibits
    Mibit,  // Mebibits
    Gibit,  // Gibibits
    Tibit   // Tebibits
}
```

## Real-World Examples

Practical examples demonstrating common use cases and advanced scenarios for HTTP operations with comprehensive monitoring.

### Large File Download with UI Integration

Complete example showing integration with WPF UI for large file downloads with comprehensive progress reporting.

```csharp
public partial class DownloadManager : Window
{
    private readonly IHttpClientFactory _httpClientFactory;
    private CancellationTokenSource? _cancellationTokenSource;

    public DownloadManager(IHttpClientFactory httpClientFactory)
    {
        InitializeComponent();
        _httpClientFactory = httpClientFactory;
    }

    private async void StartDownload_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        var progress = new Progress<TransferState>(UpdateProgress);
        var latencyTracker = new LatencyTracker();

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromHours(2); // Long timeout for large files

            await using var fileStream = File.Create(DestinationPath.Text);

            await client.GetAsync(
                DownloadUrl.Text,
                fileStream,
                progress,
                interval: 100,
                bufferSize: 65536, // 64KB buffer
                latencyTracker,
                _cancellationTokenSource.Token);

            MessageBox.Show("Download completed successfully!");
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("Download was cancelled.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Download failed: {ex.Message}");
        }
    }

    private void UpdateProgress(TransferState state)
    {
        Dispatcher.Invoke(() =>
        {
            double percent = state.CalcProgressPercentage();
            ProgressBar.Value = percent * 100;

            PercentageLabel.Content = $"{percent:P1}";
            TransferredLabel.Content = $"{state.Total.Transferred:N0} / {state.TotalBytes:N0} bytes";

            var (speed, unit) = state.Chunk.ByteUnit;
            SpeedLabel.Content = $"{speed:N2} {unit}/s";

            var remaining = state.CalcEstimatedRemainingTime();
            if (remaining != TimeSpan.MinValue)
            {
                EtaLabel.Content = $"ETA: {remaining:hh\\:mm\\:ss}";
            }

            if (state.Latency != null && state.Latency.PacketCount > 0 && state.Latency.PacketMinMs >= 0)
            {
                LatencyLabel.Content = $"Latency: {state.Latency.PacketAvgMs:N2} ms";
            }
        });
    }
}
```

### Batch File Download with Parallel Processing

Example demonstrating parallel downloads with individual progress tracking for each file (based on ConsoleExample pattern).

```csharp
public class BatchDownloadManager
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BatchDownloadManager(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task DownloadFilesAsync(IEnumerable<DownloadItem> items)
    {
        var downloadTasks = items.Select(async (item, index) =>
        {
            var progress = new Progress<TransferState>(state =>
            {
                Console.SetCursorPosition(0, index * 3);
                var (speed, unit) = state.Chunk.ByteUnit;
                var (bitSpeed, bitUnit) = state.Chunk.BitUnit;

                bool isDownloading = Math.Abs(1 - state.CalcProgressPercentage()) > 0.001;
                Console.WriteLine($"File {index + 1}: {state.CalcProgressPercentage():P1} - {bitSpeed:N2} {bitUnit} | {speed:N2} {unit}/s {(isDownloading ? "" : "done.")}");
            });

            var latencyTracker = new LatencyTracker();

            using var client = _httpClientFactory.CreateClient();
            await using var fileStream = File.Create(item.DestinationPath);

            await client.GetAsync(
                item.Url,
                fileStream,
                progress,
                interval: 250,
                bufferSize: 512,
                latencyTracker);

            Console.SetCursorPosition(0, index * 3 + 1);
            Console.WriteLine($"Completed: {item.DestinationPath} - Avg Latency: {latencyTracker.PacketAvgMs:N2} ms");
        });

        await Task.WhenAll(downloadTasks);
    }
}

public record DownloadItem(string Url, string DestinationPath);
```

### API Integration with Progress Monitoring

Example showing integration with REST APIs that require file uploads with authentication and progress tracking.

```csharp
public class ApiFileUploader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;

    public ApiFileUploader(IHttpClientFactory httpClientFactory, string apiKey)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = apiKey;
    }

    public async Task<string> UploadDocumentAsync(string filePath, IProgress<TransferState> progress)
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {_apiKey}",
            ["X-Upload-Type"] = "document",
            ["X-Client-Version"] = "1.0.0"
        };

        var latencyTracker = new LatencyTracker();

        using var client = _httpClientFactory.CreateClient();

        var uploadResponse = await client.PostAsync(
            "https://api.example.com/documents/upload",
            filePath,
            progress,
            interval: 100,
            bufferSize: 512,
            latencyTracker,
            headers: headers);

        // Parse response for document ID
        var responseContent = await uploadResponse.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UploadResult>(responseContent);

        Console.WriteLine($"Upload completed - Latency: {latencyTracker.PacketAvgMs:N2} ms");
        if (latencyTracker.TimeToFirstByte.HasValue)
        {
            Console.WriteLine($"TTFB: {latencyTracker.TimeToFirstByte.Value / 1_000_000:N0} ms");
        }

        return result.DocumentId;
    }
}

public record UploadResult(string DocumentId);
```

## Sample Applications
The included sample applications provide practical, real-world examples of HTTP operations with comprehensive monitoring across different application types. All samples feature identical visual designs showcasing detailed progress reporting, latency tracking, and overall performance statistics.

### ConsoleExample - Complete Implementation
A comprehensive console application demonstrating all library features including downloads, uploads, progress reporting, and latency tracking with parallel processing.

**Location**: `samples/ConsoleExample/`

**Run the example**:
```bash
dotnet run --project samples/consoleExample --framework net8.0ss
````

**Key Features**:

-   **Interactive Menu**: Choose between download and upload operations
-   **Parallel Downloads**: Multiple simultaneous downloads with individual progress bars
-   **Real-time Progress**: Live console updates with transfer statistics
-   **Latency Monitoring**: Comprehensive latency tracking and reporting
-   **Error Handling**: Robust error handling with detailed error messages
-   **Performance Metrics**: Detailed performance analysis and statistics

**Core Implementation**:

```csharp
// Program.cs - Main entry point with HttpClientFactory setup
using Microsoft.Extensions.DependencyInjection;

// Initialize the IHttpClientFactory for creating HttpClient instances
IHttpClientFactory InitializeHttpClientFactory()
{
    ServiceCollection builder = new();
    builder.AddHttpClient();
    ServiceProvider serviceProvider = builder.BuildServiceProvider();
    return serviceProvider.GetRequiredService<IHttpClientFactory>();
}

IHttpClientFactory httpClientFactory = InitializeHttpClientFactory();

// Example download URL and file paths
string url = "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe";
string[] saveFiles = ["ac95c389-31ae-416f-a8cd-fdfb5969d528.cbz", "ac95c389-31ae-416f-a8cd-fdfb5969d529.cbz", "ac95c389-31ae-416f-a8cd-fdfb5969d527.cbz", "ac95c389-31ae-416f-a8cd-fdfb5969d526.cbz"];

// Run parallel downloads
await FileTransferHelper.RunDownloadAsync(httpClientFactory, new Uri(url), saveFiles, reportInterval: 250);
```

**FileTransferHelper Implementation**:

```csharp
public static class FileTransferHelper
{
    /// <summary>
    /// Runs multiple downloads in parallel, each with its own progress bar.
    /// </summary>
    public static async Task RunDownloadAsync(IHttpClientFactory httpClientFactory, Uri urlPath, string[] saveFiles, int interval)
    {
        Console.WriteLine($"Downloading: {urlPath}");
        Console.WriteLine();
        List<Task> downloadTasks = [];
        for (int i = 0; i < saveFiles.Length; i++)
        {
            // Each file gets its own progress bar at a different console row
            downloadTasks.Add(FileDownloadAsync(httpClientFactory, urlPath, saveFiles[i], 0, 8 + i, interval, true));
        }
        await Task.WhenAll(downloadTasks); // Wait for all downloads to finish
    }

    /// <summary>
    /// Handles a single file download with progress and latency tracking.
    /// </summary>
    public static async Task FileDownloadAsync(IHttpClientFactory httpClientFactory, Uri downloadUrl, string file, int left, int top, int interval = 100, bool isCompactMode = false)
    {
        await Task.Yield();
        using HttpClient client = httpClientFactory.CreateClient();
        await using FileStream fileStream = File.Create(file);

        Progress progress = new(left, top);
        LatencyTracker latency = new();

        await client.GetAsync(downloadUrl, fileStream,
            progress: new Progress<TransferState>(async state =>
            {
                if (isCompactMode)
                    await progress.CompactReport(state);
                else
                    await progress.Report(state);
            }),
            interval, 512, latency);
    }
}
```

**Progress Reporting**:

```csharp
public sealed class Progress
{
    public async Task CompactReport(TransferState state)
    {
        double progress = state.CalcProgressPercentage();
        (double Speed, ByteUnit Size) chunkByteRate = state.Chunk.ByteUnit;
        (double Speed, BitUnit Size) chunkBitRate = state.Chunk.BitUnit;

        bool isDownloading = Math.Abs(1 - progress) > 0.001;

        StringBuilder sb = new();
        sb.Append($"Receiving: {(progress < 0D ? "" : $"{progress:P0}")} ({state.Total.Transferred:N0}/{state.TotalBytes:N0}), {chunkBitRate.Speed:N2}{chunkBitRate.Size} | {chunkByteRate.Speed:N2}{chunkByteRate.Size}/s {(isDownloading ? "" : "done.")}");

        // Show latency if available
        if (state.Latency is not null && state.Latency.PacketCount > 0 && state.Latency.PacketMinMs >= 0)
        {
            sb.Append($"| Lat: {state.Latency.PacketAvgMs:N3} ms ({state.Latency.PacketMinMs:N5} ms - {state.Latency.PacketMaxMs:N3} ms | TTFB: {state.Latency.TimeToFirstByte:N0} ms");
        }

        DisplayReport(sb);
    }
}
```

### WinFormsExample - Visual Download Manager

A complete Windows Forms application showcasing parallel downloads with comprehensive visual progress tracking and performance statistics using dependency injection.

**Location**: `samples/WinFormsExample/`

**Run the example**:

```bash
dotnet run --project samples/WinFormsExample --framework net8.0-windows
```

**Key Features**:

-   **Modern UI Design**: Clean, professional interface with styled buttons and progress controls
-   **Parallel Downloads**: 4 simultaneous downloads with individual progress tracking
-   **Dependency Injection**: Uses `Blazing.Extensions.DependencyInjection` for automatic service registration
-   **Real-time Statistics**: Comprehensive performance dashboard with 8 statistics cards
-   **Start/Stop Control**: Full control over download operations with cancellation support
-   **Auto-cleanup**: Temporary files are automatically managed

**Service Registration** (`Program.cs`):

```csharp
using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

internal static class Program
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;

    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Configure services
        var services = new ServiceCollection();

        // Register HttpClient with factory
        services.AddHttpClient("DownloadClient", client =>
        {
            client.Timeout = TimeSpan.FromMinutes(30);
        });

        // Auto-discover and register all services with AutoRegister attribute
        services.Register(typeof(Program).Assembly);

        // Build service provider
        ServiceProvider = services.BuildServiceProvider();

        // Resolve and run main form
        var mainForm = ServiceProvider.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }
}
```

**DownloadService Implementation**:

```csharp
[AutoRegister(ServiceLifetime.Singleton)]
public class DownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DownloadService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("DownloadClient");
        await using var fileStream = File.Create(destinationPath);

        await client.GetAsync(
            url,
            fileStream,
            progress,
            interval: 100,
            bufferSize: 65536, // 64KB buffer
            latencyTracker,
            cancellationToken);
    }
}
```

**MainForm Implementation**:

```csharp
[AutoRegister(ServiceLifetime.Transient)]
public class MainForm : Form
{
    private readonly DownloadService _downloadService;
    private readonly List<(DownloadProgressControl Control, CancellationTokenSource Cts)> _downloadControls = new();

    // Download URLs for testing
    private readonly string[] _downloadUrls =
    [
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe"
    ];

    public MainForm(DownloadService downloadService)
    {
        _downloadService = downloadService;
        InitializeComponent();
    }
}
```

**Visual Features**:

-   Modern flat design with styled buttons (Start: blue, Stop: red)
-   Statistics dashboard with 8 performance cards showing real-time metrics
-   Individual download progress controls with detailed transfer information
-   Scrollable downloads list with professional styling
-   Progress bars with smooth animations and percentage display
-   Responsive layout adapting to window size

### WpfExample - MVVM Download Manager

A complete WPF application using MVVM pattern with `CommunityToolkit.Mvvm` and `Blazing.Extensions.DependencyInjection` for a clean, maintainable architecture featuring parallel downloads with comprehensive visual tracking.

**Location**: `samples/WpfExample/`

**Run the example**:

```bash
dotnet run --project samples/WpfExample --framework net8.0-windows
```

**Key Features**:

-   **MVVM Architecture**: Clean separation of concerns using `CommunityToolkit.Mvvm`
-   **Dependency Injection**: Uses `Blazing.Extensions.DependencyInjection` for automatic service registration
-   **Parallel Downloads**: 4 simultaneous downloads with individual progress tracking
-   **Data Binding**: Full WPF data binding with `ObservableObject` and `RelayCommand`
-   **Real-time Statistics**: Comprehensive performance dashboard
-   **Visual States**: Color-coded completion states and professional styling

**App.xaml.cs - Dependency Injection Setup**:

```csharp
using System.Windows;
using Blazing.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using WpfExample.Views;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure services using Blazing.Extensions.DependencyInjection
        this.ConfigureServices(services =>
        {
            // Register HttpClient with factory
            services.AddHttpClient("DownloadClient", client =>
            {
                client.Timeout = TimeSpan.FromMinutes(30);
            });

            // Auto-discover and register all services, ViewModels, and Views with AutoRegister attribute
            services.Register(typeof(App).Assembly);
        });

        // Resolve and show main window
        var serviceProvider = this.GetServices();
        var mainWindow = serviceProvider!.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```

**DownloadService Implementation**:

```csharp
[AutoRegister(ServiceLifetime.Singleton)]
public class DownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DownloadService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        CancellationToken cancellationToken = default)
    {
        using var client = _httpClientFactory.CreateClient("DownloadClient");
        await using var fileStream = File.Create(destinationPath);

        await client.GetAsync(
            url,
            fileStream,
            progress,
            interval: 100,
            bufferSize: 65536, // 64KB buffer
            latencyTracker,
            cancellationToken);
    }
}
```

**MainViewModel - MVVM with CommunityToolkit**:

```csharp
[AutoRegister(ServiceLifetime.Transient)]
public partial class MainViewModel : ObservableObject
{
    private readonly DownloadService _downloadService;
    private CancellationTokenSource? _globalCancellationTokenSource;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private ObservableCollection<DownloadItemViewModel> _downloads = new();

    // Statistics properties with ObservableProperty
    [ObservableProperty] private string _totalDownloadsText = "0";
    [ObservableProperty] private string _activeDownloadsText = "0";
    [ObservableProperty] private string _completedDownloadsText = "0";
    [ObservableProperty] private string _failedDownloadsText = "0";
    [ObservableProperty] private string _totalBytesText = "0 B";
    [ObservableProperty] private string _overallSpeedText = "0.00 B/s";
    [ObservableProperty] private string _averageLatencyText = "0.00 ms";
    [ObservableProperty] private string _totalElapsedText = "0.0s";

    // Download URLs for testing
    private readonly string[] _downloadUrls = new[]
    {
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe"
    };

    public MainViewModel(DownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    [RelayCommand]
    private async Task StartDownloadsAsync()
    {
        IsDownloading = true;
        Downloads.Clear();
        ResetStatistics();

        // Initialize statistics
        _totalDownloads = _downloadUrls.Length;
        _activeDownloads = _downloadUrls.Length;
        _startTime = DateTime.Now;
        UpdateStatisticsDisplay();

        // Create download items and start downloads
        var downloadTasks = new List<Task>();
        for (int i = 0; i < _downloadUrls.Length; i++)
        {
            var downloadItem = new DownloadItemViewModel
            {
                Url = _downloadUrls[i],
                FileName = $"dotnet-sdk-{i + 1}.exe"
            };
            Downloads.Add(downloadItem);
            downloadTasks.Add(StartSingleDownloadAsync(downloadItem, i));
        }

        await Task.WhenAll(downloadTasks);
        IsDownloading = false;
    }

    [RelayCommand]
    private void StopDownloads()
    {
        _globalCancellationTokenSource?.Cancel();
        IsDownloading = false;
    }
}
```

**Visual Design**:

-   Modern WPF styling with data binding and MVVM patterns
-   Statistics dashboard with 8 real-time performance cards
-   Individual download progress items with detailed transfer information
-   Professional color-coded states (green for success, red for errors)
-   Responsive XAML layout with proper data binding
-   Command pattern implementation with RelayCommand

**Dependency Injection Integration**:

Both WinForms and WPF examples demonstrate proper DI patterns:
- **WinFormsExample**: Traditional ServiceCollection pattern in Program.cs with static ServiceProvider
- **WpfExample**: Blazing.Extensions.DependencyInjection integration in App.xaml.cs with `ConfigureServices` extension

**Common Features Across All Samples**:
- ✅ Parallel download support
- ✅ Real-time progress reporting
- ✅ Latency tracking and TTFB measurements
- ✅ Transfer rate statistics (current/average/maximum)
- ✅ Time estimation (elapsed/remaining)
- ✅ Visual feedback (progress bars, colors)
- ✅ Start/stop control
- ✅ Error handling and visual error states
- ✅ Automatic resource cleanup

## Best Practices
Follow these recommended patterns for optimal performance, reliability, and maintainability when using the library.

### Error Handling
Implement comprehensive error handling for network operations, file I/O, and progress reporting scenarios.

```csharp
public async Task<bool> SafeDownloadAsync(string url, string filePath)
{
    var progress = new Progress<TransferState>(state =>
    {
        // Safe progress reporting with exception handling
        try
        {
            UpdateUI(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress UI");
        }
    });

    try
    {
        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(30);

        await using var fileStream = File.Create(filePath);

        await client.GetAsync(url, fileStream, progress,
            interval: 1000,
            bufferSize: 32768);

        return true;
    }
    catch (HttpRequestException ex)
    {
        _logger.LogError(ex, "HTTP error during download: {StatusCode}", ex.Data["StatusCode"]);
        return false;
    }
    catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
    {
        _logger.LogError("Download timed out after {Timeout}", client.Timeout);
        return false;
    }
    catch (IOException ex)
    {
        _logger.LogError(ex, "File I/O error during download");
        return false;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Unexpected error during download");
        return false;
    }
    finally
    {
        // Clean up partial files on failure
        if (File.Exists(filePath) && new FileInfo(filePath).Length == 0)
        {
            File.Delete(filePath);
        }
    }
}
````

### Memory Management

Optimize memory usage for large file operations and long-running applications.

```csharp
// Optimal configuration for different scenarios
public static class TransferConfig
{
    // Large files over fast connections
    public static readonly TransferSettings HighPerformance = new()
    {
        BufferSize = 1048576,    // 1MB buffer
        ReportInterval = 2000,   // Report every 2 seconds
        EnableLatencyTracking = true
    };

    // Constrained memory environments
    public static readonly TransferSettings MemoryEfficient = new()
    {
        BufferSize = 4096,       // 4KB buffer
        ReportInterval = 5000,   // Less frequent reporting
        EnableLatencyTracking = false
    };

    // Mobile/battery-conscious settings
    public static readonly TransferSettings BatteryOptimized = new()
    {
        BufferSize = 8192,       // 8KB buffer
        ReportInterval = 10000,  // Minimal UI updates
        EnableLatencyTracking = false
    };
}

// Proper resource disposal
public async Task TransferWithProperCleanupAsync()
{
    LatencyTracker? latencyTracker = null;
    HttpClient? client = null;
    Stream? fileStream = null;

    try
    {
        latencyTracker = new LatencyTracker();
        client = _httpClientFactory.CreateClient();
        fileStream = File.Create("download.tmp");

        await client.GetAsync(url, fileStream, progress,
            latencyTracker: latencyTracker);
    }
    finally
    {
        // Explicit cleanup in reverse order
        fileStream?.Dispose();
        client?.Dispose();
        latencyTracker = null;
    }
}
```

### Thread Safety

Ensure thread-safe operations when using progress reporting in multi-threaded scenarios.

```csharp
public class ThreadSafeProgressReporter
{
    private readonly object _lockObject = new();
    private readonly IProgress<TransferState> _progress;
    private DateTime _lastUpdate = DateTime.MinValue;
    private readonly TimeSpan _minimumInterval = TimeSpan.FromMilliseconds(100);

    public ThreadSafeProgressReporter(IProgress<TransferState> progress)
    {
        _progress = progress;
    }

    public void ReportProgress(TransferState state)
    {
        lock (_lockObject)
        {
            var now = DateTime.Now;
            if (now - _lastUpdate >= _minimumInterval)
            {
                _progress.Report(state);
                _lastUpdate = now;
            }
        }
    }
}

// Usage with multiple concurrent transfers
public async Task ConcurrentDownloadsAsync()
{
    var progressReporter = new ThreadSafeProgressReporter(
        new Progress<TransferState>(UpdateGlobalProgress));

    var tasks = urls.Select(async url =>
    {
        var localProgress = new Progress<TransferState>(state =>
        {
            progressReporter.ReportProgress(state);
        });

        using var client = _httpClientFactory.CreateClient();
        await using var stream = File.Create($"download-{Guid.NewGuid()}.tmp");

        await client.GetAsync(url, stream, localProgress);
    });

    await Task.WhenAll(tasks);
}
```

## Requirements

-   .NET 8.0 or .NET 9.0
-   Microsoft.Extensions.Http 8.0.0 or later (for IHttpClientFactory support)

## Project Structure

The solution is organized with clear separation between core functionality and sample applications, demonstrating usage across Console, WinForms, and WPF application types.

```
Blazing.Extensions.Http/                    # Solution root
├── src/
│   └── Blazing.Extensions.Http/            # Main HTTP extensions library
│       ├── Models/                         # Transfer and latency models
│       │   ├── TransferState.cs            # Main transfer state tracking
│       │   ├── LatencyTracker.cs           # Network latency measurement
│       │   ├── TransferRate.cs             # Transfer rate calculations
│       │   ├── AverageTransferRate.cs      # Running average calculations
│       │   ├── TransferRateBase.cs         # Base transfer rate functionality
│       │   ├── ByteUnit.cs                 # Byte unit enumeration
│       │   └── BitUnit.cs                  # Bit unit enumeration
│       └── HttpClientExtension.cs          # Core extension methods
├── samples/
│   ├── ConsoleExample/                     # Console application demo
│   │   ├── Program.cs                      # Main application entry
│   │   ├── FileTransferHelper.cs           # Transfer operation helpers
│   │   ├── Progress.cs                     # Progress reporting implementation
│   │   └── InternalLock.cs                 # Thread synchronization helper
│   ├── WinFormsExample/                    # Windows Forms application demo
│   │   ├── Program.cs                      # Application entry with DI setup
│   │   ├── MainForm.cs                     # Main form with download UI
│   │   ├── DownloadService.cs              # HTTP download service
│   │   ├── DownloadProgressControl.cs      # Individual download progress control
│   │   └── StatisticsPanel.cs              # Overall statistics display
│   └── WpfExample/                         # WPF application demo (MVVM)
│       ├── App.xaml                        # Application definition
│       ├── App.xaml.cs                     # Application startup with DI
│       ├── Services/
│       │   └── DownloadService.cs          # HTTP download service
│       ├── ViewModels/
│       │   ├── MainViewModel.cs            # Main window view model
│       │   └── DownloadItemViewModel.cs    # Per-download view model
│       ├── Views/
│       │   ├── MainWindow.xaml             # Main window XAML
│       │   └── MainWindow.xaml.cs          # Main window code-behind
│       └── Converters/
│           └── InverseBoolConverter.cs     # Boolean inversion converter
├── LICENSE                                 # MIT License
└── README.md                               # This file
```

## Building

The solution supports .NET 8.0 and .NET 9.0 and provides straightforward build and execution commands for all projects and samples.

```bash
# Build the entire solution
dotnet build

# Build only the library
dotnet build src/Blazing.Extensions.Http

# Create NuGet package
dotnet pack src/Blazing.Extensions.Http -c Release

# Run sample applications
dotnet run --project samples/ConsoleExample --framework net8.0          # Console example
dotnet run --project samples/WinFormsExample --framework net8.0-windows # WinForms example
dotnet run --project samples/WpfExample --framework net8.0-windows      # WPF example
```

## Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Setup

1. Clone the repository
2. Install .NET 8.0 or .NET 9.0 SDK
3. Run `dotnet restore`
4. Run `dotnet build`

### Running Examples

```bash
# Console example with interactive menu
dotnet run --project samples/ConsoleExample --framework net8.0

# WinForms example with visual download manager
dotnet run --project samples/WinFormsExample --framework net8.0-windows

# WPF example with MVVM architecture
dotnet run --project samples/WpfExample --framework net8.0-windows
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

-   Built on Microsoft's excellent [HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) and [IHttpClientFactory](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.http.ihttpclientfactory) implementations
-   Inspired by the need for comprehensive HTTP operation monitoring in modern .NET applications
-   High-precision timing implemented using [Stopwatch](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch) for accurate performance measurements

## History

### V1.0.0 (.Net 8.0+)

-   **Core HTTP Extensions** - Added comprehensive HttpClient extension methods for downloads and uploads
-   **Progress Reporting** - Implemented detailed progress reporting with TransferState model
-   **Latency Tracking** - Added high-precision latency measurement with LatencyTracker
-   **Transfer Statistics** - Comprehensive transfer rate calculations with multiple unit representations
-   **Custom Headers** - Support for custom headers in both download and upload operations
-   **Multipart Uploads** - Built-in multipart/form-data support for file uploads
-   **Performance Monitoring** - Real-time performance statistics including TTFB and packet latency
-   **Sample Applications** - Created three comprehensive sample applications:
    -   **ConsoleExample** - Interactive console app with parallel downloads
    -   **WinFormsExample** - Visual download manager with DI integration
    -   **WpfExample** - MVVM-based download manager using CommunityToolkit.Mvvm
-   **Memory Efficiency** - Optimized for minimal memory overhead during transfer operations
-   **Thread Safety** - Thread-safe progress reporting and state management
-   **Comprehensive API** - Full API coverage for common HTTP operation scenarios
