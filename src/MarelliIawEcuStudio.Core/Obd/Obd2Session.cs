using System.Text;
using MarelliIawEcuStudio.Core.KLine;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Core.Obd;

public sealed record Obd2Options
{
    public KLineTiming Timing { get; init; } = KLineTiming.Default;

    /// <summary>KKL cables echo every byte.</summary>
    public bool AdapterEchoes { get; init; } = true;

    /// <summary>Allow mode 04 (clear codes and freeze frames, resets the readiness monitors).</summary>
    public bool AllowClear { get; init; }
}

/// <summary>What the car answered at connection.</summary>
/// <param name="Ecus">Addresses of the ECUs that answer OBD requests (0x10 is usually the engine).</param>
/// <param name="SupportedPids">Mode 01 PIDs reported as supported by any ECU.</param>
public sealed record ObdConnectionInfo(KLineProtocol Protocol, string Keywords, IReadOnlyList<byte> Ecus, IReadOnlySet<byte> SupportedPids)
{
    public string ProtocolName => Protocol switch
    {
        KLineProtocol.Iso9141 => "ISO 9141-2",
        KLineProtocol.Kwp2000Slow => "ISO 14230-4 KWP2000 (5-baud init)",
        _ => "ISO 14230-4 KWP2000 (fast init)",
    };
}

/// <summary>One decoded value.</summary>
public sealed record ObdReading(ObdPid Pid, byte Ecu, double Value)
{
    public string Text => Pid.FormatValue(Value);
}

/// <summary>Mode 01 PID 01, modes 03 and 07.</summary>
/// <param name="Mil">The check engine light is on.</param>
/// <param name="ReportedCount">Number of stored codes according to the ECU (PID 01).</param>
public sealed record ObdDtcReport(bool Mil, int ReportedCount, IReadOnlyList<Dtc> Stored, IReadOnlyList<Dtc> Pending);

/// <summary>
/// OBD-II / EOBD diagnostics over the K-Line (ISO 9141-2 and ISO 14230-4),
/// for the FIAT, Lancia and Alfa Romeo cars of 2001 onwards, with the same
/// KKL cable as the IAW units. Read only unless <see cref="Obd2Options.AllowClear"/>.
/// </summary>
public sealed class Obd2Session : IDisposable
{
    private readonly Lock _line = new();

    public Obd2Session(ITransport transport, Obd2Options? options = null)
    {
        Options = options ?? new Obd2Options();
        Link = new KLineLink(transport, Options.Timing, Options.AdapterEchoes);
    }

    public Obd2Options Options { get; }

    public KLineLink Link { get; }

    public ObdConnectionInfo? Connection { get; private set; }

    /// <summary>The order tried by <see cref="Connect"/> without a protocol: the 5-baud init covers ISO 9141-2 and KWP2000 slow.</summary>
    public static IReadOnlyList<KLineProtocol> AutoOrder { get; } = [KLineProtocol.Iso9141, KLineProtocol.Kwp2000Fast];

    /// <summary>Wakes the ECUs up and reads which PIDs they support.</summary>
    /// <param name="protocol">Null tries the 5-baud init, then the fast init.</param>
    public ObdConnectionInfo Connect(KLineProtocol? protocol = null, CancellationToken cancellation = default)
    {
        lock (_line)
        {
            var errors = new List<string>();
            foreach (var p in protocol is { } only ? [only] : AutoOrder)
            {
                cancellation.ThrowIfCancellationRequested();
                try
                {
                    var init = Link.Init(p, cancellation: cancellation);
                    var supported = ReadSupportedPids();
                    Connection = new ObdConnectionInfo(init.Protocol, init.Keywords,
                        [.. supported.Keys.Concat(init.Responders).Distinct().Order()], supported.Values.SelectMany(s => s).ToHashSet());
                    return Connection;
                }
                catch (KLineException ex)
                {
                    errors.Add($"{p}: {ex.Message}");
                }
            }
            throw new KLineException("No OBD ECU answered. " + string.Join(" ", errors));
        }
    }

    /// <summary>Mode 01 PIDs 00, 20, 40...: a bitmap of 32 PIDs each, the last bit telling whether the next block exists.</summary>
    private Dictionary<byte, HashSet<byte>> ReadSupportedPids()
    {
        var result = new Dictionary<byte, HashSet<byte>>();
        for (var block = 0x00; block <= 0xC0; block += 0x20)
        {
            var frames = Link.Request([0x01, (byte)block]);
            var more = false;
            foreach (var f in frames.Where(f => f.Data.Length >= 6 && f.Data[0] == 0x41 && f.Data[1] == block))
            {
                if (!result.TryGetValue(f.Source, out var set))
                    result[f.Source] = set = [];
                var bits = (uint)((f.Data[2] << 24) | (f.Data[3] << 16) | (f.Data[4] << 8) | f.Data[5]);
                for (var i = 0; i < 32; i++)
                {
                    if ((bits & (1u << (31 - i))) != 0)
                        set.Add((byte)(block + i + 1));
                }
                more |= (bits & 1) != 0;
            }
            if (block == 0 && result.Count == 0)
                throw new KLineException("The ECU woke up but did not answer the supported-PIDs request (01 00).");
            if (!more)
                break;
        }
        return result;
    }

