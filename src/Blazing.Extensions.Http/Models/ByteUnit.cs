namespace Blazing.Extensions.Http.Models;

/// <summary>
/// Represents the available byte units for transfer rate calculations.
/// </summary>
public enum ByteUnit
{
    /// <summary>
    /// Bytes per second.
    /// </summary>
    B,
    
    /// <summary>
    /// Kibibytes per second (1024 bytes).
    /// </summary>
    KiB,
    
    /// <summary>
    /// Mebibytes per second (1024^2 bytes).
    /// </summary>
    MiB,
    
    /// <summary>
    /// Gibibytes per second (1024^3 bytes).
    /// </summary>
    GiB,
    
    /// <summary>
    /// Tebibytes per second (1024^4 bytes).
    /// </summary>
    TiB
}