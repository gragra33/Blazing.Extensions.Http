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
    private bool _canCancel = true;

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
        CanCancel = false;
    }

    public void MarkError()
    {
        IsComplete = false;
        IsError = true;
        CanCancel = false;
    }
}
