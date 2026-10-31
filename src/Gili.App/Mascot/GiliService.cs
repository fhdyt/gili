using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Gili.Core.Mascot;
using Gili.Core.Motion;
using Gili.Platform.Input;

namespace Gili.App.Mascot;

/// <summary>
/// Drives every <see cref="GiliEyes"/> together (PRD 6.3 "Maskot Gili", "Ekspresi Gili"): reads the pointer and mouse
/// buttons 30 times a second while Gili shows, springs the gaze, blinks at random and on every click, goes wide-eyed
/// during a drag, falls asleep after 5 minutes without input and nods to music. Nothing runs while Gili is hidden.
/// </summary>
public sealed class GiliService : IDisposable
{
    private readonly List<GiliEyes> _eyes = [];
    private readonly DispatcherTimer _poll = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _second = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _blink = new();
    private readonly DispatcherTimer _happy = new();
    private bool _isHappy;
    private Func<Point>? _watch;
    private DateTime _watchUntil;
    private bool _startled;
    private bool _focused;
    private int? _minute;
    private DateTime _glanceAt = DateTime.MinValue;
    private DateTime _glanceUntil = DateTime.MinValue;
    private bool _glancing;
    private bool _shown;
    private bool _paused;
    private readonly Spring _x = new(0) { Parameters = GiliRules.EyeSpring };
    private readonly Spring _y = new(0) { Parameters = GiliRules.EyeSpring };
    private readonly Random _random = new();
    private GiliForm _form = GiliForm.InIsland;
    private GiliMood _mood = GiliMood.Neutral;
    private PointerSample _last = new(int.MinValue, int.MinValue, false, false);
    private Point _dragFrom;
    private bool _dragging;
    private bool _music;
    private bool _visible;
    private bool _stepping;
    private bool _nextIsDouble;
    private TimeSpan _idle;
    private TimeSpan _lastFrame;
    private DateTime _lastMove = DateTime.UtcNow;

    public GiliService()
    {
        _happy.Tick += (_, _) =>
        {
            _happy.Stop();
            _isHappy = false;
            UpdateMood();
        };
        _poll.Tick += (_, _) => Poll();
        _second.Tick += (_, _) => EverySecond();
        _blink.Tick += (_, _) => RandomBlink();
    }

    public void Register(GiliEyes eyes) => _eyes.Add(eyes);

    /// <summary>The form on show aims the gaze: beside the menu while open, the circle beside an activity, else in the island.</summary>
    public void SetActiveForm(GiliForm form)
    {
        if (_form == form) return;
        _form = form;
        _last = _last with { X = int.MinValue };
    }

    public void SetMusicPlayingInSlot(bool playing)
    {
        _music = playing;
        UpdateMood();
    }

    /// <summary>A floating note came home into the closed island: happy for 0.9 s (PRD 6.3 "Ekspresi Gili" #2).</summary>
    public void BeHappy()
    {
        _happy.Stop();
        _happy.Interval = GiliRules.HappyFor;
        _happy.Start();
        _isHappy = true;
        UpdateMood();
    }

    /// <summary>
    /// The eyes follow this point (screen DIPs) instead of the pointer for a while: a floating note being moved or
    /// flying home, or one just let go outside the island (PRD 6.3 "Arah pandang"). Ends early with <see cref="Unwatch"/>.
    /// </summary>
    public void Watch(Func<Point> target, TimeSpan duration)
    {
        _watch = target;
        _watchUntil = DateTime.UtcNow + duration;
        if (_visible) Aim(target());
    }

    public void Unwatch()
    {
        if (_watch is null) return;
        _watch = null;
        if (_visible) Aim(Held() ?? ToDips(PointerState.Read()));
    }

    /// <summary>An alarm, timer, pomodoro or reminder card shows: startled until it is answered (PRD 6.3 "Ekspresi Gili" #1).</summary>
    public void SetStartled(bool startled)
    {
        if (_startled == startled) return;
        _startled = startled;
        UpdateMood();
    }

    /// <summary>A running timer, stopwatch or focus session holds the slot: focused (PRD 6.3 "Ekspresi Gili" #5).</summary>
    public void SetFocused(bool focused)
    {
        if (_focused == focused) return;
        _focused = focused;
        UpdateMood();
    }

    /// <summary>The minute the slot shows (null without a timer there), read once a second while Gili shows.</summary>
    public Func<int?>? SlotMinute { get; set; }

    /// <summary>The middle of the slot's digits on screen, in DIPs, which a glance looks at.</summary>
    public Func<Point?>? SlotDigits { get; set; }

    /// <summary>Gili shows (Show Gili on, bar visible, no fullscreen): only then are the pointer and the clock read.</summary>
    public void SetVisible(bool visible)
    {
        _shown = visible;
        Apply();
    }

    /// <summary>Nothing is seen while the screen is off or the laptop sleeps, so the eyes stop too (PRD 8.1).</summary>
    public void SetPaused(bool paused)
    {
        _paused = paused;
        Apply();
    }

    private void Apply()
    {
        var visible = _shown && !_paused;
        if (_visible == visible) return;
        _visible = visible;
        if (visible)
        {
            _idle = PointerState.IdleTime();
            _poll.Start();
            _second.Start();
            ScheduleBlink();
            UpdateMood();
            return;
        }
        _poll.Stop();
        _second.Stop();
        _blink.Stop();
        StopStepping();
    }

    public void Dispose() => SetVisible(false);

