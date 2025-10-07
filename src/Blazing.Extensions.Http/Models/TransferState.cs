namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Tracks the state and statistics of a file transfer, including timing, progress, and latency.
/// </summary>
public sealed class TransferState
{
    #region Properties

    /// <summary>
    /// Gets the time when the transfer started.
    /// </summary>
    public DateTimeOffset StartTime { get; private set; }

    /// <summary>
    /// Gets the time when the last progress check occurred.
    /// </summary>
    public DateTimeOffset LastCheckTime { get; private set; }

    /// <summary>
    /// Gets the time when the transfer stopped.
    /// </summary>
    public DateTimeOffset StopTime { get; private set; }

    /// <summary>
    /// Gets or sets the total number of bytes to transfer.
    /// </summary>
    public double TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets the total transfer statistics.
    /// </summary>
    public Transfer Total { get; set; } = new();

    /// <summary>
    /// Gets or sets the statistics for the current chunk.
    /// </summary>
    public Transfer Chunk { get; set; } = new();

    /// <summary>
    /// Gets or sets the average transfer statistics.
    /// </summary>
    public AverageTransfer Average { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum transfer statistics.
    /// </summary>
    public Transfer Maximum { get; set; } = new();

    /// <summary>
    /// Gets or sets the time to first byte (TimeToFirstByte) in nanoseconds.
    /// </summary>
    public double? TTFB { get; set; }

    /// <summary>
    /// Gets or sets the latency tracker for this transfer.
    /// </summary>
    public LatencyTracker? Latency { get; set; }

    #endregion

    /// <summary>
    /// Calculates the progress percentage of the transfer.
    /// </summary>
    public double CalcProgressPercentage()
        => TotalBytes < 1D ? -1D : Total.Transferred / TotalBytes;

    /// <summary>
    /// Marks the start of the transfer and sets the total bytes.
    /// </summary>
    public void Start(long? totalBytes)
    {
        StartTime = DateTimeOffset.Now;
        TotalBytes = totalBytes ?? 0D;
    }

    /// <summary>
    /// Marks the stop time of the transfer.
    /// </summary>
    public void Stop()
        => StopTime = DateTimeOffset.Now;

    /// <summary>
    /// Calculates the remaining size and its unit.
    /// </summary>
    public (double Bytes, ByteUnit Unit) CalcRemainingSize()
    {
        if (TotalBytes < 1D)
            return (0, ByteUnit.B);

        double size = TotalBytes - Total.Transferred;

        ByteUnit rate = 0; 

        while (size > 1024D)
        {
            size /= 1024D;
            rate ++;
        }

        return (size, rate);
    }

    /// <summary>
    /// Calculates the estimated remaining time for the transfer.
    /// </summary>
    public TimeSpan CalcEstimatedRemainingTime()
    {
        if (TotalBytes < 1D)
            return TimeSpan.MinValue;

        return TimeSpan.FromSeconds(
            (TotalBytes - Total.Transferred) /
            (Total.Transferred / Total.Elapsed.TotalSeconds));
    }

    /// <summary>
    /// Updates the transfer state with a new chunk size and recalculates statistics.
    /// </summary>
    public TransferState Update(int chunkSize)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        Chunk.Elapsed = now - LastCheckTime;
        Chunk.Transferred = chunkSize;
        Chunk.CalcRates();

        Average.Update(Chunk);

        Total.Elapsed = now - StartTime;
        Total.Transferred += chunkSize;
        Total.CalcRates();
        
        if (Chunk.RawSpeed > Maximum.RawSpeed)
        {
            Maximum.Elapsed = Chunk.Elapsed;
            Maximum.Transferred = Chunk.Transferred;
            Maximum.CalcRates();
        }

        LastCheckTime = now;

        return this;
    }
}