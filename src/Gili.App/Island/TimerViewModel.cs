using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Timers;

namespace Gili.App.Island;

/// <summary>The Timer page (mockup renderTimerPage): the segment, big digits, the line below, the ring and the buttons.</summary>
public sealed partial class TimerViewModel : ObservableObject
{
    public static readonly Brush Orange = Frozen(0xFF, 0x9F, 0x0A);
    public static readonly Brush Red = Frozen(0xFF, 0x45, 0x3A);
    public static readonly Brush Green = Frozen(0x30, 0xD1, 0x58);
    public static readonly Brush LightBlue = Frozen(0x64, 0xD2, 0xFF);

    private readonly TimerService _timers;

    /// <summary>Tenths on the stopwatch move ten times a second, only while the page is open and it runs.</summary>
    private readonly DispatcherTimer _fast = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _pageOpen;

    [ObservableProperty] private string _big = "";
    [ObservableProperty] private string _sub = "";
    [ObservableProperty] private string _buttonText = "Start";
    [ObservableProperty] private double _fraction = 1;
    [ObservableProperty] private Brush _ringBrush = Orange;
    [ObservableProperty] private bool _showPresets = true;
    [ObservableProperty] private TimerMode _mode;

    public TimerViewModel(TimerService timers)
    {
        _timers = timers;
        timers.Changed += Refresh;
        _fast.Tick += (_, _) => Refresh();
        Refresh();
    }

    public IReadOnlyList<string> Modes { get; } = ["Timer", "Stopwatch", "Pomodoro"];

    /// <summary>The segment as text, for the segmented control.</summary>
    public string ModeName
    {
        get => Modes[(int)_timers.Mode];
        set => _timers.Mode = (TimerMode)Math.Max(0, Modes.ToList().IndexOf(value));
    }

    public void SetPageOpen(bool open)
    {
        _pageOpen = open;
        UpdateFastTick();
    }

    [RelayCommand]
    private void StartOrPause() => _timers.StartOrPause();

    [RelayCommand]
    private void Reset() => _timers.Reset();

    [RelayCommand]
    private void Preset(string seconds) => _timers.Preset(int.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture));

    private void Refresh()
    {
        var now = DateTimeOffset.UtcNow;
        Mode = _timers.Mode;
        OnPropertyChanged(nameof(ModeName));
        switch (_timers.Mode)
        {
            case TimerMode.Timer:
                var timer = _timers.Timer;
                Big = TimerText.Clock(timer.Remaining(now));
                Sub = TimerText.TimerSub(timer);
                ButtonText = TimerText.Button(timer.IsRunning, timer.IsPaused);
                Fraction = timer.Remaining(now).TotalSeconds / Math.Max(1, timer.TotalSeconds);
                RingBrush = Orange;
                break;
            case TimerMode.Stopwatch:
                var stopwatch = _timers.Stopwatch;
                var elapsed = stopwatch.Elapsed(now);
                Big = TimerText.Stopwatch(elapsed, tenths: true);
                Sub = TimerText.StopwatchSub(stopwatch);
                ButtonText = TimerText.Button(stopwatch.IsRunning, elapsed > TimeSpan.Zero);
                Fraction = elapsed.TotalSeconds % 60 / 60;
                RingBrush = Orange;
                break;
            default:
                var pomodoro = _timers.Pomodoro;
                Big = TimerText.Clock(pomodoro.Clock.Remaining(now));
                Sub = TimerText.PomodoroSub(pomodoro, _timers.Plan);
                ButtonText = TimerText.Button(pomodoro.Clock.IsRunning, started: false);
                Fraction = pomodoro.Clock.Remaining(now).TotalSeconds / Math.Max(1, pomodoro.Clock.TotalSeconds);
                RingBrush = Red;
                break;
        }
        ShowPresets = _timers.Mode == TimerMode.Timer;
        UpdateFastTick();
    }

    private void UpdateFastTick()
    {
        if (_pageOpen && _timers.Mode == TimerMode.Stopwatch && _timers.Stopwatch.IsRunning) _fast.Start();
        else _fast.Stop();
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
