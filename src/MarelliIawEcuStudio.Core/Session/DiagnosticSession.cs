using System.Diagnostics;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Core.Session;

/// <summary>What the session may send in active diagnostic mode.</summary>
public enum ActiveCommandPolicy
{
    /// <summary>Read only: actuator tests, clear codes and adjustments are refused.</summary>
    Disabled,

    /// <summary>Allowed for definitions marked <c>verified</c> only.</summary>
    VerifiedOnly,

    /// <summary>Allowed for every definition.</summary>
    All,
}

public sealed record SessionOptions
{
    public ProtocolTiming Timing { get; init; } = ProtocolTiming.Default;

    /// <summary>KKL cables echo every byte; set false for adapters that do not.</summary>
    public bool AdapterEchoes { get; init; } = true;

    /// <summary>Listen for the ISO code broadcast at key-on before initialising (as IES2 does).</summary>
    public bool ListenForIsoBroadcast { get; init; } = true;

    public ActiveCommandPolicy ActiveCommands { get; init; } = ActiveCommandPolicy.VerifiedOnly;
}

/// <summary>Identification of the connected ECU.</summary>
public sealed record ConnectionInfo(EcuDefinition Definition, string? Iso, string? Codric, bool HasImmobilizer, string? VehicleModel, bool IsoFromBroadcast)
{
    /// <summary>ISO code as shown by IES2: <c>55-D0-85-02-94-40</c>.</summary>
    public string FormattedIso => Iso is { Length: 12 } ? string.Join('-', Enumerable.Range(0, 6).Select(i => Iso.Substring(i * 2, 2))) : Iso ?? "-";

    /// <summary>CODRIC as shown by IES2: <c>61602.063.01</c>.</summary>
    public string FormattedCodric => Codric is { Length: 10 } ? $"{Codric[..5]}.{Codric[5..8]}.{Codric[8..]}" : Codric ?? "-";

    /// <summary>Family name, with the "ECOL" suffix IES2 shows for units without immobiliser.</summary>
    public string EcuDisplayName => (Definition.LongName ?? Definition.Name) + (HasImmobilizer ? "" : " ECOL");
}

public enum ActiveOutcome
{
    Passed,
    Failed,
    Timeout,
    InitFailed,
    Cancelled,
}

/// <summary>Result of an actuator test, clear codes or adjustment.</summary>
/// <param name="ResultCode">Raw code as in IES2: 0xFF passed, 0xBB timeout, 0xAA init failed, other failed.</param>
public sealed record ActiveResult(ActiveOutcome Outcome, byte ResultCode)
{
    public static ActiveOutcome FromCode(byte code) => code switch
    {
        IawLink.TestPassed => ActiveOutcome.Passed,
        0xBB => ActiveOutcome.Timeout,
        0xAA => ActiveOutcome.InitFailed,
        _ => ActiveOutcome.Failed,
    };
}

public sealed class UnrecognizedEcuException(string? iso, string? codric)
    : Exception($"Unrecognised ECU (ISO {iso ?? "none"}, CODRIC {codric ?? "none"})")
{
    public string? Iso { get; } = iso;

    public string? Codric { get; } = codric;
}

public sealed class CommunicationLostException() : Exception("Communication with the ECU was lost.");

public sealed class ActiveCommandNotAllowedException(string message) : Exception(message);

/// <summary>What one polling cycle should read.</summary>
public sealed record PollRequest
{
    /// <summary>Parameters to read; their request bytes are polled in ascending order.</summary>
    public IReadOnlyCollection<ParameterDecoder> Parameters { get; init; } = [];

    /// <summary>Read engine and immobiliser errors.</summary>
    public bool Errors { get; init; }

    /// <summary>Errors are read every N-th cycle (IES2: every 5th).</summary>
    public int ErrorEveryCycles { get; init; } = 5;
}

/// <summary>
/// A connection to one ECU: identification, live data polling, error reading
/// and active commands. Ported from the background workers of the IES2 main
/// form, without any UI dependency.
/// </summary>
/// <remarks>
/// All line access is serialised with a lock, so an active test can be
/// started while a polling loop runs on another thread: it waits for the
/// current cycle and the loop continues afterwards.
/// </remarks>
public sealed class DiagnosticSession : IDisposable
{
    private readonly Lock _line = new();
    private readonly DefinitionRegistry _registry;
    private int _cycle;

