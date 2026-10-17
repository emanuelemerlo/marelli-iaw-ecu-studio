using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Simulator;

public enum TraceReplayMode
{
    /// <summary>
    /// The software must send exactly the requests of the trace, in the same
    /// order; each one gets the bytes that followed it in the trace. A
    /// different request raises <see cref="TraceDivergedException"/>; after the
    /// last event the line goes silent. For regression tests.
    /// </summary>
    Strict,

    /// <summary>
    /// Any request is accepted and gets the answers recorded for the same
    /// request byte and speed, in turn. Requests never seen get no answer. To
    /// try the software with the values of a real car.
    /// </summary>
    Lookup,
}

/// <summary>A strict replay received a request the trace does not have at that point.</summary>
public sealed class TraceDivergedException(string message) : IOException(message);

/// <summary>
/// An <see cref="ITransport"/> that answers with the bytes recorded in a line
/// trace (<see cref="LineTraceWriter"/>): a real ECU session, played back
/// without a car. Bytes are delivered at once; the recorded timing is not
/// reproduced.
/// </summary>
public sealed class TraceReplayTransport : ITransport
{
    private readonly Lock _gate = new();
    private readonly Queue<byte> _rx = new();
    private readonly IReadOnlyList<TraceEvent> _events;
    private readonly byte[] _broadcast;
    private readonly Dictionary<(int Baud, byte Request), List<byte[]>> _answers = [];
    private readonly Dictionary<(int Baud, byte Request), int> _turn = [];
    private readonly bool _traceEchoes;
    private int _cursor;
    private bool _broadcastDone;
    private int _baudRate = 9600;

    public TraceReplayTransport(LineTrace trace, TraceReplayMode mode = TraceReplayMode.Strict, string? name = null)
    {
        Trace = trace;
        Mode = mode;
        Name = "trace:" + (name ?? trace.Metadata.GetValueOrDefault("transport") ?? "replay");
        _events = [.. trace.Events.Where(e => e.Direction != LineDirection.Marker)];
        _traceEchoes = _events.Any(e => e.Direction == LineDirection.Echo);

        // Bytes received before the first request: the ISO code broadcast at key-on.
        var firstSent = FirstIndexFrom(0, e => e.Direction == LineDirection.Sent);
        _broadcast = [.. _events.Take(firstSent).Where(IsIncoming).Select(e => e.Value)];

        for (var i = firstSent; i < _events.Count; i++)
        {
            if (_events[i].Direction != LineDirection.Sent)
                continue;
            var key = (_events[i].BaudRate, _events[i].Value);
            if (!_answers.TryGetValue(key, out var list))
                _answers[key] = list = [];
            list.Add(AnswerAfter(i));
        }
    }

    public LineTrace Trace { get; }

    public TraceReplayMode Mode { get; }

    public string Name { get; }

    public bool IsOpen { get; private set; }

    public int ReadTimeout { get; set; } = 500;

    /// <summary>Strict mode: true once every recorded request has been replayed.</summary>
    public bool IsExhausted
    {
        get
        {
            lock (_gate)
                return Mode == TraceReplayMode.Strict && FirstIndexFrom(_cursor, e => e.Direction == LineDirection.Sent) >= _events.Count;
        }
    }

    public int BaudRate
    {
        get => _baudRate;
        set
        {
            _baudRate = value;
            if (IsOpen && value == IawLink.InitBaudRate && !_broadcastDone)
            {
                _broadcastDone = true;
                lock (_gate)
                {
                    foreach (var b in _broadcast)
                        _rx.Enqueue(b);
                }
            }
        }
    }

    public int BytesToRead
    {
        get
        {
            lock (_gate)
                return _rx.Count;
        }
    }

    public void Open()
    {
        lock (_gate)
        {
            IsOpen = true;
            _broadcastDone = false;
            _rx.Clear();
            _cursor = FirstIndexFrom(0, e => e.Direction == LineDirection.Sent);
            _turn.Clear();
        }
    }

    public void Close()
    {
        IsOpen = false;
        lock (_gate)
            _rx.Clear();
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        EnsureOpen();
        foreach (var b in data)
        {
            lock (_gate)
            {
                var answer = Mode == TraceReplayMode.Strict ? NextStrict(b) : NextLookup(b);
                foreach (var a in answer)
                    _rx.Enqueue(a);
            }
        }
    }

    private byte[] NextStrict(byte request)
    {
        var i = FirstIndexFrom(_cursor, e => e.Direction == LineDirection.Sent);
        if (i >= _events.Count)
            return []; // end of the trace: the ECU is gone
        var expected = _events[i];
        if (expected.Value != request || expected.BaudRate != _baudRate)
            throw new TraceDivergedException(
                $"Replay diverged at {expected.TimeMs:0.0} ms: the trace sent 0x{expected.Value:X2} at {expected.BaudRate} baud, " +
                $"the software sent 0x{request:X2} at {_baudRate} baud.");
        _cursor = i + 1;
        return AnswerAfter(i);
    }

    private byte[] NextLookup(byte request)
    {
        var key = (_baudRate, request);
        if (!_answers.TryGetValue(key, out var list))
            return _traceEchoes ? [request] : [];
        var turn = _turn.GetValueOrDefault(key);
        _turn[key] = turn + 1;
        return list[turn % list.Count];
    }

    /// <summary>The bytes that came back after the request at <paramref name="sentIndex"/>, up to the next request.</summary>
    private byte[] AnswerAfter(int sentIndex)
    {
        var bytes = new List<byte>();
        for (var j = sentIndex + 1; j < _events.Count && _events[j].Direction != LineDirection.Sent; j++)
        {
            if (IsIncoming(_events[j]))
                bytes.Add(_events[j].Value);
        }
        return [.. bytes];
    }

    private static bool IsIncoming(TraceEvent e) =>
        e.Direction is LineDirection.Received or LineDirection.Echo or LineDirection.Discarded;

    private int FirstIndexFrom(int start, Func<TraceEvent, bool> match)
    {
        var i = start;
        while (i < _events.Count && !match(_events[i]))
            i++;
        return i;
    }

    public byte ReadByte()
    {
        EnsureOpen();
        var deadline = Environment.TickCount64 + ReadTimeout;
        while (true)
        {
            lock (_gate)
            {
                if (_rx.TryDequeue(out var b))
                    return b;
            }
            if (Environment.TickCount64 >= deadline)
                throw new TimeoutException("The replayed ECU did not answer.");
            Thread.Sleep(1);
        }
    }

    public void DiscardInBuffer()
    {
        lock (_gate)
            _rx.Clear();
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new InvalidOperationException("The port is closed.");
    }

    public void Dispose() => Close();
}
