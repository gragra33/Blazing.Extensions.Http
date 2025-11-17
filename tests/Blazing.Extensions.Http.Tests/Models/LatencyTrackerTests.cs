namespace Blazing.Extensions.Http.Tests.Models;

public class LatencyTrackerTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var tracker = new LatencyTracker();

        // Assert
        tracker.PacketCount.Should().Be(0);
        tracker.PacketAvg.Should().Be(0);
        tracker.TimeToFirstByte.Should().BeNull();
    }

    [Fact]
    public void UpdatePacketLatency_WithFirstPacket_ShouldSetTimeToFirstByte()
    {
        // Arrange
        var tracker = new LatencyTracker();
        double firstPacketNs = 50_000_000; // 50ms in nanoseconds

        // Act
        tracker.UpdatePacketLatency(firstPacketNs);

        // Assert
        tracker.TimeToFirstByte.Should().NotBeNull();
        tracker.TimeToFirstByte!.Value.Should().BeApproximately(50, 0.1); // 50ms
        tracker.PacketCount.Should().Be(0); // First packet is TTFB, not counted in streaming stats
    }

    [Fact]
    public void UpdatePacketLatency_WithSubsequentPackets_ShouldUpdateStatistics()
    {
        // Arrange
        var tracker = new LatencyTracker();
        
        // Act - First packet for TTFB
        tracker.UpdatePacketLatency(50_000_000); // 50ms
        
        // Subsequent packets for streaming stats
        tracker.UpdatePacketLatency(10_000_000); // 10ms
        tracker.UpdatePacketLatency(20_000_000); // 20ms
        tracker.UpdatePacketLatency(30_000_000); // 30ms

        // Assert
        tracker.PacketCount.Should().Be(3);
        tracker.PacketAvgMs.Should().BeApproximately(20, 0.1); // Average of 10, 20, 30
        tracker.PacketMinMs.Should().BeApproximately(10, 0.1);
        tracker.PacketMaxMs.Should().BeApproximately(30, 0.1);
    }

    [Fact]
    public void UpdatePacketLatency_WithVerySmallValues_ShouldIgnore()
    {
        // Arrange
        var tracker = new LatencyTracker();
        
        // Act - Values below 10 nanoseconds should be ignored
        tracker.UpdatePacketLatency(5);
        tracker.UpdatePacketLatency(8);

        // Assert
        tracker.PacketCount.Should().Be(0);
        tracker.TimeToFirstByte.Should().BeNull();
    }

    [Fact]
    public void PacketMinMs_WhenNoPackets_ShouldReturnNegativeOne()
    {
        // Arrange
        var tracker = new LatencyTracker();

        // Act
        var min = tracker.PacketMinMs;

        // Assert
        min.Should().Be(-1);
    }

    [Fact]
    public void PacketMaxMs_WhenNoPackets_ShouldReturnNegativeOne()
    {
        // Arrange
        var tracker = new LatencyTracker();

        // Act
        var max = tracker.PacketMaxMs;

        // Assert
        max.Should().Be(-1);
    }

    [Fact]
    public void CurrentPacketMs_ShouldReturnLatestPacketInMilliseconds()
    {
        // Arrange
        var tracker = new LatencyTracker();
        tracker.UpdatePacketLatency(50_000_000); // TTFB
        tracker.UpdatePacketLatency(25_000_000); // 25ms

        // Act
        var current = tracker.CurrentPacketPacketMs;

        // Assert
        current.Should().BeApproximately(25, 0.1);
    }
}