    public DiagnosticSession(ITransport transport, DefinitionRegistry registry, SessionOptions? options = null)
    {
        Options = options ?? new SessionOptions();
        _registry = registry;
        Link = new IawLink(transport, Options.Timing, Options.AdapterEchoes);
    }

    public SessionOptions Options { get; }

    public IawLink Link { get; }

    public ITransport Transport => Link.Transport;

    public RegisterBuffer Buffer { get; } = new();

    public ConnectionInfo? Connection { get; private set; }

    public EcuDefinition Definition => Connection?.Definition ?? throw new InvalidOperationException("Not connected.");

    public IReadOnlyList<ParameterDecoder> Parameters { get; private set; } = [];

    /// <summary>Raised after each polling cycle that read parameters.</summary>
    public event Action<DiagnosticSession>? ParametersUpdated;

    /// <summary>Raised after each polling cycle that read errors.</summary>
    public event Action<DiagnosticSession>? ErrorsUpdated;

    private ProtocolTiming Timing => Options.Timing;

    /// <summary>
    /// Opens the line, identifies the ECU and enters passive diagnostic mode.
    /// </summary>
    /// <param name="ecuId">Definition id, or null to detect the family automatically.</param>
    public ConnectionInfo Connect(string? ecuId, CancellationToken cancellation = default)
    {
        lock (_line)
        {
            if (!Transport.IsOpen)
                Transport.Open();
            Buffer.Clear();

            var iso = Options.ListenForIsoBroadcast ? Link.WaitForIsoBroadcast(cancellation) : null;
            var fromBroadcast = iso is not null;
            string? codric;
            EcuDefinition definition;

            if (ecuId is null)
            {
                // A family without slow init (IAW-04K.P8) is recognised from the broadcast alone.
                var direct = fromBroadcast
                    ? _registry.AutoDetectOrder.FirstOrDefault(d => !ProfileOf(d).SlowInit && MatchesIso(d, iso))
                    : null;
                if (direct is not null)
                {
                    definition = direct;
                    Link.InitPassiveDiagnostics(ProfileOf(definition));
                    codric = Link.ReadCodric(ProfileOf(definition));
                }
                else
                {
                    Link.InitPassiveDiagnostics(IdentificationProfile.Standard);
                    codric = Link.ReadCodric(IdentificationProfile.Standard);
                    if (!fromBroadcast)
                        iso = Link.ReadIso(IdentificationProfile.Standard);
                    // IES2 took any unit not answering the 0x55 sync for an IAW-16F, which
                    // also matched other families (an IAW-04K.P8 without broadcast). The
                    // fallback ISO is accepted only when the CODRIC confirms the family.
                    var fallback = !fromBroadcast && iso == IdentificationProfile.Standard.IsoFallback;
                    definition = _registry.AutoDetectOrder.FirstOrDefault(d => MatchesIso(d, iso) && (!fallback || MatchesCodric(d, codric)))
                        ?? throw new UnrecognizedEcuException(iso, codric);
                }
            }
            else
            {
                definition = _registry.Get(ecuId);
                var profile = ProfileOf(definition);
                Link.InitPassiveDiagnostics(profile);
                codric = Link.ReadCodric(profile);
                if (!fromBroadcast)
                    iso = Link.ReadIso(profile);
            }

            var hasImmobilizer = Link.ProbeImmobilizer();
            var vehicle = definition.Vehicles.FirstOrDefault(v => v.Iso == iso)?.Model;
            Parameters = [.. definition.Parameters.Select(p => new ParameterDecoder(p, definition.Tables))];
            _cycle = 0;
            Connection = new ConnectionInfo(definition, iso, codric, hasImmobilizer, vehicle, fromBroadcast);
            return Connection;
        }
    }

    /// <summary>Re-enters passive mode after a communication loss (IES2 F12).</summary>
    public void Reinitialize()
    {
        lock (_line)
            Link.InitPassiveDiagnostics(ProfileOf(Definition));
    }

