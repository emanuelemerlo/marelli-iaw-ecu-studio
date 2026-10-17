namespace MarelliIawEcuStudio.Core.Transport;

/// <summary>
/// Optional control of the line signals, implemented by transports that can
/// drive them: a serial port, a network bridge, the simulator.
/// </summary>
/// <remarks>
/// A break holds the K-Line low. The 5-baud address of ISO 9141-2 and
/// KWP2000 and the 25 ms wake-up pattern of the KWP2000 fast init are sent
/// as timed breaks, because USB serial chips cannot run at 5 baud.
/// </remarks>
public interface ILineControl
{
    /// <summary>Holds the line low (true) or releases it (false).</summary>
    void SetBreak(bool on);

    /// <summary>The DTR signal; some cables power their K-Line chip from it.</summary>
    bool DtrEnable { get; set; }
}
