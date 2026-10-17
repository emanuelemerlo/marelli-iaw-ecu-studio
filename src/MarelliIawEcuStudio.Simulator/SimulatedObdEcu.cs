using System.Text;
using MarelliIawEcuStudio.Core.KLine;
using MarelliIawEcuStudio.Core.Obd;

namespace MarelliIawEcuStudio.Simulator;

/// <summary>
/// An OBD-II ECU at message level: supported PIDs, live values, trouble
/// codes, VIN. Values are kept as the raw bytes of the answers.
/// </summary>
public sealed class SimulatedObdEcu
{
    private readonly Dictionary<byte, byte[]> _pids = [];
    private readonly HashSet<byte> _pendingOnce = [];

    public SimulatedObdEcu(byte address = 0x10)
    {
        Address = address;
        // A warm engine at idle.
        SetRaw(0x01, 0x00, 0x07, 0x65, 0x00);
        SetValue("LOAD", 22);
        SetValue("ECT", 88);
        SetValue("STFT1", 1.6);
        SetValue("LTFT1", -3.1);
        SetValue("MAP", 34);
        SetValue("RPM", 850);
        SetValue("VSS", 0);
        SetValue("SPARKADV", 12);
        SetValue("IAT", 31);
        SetValue("TP", 3.9);
        SetRaw(0x14, 0x5A, 0xFF);
        SetRaw(0x15, 0x8C, 0xFF);
        SetValue("RUNTM", 640);
        SetValue("VPWR", 14.1);
        SetRaw(0x1C, 0x06); // EOBD
    }

    public byte Address { get; }

    public string? Vin { get; set; } = "ZFA18800000123456";

    public List<Dtc> StoredCodes { get; } = [];

    public List<Dtc> PendingCodes { get; } = [];

    /// <summary>The check engine light; cleared with the codes.</summary>
    public bool Mil { get; set; }

    /// <summary>Requests answered, for test assertions.</summary>
    public List<byte[]> Requests { get; } = [];

    public void SetRaw(byte pid, params byte[] data) => _pids[pid] = data;

    /// <summary>Sets a standard PID from its physical value (inverse of the J1979 formulas).</summary>
    public void SetValue(string id, double value)
    {
        var pid = ObdPids.Find(id) ?? throw new KeyNotFoundException(id);
        // Search the raw value whose decoding is closest: simple and exact for every formula of the table.
        var best = new byte[pid.Bytes];
        var bestError = double.MaxValue;
        var max = pid.Bytes == 1 ? 255 : 65535;
        for (var raw = 0; raw <= max; raw++)
        {
            var bytes = pid.Bytes == 1 ? new[] { (byte)raw } : [(byte)(raw >> 8), (byte)raw];
            var error = Math.Abs(pid.Decode(bytes) - value);
            if (error < bestError)
            {
                bestError = error;
                best = bytes;
            }
        }
        if (pid.Bytes == 2 && pid.Pid is >= 0x14 and <= 0x1B)
            best = [best[0], 0xFF];
        _pids[pid.Pid] = best;
    }

    public void Unsupport(byte pid) => _pids.Remove(pid);

    /// <summary>The next request with this service id first gets a "response pending" (7F sid 78).</summary>
    public void RespondPendingOnce(byte service) => _pendingOnce.Add(service);

    /// <summary>Answers one request; an empty list means no answer.</summary>
    public IReadOnlyList<byte[]> Handle(byte[] request, byte keyByte1, byte keyByte2)
    {
        Requests.Add(request);
        var answers = new List<byte[]>();
        if (_pendingOnce.Remove(request[0]))
            answers.Add([0x7F, request[0], 0x78]);
        answers.AddRange(Answer(request, keyByte1, keyByte2));
        return answers;
    }

    private IEnumerable<byte[]> Answer(byte[] r, byte kb1, byte kb2)
    {
        switch (r)
        {
            case [0x81, ..]:
                yield return [0xC1, kb1, kb2];
                break;
            case [0x3E, ..]:
                yield return [0x7E];
                break;
            case [0x01, var pid] when pid % 0x20 == 0:
                yield return [0x41, pid, .. SupportedBitmap(pid)];
                break;
            case [0x01, 0x01]:
                var status = _pids[0x01];
                yield return [0x41, 0x01, (byte)((Mil ? 0x80 : 0) | Math.Min(StoredCodes.Count, 0x7F)), .. status[1..]];
                break;
            case [0x01, var pid] when _pids.TryGetValue(pid, out var data):
                yield return [0x41, pid, .. data];
                break;
            case [0x03]:
            case [0x07]:
                var codes = r[0] == 0x03 ? StoredCodes : PendingCodes;
                var chunks = codes.Chunk(3).ToList();
                if (chunks.Count == 0)
                    chunks.Add([]);
                foreach (var chunk in chunks)
                {
                    var message = new List<byte> { (byte)(r[0] + 0x40) };
                    foreach (var c in chunk)
                    {
                        message.Add((byte)(c.Raw >> 8));
                        message.Add((byte)c.Raw);
                    }
                    while (message.Count < 7)
                        message.Add(0);
                    yield return [.. message];
                }
                break;
            case [0x04]:
                StoredCodes.Clear();
                PendingCodes.Clear();
                Mil = false;
                yield return [0x44];
                break;
            case [0x09, 0x02] when Vin is not null:
                var vin = new byte[20];
                Encoding.ASCII.GetBytes(Vin.PadLeft(17)[..17]).CopyTo(vin, 3);
                for (byte n = 1; n <= 5; n++)
                    yield return [0x49, 0x02, n, .. vin.AsSpan((n - 1) * 4, 4)];
                break;
        }
        // Anything else: OBD ECUs on the K-Line do not answer unsupported requests.
    }

    private byte[] SupportedBitmap(byte block)
    {
        uint bits = 0;
        foreach (var pid in _pids.Keys.Where(p => p > block && p <= block + 0x20))
            bits |= 1u << (31 - (pid - block - 1));
        if (_pids.Keys.Any(p => p > block + 0x20))
            bits |= 1; // the next block exists
        return [(byte)(bits >> 24), (byte)(bits >> 16), (byte)(bits >> 8), (byte)bits];
    }

    /// <summary>Encodes the answers as they appear on the line.</summary>
    internal IEnumerable<byte> Encode(KLineProtocol protocol, IEnumerable<byte[]> answers) =>
        answers.SelectMany(a => KLineFraming.EncodeResponse(protocol, Address, a));
}
