using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarelliIawEcuStudio.Presentation.Live;

namespace MarelliIawEcuStudio.Presentation.ViewModels;

/// <summary>
/// Live graph: one series per plotted parameter, filled from the polling
/// cycles. The plot itself (ScottPlot) lives in the view.
/// </summary>
public sealed partial class GraphViewModel : ObservableObject, IPlotSource
{
    private readonly Dictionary<string, PlotSeries> _series = new(StringComparer.Ordinal);
    private IReadOnlyList<PlotSeries> _visible = [];

    public static IReadOnlyList<int> WindowChoices { get; } = [10, 30, 60, 300, 0];

    public event Action<bool>? PlotChanged;

    public IReadOnlyList<PlotSeries> VisibleSeries => _visible;

    [ObservableProperty]
    public partial bool SeparateScales { get; set; } = true;

    /// <summary>Visible window in seconds; 0 shows the whole recording.</summary>
    [ObservableProperty]
    public partial int WindowSecondsChoice { get; set; } = 30;

    [ObservableProperty]
    public partial bool Follow { get; set; } = true;

    [ObservableProperty]
    public partial bool HasSeries { get; set; }

    public double? WindowSeconds => WindowSecondsChoice > 0 ? WindowSecondsChoice : null;

    public double? CursorSeconds => null;

    partial void OnSeparateScalesChanged(bool value) => PlotChanged?.Invoke(true);

    partial void OnWindowSecondsChoiceChanged(int value) => PlotChanged?.Invoke(false);

    partial void OnFollowChanged(bool value) => PlotChanged?.Invoke(false);

    /// <summary>Sets which parameters are plotted, in display order.</summary>
    public void SetPlotted(IEnumerable<ParameterRowViewModel> rows)
    {
        var list = new List<PlotSeries>();
        foreach (var row in rows)
        {
            if (!_series.TryGetValue(row.Id, out var s))
                _series[row.Id] = s = new PlotSeries(row.Id, row.Name, row.Unit);
            s.Name = row.Name;
            s.Unit = row.Unit;
            list.Add(s);
        }
        _visible = list;
        HasSeries = list.Count > 0;
        PlotChanged?.Invoke(true);
    }

    /// <summary>Adds the values of one cycle. Call on the UI thread.</summary>
    public void Append(LiveCycle cycle)
    {
        var added = false;
        foreach (var s in _visible)
        {
            if (cycle.Values.TryGetValue(s.Id, out var v) && v is { } value)
            {
                s.Add(cycle.TimeSeconds, (double)value);
                added = true;
            }
        }
        if (added)
            PlotChanged?.Invoke(false);
    }

    [RelayCommand]
    public void Clear()
    {
        foreach (var s in _series.Values)
            s.Clear();
        PlotChanged?.Invoke(true);
    }

    /// <summary>Forgets all series (new connection).</summary>
    public void Reset()
    {
        _series.Clear();
        _visible = [];
        HasSeries = false;
        PlotChanged?.Invoke(true);
    }
}
