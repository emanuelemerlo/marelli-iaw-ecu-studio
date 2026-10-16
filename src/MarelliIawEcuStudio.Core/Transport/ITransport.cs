namespace MarelliIawEcuStudio.Core.Transport;

/// <summary>
/// A byte channel to the ECU: a serial K-Line adapter, a network bridge or
/// the simulator. The semantics follow <c>System.IO.Ports.SerialPort</c> so
/// the protocol code reads the same as the original IES2.
/// </summary>
public interface ITransport : IDisposable
{
    /// <summary>Human readable name, for example <c>/dev/ttyUSB0</c> or <c>simulator:IAW-6F/16F</c>.</summary>
    string Name { get; }

    bool IsOpen { get; }

    void Open();

    void Close();

    /// <summary>Line speed. The IAW protocol uses 1200 baud for init and 7680 baud afterwards.</summary>
    int BaudRate { get; set; }

    /// <summary>Timeout for <see cref="ReadByte"/>, in milliseconds.</summary>
    int ReadTimeout { get; set; }

    /// <summary>Number of received bytes waiting to be read.</summary>
    int BytesToRead { get; }

    void Write(ReadOnlySpan<byte> data);

    /// <summary>Reads one byte, throwing <see cref="TimeoutException"/> after <see cref="ReadTimeout"/>.</summary>
    byte ReadByte();

    /// <summary>Drops any received bytes not read yet.</summary>
    void DiscardInBuffer();
}
