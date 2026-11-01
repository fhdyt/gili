using System.Windows.Media;
using System.Windows.Threading;
using Gili.Core.Alarms;
using Gili.Core.Claude;
using Gili.Core.Clipboard;
using Gili.Core.Devices;
using Gili.Core.FileOps;
using Gili.Core.Island;
using Gili.Core.Power;
using Gili.Core.Settings;
using Gili.Core.Shelf;
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
    private readonly ClipboardService _clipboard;
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
        ProjectService projects, BluetoothMonitor bluetooth, ShelfService shelf, Func<AppSettings> settings, ClipboardHistory clipboardHistory, ClipboardImages? clipboardImages = null)
    {
        ClipboardHistory = clipboardHistory;
        ClipboardImages = clipboardImages;
        _clipboard = new ClipboardService(clipboardHistory, clipboardImages);
        _settings = settings;
        Shelf = shelf;
        Lyrics = new LyricsService(media, settings);
        _dragLeave.Tick += (_, _) => EndDrag();
        _shelfCheck.Tick += (_, _) =>
        {
            Shelf.Prune();
            Shelf.SweepLeftovers(DateTimeOffset.UtcNow);
            SyncShelfCheck();
        };
        _demoDrop.Tick += (_, _) => DropDemo();
        _demoCut.Tick += (_, _) =>
        {
            _demoCut.Stop();
            DragEntered(overIsland: true, ShelfMode.Cut);
        };
        shelf.Changed += SyncShelfCheck;
        SyncShelfCheck();
        // Files waiting on the Shelf hold the slot above every running activity (issue 37).
        shelf.Changed += () => _coordinator.SetActivity(ActivityKind.Shelf, shelf.Items.Count > 0);
        _coordinator.SetActivity(ActivityKind.Shelf, shelf.Items.Count > 0);
        _dispatcher = dispatcher;
        _updateCheck.Tick += (_, _) => RefreshUpdates();
        _updateCheck.Start();
        RefreshUpdates();
        Media = media;
        Timers = timers;
        Alarms = alarms;
        Todos = todos;
        Notes = notes;
        Projects = projects;
        _deadline.Tick += (_, _) =>
        {
            _deadline.Stop();
            _coordinator.Tick();
            ScheduleDeadline();
        };
        _windowSecond.Tick += (_, _) => ClaudeWindowTick?.Invoke();
        _coordinator.Changed += view =>
        {
            ScheduleDeadline();
            SyncWindowSecond();
            // Files gone since the last look leave as the Shelf page opens (PRD 6.3).
            var shelfOpen = view.Mode == IslandMode.Expanded && view.Page == IslandPage.Shelf;
            if (shelfOpen && !_shelfOpen) Shelf.Prune();
            _shelfOpen = shelfOpen;
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

    public ProjectService Projects { get; }

    public ShelfService Shelf { get; }

    /// <summary>Lyrics of the playing song (F-ISL-26).</summary>
    public LyricsService Lyrics { get; }

    /// <summary>Explorer's copy, move and delete progress (F-ISL-15); set once by the app.</summary>
    public ExplorerOpsService? FileOps { get; private set; }

    /// <summary>The slot or the conflict of Explorer's operations changed.</summary>
    public event Action? FileOpsChanged;

    public const string FileConflictCard = "fileop-conflict";

    // The conflict the user answered from the card (or whose answer couldn't reach Explorer): that one doesn't bring the
    // card back, and the slot says Needs attention meanwhile. Explorer's next question, even right away, gets its own card.
    private string? _answeredConflict;

    private static string ConflictKey(FileOp conflict) => conflict.Conflict + "\n" + conflict.Question + "\n" + conflict.Label;

    public void AttachFileOps(ExplorerOpsService ops)
    {
        FileOps = ops;
        ops.Changed += () =>
        {
            _coordinator.SetActivity(ActivityKind.FileOperation, ops.Slot is not null);
            if (ops.Conflict is null)
            {
                _answeredConflict = null;
                if (_coordinator.HasCard(FileConflictCard)) _coordinator.DismissCard(FileConflictCard);
            }
            // A conflict opens the island as a silent card, priority 1 (PRD 6.3 "Konflik file").
            else if (ConflictKey(ops.Conflict) != _answeredConflict && !_coordinator.HasCard(FileConflictCard))
                _coordinator.ShowCard(new IslandCard(FileConflictCard, CardPriority.Urgent, TimeSpan.Zero));
            FileOpsChanged?.Invoke();
        };
    }

    private CardContent? FileConflictContent(string id)
    {
        if (FileOps?.Conflict is not { } conflict) return null;
        var yellow = Color.FromRgb(0xFF, 0xD6, 0x0A);
        Action Answer(FileOpAnswer answer) => Card(id, async () =>
        {
            _answeredConflict = ConflictKey(conflict);
            if (!await FileOps.AnswerAsync(answer)) FileOps.ShowDialog();
        });
        Action open = Card(id, () =>
        {
            _answeredConflict = ConflictKey(conflict);
            FileOps.ShowDialog();
        });
        IReadOnlyList<CardButton> buttons = conflict.Conflict switch
        {
            FileOpConflict.Admin => [new("Open dialog", true, open)],
            FileOpConflict.InUse => [new("Skip", false, Answer(FileOpAnswer.Skip)), new("Try again", true, Answer(FileOpAnswer.Retry))],
            _ => [new("Skip", false, Answer(FileOpAnswer.Skip)), new("Replace", true, Answer(FileOpAnswer.Replace))],
        };
        return new CardContent("Icon.Alert", yellow, FileOpRules.ConflictTitle(conflict.Conflict), conflict.Question ?? conflict.Label, buttons, Pulse: false);
    }

    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _updateCheck = new() { Interval = TimeSpan.FromMinutes(30) };
    private bool _checkingUpdates;

    /// <summary>Windows has updates waiting: Power shows "Update and …" and the bar's icon an orange dot (F-RIGHT-08).</summary>
    public bool UpdatesPending { get; private set; }

    public event Action? UpdatesChanged;

    /// <summary>Read at start, when the Power island opens, and every 30 minutes (PRD 9.2), off the UI thread.</summary>
    public void RefreshUpdates()
    {
        if (_checkingUpdates) return;
        _checkingUpdates = true;
        Task.Run(PendingUpdates.Read).ContinueWith(read => _dispatcher.InvokeAsync(() =>
        {
            _checkingUpdates = false;
            var pending = read.IsCompletedSuccessfully && read.Result;
            if (pending == UpdatesPending) return;
            UpdatesPending = pending;
            UpdatesChanged?.Invoke();
        }), TaskScheduler.Default);
    }

    private readonly DispatcherTimer _dragLeave = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private readonly DispatcherTimer _shelfCheck = new() { Interval = TimeSpan.FromMinutes(1) };
    private readonly DispatcherTimer _demoDrop = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _demoCut = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _dragging;
    private bool _shelfOpen;
    private bool _shelfPaused;
    private ShelfItem? _demoShelf;

    /// <summary>Set while a Shelf tile is being dragged out, so Gili's own drag doesn't open the drop zone.</summary>
    public bool DraggingOut { get; set; }

    /// <summary>Whether the pointer of a file drag is over the island itself (the drop zone lights up, mockup .hot).</summary>
    public bool DropHot { get; private set; }

    /// <summary>The drop zone's column under the pointer: Copy on the left, Cut on the right (F-ISL-30).</summary>
    public ShelfMode DropMode { get; private set; }

    /// <summary>Gili's own Shelf folder, for files from a zip or the temp folder (F-ISL-31).</summary>
    public Gili.Platform.Shelf.ShelfFolder? ShelfFolder { get; init; }

    public event Action? DropHotChanged;

    /// <summary>A drop landed on the Shelf; the left icon bounces (PRD 6.2).</summary>
    public event Action? ShelfAdded;

    /// <summary>
    /// Files dragged over the bar or the island turn the island into the drop zone (PRD 6.3). Leaving is held for
    /// 150 ms, so moving from the bar onto the island doesn't make it flicker (plan 3d).
    /// </summary>
    public void DragEntered(bool overIsland, ShelfMode mode = ShelfMode.Copy)
    {
        if (DraggingOut) return;
        _dragLeave.Stop();
        SetDropHot(overIsland, mode);
        if (_dragging) return;
        _dragging = true;
        _coordinator.SetDropZone(true);
    }

    public void DragLeft()
    {
        if (!_dragging) return;
        _dragLeave.Stop();
        _dragLeave.Start();
    }

    /// <summary>
    /// Adds what was dropped as one item in its column and opens the Shelf page; false when the Shelf is full. Files in
    /// the temp folder are copied into Gili's Shelf folder first, on a worker thread, since they vanish (F-ISL-31).
    /// </summary>
    public bool DropFiles(IReadOnlyList<string> paths, ShelfMode mode = ShelfMode.Copy)
    {
        EndDrag();
        if (Shelf.IsFull) return false;
        _ = AddDropped(paths, mode);
        return true;
    }

    private async Task AddDropped(IReadOnlyList<string> paths, ShelfMode mode)
    {
        var (kept, folder) = ShelfFolder is null ? (paths, null) : await ShelfFolder.KeepTempFiles(paths, System.IO.Path.GetTempPath());
        Added(Shelf.Add(kept, DateTimeOffset.UtcNow, mode, folder), folder);
    }

    /// <summary>
    /// Files dragged without a path (a zip folder, an Outlook attachment): read into a new folder of Gili's own while
    /// the drop lasts; a drop over 4 GB is refused with a notice (F-ISL-31).
    /// </summary>
    public bool DropVirtualFiles(System.Runtime.InteropServices.ComTypes.IDataObject data, ShelfMode mode)
    {
        EndDrag();
        if (Shelf.IsFull || ShelfFolder is null || Gili.Platform.Shelf.VirtualFiles.Describe(data) is not { Count: > 0 } entries) return false;
        if (!ShelfDrop.FitsLimit(entries.Sum(e => e.Size)))
        {
            _coordinator.ShowWide(new WideEvent("shelf-too-big", WideIcon.TrayDown, WideTone.Warn,
                [new WideRun("Too big for the Shelf"), new WideRun("· 4 GB at most", WideRunStyle.Muted, SpaceBefore: true)], TimeSpan.FromSeconds(2.4)));
            return false;
        }
        var folder = ShelfFolder.Create();
        var paths = Gili.Platform.Shelf.VirtualFiles.Save(data, entries, folder);
        return Added(Shelf.Add(paths, DateTimeOffset.UtcNow, mode, folder), folder);
    }

    private bool Added(ShelfItem? item, string? folder)
    {
        if (item is null)
        {
            if (folder is not null) ShelfFolder?.Delete(folder);
            return false;
        }
        ShelfAdded?.Invoke();
        _coordinator.Open(IslandPage.Shelf);
        return true;
    }

    /// <summary>The Shelf's one-minute check for missing files stops with the rest of the polling (PRD 8.1).</summary>
    private void PauseShelfCheck(bool paused)
    {
        _shelfPaused = paused;
        SyncShelfCheck();
    }

    /// <summary>
    /// The demo's drop: a second of drop zone, the pointer over Copy and then over Cut (F-ISL-30), then its item on the
    /// Shelf page as a Cut, never saved (F-BAR-09).
    /// </summary>
    public void ShowDemoShelf(string name, long size)
    {
        _demo = true;
        _demoShelf = new ShelfItem(-1, [System.IO.Path.Combine(@"C:\Demo", name)], name, size, -1, DateTimeOffset.UtcNow, ShelfMode.Cut);
        DragEntered(overIsland: true, ShelfMode.Copy);
        _demoCut.Start();
        _demoDrop.Start();
    }

    /// <summary>The demo's item leaves the Shelf, as if it was dragged out.</summary>
    public void HideDemoShelf()
    {
        _demoShelf = null;
        Shelf.ShowDemo(null);
    }

    private void DropDemo()
    {
        _demoDrop.Stop();
        _demoCut.Stop();
        EndDrag();
        if (_demoShelf is null) return;
        Shelf.ShowDemo(_demoShelf);
        ShelfAdded?.Invoke();
        _coordinator.Open(IslandPage.Shelf);
    }

    private void EndDrag()
    {
        _dragLeave.Stop();
        SetDropHot(false, ShelfMode.Copy);
        if (!_dragging) return;
        _dragging = false;
        _coordinator.SetDropZone(false);
    }

    private void SetDropHot(bool hot, ShelfMode mode)
    {
        if (DropHot == hot && DropMode == mode) return;
        DropHot = hot;
        DropMode = mode;
        DropHotChanged?.Invoke();
    }

    private void SyncShelfCheck()
    {
        // Leftover folders of items taken out keep the minute check running until they are deleted (issue #38).
        if ((Shelf.Items.Count > 0 || Shelf.HasLeftovers) && !_shelfPaused) _shelfCheck.Start();
        else _shelfCheck.Stop();
    }

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
            FileConflictCard => FileConflictContent(id),
            ClaudeCards.CardId when _claudeCard is { } claude => ClaudeContent(id, claude),
            ClaudeResetCard => new("Icon.Hourglass", Color.FromRgb(0xD9, 0x77, 0x57), "Claude usage reset", "Your 5-hour limit is available again",
                [
                    new("Close", false, Card(id, () => { })),
                    new("Open Claude", true, Card(id, () =>
                    {
                        if (OpenClaude?.Invoke() != true) _coordinator.Open(IslandPage.Claude);
                    })),
                ]),
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
            ],
            Pulse: false);
    }

    /// <summary>
    /// The Claude glyph in yellow without a pulse, the title for the wait, the session, its question or message, Later
    /// and Open Claude / Open terminal (mockup CARDS.claude).
    /// </summary>
    private CardContent ClaudeContent(string id, ClaudeCardView card)
    {
        var yellow = Color.FromRgb(0xFF, 0xD6, 0x0A);
        void Closed() => ClaudeCardClosed?.Invoke();
        return new("Icon.Claude", yellow, card.Title, card.Name,
            [
                new("Later", false, Card(id, Closed)),
                new(card.OpenLabel, true, Card(id, () =>
                {
                    Closed();
                    OpenClaudeSession(card.SessionId);
                })),
            ],
            Quote: card.Quote, Filled: true, Pulse: false);
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

    /// <summary>The Claude activity in the slot (F-CLD-02); <see cref="ClaudeService"/> sets it through the App.</summary>
    public ClaudeSlotView Claude { get; private set; } = ClaudeSlotView.Empty;

    /// <summary>Every live session, for the Claude page (F-CLD-05).</summary>
    public IReadOnlyList<ClaudeSession> ClaudeSessions { get; private set; } = [];

    /// <summary>Sessions that finished unseen, for the green bubble (F-CLD-04).</summary>
    public IReadOnlyList<ClaudeSession> ClaudeUnseen { get; private set; } = [];

    public event Action? ClaudeChanged;

    /// <summary>A session's window should come forward and its finish count as seen (F-CLD-06).</summary>
    public event Action<string>? ClaudeSessionRequested;

    /// <summary>The Needs input card was closed by Later or Open (F-CLD-03).</summary>
    public event Action? ClaudeCardClosed;

    /// <summary>All sessions are one activity: waiting or just done at order 2, working at order 6 (PRD 6.3).</summary>
    public void SetClaude(ClaudeSlotView view, IReadOnlyList<ClaudeSession> sessions)
    {
        Claude = view;
        ClaudeSessions = sessions;
        ClaudeUnseen = ClaudeSlot.Unseen(sessions);
        _coordinator.SetActivity(ActivityKind.ClaudeAttention, view.State is ClaudeSlotState.NeedsInput or ClaudeSlotState.Done);
        _coordinator.SetActivity(ActivityKind.ClaudeWorking, view.State == ClaudeSlotState.Working);
        _coordinator.SetClaudeUnseen(ClaudeUnseen.Count > 0);
        ClaudeChanged?.Invoke();
    }

    public void OpenClaudeSession(string id) => ClaudeSessionRequested?.Invoke(id);

    private ClaudeCardView? _claudeCard;

    /// <summary>The card for a new wait: 8 s, held during a focus phase, Ping once (PRD 6.3 "Kartu Needs input").</summary>
    public void ShowClaudeCard(ClaudeCardView card)
    {
        _claudeCard = card;
        _coordinator.ShowCard(new IslandCard(ClaudeCards.CardId, CardPriority.Reminder, TimeSpan.FromSeconds(8)));
    }

    /// <summary>The same wait with fuller text: the card on show changes without starting its 8 s again.</summary>
    public void RefreshClaudeCard(ClaudeCardView card)
    {
        _claudeCard = card;
        if (View.Card?.Id == ClaudeCards.CardId) CardChanged?.Invoke();
    }

    /// <summary>The session was answered, stopped or went away: its card goes with it.</summary>
    public void DismissClaudeCard() => _coordinator.DismissCard(ClaudeCards.CardId);

    /// <summary>
    /// The Claude slot (mockup clClick): one session involved brings its window forward and the island stays closed;
    /// more open the Claude page.
    /// </summary>
    private bool ClickClaudeSlot()
    {
        var involved = ClaudeSlot.Involved(ClaudeSessions);
        if (involved.Count != 1) return false;
        OpenClaudeSession(involved[0].Id);
        return true;
    }

    /// <summary>The green bubble: one finished session comes forward, several open the Claude page (PRD 6.3).</summary>
    public void OpenDoneBubble()
    {
        if (ClaudeUnseen.Count == 1) OpenClaudeSession(ClaudeUnseen[0].Id);
        else _coordinator.Open(IslandPage.Claude);
    }

    public const string ClaudeResetCard = "claude-reset";

    /// <summary>The Claude 5-hour window on show (F-CLD-08); null while there is none or its switch is off.</summary>
    public ClaudeUsageWindow? ClaudeWindow { get; private set; }

    /// <summary>A second passed while the window's countdown shows in the slot or on the Claude page (PRD 8.1).</summary>
    public event Action? ClaudeWindowTick;

    /// <summary>
    /// The 5-hour timer is on and Claude Desktop is open, so the Claude page shows the window's row, with No window running
    /// when there is none, where its reset time can be set by hand (revision 18).
    /// </summary>
    public bool ClaudeTimerShown { get; private set; }

    /// <summary>The reset card's Open Claude: the App brings Claude forward, or says it could not.</summary>
    public Func<bool>? OpenClaude { get; set; }

    /// <summary>The reset time shown in Claude Desktop, typed on the Claude page (revision 18).</summary>
    public Action<DateTimeOffset>? SetClaudeReset { get; set; }

    private readonly DispatcherTimer _windowSecond = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>A running window is the last activity; a reached limit comes right after a waiting session (PRD 6.3).</summary>
    public void SetClaudeWindow(ClaudeUsageWindow? window, bool timerShown)
    {
        ClaudeWindow = timerShown ? window : null;
        ClaudeTimerShown = timerShown;
        window = ClaudeWindow;
        _coordinator.SetActivity(ActivityKind.ClaudeLimit, window is { LimitHit: true });
        _coordinator.SetActivity(ActivityKind.ClaudeWindow, window is { LimitHit: false });
        SyncWindowSecond();
        ClaudeChanged?.Invoke();
    }

    /// <summary>The window ended: a card like a finished timer's, held during a focus phase (PRD 6.3).</summary>
    public void ShowClaudeReset() =>
        _coordinator.ShowCard(new IslandCard(ClaudeResetCard, CardPriority.Urgent, TimeSpan.Zero, HoldInFocus: true));

    private void SyncWindowSecond()
    {
        var view = View;
        var shown = ClaudeWindow is not null && (
            (view.Mode == IslandMode.Compact && view.Slot is ActivityKind.ClaudeLimit or ActivityKind.ClaudeWindow)
            || (view.Mode == IslandMode.Expanded && view.Page == IslandPage.Claude));
        _windowSecond.IsEnabled = shown;
    }

    /// <summary>Whether a fullscreen app hides the bar: a new Claude wait is skipped, not kept (PRD 6.3).</summary>
    public bool IsFullscreen { get; private set; }

    /// <summary>The latest copy's snippet and its thumbnail (images only), shown while it holds the slot (F-ISL-17).</summary>
    public (ClipboardSnippet Snippet, ImageSource? Thumb)? Clipboard { get; private set; }

    public event Action? ClipboardChanged;

    /// <summary>The last 50 texts copied (F-LEFT-03).</summary>
    public ClipboardHistory ClipboardHistory { get; }

    /// <summary>The history's images on disk (F-ISL-32); null without a data folder.</summary>
    public ClipboardImages? ClipboardImages { get; }

    /// <summary>A history row was picked: the island closes and the text goes where the user was typing (F-ISL-28).</summary>
    public event Action? PasteRequested;

    /// <summary>
    /// Puts the row on the clipboard (without a snippet or a new row): text as plain text, an image as a bitmap, files
    /// as a file list to copy (F-ISL-32). It moves to the top, then the island closes and pastes it. A row whose image
    /// or files are gone pastes nothing and leaves.
    /// </summary>
    public void Paste(long id)
    {
        if (ClipboardHistory.Items.FirstOrDefault(e => e.Id == id) is not { } entry) return;
        switch (entry.Kind)
        {
            case ClipKind.Image:
                if (ClipboardImages?.Load(entry.Text) is not { } image)
                {
                    ClipboardHistory.Remove(id);
                    return;
                }
                _clipboard.IgnoreNext();
                if (!ClipboardWrite.Image(image)) return;
                break;
            case ClipKind.Files:
                var files = entry.Files.Where(p => System.IO.File.Exists(p) || System.IO.Directory.Exists(p)).ToList();
                if (files.Count == 0)
                {
                    ClipboardHistory.Remove(id);
                    return;
                }
                _clipboard.IgnoreNext();
                if (!ClipboardWrite.Files(files)) return;
                break;
            default:
                _clipboard.Ignore(entry.Text);
                Gili.Platform.Clipboard.ClipboardPaste.SetText(entry.Text);
                break;
        }
        ClipboardHistory.Use(id);
        PasteRequested?.Invoke();
    }

    /// <summary>A short notice from outside the island, like "Updated to Gili 0.2.0" (issue #27).</summary>
    public void ShowNotice(WideEvent wide) => _coordinator.ShowWide(wide);

    /// <summary>No window could take the paste (desktop, admin window): the text waits on the clipboard (PRD 6.3).</summary>
    public void ShowPasteHint() => _coordinator.ShowWide(new WideEvent("clipboard-paste", WideIcon.Clipboard, WideTone.Text,
        [new WideRun("Copied"), new WideRun("· press Ctrl+V to paste", WideRunStyle.Muted, SpaceBefore: true)], TimeSpan.FromSeconds(2)));

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
        PauseShelfCheck(paused);
        if (paused) _updateCheck.Stop();
        else _updateCheck.Start();
    }

    public void OpenFromIsland()
    {
        if (View.Slot is ActivityKind.ClaudeAttention or ActivityKind.ClaudeWorking && ClickClaudeSlot()) return;
        OpenActivity(View.Slot);
    }

    public void OpenBubble() => OpenActivity(View.Bubble);

    /// <summary>
    /// A timer or stopwatch opens the Timer page on its own segment. A labeled timer in the slot opens Tools on its Timer
    /// tab instead, and the island stays compact (plan 2g decision 2).
    /// </summary>
    private void OpenActivity(ActivityKind? kind)
    {
        // Explorer's operation: a waiting conflict opens its card again, otherwise Explorer's dialog comes forward;
        // the island never opens for a file operation (PRD 6.3).
        if (kind == ActivityKind.FileOperation && FileOps is { } ops)
        {
            if (ops.Conflict is not null)
            {
                _answeredConflict = null;
                _coordinator.ShowCard(new IslandCard(FileConflictCard, CardPriority.Urgent, TimeSpan.Zero));
            }
            else ops.ShowDialog();
            return;
        }
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

    /// <summary>Ctrl+Alt+Space (F-TOOL-10): the open island closes; a closed one opens on its activity's or last page.</summary>
    public void ToggleIsland()
    {
        if (View.Mode == IslandMode.Expanded) Close();
        else _coordinator.OpenFor(null);
    }

    public void Toggle(IslandPage page) => _coordinator.Toggle(page);

    public void Toggle(ControlKind control) => _coordinator.Toggle(control);

    public void Open(ControlKind control) => _coordinator.Open(control);

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
        _demoDrop.Stop();
        _demoCut.Stop();
        _demoShelf = null;
        EndDrag();
        Shelf.ShowDemo(null);
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

    public void SetFullscreen(bool fullscreen)
    {
        IsFullscreen = fullscreen;
        _coordinator.SetFullscreen(fullscreen);
    }

    public void Dispose()
    {
        _clipboard.Dispose();
        _deadline.Stop();
        _windowSecond.Stop();
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
