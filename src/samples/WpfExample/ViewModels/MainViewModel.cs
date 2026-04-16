using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Blazing.Extensions.DependencyInjection;
using Blazing.Extensions.Http.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WpfExample.Services;

namespace WpfExample.ViewModels;

/// <summary>
/// Main ViewModel for the download manager application.
/// </summary>
[AutoRegister(ServiceLifetime.Transient)]
public partial class MainViewModel : ObservableObject
{
    private readonly DownloadService _downloadService;
    private CancellationTokenSource? _globalCancellationTokenSource;
    private List<CancellationTokenSource> _individualCancellationTokenSources = new();
    private readonly List<string> _destinationPaths = [];

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private ObservableCollection<DownloadItemViewModel> _downloads = new();

    // Statistics properties
    [ObservableProperty]
    private string _totalDownloadsText = "0";

    [ObservableProperty]
    private string _activeDownloadsText = "0";

    [ObservableProperty]
    private string _completedDownloadsText = "0";

    [ObservableProperty]
    private string _failedDownloadsText = "0";

    [ObservableProperty]
    private string _totalBytesText = "0 B";

    [ObservableProperty]
    private string _overallSpeedText = "0.00 B/s";

    [ObservableProperty]
    private string _averageLatencyText = "0.00 ms";

    [ObservableProperty]
    private string _totalElapsedText = "0.0s";

    private int _totalDownloads;
    private long _totalBytes;
    private double _totalSpeed;
    private double _totalLatency;
    private int _latencyCount;
    private DateTime _startTime;
    private int _activeDownloads;
    private int _completedDownloads;
    private int _failedDownloads;

    // Download URLs for testing
    private readonly string[] _downloadUrls = new[]
    {
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe",
        "https://download.visualstudio.microsoft.com/download/pr/89a2923a-18df-4dce-b069-51e687b04a53/9db4348b561703e622de7f03b1f11e93/dotnet-sdk-7.0.203-win-x64.exe"
    };

    public MainViewModel(DownloadService downloadService)
    {
        _downloadService = downloadService;
    }

    [RelayCommand]
    private async Task StartDownloadsAsync()
    {
        IsDownloading = true;

        // Clear previous downloads
        Downloads.Clear();
        
        // Dispose old cancellation token sources
        foreach (var cts in _individualCancellationTokenSources)
        {
            cts.Dispose();
        }
        _individualCancellationTokenSources.Clear();
        
        ResetStatistics();

        // Initialize statistics
        _totalDownloads = _downloadUrls.Length;
        _activeDownloads = _downloadUrls.Length;
        _startTime = DateTime.Now;
        UpdateStatisticsDisplay();

        // Initialize destination paths for this batch
        _destinationPaths.Clear();
        for (int i = 0; i < _downloadUrls.Length; i++)
            _destinationPaths.Add(Path.Combine(Path.GetTempPath(), $"download_{i}_{Guid.NewGuid()}.tmp"));

        // Create global cancellation token source
        _globalCancellationTokenSource = new CancellationTokenSource();

        // Create download view models with individual cancellation support
        for (int i = 0; i < _downloadUrls.Length; i++)
        {
            var individualCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellationTokenSource.Token);
            _individualCancellationTokenSources.Add(individualCts);
            
            var downloadVm = new DownloadItemViewModel
            {
                FileName = $"Download #{i + 1}: File{i + 1}.exe"
            };
            downloadVm.SetCancellationTokenSource(individualCts);
            
            Downloads.Add(downloadVm);
        }

        // Start all downloads
        var downloadTasks = new List<Task>();
        for (int i = 0; i < _downloadUrls.Length; i++)
        {
            int index = i;
            downloadTasks.Add(DownloadFileAsync(index, _downloadUrls[i], _individualCancellationTokenSources[i].Token));
        }

