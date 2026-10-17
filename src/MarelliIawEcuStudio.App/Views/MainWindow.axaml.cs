using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using MarelliIawEcuStudio.Presentation.Localization;
using MarelliIawEcuStudio.Presentation.ViewModels;

namespace MarelliIawEcuStudio.App.Views;

public partial class MainWindow : Window
{
    private readonly Stopwatch _replayClock = Stopwatch.StartNew();
    private readonly DispatcherTimer _replayTimer;
    private TimeSpan _lastTick;

    public MainWindow()
    {
        InitializeComponent();
        // Drives log replay: the player advances by the real time elapsed between ticks.
        _replayTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Background, (_, _) => TickReplay());
        _replayTimer.Start();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                ApplyTheme(vm.SelectedTheme);
                vm.PropertyChanged += (_, e) =>
                {
                    // Show live data as soon as the ECU answers (the replay tab is the only one enabled before).
                    if (e.PropertyName == nameof(MainViewModel.State) && vm.State == ConnectionState.Connected && Tabs.SelectedItem == ReplayTab)
                        Tabs.SelectedItem = DashboardTab;
                    if (e.PropertyName == nameof(MainViewModel.SelectedTheme))
                        ApplyTheme(vm.SelectedTheme);
                };
            }
        };
    }

    /// <summary>"system" leaves the choice to the operating system, and follows it when it changes.</summary>
    private static void ApplyTheme(ThemeOption? theme)
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant = theme?.Code switch
        {
            ThemeOption.Light => ThemeVariant.Light,
            ThemeOption.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private void TickReplay()
    {
        var now = _replayClock.Elapsed;
        var elapsed = now - _lastTick;
        _lastTick = now;
        if (DataContext is MainViewModel vm)
            vm.Replay.Tick(elapsed);
    }
}
