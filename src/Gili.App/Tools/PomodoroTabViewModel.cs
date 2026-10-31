using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.App.Island;
using Gili.Core.Settings;
using Gili.Core.Timers;

namespace Gili.App.Tools;

/// <summary>One length in the Pomodoro tab (mockup PLAN_ROWS): a name, its value and a stepper that stops at the range.</summary>
public sealed partial class PomodoroLengthViewModel : ObservableObject
{
    private readonly Func<AppSettings, int> _get;
    private readonly Func<AppSettings, int, AppSettings> _set;
    private readonly Func<int, string> _format;
    private readonly int _min;
    private readonly int _max;
    private readonly SettingsHub _settings;
    private int _value;

    [ObservableProperty] private string _valueText = "";

    public PomodoroLengthViewModel(SettingsHub settings, string name, Func<AppSettings, int> get, Func<AppSettings, int, AppSettings> set,
        int min, int max, Func<int, string> format)
    {
        _settings = settings;
        Name = name;
        _get = get;
        _set = set;
        _min = min;
        _max = max;
        _format = format;
        DecreaseCommand = new RelayCommand(() => Step(-1), () => _value > _min);
        IncreaseCommand = new RelayCommand(() => Step(1), () => _value < _max);
        Show();
    }

    public string Name { get; }

    public string DecreaseName => $"Decrease {Name}";

    public string IncreaseName => $"Increase {Name}";

    public IRelayCommand DecreaseCommand { get; }

    public IRelayCommand IncreaseCommand { get; }

    public void Show()
    {
        _value = _get(_settings.Current);
        ValueText = _format(_value);
        DecreaseCommand.NotifyCanExecuteChanged();
        IncreaseCommand.NotifyCanExecuteChanged();
    }

    private void Step(int direction) => _settings.Update(s => _set(s, Math.Clamp(_get(s) + direction, _min, _max)));
}

/// <summary>
/// The Pomodoro tab in Tools (F-TOOL-03): the island's pomodoro and the four lengths. The steppers write settings;
/// the service follows through <c>SettingsHub.Changed</c>.
/// </summary>
public sealed partial class PomodoroTabViewModel : ObservableObject
{
    private readonly TimerService _timers;
    private readonly SettingsHub _settings;

    [ObservableProperty] private string _sub = "";
    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _buttonText = "Start";

    public PomodoroTabViewModel(TimerService timers, SettingsHub settings)
    {
        _timers = timers;
        _settings = settings;
        Lengths =
        [
            new(settings, "Focus", s => s.PomodoroFocus, (s, v) => s with { PomodoroFocus = v }, 1, 120, TimerText.Minutes),
            new(settings, "Short break", s => s.PomodoroBreak, (s, v) => s with { PomodoroBreak = v }, 1, 60, TimerText.Minutes),
            new(settings, "Long break", s => s.PomodoroLongBreak, (s, v) => s with { PomodoroLongBreak = v }, 1, 60, TimerText.Minutes),
            new(settings, "Long break after", s => s.PomodoroRounds, (s, v) => s with { PomodoroRounds = v }, 2, 8, TimerText.Sessions),
        ];
        timers.Changed += Refresh;
        settings.Changed += OnSettings;
        Refresh();
    }

    public IReadOnlyList<PomodoroLengthViewModel> Lengths { get; }

    public void Detach()
    {
        _timers.Changed -= Refresh;
        _settings.Changed -= OnSettings;
    }

    [RelayCommand]
    private void Start() => _timers.StartOrPausePomodoro();

    [RelayCommand]
    private void Reset() => _timers.ResetPomodoro();

    private void OnSettings(AppSettings settings)
    {
        foreach (var length in Lengths) length.Show();
        Refresh();
    }

    private void Refresh()
    {
        var pomodoro = _timers.Pomodoro;
        Sub = TimerText.PomodoroSub(pomodoro, _timers.Plan);
        Time = TimerText.Clock(pomodoro.Clock.Remaining(DateTimeOffset.UtcNow));
        ButtonText = TimerText.Button(pomodoro.Clock.IsRunning, pomodoro.Clock.IsPaused);
    }
}
