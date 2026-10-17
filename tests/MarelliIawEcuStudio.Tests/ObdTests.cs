using System.Globalization;
using System.Text.RegularExpressions;
using MarelliIawEcuStudio.Core.KLine;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Obd;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Simulator;

namespace MarelliIawEcuStudio.Tests;

/// <summary>Framed K-Line (ISO 9141-2, ISO 14230) and OBD-II against the simulated ECUs.</summary>
[Collection(nameof(TimingCollection))]
public class ObdTests
{
    private static readonly KLineTiming Fast = KLineTiming.Fast;

    public static readonly TheoryData<KLineProtocol> Protocols = new(KLineProtocol.Iso9141, KLineProtocol.Kwp2000Slow, KLineProtocol.Kwp2000Fast);

    private static (Obd2Session Session, KLineSimulatorTransport Line) Create(KLineProtocol protocol, Obd2Options? options = null, params SimulatedObdEcu[] ecus)
    {
        var line = new KLineSimulatorTransport(protocol, Fast, ecus);
        return (new Obd2Session(line, (options ?? new Obd2Options()) with { Timing = Fast }), line);
    }

    /// <summary>Connects, adding what the simulated ECU saw of the init to a failure.</summary>
    private static ObdConnectionInfo Connect(Obd2Session session, KLineProtocol? protocol = null)
    {
        try
        {
            return session.Connect(protocol, TestContext.Current.CancellationToken);
        }
        catch (KLineException ex)
        {
            throw new KLineException($"{ex.Message} Simulator: {((KLineSimulatorTransport)session.Link.Transport).InitLog}");
        }
    }

    // ---- framing -------------------------------------------------------------------

    [Fact]
    public void EncodesRequestsAsTheStandardsShow()
    {
        // ISO 9141-2: 68 6A F1 01 0C cs; ISO 14230-4: C2 33 F1 01 0C cs.
        Assert.Equal(Convert.FromHexString("686AF1010CD0"), KLineFraming.EncodeRequest(KLineProtocol.Iso9141, 0x33, [0x01, 0x0C]));
        Assert.Equal(Convert.FromHexString("C233F1010CF3"), KLineFraming.EncodeRequest(KLineProtocol.Kwp2000Fast, 0x33, [0x01, 0x0C]));
        Assert.Equal(Convert.FromHexString("C133F18166"), KLineFraming.EncodeRequest(KLineProtocol.Kwp2000Fast, 0x33, [0x81]));
    }

    [Fact]
    public void SplitsIso9141AnswersByChecksumEvenWhenItMatchesEarly()
    {
        // 48+6B+10+41 = 0x10C: the checksum "matches" at the PID byte 0C of the same message.
        var rpm = KLineFraming.EncodeResponse(KLineProtocol.Iso9141, 0x10, [0x41, 0x0C, 0x0D, 0x48]);
        var second = KLineFraming.EncodeResponse(KLineProtocol.Iso9141, 0x18, [0x41, 0x0C, 0x00, 0x00]);
        var frames = KLineFraming.Decode(KLineProtocol.Iso9141, [.. rpm, .. second], out var consumed);
        Assert.Equal(2, frames.Count);
        Assert.Equal([0x41, 0x0C, 0x0D, 0x48], frames[0].Data);
        Assert.Equal((byte)0x18, frames[1].Source);
        Assert.Equal(rpm.Length + second.Length, consumed);
    }

    [Fact]
    public void DecodesKwpMessagesWithASeparateLengthByte()
    {
        var data = Enumerable.Range(0, 70).Select(i => (byte)i).ToArray();
        var message = KLineFraming.EncodeResponse(KLineProtocol.Kwp2000Fast, 0x10, data);
        Assert.Equal(0x80, message[0]);
        var frames = KLineFraming.Decode(KLineProtocol.Kwp2000Fast, message, out _);
        Assert.Equal(data, Assert.Single(frames).Data);
    }

    [Fact]
    public void RejectsKwpMessagesWithABadChecksum()
    {
        var message = KLineFraming.EncodeResponse(KLineProtocol.Kwp2000Fast, 0x10, [0x41, 0x0D, 0x32]);
        message[^1] ^= 0xFF;
        Assert.Empty(KLineFraming.Decode(KLineProtocol.Kwp2000Fast, message, out var consumed));
        Assert.Equal(0, consumed);
    }

