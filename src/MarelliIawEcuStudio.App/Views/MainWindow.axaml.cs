using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Threading;
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
                vm.PropertyChanged += (_, e) =>
                {
                    // Show live data as soon as the ECU answers (the replay tab is the only one enabled before).
                    if (e.PropertyName == nameof(MainViewModel.State) && vm.State == ConnectionState.Connected && Tabs.SelectedItem == ReplayTab)
                        Tabs.SelectedItem = DashboardTab;
                };
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
