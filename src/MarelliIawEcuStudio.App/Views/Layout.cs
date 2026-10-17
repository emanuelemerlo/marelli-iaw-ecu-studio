using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace MarelliIawEcuStudio.App.Views;

/// <summary>
/// Scales its content so that it fills the visible area without scrolling:
/// smaller on a small window, larger on a big screen. The content is laid out
/// at the width it gets after scaling, so wrapping panels reflow instead of
/// just shrinking. Below <see cref="MinScale"/> text would be too small to read,
/// so the content stops shrinking and the surrounding <see cref="ScrollViewer"/>
/// scrolls instead.
/// </summary>
/// <remarks>
/// Put it directly inside a vertical <see cref="ScrollViewer"/>: the height to
/// fit is that of the scroll viewer, since a scroll viewer gives its content an
/// infinite height.
/// </remarks>
public sealed class FitToViewport : Decorator
{
    public static readonly StyledProperty<double> MinScaleProperty =
        AvaloniaProperty.Register<FitToViewport, double>(nameof(MinScale), 0.8);

    public static readonly StyledProperty<double> MaxScaleProperty =
        AvaloniaProperty.Register<FitToViewport, double>(nameof(MaxScale), 1.4);

    /// <summary>The scale chosen by the last layout pass, for tests.</summary>
    /// <summary>
    /// Content shorter than this share of the viewport at its natural size is not
    /// enlarged: a single card on an empty page should not turn into a poster.
    /// </summary>
    public static readonly StyledProperty<double> GrowThresholdProperty =
        AvaloniaProperty.Register<FitToViewport, double>(nameof(GrowThreshold), 0.6);

    public static readonly DirectProperty<FitToViewport, double> ScaleProperty =
        AvaloniaProperty.RegisterDirect<FitToViewport, double>(nameof(Scale), o => o.Scale);

    private ScrollViewer? _viewer;
    private double _scale = 1;
    private Size _lastViewport;
    private double _lastHeight = double.NaN;

    static FitToViewport()
    {
        AffectsMeasure<FitToViewport>(MinScaleProperty, MaxScaleProperty, GrowThresholdProperty);
    }

    public double MinScale { get => GetValue(MinScaleProperty); set => SetValue(MinScaleProperty, value); }

    public double MaxScale { get => GetValue(MaxScaleProperty); set => SetValue(MaxScaleProperty, value); }

    public double GrowThreshold { get => GetValue(GrowThresholdProperty); set => SetValue(GrowThresholdProperty, value); }

    public double Scale
    {
        get => _scale;
        private set => SetAndRaise(ScaleProperty, ref _scale, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewer = this.FindAncestorOfType<ScrollViewer>();
        if (_viewer is not null)
            _viewer.PropertyChanged += OnViewerPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewer is not null)
            _viewer.PropertyChanged -= OnViewerPropertyChanged;
        _viewer = null;
        base.OnDetachedFromVisualTree(e);
    }

    // A scroll viewer measures its content with an infinite height, so a change of
    // its height alone would not reach this control.
    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty && e.OldValue is Rect old && e.NewValue is Rect now && Math.Abs(old.Height - now.Height) > 0.5)
            InvalidateMeasure();
    }

    private Size Viewport(Size available)
    {
        var height = double.IsFinite(available.Height) ? available.Height : _viewer?.Bounds.Height ?? double.PositiveInfinity;
        if (height <= 0)
            height = double.PositiveInfinity; // not arranged yet
        return new Size(available.Width, height);
    }

    private double HeightAt(double scale, double width)
    {
        Child!.Measure(new Size(width / scale, double.PositiveInfinity));
        return Child.DesiredSize.Height;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Child is null)
            return default;
        var viewport = Viewport(availableSize);
        if (!double.IsFinite(viewport.Width))
        {
            Scale = 1;
            Child.Measure(availableSize);
            return Child.DesiredSize;
        }
        if (!double.IsFinite(viewport.Height))
        {
            Scale = Math.Clamp(1, MinScale, MaxScale);
            return new Size(viewport.Width, HeightAt(Scale, viewport.Width) * Scale);
        }

        // The values change several times per second and re-measure the content:
        // keep the scale while the content still fits and its height has not changed.
        var height = HeightAt(Scale, viewport.Width);
        var unchanged = viewport == _lastViewport && Math.Abs(height - _lastHeight) < 0.5;
        if (!unchanged)
        {
            // scale × height(scale) grows with the scale (narrower content wraps more):
            // find the largest scale that still fits by bisection.
            var natural = HeightAt(1, viewport.Width);
            var max = natural < viewport.Height * GrowThreshold ? Math.Min(1, MaxScale) : MaxScale;
            double lo = Math.Min(MinScale, max), hi = max;
            if (HeightAt(hi, viewport.Width) * hi <= viewport.Height)
            {
                lo = hi;
            }
            else
            {
                for (var i = 0; i < 12 && hi - lo > 0.005; i++)
                {
                    var mid = (lo + hi) / 2;
                    if (HeightAt(mid, viewport.Width) * mid <= viewport.Height)
                        lo = mid;
                    else
                        hi = mid;
                }
            }
            Scale = lo;
            height = HeightAt(Scale, viewport.Width);
            _lastViewport = viewport;
            _lastHeight = height;
        }
        return new Size(viewport.Width, Math.Max(height * Scale, 0));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Child is null)
            return finalSize;
        var scale = Scale;
        Child.RenderTransformOrigin = RelativePoint.TopLeft;
        Child.RenderTransform = Math.Abs(scale - 1) < 0.001 ? null : new ScaleTransform(scale, scale);
        Child.Arrange(new Rect(0, 0, finalSize.Width / scale, Math.Max(Child.DesiredSize.Height, finalSize.Height / scale)));
        return finalSize;
    }
}

