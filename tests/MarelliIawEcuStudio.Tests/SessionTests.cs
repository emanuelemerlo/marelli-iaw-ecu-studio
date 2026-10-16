using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Simulator;

namespace MarelliIawEcuStudio.Tests;

/// <summary>End-to-end protocol tests: Core session against the simulated ECU.</summary>
public class SessionTests
{
    private static readonly DefinitionRegistry Registry = DefinitionRegistry.BuiltIn();

    public static readonly TheoryData<string> AutoDetectFamilies = new(Registry.AutoDetectOrder.Select(d => d.Id));

    private static (DiagnosticSession Session, SimulatorTransport Transport) Create(
        string id, SimulatedEcuOptions? ecuOptions = null, SessionOptions? sessionOptions = null)
    {
        var ecu = new SimulatedEcu(Registry.Get(id), ecuOptions);
        var transport = new SimulatorTransport(ecu);
        var session = new DiagnosticSession(transport, Registry, sessionOptions ?? new SessionOptions
        {
            Timing = ProtocolTiming.Fast,
            ListenForIsoBroadcast = ecuOptions?.BroadcastIso ?? false,
        });
        return (session, transport);
    }

    [Theory]
    [MemberData(nameof(AutoDetectFamilies))]
    public void AutoDetectsEveryFamilyFromTheIsoBroadcast(string id)
    {
        var (session, transport) = Create(id, new SimulatedEcuOptions { BroadcastIso = true });
        using (session)
        {
            var c = session.Connect(null, TestContext.Current.CancellationToken);
            Assert.Equal(id, c.Definition.Id);
            Assert.True(c.IsoFromBroadcast);
            Assert.Equal(transport.Ecu.Iso, c.Iso);
            Assert.Equal(transport.Ecu.Codric, c.Codric);
            Assert.NotNull(c.VehicleModel);
            Assert.True(transport.Ecu.IsInitialized);
        }
    }

    [Theory]
    [InlineData("IAW-8F/18F")]
    [InlineData("IAW-8F.68")]
    [InlineData("IAW-18FD/1G7")]
    public void AutoDetectsByReadingTheIsoCodeWhenThereIsNoBroadcast(string id)
    {
        var (session, transport) = Create(id);
        using (session)
        {
            var c = session.Connect(null, TestContext.Current.CancellationToken);
            Assert.Equal(id, c.Definition.Id);
            Assert.False(c.IsoFromBroadcast);
            Assert.Equal(transport.Ecu.Iso, c.Iso);
        }
    }

    [Fact]
    public void Iaw16fWithoutBroadcastFallsBackLikeIes2()
    {
        // IAW-16F answers 0x2A with a status byte instead of the 0x55 sync.
        var (session, transport) = Create("IAW-6F/16F");
        transport.Ecu.Registers[0x2A] = 0x04;
        using (session)
        {
            var c = session.Connect(null, TestContext.Current.CancellationToken);
            Assert.Equal("IAW-6F/16F", c.Definition.Id);
            Assert.Equal("55D085??????", c.Iso);
            Assert.Null(c.VehicleModel);
        }
    }

