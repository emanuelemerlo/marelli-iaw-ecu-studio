using System.Text;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Protocol;

namespace MarelliIawEcuStudio.Simulator;

public sealed record SimulatedEcuOptions
{
    /// <summary>ISO code; defaults to the first vehicle of the definition.</summary>
    public string? Iso { get; init; }

    /// <summary>CODRIC; defaults to a code built from the definition's CODRIC prefix.</summary>
    public string? Codric { get; init; }

    /// <summary>Whether a FIAT CODE immobiliser answers (requests 0x71-0x73).</summary>
    public bool? HasImmobilizer { get; init; }

    /// <summary>Send the ISO code at 1200 baud when the line opens, as at key-on.</summary>
    public bool BroadcastIso { get; init; }

    /// <summary>Delay before the broadcast starts.</summary>
    public int BroadcastDelayMs { get; init; }

    /// <summary>Time an actuator test takes before its result byte arrives.</summary>
    public int TestDurationMs { get; init; } = 5;

    /// <summary>Result byte of actuator tests (0xFF = passed).</summary>
    public byte TestResult { get; init; } = IawLink.TestPassed;

    /// <summary>Probability (0-1) that the ECU does not answer a request.</summary>
    public double DropProbability { get; init; }

    /// <summary>Probability (0-1) that an answer is replaced by a random byte.</summary>
    public double CorruptProbability { get; init; }

    public int Seed { get; init; } = 1;
}

/// <summary>Bytes the ECU sends in answer to a host byte.</summary>
public readonly record struct EcuEmission(byte Value, int DelayMs);

/// <summary>
/// A model of an IAW ECU at protocol level: slow init, passive mode register
/// reads, identification codes, active mode with tests and adjustments. All
/// behaviour is derived from an <see cref="EcuDefinition"/>.
/// </summary>
public sealed class SimulatedEcu
{
    private enum Mode
    {
        Idle,
        Passive,
        Active,
    }

    private readonly IdentificationProfile _profile;
    private readonly Random _random;
    private readonly Dictionary<byte, ActiveTestDefinition> _tests = [];
    private readonly HashSet<byte> _adjustmentCodes = [];
    private readonly Dictionary<byte, AdjustmentDefinition> _oneByteAdjustments = [];
    private readonly Dictionary<string, ParameterDecoder> _parameters;
    private Mode _mode;
    private int _initStep;
    private AdjustmentDefinition? _pendingValue;

    public SimulatedEcu(EcuDefinition definition, SimulatedEcuOptions? options = null)
    {
        Definition = definition;
        Options = options ?? new SimulatedEcuOptions();
        _profile = IdentificationProfile.Get(definition.Protocol.Identification);
        _random = new Random(Options.Seed);
        Iso = Options.Iso ?? definition.Vehicles.FirstOrDefault(v => !v.Iso.Contains('?'))?.Iso ?? BuildIso(definition);
        Codric = Options.Codric ?? BuildCodric(definition);
        HasImmobilizer = Options.HasImmobilizer ?? definition.Errors.Immobilizer.Items.Count > 0;
        _parameters = definition.Parameters.ToDictionary(p => p.Id, p => new ParameterDecoder(p, definition.Tables), StringComparer.OrdinalIgnoreCase);

        foreach (var t in definition.ActiveTests.Append(definition.ClearCodes).OfType<ActiveTestDefinition>())
            _tests.TryAdd(t.Requests[^1], t);
        foreach (var a in definition.Adjustments)
        {
            foreach (var c in a.PreSet.Concat(a.PostSet ?? []))
                _adjustmentCodes.Add(c);
            if (a.Type == "onebyte" && a.PreSet.Length > 0)
                _oneByteAdjustments[a.PreSet[^1]] = a;
        }

        WriteIdentification();
        Reset();
    }

    public EcuDefinition Definition { get; }

    public SimulatedEcuOptions Options { get; }

    public string Iso { get; }

    public string Codric { get; }

    public bool HasImmobilizer { get; set; }

    /// <summary>ECU memory as seen through single-byte requests.</summary>
    public RegisterBuffer Registers { get; } = new();

    /// <summary>Codes received in active mode, for test assertions.</summary>
    public List<byte> ActiveLog { get; } = [];

