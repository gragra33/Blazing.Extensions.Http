namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents an average transfer rate calculator that accumulates and averages transfer speeds over time.
/// </summary>
public sealed class AverageTransfer : TransferRateBase
{
    /// <summary>
    /// Gets the number of transfer rate samples that have been averaged.
    /// </summary>
    public int Count { get; private set; }

    private double _runningTotalRawSpeed;

    /// <summary>
    /// Updates the average transfer rate with a new sample.
    /// </summary>
    /// <param name="rate">The transfer rate sample to include in the average.</param>
    public void Update(TransferRateBase rate)
    {
        ArgumentNullException.ThrowIfNull(rate);
        
        Count++;
        _runningTotalRawSpeed += rate.RawSpeed;
        RawSpeed = _runningTotalRawSpeed / Count;

        CalcRates();
    }

    /// <summary>
    /// Calculates the byte and bit rates based on the current average raw speed.
    /// </summary>
    private void CalcRates()
    {
        ByteUnit byteUnit = Models.ByteUnit.B; 
        BitUnit bitUnit = Models.BitUnit.b; 

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
}