    [Fact]
    public void FallbackIsoIsNotTrustedWhenTheCodricBelongsToAnotherFamily()
    {
        // An IAW-04K.P8 polled with the standard profile does not answer 0x55 either;
        // IES2 would have shown it as an IAW-16F and decoded the wrong registers.
        var (session, _) = Create("IAW-04K.P8");
        using (session)
            Assert.Throws<UnrecognizedEcuException>(() => session.Connect(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Iaw04kCanBeSelectedManuallyWithoutBroadcast()
    {
        var (session, transport) = Create("IAW-04K.P8");
        using (session)
        {
            var c = session.Connect("IAW-04K.P8", TestContext.Current.CancellationToken);
            Assert.Equal(transport.Ecu.Iso, c.Iso);
            Assert.Equal(IawLink.CommunicationBaudRate, transport.BaudRate);
        }
    }

    [Fact]
    public void UnknownEcuIsReported()
    {
        var (session, transport) = Create("IAW-8F/18F", new SimulatedEcuOptions { Iso = "55AAAA000000" });
        using (session)
        {
            var ex = Assert.Throws<UnrecognizedEcuException>(() => session.Connect(null, TestContext.Current.CancellationToken));
            Assert.Equal("55AAAA000000", ex.Iso);
        }
    }

    [Fact]
    public void ImmobilizerIsDetectedFromTheCodeProbe()
    {
        var (with, _) = Create("IAW-6F/16F", new SimulatedEcuOptions { HasImmobilizer = true });
        var (without, _) = Create("IAW-6F/16F", new SimulatedEcuOptions { HasImmobilizer = false });
        using (with)
        using (without)
        {
            Assert.True(with.Connect("IAW-6F/16F", TestContext.Current.CancellationToken).HasImmobilizer);
            var c = without.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            Assert.False(c.HasImmobilizer);
            Assert.EndsWith(" ECOL", c.EcuDisplayName);
        }
    }

    [Theory]
    [InlineData("IAW-6F/16F")]
    [InlineData("IAW-8F/18F")]
    [InlineData("IAW-04K.P8")]
    public void PollingReadsLiveValues(string id)
    {
        var (session, transport) = Create(id);
        transport.Ecu.SetValue("PERIODE", 3000);
        transport.Ecu.SetValue("MT_EAU_L", 90);
        using (session)
        {
            session.Connect(id, TestContext.Current.CancellationToken);
            var rpm = session.GetParameter("PERIODE");
            var water = session.GetParameter("MT_EAU_L");
            session.PollOnce(new PollRequest { Parameters = [rpm, water] });
            Assert.Equal(3000, (double)rpm.Decode(session.Buffer)!.Value, 0);
            Assert.InRange((double)water.Decode(session.Buffer)!.Value, 89, 91);
            Assert.True(rpm.IsValid(session.Buffer));
        }
    }

    [Fact]
    public void ErrorsAreReadAndDecoded()
    {
        var (session, transport) = Create("IAW-8F/18F", new SimulatedEcuOptions { HasImmobilizer = true });
        transport.Ecu.SetError("ErrECT", active: true, stored: true, highReason: true);
        transport.Ecu.SetError("ErrStartDis", active: true, immobilizer: true);
        using (session)
        {
            session.Connect(null, TestContext.Current.CancellationToken);
            session.PollOnce(new PollRequest { Errors = true });
            var ect = Assert.Single(session.EngineErrors(), e => e.IsPresent);
            Assert.Equal("ErrECT", ect.Definition.NameKey);
            Assert.True(ect.IsActive);
            Assert.True(ect.IsStored);
            Assert.Equal(Core.Localization.Localizer.Get("ShortToGND"), ect.Reason);
            var immo = Assert.Single(session.ImmobilizerErrors(), e => e.IsPresent);
            Assert.Equal("FIAT CODE", immo.Reason);
        }
    }

    [Fact]
    public void ErrorsAreReadEveryFifthCycle()
    {
        var (session, transport) = Create("IAW-8F/18F");
        using (session)
        {
            session.Connect(null, TestContext.Current.CancellationToken);
            var reads = 0;
            session.ErrorsUpdated += _ => reads++;
            for (var i = 0; i < 10; i++)
                session.PollOnce(new PollRequest { Errors = true });
            Assert.Equal(2, reads);
        }
    }

    [Fact]
    public void ActiveTestPassesAndReturnsToPassiveMode()
    {
        var (session, transport) = Create("IAW-6F/16F", sessionOptions: new SessionOptions { Timing = ProtocolTiming.Fast, ListenForIsoBroadcast = false });
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            var pump = session.Definition.ActiveTests[0];
            var result = session.RunActiveTest(pump, TestContext.Current.CancellationToken);
            Assert.Equal(ActiveOutcome.Passed, result.Outcome);
            Assert.False(transport.Ecu.IsInActiveMode);
            Assert.Equal([pump.Requests[0], IawLink.ExitActiveMode], transport.Ecu.ActiveLog);
        }
    }

    [Fact]
    public void FailedTestResultIsReported()
    {
        var (session, _) = Create("IAW-6F/16F", new SimulatedEcuOptions { TestResult = 0x01 });
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            var result = session.RunActiveTest(session.Definition.ActiveTests[1], TestContext.Current.CancellationToken);
            Assert.Equal(ActiveOutcome.Failed, result.Outcome);
            Assert.Equal(0x01, result.ResultCode);
        }
    }

    [Fact]
    public void ClearCodesErasesStoredErrors()
    {
        var (session, transport) = Create("IAW-6F/16F");
        transport.Ecu.SetError("ErrTPS", stored: true);
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            session.PollOnce(new PollRequest { Errors = true });
            Assert.Contains(session.EngineErrors(), e => e.IsPresent);
            Assert.Equal(ActiveOutcome.Passed, session.ClearCodes(TestContext.Current.CancellationToken).Outcome);
            session.PollOnce(new PollRequest { Errors = true, ErrorEveryCycles = 1 });
            Assert.DoesNotContain(session.EngineErrors(), e => e.IsPresent);
        }
    }

