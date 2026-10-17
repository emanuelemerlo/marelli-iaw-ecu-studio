using System.IO.Ports;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Transport.Serial;

/// <summary>
/// K-Line access through a serial port (KKL cable with FTDI, CH340, PL2303...).
/// </summary>
/// <remarks>
/// The IAW protocol needs 7680 baud, which is not a standard POSIX speed.
/// .NET sets it through <c>termios2</c> on Linux and <c>IOSSIOSPEED</c> on
/// macOS; whether the adapter really produces it depends on its driver.
/// FTDI chips are the reference.
/// </remarks>
public sealed class SerialTransport : ITransport, ILineControl
{
    private readonly SerialPort _port;

    /// <param name="portName">For example <c>COM3</c>, <c>/dev/ttyUSB0</c> or <c>/dev/cu.usbserial-A10K</c>.</param>
    /// <param name="dtrEnable">
    /// Raise DTR. IES2 left DTR and RTS low (the SerialPort defaults); a few
    /// cables power their K-Line transceiver from DTR and need it.
    /// </param>
    public SerialTransport(string portName, bool dtrEnable = false, bool rtsEnable = false)
    {
        _port = new SerialPort(portName, 9600, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,
            DtrEnable = dtrEnable,
            RtsEnable = rtsEnable,
            ReadTimeout = 500,
            WriteTimeout = 500,
        };
    }

    public string Name => _port.PortName;

    public bool IsOpen => _port.IsOpen;

    public int BaudRate
    {
        get => _port.BaudRate;
        set
        {
            try
            {
                _port.BaudRate = value;
            }
            catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException)
            {
                throw new NotSupportedException(
                    $"The adapter on {Name} rejected {value} baud. The IAW protocol needs 1200 and 7680 baud; " +
                    "FTDI based cables support both. On Linux check the driver supports custom speeds.", ex);
            }
        }
    }

    public int ReadTimeout
    {
        get => _port.ReadTimeout;
        set => _port.ReadTimeout = value;
    }

    public int BytesToRead => _port.BytesToRead;

    public void Open()
    {
        try
        {
            _port.Open();
        }
        catch (UnauthorizedAccessException ex) when (OperatingSystem.IsLinux())
        {
            throw new UnauthorizedAccessException(
                $"Permission denied on {Name}. Add your user to the 'dialout' group (sudo usermod -aG dialout $USER), then log out and in again.", ex);
        }
    }

    public void Close()
    {
        if (_port.IsOpen)
            _port.Close();
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        var buffer = data.ToArray();
        _port.Write(buffer, 0, buffer.Length);
    }

    public byte ReadByte()
    {
        var value = _port.ReadByte();
        if (value < 0)
            throw new TimeoutException($"{Name}: end of stream.");
        return (byte)value;
    }

    public void DiscardInBuffer() => _port.DiscardInBuffer();

    public void SetBreak(bool on) => _port.BreakState = on;

    public bool DtrEnable
    {
        get => _port.DtrEnable;
        set => _port.DtrEnable = value;
    }

    public void Dispose()
    {
        Close();
        _port.Dispose();
    }
}
