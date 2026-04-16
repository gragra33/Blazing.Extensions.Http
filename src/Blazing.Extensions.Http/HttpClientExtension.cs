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
    /// <returns>A <see cref="GetResult"/> indicating success or failure; never throws on HTTP errors or cancellation.</returns>
    public static async Task<GetResult> GetAsync(
        this HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, null, cancellationToken).ConfigureAwait(false);
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
    /// <returns>A <see cref="GetResult"/> indicating success or failure; never throws on HTTP errors or cancellation.</returns>
    public static async Task<GetResult> GetAsync(
        this HttpClient client,
        string Url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        return await GetAsync(client, new Uri(Url), destStream, progress, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);
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
    /// <returns>A <see cref="GetResult"/> indicating success or failure; never throws on HTTP errors or cancellation.</returns>
    public static async Task<GetResult> GetAsync(
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
        return await InternalGetAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);
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
    /// <returns>A <see cref="GetResult"/> indicating success or failure; never throws on HTTP errors or cancellation.</returns>
    public static async Task<GetResult> GetAsync(
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
        return await GetAsync(client, new Uri(Url), destStream, progress, interval, bufferSize, latencyTracker, headers, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Core logic for downloading a file, reporting progress, and tracking latency.
    /// Returns a <see cref="GetResult"/>; never throws on HTTP errors or cancellation.
    /// </summary>
    private static async Task<GetResult> InternalGetAsync(
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

                return GetResult.Failed(response.StatusCode, $"{response.ReasonPhrase}: {errorContent}");
            }

            Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

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
            return GetResult.Ok(response.StatusCode);
        }
        catch (OperationCanceledException ex)
        {
            return GetResult.Failed(null, "Cancelled", ex);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            response?.Dispose();
            return GetResult.Failed(null, ex.Message, ex);
        }
    }

    /// <summary>
    /// Core logic for a resumable file download using HTTP Range Requests.
    /// Returns a <see cref="DownloadResult"/>; never throws on HTTP errors or cancellation.
    /// On cancellation, the result carries a <see cref="ResumeToken"/> for subsequent resume.
    /// </summary>
    private static async Task<DownloadResult> InternalDownloadAsync(
        HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        int interval,
        int bufferSize,
        LatencyTracker? latencyTracker,
        ResumeToken? resumeToken,
        CancellationToken cancellationToken,
        IDictionary<string, string>? headers = null)
    {
        HttpResponseMessage? response = null;
        string? etag = null;
        DateTimeOffset? lastModified = null;
        long bytesWritten = resumeToken?.BytesWritten ?? 0L;

        try
        {
            long requestStartTicks = Stopwatch.GetTimestamp();
            using HttpRequestMessage request = new(HttpMethod.Get, url);

            // Apply optional custom headers
            if (headers != null)
            {
                foreach (KeyValuePair<string, string> kvp in headers)
                    request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);
            }

            // Apply Range and If-Range headers for resume
            if (resumeToken != null)
            {
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(resumeToken.BytesWritten, null);
                string? ifRange = resumeToken.ETag ?? resumeToken.LastModified?.ToString("R");
                if (ifRange != null)
                    request.Headers.TryAddWithoutValidation("If-Range", ifRange);
            }

            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            // Capture validators for future resume tokens
            etag = response.Headers.ETag?.Tag;
            lastModified = response.Content.Headers.LastModified;

            // If we requested a range but got 200 OK, the server doesn't support Range requests
            if (resumeToken != null && response.StatusCode == HttpStatusCode.OK)
                return DownloadResult.Failed(response.StatusCode, "Server does not support Range requests");

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = string.Empty;
                try { errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    // Ignore errors when reading error content during cancellation
                }
                catch (HttpRequestException)
                {
                    // Ignore errors when reading error content during HTTP errors
                }
                return DownloadResult.Failed(response.StatusCode, $"{response.ReasonPhrase}: {errorContent}");
            }

            long? length = response.Content.Headers.ContentLength;
            long? totalBytes = response.Content.Headers.ContentRange?.Length ?? length;

            long startOffset = resumeToken?.BytesWritten ?? 0L;

            Stream httpStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            int position = 0;
            int bytesRead;
            byte[] buffer = new byte[bufferSize];

            TransferState transferState = new();
            transferState.Start(totalBytes, startOffset);

            Stopwatch stopwatch = Stopwatch.StartNew();
            long lastReport = 0;
            long lastPacketTicks = requestStartTicks;
            double tickFrequency = 1_000_000_000.0 / Stopwatch.Frequency;

            while ((bytesRead = await httpStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                long nowTicks = Stopwatch.GetTimestamp();
                if (latencyTracker != null)
                {
                    double packetNs = (nowTicks - lastPacketTicks) * tickFrequency;
                    latencyTracker.UpdatePacketLatency(packetNs);
                    transferState.Latency = latencyTracker;
                }
                lastPacketTicks = nowTicks;

                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                position += bytesRead;
                bytesWritten += bytesRead; // absolute; never reset

                long now = stopwatch.ElapsedMilliseconds;
                if (now - lastReport >= interval)
                {
                    lastReport = now;
                    progress.Report(transferState.Update(position));
                    position = 0;
                }
            }

            transferState.Stop();
            progress.Report(transferState.Update(position));
            return DownloadResult.Ok(response.StatusCode);
        }
        catch (OperationCanceledException ex)
        {
            // Return a resume token — StatusCode is null (no HTTP response involved in cancellation)
            return DownloadResult.Cancelled(new ResumeToken(url, bytesWritten, etag, lastModified), ex);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            response?.Dispose();
            return DownloadResult.Failed(null, ex.Message, ex);
        }
    }

    /// <summary>
    /// Downloads a file from the specified URL with resumable cancellation support.
    /// Returns a <see cref="DownloadResult"/> instead of throwing on cancellation or HTTP errors.
    /// On cancellation, the result carries a <see cref="ResumeToken"/>; pass it back on the next call to resume.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="resumeToken">Optional token from a previous cancellation; when provided, a Range request is sent to continue from the saved offset.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
    public static async Task<DownloadResult> DownloadAsync(
        this HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        ResumeToken? resumeToken = null,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(destStream);
        ArgumentNullException.ThrowIfNull(progress);
        return await InternalDownloadAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, resumeToken, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file from the specified URL with resumable cancellation support.
    /// Returns a <see cref="DownloadResult"/> instead of throwing on cancellation or HTTP errors.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="resumeToken">Optional token from a previous cancellation; when provided, a Range request is sent to continue from the saved offset.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
    public static async Task<DownloadResult> DownloadAsync(
        this HttpClient client,
        string url,
        Stream destStream,
        IProgress<TransferState> progress,
        ResumeToken? resumeToken = null,
        int interval = 100,
        int bufferSize = 512,
        LatencyTracker? latencyTracker = null,
        CancellationToken cancellationToken = default)
        => await DownloadAsync(client, new Uri(url), destStream, progress, resumeToken, interval, bufferSize, latencyTracker, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Downloads a file from the specified URL with resumable cancellation support and custom request headers.
    /// Returns a <see cref="DownloadResult"/> instead of throwing on cancellation or HTTP errors.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="resumeToken">Optional token from a previous cancellation; when provided, a Range request is sent to continue from the saved offset.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="headers">Optional per-request headers; applied without mutating <see cref="HttpClient.DefaultRequestHeaders"/>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
    public static async Task<DownloadResult> DownloadAsync(
        this HttpClient client,
        Uri url,
        Stream destStream,
        IProgress<TransferState> progress,
        ResumeToken? resumeToken,
        int interval,
        int bufferSize,
        LatencyTracker? latencyTracker,
        IDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(destStream);
        ArgumentNullException.ThrowIfNull(progress);
        return await InternalDownloadAsync(client, url, destStream, progress, interval, bufferSize, latencyTracker, resumeToken, cancellationToken, headers).ConfigureAwait(false);
    }

    /// <summary>
    /// Downloads a file from the specified URL with resumable cancellation support and custom request headers.
    /// Returns a <see cref="DownloadResult"/> instead of throwing on cancellation or HTTP errors.
    /// </summary>
    /// <param name="client">The HttpClient instance to use.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="destStream">The destination stream to write the downloaded data to.</param>
    /// <param name="progress">Progress reporter for transfer state.</param>
    /// <param name="resumeToken">Optional token from a previous cancellation; when provided, a Range request is sent to continue from the saved offset.</param>
    /// <param name="interval">Progress report interval in milliseconds.</param>
    /// <param name="bufferSize">Buffer size for reading the stream.</param>
    /// <param name="latencyTracker">Optional latency tracker for TimeToFirstByte measurement.</param>
    /// <param name="headers">Optional per-request headers; applied without mutating <see cref="HttpClient.DefaultRequestHeaders"/>.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A <see cref="DownloadResult"/> indicating success, failure, or cancellation with a resume token.</returns>
    public static async Task<DownloadResult> DownloadAsync(
        this HttpClient client,
        string url,
        Stream destStream,
        IProgress<TransferState> progress,
        ResumeToken? resumeToken,
        int interval,
        int bufferSize,
        LatencyTracker? latencyTracker,
        IDictionary<string, string>? headers,
        CancellationToken cancellationToken = default)
        => await DownloadAsync(client, new Uri(url), destStream, progress, resumeToken, interval, bufferSize, latencyTracker, headers, cancellationToken).ConfigureAwait(false);

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