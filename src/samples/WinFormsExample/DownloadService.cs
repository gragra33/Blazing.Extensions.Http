using Blazing.Extensions.DependencyInjection;
using Blazing.Extensions.Http;
using Blazing.Extensions.Http.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WinFormsExample;

/// <summary>
/// Service responsible for managing file downloads with progress tracking.
/// </summary>
[AutoRegister(ServiceLifetime.Singleton)]
internal sealed class DownloadService
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="DownloadService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    public DownloadService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Downloads a file with progress and latency tracking.
    /// </summary>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="progress">Progress reporting mechanism.</param>
    /// <param name="latencyTracker">Latency tracking mechanism.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DownloadFileAsync(
        string url, 
        string destinationPath, 
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await DownloadFileAsync(new Uri(url), destinationPath, progress, latencyTracker, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file with progress and latency tracking.
    /// </summary>
    /// <param name="uri">The URI to download from.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="progress">Progress reporting mechanism.</param>
    /// <param name="latencyTracker">Latency tracking mechanism.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task DownloadFileAsync(
        Uri uri, 
        string destinationPath, 
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(destinationPath);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(latencyTracker);

        using var client = _httpClientFactory.CreateClient("DownloadClient");
        using FileStream fileStream = File.Create(destinationPath);
        
        await client.GetAsync(
            uri,
            fileStream,
            progress,
            interval: 100,
            bufferSize: 65536, // 64KB buffer
            latencyTracker,
            cancellationToken).ConfigureAwait(false);
    }
}