    public bool IsInActiveMode => _mode == Mode.Active;

    public bool IsInitialized => _mode != Mode.Idle;

    /// <summary>Back to power-on state: diagnostic mode must be entered again.</summary>
    public void Reset()
    {
        _mode = _profile.SlowInit ? Mode.Idle : Mode.Passive;
        _initStep = 0;
        _pendingValue = null;
    }

    /// <summary>Bytes sent spontaneously at key-on (the ISO code).</summary>
    public IEnumerable<EcuEmission> KeyOnBroadcast()
    {
        if (!Options.BroadcastIso)
            yield break;
        var bytes = Convert.FromHexString(Iso);
        for (var i = 0; i < bytes.Length; i++)
            yield return new EcuEmission(bytes[i], i == 0 ? Options.BroadcastDelayMs : 0);
    }

    /// <summary>Processes one byte from the tester and returns the ECU answer, if any.</summary>
    public IEnumerable<EcuEmission> Receive(byte value, int baudRate)
    {
        if (_mode == Mode.Idle)
        {
            if (baudRate == IawLink.InitBaudRate)
                AdvanceInit(value);
            return [];
        }
        if (baudRate != IawLink.CommunicationBaudRate)
            return [];
        var answer = _mode == Mode.Active ? ReceiveActive(value) : ReceivePassive(value);
        return answer.Select(Fault).OfType<EcuEmission>().ToList();
    }

    private void AdvanceInit(byte value)
    {
        _initStep = (_initStep, value) switch
        {
            (_, 0x0F) => 1,
            (1, 0xAA) => 2,
            (2, 0xCC) => 3,
            _ => 0,
        };
        if (_initStep == 3)
            _mode = Mode.Passive;
    }

    private IEnumerable<EcuEmission> ReceivePassive(byte request)
    {
        if (request == IawLink.EnterActiveMode)
        {
            _mode = Mode.Active;
            return [new EcuEmission(request, 0)];
        }
        if (request is >= 0x71 and <= 0x73 && !HasImmobilizer)
            return [];
        return [new EcuEmission(Registers[request], 0)];
    }

    private IEnumerable<EcuEmission> ReceiveActive(byte code)
    {
        ActiveLog.Add(code);
        if (_pendingValue is not null && code != IawLink.ExitActiveMode)
        {
            Registers[_pendingValue.StatusRequest] = code;
            _pendingValue = null;
            return [new EcuEmission(code, 0)];
        }
        if (code == IawLink.ExitActiveMode)
        {
            _mode = Mode.Passive;
            _pendingValue = null;
            return [new EcuEmission(IawLink.ExitActiveMode, 0)];
        }
        if (_tests.TryGetValue(code, out var test))
        {
            if (test == Definition.ClearCodes && Options.TestResult == IawLink.TestPassed)
                ClearErrors();
            return [new EcuEmission(code, 0), new EcuEmission(Options.TestResult, Options.TestDurationMs)];
        }
        if (_adjustmentCodes.Contains(code))
        {
            if (_oneByteAdjustments.TryGetValue(code, out var adjustment))
                _pendingValue = adjustment;
            return [new EcuEmission(code, 0)];
        }
        return [];
    }

    private EcuEmission? Fault(EcuEmission e)
    {
        if (Options.DropProbability > 0 && _random.NextDouble() < Options.DropProbability)
            return null;
        if (Options.CorruptProbability > 0 && _random.NextDouble() < Options.CorruptProbability)
            return e with { Value = (byte)_random.Next(256) };
        return e;
    }

    /// <summary>Sets a parameter to the raw value whose decoding is closest to <paramref name="value"/>.</summary>
    public void SetValue(string parameterId, double value)
    {
        var p = _parameters.TryGetValue(parameterId, out var found)
            ? found
            : throw new KeyNotFoundException($"{Definition.Id} has no parameter '{parameterId}'");
        var spec = p.Definition.Decode;
        var r = p.Definition.Requests;
        if (spec.Raw == "bit")
        {
            var on = (value != 0) ^ spec.Invert;
            var mask = (byte)(1 << spec.Bit);
            Registers[r[0]] = on ? (byte)(Registers[r[0]] | mask) : (byte)(Registers[r[0]] & ~mask);
            return;
        }
        var wide = r.Length == 2;
        var best = 0;
        var bestError = double.MaxValue;
        for (var raw = 0; raw < (wide ? 65536 : 256); raw++)
        {
            WriteRaw(r, raw, wide);
            if (p.Decode(Registers) is not { } decoded)
                continue;
            var error = Math.Abs((double)decoded - value);
            if (error < bestError)
            {
                bestError = error;
                best = raw;
            }
        }
        WriteRaw(r, best, wide);
    }