    // ---- init and session --------------------------------------------------------

    [Theory]
    [MemberData(nameof(Protocols))]
    public void ConnectsWithAutomaticProtocolDetection(KLineProtocol protocol)
    {
        var (session, line) = Create(protocol);
        using (session)
        {
            var c = Connect(session);
            Assert.Equal(protocol, c.Protocol);
            Assert.Equal([(byte)0x10], c.Ecus);
            Assert.Contains((byte)0x0C, c.SupportedPids);
            Assert.True(line.IsAwake);
            if (protocol != KLineProtocol.Kwp2000Fast)
                Assert.Equal((byte)0x33, line.LastAddress); // the 5-baud address was sent bit by bit with breaks
        }
    }

    [Theory]
    [MemberData(nameof(Protocols))]
    public void ReadsLiveValues(KLineProtocol protocol)
    {
        var (session, line) = Create(protocol);
        using (session)
        {
            Connect(session, protocol);
            line.Ecus[0].SetValue("RPM", 2125.5);
            line.Ecus[0].SetValue("ECT", 92);
            var values = session.Read([ObdPids.Find("RPM")!, ObdPids.Find("ECT")!, ObdPids.Find("VPWR")!]).ToDictionary(r => r.Pid.Id, r => r.Value);
            Assert.Equal(2125.5, values["RPM"]);
            Assert.Equal(92, values["ECT"]);
            Assert.Equal(14.1, values["VPWR"], 2);
        }
    }

    [Fact]
    public void SupportedPidsFollowTheBitmapsAcrossBlocks()
    {
        var (session, _) = Create(KLineProtocol.Iso9141);
        using (session)
        {
            var c = Connect(session);
            Assert.Contains((byte)0x42, c.SupportedPids); // second block (0x21-0x40) says the third exists
            Assert.DoesNotContain((byte)0x10, c.SupportedPids); // no MAF on a MAP engine
            Assert.Equal(c.SupportedPids.Count(p => ObdPids.Get(p) is not null), session.SupportedKnownPids().Count);
        }
    }

    [Fact]
    public void ReadsTroubleCodesAndVin()
    {
        var ecu = new SimulatedObdEcu { Mil = true };
        ecu.StoredCodes.AddRange([Dtc.Parse("P0301"), Dtc.Parse("P0171"), Dtc.Parse("P0420"), Dtc.Parse("P1250")]);
        ecu.PendingCodes.Add(Dtc.Parse("P0302"));
        var (session, _) = Create(KLineProtocol.Kwp2000Fast, null, ecu);
        using (session)
        {
            Connect(session);
            var report = session.ReadDtcs();
            Assert.True(report.Mil);
            Assert.Equal(4, report.ReportedCount);
            Assert.Equal(["P0301", "P0171", "P0420", "P1250"], report.Stored.Select(d => d.Code)); // two messages of three codes
            Assert.Equal(["P0302"], report.Pending.Select(d => d.Code));
            Assert.False(report.Stored[3].IsGeneric);
            Assert.Equal("ZFA18800000123456", session.ReadVin());
        }
    }

    [Fact]
    public void ClearingCodesNeedsPermission()
    {
        var ecu = new SimulatedObdEcu { Mil = true };
        ecu.StoredCodes.Add(Dtc.Parse("P0301"));
        var (readOnly, _) = Create(KLineProtocol.Iso9141, null, ecu);
        using (readOnly)
        {
            Connect(readOnly);
            Assert.Throws<ActiveCommandNotAllowedException>(() => readOnly.ClearDtcs());
            Assert.Single(ecu.StoredCodes);
        }
        var (allowed, _) = Create(KLineProtocol.Iso9141, new Obd2Options { AllowClear = true }, ecu);
        using (allowed)
        {
            Connect(allowed);
            Assert.True(allowed.ClearDtcs());
            Assert.Empty(allowed.ReadDtcs().Stored);
            Assert.False(ecu.Mil);
        }
    }

