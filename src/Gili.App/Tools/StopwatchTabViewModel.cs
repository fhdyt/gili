using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.App.Island;
using Gili.Core.Timers;

namespace Gili.App.Tools;

/// <summary>One lap: its name, the time since the lap before, and the total.</summary>
public sealed record LapRowViewModel(string Name, string Split, string Total);

/// <summary>
/// The Stopwatch tab in Tools (F-TOOL-02): the island's stopwatch plus laps. Tenths come from a 100 ms tick that runs
/// only while the tab shows and the stopwatch runs (PRD 8.1).
/// </summary>
public sealed partial class StopwatchTabViewModel : ObservableObject
{
    private readonly TimerService _timers;
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _visible;
    private int _shownLaps = -1;

    [ObservableProperty] private string _time = "00:00.0";
    [ObservableProperty] private string _buttonText = "Start";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LapCommand))]
    private bool _canLap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private bool _canReset;

    public StopwatchTabViewModel(TimerService timers)
    {
        _timers = timers;
        _tick.Tick += (_, _) => ShowTime();
        timers.Changed += Refresh;
        Refresh();
    }

    public ObservableCollection<LapRowViewModel> Laps { get; } = [];

    public bool IsEmpty => Laps.Count == 0;

    /// <summary>Whether the tab shows: the 100 ms tick runs only then.</summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        Refresh();
    }

    public void Detach()
    {
        _tick.Stop();
        _timers.Changed -= Refresh;
    }

    [RelayCommand]
    private void Start() => _timers.StartOrPauseStopwatch();

    [RelayCommand(CanExecute = nameof(CanLap))]
    private void Lap() => _timers.Lap();

    [RelayCommand(CanExecute = nameof(CanReset))]
    private void Reset() => _timers.ResetStopwatch();

    private void ShowTime() => Time = TimerText.Stopwatch(_timers.Stopwatch.Elapsed(DateTimeOffset.UtcNow), tenths: true);

    private void Refresh()
    {
        var state = _timers.Stopwatch;
        var elapsed = state.Elapsed(DateTimeOffset.UtcNow);
        ShowTime();
        ButtonText = TimerText.Button(state.IsRunning, elapsed > TimeSpan.Zero);
        CanLap = state.CanLap;
        CanReset = elapsed > TimeSpan.Zero;
        if (state.Laps.Count != _shownLaps)
        {
            _shownLaps = state.Laps.Count;
            Laps.Clear();
            long before = 0;
            var rows = new List<LapRowViewModel>();
            for (var i = 0; i < state.Laps.Count; i++)
            {
                rows.Add(new(TimerText.LapName(i + 1), Ms(state.Laps[i] - before), Ms(state.Laps[i])));
                before = state.Laps[i];
            }
            // Newest on top.
            for (var i = rows.Count - 1; i >= 0; i--) Laps.Add(rows[i]);
            OnPropertyChanged(nameof(IsEmpty));
        }
        if (_visible && state.IsRunning) _tick.Start();
        else _tick.Stop();
    }

    private static string Ms(long ms) => TimerText.Stopwatch(TimeSpan.FromMilliseconds(ms), tenths: true);
}