    public void Disconnect()
    {
        lock (_line)
        {
            Connection = null;
            if (Transport.IsOpen)
                Transport.Close();
        }
    }

    public ParameterDecoder GetParameter(string id) =>
        Parameters.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw new KeyNotFoundException($"Parameter '{id}' is not defined for {Definition.Id}. Known: {string.Join(", ", Parameters.Select(p => p.Id))}");

    /// <summary>Decoded engine errors from the last read.</summary>
    public IReadOnlyList<ErrorState> EngineErrors() => ErrorDecoder.DecodeAll(Definition.Errors.Engine, Buffer);

    /// <summary>Decoded immobiliser errors from the last read (empty without immobiliser).</summary>
    public IReadOnlyList<ErrorState> ImmobilizerErrors() =>
        Connection?.HasImmobilizer == true ? ErrorDecoder.DecodeAll(Definition.Errors.Immobilizer, Buffer) : [];

    /// <summary>
    /// Runs one polling cycle. Throws <see cref="CommunicationLostException"/>
    /// when more than one request in a row failed, as IES2 did.
    /// </summary>
    public void PollOnce(PollRequest request)
    {
        lock (_line)
        {
            Transport.ReadTimeout = Timing.PollingReadTimeoutMs;
            var failures = 0;

            var registers = new SortedSet<byte>(request.Parameters.SelectMany(p => p.Requests));
            foreach (var register in registers)
            {
                failures = QueryInto(register, failures);
                IawLink.Sleep(Timing.PollingDelayMs);
            }
            var readParameters = registers.Count > 0;

            var readErrors = request.Errors && _cycle % Math.Max(1, request.ErrorEveryCycles) == 0;
            if (readErrors)
            {
                foreach (var register in Definition.Errors.Engine.Requests)
                {
                    failures = QueryInto(register, failures);
                    IawLink.Sleep(Timing.PollingDelayMs);
                }
                if (Connection!.HasImmobilizer)
                {
                    foreach (var register in Definition.Errors.Immobilizer.Requests)
                    {
                        failures = QueryInto(register, failures);
                        IawLink.Sleep(Timing.PollingDelayMs);
                    }
                }
            }
            _cycle++;

            if (readParameters)
                ParametersUpdated?.Invoke(this);
            if (readErrors)
                ErrorsUpdated?.Invoke(this);
            if (failures > 1)
                throw new CommunicationLostException();
            if (!readParameters && !readErrors)
                IawLink.Sleep(Timing.IdleDelayMs);
        }
    }

