using System.Windows.Media;
using System.Windows.Threading;
using Gili.Core.Alarms;
using Gili.Core.Clipboard;
using Gili.Core.Devices;
using Gili.Core.Island;
using Gili.Core.Power;
using Gili.Core.Settings;
using Gili.Core.Sound;
using Gili.Core.Timers;
using Gili.Core.Todos;
using Gili.Platform.Devices;
using Gili.Platform.Modules;
using Gili.Platform.Power;

namespace Gili.App.Island;

/// <summary>Feeds the providers into the IslandCoordinator and runs its timers for wide notices and reminder cards.</summary>
public sealed class IslandController : IDisposable
{
    private readonly IslandCoordinator _coordinator = new(() => DateTimeOffset.Now);
    private readonly DispatcherTimer _deadline = new(DispatcherPriority.Normal);
    private readonly PowerMonitor _power = new();
    private readonly ClipboardService _clipboard = new();
    private readonly LockKeyMonitor _lockKeys = new();
    private readonly PrivacyMonitor _privacy = new();
    private readonly Func<AppSettings> _settings;
    private BatteryAlerts _battery = new();
    private LockKeys _demoKeys;
    private PageSet _hiddenTabs;
    // The alarm each alarm card stands for, as it was when it rang, and whether it was late.
    private readonly Dictionary<string, (Alarm Alarm, bool Missed)> _ringing = [];
    // The task each reminder card stands for, as it was when it came.
    private readonly Dictionary<string, Todo> _reminders = [];
    private bool _demo;
    private string? _soundCard;

    public IslandController(Dispatcher dispatcher, MediaService media, TimerService timers, AlarmService alarms, TodoService todos, NoteService notes,
        BluetoothMonitor bluetooth, Func<AppSettings> settings)
    {
        _settings = settings;
        Media = media;
        Timers = timers;
        Alarms = alarms;
        Todos = todos;
        Notes = notes;
        _deadline.Tick += (_, _) =>
        {
            _deadline.Stop();
            _coordinator.Tick();
            ScheduleDeadline();
        };
        _coordinator.Changed += view =>
        {
            ScheduleDeadline();
            ViewChanged?.Invoke(view);
            // A reminder that closed itself is gone from the queue; its task goes too.
            foreach (var card in _reminders.Keys.Where(id => !_coordinator.HasCard(id)).ToList()) _reminders.Remove(card);
            if (view.Card?.Id == _soundCard) return;
            _soundCard = view.Card?.Id;
            CardShown?.Invoke(_soundCard);
        };
        media.Updated += () => _coordinator.SetActivity(ActivityKind.Media, media.IsActive);
        _power.Changed += snapshot => dispatcher.InvokeAsync(() =>
        {
            // During the demo real readings wait; EndDemo reads the charger again.
            if (!_demo) ApplyPower(snapshot);
        });
        ApplyPower(PowerMonitor.Read());
        // Lock keys, Bluetooth devices and the privacy dots wait during the demo like the charger does.
        _lockKeys.Changed += (before, now) => dispatcher.InvokeAsync(() =>
        {
            if (_demo) return;
            foreach (var notice in LockKeyRules.WideFor(before, now)) _coordinator.ShowWide(notice);
        });
        bluetooth.DeviceChanged += change => dispatcher.InvokeAsync(() =>
        {
            if (!_demo) _coordinator.ShowWide(BluetoothRules.WideFor(change));
        });
        _privacy.Changed += use => dispatcher.InvokeAsync(() =>
        {
            if (!_demo) SetPrivacy(use);
        });
        _privacy.Start();
        _clipboard.Copied += ShowClipboard;
        timers.Changed += ApplyTimers;
        timers.Finished += id => _coordinator.ShowCard(new IslandCard(id, CardPriority.Urgent, TimeSpan.Zero));
        timers.Stale += id => DismissCard(id);
        timers.Relabeled += _ => RefreshCard();
        ApplyTimers();
        alarms.Ringing += (alarm, missed) => Ring(alarm, missed, AlarmCard(alarm.Id));
        alarms.Stale += id => DismissCard(AlarmCard(id));
        todos.Reminding += todo => Remind(todo, TodoCard(todo.Id));
        todos.Stale += id => DismissCard(TodoCard(id));
    }

    public TodoService Todos { get; }

    public NoteService Notes { get; }

    /// <summary>The demo's task reminder, shown like a real one and never saved (F-BAR-09).</summary>
    public void ShowDemoReminder(string title, TimeOnly time) =>
        Remind(new Todo(0, title, null, time, null, null, 0, DateTimeOffset.UtcNow), Gili.Core.Demo.DemoReminder.CardId);

    private static string TodoCard(long id) => $"todo:{id}";

