using CommunityToolkit.Mvvm.ComponentModel;

namespace Gili.App.Tools;

/// <summary>The Tools window (PRD 7.5): tabs on the left; Timer, Stopwatch, Pomodoro and Alarms work, Todo arrives later.</summary>
public sealed partial class ToolsViewModel : ObservableObject
{
    public const string TimerTab = "Timer";
    public const string StopwatchTab = "Stopwatch";
    public const string PomodoroTab = "Pomodoro";
    public const string AlarmsTab = "Alarms";
    public const string TodoTab = "Todo";

    public ToolsViewModel(TimersTabViewModel timers, StopwatchTabViewModel stopwatch, PomodoroTabViewModel pomodoro, AlarmsTabViewModel alarms, string tab)
    {
        Timers = timers;
        Stopwatch = stopwatch;
        Pomodoro = pomodoro;
        Alarms = alarms;
        _selectedTab = tab;
        stopwatch.SetVisible(IsStopwatch);
    }

    public IReadOnlyList<string> Tabs { get; } = [TimerTab, StopwatchTab, PomodoroTab, AlarmsTab, TodoTab];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimer), nameof(IsStopwatch), nameof(IsPomodoro), nameof(IsAlarms), nameof(IsTodo), nameof(IsComingLater))]
    private string _selectedTab;

    public TimersTabViewModel Timers { get; }

    public StopwatchTabViewModel Stopwatch { get; }

    public PomodoroTabViewModel Pomodoro { get; }

    public AlarmsTabViewModel Alarms { get; }

    public bool IsTimer => SelectedTab == TimerTab;

    public bool IsStopwatch => SelectedTab == StopwatchTab;

    public bool IsPomodoro => SelectedTab == PomodoroTab;

    public bool IsAlarms => SelectedTab == AlarmsTab;

    public bool IsTodo => SelectedTab == TodoTab;

    /// <summary>The tab that still shows "Coming in a later version".</summary>
    public bool IsComingLater => IsTodo;

    partial void OnSelectedTabChanged(string value) => Stopwatch.SetVisible(IsStopwatch);

    public void Detach()
    {
        Timers.Detach();
        Stopwatch.Detach();
        Pomodoro.Detach();
        Alarms.Detach();
    }
}
