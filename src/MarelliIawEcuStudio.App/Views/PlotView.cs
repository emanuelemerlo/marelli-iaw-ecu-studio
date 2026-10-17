using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using MarelliIawEcuStudio.Presentation.Live;
using ScottPlot;
using ScottPlot.Avalonia;
using ScottPlot.Plottables;

namespace MarelliIawEcuStudio.App.Views;

/// <summary>
/// ScottPlot view of an <see cref="IPlotSource"/>: zoom with the wheel, pan by
/// dragging, double-click to reset. With separate scales each series gets its
/// own Y axis, coloured like its line. Redraws at most 15 times per second.
/// Background, axes, grid and legend follow the light or dark theme.
/// </summary>
public sealed class PlotView : UserControl
{
    public static readonly StyledProperty<IPlotSource?> SourceProperty =
        AvaloniaProperty.Register<PlotView, IPlotSource?>(nameof(Source));

    private readonly AvaPlot _plot = new();
    private readonly DispatcherTimer _timer;
    private readonly List<IYAxis> _extraAxes = [];
    private VerticalLine? _cursor;
    private IPanel? _legend;
    private bool _dirty;
    private bool _rebuild = true;

    public PlotView()
    {
        Content = _plot;
        ActualThemeVariantChanged += (_, _) =>
        {
            OnPlotChanged(true);
            RenderIfDirty();
        };
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(66), DispatcherPriority.Render, (_, _) => RenderIfDirty());
        Rebuild();
    }

    public IPlotSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The ScottPlot control, for tests and screenshots.</summary>
    public AvaPlot Plot => _plot;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            if (change.OldValue is IPlotSource old)
                old.PlotChanged -= OnPlotChanged;
            if (change.NewValue is IPlotSource source)
                source.PlotChanged += OnPlotChanged;
            OnPlotChanged(true);
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPlotChanged(bool structural)
    {
        _rebuild |= structural;
        _dirty = true;
    }

    /// <summary>Applies pending changes now (normally done by the timer).</summary>
    public void RenderIfDirty()
    {
        if (!_dirty)
            return;
        _dirty = false;
        if (_rebuild)
        {
            _rebuild = false;
            Rebuild();
        }
        UpdateLimits();
        _plot.Refresh();
    }

    private void Rebuild()
    {
        var plot = _plot.Plot;
        plot.Clear();
        foreach (var axis in _extraAxes)
            plot.Axes.Remove(axis);
        if (_legend is not null)
            plot.Axes.Remove(_legend); // ShowLegend adds a new panel each time
        _legend = null;
        _extraAxes.Clear();
        plot.Axes.Left.Label.Text = "";
        plot.Axes.Bottom.Label.Text = "s";
        var theme = ApplyTheme();
        _cursor = null;

        var source = Source;
        if (source is null || source.VisibleSeries.Count == 0)
            return;

        var palette = new ScottPlot.Palettes.Category10();
        for (var i = 0; i < source.VisibleSeries.Count; i++)
        {
            var s = source.VisibleSeries[i];
            var color = palette.GetColor(i);
            var line = plot.Add.ScatterLine(s.Xs, s.Ys, color);
            line.LegendText = s.Label;
            line.LineWidth = 1.5f;
            line.MarkerSize = 0;
            if (source.SeparateScales)
            {
                IYAxis axis;
                if (i == 0)
                {
                    axis = plot.Axes.Left;
                }
                else
                {
                    axis = i % 2 == 1 ? plot.Axes.AddRightAxis() : plot.Axes.AddLeftAxis();
                    _extraAxes.Add(axis);
                }
                axis.Label.Text = s.Label;
                axis.Label.ForeColor = color;
                axis.TickLabelStyle.ForeColor = color;
                line.Axes.YAxis = axis;
            }
        }
        if (!source.SeparateScales && source.VisibleSeries.Count == 1)
            plot.Axes.Left.Label.Text = source.VisibleSeries[0].Label;
        _legend = plot.ShowLegend(Edge.Bottom);
        if (source.CursorSeconds is not null)
            _cursor = plot.Add.VerticalLine(0, 1.5f, theme.Text, LinePattern.Dashed);
        AutoScaleAll();
    }

    /// <summary>Colours of the figure for the current theme; returns them for the plottables.</summary>
    private (ScottPlot.Color Text, ScottPlot.Color Background) ApplyTheme()
    {
        ScottPlot.Color Get(string key, string fallback) =>
            this.TryFindResource(key, ActualThemeVariant, out var value) && value is ISolidColorBrush brush
                ? new ScottPlot.Color(brush.Color.R, brush.Color.G, brush.Color.B, brush.Color.A)
                : ScottPlot.Color.FromHex(fallback);
        var text = Get("TextBrush", "#1C1F23");
        var background = Get("PlotBackgroundBrush", "#FFFFFF");
        var grid = Get("PlotGridBrush", "#E6E8EB");
        var plot = _plot.Plot;
        plot.FigureBackground.Color = background;
        plot.DataBackground.Color = background;
        plot.Axes.Color(text);
        plot.Grid.MajorLineColor = grid;
        plot.Legend.BackgroundColor = background;
        plot.Legend.FontColor = text;
        plot.Legend.OutlineColor = grid;
        return (text, background);
    }

    /// <summary>Fits every Y axis (also the extra ones) to its own series, and X to all data.</summary>
    private void AutoScaleAll()
    {
        var plot = _plot.Plot;
        plot.Axes.AutoScaleX();
        foreach (var axis in plot.GetPlottables().OfType<Scatter>().Select(s => s.Axes.YAxis).Distinct())
            plot.Axes.AutoScaleY(axis);
    }

    private void UpdateLimits()
    {
        var source = Source;
        if (source is null || source.VisibleSeries.Count == 0)
            return;
        var plot = _plot.Plot;
        if (_cursor is not null && source.CursorSeconds is { } cursor)
            _cursor.X = cursor;
        if (!source.Follow)
            return;
        var latest = source.VisibleSeries.Where(s => s.Xs.Count > 0).Select(s => s.Xs[^1]).DefaultIfEmpty(0).Max();
        AutoScaleAll();
        if (source.WindowSeconds is { } window)
            plot.Axes.SetLimitsX(Math.Max(0, latest - window), Math.Max(window, latest));
    }
}