    [Theory]
    [InlineData(KLineProtocol.Iso9141)]
    [InlineData(KLineProtocol.Kwp2000Fast)]
    public void SeveralEcusAnswerTheSameRequest(KLineProtocol protocol)
    {
        var engine = new SimulatedObdEcu(0x10);
        var gearbox = new SimulatedObdEcu(0x18);
        engine.SetValue("RPM", 900);
        gearbox.SetValue("RPM", 905);
        var (session, line) = Create(protocol, null, engine, gearbox);
        line.InterEcuDelayMs = 3;
        using (session)
        {
            var c = Connect(session, protocol);
            Assert.Equal([(byte)0x10, (byte)0x18], c.Ecus);
            Assert.Equal(2, session.Link.ExpectedResponders);
            var rpm = session.Read([ObdPids.Find("RPM")!]);
            Assert.Equal([900.0, 905.0], rpm.OrderBy(r => r.Ecu).Select(r => r.Value));
        }
    }

    [Fact]
    public void WaitsThroughResponsePending()
    {
        var ecu = new SimulatedObdEcu();
        var (session, _) = Create(KLineProtocol.Kwp2000Fast, null, ecu);
        using (session)
        {
            Connect(session);
            ecu.RespondPendingOnce(0x09);
            Assert.Equal("ZFA18800000123456", session.ReadVin());
        }
    }

    [Fact]
    public void ReportsWhenNoEcuAnswers()
    {
        var (session, line) = Create(KLineProtocol.Iso9141);
        line.EcuConnected = false;
        using (session)
        {
            var ex = Assert.Throws<KLineException>(() => Connect(session));
            Assert.Contains("No OBD ECU answered", ex.Message);
            Assert.Contains("Iso9141", ex.Message);
            Assert.Contains("Kwp2000Fast", ex.Message);
        }
    }

    [Fact]
    public void LosingTheEcuEndsTheSession()
    {
        var (session, line) = Create(KLineProtocol.Iso9141);
        using (session)
        {
            Connect(session);
            line.EcuConnected = false;
            Assert.Throws<CommunicationLostException>(() => session.Read([ObdPids.Find("RPM")!, ObdPids.Find("ECT")!]));
        }
    }

    [Fact]
    public void TraceRecordsTheInitAndTheMessages()
    {
        var (session, _) = Create(KLineProtocol.Iso9141);
        var text = new StringWriter();
        using (session)
        using (var trace = new LineTraceWriter(text))
        {
            session.Link.Monitor += trace.Write;
            Connect(session);
            session.Read([ObdPids.Find("RPM")!]);
        }
        var events = LineTraceReader.Read(new StringReader(text.ToString())).Events;
        Assert.Contains(events, e => e.Note == "5-baud init 0x33");
        Assert.Equal([0x55, 0x08, 0x08], events.Where(e => e.Direction == LineDirection.Received).Take(3).Select(e => e.Value));
        Assert.All(events.Where(e => e.Direction == LineDirection.Sent), e => Assert.Equal(KLineTiming.BaudRate, e.BaudRate));
    }

    /// <summary>
    /// A busy machine (macOS CI) can pause the tester in the middle of the short fast-init
    /// pulse: the simulated ECU must still recognise the fast init by its shape.
    /// </summary>
    [Fact]
    public void FastInitSurvivesAPausedTester()
    {
        var line = new KLineSimulatorTransport(KLineProtocol.Kwp2000Fast, Fast);
        using var session = new Obd2Session(line, new Obd2Options { Timing = Fast with { FastInitLowMs = 35 } });
        Assert.Equal(KLineProtocol.Kwp2000Fast, Connect(session, KLineProtocol.Kwp2000Fast).Protocol);
    }

    /// <summary>
    /// The simulated K-Line, with one line change held back as if the operating
    /// system had paused the tester (what happens on a busy macOS CI machine).
    /// </summary>
    private sealed class LateLine(KLineSimulatorTransport line, int lateChange, int lateMs) : ITransport, ILineControl
    {
        private int _changes;

        public KLineSimulatorTransport Line => line;

        public void SetBreak(bool on)
        {
            if (++_changes == lateChange)
                Thread.Sleep(lateMs);
            line.SetBreak(on);
        }

        public bool DtrEnable { get => line.DtrEnable; set => line.DtrEnable = value; }

        public string Name => line.Name;

        public bool IsOpen => line.IsOpen;

        public void Open() => line.Open();

        public void Close() => line.Close();

        public int BaudRate { get => line.BaudRate; set => line.BaudRate = value; }

        public int ReadTimeout { get => line.ReadTimeout; set => line.ReadTimeout = value; }

