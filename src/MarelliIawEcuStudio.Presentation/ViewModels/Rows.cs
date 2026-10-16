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
        }
    }

    public void Update(decimal? value)
    {
        Value = value;
        IsValid = value is not null;
        ValueText = value is null ? "#ERR" : value.Value.ToString(Decoder.GetFormat(), CultureInfo.CurrentCulture);
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

    public override string ToString() => Name;
}
