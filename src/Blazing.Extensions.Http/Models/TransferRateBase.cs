namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents the base class for transfer rate calculations, including byte and bit rates and raw speed.
/// </summary>
public abstract class TransferRateBase
{
    /// <summary>
    /// Gets or sets the transfer rate in bytes per second, with the appropriate byte unit.
    /// </summary>
    public (double Speed, ByteUnit Size) ByteUnit { get; internal set; }

    /// <summary>
    /// Gets or sets the transfer rate in bits per second, with the appropriate bit unit.
    /// </summary>
    public (double Speed, BitUnit Size) BitUnit { get; internal set; }

    /// <summary>
    /// Gets or sets the raw transfer speed in bytes per second.
    /// </summary>
    public double RawSpeed { get; internal set; } // bytes/second
}