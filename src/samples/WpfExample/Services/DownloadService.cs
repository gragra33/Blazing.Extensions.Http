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
        /// Downloads a file with progress and latency tracking.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="latencyTracker">Latency tracker for TTFB measurement.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DownloadFileAsync(
            string url,
            string destinationPath,
            IProgress<TransferState> progress,
            LatencyTracker latencyTracker,
            CancellationToken cancellationToken = default)
        {
            await DownloadFileAsync(new Uri(url), destinationPath, progress, latencyTracker, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Downloads a file with progress and latency tracking.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="destinationPath">The destination file path.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="latencyTracker">Latency tracker for TTFB measurement.</param>
        /// <param name="cancellationToken">Cancellation token for the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task DownloadFileAsync(
            Uri url,
            string destinationPath,
            IProgress<TransferState> progress,
            LatencyTracker latencyTracker,
            CancellationToken cancellationToken = default)
        {
            using var client = httpClientFactory.CreateClient("DownloadClient");
#pragma warning disable CA2000 // Dispose objects before losing scope - fileStream is disposed by await using
            await using FileStream fileStream = File.Create(destinationPath);
#pragma warning restore CA2000

            await client.GetAsync(
                url,
                fileStream,
                progress,
                interval: 100,
                bufferSize: 65536, // 64KB buffer
                latencyTracker,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
