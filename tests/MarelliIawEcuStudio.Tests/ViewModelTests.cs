using System.Globalization;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Presentation.Services;
using MarelliIawEcuStudio.Presentation.ViewModels;
using MarelliIawEcuStudio.Simulator;

namespace MarelliIawEcuStudio.Tests;

internal sealed class FakeDialogs : IDialogService
{
    public bool ConfirmAnswer { get; set; } = true;

    public byte? ByteAnswer { get; set; }

    public List<string> Confirmations { get; } = [];

    public List<string> Messages { get; } = [];

    public byte? LastByteCurrent { get; private set; }

    public string? FileToOpen { get; set; }

    public Task<bool> ConfirmAsync(string title, string message)
    {
        Confirmations.Add(message);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task<byte?> AskByteAsync(string title, byte current)
    {
        LastByteCurrent = current;
        return Task.FromResult(ByteAnswer);
    }

    public Task<string?> PickLogFileAsync(string? startFolder) => Task.FromResult(FileToOpen);

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}

/// <summary>Simulator with a given ISO code.</summary>
internal sealed class IsoFactory(string iso) : ITransportFactory
{
    public IReadOnlyList<ConnectionTarget> ListTargets() =>
        [new ConnectionTarget(ConnectionTarget.SimulatorId, "Simulator", "", true)];

    public ITransport Create(ConnectionTarget target, EcuDefinition simulatedEcu, bool broadcastIso, bool dtrEnable) =>
        new SimulatorTransport(new SimulatedEcu(DefinitionRegistry.BuiltIn().Get("IAW-8F/18F"), new SimulatedEcuOptions { Iso = iso }));
}

/// <summary>
/// Like a UI dispatcher: posted actions queue up and run on the test thread
/// when it pumps, so view model state is only touched by one thread.
/// </summary>
internal sealed class QueueDispatcher : IUiDispatcher
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue = new();

    public void Post(Action action) => _queue.Enqueue(action);

    // Dialog fakes are thread-safe, and the caller is a background thread waiting for the answer.
    public Task<T> InvokeAsync<T>(Func<Task<T>> action) => action();

    public void Pump()
    {
        while (_queue.TryDequeue(out var a))
            a();
    }
}

/// <summary>Only the simulator, configured by the test.</summary>
internal sealed class SimulatorFactory(Action<SimulatedEcu>? setup = null, EcuDefinition? family = null) : ITransportFactory
{
    public SimulatorTransport? Last { get; private set; }

    public IReadOnlyList<ConnectionTarget> ListTargets() =>
        [new ConnectionTarget(ConnectionTarget.SimulatorId, "Simulator", "", true)];

    public ITransport Create(ConnectionTarget target, EcuDefinition simulatedEcu, bool broadcastIso, bool dtrEnable)
    {
        var ecu = new SimulatedEcu(family ?? simulatedEcu, new SimulatedEcuOptions { BroadcastIso = broadcastIso, HasImmobilizer = true });
        setup?.Invoke(ecu);
        return Last = new SimulatorTransport(ecu);
    }
}

/// <summary>The view models driven like a user would, against the simulator.</summary>
[Collection(nameof(UiCultureCollection))]
public sealed class ViewModelTests : IAsyncLifetime
{
    private static readonly DefinitionRegistry Registry = DefinitionRegistry.BuiltIn();
    private readonly string _logDir = Directory.CreateTempSubdirectory("iaw-vm-").FullName;
    private MainViewModel? _vm;
    private readonly QueueDispatcher _ui = new();