        public int BytesToRead => line.BytesToRead;

        public void Write(ReadOnlySpan<byte> data) => line.Write(data);

        public byte ReadByte() => line.ReadByte();

        public void DiscardInBuffer() => line.DiscardInBuffer();

        public void Dispose() => line.Dispose();
    }

    private static List<string> Markers(KLineLink link)
    {
        var notes = new List<string>();
        link.Monitor += e =>
        {
            if (e.Direction == LineDirection.Marker && e.Note is { } note)
                notes.Add(note);
        };
        return notes;
    }

    [Theory]
    [InlineData(KLineProtocol.Iso9141, 4, 60)]     // an edge in the middle of the address, 1.5 bits late: wrong address
    [InlineData(KLineProtocol.Kwp2000Fast, 3, 450)] // the end of the wake-up pulse, longer than a whole address
    public void AnInitWithALateLineChangeIsRepeated(KLineProtocol protocol, int lateChange, int lateMs)
    {
        // Line change 1 is the release of the idle line before the init.
        var line = new LateLine(new KLineSimulatorTransport(protocol, Fast), lateChange, lateMs);
        using var session = new Obd2Session(line, new Obd2Options { Timing = Fast });
        var notes = Markers(session.Link);
        var info = session.Connect(protocol, TestContext.Current.CancellationToken);
        Assert.Equal(protocol, info.Protocol);
        Assert.Single(notes, n => n.StartsWith("init repeated", StringComparison.Ordinal));
    }

    /// <summary>
    /// With no ECU the init fails, and is repeated only for a line change that really
    /// came late (which a loaded machine can cause by itself), never more than allowed.
    /// </summary>
    [Fact]
    public void AnAbsentEcuIsNotRetriedWithoutReason()
    {
        var line = new KLineSimulatorTransport(KLineProtocol.Iso9141, Fast) { EcuConnected = false };
        using var session = new Obd2Session(line, new Obd2Options { Timing = Fast });
        var notes = Markers(session.Link);
        Assert.Throws<KLineException>(() => session.Connect(KLineProtocol.Iso9141, TestContext.Current.CancellationToken));
        var inits = notes.Count(n => n.StartsWith("5-baud init", StringComparison.Ordinal));
        var repeats = notes.Where(n => n.StartsWith("init repeated", StringComparison.Ordinal)).ToList();
        Assert.Equal(1 + repeats.Count, inits);
        Assert.InRange(inits, 1, KLineLink.MaxInitAttempts);
        Assert.All(repeats, n => Assert.True(
            int.Parse(Regex.Match(n, @"came (\d+) ms late").Groups[1].Value, CultureInfo.InvariantCulture) >= Fast.FiveBaudBitMs * 0.2, n));
    }

    // ---- KWP2000 services ----------------------------------------------------------

    [Fact]
    public void KwpClientReportsNegativeResponses()
    {
        var (session, _) = Create(KLineProtocol.Kwp2000Fast);
        using (session)
        {
            Connect(session);
            var kwp = new Kwp2000Client(session.Link);
            kwp.TesterPresent();
            Assert.Throws<KLineException>(() => kwp.ReadDataByLocalIdentifier(0x01)); // the OBD simulator does not answer it
            Assert.True(Kwp2000Client.IsReadOnly(Kwp2000Client.ReadDataByLocalIdentifierId));
            Assert.False(Kwp2000Client.IsReadOnly(Kwp2000Client.ClearDiagnosticInformationId));
            Assert.Equal("response pending", Kwp2000Client.Describe(0x78));
        }
    }

    // ---- DTC codes -------------------------------------------------------------------

    [Theory]
    [InlineData("P0301", 0x0301, true)]
    [InlineData("P1250", 0x1250, false)]
    [InlineData("P2187", 0x2187, true)]
    [InlineData("C0035", 0x4035, true)]
    [InlineData("B1A01", 0x9A01, false)]
    [InlineData("U0100", 0xC100, true)]
    public void FormatsTroubleCodes(string code, int raw, bool generic)
    {
        var dtc = Dtc.Parse(code);
        Assert.Equal(raw, dtc.Raw);
        Assert.Equal(code, new Dtc((ushort)raw).Code);
        Assert.Equal(generic, dtc.IsGeneric);
    }
}
