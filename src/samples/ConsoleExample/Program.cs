// Program.cs - Console Example for Blazing.Extensions.Http
// Demonstrates file download and upload with progress and latency reporting.
// Uses IHttpClientFactory and Blazing.Extensions.Http for advanced HTTP operations.

namespace ConsoleExample;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Console application demonstrating file transfer operations with progress reporting.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point for the console application.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main()
    {
        // Initialize the IHttpClientFactory for creating HttpClient instances
        IHttpClientFactory httpClientFactory = InitializeHttpClientFactory();

        // Example download URL and file paths for saving downloaded files
        string url = "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe";
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

        try
        {
            if (choice == '1')
            {
                // Download multiple files in parallel
                await FileTransferHelper.RunDownloadAsync(httpClientFactory, new Uri(url), [saveFile1, saveFile2, saveFile3, saveFile4], reportInterval).ConfigureAwait(false);
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
}
