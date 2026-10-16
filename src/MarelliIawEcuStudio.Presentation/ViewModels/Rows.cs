using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Presentation.Localization;

namespace MarelliIawEcuStudio.Presentation.ViewModels;

/// <summary>A live parameter in the Parameters tab.</summary>
public sealed partial class ParameterRowViewModel : ObservableObject
{
    public ParameterRowViewModel(ParameterDecoder decoder)
    {
        Decoder = decoder;
        RefreshTexts();
    }

    public ParameterDecoder Decoder { get; }

    /// <summary>Dashboard group and instrument of this parameter.</summary>
    public DisplaySpec Display => Decoder.Definition.EffectiveDisplay;

    public bool IsLedWarning => Display.Led == "warning";

    public bool IsLedInfo => Display.Led == "info";

    public string Id => Decoder.Id;

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Unit { get; set; } = "";

    /// <summary>Polled when true.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    /// <summary>Shown in the live graph when true (implies polling).</summary>
    [ObservableProperty]
    public partial bool IsPlotted { get; set; }

    [ObservableProperty]
    public partial string ValueText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsValid { get; set; } = true;

    public decimal? Value { get; private set; }

    /// <summary>Value for the instruments; NaN when unknown.</summary>
    [ObservableProperty]
    public partial double NumericValue { get; set; } = double.NaN;

    /// <summary>For LEDs: the flag is set.</summary>
    [ObservableProperty]
    public partial bool IsOn { get; set; }

    partial void OnIsPlottedChanged(bool value)
    {
        if (value)
            IsSelected = true;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!value)
        {
            IsPlotted = false;
            ValueText = "";
            NumericValue = double.NaN;
            IsOn = false;
        }
    }

    public void Update(decimal? value)
    {
        Value = value;
        IsValid = value is not null;
        ValueText = value is null ? "#ERR" : value.Value.ToString(Decoder.GetFormat(), CultureInfo.CurrentCulture);
        NumericValue = value is null ? double.NaN : (double)value.Value;
        IsOn = value is > 0;
    }

    public void RefreshTexts()
    {
        Name = Decoder.GetName();
        Unit = Decoder.GetUnit();
        if (Value is not null)
            Update(Value);
    }
}

/// <summary>A present error in the Errors tab.</summary>
public sealed record ErrorRowViewModel(string Name, string Status, string Reason, bool IsActive)
{
    public static ErrorRowViewModel From(ErrorState e) => new(e.GetName(), e.GetStatusText(), e.Reason, e.IsActive);
}

/// <summary>An actuator test or an adjustment, with the result of its last run.</summary>
public sealed partial class ActiveItemViewModel : ObservableObject
{
    public ActiveItemViewModel(ActiveTestDefinition test)
    {
        Test = test;
        RefreshTexts();
    }

    public ActiveItemViewModel(AdjustmentDefinition adjustment)
    {
        Adjustment = adjustment;
        RefreshTexts();
    }

    public ActiveTestDefinition? Test { get; }

    public AdjustmentDefinition? Adjustment { get; }

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Detail { get; set; } = "";

    [ObservableProperty]
    public partial string Condition { get; set; } = "";

    [ObservableProperty]
    public partial string Result { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPassed), nameof(IsFailed))]
    public partial bool? Passed { get; set; }

    public bool IsPassed => Passed == true;

    public bool IsFailed => Passed == false;

    public void RefreshTexts()
    {
        if (Test is not null)
        {
            Name = Localizer.Text(Test.NameKey, Test.Name);
            Detail = $"{Test.TimeoutSeconds} s";
            Condition = Ui.Get(Test.EngineRunning ? "EngineRunning" : "EngineStopped");
        }
        else if (Adjustment is not null)
        {
            Name = Localizer.Text(Adjustment.NameKey, Adjustment.Name);
            Detail = Adjustment.Type;
            Condition = Ui.Get("EngineStopped");
        }
    }

    /// <summary>Shows an outcome with the IES2 wording (PASSED, FAILED, TIMEOUT...).</summary>
    public void SetOutcome(ActiveOutcome outcome)
    {
        Passed = outcome == ActiveOutcome.Passed;
        Result = Ui.Get(outcome switch
        {
            ActiveOutcome.Passed => "Passed",
            ActiveOutcome.Timeout => "Timeout",
            ActiveOutcome.InitFailed => "InitFailed",
            ActiveOutcome.Cancelled => "Cancelled",
            _ => "Failed",
        });
    }

    public void SetPending()
    {
        Passed = null;
        Result = Ui.Get("InProceed");
    }
}

/// <summary>An entry of the ECU selector: automatic detection or one family.</summary>
public sealed record EcuChoice(EcuDefinition? Definition)
{
    public string Name => Definition is null
        ? Localizer.Get("autodetect")
        : (Definition.LongName ?? Definition.Name) + (Definition.Verified ? "" : $" ({Ui.Get("Unverified")})");

    /// <summary>Second line: other names of the unit and a few vehicles, so users recognise their ECU.</summary>
    public string Subtitle
    {
        get
        {
            if (Definition is null)
                return Ui.Get("AutoDetectHint");
            var parts = new List<string>();
            if (Definition.Aliases.Count > 0)
                parts.Add(Ui.Get("AlsoKnownAs") + " " + string.Join(", ", Definition.Aliases.Select(a => a.Name)));
            var cars = (Definition.Family?.Compatibility.Select(c => $"{c.Brand} {c.Model}") ?? [])
                .Concat(Definition.Vehicles.Select(v => v.Model)).Distinct().Take(3).ToList();
            if (cars.Count > 0)
                parts.Add(string.Join(", ", cars) + "…");
            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// True when every word of <paramref name="filter"/> appears in the name, an
    /// alias or code, a vehicle model or a brand. Case, accents, dashes and dots are ignored.
    /// </summary>
    public bool Matches(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || Definition is null)
            return true;
        var d = Definition;
        IEnumerable<string> haystack =
        [
            d.Id, d.Name, d.LongName ?? "",
            .. d.Aliases.SelectMany(a => new[] { a.Name, a.Note ?? "" }),
            .. d.Vehicles.Select(v => v.Model),
            .. (d.Family?.Compatibility ?? []).SelectMany(c => new[] { c.Brand + " " + c.Model, c.Engines ?? "" }),
        ];
        // Every word typed must appear somewhere ("coupe turbo", "4wf p8").
        var normalized = haystack.Select(Normalize).ToList();
        return filter.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).Where(w => w.Length > 0)
            .All(w => normalized.Any(h => h.Contains(w, StringComparison.Ordinal)));
    }

    private static string Normalize(string s) =>
        new([.. s.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.IsLetterOrDigit(c))
            .Select(char.ToUpperInvariant)]);

    public override string ToString() => Name;
}
