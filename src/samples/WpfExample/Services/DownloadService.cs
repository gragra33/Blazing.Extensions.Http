using System.IO;
using System.Net.Http;
using Blazing.Extensions.DependencyInjection;
using Blazing.Extensions.Http;
using Blazing.Extensions.Http.Models;
using Microsoft.Extensions.DependencyInjection;

namespace WpfExample.Services
{
    /// <summary>
    /// Service responsible for managing file downloads with progress tracking.
    /// </summary>
    [AutoRegister(ServiceLifetime.Singleton)]
#pragma warning disable CA1812 // Avoid uninstantiated internal classes - Class is instantiated via DI
    public sealed class DownloadService(IHttpClientFactory httpClientFactory)
#pragma warning restore CA1812
    {
        /// <summary>
        /// Downloads a file with progress and latency tracking, supporting resume after cancellation.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="latencyTracker">Latency tracker for TTFB measurement.</param>
        /// <param name="resumeToken">Optional resume token from a prior cancellation; when provided the file is opened for append and a Range request is sent.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
        public async Task<DownloadResult> DownloadFileAsync(
            string url,
            string destinationPath,
            IProgress<TransferState> progress,
            LatencyTracker latencyTracker,
            ResumeToken? resumeToken = null,
            CancellationToken cancellationToken = default)
        {
            return await DownloadFileAsync(new Uri(url), destinationPath, progress, latencyTracker, resumeToken, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file with progress and latency tracking, supporting resume after cancellation.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="latencyTracker">Latency tracker for TTFB measurement.</param>
        /// <param name="resumeToken">Optional resume token from a prior cancellation; when provided the file is opened for append and a Range request is sent.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
        public async Task<DownloadResult> DownloadFileAsync(
            Uri url,
            string destinationPath,
            IProgress<TransferState> progress,
            LatencyTracker latencyTracker,
            ResumeToken? resumeToken,
            CancellationToken cancellationToken = default)
        {
            using var client = httpClientFactory.CreateClient("DownloadClient");

            if (resumeToken != null)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope - fileStream is disposed by await using
                await using FileStream fileStream = new(destinationPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
#pragma warning restore CA2000
                fileStream.Seek(resumeToken.BytesWritten, SeekOrigin.Begin);
                return await client.DownloadAsync(
                    url,
                    fileStream,
                    progress,
                    resumeToken,
                    interval: 100,
                    bufferSize: 65536,
                    latencyTracker,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
#pragma warning disable CA2000 // Dispose objects before losing scope - fileStream is disposed by await using
                await using FileStream fileStream = File.Create(destinationPath);
#pragma warning restore CA2000
                return await client.DownloadAsync(
                    url,
                    fileStream,
                    progress,
                    resumeToken: null,
                    interval: 100,
                    bufferSize: 65536,
                    latencyTracker,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
