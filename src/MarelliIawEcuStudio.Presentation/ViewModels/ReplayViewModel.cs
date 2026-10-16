using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarelliIawEcuStudio.Core.Logging;
using MarelliIawEcuStudio.Presentation.Live;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Presentation.Services;

namespace MarelliIawEcuStudio.Presentation.ViewModels;

/// <summary>A column of the replayed log, with its value at the cursor.</summary>
public sealed partial class ReplayColumnViewModel(int index, LogColumn column) : ObservableObject
{
    public int Index { get; } = index;

    public string Name { get; } = column.Name;

    public string Unit { get; } = column.Unit;

    [ObservableProperty]
    public partial bool IsPlotted { get; set; } = index < 4;

    [ObservableProperty]
    public partial string ValueText { get; set; } = "";
}

/// <summary>
/// Plays back a CSV log (this program's or an IES2 export) without a car:
/// play, pause, scrub and 1x/2x/5x/10x speed, with the values at the cursor
/// and the same plot as the live view.
/// </summary>
public sealed partial class ReplayViewModel : ObservableObject, IPlotSource
{
    private readonly IDialogService _dialogs;
    private readonly Func<string?> _logFolder;
    private LogPlayer? _player;
    private IReadOnlyList<PlotSeries> _allSeries = [];
    private IReadOnlyList<PlotSeries> _visible = [];
    private bool _updatingPosition;

    public ReplayViewModel(IDialogService dialogs, Func<string?> logFolder)
    {
        _dialogs = dialogs;
        _logFolder = logFolder;
    }

    public static IReadOnlyList<double> SpeedChoices { get; } = [1, 2, 5, 10];

    public event Action<bool>? PlotChanged;

    public ObservableCollection<ReplayColumnViewModel> Columns { get; } = [];

    public IReadOnlyList<PlotSeries> VisibleSeries => _visible;

    [ObservableProperty]
    public partial bool SeparateScales { get; set; } = true;

    public double? WindowSeconds => null;

    public bool Follow => false;

    public double? CursorSeconds => _player is null ? null : _player.PositionMs / 1000.0;

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    public partial bool IsPlaying { get; set; }

    public string PlayPauseText => Ui.Get(IsPlaying ? "Pause" : "Play");

    [ObservableProperty]
    public partial double Speed { get; set; } = 1;

    /// <summary>Slider position in milliseconds from the first sample.</summary>
    [ObservableProperty]
    public partial double PositionMs { get; set; }

    [ObservableProperty]
    public partial double DurationMs { get; set; }

    [ObservableProperty]
    public partial string PositionText { get; set; } = "";

    [ObservableProperty]
    public partial string FileName { get; set; } = "";

    [ObservableProperty]
    public partial string Summary { get; set; } = "";

    partial void OnSpeedChanged(double value)
    {
        if (_player is not null && value > 0)
            _player.Speed = value;
    }

    partial void OnPositionMsChanged(double value)
    {
        if (_player is null || _updatingPosition)
            return;
        _player.PositionMs = value; // scrubbing
        ShowPosition();
    }

    partial void OnSeparateScalesChanged(bool value) => PlotChanged?.Invoke(true);

    [RelayCommand]
    private async Task OpenAsync()
    {
        var path = await _dialogs.PickLogFileAsync(_logFolder());
        if (path is not null)
            await LoadAsync(path);
    }

    /// <summary>Loads a log file; errors are shown to the user.</summary>
    public async Task<bool> LoadAsync(string path)
    {
        DataLog log;
        try
        {
            log = await Task.Run(() => CsvLogReader.Read(path));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            await _dialogs.ShowMessageAsync(Ui.Get("OpenLog"), Ui.Format("OpenFailed", ex.Message));
            return false;
        }
        Load(log, Path.GetFileName(path));
        return true;
    }

    public void Load(DataLog log, string fileName)
    {
        _player = new LogPlayer(log) { Speed = Speed };
        _player.Finished += _ => IsPlaying = false;

        Columns.Clear();
        var series = new List<PlotSeries>();
        for (var i = 0; i < log.Columns.Count; i++)
        {
            var c = log.Columns[i];
            var column = new ReplayColumnViewModel(i, c);
            column.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ReplayColumnViewModel.IsPlotted))
                    UpdateVisible();
            };
            Columns.Add(column);
            var s = new PlotSeries(c.Id, c.Name, c.Unit);
            foreach (var sample in log.Samples)
                if (!double.IsNaN(sample.Values[i]))
                    s.Add((sample.TimeMs - _player.StartMs) / 1000.0, sample.Values[i], int.MaxValue);
            series.Add(s);
        }
        _allSeries = series;

        FileName = fileName;
        var ecu = log.Metadata.GetValueOrDefault("ecu") ?? (log.IsLegacyIes2 ? Ui.Get("ReplayLegacy") : "");
        var vehicle = log.Metadata.GetValueOrDefault("vehicle");
        Summary = string.Join(" · ", new[] { ecu, vehicle, Ui.Format("Samples", log.Samples.Count), FormatTime(_player.DurationMs) }
            .Where(s => !string.IsNullOrEmpty(s)));
        DurationMs = _player.DurationMs;
        IsPlaying = false;
        IsLoaded = true;
        SetPosition(0);
        UpdateVisible();
    }

    private void UpdateVisible()
    {
        _visible = [.. Columns.Where(c => c.IsPlotted).Select(c => _allSeries[c.Index])];
        PlotChanged?.Invoke(true);
    }

    [RelayCommand]
    private void TogglePlay()
    {
        if (_player is null)
            return;
        _player.TogglePlay();
        IsPlaying = _player.IsPlaying;
    }

    /// <summary>Advances playback; called by a UI timer.</summary>
    public void Tick(TimeSpan elapsed)
    {
        if (_player is not { IsPlaying: true })
            return;
        _player.Advance(elapsed);
        SetPosition(_player.PositionMs);
        IsPlaying = _player.IsPlaying;
    }

    private void SetPosition(double ms)
    {
        _updatingPosition = true;
        PositionMs = ms;
        _updatingPosition = false;
        ShowPosition();
    }

    private void ShowPosition()
    {
        if (_player is null)
            return;
        PositionText = $"{FormatTime((long)_player.PositionMs)} / {FormatTime(_player.DurationMs)}";
        if (_player.CurrentSample is { } sample)
        {
            foreach (var c in Columns)
            {
                var v = sample.Values[c.Index];
                c.ValueText = double.IsNaN(v) ? "" : v.ToString("0.##", CultureInfo.CurrentCulture);
            }
        }
        PlotChanged?.Invoke(false);
    }

    private static string FormatTime(long ms) => TimeSpan.FromMilliseconds(ms).ToString(ms >= 3_600_000 ? @"h\:mm\:ss" : @"m\:ss\.f", CultureInfo.InvariantCulture);
}
