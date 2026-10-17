using System.Net;
using System.Net.Sockets;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Transport.Network;

/// <summary>
/// Shares a local K-Line adapter (or the simulator) on the network with RFC
/// 2217, so a laptop or phone can diagnose a car through a Raspberry Pi left
/// in it. One client at a time; the adapter is opened for each client and
/// closed when it leaves.
/// </summary>
/// <remarks>
/// The bridge passes bytes, speed and line signals through and does not
/// look at them: the safety checks on active commands stay in the client.
/// </remarks>
public sealed class Rfc2217Server : IDisposable
{
    private readonly Func<ITransport> _openTransport;
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private Task? _accept;
    private TcpClient? _current;

    /// <param name="openTransport">Creates the adapter transport for a new client (not opened yet).</param>
    public Rfc2217Server(Func<ITransport> openTransport, IPEndPoint endpoint)
    {
        _openTransport = openTransport;
        _listener = new TcpListener(endpoint);
    }

    /// <summary>Progress messages: client connected, speed changes, errors.</summary>
    public Action<string>? Log { get; init; }

    /// <summary>The address actually listened on (useful with port 0).</summary>
    public IPEndPoint LocalEndpoint => (IPEndPoint)_listener.LocalEndpoint;

    public void Start()
    {
        _listener.Start(1);
        _accept = Task.Run(AcceptLoop);
    }

    private async Task AcceptLoop()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException)
            {
                return;
            }
            Log?.Invoke($"client {client.Client.RemoteEndPoint} connected");
            _current = client;
            try
            {
                Serve(client);
            }
            catch (Exception ex) when (ex is IOException or SocketException or InvalidOperationException or ObjectDisposedException
                                           or UnauthorizedAccessException or NotSupportedException or TimeoutException)
            {
                Log?.Invoke("error: " + ex.Message);
            }
            finally
            {
                _current = null;
                client.Dispose();
                Log?.Invoke("client disconnected");
            }
        }
    }

    private void Serve(TcpClient client)
    {
        client.NoDelay = true;
        using var transport = _openTransport();
        transport.Open();
        var stream = client.GetStream();
        var sendGate = new Lock();
        void Send(byte[] bytes)
        {
            lock (sendGate)
                stream.Write(bytes);
        }

        using var done = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        var parser = new TelnetParser
        {
            Data = b => transport.Write([b]),
            Negotiation = (verb, option) =>
            {
                if (verb == Telnet.Will)
                    Send([Telnet.Iac, Telnet.Supported(option) ? Telnet.Do : Telnet.Dont, option]);
                else if (verb == Telnet.Do)
                    Send([Telnet.Iac, Telnet.Supported(option) ? Telnet.Will : Telnet.Wont, option]);
            },
            Subnegotiation = (option, payload) =>
            {
                if (option == Telnet.OptionComPort && payload.Length > 0)
                    Send(Telnet.Subnegotiation((byte)(payload[0] + Telnet.ServerOffset), Apply(transport, payload[0], payload[1..])));
            },
        };

        // Adapter → network.
        var pump = Task.Run(() =>
        {
            var token = done.Token;
            var chunk = new List<byte>(64);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var n = transport.BytesToRead;
                    if (n == 0)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    chunk.Clear();
                    for (var i = 0; i < n; i++)
                        chunk.Add(transport.ReadByte());
                    Send(Telnet.Escape(chunk.ToArray()));
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException or TimeoutException)
                {
                    return;
                }
            }
        });

        // Network → adapter.
        var buffer = new byte[512];
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var n = stream.Read(buffer, 0, buffer.Length);
                if (n == 0)
                    break;
                for (var i = 0; i < n; i++)
                    parser.Feed(buffer[i]);
            }
        }
        finally
        {
            done.Cancel();
            pump.Wait(1000);
            transport.Close();
        }
    }

    /// <summary>Applies a COM port setting and returns the value to confirm.</summary>
    private byte[] Apply(ITransport transport, byte command, byte[] value)
    {
        switch (command)
        {
            case Telnet.SetBaudRate when value.Length == 4:
                var baud = (value[0] << 24) | (value[1] << 16) | (value[2] << 8) | value[3];
                if (baud != 0) // 0 asks for the current value
                {
                    transport.BaudRate = baud;
                    Log?.Invoke($"speed {baud} baud");
                }
                return Telnet.BigEndian(transport.BaudRate);
            case Telnet.SetControl when value.Length == 1 && transport is ILineControl line:
                switch (value[0])
                {
                    case Telnet.ControlBreakOn:
                        line.SetBreak(true);
                        break;
                    case Telnet.ControlBreakOff:
                        line.SetBreak(false);
                        break;
                    case Telnet.ControlDtrOn:
                        line.DtrEnable = true;
                        break;
                    case Telnet.ControlDtrOff:
                        line.DtrEnable = false;
                        break;
                }
                return value;
            case Telnet.PurgeData:
                if (value.Length == 1 && value[0] is 1 or 3)
                    transport.DiscardInBuffer();
                return value;
            default:
                // Data size, parity, stop bits: the K-Line is always 8N1.
                return value;
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _listener.Stop();
        _current?.Dispose(); // ends the blocking read of the client being served
        try
        {
            _accept?.Wait(2000);
        }
        catch (AggregateException)
        {
        }
        _stop.Dispose();
    }
}
