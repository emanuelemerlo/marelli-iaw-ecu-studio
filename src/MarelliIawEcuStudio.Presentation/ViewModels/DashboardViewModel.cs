using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Presentation.Localization;

namespace MarelliIawEcuStudio.Presentation.ViewModels;

/// <summary>
/// A logical group of parameters on the dashboard (engine, temperatures,
/// electrical...), split by instrument: round gauges, bars, LEDs, values.
/// </summary>
public sealed partial class DashboardGroupViewModel : ObservableObject
{
    public DashboardGroupViewModel(string key, IReadOnlyList<ParameterRowViewModel> rows)
    {
        Key = key;
        Rows = rows;
        Gauges = [.. rows.Where(r => r.Display.Widget == DisplaySpec.WidgetGauge)];
        Bars = [.. rows.Where(r => r.Display.Widget == DisplaySpec.WidgetBar)];
        Leds = [.. rows.Where(r => r.Display.Widget == DisplaySpec.WidgetLed)];
        Values = [.. rows.Where(r => r.Display.Widget == DisplaySpec.WidgetValue)];
        RefreshTexts();
    }

    public string Key { get; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    public IReadOnlyList<ParameterRowViewModel> Rows { get; }

    public IReadOnlyList<ParameterRowViewModel> Gauges { get; }

    public IReadOnlyList<ParameterRowViewModel> Bars { get; }

    public IReadOnlyList<ParameterRowViewModel> Leds { get; }

    public IReadOnlyList<ParameterRowViewModel> Values { get; }

    public bool HasGauges => Gauges.Count > 0;

    public bool HasBars => Bars.Count > 0;

    public bool HasLeds => Leds.Count > 0;

    public bool HasValues => Values.Count > 0;

    public void RefreshTexts() => Title = Ui.Get("Group_" + Key);

    /// <summary>Groups the rows in the fixed dashboard order, skipping empty groups.</summary>
    public static IReadOnlyList<DashboardGroupViewModel> Build(IEnumerable<ParameterRowViewModel> rows)
    {
        var list = rows.ToList();
        return [.. DisplaySpec.Groups
            .Select(g => new DashboardGroupViewModel(g, [.. list.Where(r => r.Display.Group == g)]))
            .Where(g => g.Rows.Count > 0)];
    }
}

/// <summary>A brand with its compatible models, for the vehicle card.</summary>
public sealed record BrandVehiclesViewModel(string Brand, IReadOnlyList<CompatibleVehicle> Models);

/// <summary>
/// The vehicle card: the car recognised from the ISO code (from the IES2
/// tables), plus technology, period and the indicative list of compatible
/// vehicles of the ECU family.
/// </summary>
public sealed partial class VehicleInfoViewModel : ObservableObject
{
    private EcuDefinition? _definition;
    private ConnectionInfo? _connection;

    [ObservableProperty]
    public partial bool HasInfo { get; set; }

    [ObservableProperty]
    public partial string EcuName { get; set; } = "";

    /// <summary>Model recognised from the ISO code, or a note that the code is not in the table.</summary>
    [ObservableProperty]
    public partial string DetectedVehicle { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDetected { get; set; }

    [ObservableProperty]
    public partial string Technology { get; set; } = "";

    /// <summary>Other names of the unit (label codes), or empty.</summary>
    [ObservableProperty]
    public partial string AliasesText { get; set; } = "";

    [ObservableProperty]
    public partial string Period { get; set; } = "";

    [ObservableProperty]
    public partial string Notes { get; set; } = "";

    [ObservableProperty]
    public partial string CompatibilityCaption { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<BrandVehiclesViewModel> Brands { get; set; } = [];

    /// <summary>Models of the IES2 ISO table (recognised automatically).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> IsoVehicles { get; set; } = [];

    /// <summary>Shows a family before connecting (selected in the ECU list).</summary>
    public void ShowFamily(EcuDefinition? definition)
    {
        _definition = definition;
        _connection = null;
        Refresh();
    }

    /// <summary>Shows the connected ECU and the recognised vehicle.</summary>
    public void ShowConnection(ConnectionInfo connection)
    {
        _definition = connection.Definition;
        _connection = connection;
        Refresh();
    }

    public void Refresh()
    {
        var d = _definition;
        HasInfo = d is not null;
        if (d is null)
            return;
        var culture = CultureInfo.CurrentUICulture;
        EcuName = d.LongName ?? d.Name;
        Technology = d.Family?.Technology?.Get(culture) ?? "";
        AliasesText = d.Aliases.Count == 0 ? "" : Ui.Get("AlsoKnownAs") + " " +
            string.Join("; ", d.Aliases.Select(a => a.Note is null ? a.Name : $"{a.Name} ({a.Note})"));
        Period = d.Family?.Period ?? "";
        Notes = d.Family?.Notes?.Get(culture) ?? "";
        CompatibilityCaption = Ui.Get(d.Family?.CompatibilityVerified == true ? "CompatibleVehicles" : "CompatibleVehiclesIndicative");
        Brands = [.. (d.Family?.Compatibility ?? [])
            .GroupBy(c => c.Brand)
            .Select(g => new BrandVehiclesViewModel(g.Key, [.. g]))];
        IsoVehicles = [.. d.Vehicles.Select(v => v.Model).Distinct()];
        if (_connection is null)
        {
            DetectedVehicle = "";
            IsDetected = false;
        }
        else
        {
            IsDetected = _connection.VehicleModel is not null;
            DetectedVehicle = _connection.VehicleModel ?? Ui.Format("VehicleNotInTable", _connection.FormattedIso);
        }
    }
}
