using System.Globalization;

namespace MarelliIawEcuStudio.Core.Definitions;

/// <summary>
/// How a parameter is shown on the dashboard: which group it belongs to and
/// which instrument draws it. Optional; without it the parameter is shown as
/// a plain value in the "other" group (or as a LED when it is a bit).
/// </summary>
public sealed class DisplaySpec
{
    public const string GroupEngine = "engine";
    public const string GroupTemperatures = "temperatures";
    public const string GroupElectrical = "electrical";
    public const string GroupMixture = "mixture";
    public const string GroupIdle = "idle";
    public const string GroupAdaptation = "adaptation";
    public const string GroupStatus = "status";
    public const string GroupOther = "other";

    public const string WidgetGauge = "gauge";
    public const string WidgetBar = "bar";
    public const string WidgetLed = "led";
    public const string WidgetValue = "value";

    public static IReadOnlyList<string> Groups { get; } =
        [GroupEngine, GroupTemperatures, GroupElectrical, GroupMixture, GroupIdle, GroupAdaptation, GroupStatus, GroupOther];

    public static IReadOnlyList<string> Widgets { get; } = [WidgetGauge, WidgetBar, WidgetLed, WidgetValue];

    /// <summary>One of <see cref="Groups"/>.</summary>
    public string Group { get; init; } = GroupOther;

    /// <summary>
    /// <c>gauge</c> (round dial, like a rev counter), <c>bar</c> (linear),
    /// <c>led</c> (on/off indicator, bit parameters only) or <c>value</c> (number only).
    /// </summary>
    public string Widget { get; init; } = WidgetValue;

    /// <summary>Scale start for gauges and bars.</summary>
    public double Min { get; init; }

    /// <summary>Scale end for gauges and bars.</summary>
    public double Max { get; init; } = 100;

    /// <summary>For bars: fill from this value instead of from <see cref="Min"/> (corrections around 1.0 or 0).</summary>
    public double? Center { get; init; }

    /// <summary>Coloured ranges on the scale.</summary>
    public IReadOnlyList<DisplayZone> Zones { get; init; } = [];

    /// <summary>For LEDs: colour when on — <c>good</c>, <c>warning</c> or <c>info</c>.</summary>
    public string Led { get; init; } = "good";

    /// <summary>Default for parameters without a display entry.</summary>
    public static DisplaySpec DefaultFor(ParameterDefinition p) =>
        p.Decode.Raw == "bit" ? new DisplaySpec { Group = GroupStatus, Widget = WidgetLed } : new DisplaySpec();
}

/// <summary>A coloured range of a scale; <c>level</c> is <c>cold</c>, <c>warning</c> or <c>danger</c>.</summary>
public sealed record DisplayZone(double From, double To, string Level);

/// <summary>Text in several languages, keyed by culture code ("en", "it"...); falls back to English.</summary>
public sealed class LocalizedText : Dictionary<string, string>
{
    public LocalizedText() : base(StringComparer.OrdinalIgnoreCase)
    {
    }

    public string Get(CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentUICulture;
        if (culture.Name.Length > 0 && TryGetValue(culture.TwoLetterISOLanguageName, out var text))
            return text;
        return TryGetValue("en", out var en) ? en : Values.FirstOrDefault() ?? "";
    }
}

/// <summary>Background information about an ECU family, shown next to the detected vehicle.</summary>
public sealed class FamilyInfo
{
    public LocalizedText? Technology { get; init; }

    /// <summary>Indicative production period, for example <c>1995–2001</c>.</summary>
    public string? Period { get; init; }

    /// <summary>Vehicles and engines that use this family, beyond the ISO table.</summary>
    public IReadOnlyList<CompatibleVehicle> Compatibility { get; init; } = [];

    /// <summary>
    /// False while the compatibility list has not been confirmed with real
    /// units: the application then labels it as indicative.
    /// </summary>
    public bool CompatibilityVerified { get; init; }

    public LocalizedText? Notes { get; init; }
}

public sealed record CompatibleVehicle(string Brand, string Model, string? Engines);