    [Fact]
    public void ToggleAdjustmentSendsItsCodes()
    {
        var (session, transport) = Create("IAW-6F/16F");
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            var reset = session.Definition.Adjustments.Single(a => a.NameKey == "TrimReset");
            Assert.Equal(ActiveOutcome.Passed, session.RunAdjustment(reset).Outcome);
            Assert.Equal([0x89, 0x89, 0xFF], transport.Ecu.ActiveLog);
        }
    }

    [Fact]
    public void OneByteAdjustmentWritesTheChosenValue()
    {
        var (session, transport) = Create("IAW-6F/16F");
        transport.Ecu.Registers[0x0F] = 120;
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            var trim = session.Definition.Adjustments.Single(a => a.NameKey == "TrimSet");
            byte seen = 0;
            var result = session.RunAdjustment(trim, current =>
            {
                seen = current;
                return [(byte)(current + 8)];
            });
            Assert.Equal(ActiveOutcome.Passed, result.Outcome);
            Assert.Equal(120, seen);
            Assert.Equal(128, transport.Ecu.Registers[0x0F]);
        }
    }

    [Fact]
    public void ActiveCommandsAreRefusedInReadOnlyMode()
    {
        var (session, transport) = Create("IAW-6F/16F", sessionOptions: new SessionOptions
        {
            Timing = ProtocolTiming.Fast,
            ListenForIsoBroadcast = false,
            ActiveCommands = ActiveCommandPolicy.Disabled,
        });
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            Assert.Throws<ActiveCommandNotAllowedException>(() => session.ClearCodes(TestContext.Current.CancellationToken));
            Assert.Empty(transport.Ecu.ActiveLog);
        }
    }

    [Fact]
    public void ActiveCommandsAreRefusedForUnverifiedDefinitions()
    {
        var builtIn = Registry.Get("IAW-6F/16F");
        var json = System.Text.Json.JsonSerializer.Serialize(builtIn, DefinitionLoader.JsonOptions)
            .Replace("\"verified\": true", "\"verified\": false");
        var unverified = DefinitionLoader.Parse(json, "community.json");
        var registry = new DefinitionRegistry([unverified]);
        var transport = new SimulatorTransport(new SimulatedEcu(unverified));
        using var session = new DiagnosticSession(transport, registry, new SessionOptions { Timing = ProtocolTiming.Fast, ListenForIsoBroadcast = false });
        session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
        var ex = Assert.Throws<ActiveCommandNotAllowedException>(() => session.RunActiveTest(unverified.ActiveTests[0], TestContext.Current.CancellationToken));
        Assert.Contains("unverified", ex.Message);
    }

    [Fact]
    public void LostConnectionIsDetected()
    {
        var (session, transport) = Create("IAW-6F/16F");
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            transport.EcuConnected = false;
            var request = new PollRequest { Parameters = [.. session.Parameters.Take(3)] };
            Assert.Throws<CommunicationLostException>(() => session.PollOnce(request));
            Assert.Equal("#ERR", session.Parameters[0].Format(session.Buffer));
        }
    }

    [Fact]
    public void WorksWithAdaptersWithoutEchoAndWithLatency()
    {
        var ecu = new SimulatedEcu(Registry.Get("IAW-8F/18F"));
        var transport = new SimulatorTransport(ecu) { Echo = false, LatencyMs = 15 };
        using var session = new DiagnosticSession(transport, Registry, new SessionOptions
        {
            Timing = ProtocolTiming.Fast with { PollingReadTimeoutMs = 200 },
            AdapterEchoes = false,
            ListenForIsoBroadcast = false,
        });
        ecu.SetValue("M_VBATT", 12.5);
        session.Connect(null, TestContext.Current.CancellationToken);
        var battery = session.GetParameter("M_VBATT");
        session.PollOnce(new PollRequest { Parameters = [battery] });
        Assert.Equal(12.5m, battery.Decode(session.Buffer));
    }

    [Fact]
    public async Task PollingOnABackgroundThreadCanBeInterruptedByATest()
    {
        var (session, transport) = Create("IAW-6F/16F");
        using (session)
        {
            session.Connect("IAW-6F/16F", TestContext.Current.CancellationToken);
            using var stop = new CancellationTokenSource();
            var cycles = 0;
            session.ParametersUpdated += _ => Interlocked.Increment(ref cycles);
            var request = new PollRequest { Parameters = [.. session.Parameters] };
            var loop = Task.Run(() => session.RunPolling(() => request, stop.Token), TestContext.Current.CancellationToken);
            SpinWait.SpinUntil(() => Volatile.Read(ref cycles) > 2, 5000);
            var result = session.RunActiveTest(session.Definition.ActiveTests[0], TestContext.Current.CancellationToken);
            var after = Volatile.Read(ref cycles);
            SpinWait.SpinUntil(() => Volatile.Read(ref cycles) > after + 2, 5000);
            stop.Cancel();
            await loop;
            Assert.Equal(ActiveOutcome.Passed, result.Outcome);
            Assert.True(Volatile.Read(ref cycles) > after, "polling resumed after the test");
        }
    }

    [Fact]
    public void LineMonitorSeesEveryByte()
    {
        var (session, _) = Create("IAW-8F/18F");
        var events = new List<LineEvent>();
        session.Link.Monitor = events.Add;
        using (session)
        {
            session.Connect(null, TestContext.Current.CancellationToken);
            Assert.Equal([0x0F, 0xAA, 0xCC], events.Where(e => e.BaudRate == IawLink.InitBaudRate && e.Direction == LineDirection.Sent).Select(e => e.Value));
            Assert.Contains(events, e => e.Direction == LineDirection.Received && e.Value == 0x55);
        }
    }
}