    /// <summary>Current decoded value of a parameter.</summary>
    public decimal? GetValue(string parameterId) => _parameters[parameterId].Decode(Registers);

    private void WriteRaw(byte[] requests, int raw, bool wide)
    {
        if (wide)
        {
            Registers[requests[0]] = (byte)(raw >> 8);
            Registers[requests[1]] = (byte)raw;
        }
        else
        {
            Registers[requests[0]] = (byte)raw;
        }
    }

    /// <summary>Sets the flags of an error, identified by its name key or name.</summary>
    public void SetError(string nameKeyOrName, bool active = false, bool stored = false, bool verified = false, bool highReason = false, bool immobilizer = false)
    {
        var group = immobilizer ? Definition.Errors.Immobilizer : Definition.Errors.Engine;
        var e = group.Items.FirstOrDefault(i => string.Equals(i.NameKey ?? i.Name, nameKeyOrName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"{Definition.Id} has no error '{nameKeyOrName}'");
        SetBit(e.Active, e.Bit, active);
        SetBit(e.Stored, e.Bit, stored);
        if (e.Verified != 0)
            SetBit(e.Verified, e.Bit, verified);
        if (e.Ext.Active != 0 || e.Ext.Stored != 0)
        {
            SetBit(e.Ext.Active, e.Ext.Bit, highReason);
            SetBit(e.Ext.Stored, e.Ext.Bit, highReason);
            if (e.Ext.Verified != 0)
                SetBit(e.Ext.Verified, e.Ext.Bit, highReason);
        }
    }

    private void SetBit(byte register, int bit, bool on)
    {
        var mask = (byte)(1 << bit);
        Registers[register] = on ? (byte)(Registers[register] | mask) : (byte)(Registers[register] & ~mask);
    }

    private void ClearErrors()
    {
        foreach (var group in new[] { Definition.Errors.Engine, Definition.Errors.Immobilizer })
            foreach (var e in group.Items)
                foreach (var reg in new[] { e.Active, e.Verified, e.Stored, e.Ext.Active, e.Ext.Verified, e.Ext.Stored })
                    if (reg != 0)
                        Registers[reg] = 0;
    }

    /// <summary>
    /// Puts ISO and CODRIC in the identification registers, unless the family
    /// uses those registers for data (IAW-16F answers 0x2A-0x2F with status
    /// bytes, which is why IES2 falls back to <c>55D085??????</c>).
    /// </summary>
    private void WriteIdentification()
    {
        var used = new HashSet<byte>(Definition.Parameters.SelectMany(p => p.Requests)
            .Concat(Definition.Errors.Engine.Requests).Concat(Definition.Errors.Immobilizer.Requests));
        if (!_profile.IsoRequests.Any(used.Contains))
        {
            var iso = Convert.FromHexString(Iso.Replace('?', '0'));
            for (var i = 0; i < iso.Length; i++)
                Registers[_profile.IsoRequests[i]] = iso[i];
        }
        if (!_profile.CodricRequests.Any(used.Contains))
        {
            var codric = Encoding.ASCII.GetBytes(Codric.PadRight(10, '0'));
            for (var i = 0; i < _profile.CodricRequests.Length; i++)
                Registers[_profile.CodricRequests[i]] = codric[i];
        }
    }

    private static string BuildIso(EcuDefinition d)
    {
        var m = d.Detection.IsoMatches.FirstOrDefault();
        var iso = "55" + new string('0', 10);
        return m is null ? iso : iso[..m.Offset] + m.Value + iso[(m.Offset + m.Value.Length)..];
    }

    private static string BuildCodric(EcuDefinition d)
    {
        var prefix = d.Detection.CodricMatches.FirstOrDefault()?.Value ?? "61600";
        return (prefix + "0000000000")[..10];
    }
}
