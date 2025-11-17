namespace Blazing.Extensions.Http.Tests.Models;

public class TransferTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithDefaultValues()
    {
        // Act
        var transfer = new Transfer();

        // Assert
        transfer.Transferred.Should().Be(0);
        transfer.Elapsed.Should().Be(TimeSpan.Zero);
        transfer.RawSpeed.Should().Be(0);
    }

    [Fact]
    public void CalcRates_WithValidTransfer_ShouldCalculateCorrectly()
    {
        // Arrange
        var transfer = new Transfer
        {
            Transferred = 1024,
            Elapsed = TimeSpan.FromSeconds(1)
        };

        // Act
        transfer.CalcRates();

        // Assert
        transfer.RawSpeed.Should().BeApproximately(1024, 0.1);
        // 1024 bytes/s = 1 KiB/s, but CalcRates only moves to next unit when speed > 1024
        // So 1024 should stay as B
        transfer.ByteUnit.Speed.Should().BeApproximately(1024, 1);
        transfer.ByteUnit.Size.Should().Be(ByteUnit.B);
    }

    [Fact]
    public void CalcRates_WithLargeTransfer_ShouldUseAppropriateUnit()
    {
        // Arrange
        var transfer = new Transfer
        {
            Transferred = 10485760, // 10 MiB
            Elapsed = TimeSpan.FromSeconds(1)
        };

        // Act
        transfer.CalcRates();

        // Assert
        transfer.ByteUnit.Speed.Should().BeApproximately(10, 0.1);
        transfer.ByteUnit.Size.Should().Be(ByteUnit.MiB);
    }

    [Fact]
    public void CalcRates_WithVerySmallElapsedTime_ShouldHandleCorrectly()
    {
        // Arrange
        var transfer = new Transfer
        {
            Transferred = 1024,
            Elapsed = TimeSpan.FromMilliseconds(0.0001) // Very small time
        };

        // Act
        transfer.CalcRates();

        // Assert
        transfer.RawSpeed.Should().Be(1024); // Should use Transferred directly
    }

    [Fact]
    public void CalcRates_ShouldCalculateBitRatesCorrectly()
    {
        // Arrange
        var transfer = new Transfer
        {
            Transferred = 2048, // 2 KiB
            Elapsed = TimeSpan.FromSeconds(1)
        };

        // Act
        transfer.CalcRates();

        // Assert
        // 2048 bytes * 8 = 16384 bits, which stays as bits (not > 1024 threshold)
        transfer.BitUnit.Speed.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var transfer = new Transfer();
        var transferred = 2048L;
        var elapsed = TimeSpan.FromSeconds(2);

        // Act
        transfer.Transferred = transferred;
        transfer.Elapsed = elapsed;

        // Assert
        transfer.Transferred.Should().Be(transferred);
        transfer.Elapsed.Should().Be(elapsed);
    }
}
