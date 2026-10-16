namespace MarelliIawEcuStudio.Core.Session;

/// <summary>
/// Measures how many polling cycles per second actually complete, over a
/// sliding window. With one byte per request at 7680 baud the refresh rate
/// depends on how many parameters are selected and on the adapter latency,
/// so the UI shows the real figure.
/// </summary>
public sealed class RateMeter(TimeSpan window)
{
    private readonly Queue<TimeSpan> _ticks = new();
    private readonly Lock _gate = new();

    public RateMeter() : this(TimeSpan.FromSeconds(3))
    {
    }

    /// <summary>Records one completed cycle at time <paramref name="now"/> (monotonic).</summary>
    public void Tick(TimeSpan now)
    {
        lock (_gate)
        {
            _ticks.Enqueue(now);
            while (_ticks.Count > 0 && now - _ticks.Peek() > window)
                _ticks.Dequeue();
        }
    }

    /// <summary>Cycles per second in the window ending at <paramref name="now"/>.</summary>
    public double RatePerSecond(TimeSpan now)
    {
        lock (_gate)
        {
            while (_ticks.Count > 0 && now - _ticks.Peek() > window)
                _ticks.Dequeue();
            if (_ticks.Count < 2)
                return 0;
            var span = (now - _ticks.Peek()).TotalSeconds;
            return span > 0 ? (_ticks.Count - 1) / span : 0;
        }
    }

    public void Reset()
    {
        lock (_gate)
            _ticks.Clear();
    }
}