    /// <summary>A reminder card shows for 10 s, waits while a focus phase runs, and is dropped in fullscreen (PRD 6.3).</summary>
    private void Remind(Todo todo, string cardId)
    {
        _reminders[cardId] = todo;
        _coordinator.ShowCard(new IslandCard(cardId, CardPriority.Reminder, TimeSpan.FromSeconds(10)));
    }

    public TimerService Timers { get; }

    public AlarmService Alarms { get; }

    /// <summary>Whether the clock reads 24-hour, for the alarm card's title.</summary>
    public Func<bool> Use24Hour { get; set; } = () => true;

    /// <summary>The sound an alarm card rings: its alarm's own (PRD 6.6); null for other cards.</summary>
    public string? AlarmSound(string? cardId) => cardId is not null && _ringing.TryGetValue(cardId, out var r) ? r.Alarm.Sound : null;

    /// <summary>The Alarms page's Manage alarms: Tools opens on its Alarms tab.</summary>
    public event Action<string>? ToolsRequested;

    public void ManageAlarms()
    {
        Close();
        ToolsRequested?.Invoke("Alarms");
    }

    /// <summary>The demo's alarm, shown like a real one and never saved (F-BAR-09).</summary>
    public void ShowDemoAlarm(TimeOnly time, string label) =>
        Ring(new Alarm(0, time, Gili.Core.Time.Days.None, label, "Radar", true, null), missed: false, Gili.Core.Demo.DemoAlarm.CardId);

    private static string AlarmCard(long id) => $"alarm:{id}";

    /// <summary>An alarm's card is urgent and stays until answered; two at once wait in turn (F-TOOL-05).</summary>
    private void Ring(Alarm alarm, bool missed, string cardId)
    {
        _ringing[cardId] = (alarm, missed);
        _coordinator.ShowCard(new IslandCard(cardId, CardPriority.Urgent, TimeSpan.Zero));
    }

    /// <summary>The card now showing, or null when none: what plays its sound (PRD 6.6).</summary>
    public event Action<string?>? CardShown;

    /// <summary>The volume or mute changed while a card shows: its warning and Unmute button follow.</summary>
    public event Action? CardChanged;

    /// <summary>The default speaker's volume and mute, for the card's warning (F-SND-05).</summary>
    public Func<(int Percent, bool Muted)?>? Volume { get; set; }

    /// <summary>Unmutes Windows, only from the card's button.</summary>
    public Action? Unmute { get; set; }

    public void RefreshCard()
    {
        if (View.Card is not null) CardChanged?.Invoke();
    }

    /// <summary>
    /// What a card shows and what its buttons do (PRD 6.3 "Kartu notifikasi", mockup CARDS). Each button closes the card
    /// after its action.
    /// </summary>
    public CardContent? CardFor(string id)
    {
        var red = Color.FromRgb(0xFF, 0x45, 0x3A);
        var orange = Color.FromRgb(0xFF, 0x9F, 0x0A);
        var green = Color.FromRgb(0x30, 0xD1, 0x58);
        CardContent? content = id switch
        {
            "timer" when Timers.DemoLabel is { } label => new("Icon.Hourglass", orange, TimerText.LabeledDone(label),
                TimerText.LabeledSub(Timers.Timer.TotalSeconds),
                [new("Repeat", false, Card(id, Timers.Repeat)), new("Close", true, Card(id, () => { }))]),
            "timer" => new("Icon.Hourglass", orange, TimerText.TimerDone(Timers.Timer.TotalSeconds), null,
                [new("Repeat", false, Card(id, Timers.Repeat)), new("Close", true, Card(id, () => { }))]),
            _ when LabeledTimerOf(id) is { } labeled => new("Icon.Hourglass", orange, TimerText.LabeledDone(labeled.Label),
                TimerText.LabeledSub(labeled.Clock.TotalSeconds),
                [new("Repeat", false, Card(id, () => Timers.RepeatTimer(labeled.Id))), new("Close", true, Card(id, () => { }))]),
            "pomo-focus" => new("Icon.Hourglass", red, "Focus session done", TimerText.FocusDone(Timers.Pomodoro, Timers.Plan),
                [new("Skip", false, Card(id, Timers.SkipBreak)), new("Start break", true, Card(id, Timers.StartBreak))]),
            "pomo-break" => new("Icon.Hourglass", green, "Break over", TimerText.BreakOver(Timers.Pomodoro, Timers.Plan),
                [new("Stop", false, Card(id, Timers.StopPomodoro)), new("Start focus", true, Card(id, Timers.StartFocus))]),
            _ when _ringing.TryGetValue(id, out var ringing) => AlarmContent(id, ringing.Alarm, ringing.Missed, orange),
            _ when _reminders.TryGetValue(id, out var todo) => ReminderContent(id, todo, green),
            _ => null,
        };
        return content is null ? null : WithMuteWarning(id, content);
    }

