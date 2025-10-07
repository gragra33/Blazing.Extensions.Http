namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents a transfer rate calculation for a single transfer, including transferred bytes and elapsed time.
/// </summary>
public sealed class Transfer : TransferRateBase
{
    /// <summary>
    /// Gets or sets the total number of bytes transferred.
    /// </summary>
    public long Transferred { get; set; }

    /// <summary>
    /// Gets or sets the elapsed time for the transfer.
    /// </summary>
    public TimeSpan Elapsed { get; set; }

    /// <summary>
    /// Calculates the byte and bit rates based on the current transfer.
    /// </summary>
    public void CalcRates()
    {
        ByteUnit byteUnit = Models.ByteUnit.B; 
        BitUnit bitUnit = Models.BitUnit.b; 

        RawSpeed = CalcRawSpeed();
        double speed = RawSpeed;

        // Determine the appropriate byte unit
        while (speed > 1024)
        {
            speed /= 1024;
            byteUnit++;
        }
        ByteUnit = (speed, byteUnit);

        // Determine the appropriate bit unit
        speed = RawSpeed * 8;
        while (speed > 1024)
        {
            speed /= 1024;
            bitUnit++;
        }
        BitUnit = (speed, bitUnit);
    }

    /// <summary>
    /// Calculates the raw transfer speed in bytes per second.
    /// </summary>
    internal double CalcRawSpeed()
        => Elapsed.TotalSeconds < 0.001D ? Transferred : Transferred / Elapsed.TotalSeconds;
}