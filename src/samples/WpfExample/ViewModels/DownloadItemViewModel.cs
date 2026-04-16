using Blazing.Extensions.Http.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WpfExample.ViewModels;

/// <summary>
/// ViewModel for individual download progress tracking.
/// </summary>
public partial class DownloadItemViewModel : ObservableObject
{
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private string _progressText = "0%";

    [ObservableProperty]
    private string _transferredText = "Transferred: 0 / 0 bytes";

    [ObservableProperty]
    private string _currentSpeedText = "Current Speed: 0.00 B/s (0.00 bit)";

    [ObservableProperty]
    private string _averageSpeedText = "Average Speed: 0.00 B/s";

    [ObservableProperty]
    private string _maximumSpeedText = "Maximum Speed: 0.00 B/s";

    [ObservableProperty]
    private string _elapsedTimeText = "Elapsed: 0.0s";

    [ObservableProperty]
    private string _remainingTimeText = "Remaining: 0.0s";

    [ObservableProperty]
    private string _latencyText = "Latency: 0.00 ms (0.00 - 0.00 ms)";

    [ObservableProperty]
    private string _ttfbText = "TTFB: 0 ms";

    [ObservableProperty]
    private bool _isComplete;

    [ObservableProperty]
    private bool _isError;

    [ObservableProperty]
    private bool _isCancelled;

    [ObservableProperty]
    private bool _canResume;

    [ObservableProperty]
    private bool _canCancel = true;

    /// <summary>Gets the resume token captured on cancellation, or <c>null</c> if not cancelled.</summary>
    public ResumeToken? ResumeToken { get; internal set; }

    /// <summary>Gets or sets the local file path where this download is being saved.</summary>
    public string? DestinationPath { get; set; }

    public void SetCancellationTokenSource(CancellationTokenSource? cts)
    {
        _cancellationTokenSource = cts;
        CanCancel = cts != null;
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelDownload()
    {
        _cancellationTokenSource?.Cancel();
        CanCancel = false;
    }

    public void UpdateProgress(TransferState state)
    {
        try
        {
            double percent = state.CalcProgressPercentage();
            if (percent >= 0)
            {
                ProgressPercentage = Math.Min(percent * 100, 100);
                ProgressText = $"{percent:P1}";
            }
            else
            {
                ProgressText = "Unknown";
            }

            TransferredText = $"Transferred: {state.Total.Transferred:N0} / {state.TotalBytes:N0} bytes";

            var (chunkSpeed, chunkUnit) = state.Chunk.ByteUnit;
            var (chunkBitSpeed, chunkBitUnit) = state.Chunk.BitUnit;
            CurrentSpeedText = $"Current Speed: {chunkSpeed:N2} {chunkUnit}/s ({chunkBitSpeed:N2} {chunkBitUnit})";

            var (avgSpeed, avgUnit) = state.Average.ByteUnit;
            AverageSpeedText = $"Average Speed: {avgSpeed:N2} {avgUnit}/s";

            var (maxSpeed, maxUnit) = state.Maximum.ByteUnit;
            MaximumSpeedText = $"Maximum Speed: {maxSpeed:N2} {maxUnit}/s";

            ElapsedTimeText = $"Elapsed: {state.Total.Elapsed.TotalSeconds:N1}s";

            var remaining = state.CalcEstimatedRemainingTime();
            if (remaining != TimeSpan.MinValue)
            {
                RemainingTimeText = $"Remaining: {remaining.TotalSeconds:N1}s";
            }
            else
            {
                RemainingTimeText = "Remaining: Unknown";
            }

            if (state.Latency != null && state.Latency.PacketCount > 0 && state.Latency.PacketMinMs >= 0)
            {
                LatencyText = $"Latency: {state.Latency.PacketAvgMs:N2} ms ({state.Latency.PacketMinMs:N2} - {state.Latency.PacketMaxMs:N2} ms)";

                if (state.Latency.TimeToFirstByte.HasValue && state.Latency.TimeToFirstByte.Value > 0)
                {
                    TtfbText = $"TTFB: {state.Latency.TimeToFirstByte.Value:N0} ms";
                }
                else
                {
                    TtfbText = "TTFB: Unknown";
                }
            }
            else
            {
                LatencyText = "Latency: Unknown";
                TtfbText = "TTFB: Unknown";
            }
        }
        catch
        {
            // Ignore errors during updates
        }
    }

    public void MarkComplete()
    {
        IsComplete = true;
        IsError = false;
        IsCancelled = false;
        CanResume = false;
        CanCancel = false;
    }

    public void MarkError()
    {
        IsComplete = false;
        IsError = true;
        IsCancelled = false;
        CanResume = false;
        CanCancel = false;
    }

    /// <summary>
    /// Marks this download as cancelled and stores the <paramref name="resumeToken"/> for a subsequent resume.
    /// Sets <see cref="CanResume"/> when a token is available.
    /// </summary>
    /// <param name="resumeToken">The resume token from the cancelled <see cref="DownloadResult"/>.</param>
    public void MarkCancelled(ResumeToken? resumeToken)
    {
        IsCancelled = true;
        IsError = false;
        IsComplete = false;
        CanCancel = false;
        ResumeToken = resumeToken;
        CanResume = resumeToken != null;
    }

    /// <summary>Resets this ViewModel to its initial state in preparation for a resumed download.</summary>
    public void ResetForResume()
    {
        IsCancelled = false;
        CanResume = false;
        ResumeToken = null;
        ProgressPercentage = 0;
        ProgressText = "0%";
    }
}
