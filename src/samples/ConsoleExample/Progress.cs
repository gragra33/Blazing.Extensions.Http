using System.Globalization;
using System.Text;
using Blazing.Extensions.Http.Models;

namespace ConsoleExample;

/// <summary>
/// Provides progress reporting functionality for console applications.
/// </summary>
internal sealed class Progress
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Progress"/> class.
    /// </summary>
    /// <param name="left">The left cursor position.</param>
    /// <param name="top">The top cursor position.</param>
    public Progress(int left, int top)
    {
        Console.SetCursorPosition(left, top);
        cursorPosition = Console.GetCursorPosition();
    }

    private readonly (int Left, int Top) cursorPosition;

    /// <summary>
    /// Generates a compact progress report.
    /// </summary>
    /// <param name="state">The transfer state to report.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CompactReport(TransferState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        await Task.Yield();

        StringBuilder sb = new();

        double progress = state.CalcProgressPercentage();

        (double chunkByteSpeed, ByteUnit chunkByteSize) = state.Chunk.ByteUnit;
        (double chunkBitSpeed, BitUnit chunkBitUnit) = state.Chunk.BitUnit;
        
        bool isDownloading = Math.Abs(1 - progress) > 0.001;

        _ = sb.Append(CultureInfo.InvariantCulture, $"Receiving: {(progress < 0D ? "" : $"{progress:P0}")} ({state.Total.Transferred:N0}/{state.TotalBytes:N0}), {chunkBitSpeed:N2}{chunkBitUnit} | {chunkByteSpeed:N2}{chunkByteSize}/s {(isDownloading ? "" : "done.")}");
        
        // Only show latency if we have valid measurements (PacketMinMs >= 0)
        if (state.Latency is not null && state.Latency.PacketCount > 0 && state.Latency.PacketMinMs >= 0)
        {
            string latencyText = Math.Abs(state.Latency.PacketMinMs - state.Latency.PacketMaxMs) < 0.001
                ? $"| Lat: {state.Latency.PacketAvgMs:N3} ms | TTFB: {state.Latency.TimeToFirstByte} ms1"
                : $"| Lat: {state.Latency.PacketAvgMs:N3} ms ({state.Latency.PacketMinMs:N5} ms - {state.Latency.PacketMaxMs:N3} ms | TTFB: {state.Latency.TimeToFirstByte:N0} ms";
            _ = sb.Append(latencyText);
        }

        _ = sb.Append("     ");

        DisplayReport(sb);
    }

    /// <summary>
    /// Generates a detailed progress report.
    /// </summary>
    /// <param name="state">The transfer state to report.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task Report(TransferState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        await Task.Yield();

        StringBuilder sb = new();

        double progress = state.CalcProgressPercentage();
        (double Bytes, ByteUnit Unit) = state.CalcRemainingSize();
        TimeSpan remainingTime = state.CalcEstimatedRemainingTime();

        TimeSpan EstTime = remainingTime + state.Total.Elapsed;
        string estimate = EstTime.TotalSeconds >= 0 ? $"of {EstTime.TotalSeconds:N3} seconds" : "";

        (double Speed, ByteUnit Size) = state.Chunk.ByteUnit;
        (double chunkBitSpeed, BitUnit chunkBitSize) = state.Chunk.BitUnit;
        
        (double maxByteSpeed, ByteUnit maxByteSize) = state.Maximum.ByteUnit;
        (double maxBitSpeed, BitUnit maxBitSize) = state.Maximum.BitUnit;
        
        (double avgByteSpeed, ByteUnit avgByteSize) = state.Average.ByteUnit;
        (double avgBitSpeed, BitUnit avgBitSize) = state.Average.BitUnit;

        bool isDownloading = Math.Abs(1 - progress) > 0.001;

        _ = sb
            .AppendLine(CultureInfo.InvariantCulture, $"Progress:     {(progress < 0D ? "unknown" : $"{progress:P2}")} | {state.Total.Elapsed.TotalSeconds:N3} seconds {estimate}          ")
            .AppendLine(CultureInfo.InvariantCulture, $"Transferred:  {state.Total.Transferred:N0} of {state.TotalBytes:N0} bytes");
        
        if (isDownloading)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Current Rate: {chunkBitSpeed:N3} {chunkBitSize} ({Speed:N3} {Size}) / second  | {state.Chunk.Transferred:N0}B / {state.Chunk.Elapsed.TotalMilliseconds:N2}ms                                 ");
        }
        
        _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Maximum Rate: {maxBitSpeed:N3} {maxBitSize} ({maxByteSpeed:N3} {maxByteSize}) / second          ")
            .AppendLine(CultureInfo.InvariantCulture, $"Average Rate: {avgBitSpeed:N3} {avgBitSize} ({avgByteSpeed:N3} {avgByteSize}) / second          ");

        // Only show latency if we have valid measurements (PacketMinMs >= 0)
        if (state.Latency is not null && state.Latency.PacketCount > 0 && state.Latency.PacketMinMs >= 0)
        {
            _ = sb.AppendLine(CultureInfo.InvariantCulture, $"Latency:     {state.Latency.PacketAvgMs:N3} ms ({state.Latency.PacketMinMs:N3} ms - {state.Latency.PacketMaxMs:N3} ms)");
        }

        _ = isDownloading
            ? sb.AppendLine(CultureInfo.InvariantCulture, $"Remaining:    {(Bytes < 0D ? "unknown" : $"{Bytes:N3} {Unit}")} | {(remainingTime == TimeSpan.MinValue ? "unknown" : $"{remainingTime.TotalSeconds} seconds          ")}")
            : sb.AppendLine(new string(' ', 100)).AppendLine(new string(' ', 100));

        DisplayReport(sb);
    }

    private void DisplayReport(StringBuilder sb)
    {
        InternalLock.Wait();
        Console.SetCursorPosition(cursorPosition.Left, cursorPosition.Top);
        Console.WriteLine(sb);
        InternalLock.Release();
    }
}