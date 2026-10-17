using System.Diagnostics;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Core.KLine;

/// <summary>
/// Timing of the framed K-Line protocols (ISO 9141-2, ISO 14230). Names
/// follow the standards (W1-W5, P2-P4); the timeouts include a margin for
/// USB and network latency.
/// </summary>
public sealed record KLineTiming
{
    public const int BaudRate = 10400;

    /// <summary>Line high before an init (W5 / W0).</summary>
    public int IdleBeforeInitMs { get; init; } = 300;

    /// <summary>One bit of the 5-baud address.</summary>
    public int FiveBaudBitMs { get; init; } = 200;

    /// <summary>Wait for the 0x55 sync after the address (W1 is 60-300 ms).</summary>
    public int SyncTimeoutMs { get; init; } = 400;

    /// <summary>Wait for each keyword byte (W2, W3 up to 20 ms).</summary>
    public int KeywordTimeoutMs { get; init; } = 100;

    /// <summary>Pause before sending the inverted keyword 2 (W4 is 25-50 ms).</summary>
    public int W4Ms { get; init; } = 30;

    /// <summary>Fast init: line low (TiniL, 25 ms) and then high, before StartCommunication.</summary>
    public int FastInitLowMs { get; init; } = 25;

    public int FastInitHighMs { get; init; } = 25;

    /// <summary>Pause between two bytes sent by the tester (P4, 5-20 ms in ISO 9141-2).</summary>
    public int P4Ms { get; init; } = 5;

    /// <summary>Wait for the first byte of an answer (P2 is up to 50 ms).</summary>
    public int ResponseTimeoutMs { get; init; } = 300;

    /// <summary>
    /// Pause that ends a message. Must exceed the ECU inter-byte time (P1, up to
    /// 20 ms) and stay below the gap between two ECUs (P2, from 25 ms); set the
    /// FTDI latency timer to 1 ms so USB does not add its own pauses.
    /// </summary>
    public int FrameGapMs { get; init; } = 22;

    /// <summary>Silence that ends the answers of all ECUs to one request.</summary>
    public int EndOfResponseMs { get; init; } = 100;

    /// <summary>Longest wait while an ECU answers "response pending" (NRC 0x78, P2* up to 5 s).</summary>
    public int PendingTimeoutMs { get; init; } = 5000;

    /// <summary>Pause after an answer before the next request (P3, at least 55 ms).</summary>
    public int P3Ms { get; init; } = 60;

    /// <summary>A request is sent at least this often, or the ECU leaves the session (P3 max is 5 s).</summary>
    public int KeepAliveMs { get; init; } = 2000;

    public static KLineTiming Default { get; } = new();

    /// <summary>
    /// For the simulator: the 5-baud address takes 360 ms instead of 1.8 s,
    /// with bits long enough (±20 ms) to survive a busy test machine.
    /// </summary>
    public static KLineTiming Fast { get; } = new()
    {
        IdleBeforeInitMs = 0,
        FiveBaudBitMs = 40,
        SyncTimeoutMs = 200,
        KeywordTimeoutMs = 50,
        W4Ms = 0,
        FastInitLowMs = 5,
        FastInitHighMs = 5,
        P4Ms = 0,
        ResponseTimeoutMs = 100,
        EndOfResponseMs = 20,
        FrameGapMs = 8,
        PendingTimeoutMs = 500,
        P3Ms = 0,
    };
}

/// <summary>Result of a K-Line init.</summary>
/// <param name="Responders">Addresses of the ECUs that answered (known after the first request for ISO 9141-2).</param>
public sealed record KLineInitResult(KLineProtocol Protocol, byte KeyByte1, byte KeyByte2, IReadOnlyList<byte> Responders)
{
    public string Keywords => $"{KeyByte1:X2} {KeyByte2:X2}";
}

/// <summary>The ECU did not complete the init or a message was malformed.</summary>
public sealed class KLineException(string message) : IOException(message);

