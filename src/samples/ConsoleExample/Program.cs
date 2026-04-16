// Program.cs - Console Example for Blazing.Extensions.Http
// Demonstrates file download and upload with progress and latency reporting.
// Uses IHttpClientFactory and Blazing.Extensions.Http for advanced HTTP operations.

namespace ConsoleExample;

using Blazing.Extensions.Http.Models;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Console application demonstrating file transfer operations with progress reporting.
/// </summary>
internal static class Program
{
#pragma warning disable S1075 // Hardcoded URI in sample application
    private const string SampleDownloadUrl = "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe";
#pragma warning restore S1075

    /// <summary>
    /// Main entry point for the console application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main()
    {
        // Initialize the IHttpClientFactory for creating HttpClient instances
        IHttpClientFactory httpClientFactory = InitializeHttpClientFactory();

        // Example download URL and file paths for saving downloaded files
        string url = SampleDownloadUrl;
        string saveFile1 = "ac95c389-31ae-416f-a8cd-fdfb5969d528.cbz";
        string saveFile2 = "ac95c389-31ae-416f-a8cd-fdfb5969d529.cbz";
        string saveFile3 = "ac95c389-31ae-416f-a8cd-fdfb5969d527.cbz";
        string saveFile4 = "ac95c389-31ae-416f-a8cd-fdfb5969d526.cbz";
        // Placeholder for upload URL and file to upload
        string uploadUrl = "[upload url goes here]";
        string uploadFile = @".\files\test.dat";

        // Prepare console UI
        Console.Clear();
        Console.CursorVisible = false;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
        Console.WriteLine("Select an operation:");
        Console.WriteLine("1. Download files");
        Console.WriteLine("2. Upload file");
        Console.Write("Enter choice (1 or 2): ");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
        char choice = Console.ReadKey().KeyChar;
        Console.WriteLine();

        bool success = true;
        int reportInterval = 250;
        string[] saveFiles = [saveFile1, saveFile2, saveFile3, saveFile4];

        try
        {
            if (choice == '1')
            {
                success = await RunDownloadSessionAsync(httpClientFactory, new Uri(url), saveFiles, reportInterval).ConfigureAwait(false);
            }
            else if (choice == '2')
            {
                // Upload a file
                await FileTransferHelper.RunUploadAsync(httpClientFactory, new Uri(uploadUrl), uploadFile, reportInterval).ConfigureAwait(false);
            }
            else
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Console.WriteLine("Invalid choice.");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                success = false;
            }
        }
        catch (HttpRequestException ex)
        {
            // Handle HTTP-specific errors
            Console.WriteLine($"HTTP Error: {ex.Message}");
            success = false;
        }
        catch (TaskCanceledException ex)
        {
            // Handle timeout errors
            Console.WriteLine($"Request timed out: {ex.Message}");
            success = false;
        }
        catch (IOException ex)
        {
            // Handle file I/O errors
            Console.WriteLine($"File I/O Error: {ex.Message}");
            success = false;
        }
        catch (UnauthorizedAccessException ex)
        {
            // Handle access denied errors
            Console.WriteLine($"Access denied: {ex.Message}");
            success = false;
        }

        // Display final status at a fixed position
        Console.SetCursorPosition(0, 7);
#pragma warning disable CA1303 // Do not pass literals as localized parameters
        Console.Write($"File Download/Upload {(success ? "completed" : "failed")}!");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
        _ = Console.ReadKey();
        Console.WriteLine();
    }

    /// <summary>
    /// Runs the download session: downloads all files, shows a per-file summary, and loops to resume any cancelled downloads.
    /// </summary>
    /// <param name="httpClientFactory">Factory used to create HTTP clients.</param>
    /// <param name="url">The URL to download from.</param>
    /// <param name="saveFiles">Destination file paths, one per parallel download.</param>
    /// <param name="reportInterval">Progress report interval in milliseconds.</param>
    /// <returns><see langword="true"/> if all files completed successfully.</returns>
    private static async Task<bool> RunDownloadSessionAsync(
        IHttpClientFactory httpClientFactory, Uri url, string[] saveFiles, int reportInterval)
    {
        DownloadResult[] results = await RunWithConsoleCancellationAsync(
            ct => FileTransferHelper.RunDownloadAsync(httpClientFactory, url, saveFiles, reportInterval, ct)).ConfigureAwait(false);

        PrintDownloadSummary(results, saveFiles, complete: false);

        while (results.Any(r => !r.IsSuccess && r.ResumeToken != null))
        {
#pragma warning disable CA1303
            Console.Write("\nSome downloads were cancelled. Resume? (y/n): ");
#pragma warning restore CA1303
            char answer = Console.ReadKey().KeyChar;
            Console.WriteLine();
            if (answer != 'y' && answer != 'Y') break;

            results = await RunWithConsoleCancellationAsync(
                ct => FileTransferHelper.ResumeDownloadsAsync(httpClientFactory, results, saveFiles, reportInterval, ct)).ConfigureAwait(false);

            PrintDownloadSummary(results, saveFiles, complete: true);
        }

        return results.All(r => r.IsSuccess);
    }

    /// <summary>Prints per-file download status to the console.</summary>
    /// <param name="results">The download results to summarise.</param>
    /// <param name="saveFiles">Destination file paths used to compute the cursor row.</param>
    /// <param name="complete">
    /// When <see langword="true"/> appends trailing spaces to overwrite a shorter previous status.
    /// </param>
    private static void PrintDownloadSummary(DownloadResult[] results, string[] saveFiles, bool complete)
    {
        Console.SetCursorPosition(0, 8 + saveFiles.Length + 1);
        for (int i = 0; i < results.Length; i++)
        {
            DownloadResult r = results[i];
            string trail = complete ? "     " : string.Empty;
            if (r.IsSuccess)
                Console.WriteLine($"  File {i + 1}: Complete{trail}");
            else if (r.ResumeToken != null)
                Console.WriteLine($"  File {i + 1}: Cancelled (resumable at {r.ResumeToken.BytesWritten:N0} bytes)");
            else
                Console.WriteLine($"  File {i + 1}: Failed - {r.ErrorMessage}");
        }
    }

    /// <summary>
    /// Sets up the dependency injection container and registers HttpClientFactory.
    /// </summary>
    /// <returns>An instance of IHttpClientFactory for creating HTTP clients.</returns>
    private static IHttpClientFactory InitializeHttpClientFactory()
    {
        ServiceCollection builder = new();
        builder.AddHttpClient();
        ServiceProvider serviceProvider = builder.BuildServiceProvider();
        return serviceProvider.GetRequiredService<IHttpClientFactory>();
    }

    /// <summary>
    /// Runs a console operation with Ctrl+C mapped to a scoped <see cref="CancellationToken"/>.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <returns>The operation result.</returns>
    private static async Task<T> RunWithConsoleCancellationAsync<T>(Func<CancellationToken, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using CancellationTokenSource cts = new();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;
        try
        {
            return await operation(cts.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }
}