    /// <summary>Polls until cancelled. Run it on a background thread.</summary>
    public void RunPolling(Func<PollRequest> request, CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            PollOnce(request());
            Thread.Yield();
        }
    }

    private int QueryInto(byte register, int failures)
    {
        var ok = Link.Query(register, out var value);
        Buffer.Store(register, value, ok);
        return ok ? 0 : failures + 1;
    }

    /// <summary>Runs an actuator test (IES2 <c>bgwTest</c>).</summary>
    public ActiveResult RunActiveTest(ActiveTestDefinition test, CancellationToken cancellation = default)
    {
        EnsureActiveAllowed();
        lock (_line)
        {
            var code = RunActive(test, cancellation, out var cancelled);
            return new ActiveResult(cancelled ? ActiveOutcome.Cancelled : ActiveResult.FromCode(code), code);
        }
    }

    /// <summary>Clears stored error codes. The ignition must be on and the engine stopped.</summary>
    public ActiveResult ClearCodes(CancellationToken cancellation = default) =>
        RunActiveTest(Definition.ClearCodes ?? throw new InvalidOperationException($"{Definition.Id} has no clear codes command."), cancellation);

    private byte RunActive(ActiveTestDefinition test, CancellationToken cancellation, out bool cancelled)
    {
        cancelled = false;
        Transport.ReadTimeout = Timing.ActiveTestReadTimeoutMs;
        if (!Link.Query(IawLink.EnterActiveMode, out _))
            return 0xAA;

        foreach (var code in test.Requests)
        {
            IawLink.Sleep(Timing.ActiveCodeDelayBeforeMs);
            var ok = Link.Query(code, out var response);
            IawLink.Sleep(Timing.ActiveCodeDelayAfterMs);
            if (!ok || response != code)
            {
                ExitActive();
                return 0xBB;
            }
        }

        var limit = test.TimeoutSeconds * 1000L + Timing.ActiveResultGraceMs;
        var sw = Stopwatch.StartNew();
        while (Transport.BytesToRead == 0 && sw.ElapsedMilliseconds < limit)
        {
            if (cancellation.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }
            IawLink.Sleep(Timing.ActiveResultPollMs);
        }

        byte result;
        if (!cancelled && Transport.BytesToRead != 0)
        {
            result = Transport.ReadByte();
            IawLink.Sleep(Timing.ActiveResultPollMs);
        }
        else
        {
            Link.Query(IawLink.ExitActiveMode, out _); // abort the test
            result = 0xBB;
        }
        ExitActive();
        return result;
    }

    /// <summary>
    /// Runs an adjustment (IES2 <c>ExecAdjustment</c>). For <c>onebyte</c>
    /// adjustments, <paramref name="chooseValues"/> receives the current value
    /// and returns the bytes to send (IES2 asked the user with a slider).
    /// </summary>
    public ActiveResult RunAdjustment(AdjustmentDefinition adjustment, Func<byte, IEnumerable<byte>>? chooseValues = null)
    {
        EnsureActiveAllowed();
        if (adjustment.Type == "onebyte" && chooseValues is null)
            throw new ArgumentNullException(nameof(chooseValues), "A one-byte adjustment needs a value.");
        lock (_line)
        {
            Transport.ReadTimeout = Timing.AdjustmentReadTimeoutMs;
            byte status = 0;
            if (adjustment.StatusRequest != 0)
            {
                if (!Link.Query(adjustment.StatusRequest, out status))
                {
                    ExitActive();
                    return new ActiveResult(ActiveOutcome.InitFailed, 0xAA);
                }
            }
            if (!Link.Query(IawLink.EnterActiveMode, out _))
                return new ActiveResult(ActiveOutcome.InitFailed, 0xAA);

            foreach (var code in adjustment.PreSet)
            {
                IawLink.Sleep(Timing.ActiveCodeDelayBeforeMs);
                if (!Link.Query(code, out var response) || response != code)
                {
                    ExitActive();
                    ExitActive();
                    return new ActiveResult(ActiveOutcome.InitFailed, 0xAA);
                }
            }

            if (adjustment.Type == "onebyte")
            {
                foreach (var value in chooseValues!(status))
                    Link.Query(value, out _);
            }

            foreach (var code in adjustment.PostSet ?? [])
            {
                IawLink.Sleep(Timing.ActiveCodeDelayBeforeMs);
                if (!Link.Query(code, out var response) || response != code)
                {
                    ExitActive();
                    ExitActive();
                    return new ActiveResult(ActiveOutcome.Failed, 0x00);
                }
            }
            ExitActive();
            return new ActiveResult(ActiveOutcome.Passed, IawLink.TestPassed);
        }
    }

    private bool ExitActive() => Link.Query(IawLink.ExitActiveMode, out var r) && r == IawLink.ExitActiveMode;

    private void EnsureActiveAllowed()
    {
        var definition = Definition;
        switch (Options.ActiveCommands)
        {
            case ActiveCommandPolicy.Disabled:
                throw new ActiveCommandNotAllowedException("Active commands are disabled (read-only session).");
            case ActiveCommandPolicy.VerifiedOnly when !definition.Verified:
                throw new ActiveCommandNotAllowedException($"Active commands are disabled for the unverified definition '{definition.Id}'.");
        }
    }

    private static IdentificationProfile ProfileOf(EcuDefinition d) => IdentificationProfile.Get(d.Protocol.Identification);

    private static bool MatchesIso(EcuDefinition d, string? iso) => d.Detection.IsoMatches.Any(m => m.IsMatch(iso));

    private static bool MatchesCodric(EcuDefinition d, string? codric) =>
        d.Detection.CodricMatches.Count == 0 || d.Detection.CodricMatches.Any(m => m.IsMatch(codric));

    public void Dispose()
    {
        Disconnect();
        Transport.Dispose();
    }
}