/// <summary>
/// A framed K-Line connection: 5-baud or fast init, then requests answered
/// by one or more ECUs. Works through any <see cref="ITransport"/> that also
/// implements <see cref="ILineControl"/> (for the init breaks): a KKL cable,
/// a network bridge, the simulator.
/// </summary>
public sealed class KLineLink
{
    private readonly ILineControl _line;
    private long _lastRequest;

    public KLineLink(ITransport transport, KLineTiming? timing = null, bool adapterEchoes = true)
    {
        Transport = transport;
        _line = transport as ILineControl
            ?? throw new NotSupportedException($"{transport.Name} cannot drive the line low, which the K-Line init needs.");
        Timing = timing ?? KLineTiming.Default;
        AdapterEchoes = adapterEchoes;
    }

    public ITransport Transport { get; }

    public KLineTiming Timing { get; }

    public bool AdapterEchoes { get; }

    /// <summary>Observers of every byte, as for <see cref="IawLink.Monitor"/>: line traces work the same way.</summary>
    public Action<LineEvent>? Monitor { get; set; }

    /// <summary>The protocol in use after <see cref="Init"/>.</summary>
    public KLineProtocol? Protocol { get; private set; }

    /// <summary>Target address of the requests (0x33, the OBD functional address, by default).</summary>
    public byte Target { get; private set; } = KLineFraming.ObdFunctionalAddress;

    /// <summary>
    /// Number of ECUs that answer each request; once known, reading stops as
    /// soon as they have all answered instead of waiting for silence.
    /// </summary>
    public int ExpectedResponders { get; set; }

    /// <summary>True when no request was sent for <see cref="KLineTiming.KeepAliveMs"/>.</summary>
    public bool NeedsKeepAlive => Protocol is not null && Environment.TickCount64 - _lastRequest > Timing.KeepAliveMs;

    /// <summary>
    /// Wakes the ECUs up. <see cref="KLineProtocol.Iso9141"/> and
    /// <see cref="KLineProtocol.Kwp2000Slow"/> both use the 5-baud init; the
    /// keywords decide which of the two the ECU speaks.
    /// </summary>
    public KLineInitResult Init(KLineProtocol protocol, byte address = KLineFraming.ObdFunctionalAddress, CancellationToken cancellation = default)
    {
        if (!Transport.IsOpen)
            Transport.Open();
        Target = address;
        Protocol = null;
        ExpectedResponders = 0;
        Transport.BaudRate = KLineTiming.BaudRate;
        _line.SetBreak(false);
        Delay(Timing.IdleBeforeInitMs, cancellation);
        var result = protocol == KLineProtocol.Kwp2000Fast ? FastInit(cancellation) : FiveBaudInit(address, cancellation);
        Protocol = result.Protocol;
        ExpectedResponders = result.Responders.Count;
        _lastRequest = Environment.TickCount64;
        Delay(Timing.P3Ms, cancellation);
        return result;
    }

