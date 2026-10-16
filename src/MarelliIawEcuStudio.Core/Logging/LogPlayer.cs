namespace MarelliIawEcuStudio.Core.Logging;

/// <summary>
/// Plays back a recorded <see cref="DataLog"/> as if it were live: play,
/// pause, seek and speed (1x, 2x, 5x...). Time is driven from outside with
/// <see cref="Advance"/>, so the player has no timer of its own and can be
/// tested deterministically.
/// </summary>
public sealed class LogPlayer
{
    private double _positionMs;
    private double _speed = 1;

    public LogPlayer(DataLog log)
    {
        Log = log;
        StartMs = log.Samples.Count > 0 ? log.Samples[0].TimeMs : 0;
        DurationMs = log.Samples.Count > 0 ? log.Samples[^1].TimeMs - StartMs : 0;
    }

    public DataLog Log { get; }

    /// <summary>Timestamp of the first sample.</summary>
    public long StartMs { get; }

    /// <summary>Time between first and last sample.</summary>
    public long DurationMs { get; }

    public bool IsPlaying { get; private set; }

    public bool IsAtEnd => _positionMs >= DurationMs;

    /// <summary>Playback position relative to the first sample, in milliseconds.</summary>
    public double PositionMs
    {
        get => _positionMs;
        set
        {
            var clamped = Math.Clamp(value, 0, DurationMs);
            if (clamped == _positionMs)
                return;
            _positionMs = clamped;
            PositionChanged?.Invoke(this);
        }
    }

    /// <summary>Playback speed multiplier; must be positive.</summary>
    public double Speed
    {
        get => _speed;
        set => _speed = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Speed must be positive.");
    }

    /// <summary>Raised whenever the position changes (playing or seeking).</summary>
    public event Action<LogPlayer>? PositionChanged;

    /// <summary>Raised when playback reaches the end and stops.</summary>
    public event Action<LogPlayer>? Finished;

    public void Play()
    {
        if (Log.Samples.Count == 0)
            return;
        if (IsAtEnd)
            PositionMs = 0;
        IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;

    public void TogglePlay()
    {
        if (IsPlaying)
            Pause();
        else
            Play();
    }

    /// <summary>Moves the position forward by <paramref name="realElapsed"/> × <see cref="Speed"/> while playing.</summary>
    public void Advance(TimeSpan realElapsed)
    {
        if (!IsPlaying)
            return;
        PositionMs = _positionMs + realElapsed.TotalMilliseconds * _speed;
        if (IsAtEnd)
        {
            IsPlaying = false;
            Finished?.Invoke(this);
        }
    }

    /// <summary>Index of the last sample at or before the current position (-1 when the log is empty).</summary>
    public int CurrentIndex => IndexAt(StartMs + _positionMs);

    public LogSample? CurrentSample => CurrentIndex >= 0 ? Log.Samples[CurrentIndex] : null;

    /// <summary>Index of the last sample with a timestamp at or before <paramref name="timeMs"/>.</summary>
    public int IndexAt(double timeMs)
    {
        var samples = Log.Samples;
        if (samples.Count == 0)
            return -1;
        int lo = 0, hi = samples.Count - 1;
        if (timeMs < samples[0].TimeMs)
            return 0;
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            if (samples[mid].TimeMs <= timeMs)
                lo = mid;
            else
                hi = mid - 1;
        }
        return lo;
    }
}
