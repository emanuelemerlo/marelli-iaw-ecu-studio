using System.Diagnostics;
using System.Text;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Core.Protocol;

/// <summary>
/// The FIAT/Marelli IAW diagnostic protocol at byte level.
/// </summary>
/// <remarks>
/// <para>This is not ISO 9141-2 or KWP2000. After a slow init at 1200 baud
/// (bytes 0x0F, 0xAA, 0xCC) the ECU talks at 7680 baud. Every request is a
/// single byte; the K-Line adapter echoes it, then the ECU answers with a
/// single byte. Requests 0x80 and above are active commands (actuator
/// tests, clear codes, adjustments), valid only inside active diagnostic
/// mode opened with 0xAA and closed with 0xFF.</para>
/// <para>Ported from the static helpers of the IES2 <c>ecu</c> class.</para>
/// </remarks>
public sealed class IawLink
{
    public const int InitBaudRate = 1200;
    public const int CommunicationBaudRate = 7680;

    public const byte EnterActiveMode = 0xAA;
    public const byte ExitActiveMode = 0xFF;
    public const byte TestPassed = 0xFF;
    public const byte ImmobilizerProbe = 0x71;

    private static readonly byte[] SlowInitSequence = [0x0F, 0xAA, 0xCC];

    public IawLink(ITransport transport, ProtocolTiming? timing = null, bool adapterEchoes = true)
    {
        Transport = transport;
        Timing = timing ?? ProtocolTiming.Default;
        AdapterEchoes = adapterEchoes;
    }

    public ITransport Transport { get; }

    public ProtocolTiming Timing { get; }

    /// <summary>
    /// Whether the adapter returns every sent byte before the ECU answer.
    /// KKL cables do; some bridges do not.
    /// </summary>
    public bool AdapterEchoes { get; }

    /// <summary>Optional observer of every byte on the line, for capture files.</summary>
    public Action<LineEvent>? Monitor { get; set; }

    /// <summary>
    /// Sends one request and reads the answer. Returns false on timeout, like
    /// <c>ecu.Query</c> in IES2.
    /// </summary>
    public bool Query(byte request, out byte response)
    {
        Transport.DiscardInBuffer();
        Transport.Write([request]);
        Monitor?.Invoke(new LineEvent(LineDirection.Sent, request, Transport.BaudRate));
        try
        {
            if (AdapterEchoes)
                Transport.ReadByte();
            response = Transport.ReadByte();
            Monitor?.Invoke(new LineEvent(LineDirection.Received, response, Transport.BaudRate));
            return true;
        }
        catch (TimeoutException)
        {
            Monitor?.Invoke(new LineEvent(LineDirection.Timeout, request, Transport.BaudRate));
            response = 0;
            return false;
        }
    }

    /// <summary>Enters passive diagnostic mode (IES2 <c>InitPasvDiag</c>).</summary>
    public void InitPassiveDiagnostics(IdentificationProfile profile)
    {
        if (profile.SlowInit)
        {
            Transport.BaudRate = InitBaudRate;
            for (var i = 0; i < SlowInitSequence.Length; i++)
            {
                Transport.Write([SlowInitSequence[i]]);
                Monitor?.Invoke(new LineEvent(LineDirection.Sent, SlowInitSequence[i], InitBaudRate));
                Sleep(Timing.SlowInitDelaysMs[i]);
            }
            while (Transport.BytesToRead != 0)
                Transport.ReadByte();
        }
        Transport.BaudRate = CommunicationBaudRate;
    }

    /// <summary>
    /// Listens at 1200 baud for the ISO code that the ECU sends by itself at
    /// key-on (IES2 <c>bgwIsoWait</c>). Returns null if nothing arrives in time.
    /// </summary>
    public string? WaitForIsoBroadcast(CancellationToken cancellation)
    {
        Transport.ReadTimeout = Timing.IsoBroadcastReadTimeoutMs;
        Transport.BaudRate = InitBaudRate;
        var iso = new byte[6];
        var sync = false;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < Timing.IsoBroadcastWaitMs && !sync)
        {
            cancellation.ThrowIfCancellationRequested();
            if (Transport.BytesToRead == 0)
            {
                Sleep(Timing.IsoBroadcastPollMs);
                continue;
            }
            var b = Transport.ReadByte();
            Monitor?.Invoke(new LineEvent(LineDirection.Received, b, InitBaudRate));
            if (b == IdentificationProfile.SyncByte)
            {
                sync = true;
                iso[0] = b;
            }
        }
        if (!sync)
            return null;
        try
        {
            for (var i = 1; i < iso.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                iso[i] = Transport.ReadByte();
                Monitor?.Invoke(new LineEvent(LineDirection.Received, iso[i], InitBaudRate));
            }
        }
        catch (TimeoutException)
        {
            return null;
        }
        Sleep(Timing.IsoBroadcastSettleMs);
        return Convert.ToHexString(iso);
    }

    /// <summary>Reads the 6-byte ISO code as 12 hex digits (IES2 <c>ReadISO</c>).</summary>
    public string? ReadIso(IdentificationProfile profile)
    {
        var iso = new byte[profile.IsoRequests.Length];
        for (var i = 0; i < iso.Length; i++)
        {
            Sleep(Timing.IdentificationDelayMs);
            if (!Query(profile.IsoRequests[i], out iso[i]))
                return null;
            if (i == 0 && iso[0] != IdentificationProfile.SyncByte)
                return profile.IsoFallback;
        }
        return Convert.ToHexString(iso);
    }

    /// <summary>Reads the 10-character CODRIC spare part code (IES2 <c>ReadCODRIC</c>).</summary>
    public string? ReadCodric(IdentificationProfile profile)
    {
        var sb = new StringBuilder(profile.CodricRequests.Length);
        foreach (var request in profile.CodricRequests)
        {
            Sleep(Timing.IdentificationDelayMs);
            if (!Query(request, out var b))
                return null;
            sb.Append((char)b);
        }
        return sb.ToString();
    }

    /// <summary>True when the FIAT CODE immobiliser answers (IES2 <c>CheckCODE</c>).</summary>
    public bool ProbeImmobilizer() => Query(ImmobilizerProbe, out _);

    internal static void Sleep(int ms)
    {
        if (ms > 0)
            Thread.Sleep(ms);
    }
}

public enum LineDirection
{
    Sent,
    Received,
    Timeout,
}

/// <summary>One byte seen on the line.</summary>
public readonly record struct LineEvent(LineDirection Direction, byte Value, int BaudRate)
{
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
}
