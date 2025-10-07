using System.Diagnostics;
using System.Net;
using Blazing.Extensions.Http.Models;

namespace Blazing.Extensions.Http;

/// <summary>
/// Provides extension methods for HttpClient with progress reporting and latency tracking capabilities.
/// </summary>
public static class HttpClientExtension
{
    /// <summary>
    /// Downloads a file from the specified URL to the provided destination stream, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public static async Task GetAsync(
        this HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        await GetAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file from the specified URL to the provided destination stream, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="Url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public static async Task GetAsync(
        this HttpClient client,
        string Url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        await GetAsync(client, new Uri(Url), destStream, progress, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file from the specified URL to the provided destination stream, with optional headers, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="headers">Optional headers to add to the request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public static async Task GetAsync(
        this HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval,
        int bufferSize,
        LatencyTracker? latencyTracker,
        IDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(destStream);
        ArgumentNullException.ThrowIfNull(progress);
        
        // Apply custom headers if provided
        if (headers != null)
        {
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                _ = client.DefaultRequestHeaders.Remove(kvp.Key);
                client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }
        }
        await InternalGetAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file from the specified URL to the provided destination stream, with optional headers, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="Url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="headers">Optional headers to add to the request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public static async Task GetAsync(
        this HttpClient client,
        string Url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval,
        int bufferSize,
        LatencyTracker? latencyTracker,
        IDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        await GetAsync(client, new Uri(Url), destStream, progress, interval, bufferSize, latencyTracker, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Core logic for downloading a file, reporting progress, and tracking latency.
    /// </summary>
    private static async Task InternalGetAsync(
        HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval,
        int bufferSize,
        LatencyTracker? latencyTracker,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;
        try
        {
            // Capture timestamp before request for accurate TimeToFirstByte
            long requestStartTicks = Stopwatch.GetTimestamp();
            response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = string.Empty;

                try
                {
                    errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Ignore errors when reading error content during cancellation
                }
                catch (HttpRequestException)
                {
                    // Ignore errors when reading error content during HTTP errors
                }

                throw new HttpRequestException($"{response.ReasonPhrase}: {errorContent}", null, response.StatusCode);
            }

            // Websites report either, not both for content length
            long? length = response.Content.Headers.ContentLength;
            long? totalBytes = response.Content.Headers.ContentRange?.Length;

            if (!totalBytes.HasValue && length.HasValue)
            {
                totalBytes = length.Value;
            }

            int position = 0;
            int bytesRead;
            byte[] buffer = new byte[bufferSize];

            TransferState transferState = new();
            transferState.Start(totalBytes ?? 0);

            Stopwatch stopwatch = Stopwatch.StartNew();
            long lastReport = 0;
            long lastPacketTicks = requestStartTicks;
            double tickFrequency = 1_000_000_000.0 / Stopwatch.Frequency; // ns per tick

            while ((bytesRead = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                if (latencyTracker != null)
                {
                    double packetNs = (nowTicks - lastPacketTicks) * tickFrequency;
                    latencyTracker.UpdatePacketLatency(packetNs);
                    // Always update the transferState.Latency reference so UI can display latest values
                    transferState.Latency = latencyTracker;
                }
                lastPacketTicks = nowTicks;
                position += bytesRead;

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastReport >= interval)
                {
                    lastReport = now;
                    progress.Report(transferState.Update(position));
                    position = 0;
                }
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            }
            // Mark transfer as complete
            transferState.Stop();
            progress.Report(transferState.Update(position));
        }
        catch (Exception)
        {
            response?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Uploads a file to the specified URL using multipart/form-data, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to upload to.</param>
    /// <param name="filePath">The file path to upload.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the file.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public static async Task PostAsync(
        this HttpClient client,
        string url,
        string filePath,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        FileInfo fileInfo = new(filePath);
        long totalBytes = fileInfo.Length;
        FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        await using (fileStream.ConfigureAwait(false))
        {
            using ProgressStreamContent streamContent = new(fileStream, bufferSize, progress, totalBytes, interval, latencyTracker, cancellationToken);
            using MultipartFormDataContent content = new()
            {
                { streamContent, "file", Path.GetFileName(filePath) }
            };
            await PostAsync(client, new Uri(url), content, progress, interval, bufferSize, latencyTracker, null, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Uploads a file to the specified URL using multipart/form-data, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to upload to.</param>
    /// <param name="filePath">The file path to upload.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the file.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
#pragma warning disable CA1801, IDE0060 // Remove unused parameter - API compatibility
    public static async Task PostAsync(
        this HttpClient client,
        Uri url,
        string filePath,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);
        await PostAsync(client, url.ToString(), filePath, progress, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);
    }
#pragma warning restore CA1801, IDE0060

    /// <summary>
    /// Uploads content to the specified URL, with optional headers, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to upload to.</param>
    /// <param name="content">The HTTP content to upload.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the content.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="headers">Optional headers to add to the request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public static async Task PostAsync(
        this HttpClient client,
        string url,
        HttpContent content,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        
        // Apply custom headers if provided
        if (headers != null)
        {
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                _ = client.DefaultRequestHeaders.Remove(kvp.Key);
                client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }
        }
        await InternalPostAsync(client, new Uri(url), content, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Uploads content to the specified URL, with optional headers, reporting progress and tracking latency.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to upload to.</param>
    /// <param name="content">The HTTP content to upload.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the content.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="headers">Optional headers to add to the request.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
#pragma warning disable CA1801, IDE0060 // Remove unused parameter - API compatibility
    public static async Task PostAsync(
        this HttpClient client,
        Uri url,
        HttpContent content,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        IDictionary<string, string>? headers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(url);
        if (headers is not null)
        {
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                _ = client.DefaultRequestHeaders.Remove(kvp.Key);
                client.DefaultRequestHeaders.Add(kvp.Key, kvp.Value);
            }
        }
        await InternalPostAsync(client, url, content, cancellationToken).ConfigureAwait(false);
    }
#pragma warning restore CA1801, IDE0060

    /// <summary>
    /// Core logic for uploading content, reporting progress, and tracking latency.
    /// </summary>
    private static async Task InternalPostAsync(
        HttpClient client,
        Uri url,
        HttpContent content,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;
        try
        {
            response = await client.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = string.Empty;
                try
                {
                    errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Ignore errors when reading error content during cancellation
                }
                catch (HttpRequestException)
                {
                    // Ignore errors when reading error content during HTTP errors
                }
                throw new HttpRequestException($"{response.ReasonPhrase}: {errorContent}", null, response.StatusCode);
            }
        }
        catch (Exception)
        {
            response?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// HttpContent wrapper for reporting upload progress.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="bufferSize">The buffer size for reading.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="totalBytes">Total bytes to upload.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    private sealed class ProgressStreamContent(Stream stream, int bufferSize, IProgress<TransferState> progress, long totalBytes, int interval, LatencyTracker? latencyTracker = null, CancellationToken cancellationToken = default) : HttpContent
    {
        private readonly Stream _stream = stream;
        private readonly int _bufferSize = bufferSize;
        private readonly IProgress<TransferState> _progress = progress;
        private readonly long _totalBytes = totalBytes;
        private readonly int _interval = interval;
        private readonly TransferState _transferState = new();
        private readonly LatencyTracker? _latencyTracker = latencyTracker;
        private readonly CancellationToken _cancellationToken = cancellationToken;
        private long _uploaded;
        private bool _started;

        /// <summary>
        /// Serializes the stream to the outgoing HTTP request, reporting progress.
        /// </summary>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            byte[] buffer = new byte[_bufferSize];
            int bytesRead;
            Stopwatch stopwatch = Stopwatch.StartNew();
            long lastReport = 0;
            long lastPacketTicks = Stopwatch.GetTimestamp();
            double tickFrequency = 1_000_000_000.0 / Stopwatch.Frequency;
            if (!_started)
            {
                _transferState.Start(_totalBytes);
                _started = true;
            }
            while ((bytesRead = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length), _cancellationToken).ConfigureAwait(false)) > 0)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                if (_latencyTracker != null)
                {
                    double packetNs = (nowTicks - lastPacketTicks) * tickFrequency;
                    _latencyTracker.UpdatePacketLatency(packetNs);
                    // Always update the transferState.Latency reference so UI can display latest values
                    _transferState.Latency = _latencyTracker;
                }
                lastPacketTicks = nowTicks;

                await stream.WriteAsync(buffer.AsMemory(0, bytesRead), _cancellationToken).ConfigureAwait(false);
                _uploaded += bytesRead;

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastReport >= _interval)
                {
                    lastReport = now;
                    _progress.Report(_transferState.Update((int)_uploaded));
                    _uploaded = 0;
                }
            }
            // Mark transfer as complete
            _transferState.Stop();
            _progress.Report(_transferState.Update(0));
        }

        /// <summary>
        /// Tries to compute the length of the stream.
        /// </summary>
        protected override bool TryComputeLength(out long length)
        {
            length = _totalBytes;
            return true;
        }

        /// <summary>
        /// Disposes of the resources used by the ProgressStreamContent.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}