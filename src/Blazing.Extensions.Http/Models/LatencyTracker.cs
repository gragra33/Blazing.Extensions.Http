namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Tracks latency statistics for TimeToFirstByte and per-packet measurements in nanoseconds and milliseconds.
/// </summary>
public sealed class LatencyTracker
{
    private double _currentPacket;
    private double? _packetMin;
    private double? _packetMax;
    private double _packetTotal;
    private int _packetCount;
    private double? timeToFirstByte; // Store TimeToFirstByte (TTFB) internally
    private bool _firstPacket = true;

    /// <summary>
    /// Gets the most recent latency value in nanoseconds.
    /// </summary>
    public double CurrentPacketPacketMs => _currentPacket / 1_000_000.0;

    /// <summary>
    /// Gets the average per-packet latency in nanoseconds.
    /// </summary>
    public double PacketAvg => _packetCount > 0 ? _packetTotal / _packetCount : 0;
    
    /// <summary>
    /// Gets the number of packets measured.
    /// </summary>
    public int PacketCount => _packetCount;

    /// <summary>
    /// Gets the time to first byte (TimeToFirstByte) in nanoseconds.
    /// </summary>
    public double? TimeToFirstByte => timeToFirstByte / 1_000_000.0;

    /// <summary>
    /// Gets the minimum per-packet latency in milliseconds. Returns -1 if no valid packets measured.
    /// </summary>
    public double PacketMinMs => _packetMin.HasValue ? _packetMin.Value / 1_000_000.0 : -1;
    
    /// <summary>
    /// Gets the maximum per-packet latency in milliseconds. Returns -1 if no valid packets measured.
    /// </summary>
    public double PacketMaxMs => _packetMax.HasValue ? _packetMax.Value / 1_000_000.0 : -1;
    
    /// <summary>
    /// Gets the average per-packet latency in milliseconds.
    /// </summary>
    public double PacketAvgMs => PacketAvg / 1_000_000.0;

    /// <summary>
    /// Call this for every packet (buffer read/write) to track per-packet latency.
    /// The first call will also set TimeToFirstByte if not already set.
    /// </summary>
    public void UpdatePacketLatency(double nanoseconds)
    {
        // Ignore values less than 0.00001 ms (10 nanoseconds)
        if (nanoseconds < 10) // 0.00001 ms = 10 ns
            return;

        _currentPacket = nanoseconds;

        if (_firstPacket)
        {
            // First packet is TTFB - don't include in streaming latency stats
            timeToFirstByte = nanoseconds;
            _firstPacket = false;
        }
        else
        {
            // For streaming packets only, update min/max
            // Update _packetMin if: no value, value is effectively 0 (< 2ns), or new value is smaller
            if (!_packetMin.HasValue || _packetMin.Value < 2 || nanoseconds < _packetMin.Value)
                _packetMin = nanoseconds;
            if (!_packetMax.HasValue || nanoseconds > _packetMax.Value)
                _packetMax = nanoseconds;
            
            _packetTotal += nanoseconds;
            _packetCount++;
        }
    }
}
