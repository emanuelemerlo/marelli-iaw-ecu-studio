using System.Diagnostics;
using MarelliIawEcuStudio.Core.KLine;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Simulator;

/// <summary>
/// A K-Line with OBD ECUs behind a simulated KKL cable. Like a real ECU it
/// recognises the 5-baud address and the fast init from the timing of the
/// line breaks, echoes every byte and answers framed requests.
/// </summary>
public sealed class KLineSimulatorTransport : ITransport, ILineControl
{
    private enum State
    {
        Asleep,
        AwaitingInvertedKeyword,
        Awake,
    }

    private readonly Lock _gate = new();
    private readonly LinkedList<(long DueMs, byte Value)> _rx = new();
    private readonly List<(double TimeMs, bool Low)> _edges = [];
    private readonly List<byte> _request = [];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private State _state;
    private bool _fastWake;

    /// <param name="protocol">What the ECUs speak; Kwp2000Slow and Iso9141 answer the 5-baud init, Kwp2000Fast the fast init.</param>
    /// <param name="ecus">The ECUs on the line; the first gives the keywords. Defaults to one engine ECU at 0x10.</param>
    public KLineSimulatorTransport(KLineProtocol protocol, KLineTiming? timing = null, params SimulatedObdEcu[] ecus)
    {
        Protocol = protocol;
        Timing = timing ?? KLineTiming.Default;
        Ecus = ecus.Length > 0 ? ecus : [new SimulatedObdEcu()];
        (KeyByte1, KeyByte2) = protocol == KLineProtocol.Iso9141 ? ((byte)0x08, (byte)0x08) : ((byte)0xEF, (byte)0x8F);
    }

    public KLineProtocol Protocol { get; }

    /// <summary>Timing the tester uses: the bit time of the 5-baud address is read with it.</summary>
    public KLineTiming Timing { get; }

    public IReadOnlyList<SimulatedObdEcu> Ecus { get; }

    public byte KeyByte1 { get; set; }

    public byte KeyByte2 { get; set; }

    /// <summary>False: the ignition is off, only the cable echo comes back.</summary>
    public bool EcuConnected { get; set; } = true;

    public bool Echo { get; set; } = true;

    /// <summary>Gap between the answers of two ECUs (P2, 25 ms and more on a real car).</summary>
    public int InterEcuDelayMs { get; set; }

    /// <summary>Address decoded from the last 5-baud init, for tests.</summary>
    public byte? LastAddress { get; private set; }

    public bool IsAwake => _state == State.Awake;

    public string Name => $"obd-simulator:{Protocol}";

    public bool IsOpen { get; private set; }

    public int BaudRate { get; set; } = 9600;

    public int ReadTimeout { get; set; } = 500;

    public bool DtrEnable { get; set; }

    private double Now => _clock.Elapsed.TotalMilliseconds;

    public void Open()
    {
        IsOpen = true;
        _state = State.Asleep;
    }

    public void Close()
    {
        IsOpen = false;
        lock (_gate)
            _rx.Clear();
    }

    public void SetBreak(bool on)
    {
        lock (_gate)
        {
            if (_edges.Count == 0 && !on)
                return; // releasing an idle line
            _edges.Add((Now, on));
            if (on && _edges.Count == 1)
                _state = State.Asleep; // a new init starts
        }
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (!IsOpen)
            throw new InvalidOperationException("The port is closed.");
        lock (_gate)
        {
            foreach (var b in data)
            {
                if (Echo)
                    _rx.AddLast(((long)Now, b));
                if (!EcuConnected || BaudRate != KLineTiming.BaudRate)
                    continue;
                DetectFastInit();
                switch (_state)
                {
                    case State.AwaitingInvertedKeyword:
                        if (b == (byte)~KeyByte2)
                        {
                            Enqueue([(byte)~LastAddress!.Value], 0);
                            _state = State.Awake;
                        }
                        break;
                    case State.Awake:
                        _request.Add(b);
                        break;
                    case State.Asleep when _fastWake:
                        _request.Add(b);
                        break;
                }
            }
        }
    }