    private ObdConnectionInfo Connected => Connection ?? throw new InvalidOperationException("Not connected.");

    /// <summary>The known PIDs this car supports, in table order.</summary>
    public IReadOnlyList<ObdPid> SupportedKnownPids() => [.. ObdPids.All.Where(p => Connected.SupportedPids.Contains(p.Pid))];

    /// <summary>Reads PIDs one by one (the K-Line allows one per request), from every ECU that answers.</summary>
    public IReadOnlyList<ObdReading> Read(IEnumerable<ObdPid> pids)
    {
        _ = Connected;
        var readings = new List<ObdReading>();
        var failures = 0;
        lock (_line)
        {
            foreach (var pid in pids)
            {
                var frames = Link.Request([0x01, pid.Pid]);
                var answered = false;
                foreach (var f in frames.Where(f => f.Data.Length >= 2 + pid.Bytes && f.Data[0] == 0x41 && f.Data[1] == pid.Pid))
                {
                    readings.Add(new ObdReading(pid, f.Source, pid.Decode(f.Data[2..])));
                    answered = true;
                }
                failures = answered || frames.Count > 0 ? 0 : failures + 1;
                if (failures > 1)
                    throw new CommunicationLostException();
            }
        }
        return readings;
    }

    /// <summary>MIL state and stored and pending trouble codes.</summary>
    public ObdDtcReport ReadDtcs()
    {
        _ = Connected;
        lock (_line)
        {
            var status = Link.Request([0x01, 0x01]).FirstOrDefault(f => f.Data is [0x41, 0x01, _, ..]);
            var mil = status is not null && (status.Data[2] & 0x80) != 0;
            var count = status is null ? 0 : status.Data[2] & 0x7F;
            return new ObdDtcReport(mil, count, ReadCodes(0x03), ReadCodes(0x07));
        }
    }

    /// <summary>Modes 03 and 07: up to three codes per message, zero padded, as many messages as needed.</summary>
    private List<Dtc> ReadCodes(byte mode)
    {
        var codes = new List<Dtc>();
        foreach (var f in Link.Request([mode]).Where(f => f.Data.Length >= 1 && f.Data[0] == mode + 0x40))
        {
            for (var i = 1; i + 1 < f.Data.Length; i += 2)
            {
                var raw = (ushort)((f.Data[i] << 8) | f.Data[i + 1]);
                if (raw != 0)
                    codes.Add(new Dtc(raw));
            }
        }
        return [.. codes.Distinct()];
    }

    /// <summary>
    /// Mode 04: clears codes and freeze frames and resets the readiness
    /// monitors (the car then fails an emissions test until they complete).
    /// Ignition on, engine stopped.
    /// </summary>
    public bool ClearDtcs()
    {
        if (!Options.AllowClear)
            throw new ActiveCommandNotAllowedException("Clearing OBD codes is disabled (read-only session).");
        _ = Connected;
        lock (_line)
            return Link.Request([0x04]).Any(f => f.Data is [0x44, ..]);
    }

    /// <summary>Mode 09 PID 02: the 17-character VIN, or null when the car does not report it.</summary>
    public string? ReadVin() => ReadInfoString(0x02, 17);

    /// <summary>Mode 09 PID 04: calibration identification, or null.</summary>
    public string? ReadCalibrationId() => ReadInfoString(0x04, 16);

    /// <summary>Mode 09 answers on the K-Line: one message per 4 bytes, numbered, the first ones padded with zeros.</summary>
    private string? ReadInfoString(byte infoType, int length)
    {
        _ = Connected;
        lock (_line)
        {
            var parts = Link.Request([0x09, infoType])
                .Where(f => f.Data.Length >= 4 && f.Data[0] == 0x49 && f.Data[1] == infoType)
                .GroupBy(f => f.Source)
                .FirstOrDefault();
            if (parts is null)
                return null;
            var bytes = parts.OrderBy(f => f.Data[2]).SelectMany(f => f.Data[3..]).ToList();
            var text = Encoding.ASCII.GetString([.. bytes.Where(b => b is >= 0x20 and < 0x7F)]).Trim();
            return text.Length == 0 ? null : text.Length > length ? text[^length..] : text;
        }
    }

    /// <summary>Sends a request when the line has been quiet too long, so the ECU stays in diagnostic mode.</summary>
    public void KeepAlive()
    {
        lock (_line)
        {
            if (Connection is not null && Link.NeedsKeepAlive)
                Link.Request([0x01, 0x00]);
        }
    }

    public void Dispose() => Link.Transport.Dispose();
}