    private void Poll()
    {
        var sample = PointerState.Read();
        var point = ToDips(sample);
        // A watched note, then a glance at the slot's digits, win over the pointer and are followed every read.
        if (_watch is not null && DateTime.UtcNow >= _watchUntil) _watch = null;
        if (_glancing && DateTime.UtcNow >= _glanceUntil)
        {
            _glancing = false;
            if (_watch is null) Aim(point);
        }
        var held = Held();
        if (held is { } target) Aim(target);
        if (sample.X != _last.X || sample.Y != _last.Y)
        {
            _lastMove = DateTime.UtcNow;
            Wake();
            if (held is null) Aim(point);
            if (_last.Left && sample.Left && !_dragging && (point - _dragFrom).Length >= GiliRules.DragThreshold)
            {
                _dragging = true;
                UpdateMood();
            }
        }
        var pressed = (sample.Left && !_last.Left) || (sample.Right && !_last.Right);
        if (sample.Left && !_last.Left) _dragFrom = point;
        if (!sample.Left && _dragging)
        {
            _dragging = false;
            UpdateMood();
        }
        if (pressed && !Wake() && GiliRules.CanBlink(_mood)) BlinkAll(twice: false);
        _last = sample;
    }

    /// <summary>Input counts as activity at once, so a sleeping Gili wakes on the move or click itself, not a second later.</summary>
    private bool Wake()
    {
        var wasAsleep = _mood == GiliMood.Sleep;
        _idle = TimeSpan.Zero;
        UpdateMood();
        return wasAsleep;
    }

    private void EverySecond()
    {
        _idle = PointerState.IdleTime();
        UpdateMood();
        // Focused on a timer, the eyes glance at the digits for 1.2 s whenever a new minute shows, at most every 4 s.
        var minute = SlotMinute?.Invoke();
        var now = DateTime.UtcNow;
        if (_mood == GiliMood.Focus && GiliRules.Glances(_minute, minute, _glanceAt, now))
        {
            _glanceAt = now;
            _glanceUntil = now + GiliRules.GlanceFor;
            _glancing = true;
            if (Held() is { } digits) Aim(digits);
        }
        _minute = minute;
        // After 8 s of a still pointer the eyes drift back to the middle.
        if (Held() is null && now - _lastMove >= GiliRules.RecentreAfter) Retarget(0, 0);
    }

    /// <summary>What the eyes look at instead of the pointer: a watched note, then the slot's digits during a glance (PRD 6.3 "Arah pandang").</summary>
    private Point? Held()
    {
        if (_watch is { } watched) return watched();
        return _glancing ? SlotDigits?.Invoke() : null;
    }

    private void UpdateMood()
    {
        var mood = GiliRules.Mood(new(_dragging, _idle, _music, _isHappy, _startled, _focused));
        if (mood == _mood) return;
        var wasAsleep = _mood == GiliMood.Sleep;
        _mood = mood;
        foreach (var eyes in _eyes) eyes.SetMood(mood);
        if (mood == GiliMood.Sleep) Retarget(0, 0);
        // Waking up is a double blink.
        if (wasAsleep && _visible) BlinkAll(twice: true);
    }

    private void Aim(Point pointer)
    {
        if (_mood == GiliMood.Sleep || !SystemParameters.ClientAreaAnimation) return;
        if (_eyes.FirstOrDefault(e => e.Form == _form && e.IsVisible) is not { } eyes) return;
        if (PresentationSource.FromVisual(eyes) is null) return;
        var device = eyes.PointToScreen(new Point(eyes.ActualWidth / 2, eyes.ActualHeight / 2));
        var centre = FromDevice(eyes, device);
        var (x, y) = GiliRules.Aim(pointer.X, pointer.Y, centre.X, _form, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
        Retarget(x, y);
    }

    private void Retarget(double x, double y)
    {
        if (_x.Target == x && _y.Target == y) return;
        _x.Target = x;
        _y.Target = y;
        if (!SystemParameters.ClientAreaAnimation)
        {
            _x.SnapTo(x);
            _y.SnapTo(y);
            LookAll();
            return;
        }
        if (_stepping) return;
        _stepping = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastFrame) return;
        var seconds = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        _x.Step(seconds);
        _y.Step(seconds);
        LookAll();
        if (_x.IsAtRest && _y.IsAtRest) StopStepping();
    }

    private void StopStepping()
    {
        if (!_stepping) return;
        _stepping = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private void LookAll()
    {
        foreach (var eyes in _eyes)
        {
            if (eyes.IsVisible) eyes.Look(_x.Value, _y.Value);
        }
    }

    private void BlinkAll(bool twice)
    {
        foreach (var eyes in _eyes)
        {
            if (eyes.IsVisible) eyes.Blink(twice);
        }
    }

    private void ScheduleBlink()
    {
        var (wait, twice) = GiliRules.NextBlink(_random);
        _nextIsDouble = twice;
        _blink.Stop();
        _blink.Interval = wait;
        _blink.Start();
    }

    private void RandomBlink()
    {
        if (GiliRules.CanBlink(_mood)) BlinkAll(_nextIsDouble);
        ScheduleBlink();
    }

    private Point ToDips(PointerSample sample)
    {
        var eyes = _eyes.FirstOrDefault(e => PresentationSource.FromVisual(e) is not null);
        return eyes is null ? new Point(sample.X, sample.Y) : FromDevice(eyes, new Point(sample.X, sample.Y));
    }

    private static Point FromDevice(Visual visual, Point device) =>
        PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformFromDevice.Transform(device) ?? device;
}
