using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Windows;
using System.Windows.Markup;
using Gili.App.Bar;
using Gili.App.Island;
using Gili.App.Mascot;
using Gili.App.Settings;
using Gili.App.Tools;
using Gili.Core;
using Gili.Core.Diagnostics;
using Gili.Core.Island;
using Gili.Core.Modules;
using Gili.Core.Settings;
using Gili.Core.Shelf;
using Gili.Core.Taskbar;
using Gili.Data;
using Gili.Platform.Power;
using Gili.Platform.Shell;
using Gili.Platform.Stats;

namespace Gili.App;

public partial class App : Application
{
    private static readonly InstallKind Kind = AppPaths.KindOf(Environment.ProcessPath, PackageInfo.IsPackaged, AppPaths.InstallDirectory);

    private static readonly DataFolders Data = AppPaths.Data(PackageInfo.LocalFolder());

    private FileLog? _log;
    private SingleInstance? _instance;
    private SettingsHub? _settings;
    private SettingsStore? _settingsStore;
    private ClaudeService? _claude;
    private UpdateService? _updates;
    private TaskbarHider? _taskbar;
    private TaskbarKeeper? _taskbarKeeper;
    private TransparencyWatcher? _transparency;
    private StatsService? _stats;
    private bool _systemPageShown;
    private StatsSnapshot? _lastStats;
    private ClockService? _clock;
    private MediaService? _media;
    private IslandController? _islandController;
    private IslandWindow? _islandWindow;
    private FloatingNotes? _floating;
    private GiliService? _gili;
    private BarWindow? _bar;
    private AppsService? _apps;
    private RightService? _right;
    private DemoService? _demo;
    private Database? _database;
    private TimerService? _timers;
    private AlarmService? _alarms;
    private TodoService? _todos;
    private NoteService? _notes;
    private SoundService? _sound;
    private readonly System.Windows.Threading.DispatcherTimer _backupTimer = new() { Interval = TimeSpan.FromHours(1) };
    private readonly System.Windows.Threading.DispatcherTimer _releaseTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private SettingsWindow? _settingsWindow;
    private ToolsWindow? _toolsWindow;
    private bool? _autostartApplied;
    private StartupState? _startup;
    private bool _quitting;

    public App() => _releaseTimer.Tick += OnReleaseTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--quit"))
        {
            // Returns once the running copy is gone, so the installer can replace its files next (design 5b).
            if (SingleInstance.RequestQuit()) WaitForOthers();
            Shutdown();
            return;
        }
        if (e.Args.Contains("--prepare-uninstall"))
        {
            PrepareUninstall();
            Shutdown();
            return;
        }
        UseEnglishFormatting();
        Directory.CreateDirectory(Data.Root);
        _log = new FileLog(Data.Logs);
        _log.DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
        new CrashHandler(_log, Data.CrashMarker, () =>
        {
            try
            {
                _bar?.ReleaseAppBar();
            }
            finally
            {
                _taskbarKeeper?.Restore();
            }
        }).Attach(this);

        var restarted = e.Args.Contains("--restarted");
        _instance = SingleInstance.Acquire(restarted ? TimeSpan.FromSeconds(5) : TimeSpan.Zero,
            LaunchRules.SignalsRunningCopy(e.Args, PackageInfo.StartedByStartupTask()),
            () => Dispatcher.InvokeAsync(ShowSettings),
            () => Dispatcher.InvokeAsync(Quit));
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        var store = new SettingsStore(Data.Root, AppPaths.KeepFolder);
        _settingsStore = store;
        var restored = RestoreSettings(store);
        var loaded = store.Load();
        if (loaded.WasCorrupt) _log.Write("settings.json could not be read; renamed to settings.json.bak and using defaults");
        _settings = new SettingsHub(store, loaded.Settings, _log);
        OpenDatabase();
        _settings.Changed += SyncAutostart;
        // Restored settings carry the user's own Start with Windows choice; a reinstalled package's task starts switched on.
        if (Kind == InstallKind.Store) _ = restored ? SetStartupTaskAsync(_settings.Current.Autostart) : AdoptStartupTaskAsync();
        else SyncAutostart(_settings.Current);
        _transparency = new TransparencyWatcher();

        _stats = new StatsService(Dispatcher, () => new CpuSampler().Sample, () => new MemorySampler().Sample, () => new NetworkSampler().Sample,
            () => new GpuSampler().Sample, () => new DiskSampler().Sample);
        UpdateStatsDemand();
        _settings.Changed += _ => UpdateStatsDemand();

