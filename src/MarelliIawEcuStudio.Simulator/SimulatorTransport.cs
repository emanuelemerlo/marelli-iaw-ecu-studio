using System.Diagnostics;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Transport;

namespace MarelliIawEcuStudio.Simulator;

/// <summary>
/// An <see cref="ITransport"/> wired to a <see cref="SimulatedEcu"/>, behaving
/// like a KKL cable: every sent byte is echoed, then the ECU answer follows.
/// </summary>
public sealed class SimulatorTransport : ITransport
{
    private readonly Lock _gate = new();
    private readonly LinkedList<(long DueMs, byte Value)> _rx = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _broadcastDone;
    private int _baudRate = 9600;

    public SimulatorTransport(SimulatedEcu ecu)
    {
        Ecu = ecu;
    }

    public SimulatedEcu Ecu { get; }

    public string Name => $"simulator:{Ecu.Definition.Id}";

    /// <summary>Echo every sent byte, as KKL adapters do.</summary>
    public bool Echo { get; set; } = true;

    /// <summary>Extra delay before each ECU answer, to emulate slow adapters or radio links.</summary>
    public int LatencyMs { get; set; }

    /// <summary>When false the ECU is unplugged: only the echo comes back.</summary>
    public bool EcuConnected { get; set; } = true;

    /// <summary>Called before each request so a scenario can move the engine values.</summary>
    public Action<SimulatedEcu, TimeSpan>? Scenario { get; set; }

    public bool IsOpen { get; private set; }

    public int ReadTimeout { get; set; } = 500;

    public int BaudRate
    {
        get => _baudRate;
        set
        {
            _baudRate = value;
            if (IsOpen && value == IawLink.InitBaudRate && !_broadcastDone && EcuConnected)
            {
                _broadcastDone = true;
                lock (_gate)
                    Enqueue(Ecu.KeyOnBroadcast());
            }
        }
    }

    public int BytesToRead
    {
        get
        {
            lock (_gate)
                return _rx.Count(i => i.DueMs <= Now);
        }
    }

    private long Now => _clock.ElapsedMilliseconds;

    public void Open()
    {
        IsOpen = true;
        _broadcastDone = false;
        Ecu.Reset();
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
                if (Echo)
                    _rx.AddLast((Now, b));
                if (!EcuConnected)
                    continue;
                Scenario?.Invoke(Ecu, _clock.Elapsed);
                Enqueue(Ecu.Receive(b, _baudRate));
            }
        }
    }

    private void Enqueue(IEnumerable<EcuEmission> emissions)
    {
        var due = Now + LatencyMs;
        foreach (var e in emissions)
        {
            due += e.DelayMs;
            _rx.AddLast((due, e.Value));
        }
    }

    public byte ReadByte()
    {
        EnsureOpen();
        var deadline = Now + ReadTimeout;
        while (true)
        {
            lock (_gate)
            {
                var node = _rx.First;
                if (node is not null && node.Value.DueMs <= Now)
                {
                    _rx.RemoveFirst();
                    return node.Value.Value;
                }
            }
            if (Now >= deadline)
                throw new TimeoutException("The simulated ECU did not answer.");
            Thread.Sleep(1);
        }
    }

    public void DiscardInBuffer()
    {
        lock (_gate)
        {
            var node = _rx.First;
            while (node is not null)
            {
                var next = node.Next;
                if (node.Value.DueMs <= Now)
                    _rx.Remove(node);
                node = next;
            }
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            throw new InvalidOperationException("The port is closed.");
    }

    public void Dispose() => Close();
}