    /// <summary>
    /// A single low pulse, then data: the KWP2000 fast init. A 5-baud address
    /// has more edges before any data, and a pulse as long as a 5-baud bit is not a wake-up.
    /// </summary>
    private void DetectFastInit()
    {
        if (_edges.Count == 2 && _edges[0].Low && !_edges[1].Low && Protocol == KLineProtocol.Kwp2000Fast)
        {
            var low = _edges[1].TimeMs - _edges[0].TimeMs;
            if (low < Timing.FiveBaudBitMs * 0.75)
            {
                _fastWake = true;
                _request.Clear();
            }
            _edges.Clear();
        }
    }

    /// <summary>Runs the ECU side when the tester starts listening: decode an address, answer a request.</summary>
    private void Process()
    {
        if (!EcuConnected)
        {
            _edges.Clear();
            _request.Clear();
            return;
        }
        var bit = Timing.FiveBaudBitMs;
        if (_edges.Count > 0 && _edges[0].Low && Now - _edges[0].TimeMs >= bit * 9.5)
        {
            // A tester's edges come late, never early (its deadlines are absolute), so
            // each edge belongs to the bit boundary at or just before it: -0.25 to +0.75 bit.
            var start = _edges[0].TimeMs;
            var low = new bool[10];
            foreach (var (time, isLow) in _edges)
            {
                var boundary = (int)Math.Round((time - start) / bit - 0.25);
                for (var k = Math.Clamp(boundary, 0, 9); k < 10; k++)
                    low[k] = isLow;
            }
            byte address = 0;
            for (var i = 0; i < 8; i++)
            {
                if (!low[i + 1])
                    address |= (byte)(1 << i);
            }
            _edges.Clear();
            LastAddress = address;
            if (address == KLineFraming.ObdFunctionalAddress && Protocol != KLineProtocol.Kwp2000Fast && BaudRate == KLineTiming.BaudRate)
            {
                Enqueue([0x55, KeyByte1, KeyByte2], 0);
                _state = State.AwaitingInvertedKeyword;
            }
        }

        if (_request.Count == 0)
            return;
        var framing = Protocol == KLineProtocol.Iso9141 ? KLineProtocol.Iso9141 : KLineProtocol.Kwp2000Fast;
        var length = KLineFraming.TryDecodeRequest(framing, _request, out var frame);
        if (length == 0)
            return;
        _request.Clear();
        if (length < 0 || frame is null)
            return;
        if (_fastWake)
        {
            _fastWake = false;
            if (frame.Data is not [0x81, ..])
                return;
            _state = State.Awake;
        }
        var delay = 0;
        foreach (var ecu in Ecus)
        {
            var answers = ecu.Handle(frame.Data, KeyByte1, KeyByte2);
            if (answers.Count == 0)
                continue;
            Enqueue(ecu.Encode(framing, answers), delay);
            delay += InterEcuDelayMs;
        }
    }

    private void Enqueue(IEnumerable<byte> bytes, int delayMs)
    {
        var due = (long)Now + delayMs;
        foreach (var b in bytes)
            _rx.AddLast((due, b));
    }

    public int BytesToRead
    {
        get
        {
            lock (_gate)
            {
                Process();
                var now = (long)Now;
                return _rx.Count(i => i.DueMs <= now);
            }
        }
    }

    public byte ReadByte()
    {
        if (!IsOpen)
            throw new InvalidOperationException("The port is closed.");
        var deadline = Now + ReadTimeout;
        while (true)
        {
            lock (_gate)
            {
                Process();
                var node = _rx.First;
                if (node is not null && node.Value.DueMs <= (long)Now)
                {
                    _rx.RemoveFirst();
                    return node.Value.Value;
                }
            }
            if (Now >= deadline)
                throw new TimeoutException("The simulated OBD ECU did not answer.");
            Thread.Sleep(1);
        }
    }

    public void DiscardInBuffer()
    {
        lock (_gate)
        {
            var now = (long)Now;
            var node = _rx.First;
            while (node is not null)
            {
                var next = node.Next;
                if (node.Value.DueMs <= now)
                    _rx.Remove(node);
                node = next;
            }
        }
    }

    public void Dispose() => Close();
}
