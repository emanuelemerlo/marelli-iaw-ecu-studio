using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MarelliIawEcuStudio.App.Services;
using MarelliIawEcuStudio.App.Views;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Transport;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Presentation.Services;
using MarelliIawEcuStudio.Presentation.ViewModels;
using MarelliIawEcuStudio.Simulator;

namespace MarelliIawEcuStudio.App.Tests;

/// <summary>Entry point used by the headless session: the real App, rendered with Skia, no window system.</summary>
public static class HeadlessApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont();
}

internal sealed class AutoDialogs : IDialogService
{
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(true);

    public Task<byte?> AskByteAsync(string title, byte current) => Task.FromResult<byte?>(current);

    public Task<string?> PickLogFileAsync(string? startFolder) => Task.FromResult<string?>(null);

    public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
}

internal sealed class SimulatorOnly : ITransportFactory
{
    public IReadOnlyList<ConnectionTarget> ListTargets() =>
        [new ConnectionTarget("/dev/ttyUSB0", "/dev/ttyUSB0", "FTDI, FT232R USB UART", false), new ConnectionTarget(ConnectionTarget.SimulatorId, "Simulator", "demo, no cable needed", true)];

    public ITransport Create(ConnectionTarget target, EcuDefinition simulatedEcu, bool broadcastIso, bool dtrEnable)
    {
        var ecu = new SimulatedEcu(simulatedEcu, new SimulatedEcuOptions { BroadcastIso = broadcastIso, HasImmobilizer = true });
        ecu.SetError("ErrECT", stored: true, highReason: true);
        ecu.SetError("ErrLambda", active: true);
        return new SimulatorTransport(ecu) { Scenario = EngineScenario.Drive(50) };
    }
}

