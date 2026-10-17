using System.Net;
using System.Net.Sockets;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Presentation.Services;
using MarelliIawEcuStudio.Simulator;
using MarelliIawEcuStudio.Transport.Network;

namespace MarelliIawEcuStudio.Tests;

/// <summary>The RFC 2217 client and server, end to end over a loopback socket.</summary>
[Collection(nameof(TimingCollection))]
public class NetworkBridgeTests
{
    private static readonly DefinitionRegistry Registry = DefinitionRegistry.BuiltIn();

    private static Rfc2217Server Serve(Func<ITransport> open)
    {
        var server = new Rfc2217Server(open, new IPEndPoint(IPAddress.Loopback, 0));
        server.Start();
        return server;
    }

    [Theory]
    [InlineData("IAW-8F/18F")]
    [InlineData("IAW-04K.P8")]
    public void FullSessionThroughTheBridge(string id)
    {
        SimulatorTransport? simulator = null;
        using var server = Serve(() => simulator = new SimulatorTransport(new SimulatedEcu(Registry.Get(id))));
        using var session = new DiagnosticSession(new Rfc2217Transport("127.0.0.1", server.LocalEndpoint.Port), Registry,
            new SessionOptions { Timing = ProtocolTiming.Fast, ListenForIsoBroadcast = false, ActiveCommands = ActiveCommandPolicy.All });

        var c = session.Connect(id, TestContext.Current.CancellationToken);
        Assert.Equal(simulator!.Ecu.Iso, c.Iso);
        Assert.Equal(simulator.Ecu.Codric, c.Codric);

        simulator.Ecu.SetValue("PERIODE", 2500);
        var rpm = session.GetParameter("PERIODE");
        session.PollOnce(new PollRequest { Parameters = [rpm] });
        Assert.Equal("2500", rpm.Format(session.Buffer, System.Globalization.CultureInfo.InvariantCulture));

        // Active mode uses 0xAA/0xFF: 0xFF must survive Telnet escaping both ways.
        var result = session.RunActiveTest(session.Definition.ActiveTests[0], TestContext.Current.CancellationToken);
        Assert.Equal(ActiveOutcome.Passed, result.Outcome);
        Assert.Contains(IawLink.ExitActiveMode, simulator.Ecu.ActiveLog);
        Assert.Equal(0, session.Link.Statistics.EchoMismatch);
    }

    [Fact]
    public void ForwardsSpeedBreakAndDtr()
    {
        var line = new RecordingTransport();
        using var server = Serve(() => line);
        using var client = new Rfc2217Transport("127.0.0.1", server.LocalEndpoint.Port) { DtrEnable = true };
        client.Open();
        client.BaudRate = 10400;
        client.SetBreak(true);
        client.SetBreak(false);
        client.Write([0xFF, 0x00, 0xFF]);

        Assert.Equal(10400, line.BaudRate);
        Assert.True(line.DtrEnable);
        Assert.Equal([true, false], line.Breaks);
        WaitFor(() => line.Written.Count == 3);
        Assert.Equal([0xFF, 0x00, 0xFF], line.Written);
    }

    [Fact]
    public async Task ServerWithoutRfc2217IsReported()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var accept = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        using var client = new Rfc2217Transport("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port) { ControlTimeoutMs = 200 };
        var ex = Assert.Throws<NotSupportedException>(client.Open);
        Assert.Contains("does not speak RFC 2217", ex.Message);
        (await accept).Dispose();
    }

    [Fact]
    public void UnreachableBridgeIsAnIoError()
    {
        using var client = new Rfc2217Transport("127.0.0.1", 1);
        Assert.Throws<IOException>(client.Open);
    }

    [Theory]
    [InlineData("rfc2217://192.168.4.1:2217", "192.168.4.1", 2217)]
    [InlineData("rfc2217://pi.local", "pi.local", 2217)]
    [InlineData("RFC2217://[::1]:3000/", "::1", 3000)]
    public void ParsesAddresses(string address, string host, int port)
    {
        Assert.True(Rfc2217Transport.TryParseAddress(address, out var h, out var p));
        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    [Theory]
    [InlineData("/dev/ttyUSB0")]
    [InlineData("rfc2217://")]
    [InlineData("rfc2217://host:99999")]
    public void RejectsOtherAddresses(string address) => Assert.False(Rfc2217Transport.TryParseAddress(address, out _, out _));

    [Fact]
    public void AppOffersTheBridgeAsATarget()
    {
        Assert.Equal("rfc2217://192.168.4.1:2217", ConnectionTarget.Bridge("192.168.4.1:2217")!.Id);
        Assert.Equal("rfc2217://pi.local:2217", ConnectionTarget.Bridge("pi.local")!.Id);
        Assert.Null(ConnectionTarget.Bridge(" "));
        Assert.Null(ConnectionTarget.Bridge("host:notaport"));
    }

    private static void WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("timed out");
            Thread.Sleep(5);
        }
    }

    /// <summary>Remembers what the bridge did to the line.</summary>
    private sealed class RecordingTransport : ITransport, ILineControl
    {
        private readonly Lock _gate = new();
        private readonly List<byte> _written = [];

        public List<bool> Breaks { get; } = [];

        public List<byte> Written
        {
            get
            {
                lock (_gate)
                    return [.. _written];
            }
        }

        public string Name => "recording";

        public bool IsOpen { get; private set; }

        public int BaudRate { get; set; } = 9600;

        public int ReadTimeout { get; set; }

        public int BytesToRead => 0;

        public bool DtrEnable { get; set; }

        public void SetBreak(bool on) => Breaks.Add(on);

        public void Open() => IsOpen = true;

        public void Close() => IsOpen = false;

        public void Write(ReadOnlySpan<byte> data)
        {
            lock (_gate)
                _written.AddRange(data.ToArray());
        }

        public byte ReadByte() => throw new TimeoutException();

        public void DiscardInBuffer()
        {
        }

        public void Dispose()
        {
        }
    }
}