    private Gili.Core.Timers.LabeledTimer? LabeledTimerOf(string cardId) =>
        Timers.Labeled.FirstOrDefault(t => TimerService.CardId(t.Id) == cardId);

    /// <summary>The task, "Due 15:00 today", Snooze 10m and Done (mockup CARDS.todo).</summary>
    private CardContent ReminderContent(string id, Todo todo, Color green)
    {
        var demo = todo.Id == 0;
        return new("Icon.Todo", green, todo.Title, TodoText.CardSubtitle(todo, Todos.Today, Use24Hour()),
            [
                new("Snooze 10m", false, Card(id, () => { if (!demo) Todos.Snooze(todo.Id); })),
                new("Done", true, Card(id, () => { if (!demo) Todos.SetDone(todo.Id, true); })),
            ]);
    }

    /// <summary>"Alarm 14:30" or "Missed alarm 14:30", the label, Snooze 5m and Stop (mockup CARDS.alarm).</summary>
    private CardContent AlarmContent(string id, Alarm alarm, bool missed, Color orange)
    {
        var demo = alarm.Id == 0;
        return new("Icon.Alarm", orange, AlarmText.CardTitle(alarm, missed, Use24Hour()), AlarmText.Label(alarm),
            [
                new("Snooze 5m", false, Card(id, () => { if (!demo) Alarms.Snooze(alarm.Id); })),
                new("Stop", true, Card(id, () => { })),
            ],
            Shake: true);
    }

    /// <summary>
    /// A card that rings until answered says when nothing will be heard (muted or under 5%), with Unmute first when
    /// Windows is muted (PRD 6.6, plan 2c decision 3).
    /// </summary>
    private CardContent WithMuteWarning(string id, CardContent card)
    {
        if (SoundRules.ForCard(id, "Chime") is not { Loop: true } || Volume?.Invoke() is not { } volume) return card;
        if (!SoundRules.WarnMuted(volume.Percent, volume.Muted)) return card;
        var buttons = volume.Muted ? [new CardButton("Unmute", false, () => Unmute?.Invoke()), .. card.Buttons] : card.Buttons;
        return card with { Warning = SoundRules.Warning, Buttons = buttons };
    }

    private Action Card(string id, Action action) => () =>
    {
        action();
        DismissCard(id);
    };

    /// <summary>A running timer or pomodoro and a running stopwatch are activities; a focus phase holds reminders (F-TOOL-04).</summary>
    private void ApplyTimers()
    {
        _coordinator.SetActivity(ActivityKind.Timer, Timers.Timer.IsRunning || Timers.Pomodoro.Clock.IsRunning || Timers.SoonestTimer is not null);
        _coordinator.SetActivity(ActivityKind.Stopwatch, Timers.Stopwatch.IsRunning);
        _coordinator.SetFocus(Timers.Pomodoro.IsFocusRunning);
    }

    /// <summary>The latest copy's snippet and its thumbnail (images only), shown while it holds the slot (F-ISL-17).</summary>
    public (ClipboardSnippet Snippet, ImageSource? Thumb)? Clipboard { get; private set; }

    public event Action? ClipboardChanged;

    /// <summary>The demo's copy, shown exactly like a real one (F-BAR-09).</summary>
    public void ShowDemoClipboard(ClipboardSnippet snippet) => ShowClipboard(snippet, null);

    public IslandView View => _coordinator.View;

    public MediaService Media { get; }

    public PowerSnapshot? Power { get; private set; }

    public event Action<IslandView>? ViewChanged;

    public event Action? PowerChanged;

    /// <summary>The apps using the camera and the microphone, for the dots beside the island (F-ISL-14).</summary>
    public PrivacyUse Privacy { get; private set; } = PrivacyUse.None;

    public event Action? PrivacyChanged;

    /// <summary>
    /// Settings hid tabs: the island leaves a page that just went hidden for its nearest shown neighbour (plan 3a). A
    /// hidden page opened directly afterwards stays, with its tab shown for as long as it is open.
    /// </summary>
    public void ApplyHiddenTabs(PageSet hidden)
    {
        var added = hidden & ~_hiddenTabs;
        _hiddenTabs = hidden;
        var view = _coordinator.View;
        if (view.Mode == IslandMode.Expanded && view.Page != IslandPage.Controls && added.HasFlag(IslandTabs.Of(view.Page)))
            _coordinator.Open(IslandTabs.Fallback(hidden, view.Page));
    }

    /// <summary>Lock keys, and the packaged copy's privacy read, stop while the bar pauses its polling (PRD 8.1).</summary>
    public void SetPaused(bool paused)
    {
        _lockKeys.SetPaused(paused);
        _privacy.SetPaused(paused);
    }