    public ValueTask InitializeAsync()
    {
        Ui.Instance.SetLanguage("en");
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_vm is not null)
            await _vm.DisposeAsync();
        Ui.Instance.SetLanguage("en");
        Directory.Delete(_logDir, recursive: true);
    }

    private MainViewModel Create(SimulatorFactory factory, FakeDialogs dialogs, string ecu = "IAW-8F/18F", MemorySettingsStore? settings = null)
    {
        settings ??= new MemorySettingsStore(new AppSettings { EcuId = ecu, ListenForIsoBroadcast = false, LogFolder = _logDir });
        _vm = new MainViewModel(Registry, factory, settings, dialogs, _ui, ProtocolTiming.Fast);
        return _vm;
    }

    private void WaitFor(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            _ui.Pump();
            if (condition())
                return;
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for " + what);
            Thread.Sleep(5);
        }
    }

    [Fact]
    public async Task ConnectsAndShowsLiveValues()
    {
        var factory = new SimulatorFactory(ecu => ecu.SetValue("PERIODE", 2000));
        var vm = Create(factory, new FakeDialogs());
        Assert.True(vm.ConnectCommand.CanExecute(null));

        await vm.ConnectCommand.ExecuteAsync(null);

        Assert.Equal(ConnectionState.Connected, vm.State);
        Assert.Contains("IAW-8F/18F", vm.EcuName);
        Assert.Equal("Punto 75 1.2 Fire 8V ECE F2", vm.Vehicle);
        Assert.Equal(24, vm.Parameters.Count);
        var rpm = vm.Parameters.Single(p => p.Id == "PERIODE");
        WaitFor(() => rpm.ValueText == "2000", "engine speed");
        WaitFor(() => vm.RateText.Length > 0, "refresh rate");
        WaitFor(() => vm.Graph.VisibleSeries.Count == 4 && vm.Graph.VisibleSeries[0].Xs.Count > 0, "graph points");
        Assert.False(vm.ConnectCommand.CanExecute(null));

        await vm.DisconnectCommand.ExecuteAsync(null);
        Assert.Equal(ConnectionState.Disconnected, vm.State);
    }

    [Fact]
    public async Task UnselectedParametersAreNotPolled()
    {
        var vm = Create(new SimulatorFactory(), new FakeDialogs());
        await vm.ConnectCommand.ExecuteAsync(null);
        vm.SelectNoneCommand.Execute(null);
        Assert.All(vm.Parameters, p => Assert.False(p.IsSelected));
        Assert.All(vm.Parameters, p => Assert.False(p.IsPlotted));
        var battery = vm.Parameters.Single(p => p.Id == "M_VBATT");
        battery.IsPlotted = true;
        Assert.True(battery.IsSelected);
        WaitFor(() => battery.ValueText.Length > 0, "battery value");
        _ui.Pump();
        Assert.All(vm.Parameters.Where(p => p != battery), p => Assert.Equal("", p.ValueText));
    }

    [Fact]
    public async Task ShowsErrors()
    {
        var factory = new SimulatorFactory(ecu => ecu.SetError("ErrTPS", active: true, stored: true));
        var vm = Create(factory, new FakeDialogs());
        await vm.ConnectCommand.ExecuteAsync(null);
        WaitFor(() => vm.HasErrors, "errors");
        var error = Assert.Single(vm.Errors);
        Assert.True(error.IsActive);
        Assert.Contains("[A]", error.Status);
    }

    [Fact]
    public async Task ActuatorTestNeedsConfirmation()
    {
        var dialogs = new FakeDialogs { ConfirmAnswer = false };
        var factory = new SimulatorFactory();
        var vm = Create(factory, dialogs);
        await vm.ConnectCommand.ExecuteAsync(null);
        var pump = vm.Tests[0];

        await vm.RunTestCommand.ExecuteAsync(pump);
        Assert.Single(dialogs.Confirmations);
        Assert.Contains(pump.Name, dialogs.Confirmations[0]);
        Assert.Empty(factory.Last!.Ecu.ActiveLog);

        dialogs.ConfirmAnswer = true;
        await vm.RunTestCommand.ExecuteAsync(pump);
        Assert.True(pump.Passed);
        Assert.Equal("PASSED", pump.Result);
        Assert.Equal(ConnectionState.Connected, vm.State);
    }

    [Fact]
    public async Task ActiveCommandsCanBeDisabled()
    {
        var dialogs = new FakeDialogs();
        var factory = new SimulatorFactory();
        var vm = Create(factory, dialogs);
        vm.AllowActiveCommands = false;
        await vm.ConnectCommand.ExecuteAsync(null);
        await vm.ClearCodesCommand.ExecuteAsync(null);
        Assert.Single(dialogs.Messages);
        Assert.Empty(dialogs.Confirmations);
        Assert.Empty(factory.Last!.Ecu.ActiveLog);
    }

    [Fact]
    public async Task OneByteAdjustmentAsksForTheValue()
    {
        var dialogs = new FakeDialogs { ByteAnswer = 140 };
        var factory = new SimulatorFactory(ecu => ecu.Registers[0x0F] = 128);
        var vm = Create(factory, dialogs, "IAW-6F/16F");
        await vm.ConnectCommand.ExecuteAsync(null);
        var trim = vm.Adjustments.Single(a => a.Adjustment!.NameKey == "TrimSet");

        await vm.RunAdjustmentCommand.ExecuteAsync(trim);

        Assert.Equal((byte)128, dialogs.LastByteCurrent);
        Assert.Equal(140, factory.Last!.Ecu.Registers[0x0F]);
        Assert.True(trim.Passed);
    }

    [Fact]
    public async Task RecordsAndReplaysALog()
    {
        var vm = Create(new SimulatorFactory(ecu => ecu.SetValue("PERIODE", 1500)), new FakeDialogs());
        await vm.ConnectCommand.ExecuteAsync(null);
        vm.ToggleRecordingCommand.Execute(null);
        Assert.True(vm.IsRecording);
        var rpm = vm.Parameters.Single(p => p.Id == "PERIODE");
        WaitFor(() => rpm.ValueText == "1500", "values while recording");
        vm.SelectNoneCommand.Execute(null); // recorded columns keep being polled
        await Task.Delay(300, TestContext.Current.CancellationToken);
        vm.ToggleRecordingCommand.Execute(null);
        Assert.False(vm.IsRecording);
        Assert.True(File.Exists(vm.LastRecording));

        await vm.OpenLastRecordingCommand.ExecuteAsync(null);
        var replay = vm.Replay;
        Assert.True(replay.IsLoaded);
        Assert.Equal(24, replay.Columns.Count);
        Assert.Contains("IAW-8F/18F", replay.Summary);
        Assert.Equal("1500", replay.Columns[0].ValueText);
        var log = Core.Logging.CsvLogReader.Read(vm.LastRecording!);
        Assert.DoesNotContain(log.Samples, sample => sample.Values.Any(double.IsNaN));
        Assert.Equal(4, replay.VisibleSeries.Count);

        replay.Speed = 2;
        replay.TogglePlayCommand.Execute(null);
        Assert.True(replay.IsPlaying);
        replay.Tick(TimeSpan.FromMilliseconds(50));
        Assert.Equal(100, replay.PositionMs, 0);
        replay.Tick(TimeSpan.FromHours(1));
        Assert.False(replay.IsPlaying);
        Assert.Equal(replay.DurationMs, replay.PositionMs);

        replay.PositionMs = 0; // scrub back
        Assert.Equal(0, replay.CursorSeconds);
    }

    [Fact]
    public async Task ReplayReportsUnreadableFiles()
    {
        var dialogs = new FakeDialogs { FileToOpen = Path.Combine(_logDir, "missing.csv") };
        var vm = Create(new SimulatorFactory(), dialogs);
        await vm.Replay.OpenCommand.ExecuteAsync(null);
        Assert.False(vm.Replay.IsLoaded);
        Assert.Single(dialogs.Messages);
    }

    [Fact]
    public async Task LostConnectionCanBeRecovered()
    {
        var factory = new SimulatorFactory();
        var settings = new MemorySettingsStore(new AppSettings { EcuId = "IAW-8F/18F", ListenForIsoBroadcast = false, LogFolder = _logDir, AutoReconnect = false });
        var vm = Create(factory, new FakeDialogs(), settings: settings);
        await vm.ConnectCommand.ExecuteAsync(null);
        factory.Last!.EcuConnected = false;
        WaitFor(() => vm.State == ConnectionState.Lost, "connection loss");
        Assert.StartsWith("No ECU Response", vm.StatusText);
        Assert.Contains("ECU does not answer", vm.StatusText);
        Assert.True(vm.ReconnectCommand.CanExecute(null));

        factory.Last.EcuConnected = true;
        factory.Last.Ecu.Reset(); // ECU needs a new init, as after a real dropout
        await vm.ReconnectCommand.ExecuteAsync(null);
        Assert.Equal(ConnectionState.Connected, vm.State);
        var rpm = vm.Parameters.Single(p => p.Id == "PERIODE");
        WaitFor(() => rpm.IsValid && rpm.ValueText != "#ERR", "values after reconnect");
    }

    [Fact]
    public async Task LostConnectionIsRestoredAutomatically()
    {
        var factory = new SimulatorFactory();
        var vm = Create(factory, new FakeDialogs());
        Assert.True(vm.AutoReconnect); // on by default
        await vm.ConnectCommand.ExecuteAsync(null);
        var rpm = vm.Parameters.Single(p => p.Id == "PERIODE");

        factory.Last!.EcuConnected = false;
        WaitFor(() => vm.State == ConnectionState.Lost, "connection loss");
        WaitFor(() => vm.StatusText.Contains("reconnecting, attempt", StringComparison.Ordinal), "reconnection attempts");

        factory.Last.Ecu.Reset(); // ignition cycled
        factory.Last.Ecu.SetValue("PERIODE", 3000);
        factory.Last.EcuConnected = true;
        WaitFor(() => vm.State == ConnectionState.Connected, "automatic reconnection");
        Assert.Contains("reconnected", vm.StatusText);
        WaitFor(() => rpm.ValueText == "3000", "values after reconnection");
    }

    [Fact]
    public async Task LineTraceIsSavedForEachConnection()
    {
        var settings = new MemorySettingsStore(new AppSettings { EcuId = "IAW-8F/18F", ListenForIsoBroadcast = false, LogFolder = _logDir, TraceLine = true });
        var vm = Create(new SimulatorFactory(), new FakeDialogs(), settings: settings);
        await vm.ConnectCommand.ExecuteAsync(null);
        var file = vm.TraceFile;
        Assert.NotNull(file);
        Assert.Contains(file, vm.RecordingText);
        WaitFor(() => vm.RateText.Length > 0, "polling");
        await vm.DisconnectCommand.ExecuteAsync(null);

        var trace = Core.Logging.LineTraceReader.Read(file);
        Assert.True(trace.RequestCount > 10);
        Assert.Contains(trace.Events, e => e.Note?.StartsWith("identified", StringComparison.Ordinal) == true);
        Assert.Contains("# line:", File.ReadAllText(file));
    }

    [Fact]
    public async Task UnrecognizedEcuIsReported()
    {
        // Auto detection, and the simulated IAW-04K does not broadcast its ISO code.
        var factory = new SimulatorFactory(family: Registry.Get("IAW-04K.P8"));
        var settings = new MemorySettingsStore(new AppSettings { EcuId = null, ListenForIsoBroadcast = false, LogFolder = _logDir });
        var vm = Create(factory, new FakeDialogs(), settings: settings);
        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.Equal(ConnectionState.Disconnected, vm.State);
        Assert.Contains("could not be recognized", vm.StatusText);
    }

    [Fact]
    public async Task LanguageSwitchTranslatesParameterNames()
    {
        var vm = Create(new SimulatorFactory(), new FakeDialogs());
        await vm.ConnectCommand.ExecuteAsync(null);
        var rpm = vm.Parameters.Single(p => p.Id == "PERIODE");
        Assert.Equal("Engine Speed", rpm.Name);
        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "it");
        Assert.NotEqual("Engine Speed", rpm.Name);
        Assert.Equal("Parametri", vm.Ui["TabParameters"]);
        Assert.Equal("it", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void ThemeFollowsTheSystemByDefaultCyclesAndIsSaved()
    {
        var settings = new MemorySettingsStore(new AppSettings { EcuId = "IAW-8F/18F", ListenForIsoBroadcast = false, LogFolder = _logDir });
        var vm = Create(new SimulatorFactory(), new FakeDialogs(), settings: settings);
        Assert.True(vm.IsThemeSystem);
        Assert.Equal(["system", "light", "dark"], vm.ThemeChoices.Select(t => t.Code));

        vm.CycleThemeCommand.Execute(null);
        Assert.True(vm.IsThemeLight);
        vm.CycleThemeCommand.Execute(null);
        Assert.True(vm.IsThemeDark);
        Assert.Equal("dark", settings.Current.Theme);
        Assert.Contains("Dark", vm.ThemeToolTip);
        vm.CycleThemeCommand.Execute(null);
        Assert.True(vm.IsThemeSystem);

        // The choice survives a restart; an unknown value falls back to the system theme.
        vm.SelectedTheme = new ThemeOption(ThemeOption.Dark);
        Assert.True(new MainViewModel(Registry, new SimulatorFactory(), settings, new FakeDialogs(), _ui, ProtocolTiming.Fast).IsThemeDark);
        var odd = new MemorySettingsStore(settings.Current with { Theme = "sepia" });
        Assert.True(new MainViewModel(Registry, new SimulatorFactory(), odd, new FakeDialogs(), _ui, ProtocolTiming.Fast).IsThemeSystem);
    }

    [Fact]
    public async Task DashboardGroupsParametersByInstrument()
    {
        var vm = Create(new SimulatorFactory(ecu => ecu.SetValue("S2", 1)), new FakeDialogs());
        await vm.ConnectCommand.ExecuteAsync(null);
        var keys = vm.ParameterGroups.Select(g => g.Key).ToList();
        Assert.Equal(["engine", "temperatures", "electrical", "mixture", "idle", "adaptation", "status"], keys);
        Assert.Equal(vm.Parameters.Count, vm.ParameterGroups.Sum(g => g.Rows.Count));
        var engine = vm.DashboardLeft.Single(g => g.Key == "engine");
        Assert.Equal(["PERIODE", "MP2_MP8"], engine.Gauges.Select(r => r.Id));
        Assert.Equal(["temperatures", "electrical"], vm.DashboardRightPair.Select(g => g.Key));
        var running = vm.ParameterGroups.Single(g => g.Key == "status").Leds.Single(r => r.Decoder.Definition.NameKey == "StaEngRun");
        WaitFor(() => running.IsOn, "engine running LED");
        var knock = vm.ParameterGroups.Single(g => g.Key == "status").Leds.Single(r => r.Decoder.Definition.NameKey == "StaAdvDecr");
        Assert.True(knock.IsLedWarning);
        var battery = vm.Parameters.Single(p => p.Id == "M_VBATT");
        WaitFor(() => double.IsFinite(battery.NumericValue), "numeric value for the gauge");
    }

    [Fact]
    public async Task VehicleCardShowsDetectedCarAndFamily()
    {
        var vm = Create(new SimulatorFactory(), new FakeDialogs());
        Assert.True(vm.VehicleInfo.HasInfo); // family of the selected ECU, before connecting
        Assert.Equal("1993–1999", vm.VehicleInfo.Period);
        Assert.Equal("", vm.VehicleInfo.DetectedVehicle);

        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.True(vm.VehicleInfo.IsDetected);
        Assert.Equal("Punto 75 1.2 Fire 8V ECE F2", vm.VehicleInfo.DetectedVehicle);
        Assert.Contains(vm.VehicleInfo.Brands, b => b.Brand == "Alfa Romeo" && b.Models.Any(m => m.Model == "145 / 146"));
        Assert.Contains("indicative", vm.VehicleInfo.CompatibilityCaption);
        Assert.Contains("Punto 75 1.2 Fire 8V ECE ECOL", vm.VehicleInfo.IsoVehicles);

        vm.SelectedLanguage = vm.Languages.Single(l => l.Code == "it");
        Assert.StartsWith("Iniezione multipoint", vm.VehicleInfo.Technology);
        Assert.Contains("da verificare", vm.VehicleInfo.CompatibilityCaption);
    }

    [Fact]
    public async Task UnknownIsoIsShownOnTheVehicleCard()
    {
        // An ISO code with the family signature but not in the vehicle table.
        var vm = _vm = new MainViewModel(Registry, new IsoFactory("55318099AAAA"), new MemorySettingsStore(new AppSettings { EcuId = null, ListenForIsoBroadcast = false, LogFolder = _logDir }),
            new FakeDialogs(), _ui, ProtocolTiming.Fast);
        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.Equal(ConnectionState.Connected, vm.State);
        Assert.False(vm.VehicleInfo.IsDetected);
        Assert.Contains("55-31-80-99-AA-AA", vm.VehicleInfo.DetectedVehicle);
    }

    [Theory]
    [InlineData("4WF", "IAW-04K.P8")]
    [InlineData("coupe turbo", "IAW-04K.P8")]
    [InlineData("Integrale", "IAW-04K.P8")]
    [InlineData("alfa 33", "IAW-8F.68")]
    [InlineData("Seicento", "IAW-6F/16F")]
    public void EcuSearchFindsFamiliesByCodeOrVehicle(string filter, string expected)
    {
        var vm = Create(new SimulatorFactory(), new FakeDialogs());
        vm.EcuFilter = filter;
        Assert.Null(vm.FilteredEcus[0].Definition); // automatic detection stays available
        Assert.Contains(vm.FilteredEcus, e => e.Definition?.Id == expected);
        Assert.Equal(expected, vm.SelectedEcu.Definition?.Id);
        vm.EcuFilter = "";
        Assert.Equal(vm.Ecus.Count, vm.FilteredEcus.Count);
    }

    [Fact]
    public void EcuEntriesShowAliasesAndVehicles()
    {
        var vm = Create(new SimulatorFactory(), new FakeDialogs());
        var p8 = vm.Ecus.Single(e => e.Definition?.Id == "IAW-04K.P8");
        Assert.Contains("IAW 4WF.P8", p8.Subtitle);
        Assert.Contains("Delta HF Integrale", p8.Subtitle);
        vm.SelectedEcu = p8;
        Assert.Contains("IAW 4WF.P8", vm.VehicleInfo.AliasesText);
        Assert.Contains("Coupé 2.0 Turbo 16V (software P8)", vm.VehicleInfo.AliasesText);
    }

    [Fact]
    public void SettingsArePersisted()
    {
        var settings = new MemorySettingsStore(new AppSettings { LogFolder = _logDir });
        var vm = Create(new SimulatorFactory(), new FakeDialogs(), settings: settings);
        vm.Compatibility = true;
        vm.SelectedEcu = vm.Ecus.Single(e => e.Definition?.Id == "IAW-04K.P8");
        Assert.True(settings.Current.Compatibility);
        Assert.Equal("IAW-04K.P8", settings.Current.EcuId);
        Assert.Equal(_logDir, settings.Current.LogFolder);
    }
}

[CollectionDefinition(nameof(UiCultureCollection), DisableParallelization = true)]
public class UiCultureCollection;