        try
        {
            await Task.WhenAll(downloadTasks);
            MessageBox.Show("All downloads completed!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            MessageBox.Show("Downloads were cancelled.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error during downloads: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void StopDownloads()
    {
        _globalCancellationTokenSource?.Cancel();
    }

    private async Task DownloadFileAsync(int index, string url, CancellationToken cancellationToken)
    {
        var downloadVm = Downloads[index];
        var destinationPath = _destinationPaths[index];
        downloadVm.DestinationPath = destinationPath;

        try
        {
            var progress = new Progress<TransferState>(state =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    downloadVm.UpdateProgress(state);
                    UpdateStatistics(state);
                });
            });

            var latencyTracker = new LatencyTracker();

            DownloadResult result = await _downloadService.DownloadFileAsync(
                new Uri(url), destinationPath, progress, latencyTracker, resumeToken: null, cancellationToken);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (result.IsSuccess)
                {
                    downloadVm.MarkComplete();
                    MarkDownloadComplete();
                    // Clean up downloaded temp file on success
                    if (File.Exists(destinationPath))
                        File.Delete(destinationPath);
                }
                else if (result.ResumeToken != null)
                {
                    downloadVm.MarkCancelled(result.ResumeToken);
                    MarkDownloadFailed();
                }
                else
                {
                    downloadVm.MarkError();
                    MarkDownloadFailed();
                    if (File.Exists(destinationPath))
                        File.Delete(destinationPath);
                }
            });
        }
        catch (Exception)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                downloadVm.MarkError();
                MarkDownloadFailed();
            });
        }
    }

    /// <summary>Resumes a cancelled download for the specified <paramref name="vm"/>.</summary>
    [RelayCommand]
    private async Task ResumeDownloadAsync(DownloadItemViewModel vm)
    {
        if (vm.ResumeToken is null || vm.DestinationPath is null)
            return;

        int index = Downloads.IndexOf(vm);
        if (index < 0)
            return;

        // If the global CTS is null or already cancelled (e.g. after "Stop All Downloads"),
        // replace it with a fresh one so the resumed download is not immediately cancelled.
        if (_globalCancellationTokenSource is null || _globalCancellationTokenSource.IsCancellationRequested)
        {
            _globalCancellationTokenSource?.Dispose();
            _globalCancellationTokenSource = new CancellationTokenSource();
        }

        var individualCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCancellationTokenSource.Token);

        // Replace the old (cancelled/disposed) CTS
        _individualCancellationTokenSources[index].Dispose();
        _individualCancellationTokenSources[index] = individualCts;

        // Capture token and path BEFORE ResetForResume() clears vm.ResumeToken
        var resumeToken = vm.ResumeToken!;
        var destinationPath = vm.DestinationPath!;

        vm.ResetForResume();
        vm.SetCancellationTokenSource(individualCts);

        await ResumeFileAsync(vm, resumeToken, destinationPath, individualCts.Token).ConfigureAwait(false);
    }

    private async Task ResumeFileAsync(DownloadItemViewModel vm, ResumeToken resumeToken, string destinationPath, CancellationToken ct)
    {
        Application.Current.Dispatcher.Invoke(MarkDownloadResuming);
        try
        {
            var latencyTracker = new LatencyTracker();
            var progress = new Progress<TransferState>(state =>
                Application.Current.Dispatcher.Invoke(() =>
                {
                    vm.UpdateProgress(state);
                    UpdateStatistics(state);
                }));

            DownloadResult result = await _downloadService.DownloadFileAsync(
                resumeToken.Url, destinationPath, progress, latencyTracker, resumeToken, ct);

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (result.IsSuccess)
                {
                    vm.MarkComplete();
                    MarkDownloadComplete();
                    if (File.Exists(destinationPath))
                        File.Delete(destinationPath);
                }
                else if (result.ResumeToken != null)
                {
                    vm.MarkCancelled(result.ResumeToken); // chainable cancel → resume
                    MarkDownloadFailed();
                }
                else
                {
                    vm.MarkError();
                    MarkDownloadFailed();
                }
            });
        }
        catch (Exception)
        {
            Application.Current.Dispatcher.Invoke(() => vm.MarkError());
        }
    }

    private void ResetStatistics()
    {
        _totalDownloads = 0;
        _totalBytes = 0;
        _totalSpeed = 0;
        _totalLatency = 0;
        _latencyCount = 0;
        _startTime = DateTime.Now;
        _activeDownloads = 0;
        _completedDownloads = 0;
        _failedDownloads = 0;
        UpdateStatisticsDisplay();
    }

    private void UpdateStatistics(TransferState state)
    {
        _totalBytes = Math.Max(_totalBytes, state.Total.Transferred);
        _totalSpeed = state.Chunk.RawSpeed;

        if (state.Latency != null && state.Latency.PacketCount > 0)
        {
            _totalLatency += state.Latency.PacketAvgMs;
            _latencyCount++;
        }

        UpdateStatisticsDisplay();
    }

    private void MarkDownloadComplete()
    {
        _activeDownloads--;
        _completedDownloads++;
        UpdateStatisticsDisplay();
    }

    private void MarkDownloadFailed()
    {
        _activeDownloads--;
        _failedDownloads++;
        UpdateStatisticsDisplay();
    }

    /// <summary>Reverses the failed/active counters when a cancelled download is resumed.</summary>
    private void MarkDownloadResuming()
    {
        _activeDownloads++;
        _failedDownloads--;
        UpdateStatisticsDisplay();
    }

    private void UpdateStatisticsDisplay()
    {
        TotalDownloadsText = _totalDownloads.ToString();
        ActiveDownloadsText = _activeDownloads.ToString();
        CompletedDownloadsText = _completedDownloads.ToString();
        FailedDownloadsText = _failedDownloads.ToString();

        // Format total bytes
        double bytes = _totalBytes;
        string[] units = { "B", "KiB", "MiB", "GiB", "TiB" };
        int unitIndex = 0;
        while (bytes >= 1024 && unitIndex < units.Length - 1)
        {
            bytes /= 1024;
            unitIndex++;
        }
        TotalBytesText = $"{bytes:N2} {units[unitIndex]}";

        // Format speed
        double speed = _totalSpeed;
        unitIndex = 0;
        while (speed >= 1024 && unitIndex < units.Length - 1)
        {
            speed /= 1024;
            unitIndex++;
        }
        OverallSpeedText = $"{speed:N2} {units[unitIndex]}/s";

        // Average latency
        if (_latencyCount > 0)
        {
            AverageLatencyText = $"{(_totalLatency / _latencyCount):N2} ms";
        }

        // Total elapsed
        var elapsed = DateTime.Now - _startTime;
        TotalElapsedText = $"{elapsed.TotalSeconds:N1}s";
    }
}
