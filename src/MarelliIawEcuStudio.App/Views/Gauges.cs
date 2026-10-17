using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MarelliIawEcuStudio.Core.Definitions;

namespace MarelliIawEcuStudio.App.Views;

/// <summary>
/// Colours of the instruments, read from the theme resources of App.axaml
/// (light or dark) for the theme the control is shown in.
/// </summary>
internal sealed class GaugePalette
{
    private static readonly IBrush Fallback = Brushes.Gray;

    private GaugePalette(Control control)
    {
        IBrush Get(string key) =>
            control.TryFindResource(key, control.ActualThemeVariant, out var value) && value is IBrush brush ? brush : Fallback;
        Track = Get("GaugeTrackBrush");
        Fill = Get("GaugeFillBrush");
        Needle = Get("GaugeNeedleBrush");
        Text = Get("TextBrush");
        Muted = Get("SubtleTextBrush");
        Hub = Get("GaugeHubBrush");
        Danger = Get("GaugeDangerBrush");
        Warning = Get("GaugeWarningBrush");
        Cold = Get("GaugeColdBrush");
    }

    public IBrush Track { get; }

    public IBrush Fill { get; }

    public IBrush Needle { get; }

    public IBrush Text { get; }

    public IBrush Muted { get; }

    public IBrush Hub { get; }

    public IBrush Danger { get; }

    public IBrush Warning { get; }

    public IBrush Cold { get; }

    public static GaugePalette For(Control control) => new(control);

    public IBrush Zone(string level) => level switch
    {
        "danger" => Danger,
        "warning" => Warning,
        "cold" => Cold,
        _ => Track,
    };

    /// <summary>Fill colour for a value: the zone it falls in, else the normal colour.</summary>
    public IBrush ForValue(double value, IReadOnlyList<DisplayZone>? zones) =>
        zones?.FirstOrDefault(z => value >= z.From && value <= z.To) is { } z ? Zone(z.Level) : Fill;

    public static FormattedText Format(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal) =>
        new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);
}

/// <summary>Base for instruments: value, scale and coloured zones.</summary>
public abstract class GaugeBase : Control
{
    public static readonly StyledProperty<double> ValueProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(Value), double.NaN);
    public static readonly StyledProperty<double> MinimumProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(Minimum));
    public static readonly StyledProperty<double> MaximumProperty = AvaloniaProperty.Register<GaugeBase, double>(nameof(Maximum), 100);
    public static readonly StyledProperty<IReadOnlyList<DisplayZone>?> ZonesProperty = AvaloniaProperty.Register<GaugeBase, IReadOnlyList<DisplayZone>?>(nameof(Zones));

    static GaugeBase()
    {
        AffectsRender<GaugeBase>(ValueProperty, MinimumProperty, MaximumProperty, ZonesProperty);
    }

    public double Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    public double Minimum { get => GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    public double Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

    public IReadOnlyList<DisplayZone>? Zones { get => GetValue(ZonesProperty); set => SetValue(ZonesProperty, value); }

    protected GaugeBase()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual(); // the colours come from the theme
    }

    /// <summary>Position of <paramref name="v"/> on the scale, clamped to 0..1.</summary>
    protected double Fraction(double v) => Maximum > Minimum ? Math.Clamp((v - Minimum) / (Maximum - Minimum), 0, 1) : 0;
}

/// <summary>
/// Round dial like a rev counter: a 240° scale with coloured zones, the value
/// arc, a needle, and the value with its unit in the centre.
/// </summary>
public sealed class ArcGauge : GaugeBase
{
    public static readonly StyledProperty<string?> ValueTextProperty = AvaloniaProperty.Register<ArcGauge, string?>(nameof(ValueText));
    public static readonly StyledProperty<string?> UnitProperty = AvaloniaProperty.Register<ArcGauge, string?>(nameof(Unit));

    private const double StartAngle = 150; // degrees, 0 = east, clockwise
    private const double Sweep = 240;

    static ArcGauge()
    {
        AffectsRender<ArcGauge>(ValueTextProperty, UnitProperty);
    }

    public string? ValueText { get => GetValue(ValueTextProperty); set => SetValue(ValueTextProperty, value); }

    public string? Unit { get => GetValue(UnitProperty); set => SetValue(UnitProperty, value); }

    protected override Size MeasureOverride(Size availableSize) => new(170, 140);

