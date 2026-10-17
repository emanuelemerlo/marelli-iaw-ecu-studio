using System.Net.Sockets;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Transport.Network;

/// <summary>
/// A K-Line adapter on the network, reached with RFC 2217 (Telnet COM port
/// control): a Raspberry Pi running <c>iaw-cli bridge</c> or <c>ser2net</c>,
/// or an ESP32/ESP8266 with esp-link and a K-Line transceiver.
/// </summary>
/// <remarks>
/// <para>Unlike a transparent Bluetooth or Wi-Fi serial link, RFC 2217 carries
/// the speed changes, so the 1200 → 7680 baud switch of the IAW protocol
/// reaches the adapter. Every speed change waits for the server to confirm
/// it; a server without RFC 2217 is reported instead of talking at a wrong
/// speed.</para>
/// <para>Address: <c>rfc2217://host:port</c> (see <see cref="TryParseAddress"/>).</para>
/// </remarks>
public sealed class Rfc2217Transport : ITransport, ILineControl
{
    public const string Scheme = "rfc2217://";
    public const int DefaultPort = 2217;

    private readonly Lock _gate = new();
    private readonly Queue<byte> _rx = new();
    private readonly SemaphoreSlim _ack = new(0);
    private TcpClient? _client;
    private NetworkStream? _stream;
    private Thread? _reader;
    private TelnetParser? _parser;
    private volatile bool _comPortAccepted;
    private int _baudRate = 9600;
    private bool _dtr;
    private byte _expectedAck;

    public Rfc2217Transport(string host, int port = DefaultPort)
    {
        Host = host;
        Port = port;
    }

    public string Host { get; }

    public int Port { get; }

    public string Name => $"{Scheme}{Host}:{Port}";

    /// <summary>How long to wait for the server to confirm a speed or signal change.</summary>
    public int ControlTimeoutMs { get; init; } = 2000;

    public bool IsOpen => _client?.Connected == true;

    public int ReadTimeout { get; set; } = 500;

