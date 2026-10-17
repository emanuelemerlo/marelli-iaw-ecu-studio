using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Simulator;

namespace MarelliIawEcuStudio.Tests;

/// <summary>Typed line faults, line traces, trace replay and reconnection.</summary>
public class LineTraceTests
{
    private static readonly DefinitionRegistry Registry = DefinitionRegistry.BuiltIn();

    private static readonly SessionOptions Fast = new() { Timing = ProtocolTiming.Fast, ListenForIsoBroadcast = false };

    private static SimulatorTransport Simulator(string id = "IAW-8F/18F", SimulatedEcuOptions? options = null) =>
        new(new SimulatedEcu(Registry.Get(id), options));

    /// <summary>Connects, polls <paramref name="cycles"/> times and returns the decoded values of every cycle.</summary>
    private static List<string> ReadCycles(DiagnosticSession session, string[] parameters, int cycles, string? ecu = "IAW-8F/18F")
    {
        session.Connect(ecu, TestContext.Current.CancellationToken);
        var selected = parameters.Select(session.GetParameter).ToList();
        var values = new List<string>();
        for (var i = 0; i < cycles; i++)
        {
            session.PollOnce(new PollRequest { Parameters = selected });
            values.Add(string.Join(';', selected.Select(p => p.Format(session.Buffer))));
        }
        return values;
    }

    private static LineTrace Record(Func<DiagnosticSession, List<string>> run, out List<string> values, ITransport? transport = null, SessionOptions? options = null)
    {
        var text = new StringWriter();
        using (var trace = new LineTraceWriter(text, [new("transport", "test")]))
        using (var session = new DiagnosticSession(transport ?? Simulator(), Registry, options ?? Fast))
        {
            session.Link.Monitor += trace.Write;
            values = run(session);
            trace.Comment("line: " + session.Link.Statistics);
        }
        return LineTraceReader.Read(new StringReader(text.ToString()));
    }

    // ---- typed line faults ---------------------------------------------------

    [Fact]
    public void ExchangeTellsApartTheLineFaults()
    {
        var transport = Simulator();
        using var session = new DiagnosticSession(transport, Registry, Fast);
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        var link = session.Link;
        transport.ReadTimeout = 20;
        var before = (link.Statistics.Requests, link.Statistics.NoAnswer, link.Statistics.NoEcho, link.Statistics.EchoMismatch);

        Assert.Equal(LineFault.None, link.Exchange(0x01).Fault);

        transport.EcuConnected = false; // echo only
        var r = link.Exchange(0x01);
        Assert.Equal(LineFault.NoAnswer, r.Fault);
        Assert.False(r.Answered);
        Assert.Equal((byte)0x01, r.Echo);

        transport.Echo = false; // nothing at all
        Assert.Equal(LineFault.NoEcho, link.Exchange(0x01).Fault);

        transport.EcuConnected = true; // the ECU answer is taken for the echo
        transport.Ecu.Registers[0x01] = 0x5A;
        r = link.Exchange(0x01);
        Assert.Equal(LineFault.EchoMismatch, r.Fault);
        Assert.Equal((byte)0x5A, r.Echo);
        Assert.False(r.Answered);

        Assert.Equal(before.Requests + 4, link.Statistics.Requests);
        Assert.Equal(before.NoAnswer + 1, link.Statistics.NoAnswer);
        Assert.Equal(before.NoEcho + 1, link.Statistics.NoEcho);
        Assert.Equal(before.EchoMismatch + 1, link.Statistics.EchoMismatch);
        Assert.Equal(LineFault.EchoMismatch, link.Statistics.LastFault);
    }

    [Fact]
    public void EchoMismatchStillReadsTheAnswerAsIes2Did()
    {
        // The adapter garbles the echo but the ECU answer follows.
        var link = new IawLink(new ScriptedTransport([0x02, 0x7F]), ProtocolTiming.Fast);
        var r = link.Exchange(0x01);
        Assert.True(r.Answered);
        Assert.Equal((byte)0x7F, r.Response);
        Assert.Equal(LineFault.EchoMismatch, r.Fault);
        Assert.True(link.Query(0x01, out _) is false); // nothing left: no echo now
    }

    [Fact]
    public void CommunicationLossCarriesTheFault()
    {
        var transport = Simulator();
        using var session = new DiagnosticSession(transport, Registry, Fast);
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        var request = new PollRequest { Parameters = [session.GetParameter("PERIODE")] };

        transport.EcuConnected = false;
        var lost = Assert.Throws<CommunicationLostException>(() => session.PollOnce(request));
        Assert.Equal(LineFault.NoAnswer, lost.Fault);
        Assert.Contains("ECU does not answer", lost.Message);

        transport.Echo = false;
        lost = Assert.Throws<CommunicationLostException>(() => session.PollOnce(request));
        Assert.Equal(LineFault.NoEcho, lost.Fault);
    }

