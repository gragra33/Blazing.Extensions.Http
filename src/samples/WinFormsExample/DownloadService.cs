using System.IO;
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
    /// <param name="resumeToken">Optional resume token from a prior cancellation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
    public async Task<DownloadResult> DownloadFileAsync(
        string url, 
        string destinationPath, 
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        ResumeToken? resumeToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        return await DownloadFileAsync(new Uri(url), destinationPath, progress, latencyTracker, resumeToken, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file with progress and latency tracking, supporting resume after cancellation.
    /// </summary>
    /// <param name="uri">The URI to download from.</param>
    /// <param name="destinationPath">The destination file path.</param>
    /// <param name="progress">Progress reporting mechanism.</param>
    /// <param name="latencyTracker">Latency tracking mechanism.</param>
    /// <param name="resumeToken">Optional resume token from a prior cancellation; when provided the file is opened for append and a Range request is sent.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
    public async Task<DownloadResult> DownloadFileAsync(
        Uri uri, 
        string destinationPath, 
        IProgress<TransferState> progress,
        LatencyTracker latencyTracker,
        ResumeToken? resumeToken = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(destinationPath);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(latencyTracker);

        using var client = _httpClientFactory.CreateClient("DownloadClient");

        if (resumeToken != null)
        {
            using FileStream fileStream = new(destinationPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            fileStream.Seek(resumeToken.BytesWritten, SeekOrigin.Begin);
            return await client.DownloadAsync(
                uri, fileStream, progress, resumeToken,
                interval: 100, bufferSize: 65536, latencyTracker, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using FileStream fileStream = File.Create(destinationPath);
            return await client.DownloadAsync(
                uri, fileStream, progress, resumeToken: null,
                interval: 100, bufferSize: 65536, latencyTracker, cancellationToken).ConfigureAwait(false);
        }
    }
}
