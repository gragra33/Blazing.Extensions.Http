namespace Blazing.Extensions.Http.Tests.Models;

public class TransferStateTests
{
    [Fact]
    public void Constructor_WithDefaultValues_ShouldInitializeCorrectly()
    {
        // Act
        var state = new TransferState();

        // Assert
        state.TotalBytes.Should().Be(0);
        state.Total.Should().NotBeNull();
        state.Chunk.Should().NotBeNull();
        state.Average.Should().NotBeNull();
        state.Maximum.Should().NotBeNull();
    }

    [Fact]
    public void Start_ShouldInitializeStartTimeAndTotalBytes()
    {
        // Arrange
        var state = new TransferState();
        var totalBytes = 1000L;
        var beforeStart = DateTimeOffset.Now;

        // Act
        state.Start(totalBytes);
        var afterStart = DateTimeOffset.Now;

        // Assert
        state.TotalBytes.Should().Be(totalBytes);
        state.StartTime.Should().BeOnOrAfter(beforeStart);
        state.StartTime.Should().BeOnOrBefore(afterStart);
    }

    [Fact]
    public void Start_WithNullTotalBytes_ShouldSetToZero()
    {
        // Arrange
        var state = new TransferState();

        // Act
        state.Start(null);

        // Assert
        state.TotalBytes.Should().Be(0);
    }

    [Fact]
    public void Stop_ShouldSetStopTime()
    {
        // Arrange
        var state = new TransferState();
        state.Start(1000);
        var beforeStop = DateTimeOffset.Now;

        // Act
        state.Stop();
        var afterStop = DateTimeOffset.Now;

        // Assert
        state.StopTime.Should().BeOnOrAfter(beforeStop);
        state.StopTime.Should().BeOnOrBefore(afterStop);
    }

    [Fact]
    public void CalcProgressPercentage_WithValidData_ShouldCalculateCorrectly()
    {
        // Arrange
        var state = new TransferState();
        state.Start(1000);
        state.Total.Transferred = 500;

        // Act
        var percentage = state.CalcProgressPercentage();

        // Assert
        percentage.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void CalcProgressPercentage_WithZeroTotal_ShouldReturnNegativeOne()
    {
        // Arrange
        var state = new TransferState();
        state.Start(0);

        // Act
        var percentage = state.CalcProgressPercentage();

        // Assert
        percentage.Should().Be(-1);
    }

    [Fact]
    public void CalcRemainingSize_ShouldCalculateCorrectly()
    {
        // Arrange
        var state = new TransferState();
        state.Start(2048); // 2 KiB
        state.Total.Transferred = 1024; // 1 KiB transferred

        // Act
        var (bytes, unit) = state.CalcRemainingSize();

        // Assert
        // Remaining = 2048 - 1024 = 1024 bytes
        // CalcRemainingSize divides by 1024 while size > 1024, so 1024 stays as bytes
        bytes.Should().BeApproximately(1024, 1);
        unit.Should().Be(ByteUnit.B);
    }

    [Fact]
    public void CalcRemainingSize_WithZeroTotal_ShouldReturnZeroBytes()
    {
        // Arrange
        var state = new TransferState();
        state.Start(0);

        // Act
        var (bytes, unit) = state.CalcRemainingSize();

        // Assert
        bytes.Should().Be(0);
        unit.Should().Be(ByteUnit.B);
    }

    [Fact]
    public void Update_ShouldUpdateTransferStatistics()
    {
        // Arrange
        var state = new TransferState();
        state.Start(10000);
        System.Threading.Thread.Sleep(10); // Small delay for time calculations

        // Act
        state.Update(1024);

        // Assert
        state.Total.Transferred.Should().Be(1024);
        state.Chunk.Transferred.Should().Be(1024);
        state.Average.Count.Should().Be(1);
        state.Total.Elapsed.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Update_WithMultipleChunks_ShouldTrackMaximum()
    {
        // Arrange
        var state = new TransferState();
        state.Start(10000);

        // Act
        System.Threading.Thread.Sleep(10);
        state.Update(100);
        System.Threading.Thread.Sleep(10);
        state.Update(500);
        System.Threading.Thread.Sleep(10);
        state.Update(200);

        // Assert
        state.Total.Transferred.Should().Be(800);
        state.Maximum.Transferred.Should().BeGreaterThan(0);
        state.Average.Count.Should().Be(3);
    }

    [Fact]
    public void Latency_Property_ShouldBeSettable()
    {
        // Arrange
        var state = new TransferState();
        var latency = new LatencyTracker();

        // Act
        state.Latency = latency;

        // Assert
        state.Latency.Should().BeSameAs(latency);
    }

    [Fact]
    public void TTFB_Property_ShouldBeSettable()
    {
        // Arrange
        var state = new TransferState();
        var ttfb = 50_000_000.0; // 50ms in nanoseconds

        // Act
        state.TTFB = ttfb;

        // Assert
        state.TTFB.Should().Be(ttfb);
    }
}