    private KLineInitResult FiveBaudInit(byte address, CancellationToken cancellation)
    {
        Mark($"5-baud init 0x{address:X2}");
        // Start bit, 8 data bits LSB first, stop bit; low = break.
        var bits = new bool[10];
        bits[0] = false;
        for (var i = 0; i < 8; i++)
            bits[i + 1] = (address & (1 << i)) != 0;
        bits[9] = true;
        // Higher priority while timing the bits, so the scheduler interrupts this thread less.
        var priority = Thread.CurrentThread.Priority;
        TrySetPriority(ThreadPriority.Highest);
        try
        {
            var clock = Stopwatch.StartNew();
            bool? level = null;
            for (var i = 0; i < bits.Length - 1; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (level != bits[i])
                {
                    // Only on a change: through a network bridge each change is a round trip.
                    _line.SetBreak(!bits[i]);
                    level = bits[i];
                }
                WaitUntil(clock, (i + 1) * Timing.FiveBaudBitMs);
            }
            _line.SetBreak(false); // stop bit: the ECU answers within W1
        }
        finally
        {
            TrySetPriority(priority);
        }
        Transport.DiscardInBuffer();

        var sync = ReadInitByte(Timing.SyncTimeoutMs, "the 0x55 sync byte");
        if (sync != 0x55)
            throw new KLineException($"The ECU answered the 5-baud init with 0x{sync:X2} instead of the 0x55 sync: wrong speed or noise.");
        var kb1 = ReadInitByte(Timing.KeywordTimeoutMs, "keyword 1");
        var kb2 = ReadInitByte(Timing.KeywordTimeoutMs, "keyword 2");
        Delay(Timing.W4Ms, cancellation);
        var inverted = (byte)~kb2;
        Transport.Write([inverted]);
        Emit(LineDirection.Sent, inverted);
        if (AdapterEchoes)
            Emit(LineDirection.Echo, ReadInitByte(Timing.KeywordTimeoutMs, "the echo of the inverted keyword"));
        var confirm = ReadInitByte(Timing.SyncTimeoutMs, "the inverted address");
        if (confirm != (byte)~address)
            throw new KLineException($"The ECU confirmed the init with 0x{confirm:X2} instead of 0x{(byte)~address:X2}.");
        var kwp = kb2 == 0x8F;
        return new KLineInitResult(kwp ? KLineProtocol.Kwp2000Slow : KLineProtocol.Iso9141, kb1, kb2, []);
    }

    private KLineInitResult FastInit(CancellationToken cancellation)
    {
        Mark("fast init");
        var clock = Stopwatch.StartNew();
        _line.SetBreak(true);
        WaitUntil(clock, Timing.FastInitLowMs);
        _line.SetBreak(false);
        WaitUntil(clock, Timing.FastInitLowMs + Timing.FastInitHighMs);
        Transport.DiscardInBuffer();
        Protocol = KLineProtocol.Kwp2000Fast;
        // StartCommunication (0x81), answered with 0xC1 and the keywords.
        var frames = Exchange([0x81]);
        var answer = frames.FirstOrDefault(f => f.Data.Length >= 3 && f.Data[0] == 0xC1)
            ?? throw new KLineException(frames.Count == 0
                ? "No ECU answered the fast init."
                : $"The ECU refused StartCommunication: {frames[0]}.");
        return new KLineInitResult(KLineProtocol.Kwp2000Fast, answer.Data[1], answer.Data[2], [.. frames.Where(f => f.Data[0] == 0xC1).Select(f => f.Source).Distinct()]);
    }

    private byte ReadInitByte(int timeoutMs, string what)
    {
        Transport.ReadTimeout = timeoutMs;
        try
        {
            var b = Transport.ReadByte();
            Emit(LineDirection.Received, b);
            return b;
        }
        catch (TimeoutException)
        {
            Emit(LineDirection.Timeout, 0);
            throw new KLineException($"No answer to the init: {what} did not arrive. Check the ignition, or try another protocol.");
        }
    }

    /// <summary>
    /// Sends one request and returns the answers of every ECU. Negative
    /// "response pending" answers (7F xx 78) are skipped while waiting for
    /// the real one.
    /// </summary>
    public IReadOnlyList<KLineFrame> Request(ReadOnlySpan<byte> data)
    {
        if (Protocol is null)
            throw new InvalidOperationException("Call Init first.");
        var frames = Exchange(data);
        if (ExpectedResponders == 0 && frames.Count > 0)
            ExpectedResponders = frames.Select(f => f.Source).Distinct().Count();
        Delay(Timing.P3Ms, default);
        return frames;
    }