    public override void Render(DrawingContext context)
    {
        var palette = GaugePalette.For(this);
        var w = Bounds.Width;
        var h = Bounds.Height;
        var radius = Math.Min(w / 2, h / 1.55) - 10;
        var center = new Point(w / 2, radius + 10);
        const double thickness = 11;

        DrawArc(context, center, radius, 0, 1, palette.Track, thickness);
        foreach (var z in Zones ?? [])
            DrawArc(context, center, radius + thickness / 2 + 3, Fraction(z.From), Fraction(z.To), palette.Zone(z.Level), 4);

        var hasValue = double.IsFinite(Value);
        if (hasValue)
        {
            var f = Fraction(Value);
            DrawArc(context, center, radius, 0, f, palette.ForValue(Value, Zones), thickness);
            var a = (StartAngle + Sweep * f) * Math.PI / 180;
            var tip = new Point(center.X + Math.Cos(a) * (radius - 4), center.Y + Math.Sin(a) * (radius - 4));
            var back = new Point(center.X - Math.Cos(a) * 10, center.Y - Math.Sin(a) * 10);
            context.DrawLine(new Pen(palette.Needle, 3, lineCap: PenLineCap.Round), back, tip);
        }
        context.DrawEllipse(palette.Hub, null, center, 6, 6);

        // Scale ends.
        DrawTextAtAngle(context, palette, center, radius - 22, 0, FormatNumber(Minimum));
        DrawTextAtAngle(context, palette, center, radius - 22, 1, FormatNumber(Maximum));

        var valueText = GaugePalette.Format(hasValue ? ValueText ?? FormatNumber(Value) : "—", 22, palette.Text, FontWeight.SemiBold);
        context.DrawText(valueText, new Point(center.X - valueText.Width / 2, center.Y + 10));
        if (!string.IsNullOrEmpty(Unit))
        {
            var unit = GaugePalette.Format(Unit, 12, palette.Muted);
            context.DrawText(unit, new Point(center.X - unit.Width / 2, center.Y + 10 + valueText.Height));
        }
    }

    private static void DrawTextAtAngle(DrawingContext context, GaugePalette palette, Point center, double r, double fraction, string text)
    {
        var a = (StartAngle + Sweep * fraction) * Math.PI / 180;
        var t = GaugePalette.Format(text, 10.5, palette.Muted);
        var p = new Point(center.X + Math.Cos(a) * r - t.Width / 2, center.Y + Math.Sin(a) * r - t.Height / 2);
        context.DrawText(t, p);
    }

    private static void DrawArc(DrawingContext context, Point center, double radius, double from, double to, IBrush brush, double thickness)
    {
        if (to - from <= 0.0005)
            return;
        var a0 = (StartAngle + Sweep * from) * Math.PI / 180;
        var a1 = (StartAngle + Sweep * to) * Math.PI / 180;
        var p0 = new Point(center.X + Math.Cos(a0) * radius, center.Y + Math.Sin(a0) * radius);
        var p1 = new Point(center.X + Math.Cos(a1) * radius, center.Y + Math.Sin(a1) * radius);
        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            g.BeginFigure(p0, false);
            g.ArcTo(p1, new Size(radius, radius), 0, (to - from) * Sweep > 180, SweepDirection.Clockwise);
            g.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(brush, thickness, lineCap: PenLineCap.Flat), geometry);
    }

    internal static string FormatNumber(double v) =>
        Math.Abs(v) >= 100 || v == Math.Round(v) ? v.ToString("0", CultureInfo.CurrentCulture) : v.ToString("0.#", CultureInfo.CurrentCulture);
}

/// <summary>
/// Horizontal bar with coloured zones. With <see cref="Center"/> it fills from
/// that value in either direction, for corrections around 0 or 1.0.
/// </summary>
public sealed class LinearGauge : GaugeBase
{
    public static readonly StyledProperty<double?> CenterProperty = AvaloniaProperty.Register<LinearGauge, double?>(nameof(Center));

    static LinearGauge()
    {
        AffectsRender<LinearGauge>(CenterProperty);
    }

    public double? Center { get => GetValue(CenterProperty); set => SetValue(CenterProperty, value); }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsFinite(availableSize.Width) ? availableSize.Width : 160, 12);

    public override void Render(DrawingContext context)
    {
        var palette = GaugePalette.For(this);
        var w = Bounds.Width;
        var h = Bounds.Height;
        var bar = new Rect(0, 2, w, h - 4);
        context.DrawRectangle(palette.Track, null, bar, 3, 3);
        foreach (var z in Zones ?? [])
        {
            var x0 = Fraction(z.From) * w;
            var x1 = Fraction(z.To) * w;
            context.DrawRectangle(palette.Zone(z.Level), null, new Rect(x0, h - 3, x1 - x0, 3));
        }
        if (Center is { } c)
        {
            var cx = Fraction(c) * w;
            context.DrawLine(new Pen(palette.Muted, 1), new Point(cx, 0), new Point(cx, h));
        }
        if (!double.IsFinite(Value))
            return;
        var origin = Fraction(Center ?? Minimum) * w;
        var end = Fraction(Value) * w;
        var rect = new Rect(Math.Min(origin, end), 2, Math.Max(2, Math.Abs(end - origin)), h - 4);
        context.DrawRectangle(palette.ForValue(Value, Zones), null, rect, 3, 3);
    }
}
