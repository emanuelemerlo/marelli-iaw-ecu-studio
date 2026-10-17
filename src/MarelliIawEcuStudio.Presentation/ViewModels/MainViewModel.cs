using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarelliIawEcuStudio.Core.Decoding;
using MarelliIawEcuStudio.Core.Definitions;
using MarelliIawEcuStudio.Core.Localization;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Core.Protocol;
using MarelliIawEcuStudio.Core.Session;
using MarelliIawEcuStudio.Presentation.Live;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Presentation.Services;

namespace MarelliIawEcuStudio.Presentation.ViewModels;

public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Lost,
}

/// <summary>
/// The main window: connection, live parameters, errors, actuator tests,
/// adjustments, recording, graphs and replay. Same functions as the IES2
/// main form, without any UI framework dependency.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly DefinitionRegistry _registry;
    private readonly ITransportFactory _transports;
    private readonly ISettingsStore _settingsStore;
    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly ProtocolTiming? _timingOverride;
    private LiveConnection? _live;
    private CancellationTokenSource? _connectCancel;
    private CsvLogWriter? _recorder;
    private LineTraceWriter? _trace;
    private IReadOnlyList<ParameterDecoder> _recordedColumns = [];
    private readonly Lock _recorderGate = new();
    private bool _suppressSelection;
    private readonly bool _loaded;

    public MainViewModel(DefinitionRegistry registry, ITransportFactory transports, ISettingsStore settings,
        IDialogService dialogs, IUiDispatcher ui, ProtocolTiming? timingOverride = null)
    {
        _registry = registry;
        _transports = transports;
        _settingsStore = settings;
        _dialogs = dialogs;
        _ui = ui;
        _timingOverride = timingOverride;

        var s = settings.Load();
        Graph = new GraphViewModel { SeparateScales = s.SeparateScales, WindowSecondsChoice = s.GraphWindowSeconds };
        Replay = new ReplayViewModel(dialogs, () => LogFolder) { SeparateScales = s.SeparateScales };
        Ecus = [new EcuChoice(null), .. registry.All.OrderBy(d => d.Detection.AutoDetect ? d.Detection.Order : int.MaxValue).ThenBy(d => d.Id).Select(d => new EcuChoice(d))];
        SelectedEcu = Ecus.FirstOrDefault(e => e.Definition?.Id == s.EcuId) ?? Ecus[0];
        foreach (var e in Ecus)
            FilteredEcus.Add(e);
        Compatibility = s.Compatibility;
        ListenForIsoBroadcast = s.ListenForIsoBroadcast;
        DtrEnable = s.DtrEnable;
        AllowActiveCommands = s.AllowActiveCommands;
        AutoReconnect = s.AutoReconnect;
        TraceLine = s.TraceLine;
        BridgeAddress = s.BridgeAddress ?? "";
        LogFolder = s.LogFolder;
        SelectedLanguage = Ui.Languages.FirstOrDefault(l => l.Code == s.Language) ?? Ui.Languages[0];

        LoadTargets(s.Target);
        StatusText = Ui.Get("StatusDisconnected");
        _loaded = true;
    }

    public Ui Ui => Ui.Instance;

    public GraphViewModel Graph { get; }

    public ReplayViewModel Replay { get; }

    public DefinitionRegistry Registry => _registry;

    // ---- connection -------------------------------------------------------

    public ObservableCollection<ConnectionTarget> Targets { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyPropertyChangedFor(nameof(TargetHint))]
    public partial ConnectionTarget? SelectedTarget { get; set; }

    public string? TargetHint => SelectedTarget?.Hint;

    public IReadOnlyList<EcuChoice> Ecus { get; }

    [ObservableProperty]
    public partial EcuChoice SelectedEcu { get; set; }

    /// <summary>Text typed in the ECU search box: name, label code (e.g. 4WF.P8), vehicle or brand.</summary>
    [ObservableProperty]
    public partial string EcuFilter { get; set; } = "";

    /// <summary>The ECU list narrowed by <see cref="EcuFilter"/>; automatic detection is always first.</summary>
    public ObservableCollection<EcuChoice> FilteredEcus { get; } = [];

    partial void OnEcuFilterChanged(string value)
    {
        var keep = SelectedEcu;
        FilteredEcus.Clear();
        foreach (var e in Ecus.Where(e => e.Definition is null || e.Matches(value)))
            FilteredEcus.Add(e);
        // Typing a code selects the first family that matches it.
        var first = FilteredEcus.FirstOrDefault(e => e.Definition is not null);
        if (!string.IsNullOrWhiteSpace(value) && first is not null && !FilteredEcus.Contains(keep))
            SelectedEcu = first;
        else if (!string.IsNullOrWhiteSpace(value) && first is not null && keep.Definition is null)
            SelectedEcu = first;
        else if (FilteredEcus.Contains(keep))
            SelectedEcu = keep;
    }

    [ObservableProperty]
    public partial bool Compatibility { get; set; }

    [ObservableProperty]
    public partial bool ListenForIsoBroadcast { get; set; }

    [ObservableProperty]
    public partial bool DtrEnable { get; set; }

    [ObservableProperty]
    public partial bool AllowActiveCommands { get; set; }

    /// <summary>Restore a lost connection by itself, retrying until the ECU answers again.</summary>
    [ObservableProperty]
    public partial bool AutoReconnect { get; set; }

    /// <summary>Write every byte on the line to a trace file in the log folder, for each connection.</summary>
    [ObservableProperty]
    public partial bool TraceLine { get; set; }

    /// <summary>
    /// Address of a network bridge (<c>host:port</c>, RFC 2217: <c>iaw-cli bridge</c>,
    /// ser2net, esp-link); shown as a connection target when valid.
    /// </summary>
    [ObservableProperty]
    public partial string BridgeAddress { get; set; } = "";

    partial void OnBridgeAddressChanged(string value)
    {
        if (!_loaded)
            return;
        if (State == ConnectionState.Disconnected)
            LoadTargets(SelectedTarget?.Id);
        SaveSettings();
    }

    /// <summary>Path of the line trace of the current or last connection.</summary>
    [ObservableProperty]
    public partial string? TraceFile { get; set; }

    [ObservableProperty]
    public partial string LogFolder { get; set; }

    public IReadOnlyList<LanguageOption> Languages => Ui.Languages;

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected), nameof(IsDisconnected), nameof(IsConnecting), nameof(IsLost), nameof(IsOnline))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(DisconnectCommand), nameof(ReconnectCommand),
        nameof(ClearCodesCommand), nameof(RunTestCommand), nameof(RunAdjustmentCommand), nameof(ToggleRecordingCommand))]
    public partial ConnectionState State { get; set; }

    public bool IsConnected => State == ConnectionState.Connected;

    public bool IsDisconnected => State == ConnectionState.Disconnected;

    public bool IsConnecting => State == ConnectionState.Connecting;

    public bool IsLost => State == ConnectionState.Lost;

    /// <summary>A session exists (connected or lost): the data tabs are shown.</summary>
    public bool IsOnline => State is ConnectionState.Connected or ConnectionState.Lost;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSimulation { get; set; }

    [ObservableProperty]
    public partial string EcuName { get; set; } = "";

    [ObservableProperty]
    public partial string Iso { get; set; } = "";

    [ObservableProperty]
    public partial string Codric { get; set; } = "";

    [ObservableProperty]
    public partial string Vehicle { get; set; } = "";

    [ObservableProperty]
    public partial string Immobilizer { get; set; } = "";

    [ObservableProperty]
    public partial string RateText { get; set; } = "";

    public ConnectionInfo? Connection => _live?.Session.Connection;

    // ---- data -------------------------------------------------------------

    public ObservableCollection<ParameterRowViewModel> Parameters { get; } = [];

    /// <summary>Parameters grouped logically, for the dashboard and the table.</summary>
    public ObservableCollection<DashboardGroupViewModel> ParameterGroups { get; } = [];

    /// <summary>Dashboard left column (under the vehicle card): engine and status lights.</summary>
    public ObservableCollection<DashboardGroupViewModel> DashboardLeft { get; } = [];

    /// <summary>Dashboard right column, first row: temperatures and electrical side by side.</summary>
    public ObservableCollection<DashboardGroupViewModel> DashboardRightPair { get; } = [];

    /// <summary>Dashboard right column: mixture, idle, adaptation, other.</summary>
    public ObservableCollection<DashboardGroupViewModel> DashboardRight { get; } = [];

    /// <summary>The recognised vehicle and the ECU family information.</summary>
    public VehicleInfoViewModel VehicleInfo { get; } = new();

    public ObservableCollection<ErrorRowViewModel> Errors { get; } = [];

    public ObservableCollection<ActiveItemViewModel> Tests { get; } = [];

    public ObservableCollection<ActiveItemViewModel> Adjustments { get; } = [];

    [ObservableProperty]
    public partial bool ReadErrors { get; set; } = true;

    [ObservableProperty]
    public partial bool HasErrors { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCodesCommand), nameof(RunTestCommand), nameof(RunAdjustmentCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    public partial bool IsRecording { get; set; }

    public string RecordButtonText => Ui.Get(IsRecording ? "StopRecording" : "Record");

    [ObservableProperty]
    public partial string RecordingText { get; set; } = "";

    /// <summary>Path of the last finished recording, to open it in the replay.</summary>
    [ObservableProperty]
    public partial string? LastRecording { get; set; }

    // ---- connection commands ---------------------------------------------

    [RelayCommand]
    public void RefreshTargets() => LoadTargets(SelectedTarget?.Id);

    private void LoadTargets(string? keep)
    {
        Targets.Clear();
        if (ConnectionTarget.Bridge(BridgeAddress) is { } bridge)
            Targets.Add(bridge);
        foreach (var t in _transports.ListTargets())
            Targets.Add(t);
        SelectedTarget = Targets.FirstOrDefault(t => t.Id == keep) ?? Targets.FirstOrDefault(t => !t.IsSimulator) ?? Targets.FirstOrDefault();
    }

    private bool CanConnect() => State == ConnectionState.Disconnected && SelectedTarget is not null;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        var target = SelectedTarget!;
        SaveSettings();
        State = ConnectionState.Connecting;
        StatusText = ListenForIsoBroadcast ? Localizer.Get("Connecting") : Ui.Get("Connect") + "...";
        IsSimulation = target.IsSimulator;

        var simulated = SelectedEcu.Definition ?? _registry.Get("IAW-6F/16F");
        var transport = _transports.Create(target, simulated, ListenForIsoBroadcast, DtrEnable);
        var timing = _timingOverride ?? (Compatibility ? ProtocolTiming.Compatibility : ProtocolTiming.Default);
        if (target.IsSimulator && _timingOverride is null)
            timing = timing with { IsoBroadcastWaitMs = 3000 };
        var session = new DiagnosticSession(transport, _registry, new SessionOptions
        {
            Timing = timing,
            ListenForIsoBroadcast = ListenForIsoBroadcast,
            ActiveCommands = AllowActiveCommands ? ActiveCommandPolicy.VerifiedOnly : ActiveCommandPolicy.Disabled,
        });
        StartTrace(session, target);

        _connectCancel = new CancellationTokenSource();
        try
        {
            var ecuId = SelectedEcu.Definition?.Id;
            var token = _connectCancel.Token;
            await Task.Run(() => session.Connect(ecuId, token), token);
        }
        catch (Exception ex)
        {
            CloseTrace(session);
            session.Dispose();
            State = ConnectionState.Disconnected;
            StatusText = ex switch
            {
                OperationCanceledException => Localizer.Get("Cancelled"),
                UnrecognizedEcuException u => $"{Localizer.Get("UnrecECU")} (ISO {u.Iso ?? "-"})",
                UnauthorizedAccessException or IOException or NotSupportedException or InvalidOperationException
                    => string.Format(CultureInfo.CurrentCulture, Localizer.Get("COMopenError"), target.Name) + " " + ex.Message,
                _ => ex.Message,
            };
            if (TraceFile is not null)
                StatusText += " " + Ui.Format("TraceSaved", TraceFile);
            return;
        }
        finally
        {
            _connectCancel.Dispose();
            _connectCancel = null;
        }

        OnConnected(session);
    }

    private void OnConnected(DiagnosticSession session)
    {
        var c = session.Connection!;
        EcuName = c.EcuDisplayName;
        Iso = c.FormattedIso;
        Codric = c.FormattedCodric;
        Vehicle = c.VehicleModel ?? Localizer.Get("UnknownISO");
        Immobilizer = Ui.Get(c.HasImmobilizer ? "Present" : "Absent");

        _suppressSelection = true;
        Parameters.Clear();
        foreach (var p in session.Parameters)
        {
            var row = new ParameterRowViewModel(p);
            row.PropertyChanged += OnParameterRowChanged;
            Parameters.Add(row);
        }
        // Plot the first few parameters by default (engine speed, injection, advance, MAP...).
        foreach (var row in Parameters.Take(4))
            row.IsPlotted = true;
        _suppressSelection = false;

        ParameterGroups.Clear();
        DashboardLeft.Clear();
        DashboardRight.Clear();
        DashboardRightPair.Clear();
        foreach (var g in DashboardGroupViewModel.Build(Parameters))
        {
            ParameterGroups.Add(g);
            (g.Key switch
            {
                DisplaySpec.GroupEngine or DisplaySpec.GroupStatus => DashboardLeft,
                DisplaySpec.GroupTemperatures or DisplaySpec.GroupElectrical => DashboardRightPair,
                _ => DashboardRight,
            }).Add(g);
        }
        VehicleInfo.ShowConnection(c);

        Tests.Clear();
        foreach (var t in c.Definition.ActiveTests)
            Tests.Add(new ActiveItemViewModel(t));
        Adjustments.Clear();
        foreach (var a in c.Definition.Adjustments)
            Adjustments.Add(new ActiveItemViewModel(a));
        Errors.Clear();
        HasErrors = false;

        Graph.Reset();
        _live = new LiveConnection(session) { ReadErrors = ReadErrors, AutoReconnect = AutoReconnect };
        _live.CycleCompleted += OnCycle;
        _live.Faulted += OnFaulted;
        _live.ConnectionLost += OnConnectionLost;
        _live.Reconnecting += OnReconnecting;
        _live.Reconnected += OnReconnected;
        ApplySelection();
        _live.Start();
        State = ConnectionState.Connected;
        StatusText = Ui.Get("StatusConnected") + ": " + c.EcuDisplayName;
        if (TraceFile is not null)
            RecordingText = Ui.Format("TraceSaved", TraceFile);
    }

    [RelayCommand]
    private async Task CancelConnectAsync()
    {
        if (_connectCancel is not null)
            await _connectCancel.CancelAsync();
    }

    private bool CanDisconnect() => State is ConnectionState.Connected or ConnectionState.Lost;

    [RelayCommand(CanExecute = nameof(CanDisconnect))]
    public async Task DisconnectAsync()
    {
        StopRecording();
        _lostText = null;
        if (_live is not null)
        {
            var live = _live;
            _live = null;
            live.CycleCompleted -= OnCycle;
            live.Faulted -= OnFaulted;
            live.ConnectionLost -= OnConnectionLost;
            live.Reconnecting -= OnReconnecting;
            live.Reconnected -= OnReconnected;
            await live.StopAsync();
            CloseTrace(live.Session);
            await live.DisposeAsync();
        }
        State = ConnectionState.Disconnected;
        StatusText = Ui.Get("StatusDisconnected");
        RateText = "";
        VehicleInfo.ShowFamily(SelectedEcu.Definition);
    }

    private bool CanReconnect() => State == ConnectionState.Lost;

    /// <summary>Re-enters diagnostic mode after "No ECU response" (IES2 F12).</summary>
    [RelayCommand(CanExecute = nameof(CanReconnect))]
    private async Task ReconnectAsync()
    {
        if (_live is null)
            return;
        var live = _live;
        try
        {
            await live.StopAsync();
            await Task.Run(live.Session.Reinitialize);
            live.Start();
            State = ConnectionState.Connected;
            StatusText = Ui.Get("StatusConnected") + ": " + EcuName;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
        {
            StatusText = ex.Message;
        }
    }

    // ---- polling ------------------------------------------------------------

    private void OnParameterRowChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_suppressSelection)
            return;
        if (e.PropertyName is nameof(ParameterRowViewModel.IsSelected) or nameof(ParameterRowViewModel.IsPlotted))
            ApplySelection();
    }

    private void ApplySelection()
    {
        // Columns being recorded stay polled even if unticked, so the CSV never repeats stale values.
        _live?.SetSelection(Parameters.Where(p => p.IsSelected).Select(p => p.Decoder).Union(_recordedColumns));
        Graph.SetPlotted(Parameters.Where(p => p.IsPlotted));
    }

    partial void OnReadErrorsChanged(bool value)
    {
        if (_live is not null)
            _live.ReadErrors = value;
    }

    [RelayCommand]
    private void SelectAll() => SetAllSelected(true);

    [RelayCommand]
    private void SelectNone() => SetAllSelected(false);

    private void SetAllSelected(bool value)
    {
        _suppressSelection = true;
        foreach (var p in Parameters)
            p.IsSelected = value;
        _suppressSelection = false;
        ApplySelection();
    }

    /// <summary>Polling thread: write the CSV row, then hand the values to the UI.</summary>
    private void OnCycle(LiveCycle cycle, RegisterBuffer buffer)
    {
        lock (_recorderGate)
            _recorder?.WriteSample((long)(cycle.TimeSeconds * 1000), buffer);
        var rate = _live?.Rate.RatePerSecond(TimeSpan.FromSeconds(cycle.TimeSeconds)) ?? 0;
        _ui.Post(() => ApplyCycle(cycle, rate));
    }

    private void ApplyCycle(LiveCycle cycle, double rate)
    {
        foreach (var row in Parameters)
            if (row.IsSelected && cycle.Values.TryGetValue(row.Id, out var value)) // a cycle in flight may still carry unselected values
                row.Update(value);
        if (cycle.Errors is not null)
        {
            Errors.Clear();
            foreach (var e in cycle.Errors.Where(e => e.IsPresent))
                Errors.Add(ErrorRowViewModel.From(e));
            HasErrors = Errors.Count > 0;
        }
        Graph.Append(cycle);
        RateText = rate > 0 ? Ui.Format("RefreshRate", rate) : "";
    }

    private void OnFaulted(Exception ex) => _ui.Post(() =>
    {
        State = ConnectionState.Lost;
        StatusText = ex switch
        {
            CommunicationLostException lost => LostText(lost),
            LiveConnection.DifferentEcuException => Ui.Get("DifferentEcu"),
            _ => ex.Message,
        };
        RateText = "";
    });

    private static string LostText(CommunicationLostException lost) =>
        Localizer.Get("CommLost") + " — " + Ui.Get("LineFault_" + lost.Fault);

    private string? _lostText;

    private void OnConnectionLost(CommunicationLostException lost) => _ui.Post(() =>
    {
        _lostText = LostText(lost);
        State = ConnectionState.Lost;
        StatusText = _lostText;
        RateText = "";
    });

    private void OnReconnecting(int attempt) => _ui.Post(() =>
    {
        if (State == ConnectionState.Lost)
            StatusText = (_lostText ?? Localizer.Get("CommLost")) + " " + Ui.Format("Reconnecting", attempt);
    });

    private void OnReconnected() => _ui.Post(() =>
    {
        if (State != ConnectionState.Lost)
            return;
        State = ConnectionState.Connected;
        StatusText = Ui.Get("StatusConnected") + ": " + EcuName + " (" + Ui.Get("Reconnected") + ")";
    });

    // ---- line trace ----------------------------------------------------------

    private void StartTrace(DiagnosticSession session, ConnectionTarget target)
    {
        TraceFile = null;
        if (!TraceLine)
            return;
        var path = Path.Combine(LogFolder, $"iaw_trace_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        try
        {
            _trace = LineTraceWriter.Create(path,
            [
                new("app", "marelli-iaw-ecu-studio " + (typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0")),
                new("transport", target.IsSimulator ? session.Transport.Name : target.Name),
                new("os", System.Runtime.InteropServices.RuntimeInformation.OSDescription),
                new("options", $"compat={Compatibility} broadcast={ListenForIsoBroadcast} dtr={DtrEnable}"),
            ]);
            session.Link.Monitor += _trace.Write;
            TraceFile = path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RecordingText = ex.Message;
        }
    }

    private void CloseTrace(DiagnosticSession session)
    {
        if (_trace is not { } trace)
            return;
        _trace = null;
        session.Link.Monitor -= trace.Write;
        trace.Comment("line: " + session.Link.Statistics);
        trace.Dispose();
    }

    // ---- recording -----------------------------------------------------------

    private bool CanRecord() => State == ConnectionState.Connected || IsRecording;

    [RelayCommand(CanExecute = nameof(CanRecord))]
    private void ToggleRecording()
    {
        if (IsRecording)
        {
            StopRecording();
            return;
        }
        if (_live?.Session.Connection is not { } c)
            return;
        var columns = Parameters.Where(p => p.IsSelected).Select(p => p.Decoder).ToList();
        if (columns.Count == 0)
            return;
        var path = Path.Combine(LogFolder, $"iaw_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        try
        {
            var writer = CsvLogWriter.Create(path, c, columns, typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "0");
            lock (_recorderGate)
                _recorder = writer;
            _recordedColumns = columns;
            ApplySelection();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RecordingText = ex.Message;
            return;
        }
        LastRecording = path;
        IsRecording = true;
        RecordingText = Ui.Format("RecordingTo", path);
    }

    private void StopRecording()
    {
        CsvLogWriter? writer;
        lock (_recorderGate)
        {
            writer = _recorder;
            _recorder = null;
        }
        if (writer is null)
            return;
        writer.Dispose();
        _recordedColumns = [];
        ApplySelection();
        IsRecording = false;
        RecordingText = Ui.Format("Recorded", LastRecording);
    }

    [RelayCommand]
    private async Task OpenLastRecordingAsync()
    {
        if (LastRecording is not null)
            await Replay.LoadAsync(LastRecording);
    }

    // ---- active commands -------------------------------------------------------

    private bool CanRunActive() => State == ConnectionState.Connected && !IsBusy;

    private async Task<bool> EnsureActiveAllowedAsync()
    {
        if (AllowActiveCommands)
            return true;
        await _dialogs.ShowMessageAsync(Ui.Get("TabTests"), Ui.Get("ActiveDisabled"));
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanRunActive))]
    private async Task ClearCodesAsync()
    {
        if (!await EnsureActiveAllowedAsync() || !await _dialogs.ConfirmAsync(Ui.Get("ConfirmClearTitle"), Ui.Get("ConfirmClear")))
            return;
        await RunActiveAsync(s => s.ClearCodes(), null);
    }

    [RelayCommand(CanExecute = nameof(CanRunActive))]
    private async Task RunTestAsync(ActiveItemViewModel? item)
    {
        if (item?.Test is not { } test || !await EnsureActiveAllowedAsync())
            return;
        var question = Ui.Format("ConfirmTest", item.Name, test.TimeoutSeconds, item.Condition);
        if (!await _dialogs.ConfirmAsync(Ui.Get("ConfirmTestTitle"), question))
            return;
        await RunActiveAsync(s => s.RunActiveTest(test), item);
    }

    [RelayCommand(CanExecute = nameof(CanRunActive))]
    private async Task RunAdjustmentAsync(ActiveItemViewModel? item)
    {
        if (item?.Adjustment is not { } adjustment || !await EnsureActiveAllowedAsync())
            return;
        Func<byte, IEnumerable<byte>>? chooser = null;
        if (adjustment.Type == "onebyte")
        {
            // Runs on the session thread: ask the user on the UI thread and wait for the answer.
            chooser = current =>
            {
                var value = _ui.InvokeAsync(() => _dialogs.AskByteAsync(Ui.Format("AdjustValueTitle", item.Name), current)).GetAwaiter().GetResult();
                return value is { } v ? [v] : [];
            };
        }
        else if (!await _dialogs.ConfirmAsync(Ui.Get("ConfirmAdjustTitle"), Ui.Format("ConfirmAdjust", item.Name)))
        {
            return;
        }
        await RunActiveAsync(s => s.RunAdjustment(adjustment, chooser), item);
    }

    private async Task RunActiveAsync(Func<DiagnosticSession, ActiveResult> command, ActiveItemViewModel? item)
    {
        if (_live is null)
            return;
        IsBusy = true;
        item?.SetPending();
        StatusText = Ui.Get("Busy");
        try
        {
            var result = await _live.RunAsync(command);
            item?.SetOutcome(result.Outcome);
            StatusText = (item?.Name ?? Ui.Get("ClearCodes")) + ": " + (item?.Result ?? Ui.Get(result.Outcome == ActiveOutcome.Passed ? "Passed" : "Failed"));
        }
        catch (Exception ex) when (ex is ActiveCommandNotAllowedException or IOException or TimeoutException or InvalidOperationException)
        {
            item?.SetOutcome(ActiveOutcome.Failed);
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---- settings & language ---------------------------------------------------

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        Ui.Instance.SetLanguage(value.Code);
        foreach (var p in Parameters)
            p.RefreshTexts();
        foreach (var t in Tests.Concat(Adjustments))
            t.RefreshTexts();
        foreach (var g in ParameterGroups)
            g.RefreshTexts();
        VehicleInfo.Refresh();
        Graph.SetPlotted(Parameters.Where(p => p.IsPlotted));
        OnPropertyChanged(nameof(RecordButtonText));
        OnPropertyChanged(nameof(Ecus));
        if (State == ConnectionState.Disconnected && _loaded)
            LoadTargets(SelectedTarget?.Id); // the simulator entry is translated
        if (State == ConnectionState.Disconnected)
            StatusText = Ui.Get("StatusDisconnected");
        SaveSettings();
    }

    partial void OnCompatibilityChanged(bool value) => SaveSettings();

    partial void OnListenForIsoBroadcastChanged(bool value) => SaveSettings();

    partial void OnAllowActiveCommandsChanged(bool value) => SaveSettings();

    partial void OnAutoReconnectChanged(bool value)
    {
        if (_live is not null)
            _live.AutoReconnect = value;
        SaveSettings();
    }

    partial void OnTraceLineChanged(bool value) => SaveSettings();

    partial void OnSelectedEcuChanged(EcuChoice value)
    {
        if (State == ConnectionState.Disconnected)
            VehicleInfo.ShowFamily(value?.Definition);
        SaveSettings();
    }

    public void SaveSettings()
    {
        // Property setters run during construction too; skip until everything is loaded.
        if (!_loaded)
            return;
        _settingsStore.Save(new AppSettings
        {
            Target = SelectedTarget?.Id,
            EcuId = SelectedEcu?.Definition?.Id,
            Compatibility = Compatibility,
            ListenForIsoBroadcast = ListenForIsoBroadcast,
            DtrEnable = DtrEnable,
            AllowActiveCommands = AllowActiveCommands,
            AutoReconnect = AutoReconnect,
            TraceLine = TraceLine,
            BridgeAddress = string.IsNullOrWhiteSpace(BridgeAddress) ? null : BridgeAddress.Trim(),
            Language = SelectedLanguage?.Code,
            LogFolder = LogFolder,
            SeparateScales = Graph.SeparateScales,
            GraphWindowSeconds = Graph.WindowSecondsChoice,
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (_live is not null)
            await DisconnectAsync();
    }
}