        _clock = new ClockService();
        _media = new MediaService(Dispatcher, _log);
        _timers = new TimerService(_database is null ? null : new TimerStore(_database), () => _settings.Current.Pomodoro);
        _alarms = new AlarmService(_database is null ? null : new AlarmStore(_database), new WakeTimer());
        _todos = new TodoService(_database is null ? null : new TodoStore(_database));
        _notes = new NoteService(OpenNotes());
        // One Bluetooth watcher serves the bar's icon and the island's connect notices (plan 3a decision 4).
        var bluetooth = new Gili.Platform.Modules.BluetoothMonitor();
        var shelfFolder = new Gili.Platform.Shelf.ShelfFolder(Data.Shelf);
        var shelf = new ShelfService(_database is null ? null : new ShelfStore(_database), ProbeFile, shelfFolder.Delete);
        shelfFolder.Sweep(shelf.KeptFolders);
        var clipboardImages = new ClipboardImages(Data.Clipboard);
        var clipboardHistory = new Core.Clipboard.ClipboardHistory(_database is null ? null : new Data.ClipboardHistoryStore(_database), () => DateTimeOffset.UtcNow,
            clipboardImages.Delete);
        clipboardImages.Sweep(clipboardHistory.ImageFiles);
        _islandController = new IslandController(Dispatcher, _media, _timers, _alarms, _todos, _notes, bluetooth, shelf, () => _settings.Current, clipboardHistory, clipboardImages)
        {
            Use24Hour = () => _settings.Current.Use24HourClock,
            ShelfFolder = shelfFolder,
        };

        _apps = new AppsService(() => _bar is null ? 1 : System.Windows.Media.VisualTreeHelper.GetDpi(_bar).DpiScaleX);
        _right = new RightService(Dispatcher, _settings, _log, bluetooth);
        _right.SetHidden(_settings.Current.HiddenModules);
        _settings.Changed += s => _right.SetHidden(s.HiddenModules);
        _sound = new SoundService(Dispatcher, _settings, _log);
        _islandController.CardShown += id => _sound.PlayForCard(id, _islandController.AlarmSound(id));
        _islandController.Volume = () => _right.Volume;
        _islandController.Unmute = _right.Unmute;
        _right.Changed += _islandController.RefreshCard;
        var timerSound = _settings.Current.TimerSound;
        _settings.Changed += s =>
        {
            // Picking a timer sound in Settings plays it once (PRD 6.6).
            if (s.TimerSound == timerSound) return;
            timerSound = s.TimerSound;
            _sound.Preview(timerSound);
        };
        _bar = new BarWindow(_settings, _transparency, _stats, _clock, _apps, _right);
        _bar.PageRequested += _islandController.Toggle;
        _bar.SettingsRequested += ShowSettings;
        _bar.ToolsRequested += () => ShowTools(ToolsViewModel.AlarmsTab);
        _islandController.ToolsRequested += ShowTools;
        _bar.ClockClicked += () => _islandController.Toggle(Core.Island.IslandPage.Calendar);
        _bar.ControlRequested += _islandController.Toggle;
        _demo = new DemoService(Dispatcher, _islandController, _media);
        _bar.DemoRequested += _demo.Toggle;
        _demo.Changed += () => _bar.SetDemoRunning(_demo.IsRunning);
        _bar.QuitRequested += Quit;
        _bar.TimeChanged += _clock.Refresh;
        _settings.Changed += _ => _timers.ApplyPlan();
        _islandController.ApplyHiddenTabs(_settings.Current.HiddenTabs);
        _settings.Changed += s => _islandController.ApplyHiddenTabs(s.HiddenTabs);
        _bar.TimeChanged += _timers.Reschedule;
        _bar.TimeChanged += _alarms.Reschedule;
        _bar.TimeChanged += _todos.Reschedule;
        _bar.FullscreenChanged += _islandController.SetFullscreen;
        _bar.PollingChanged += poll =>
        {
            _right.SetPaused(!poll);
            _media.SetPaused(!poll);
            _gili?.SetPaused(!poll);
            _islandController.SetPaused(!poll);
            _log?.Write(poll ? "Polling resumed" : $"Polling paused: {_bar.PollingReason}");
        };
        _bar.Show();
        StartTaskbarKeeper();

