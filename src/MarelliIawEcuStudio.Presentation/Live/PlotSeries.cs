namespace MarelliIawEcuStudio.Presentation.Live;

/// <summary>
/// A time series for the graphs: x in seconds, y in physical units. Lists
/// are mutated on the UI thread only, so the plot can read them directly.
/// </summary>
public sealed class PlotSeries(string id, string name, string unit)
{
    /// <summary>Upper bound on the points kept for a live series (about 1 h at 10 Hz).</summary>
    public const int MaxLivePoints = 36_000;

    public string Id { get; } = id;

    public string Name { get; set; } = name;

    public string Unit { get; set; } = unit;

    public List<double> Xs { get; } = [];

    public List<double> Ys { get; } = [];

    public string Label => Unit.Length > 0 ? $"{Name} [{Unit}]" : Name;

    public void Add(double x, double y, int maxPoints = MaxLivePoints)
    {
        Xs.Add(x);
        Ys.Add(y);
        if (Xs.Count > maxPoints)
        {
            var drop = Xs.Count - maxPoints;
            Xs.RemoveRange(0, drop);
            Ys.RemoveRange(0, drop);
        }
    }

    public void Clear()
    {
        Xs.Clear();
        Ys.Clear();
    }
}

/// <summary>What a plot view shows; implemented by the live graph and by the replay.</summary>
public interface IPlotSource
{
    IReadOnlyList<PlotSeries> VisibleSeries { get; }

    /// <summary>One Y axis per series instead of a shared one.</summary>
    bool SeparateScales { get; }

    /// <summary>Visible width of the x axis in seconds, or null for everything.</summary>
    double? WindowSeconds { get; }

    /// <summary>Keep the right edge on the latest data (live) or on the cursor (replay).</summary>
    bool Follow { get; }

    /// <summary>Vertical cursor (replay position), or null.</summary>
    double? CursorSeconds { get; }

    /// <summary>Raised when data or settings change; <c>true</c> when the set of series changed.</summary>
    event Action<bool>? PlotChanged;
}
