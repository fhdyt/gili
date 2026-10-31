using CommunityToolkit.Mvvm.ComponentModel;

namespace Gili.App.Tools;

/// <summary>The Tools window (PRD 7.5): tabs on the left; Timer, Stopwatch, Pomodoro, Alarms and Todo.</summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    public const string TimerTab = "Timer";
    public const string StopwatchTab = "Stopwatch";
    public const string PomodoroTab = "Pomodoro";
    public const string AlarmsTab = "Alarms";
    public const string TodoTab = "Todo";

    public ToolsViewModel(TimersTabViewModel timers, StopwatchTabViewModel stopwatch, PomodoroTabViewModel pomodoro, AlarmsTabViewModel alarms, TodoTabViewModel todo, string tab)
    {
        Timers = timers;
        Stopwatch = stopwatch;
        Pomodoro = pomodoro;
        Alarms = alarms;
        Todo = todo;
        _selectedTab = tab;
        ShowStopwatch();
    }

    public IReadOnlyList<string> Tabs { get; } = [TimerTab, StopwatchTab, PomodoroTab, AlarmsTab, TodoTab];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimer), nameof(IsStopwatch), nameof(IsPomodoro), nameof(IsAlarms), nameof(IsTodo))]
    private string _selectedTab;

    private bool _minimized;

    public TimersTabViewModel Timers { get; }

    public StopwatchTabViewModel Stopwatch { get; }

    public PomodoroTabViewModel Pomodoro { get; }

    public AlarmsTabViewModel Alarms { get; }

    public TodoTabViewModel Todo { get; }

    public bool IsTimer => SelectedTab == TimerTab;

    public bool IsStopwatch => SelectedTab == StopwatchTab;

    public bool IsPomodoro => SelectedTab == PomodoroTab;

    public bool IsAlarms => SelectedTab == AlarmsTab;

    public bool IsTodo => SelectedTab == TodoTab;

    partial void OnSelectedTabChanged(string value) => ShowStopwatch();

    /// <summary>A minimized window shows nothing, so the stopwatch's 100 ms tick stops too (PRD 8.1).</summary>
    public void SetMinimized(bool minimized)
    {
        _minimized = minimized;
        ShowStopwatch();
    }

    /// <summary>The stopwatch shows, and its tick runs, only on its tab and only while the window is not minimized.</summary>
    private void ShowStopwatch() => Stopwatch.SetVisible(IsStopwatch && !_minimized);

    public void Detach()
    {
        Timers.Detach();
        Stopwatch.Detach();
        Pomodoro.Detach();
        Alarms.Detach();
        Todo.Detach();
    }
}