        // Owned by the bar, so the island stays above it whenever the bar re-asserts its topmost place.
        _gili = new GiliService();
        _islandWindow = new IslandWindow(_islandController, new IslandViewModel(_islandController, _settings), _settings, _gili, _right)
        {
            Owner = _bar,
        };
        _islandWindow.FloatingChanged += _bar.SetIslandFloating;
        _bar.Island = _islandController;
        _islandController.Shelf.Changed += () => _bar.ShowShelf(_islandController.Shelf.FileCount, bounce: false);
        _islandController.ShelfAdded += () => _bar.ShowShelf(_islandController.Shelf.FileCount, bounce: true);
        _bar.ShowShelf(_islandController.Shelf.FileCount, bounce: false);
        // The System page is fed, and reads everything, only while it shows (PRD 8.1).
        _islandController.ViewChanged += v => ShowSystemPage(v.Mode == IslandMode.Expanded && v.Page == IslandPage.System);
        _stats.Updated += s =>
        {
            _lastStats = s;
            if (_systemPageShown) _islandWindow.SystemStats.Apply(s);
        };
        _islandWindow.CompactChanged += _bar.SetIslandCompact;
        _claude = new ClaudeService(Dispatcher, _settings, _log, Kind, _database is null ? null : new Data.ClaudeWindowStore(_database));
        _claude.Changed += ShowClaude;
        _islandController.ClaudeSessionRequested += _claude.Focus;
        _islandController.ClaudeCardClosed += () => _claude.Cards.Close();
        _apps.ForegroundChanged += _claude.OnForeground;
        _claude.UsageEnded += () =>
        {
            if (_claude.InDemo || _settings.Current.ClaudeUsageTimer) _islandController.ShowClaudeReset();
        };
        _islandController.OpenClaude = _claude.OpenClaude;
        _bar.TimeChanged += _claude.RescheduleUsage;
        _settings.Changed += _ => ShowClaude();
        _demo.Claude = _claude;
        ShowClaude();
        _islandWindow.Show();
        StartHotkeys();
        StartUpdates();
        // Notes that floated when Gili closed come back where they were (F-TOOL-12).
        _floating = new FloatingNotes(_notes, _islandWindow);
        _islandWindow.Floating = _floating;
        _bar.FullscreenChanged += _floating.SetFullscreen;
        _floating.WatchRequested += _gili.Watch;
        _floating.WatchEnded += _gili.Unwatch;
        _floating.CameHome += _gili.BeHappy;
        _demo.NoteCameHome += () =>
        {
            _islandWindow.Gulp();
            _gili.BeHappy();
        };
        _floating.Restore();
        // A timer or pomodoro phase that ended while Gili was closed shows its card now (plan 2b decision 4).
        _timers.Reschedule();
        // Likewise an alarm missed while Gili was closed or the laptop slept rings now, marked missed (F-TOOL-05).
        _alarms.Reschedule();
        _todos.Reschedule();
        _stats.Start();
        _apps.Start();
        _right.Start();
        _ = _media.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Before the database closes: what was typed in the last half second is written too (F-TOOL-09).
        FlushNotes();
        // So a reinstall right after quitting finds everything that was typed (design 2026-10-31 data tetap).
        _settings?.Flush();
        KeepCopy();
        _demo?.Stop();
        _sound?.Dispose();
        _backupTimer.Stop();
        _releaseTimer.Stop();
        _database?.Dispose();
        _stats?.Dispose();
        _apps?.Dispose();
        _right?.Dispose();
        _gili?.Dispose();
        // Stopping clears the sessions, and the island's windows are already closed: nothing may show them again.
        _hotkeys?.Dispose();
        _updates?.Dispose();
        _claude?.Changed -= ShowClaude;
        _claude?.Dispose();
        _islandController?.Dispose();
        _alarms?.Dispose();
        _todos?.Stop();
        _media?.Dispose();
        _bar?.ReleaseAppBar();
        RestoreTaskbar();
        _taskbar?.Dispose();
        _settings?.Flush();
        _transparency?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        FlushNotes();
        _bar?.ReleaseAppBar();
        RestoreTaskbar();
        _settings?.Flush();
        base.OnSessionEnding(e);
    }

    /// <summary>
    /// Hide Windows taskbar (F-BAR-11). Applying it at start also restores what a killed run left hidden while the switch
    /// is now off; an Explorer restart hides the new taskbar again.
    /// </summary>
    private void StartTaskbarKeeper()
    {
        _taskbar = new TaskbarHider();
        _taskbarKeeper = new TaskbarKeeper(_taskbar, new TaskbarStateFile(Data.Root));
        _bar!.Taskbar = _taskbar;
        var hide = _settings!.Current.HideTaskbar;
        ApplyTaskbar(hide);
        _settings.Changed += s =>
        {
            if (s.HideTaskbar == hide) return;
            hide = s.HideTaskbar;
            ApplyTaskbar(hide);
        };
        _bar.ShellRestarted += _taskbarKeeper.ShellRestarted;
    }

    /// <summary>When the original state cannot be written, the taskbar stays as it is.</summary>
    private void ApplyTaskbar(bool hide)
    {
        try
        {
            if (_taskbarKeeper?.Apply(hide) == true) _log?.Write(hide ? "Windows taskbar hidden" : "Windows taskbar shown as it was");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Write("Could not save the taskbar's state, so it stays as it is", ex);
        }
    }

    private void RestoreTaskbar()
    {
        try
        {
            _taskbarKeeper?.Restore();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Write("Could not forget the taskbar's state", ex);
        }
    }

    private Platform.Input.HotkeyService? _hotkeys;
    private string? _hotkeysApplied;

    /// <summary>The global hotkeys (F-TOOL-10): registered while Keyboard shortcuts is on, again whenever one changes.</summary>
    private void StartHotkeys()
    {
        _hotkeys = new Platform.Input.HotkeyService();
        _hotkeys.Pressed += action => _islandWindow!.RunHotkey(action);
        ApplyHotkeys(_settings!.Current);
        _settings.Changed += ApplyHotkeys;
    }

    private void ApplyHotkeys(Core.Settings.AppSettings s)
    {
        var wanted = s.KeyboardShortcuts
            ? Core.Settings.Hotkeys.All.Select(h => (h.Action, Key: Core.Settings.Hotkeys.Parse(s.HotkeyFor(h.Action))))
                .Where(h => h.Key is not null).ToDictionary(h => h.Action, h => h.Key!.Value)
            : [];
        var applied = string.Join(",", wanted.Select(w => $"{w.Key}={w.Value}"));
        if (applied == _hotkeysApplied) return;
        _hotkeysApplied = applied;
        _hotkeys!.Apply(wanted);
        foreach (var failed in _hotkeys.Failed) _log?.Write($"The {failed} hotkey is used by another app");
    }

    /// <summary>
    /// Claude's sessions in the slot, the green bubble, the page and on the left icon (F-CLD-02, F-CLD-04, F-LEFT-05), and
    /// the Needs input card for a new wait (F-CLD-03).
    /// </summary>
    private void ShowClaude()
    {
        var sessions = _claude!.Board.Sessions;
        _islandController!.SetClaude(Core.Claude.ClaudeSlot.For(sessions, DateTimeOffset.Now), [.. sessions]);
        var cards = _claude.Cards;
        switch (cards.Update(sessions, _islandController.IsFullscreen))
        {
            case Core.Claude.ClaudeCardChange.Show:
                _islandController.ShowClaudeCard(cards.Current!);
                break;
            case Core.Claude.ClaudeCardChange.Refresh:
                _islandController.RefreshClaudeCard(cards.Current!);
                break;
            case Core.Claude.ClaudeCardChange.Dismiss:
                _islandController.DismissClaudeCard();
                break;
        }
        // The 5-hour window shows while the integration and its own switch are on, and always in the demo (F-CLD-08).
        var showWindow = _claude.InDemo || (_claude.IsOn && _settings!.Current.ClaudeUsageTimer);
        _islandController.SetClaudeWindow(showWindow ? _claude.Usage.Current : null);
        _bar!.ShowClaude(sessions.Any(s => s.Status == Core.Claude.ClaudeStatus.NeedsInput),
            sessions.Any(s => s.Status == Core.Claude.ClaudeStatus.Working), Core.Claude.ClaudeSlot.Unseen(sessions).Count > 0,
            Core.Claude.ClaudeSlot.LauncherTooltip(sessions));
    }

    /// <summary>A note that cannot be written is logged, so quitting still releases the AppBar and never turns into a crash and restart.</summary>
    private void FlushNotes()
    {
        try
        {
            _notes?.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            _log?.Write("Could not write the notes before closing", ex);
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }
        _releaseTimer.Stop();
        var model = new SettingsViewModel(_settings!, _transparency!.Enabled, Kind, _startup, () => _claude?.Status(DateTimeOffset.Now),
            _hotkeys is null ? null : new HotkeyHooks(_hotkeys.Suspend, action => _hotkeys.Failed.Contains(action)),
            Platform.Modules.GeocodingClient.SearchAsync,
            new AboutActions(Core.About.AboutInfo.VersionLine(typeof(App).Assembly.GetName().Version!, Kind), OpenLogs, PrepareStoreUninstall,
                Kind == InstallKind.Development ? null : Task.Run(() => Core.About.AboutInfo.AnotherCopyNote(Kind,
                    Kind == InstallKind.Installed && OtherCopies.StoreInstalled(), Kind == InstallKind.Store && OtherCopies.GithubInstalled())),
                () => _updates!.State, InstallUpdate, ReleaseNotesPage, SystemActions.OpenSettings),
            _apps is null ? null : new FavoriteHooks(app => _apps.Item(app).Icon, Platform.Apps.AppLauncher.KeyForFile,
                key => Platform.Apps.AppInfo.Name(key, 0)));
        _updates?.Changed += model.RefreshUpdates;
        if (_hotkeys is not null) _hotkeys.FailuresChanged += model.RefreshShortcutNotes;
        _claude?.StatusChanged += model.RefreshNotes;
        _settingsWindow = new SettingsWindow(model);
        _settingsWindow.Closed += (_, _) =>
        {
            _claude?.StatusChanged -= model.RefreshNotes;
            _updates?.Changed -= model.RefreshUpdates;
            _hotkeys?.FailuresChanged -= model.RefreshShortcutNotes;
            // A recording cut short by closing the window must not leave the hotkeys off.
            _hotkeys?.Suspend(false);
            _settingsWindow = null;
            ReleaseLater();
        };
        _settingsWindow.Show();
        // Show alone can leave it behind the active app, since the bar never takes focus (F-BAR-13).
        _settingsWindow.Activate();
    }

    /// <summary>
    /// One Tools window (PRD 7.5), made when opened and let go when closed (PRD 8.1); opening it again brings it to the
    /// front on the asked tab.
    /// </summary>
    private void ShowTools(string tab)
    {
        if (_toolsWindow is not null)
        {
            if (_toolsWindow.WindowState == WindowState.Minimized) _toolsWindow.WindowState = WindowState.Normal;
            _toolsWindow.ShowTab(tab);
            _toolsWindow.Activate();
            return;
        }
        _releaseTimer.Stop();
        var alarms = new AlarmsTabViewModel(_alarms!, () => _settings!.Current.Use24HourClock, voice => _sound?.Preview(voice));
        var model = new ToolsViewModel(new TimersTabViewModel(_timers!), new StopwatchTabViewModel(_timers!),
            new PomodoroTabViewModel(_timers!, _settings!), alarms, new TodoTabViewModel(_todos!, () => _settings!.Current.Use24HourClock), tab);
        _toolsWindow = new ToolsWindow(model);
        _toolsWindow.Closed += (_, _) =>
        {
            _toolsWindow = null;
            ReleaseLater();
        };
        _toolsWindow.Show();
        // The bar never takes focus, so Show alone can leave it behind the active app.
        _toolsWindow.Activate();
    }

    /// <summary>
    /// A closed Tools or Settings window leaves nothing referenced, but the GC keeps its memory in gen2 until something
    /// forces a collection. 30 s after the last close, with neither window open again, one full compacting collection
    /// gives it back (PRD 8.1). Each close restarts the wait; opening either window cancels it.
    /// </summary>
    private void ReleaseLater()
    {
        _releaseTimer.Stop();
        _releaseTimer.Start();
    }

    private void OnReleaseTimer(object? sender, EventArgs e)
    {
        _releaseTimer.Stop();
        if (_toolsWindow is not null || _settingsWindow is not null) return;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    /// <summary>
    /// Only the installed copy owns the Run entry, so a dev build started with `dotnet run` never registers itself
    /// (F-BAR-04; the first run of the installed copy creates the entry, PRD 9.4). The Store copy uses its StartupTask.
    /// </summary>
    private void ShowSystemPage(bool shown)
    {
        if (shown == _systemPageShown) return;
        _systemPageShown = shown;
        UpdateStatsDemand();
        if (shown && _lastStats is { } last) _islandWindow?.SystemStats.Apply(last);
    }

    /// <summary>Only the modules switched on, and everything while the System page shows, are read (PRD 8.1).</summary>
    private void UpdateStatsDemand()
    {
        if (_stats is not null) _stats.Demand = StatsDemands.For(_settings!.Current.HiddenModules, _systemPageShown);
    }

    private void SyncAutostart(AppSettings settings)
    {
        if (Kind == InstallKind.Store)
        {
            _ = SetStartupTaskAsync(settings.Autostart);
            return;
        }
        if (Kind != InstallKind.Installed || _autostartApplied == settings.Autostart) return;
        try
        {
            Autostart.Apply(settings.Autostart, Environment.ProcessPath!);
            _autostartApplied = settings.Autostart;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            _log?.Write("Could not update the autostart entry", ex);
        }
    }

    /// <summary>
    /// gili.db (PRD 10): a corrupt file is replaced by the latest backup or a new database and kept aside (PRD 11; the
    /// toast comes with the cards in plan 2b). Backed up at start and checked hourly, one copy a day.
    /// </summary>
    private void OpenDatabase()
    {
        try
        {
            _database = Database.Open(Data.Root, AppPaths.KeepFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            _log?.Write("Could not open gili.db", ex);
            return;
        }
        if (_database.CorruptCopy is not null)
            _log?.Write($"gili.db was corrupt ({_database.Outcome}); the broken file is kept at {_database.CorruptCopy}");
        if (_database.RestoredFrom is not null) _log?.Write($"Restored gili.db from {_database.RestoredFrom}");
        if (_database.MigrationCopy is not null) _log?.Write($"Copied gili.db to {_database.MigrationCopy} before migrating it");
        BackUp();
        _backupTimer.Tick += (_, _) => BackUp();
        _backupTimer.Start();
    }

    /// <summary>The notes table, without the empty notes a crash left behind while one was open (plan 2f decision 4).</summary>
    /// <summary>What the Shelf needs to know about a path: gone (null), a folder, or a file and its size.</summary>
    private static FileFacts? ProbeFile(string path)
    {
        try
        {
            if (File.Exists(path)) return new FileFacts(IsFolder: false, new FileInfo(path).Length);
            if (Directory.Exists(path)) return new FileFacts(IsFolder: true, 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable counts as gone.
        }
        return null;
    }

    private NoteStore? OpenNotes()
    {
        if (_database is null) return null;
        var store = new NoteStore(_database);
        var dropped = store.DeleteBlank();
        if (dropped > 0) _log?.Write($"Dropped {dropped} empty note(s) left from the last run");
        return store;
    }

    private void BackUp()
    {
        try
        {
            Backups.BackUpIfDue(_database!, Path.Combine(Data.Root, Backups.FolderName), DateOnly.FromDateTime(DateTime.Now));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            _log?.Write("Could not back up gili.db", ex);
        }
        KeepCopy();
    }

    /// <summary>
    /// Refreshes today's keep copy of gili.db and settings.json in %USERPROFILE%\.gili, at start, hourly and on quit, so a
    /// reinstall loses at most an hour (design 2026-10-31 data tetap). The folder is hidden to keep the profile tidy.
    /// </summary>
    private void KeepCopy()
    {
        try
        {
            if (_database is not null) Backups.Refresh(_database, AppPaths.KeepFolder, DateOnly.FromDateTime(DateTime.Now));
            if (_settingsStore is not null) SettingsStore.CopyTo(_settingsStore.FilePath, AppPaths.KeepFolder);
            var root = new DirectoryInfo(Path.GetDirectoryName(AppPaths.KeepFolder)!);
            if (root.Exists && !root.Attributes.HasFlag(FileAttributes.Hidden)) root.Attributes |= FileAttributes.Hidden;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            _log?.Write("Could not write the keep copy", ex);
        }
    }

    /// <summary>A reinstall starts without settings.json; the keep copy brings the user's settings back.</summary>
    private bool RestoreSettings(SettingsStore store)
    {
        try
        {
            if (!store.RestoreFrom(AppPaths.KeepFolder)) return false;
            _log?.Write("Restored settings.json from the keep copy");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Write("Could not restore settings.json", ex);
            return false;
        }
    }

    /// <summary>The Store copy follows Task Manager's Startup apps at start, since the user may have changed it there.</summary>
    private async Task AdoptStartupTaskAsync()
    {
        try
        {
            _startup = await PackagedStartup.ReadAsync();
            var on = StartupRules.AutostartFor(_startup.Value);
            _settings!.Update(s => s with { Autostart = on });
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
        {
            _log?.Write("Could not read the startup task", ex);
        }
    }

    private async Task SetStartupTaskAsync(bool on)
    {
        try
        {
            _startup = await PackagedStartup.SetAsync(on);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
        {
            _log?.Write("Could not change the startup task", ex);
        }
    }

    /// <summary>
    /// The GitHub uninstaller's first step (PRD 9.4, design 5a): the running copy quits the normal way, then Claude's
    /// hooks, a taskbar a killed run left hidden, and the start entry are put back. Always exits with 0; failures are logged.
    /// </summary>
    private static void PrepareUninstall()
    {
        var log = new FileLog(Data.Logs);
        log.Write("Preparing to uninstall");
        if (SingleInstance.RequestQuit() && !WaitForOthers()) log.Write("The running Gili did not quit within 5 seconds");
        Core.About.UninstallSteps.Run(
        [
            new("Claude hooks", () => Platform.Claude.ClaudeSettingsFile.RemoveHooks()),
            new("Windows taskbar", () =>
            {
                using var taskbar = new TaskbarHider();
                new TaskbarKeeper(taskbar, new TaskbarStateFile(Data.Root)).Restore();
            }),
            new("Start with Windows", Autostart.Remove),
        ], (step, ex) => log.Write($"Prepare to uninstall: {step} failed", ex));
    }

    /// <summary>
    /// Up to 5 seconds for the running copy to let go of the mutex and then to end: the process lives on for a moment
    /// after the mutex, holding Gili.exe that an installer or uninstaller replaces next.
    /// </summary>
    private static bool WaitForOthers()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        return SingleInstance.WaitForExit(TimeSpan.FromSeconds(5)) && OthersExited(deadline);
    }

    private static bool OthersExited(DateTime deadline)
    {
        foreach (var process in System.Diagnostics.Process.GetProcessesByName("Gili"))
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId) continue;
                var left = deadline - DateTime.UtcNow;
                if (left <= TimeSpan.Zero || !process.WaitForExit(left)) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Settings → About's Prepare to uninstall on the Store copy, which cannot run code when Windows removes it (PRD 9.4):
    /// the hooks go, quitting gives the taskbar back, and Installed apps opens for the next step.
    /// </summary>
    private void PrepareStoreUninstall()
    {
        Core.About.UninstallSteps.Run([new("Claude hooks", () => Platform.Claude.ClaudeSettingsFile.RemoveHooks())],
            (step, ex) => _log?.Write($"Prepare to uninstall: {step} failed", ex));
        _log?.Write("Prepared to uninstall");
        SystemActions.OpenSettings("ms-settings:appsfeatures");
        Quit();
    }

    /// <summary>The update check (F-PUB-04): the blue dot follows it; the Store copy comes back after Windows updates it (PRD 11).</summary>
    private void StartUpdates()
    {
        _updates = new UpdateService(_settings!, _log!, Kind);
        _updates.Changed += () => _bar?.ShowGiliUpdate(_updates.State.Status is Core.About.UpdateStatus.Available or Core.About.UpdateStatus.Installing);
        _ = _updates.StartAsync();
        if (Kind == InstallKind.Store) RestartRegistration.ForUpdates();
    }

    private void InstallUpdate()
    {
        var owner = _settingsWindow is null ? 0 : new System.Windows.Interop.WindowInteropHelper(_settingsWindow).Handle;
        _ = _updates?.InstallAsync(owner, SystemActions.OpenSettings);
    }

    /// <summary>The release page the check found; the Store copy's notes are on its Store page.</summary>
    private string? ReleaseNotesPage() =>
        Kind == InstallKind.Store && PackageInfo.FamilyName() is { } family ? "ms-windows-store://pdp/?PFN=" + family : _updates?.State.PageUrl;

    private void OpenLogs()
    {
        Directory.CreateDirectory(Data.Logs);
        SystemActions.OpenFolder(Data.Logs);
    }

    private void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        Shutdown();
    }

    /// <summary>The UI is English only, so numbers and dates never follow the Windows locale.</summary>
    private static void UseEnglishFormatting()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(english.IetfLanguageTag)));
    }
}