    /// <summary>Accepts <c>rfc2217://host[:port]</c>.</summary>
    public static bool TryParseAddress(string address, out string host, out int port)
    {
        host = "";
        port = DefaultPort;
        if (!address.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase))
            return false;
        var rest = address[Scheme.Length..].TrimEnd('/');
        var colon = rest.LastIndexOf(':');
        if (colon > 0 && !rest.EndsWith(']'))
        {
            if (!int.TryParse(rest[(colon + 1)..], out port) || port is <= 0 or > 65535)
                return false;
            rest = rest[..colon];
        }
        host = rest.Trim('[', ']');
        return host.Length > 0;
    }

    public void Open()
    {
        var client = new TcpClient { NoDelay = true };
        try
        {
            client.Connect(Host, Port);
        }
        catch (SocketException ex)
        {
            client.Dispose();
            throw new IOException($"Cannot reach the bridge at {Host}:{Port}: {ex.Message}", ex);
        }
        _client = client;
        _stream = client.GetStream();
        _comPortAccepted = false;
        lock (_gate)
            _rx.Clear();
        _parser = new TelnetParser
        {
            Data = b =>
            {
                lock (_gate)
                    _rx.Enqueue(b);
            },
            Negotiation = OnNegotiation,
            Subnegotiation = OnSubnegotiation,
        };
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "rfc2217-reader" };
        _reader.Start();

        Send([
            Telnet.Iac, Telnet.Will, Telnet.OptionComPort,
            Telnet.Iac, Telnet.Will, Telnet.OptionBinary,
            Telnet.Iac, Telnet.Do, Telnet.OptionBinary,
            Telnet.Iac, Telnet.Will, Telnet.OptionSuppressGoAhead,
            Telnet.Iac, Telnet.Do, Telnet.OptionSuppressGoAhead,
        ]);
        // 8 data bits, no parity, 1 stop bit, then the current speed and DTR.
        Control(Telnet.SetDataSize, [8]);
        Control(Telnet.SetParity, [1]);
        Control(Telnet.SetStopSize, [1]);
        BaudRate = _baudRate;
        DtrEnable = _dtr;
    }

    private void OnNegotiation(byte verb, byte option)
    {
        switch (verb)
        {
            case Telnet.Do:
                if (option == Telnet.OptionComPort)
                    _comPortAccepted = true;
                // Already offered with WILL for the supported options.
                if (!Telnet.Supported(option))
                    Send([Telnet.Iac, Telnet.Wont, option]);
                break;
            case Telnet.Will:
                if (!Telnet.Supported(option))
                    Send([Telnet.Iac, Telnet.Dont, option]);
                break;
        }
    }

    private void OnSubnegotiation(byte option, byte[] payload)
    {
        if (option != Telnet.OptionComPort || payload.Length == 0)
            return;
        if (payload[0] == _expectedAck)
            _ack.Release();
    }

    private void ReadLoop()
    {
        var stream = _stream!;
        var parser = _parser!;
        var buffer = new byte[512];
        try
        {
            while (true)
            {
                var n = stream.Read(buffer, 0, buffer.Length);
                if (n == 0)
                    break;
                for (var i = 0; i < n; i++)
                    parser.Feed(buffer[i]);
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            // Connection closed: reads time out from now on, writes fail.
        }
    }

    /// <summary>Sends a COM port subcommand and waits for the server to confirm it.</summary>
    private void Control(byte command, ReadOnlySpan<byte> value)
    {
        EnsureOpen();
        while (_ack.Wait(0))
        {
            // drop stale confirmations
        }
        _expectedAck = (byte)(command + Telnet.ServerOffset);
        Send(Telnet.Subnegotiation(command, value));
        if (!_ack.Wait(ControlTimeoutMs))
        {
            throw new NotSupportedException(_comPortAccepted
                ? $"The bridge at {Host}:{Port} did not confirm a line setting (RFC 2217 command {command})."
                : $"The server at {Host}:{Port} does not speak RFC 2217, so it cannot change the line speed. " +
                  "Use 'iaw-cli bridge', or ser2net with the 'telnet(rfc2217)' option.");
        }
    }

    public int BaudRate
    {
        get => _baudRate;
        set
        {
            _baudRate = value;
            if (IsOpen)
                Control(Telnet.SetBaudRate, Telnet.BigEndian(value));
        }
    }

    public bool DtrEnable
    {
        get => _dtr;
        set
        {
            _dtr = value;
            if (IsOpen)
                Control(Telnet.SetControl, [value ? Telnet.ControlDtrOn : Telnet.ControlDtrOff]);
        }
    }

    public void SetBreak(bool on) => Control(Telnet.SetControl, [on ? Telnet.ControlBreakOn : Telnet.ControlBreakOff]);

    public int BytesToRead
    {
        get
        {
            lock (_gate)
                return _rx.Count;
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureOpen();
        Send(Telnet.Escape(data));
    }

    private void Send(byte[] bytes)
    {
        try
        {
            lock (_stream!)
                _stream.Write(bytes);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
        {
            throw new IOException($"The connection to the bridge at {Host}:{Port} was lost.", ex);
        }
    }

    public byte ReadByte()
    {
        EnsureOpen();
        var deadline = Environment.TickCount64 + ReadTimeout;
        while (true)
        {
            lock (_gate)
            {
                if (_rx.TryDequeue(out var b))
                    return b;
            }
            if (Environment.TickCount64 >= deadline)
                throw new TimeoutException($"No data from the bridge at {Host}:{Port}.");
            Thread.Sleep(1);
        }
    }

    public void DiscardInBuffer()
    {
        lock (_gate)
            _rx.Clear();
    }

    private void EnsureOpen()
    {
        if (_stream is null)
            throw new InvalidOperationException("The connection is closed.");
    }

    public void Close()
    {
        _stream?.Dispose();
        _client?.Dispose();
        _stream = null;
        _client = null;
        _reader?.Join(1000);
        _reader = null;
    }

    public void Dispose()
    {
        Close();
        _ack.Dispose();
    }
}