    // ---- trace file ------------------------------------------------------------

    [Fact]
    public void TraceRecordsEveryByteAndRoundTrips()
    {
        var trace = Record(s => ReadCycles(s, ["PERIODE", "M_VBATT"], 2), out _,
            Simulator(options: new SimulatedEcuOptions { BroadcastIso = true }),
            Fast with { ListenForIsoBroadcast = true });

        Assert.Equal("test", trace.Metadata["transport"]);
        var e = trace.Events;
        // Key-on broadcast, then the slow init with its discarded echoes.
        Assert.Equal(LineDirection.Received, e.First(x => x.Direction != LineDirection.Marker).Direction);
        Assert.Equal([0x0F, 0xAA, 0xCC], e.Where(x => x.Direction == LineDirection.Sent && x.BaudRate == IawLink.InitBaudRate).Select(x => x.Value));
        Assert.Equal(3, e.Count(x => x.Direction == LineDirection.Discarded));
        // Every request at 7680 baud is echoed and answered.
        Assert.Equal(trace.RequestCount, e.Count(x => x.Direction == LineDirection.Echo));
        Assert.Equal(0, trace.TimeoutCount);
        Assert.Contains(e, x => x.Direction == LineDirection.Marker && x.Note!.StartsWith("identified ecu=IAW-8F/18F", StringComparison.Ordinal));
        Assert.True(e.Zip(e.Skip(1)).All(p => p.First.TimeMs <= p.Second.TimeMs));
    }

    [Fact]
    public void TraceRecordsTimeoutsAndNotes()
    {
        var text = new StringWriter();
        using (var trace = new LineTraceWriter(text))
        {
            trace.Write(new LineEvent(LineDirection.Sent, 0x31, 7680));
            trace.Write(new LineEvent(LineDirection.Timeout, 0x31, 7680));
            trace.Write(new LineEvent(LineDirection.Marker, 0, 7680) { Note = "a;b\nc" });
        }
        var read = LineTraceReader.Read(new StringReader(text.ToString()));
        Assert.Equal(1, read.TimeoutCount);
        Assert.Equal((byte)0x31, read.Events[1].Value);
        Assert.Equal("a,b c", read.Events[2].Note);
    }

    [Fact]
    public void ReaderRejectsOtherFiles()
    {
        Assert.Throws<InvalidDataException>(() => LineTraceReader.Read(new StringReader("# marelli-iaw-ecu-studio log v1\n")));
        Assert.Throws<InvalidDataException>(() => LineTraceReader.Read(new StringReader(LineTraceWriter.Signature + "\n" + LineTraceWriter.Header + "\n1.0;XX;7680;01;\n")));
    }

    // ---- replay ------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StrictReplayGivesTheRecordedValues(bool broadcast)
    {
        var options = Fast with { ListenForIsoBroadcast = broadcast };
        var trace = Record(s => ReadCycles(s, ["PERIODE", "M_VBATT", "MT_EAU_L"], 5, broadcast ? null : "IAW-8F/18F"), out var recorded,
            Simulator(options: new SimulatedEcuOptions { BroadcastIso = broadcast }), options);

        var replay = new TraceReplayTransport(trace, TraceReplayMode.Strict);
        using var session = new DiagnosticSession(replay, Registry, options);
        var replayed = ReadCycles(session, ["PERIODE", "M_VBATT", "MT_EAU_L"], 5, broadcast ? null : "IAW-8F/18F");

        Assert.Equal(recorded, replayed);
        Assert.Equal(broadcast, session.Connection!.IsoFromBroadcast);
        Assert.True(replay.IsExhausted);
        // After the end of the trace the ECU is gone.
        Assert.Throws<CommunicationLostException>(() => session.PollOnce(new PollRequest { Parameters = [session.GetParameter("PERIODE")] }));
    }

    [Fact]
    public void StrictReplayReportsWhereTheSoftwareDiverged()
    {
        var trace = Record(s => ReadCycles(s, ["PERIODE"], 2), out _);
        using var session = new DiagnosticSession(new TraceReplayTransport(trace), Registry, Fast);
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        var ex = Assert.Throws<TraceDivergedException>(() => session.PollOnce(new PollRequest { Parameters = [session.GetParameter("M_VBATT")] }));
        Assert.Contains("diverged", ex.Message);
    }

    [Fact]
    public void LookupReplayAnswersAnyRecordedRequest()
    {
        var trace = Record(s => ReadCycles(s, ["PERIODE", "M_VBATT"], 3), out var recorded);
        using var session = new DiagnosticSession(new TraceReplayTransport(trace, TraceReplayMode.Lookup), Registry, Fast);
        // Fewer parameters, more cycles than recorded: answers are reused in turn.
        var values = ReadCycles(session, ["M_VBATT"], 10);
        Assert.Equal(10, values.Count);
        Assert.All(values, v => Assert.Equal(recorded[0].Split(';')[1], v));
    }

