using System.IO;
using Blazing.Extensions.Http;
using Blazing.Extensions.Http.Models;

namespace ConsoleExample
{
    /// <summary>
    /// Helper class for file download and upload operations with progress and latency tracking.
    /// </summary>
    internal static class FileTransferHelper
    {
        /// <summary>
        /// Runs multiple downloads in parallel, each with its own progress bar.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="urlPath">The URL to download from.</param>
        /// <param name="saveFiles">Array of file paths to save the downloaded content.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token to cancel all downloads.</param>
        /// <returns>An array of <see cref="DownloadResult"/> for each file, in order.</returns>
        public static async Task<DownloadResult[]> RunDownloadAsync(IHttpClientFactory httpClientFactory, string urlPath, string[] saveFiles, int interval, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(urlPath);
            ArgumentNullException.ThrowIfNull(saveFiles);

            Console.WriteLine($"Downloading: {urlPath}");
            Console.WriteLine();
            List<Task<DownloadResult>> downloadTasks = [];
            for (int i = 0; i < saveFiles.Length; i++)
            {
                // Each file gets its own progress bar at a different console row
                downloadTasks.Add(FileDownloadAsync(httpClientFactory, new Uri(urlPath), saveFiles[i], null, 0, 8 + i, interval, true, cancellationToken));
            }
            return await Task.WhenAll(downloadTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs multiple downloads in parallel, each with its own progress bar.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="urlPath">The URL to download from.</param>
        /// <param name="saveFiles">Array of file paths to save the downloaded content.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token to cancel all downloads.</param>
        /// <returns>An array of <see cref="DownloadResult"/> for each file, in order.</returns>
        public static async Task<DownloadResult[]> RunDownloadAsync(IHttpClientFactory httpClientFactory, Uri urlPath, string[] saveFiles, int interval, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(urlPath);
            return await RunDownloadAsync(httpClientFactory, urlPath.ToString(), saveFiles, interval, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Resumes cancelled downloads from a previous <c>RunDownloadAsync</c> call.
        /// Downloads that were already successful are kept as-is in the result array.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="previousResults">Results from the previous run; cancelled entries have a non-null <see cref="DownloadResult.ResumeToken"/>.</param>
        /// <param name="saveFiles">Array of file paths matching the previous run.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="cancellationToken">Cancellation token to cancel all downloads.</param>
        /// <returns>An updated array of <see cref="DownloadResult"/> for each file, in order.</returns>
        public static async Task<DownloadResult[]> ResumeDownloadsAsync(IHttpClientFactory httpClientFactory, DownloadResult[] previousResults, string[] saveFiles, int interval, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(previousResults);
            ArgumentNullException.ThrowIfNull(saveFiles);

            List<Task<DownloadResult>> tasks = [];
            for (int i = 0; i < previousResults.Length; i++)
            {
                DownloadResult prev = previousResults[i];
                if (prev.IsSuccess || prev.ResumeToken is null)
                {
                    tasks.Add(Task.FromResult(prev)); // already done or unresumable error
                }
                else
                {
                    int top = 8 + i;
                    Progress.MarkResuming(top);
                    tasks.Add(FileDownloadAsync(httpClientFactory, prev.ResumeToken.Url, saveFiles[i], prev.ResumeToken, 0, top, interval, true, cancellationToken));
                }
            }
            return await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs a file upload with progress reporting.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="urlPath">The URL to upload to.</param>
        /// <param name="file">The file path to upload.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunUploadAsync(IHttpClientFactory httpClientFactory, string urlPath, string file, int interval)
        {
            await FileUploadAsync(httpClientFactory, new Uri(urlPath), file, 0, 3, interval, true).ConfigureAwait(false);
        }

        /// <summary>
        /// Runs a file upload with progress reporting.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="urlPath">The URL to upload to.</param>
        /// <param name="file">The file path to upload.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunUploadAsync(IHttpClientFactory httpClientFactory, Uri urlPath, string file, int interval)
        {
            ArgumentNullException.ThrowIfNull(urlPath);
            await RunUploadAsync(httpClientFactory, urlPath.ToString(), file, interval).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles a single file download with progress and latency tracking.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="resumeToken">Optional resume token from a prior cancellation.</param>
        /// <param name="left">Console cursor left position.</param>
        /// <param name="top">Console cursor top position.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="isCompactMode">Whether to use compact progress reporting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
#pragma warning disable S107 // Methods intentionally have many parameters
        public static async Task<DownloadResult> FileDownloadAsync(IHttpClientFactory httpClientFactory, string downloadUrl, string file, ResumeToken? resumeToken, int left, int top, int interval = 100, bool isCompactMode = false, CancellationToken cancellationToken = default)
        {
            return await FileDownloadAsync(httpClientFactory, new Uri(downloadUrl), file, resumeToken, left, top, interval, isCompactMode, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles a single file download with progress and latency tracking.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="resumeToken">Optional resume token from a prior cancellation.</param>
        /// <param name="left">Console cursor left position.</param>
        /// <param name="top">Console cursor top position.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="isCompactMode">Whether to use compact progress reporting.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
        public static async Task<DownloadResult> FileDownloadAsync(IHttpClientFactory httpClientFactory, Uri downloadUrl, string file, ResumeToken? resumeToken, int left, int top, int interval = 100, bool isCompactMode = false, CancellationToken cancellationToken = default)
        {
            Progress progress = new(left, top);
            Progress<TransferState> progressHandler = new(x => _ = isCompactMode ? progress.CompactReport(x) : progress.Report(x));
            LatencyTracker latency = new();
            DownloadResult result = await DownloadFileAsync(httpClientFactory, downloadUrl, file, resumeToken, progressHandler, interval, latency, cancellationToken).ConfigureAwait(false);
            if (!result.IsSuccess && result.ResumeToken != null)
                Progress.MarkCancelled(top);
            return result;
        }

        /// <summary>
        /// Performs the actual file download using HttpClient extension method.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="resumeToken">Optional resume token from a prior cancellation.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="latency">Optional latency tracker for TTFB measurement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
        public static async Task<DownloadResult> DownloadFileAsync(IHttpClientFactory httpClientFactory, string downloadUrl, string file, ResumeToken? resumeToken, IProgress<TransferState> progress, int interval = 100, LatencyTracker? latency = null, CancellationToken cancellationToken = default)
        {
            return await DownloadFileAsync(httpClientFactory, new Uri(downloadUrl), file, resumeToken, progress, interval, latency, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the actual file download using HttpClient extension method.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="resumeToken">Optional resume token from a prior cancellation.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="latency">Optional latency tracker for TTFB measurement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
        public static async Task<DownloadResult> DownloadFileAsync(IHttpClientFactory httpClientFactory, Uri downloadUrl, string file, ResumeToken? resumeToken, IProgress<TransferState> progress, int interval = 100, LatencyTracker? latency = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(downloadUrl);
            using HttpClient client = httpClientFactory.CreateClient("CodeProjectHelp");

            if (resumeToken != null)
            {
                using FileStream fileStream = new(file, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
                fileStream.Seek(resumeToken.BytesWritten, SeekOrigin.Begin);
                return await client.DownloadAsync(downloadUrl, fileStream, progress, resumeToken, interval, 512, latency, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using FileStream fileStream = File.Create(file);
                return await client.DownloadAsync(downloadUrl, fileStream, progress, resumeToken: null, interval, 512, latency, cancellationToken).ConfigureAwait(false);
            }
        }
#pragma warning restore S107

        /// <summary>
        /// Handles a single file upload with progress and latency tracking.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="uploadUrl">The URL to upload to.</param>
        /// <param name="file">The file path to upload.</param>
        /// <param name="left">Console cursor left position.</param>
        /// <param name="top">Console cursor top position.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="isCompactMode">Whether to use compact progress reporting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task FileUploadAsync(IHttpClientFactory httpClientFactory, string uploadUrl, string file, int left, int top, int interval = 100, bool isCompactMode = false)
        {
            await FileUploadAsync(httpClientFactory, new Uri(uploadUrl), file, left, top, interval, isCompactMode).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles a single file upload with progress and latency tracking.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="uploadUrl">The URL to upload to.</param>
        /// <param name="file">The file path to upload.</param>
        /// <param name="left">Console cursor left position.</param>
        /// <param name="top">Console cursor top position.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="isCompactMode">Whether to use compact progress reporting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task FileUploadAsync(IHttpClientFactory httpClientFactory, Uri uploadUrl, string file, int left, int top, int interval = 100, bool isCompactMode = false)
        {
            Progress progress = new(left, top);
            Progress<TransferState> progressHandler = new(x => _ = isCompactMode ? progress.CompactReport(x) : progress.Report(x));
            LatencyTracker latency = new();
            await UploadFileAsync(httpClientFactory, uploadUrl, file, progressHandler, interval, latency).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the actual file upload using HttpClient extension method.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="uploadUrl">The URL to upload to.</param>
        /// <param name="file">The file path to upload.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="latency">Optional latency tracker for TTFB measurement.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task UploadFileAsync(IHttpClientFactory httpClientFactory, string uploadUrl, string file, IProgress<TransferState> progress, int interval = 100, LatencyTracker? latency = null)
        {
            await UploadFileAsync(httpClientFactory, new Uri(uploadUrl), file, progress, interval, latency).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the actual file upload using HttpClient extension method.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="uploadUrl">The URL to upload to.</param>
        /// <param name="file">The file path to upload.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="latency">Optional latency tracker for TTFB measurement.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task UploadFileAsync(IHttpClientFactory httpClientFactory, Uri uploadUrl, string file, IProgress<TransferState> progress, int interval = 100, LatencyTracker? latency = null)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(uploadUrl);
            await Task.Yield();
            using HttpClient client = httpClientFactory.CreateClient("CodeProjectHelp");
            await client.PostAsync(uploadUrl, file, progress, interval, 512, latency).ConfigureAwait(false);
        }
    }
}