    public void OpenFromIsland() => OpenActivity(View.Slot);

    public void OpenBubble() => OpenActivity(View.Bubble);

    /// <summary>
    /// A timer or stopwatch opens the Timer page on its own segment. A labeled timer in the slot opens Tools on its Timer
    /// tab instead, and the island stays compact (plan 2g decision 2).
    /// </summary>
    private void OpenActivity(ActivityKind? kind)
    {
        if (kind == ActivityKind.Timer && Timers.SlotShowsLabeled)
        {
            ToolsRequested?.Invoke("Timer");
            return;
        }
        if (kind == ActivityKind.Stopwatch) Timers.Mode = TimerMode.Stopwatch;
        else if (kind == ActivityKind.Timer) Timers.Mode = Timers.Pomodoro.Clock.IsRunning ? TimerMode.Pomodoro : TimerMode.Timer;
        _coordinator.OpenFor(kind);
    }

    public void Open(IslandPage page) => _coordinator.Open(page);

    public void Toggle(IslandPage page) => _coordinator.Toggle(page);

    public void Close() => _coordinator.Close();

    public void DismissCard(string id)
    {
        _coordinator.DismissCard(id);
        _ringing.Remove(id);
        _reminders.Remove(id);
    }

    /// <summary>The pointer went down outside the open island.</summary>
    public void Dismiss() => _coordinator.Dismiss();

    /// <summary>
    /// The demo starts from a laptop on battery at full, so its charger, battery and lock key notices show the same on
    /// every machine (F-BAR-09).
    /// </summary>
    public void BeginDemo()
    {
        _demo = true;
        _battery = new BatteryAlerts();
        _battery.Update(new PowerSnapshot(HasBattery: true, PluggedIn: false, Charging: false, 100), 20, 80);
        _demoKeys = default;
    }

    /// <summary>The demo's charger, shown like a real reading, wide notice included (F-BAR-09).</summary>
    public void SetDemoPower(PowerSnapshot snapshot)
    {
        _demo = true;
        ApplyPower(snapshot);
    }

    /// <summary>The demo's lock keys, compared with the demo's own last state (F-BAR-09).</summary>
    public void SetDemoLockKeys(LockKeys keys)
    {
        foreach (var notice in LockKeyRules.WideFor(_demoKeys, keys)) _coordinator.ShowWide(notice);
        _demoKeys = keys;
    }

    public void ShowDemoBluetooth(BluetoothChange change) => _coordinator.ShowWide(BluetoothRules.WideFor(change));

    public void SetDemoPrivacy(PrivacyUse use) => SetPrivacy(use);

    /// <summary>Closes the island and puts the real charger state back, without a wide notice for the change.</summary>
    public void EndDemo()
    {
        Close();
        if (!_demo) return;
        _demo = false;
        var real = PowerMonitor.Read();
        Power = real;
        PowerChanged?.Invoke();
        _coordinator.SetActivity(ActivityKind.Charging, PowerRules.IsCharging(real));
        // The real state comes back quietly: a fresh first reading, the keys as they are, and the real dots.
        _battery = new BatteryAlerts();
        _battery.Update(real, _settings().BatteryAlert, _settings().UnplugReminder);
        SetPrivacy(_privacy.Current);
    }

    public void SetFullscreen(bool fullscreen) => _coordinator.SetFullscreen(fullscreen);

    public void Dispose()
    {
        _clipboard.Dispose();
        _deadline.Stop();
        _power.Dispose();
        _lockKeys.Dispose();
        _privacy.Dispose();
    }

    private void ShowClipboard(ClipboardSnippet snippet, ImageSource? thumb)
    {
        Clipboard = (snippet, thumb);
        ClipboardChanged?.Invoke();
        _coordinator.ShowClipboard();
    }

    private void ApplyPower(PowerSnapshot snapshot)
    {
        var settings = _settings();
        var notices = _battery.Update(snapshot, settings.BatteryAlert, settings.UnplugReminder);
        Power = snapshot;
        PowerChanged?.Invoke();
        _coordinator.SetActivity(ActivityKind.Charging, PowerRules.IsCharging(snapshot));
        foreach (var notice in notices) _coordinator.ShowWide(notice);
    }

    private void SetPrivacy(PrivacyUse use)
    {
        if (use == Privacy) return;
        Privacy = use;
        PrivacyChanged?.Invoke();
    }

    private void ScheduleDeadline()
    {
        _deadline.Stop();
        if (_coordinator.NextDeadline is not { } at) return;
        var wait = at - DateTimeOffset.Now;
        _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
        _deadline.Start();
    }
}