    [Fact]
    public void ActiveTestsReplay()
    {
        var options = Fast with { ActiveCommands = ActiveCommandPolicy.All };
        ActiveOutcome recordedOutcome = default;
        var trace = Record(s =>
        {
            s.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
            recordedOutcome = s.RunActiveTest(s.Definition.ActiveTests[0], TestContext.Current.CancellationToken).Outcome;
            return [];
        }, out _, options: options);
        Assert.Contains(trace.Events, e => e.Direction == LineDirection.Marker && e.Note!.StartsWith("active test", StringComparison.Ordinal));

        using var session = new DiagnosticSession(new TraceReplayTransport(trace), Registry, options);
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        Assert.Equal(ActiveOutcome.Passed, recordedOutcome);
        Assert.Equal(recordedOutcome, session.RunActiveTest(session.Definition.ActiveTests[0], TestContext.Current.CancellationToken).Outcome);
    }

    // ---- reconnection ------------------------------------------------------------

    [Fact]
    public void RecoverRestoresTheConnectionAfterAPowerCycle()
    {
        var transport = Simulator();
        using var session = new DiagnosticSession(transport, Registry, Fast);
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        var request = new PollRequest { Parameters = [session.GetParameter("PERIODE")] };

        transport.EcuConnected = false;
        Assert.Throws<CommunicationLostException>(() => session.PollOnce(request));
        Assert.Equal(ReconnectOutcome.NoAnswer, session.Recover(TestContext.Current.CancellationToken, maxAttempts: 3));

        transport.Ecu.Reset(); // ignition cycled: the ECU needs a new init
        transport.EcuConnected = true;
        var attempts = new List<int>();
        Assert.Equal(ReconnectOutcome.Restored, session.Recover(TestContext.Current.CancellationToken, attempting: attempts.Add));
        Assert.Equal([1], attempts);
        session.PollOnce(request);
        Assert.True(session.GetParameter("PERIODE").IsValid(session.Buffer));
    }

    [Fact]
    public void RecoverRefusesADifferentEcu()
    {
        var first = Simulator(options: new SimulatedEcuOptions { Codric = "6160000001" });
        var second = Simulator(options: new SimulatedEcuOptions { Codric = "6160000002" });
        var transport = new SwitchTransport(first, second);
        using var session = new DiagnosticSession(transport, Registry, Fast);
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        transport.UseSecond = true;
        Assert.Equal(ReconnectOutcome.DifferentEcu, session.TryReconnect());
    }

    [Fact]
    public void RecoverStopsOnCancellation()
    {
        var transport = Simulator();
        using var session = new DiagnosticSession(transport, Registry, Fast with { Timing = ProtocolTiming.Fast with { ReconnectDelaysMs = [10_000] } });
        session.Connect("IAW-8F/18F", TestContext.Current.CancellationToken);
        using var cts = new CancellationTokenSource(50);
        Assert.ThrowsAny<OperationCanceledException>(() => session.Recover(cts.Token));
    }
}

/// <summary>Returns the given bytes in order, then times out.</summary>
internal sealed class ScriptedTransport(byte[] bytes) : ITransport
{
    private readonly Queue<byte> _rx = new(bytes);

    public string Name => "scripted";

    public bool IsOpen => true;

    public int BaudRate { get; set; } = 7680;

    public int ReadTimeout { get; set; }

    public int BytesToRead => _rx.Count;

    public void Open()
    {
    }

    public void Close()
    {
    }

    public void Write(ReadOnlySpan<byte> data)
    {
    }

    public byte ReadByte() => _rx.TryDequeue(out var b) ? b : throw new TimeoutException();

    public void DiscardInBuffer()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>Two simulated ECUs behind one line, to swap the unit under the session.</summary>
internal sealed class SwitchTransport(SimulatorTransport first, SimulatorTransport second) : ITransport
{
    public bool UseSecond { get; set; }

    private SimulatorTransport Current => UseSecond ? second : first;

    public string Name => "switch";

    public bool IsOpen => first.IsOpen;

    public int BaudRate
    {
        get => Current.BaudRate;
        set
        {
            first.BaudRate = value;
            second.BaudRate = value;
        }
    }

    public int ReadTimeout
    {
        get => Current.ReadTimeout;
        set
        {
            first.ReadTimeout = value;
            second.ReadTimeout = value;
        }
    }

    public int BytesToRead => Current.BytesToRead;

    public void Open()
    {
        first.Open();
        second.Open();
    }

    public void Close()
    {
        first.Close();
        second.Close();
    }

    public void Write(ReadOnlySpan<byte> data) => Current.Write(data);

    public byte ReadByte() => Current.ReadByte();

    public void DiscardInBuffer() => Current.DiscardInBuffer();

    public void Dispose() => Close();
}