    private List<KLineFrame> Exchange(ReadOnlySpan<byte> data)
    {
        var protocol = Protocol!.Value;
        var message = KLineFraming.EncodeRequest(protocol, Target, data);
        Transport.DiscardInBuffer();
        for (var i = 0; i < message.Length; i++)
        {
            Transport.Write([message[i]]);
            Emit(LineDirection.Sent, message[i]);
            if (i < message.Length - 1)
                Delay(Timing.P4Ms, default);
        }
        _lastRequest = Environment.TickCount64;
        if (AdapterEchoes)
        {
            Transport.ReadTimeout = Timing.ResponseTimeoutMs;
            for (var i = 0; i < message.Length; i++)
            {
                try
                {
                    Emit(LineDirection.Echo, Transport.ReadByte());
                }
                catch (TimeoutException)
                {
                    Emit(LineDirection.Timeout, message[0]);
                    throw new KLineException("The adapter did not echo the request: check the cable.");
                }
            }
        }

        // Bytes are decoded at each pause on the line, never one by one: an ISO 9141-2
        // message has no length field and its checksum can match early (48 6B 10 41 sums to 0x0C,
        // the PID of the answer to 01 0C).
        var received = new List<byte>();
        var frames = new List<KLineFrame>();
        var answered = new HashSet<byte>();
        var pending = new HashSet<byte>();
        var wait = Timing.ResponseTimeoutMs;
        var newBytes = false;
        while (true)
        {
            Transport.ReadTimeout = newBytes ? Timing.FrameGapMs : wait;
            try
            {
                var b = Transport.ReadByte();
                Emit(LineDirection.Received, b);
                received.Add(b);
                newBytes = true;
                continue;
            }
            catch (TimeoutException)
            {
                if (!newBytes)
                    break; // silence: every ECU has answered, or none will
            }
            newBytes = false;
            var decoded = KLineFraming.Decode(protocol, received, out var consumed);
            received.RemoveRange(0, consumed);
            foreach (var f in decoded)
            {
                if (f.Data is [0x7F, _, 0x78, ..])
                {
                    pending.Add(f.Source); // response pending: the real answer follows
                    continue;
                }
                pending.Remove(f.Source);
                frames.Add(f);
                answered.Add(f.Source);
            }
            // Bytes that do not form a message yet stay in 'received'; if nothing more
            // arrives, the next timeout ends the loop.
            if (pending.Count == 0 && ExpectedResponders > 0 && answered.Count >= ExpectedResponders && received.Count == 0)
                break;
            wait = pending.Count > 0 ? Timing.PendingTimeoutMs : Timing.EndOfResponseMs;
        }
        if (frames.Count == 0)
            Emit(LineDirection.Timeout, data[0]);
        return frames;
    }

    private void Mark(string note) => Monitor?.Invoke(new LineEvent(LineDirection.Marker, 0, Transport.BaudRate) { Note = note });

    private void Emit(LineDirection direction, byte value) => Monitor?.Invoke(new LineEvent(direction, value, Transport.BaudRate));

    private static void Delay(int ms, CancellationToken cancellation)
    {
        if (ms <= 0)
            return;
        if (cancellation.WaitHandle.WaitOne(ms))
            cancellation.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Waits until <paramref name="clock"/> reaches <paramref name="ms"/>. The
    /// deadlines are absolute, so a late wake-up does not delay the next edge.
    /// Sleeps in short steps and spins only for the last moment: 2 ms on Linux,
    /// 16 ms on Windows, where a sleep lasts at least one 15.6 ms timer tick.
    /// macOS coalesces timers and was seen waking a sleeping thread 80-120 ms
    /// late, longer than half a 5-baud bit, so there the whole wait spins.
    /// </summary>
    private static void WaitUntil(Stopwatch clock, int ms)
    {
        double remaining;
        while ((remaining = ms - clock.Elapsed.TotalMilliseconds) > SpinMarginMs)
            Thread.Sleep(Math.Max(1, (int)(remaining - SpinMarginMs) / 2));
        while (clock.Elapsed.TotalMilliseconds < ms)
            Thread.SpinWait(50);
    }

    private static void TrySetPriority(ThreadPriority priority)
    {
        try
        {
            Thread.CurrentThread.Priority = priority;
        }
        catch (Exception ex) when (ex is ThreadStateException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Not allowed here: the timing is still absolute, only less protected.
        }
    }

    private static readonly double SpinMarginMs =
        OperatingSystem.IsMacOS() ? double.PositiveInfinity : OperatingSystem.IsWindows() ? 16 : 2;
}