/// <summary>
/// Lays out its children in as many columns as fit (each at least
/// <see cref="MinColumnWidth"/> wide, at most <see cref="MaxColumns"/>), filling
/// columns in order and keeping them about the same height: groups of a table
/// read top to bottom, then left to right, like a newspaper.
/// </summary>
public sealed class BalancedColumns : Panel
{
    public static readonly StyledProperty<double> MinColumnWidthProperty =
        AvaloniaProperty.Register<BalancedColumns, double>(nameof(MinColumnWidth), 420);

    public static readonly StyledProperty<int> MaxColumnsProperty =
        AvaloniaProperty.Register<BalancedColumns, int>(nameof(MaxColumns), 3);

    public static readonly StyledProperty<double> ColumnSpacingProperty =
        AvaloniaProperty.Register<BalancedColumns, double>(nameof(ColumnSpacing), 16);

    public static readonly StyledProperty<double> RowSpacingProperty =
        AvaloniaProperty.Register<BalancedColumns, double>(nameof(RowSpacing), 8);

    private readonly List<(int Column, double Top)> _slots = [];
    private int _columns = 1;
    private double _columnWidth;

    static BalancedColumns()
    {
        AffectsMeasure<BalancedColumns>(MinColumnWidthProperty, MaxColumnsProperty, ColumnSpacingProperty, RowSpacingProperty);
    }

    public double MinColumnWidth { get => GetValue(MinColumnWidthProperty); set => SetValue(MinColumnWidthProperty, value); }

    public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }

    public double ColumnSpacing { get => GetValue(ColumnSpacingProperty); set => SetValue(ColumnSpacingProperty, value); }

    public double RowSpacing { get => GetValue(RowSpacingProperty); set => SetValue(RowSpacingProperty, value); }

    /// <summary>Number of columns of the last layout, for tests.</summary>
    public int Columns => _columns;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : MinColumnWidth;
        var children = Children.Where(c => c.IsVisible).ToList();
        var columns = Math.Clamp((int)((width + ColumnSpacing) / (MinColumnWidth + ColumnSpacing)), 1, Math.Max(1, MaxColumns));
        columns = Math.Min(columns, Math.Max(1, children.Count));
        _columns = columns;
        _columnWidth = (width - ColumnSpacing * (columns - 1)) / columns;
        var heights = new List<double>();
        foreach (var child in children)
        {
            child.Measure(new Size(_columnWidth, double.PositiveInfinity));
            heights.Add(child.DesiredSize.Height);
        }

        // Split the children, in order, into the columns so that the tallest column
        // is as short as possible (a handful of groups: trying every split is cheap).
        var n = heights.Count;
        var prefix = new double[n + 1];
        for (var i = 0; i < n; i++)
            prefix[i + 1] = prefix[i] + heights[i];
        double Span(int from, int to) => to > from ? prefix[to] - prefix[from] + RowSpacing * (to - from - 1) : 0;
        // best[c, i]: tallest column when the first i children fill c columns; cut[c, i]: where the last column starts.
        var best = new double[columns + 1, n + 1];
        var cut = new int[columns + 1, n + 1];
        for (var i = 1; i <= n; i++)
            best[1, i] = Span(0, i);
        for (var c = 2; c <= columns; c++)
        {
            for (var i = 0; i <= n; i++)
            {
                best[c, i] = double.PositiveInfinity;
                for (var j = 0; j <= i; j++)
                {
                    var tallestHere = Math.Max(best[c - 1, j], Span(j, i));
                    if (tallestHere < best[c, i] - 0.01)
                    {
                        best[c, i] = tallestHere;
                        cut[c, i] = j;
                    }
                }
            }
        }
        var starts = new int[columns];
        for (int c = columns, i = n; c >= 1; c--)
        {
            starts[c - 1] = c == 1 ? 0 : cut[c, i];
            i = starts[c - 1];
        }
        _slots.Clear();
        var tallest = 0.0;
        for (var c = 0; c < columns; c++)
        {
            var end = c + 1 < columns ? starts[c + 1] : n;
            var top = 0.0;
            for (var i = starts[c]; i < end; i++)
            {
                _slots.Add((c, top));
                top += heights[i] + RowSpacing;
            }
            tallest = Math.Max(tallest, top - RowSpacing);
        }
        return new Size(width, tallest);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = Children.Where(c => c.IsVisible).ToList();
        for (var i = 0; i < children.Count && i < _slots.Count; i++)
        {
            var (column, top) = _slots[i];
            children[i].Arrange(new Rect(column * (_columnWidth + ColumnSpacing), top, _columnWidth, children[i].DesiredSize.Height));
        }
        return finalSize;
    }
}

/// <summary>Small converters used by the views.</summary>
public static class Converters
{
    /// <summary>The options panel may use most of the window height, and scrolls beyond it.</summary>
    public static readonly IValueConverter OptionsHeight =
        new FuncValueConverter<Rect, double>(bounds => Math.Max(240, bounds.Height - 90));
}
