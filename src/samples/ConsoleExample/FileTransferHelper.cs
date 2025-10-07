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
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunDownloadAsync(IHttpClientFactory httpClientFactory, string urlPath, string[] saveFiles, int interval)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(urlPath);
            ArgumentNullException.ThrowIfNull(saveFiles);

            Console.WriteLine($"Downloading: {urlPath}");
            Console.WriteLine();
            List<Task> downloadTasks = [];
            for (int i = 0; i < saveFiles.Length; i++)
            {
                // Each file gets its own progress bar at a different console row
                downloadTasks.Add(FileDownloadAsync(httpClientFactory, new Uri(urlPath), saveFiles[i], 0, 8 + i, interval, true));
            }
            await Task.WhenAll(downloadTasks).ConfigureAwait(false); // Wait for all downloads to finish
        }

        /// <summary>
        /// Runs multiple downloads in parallel, each with its own progress bar.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="urlPath">The URL to download from.</param>
        /// <param name="saveFiles">Array of file paths to save the downloaded content.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunDownloadAsync(IHttpClientFactory httpClientFactory, Uri urlPath, string[] saveFiles, int interval)
        {
            ArgumentNullException.ThrowIfNull(urlPath);
            await RunDownloadAsync(httpClientFactory, urlPath.ToString(), saveFiles, interval).ConfigureAwait(false);
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
        /// <param name="left">Console cursor left position.</param>
        /// <param name="top">Console cursor top position.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="isCompactMode">Whether to use compact progress reporting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task FileDownloadAsync(IHttpClientFactory httpClientFactory, string downloadUrl, string file, int left, int top, int interval = 100, bool isCompactMode = false)
        {
            await FileDownloadAsync(httpClientFactory, new Uri(downloadUrl), file, left, top, interval, isCompactMode).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles a single file download with progress and latency tracking.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="left">Console cursor left position.</param>
        /// <param name="top">Console cursor top position.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="isCompactMode">Whether to use compact progress reporting.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task FileDownloadAsync(IHttpClientFactory httpClientFactory, Uri downloadUrl, string file, int left, int top, int interval = 100, bool isCompactMode = false)
        {
            Progress progress = new(left, top);
            Progress<TransferState> progressHandler = new(x => _ = isCompactMode ? progress.CompactReport(x) : progress.Report(x));
            LatencyTracker latency = new();
            await DownloadFileAsync(httpClientFactory, downloadUrl, file, progressHandler, interval, latency).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the actual file download using HttpClient extension method.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="latency">Optional latency tracker for TTFB measurement.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task DownloadFileAsync(IHttpClientFactory httpClientFactory, string downloadUrl, string file, IProgress<TransferState> progress, int interval = 100, LatencyTracker? latency = null)
        {
            await DownloadFileAsync(httpClientFactory, new Uri(downloadUrl), file, progress, interval, latency).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the actual file download using HttpClient extension method.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        /// <param name="downloadUrl">The URL to download from.</param>
        /// <param name="file">The file path to save the downloaded content.</param>
        /// <param name="progress">Progress reporter for transfer state.</param>
        /// <param name="interval">Progress reporting interval in milliseconds.</param>
        /// <param name="latency">Optional latency tracker for TTFB measurement.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task DownloadFileAsync(IHttpClientFactory httpClientFactory, Uri downloadUrl, string file, IProgress<TransferState> progress, int interval = 100, LatencyTracker? latency = null)
        {
            ArgumentNullException.ThrowIfNull(httpClientFactory);
            ArgumentNullException.ThrowIfNull(downloadUrl);
            await Task.Yield();
            using HttpClient client = httpClientFactory.CreateClient("CodeProjectHelp");
            using FileStream fileStream = File.Create(file);
            await client.GetAsync(downloadUrl, fileStream, progress, interval, 512, latency).ConfigureAwait(false);
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