/// <summary>
/// The real main window, rendered headless against the simulator. Screenshots
/// go to the folder in SCREENSHOT_DIR (default: the test output folder).
/// </summary>
public sealed class MainWindowTests : IAsyncLifetime
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(HeadlessApp));
    private readonly string _logDir = Directory.CreateTempSubdirectory("iaw-ui-").FullName;

    private static string ScreenshotDir =>
        Environment.GetEnvironmentVariable("SCREENSHOT_DIR") is { Length: > 0 } dir ? dir : Path.Combine(AppContext.BaseDirectory, "screenshots");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(_logDir, recursive: true);
        return ValueTask.CompletedTask;
    }

    private (MainWindow Window, MainViewModel Vm) Open(string language = "en", string ecu = "IAW-8F/18F", int width = 1180, int height = 780)
    {
        var settings = new MemorySettingsStore(new AppSettings
        {
            Target = ConnectionTarget.SimulatorId,
            EcuId = ecu,
            ListenForIsoBroadcast = false,
            LogFolder = _logDir,
            Language = language,
        });
        var window = new MainWindow { Width = width, Height = height };
        var vm = new MainViewModel(DefinitionRegistry.BuiltIn(), new SimulatorOnly(), settings, new AutoDialogs(), new AvaloniaDispatcher(),
            ProtocolTiming.Fast with { PollingDelayMs = 5 });
        window.DataContext = vm;
        window.Show();
        return (window, vm);
    }

    private static async Task Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for " + what);
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static async Task Settle(Window window, int ms = 400)
    {
        await Task.Delay(ms);
        Dispatcher.UIThread.RunJobs();
        foreach (var plot in window.GetVisualDescendants().OfType<PlotView>())
            plot.RenderIfDirty();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window window, string name)
    {
        Directory.CreateDirectory(ScreenshotDir);
        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame rendered");
#pragma warning disable CS0618 // the PNG default of the old overload is what we want
        frame.Save(Path.Combine(ScreenshotDir, name + ".png"));
#pragma warning restore CS0618
    }

    private static List<string> Texts(Control root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()
            .SelectMany(t => t.Inlines is { Count: > 0 } inlines
                ? inlines.OfType<Avalonia.Controls.Documents.Run>().Select(r => r.Text ?? "")
                : [t.Text ?? ""])
            .Where(t => t.Length > 0)];

    [Fact]
    public Task ShowsEveryTabWithLiveSimulatorData() => Session.Dispatch(async () =>
    {
        Ui.Instance.SetLanguage("en");
        var (window, vm) = Open();
        var tabs = window.FindControl<TabControl>("Tabs")!;
        await Settle(window);
        Shot(window, "00-disconnected");
        Assert.Contains("1993–1999", Texts(window)); // family card for the selected ECU, before connecting
        Assert.Contains("Not connected", Texts(window));

        await vm.ConnectCommand.ExecuteAsync(null);
        var rpm = vm.Parameters.Single(p => p.Id == "PERIODE");
        await Until(() => rpm.ValueText.Length > 0 && vm.HasErrors, "live values and errors");
        tabs.SelectedIndex = 0;
        await Settle(window, 1500);
        var dashboard = Texts(window);
        Assert.Contains("Punto 75 1.2 Fire 8V ECE F2", dashboard);
        Assert.Contains("1993–1999", dashboard);
        Assert.Contains("Engine", dashboard);
        Assert.NotEmpty(window.GetVisualDescendants().OfType<ArcGauge>());
        Assert.NotEmpty(window.GetVisualDescendants().OfType<LinearGauge>());
        Assert.Contains(window.GetVisualDescendants().OfType<Border>(), b => b.Classes.Contains("led") && b.Classes.Contains("on"));
        Shot(window, "00-dashboard");
        var expander = window.GetVisualDescendants().OfType<Expander>().First();
        expander.IsExpanded = true;
        await Settle(window);
        Assert.Contains("145 / 146", Texts(window));
        Shot(window, "00-vehicles");
        expander.IsExpanded = false;

        tabs.SelectedIndex = 1;
        await Settle(window);
        var texts = Texts(window);
        Assert.Contains("Engine Speed", texts);
        Assert.Contains(texts, t => t.Contains("IAW-8F/18F"));
        Assert.Contains("Simulation", texts);
        Shot(window, "01-parameters");

        tabs.SelectedIndex = 2;
        await Settle(window);
        Assert.Contains(Texts(window), t => t.Contains("[A]"));
        Shot(window, "02-errors");

        tabs.SelectedIndex = 3;
        await Settle(window);
        await vm.RunTestCommand.ExecuteAsync(vm.Tests[0]);
        await Settle(window);
        Assert.Contains("PASSED", Texts(window));
        Shot(window, "03-tests");

        tabs.SelectedIndex = 4;
        await Settle(window);
        Shot(window, "04-adjustments");

        tabs.SelectedIndex = 5;
        await Settle(window, 2500); // let the graph fill
        var plot = window.FindControl<PlotView>("LivePlot")!;
        Assert.Equal(4, plot.Plot.Plot.GetPlottables().Count(p => p is ScottPlot.Plottables.Scatter));
        foreach (var line in plot.Plot.Plot.GetPlottables().OfType<ScottPlot.Plottables.Scatter>())
        {
            var limits = line.GetAxisLimits();
            var axis = line.Axes.YAxis;
            Assert.True(axis.Min <= limits.Bottom && axis.Max >= limits.Top,
                $"{line.LegendText}: data {limits.Bottom}..{limits.Top} outside axis {axis.Min}..{axis.Max}");
        }
        Shot(window, "05-graphs");

        vm.ToggleRecordingCommand.Execute(null);
        await Settle(window, 1500);
        vm.ToggleRecordingCommand.Execute(null);
        await vm.OpenLastRecordingCommand.ExecuteAsync(null);
        vm.Replay.PositionMs = vm.Replay.DurationMs / 2;
        tabs.SelectedIndex = 6;
        await Settle(window);
        Assert.True(vm.Replay.IsLoaded);
        Shot(window, "06-replay");

        await vm.DisconnectCommand.ExecuteAsync(null);
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SwitchesToItalian() => Session.Dispatch(async () =>
    {
        var (window, vm) = Open("it");
        await vm.ConnectCommand.ExecuteAsync(null);
        await Until(() => vm.Parameters.Any(p => p.ValueText.Length > 0), "live values");
        await Settle(window);
        var texts = Texts(window);
        Assert.Contains("Connesso: Magneti-Marelli IAW-8F/18F MPI", texts);
        Assert.DoesNotContain("Engine Speed", texts);
        Shot(window, "07-italiano");
        await vm.DisconnectCommand.ExecuteAsync(null);
        window.Close();
        Ui.Instance.SetLanguage("en");
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ShowsTheTurboDashboardOfTheIaw04k() => Session.Dispatch(async () =>
    {
        Ui.Instance.SetLanguage("it");
        var (window, vm) = Open("it", "IAW-04K.P8");
        await vm.ConnectCommand.ExecuteAsync(null);
        await Until(() => vm.Parameters.Any(p => p.ValueText.Length > 0), "live values");
        await Settle(window, 6000); // into the acceleration of the drive scenario
        var texts = Texts(window);
        Assert.Contains("1989–1996", texts);
        Assert.Contains(vm.DashboardLeft.SelectMany(g => g.Gauges), r => r.Id == "MP2_MP8t");
        Assert.Equal(["temperatures", "electrical"], vm.DashboardRightPair.Select(g => g.Key));
        Shot(window, "08-cruscotto-04k");
        await vm.DisconnectCommand.ExecuteAsync(null);
        window.Close();
        Ui.Instance.SetLanguage("en");
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FindsTheEcuByItsLabelCode() => Session.Dispatch(async () =>
    {
        Ui.Instance.SetLanguage("it");
        var (window, vm) = Open("it", "IAW-8F/18F");
        vm.EcuFilter = "4WF";
        await Settle(window);
        Assert.Equal("IAW-04K.P8", vm.SelectedEcu.Definition?.Id);
        var texts = Texts(window);
        Assert.Contains(texts, t => t.Contains("IAW 4WF.P8"));
        Shot(window, "09-ricerca-4wf");
        window.Close();
        Ui.Instance.SetLanguage("en");
        return true;
    }, TestContext.Current.CancellationToken);

    /// <summary>The dashboard fits the window without scrolling, from a small laptop to a full HD screen.</summary>
    [Theory]
    [InlineData(1024, 640)]
    [InlineData(1180, 780)]
    [InlineData(1920, 1080)]
    public Task DashboardFitsTheWindow(int width, int height) => Session.Dispatch(async () =>
    {
        Ui.Instance.SetLanguage("en");
        var (window, vm) = Open(width: width, height: height);
        await vm.ConnectCommand.ExecuteAsync(null);
        await Until(() => vm.Parameters.Any(p => p.ValueText.Length > 0), "live values");
        await Settle(window, 1000);
        var fit = window.FindControl<FitToViewport>("DashboardFit")!;
        var viewer = fit.FindAncestorOfType<ScrollViewer>()!;
        Assert.InRange(fit.Scale, fit.MinScale, fit.MaxScale);
        // It scrolls only when fitting would make the text too small to read.
        Assert.True(viewer.Extent.Height <= viewer.Viewport.Height + 1 || fit.Scale <= fit.MinScale + 0.001,
            $"{width}x{height}: dashboard {viewer.Extent.Height:0} px high in a {viewer.Viewport.Height:0} px window (scale {fit.Scale:0.00})");
        if (width >= 1180)
            Assert.True(viewer.Extent.Height <= viewer.Viewport.Height + 1, $"{width}x{height}: the dashboard scrolls");
        if (width >= 1920)
            Assert.True(fit.Scale > 1, "a big screen enlarges the instruments");
        Shot(window, $"10-dashboard-{width}x{height}");

        window.FindControl<TabControl>("Tabs")!.SelectedIndex = 1;
        await Settle(window);
        var columns = window.GetVisualDescendants().OfType<BalancedColumns>().First();
        Assert.True(columns.Columns >= 2, "parameters use more than one column on a wide window");
        Shot(window, $"11-parameters-{width}x{height}");
        await vm.DisconnectCommand.ExecuteAsync(null);
        window.Close();
        return true;
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DarkThemeColoursEveryPart() => Session.Dispatch(async () =>
    {
        Ui.Instance.SetLanguage("it");
        var (window, vm) = Open("it", "IAW-04K.P8");
        try
        {
            vm.SelectedTheme = new ThemeOption(ThemeOption.Dark);
            await vm.ConnectCommand.ExecuteAsync(null);
            await Until(() => vm.Parameters.Any(p => p.ValueText.Length > 0), "live values");
            var tabs = window.FindControl<TabControl>("Tabs")!;
            await Settle(window, 1500);
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
            var card = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("card"));
            Assert.True(card.Background is ISolidColorBrush { Color.R: < 0x40 }, "cards are dark");
            Shot(window, "12-dark-dashboard");

            tabs.SelectedIndex = 1;
            await Settle(window);
            Shot(window, "13-dark-parameters");

            vm.Graph.SetPlotted(vm.Parameters.Take(3));
            tabs.SelectedIndex = 5;
            await Settle(window, 2000);
            var plot = window.FindControl<PlotView>("LivePlot")!.Plot.Plot;
            Assert.True(plot.FigureBackground.Color.R < 0x40, "the graph follows the dark theme");
            Shot(window, "14-dark-graphs");

            // Back to light without reopening the window.
            vm.CycleThemeCommand.Execute(null); // dark → system (light on the headless platform)
            await Settle(window);
            Assert.NotEqual(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.True(plot.FigureBackground.Color.R > 0xC0, "the graph follows the light theme again");
            await vm.DisconnectCommand.ExecuteAsync(null);
        }
        finally
        {
            vm.SelectedTheme = new ThemeOption(ThemeOption.System);
            window.Close();
            Ui.Instance.SetLanguage("en");
        }
        return true;
    }, TestContext.Current.CancellationToken);
}
