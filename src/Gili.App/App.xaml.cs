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
using Gili.Core;
using Gili.Core.Diagnostics;
using Gili.Core.Settings;
using Gili.Data;
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
    private TransparencyWatcher? _transparency;
    private StatsService? _stats;
    private ClockService? _clock;
    private MediaService? _media;
    private IslandController? _islandController;
    private IslandWindow? _islandWindow;
    private GiliService? _gili;
    private BarWindow? _bar;
    private AppsService? _apps;
    private RightService? _right;
    private DemoService? _demo;
    private Database? _database;
    private TimerService? _timers;
    private readonly System.Windows.Threading.DispatcherTimer _backupTimer = new() { Interval = TimeSpan.FromHours(1) };
    private SettingsWindow? _settingsWindow;
    private bool? _autostartApplied;
    private StartupState? _startup;
    private bool _quitting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--quit"))
        {
            SingleInstance.RequestQuit();
            Shutdown();
            return;
        }
        UseEnglishFormatting();
        Directory.CreateDirectory(Data.Root);
        _log = new FileLog(Data.Logs);
        _log.DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
        new CrashHandler(_log, Data.CrashMarker, () => _bar?.ReleaseAppBar()).Attach(this);

        var restarted = e.Args.Contains("--restarted");
        _instance = SingleInstance.Acquire(restarted ? TimeSpan.FromSeconds(5) : TimeSpan.Zero,
            () => Dispatcher.InvokeAsync(ShowSettings),
            () => Dispatcher.InvokeAsync(Quit));
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        var store = new SettingsStore(Data.Root);
        var loaded = store.Load();
        if (loaded.WasCorrupt) _log.Write("settings.json could not be read; renamed to settings.json.bak and using defaults");
        _settings = new SettingsHub(store, loaded.Settings, _log);
        OpenDatabase();
        _settings.Changed += SyncAutostart;
        if (Kind == InstallKind.Store) _ = AdoptStartupTaskAsync();
        else SyncAutostart(_settings.Current);
        _transparency = new TransparencyWatcher();

        var cpu = new CpuSampler();
        var memory = new MemorySampler();
        var network = new NetworkSampler();
        _stats = new StatsService(Dispatcher, cpu.Sample, memory.Sample, network.Sample);

        _clock = new ClockService();
        _media = new MediaService(Dispatcher, _log);
        _timers = new TimerService(_database is null ? null : new TimerStore(_database));
        _islandController = new IslandController(Dispatcher, _media, _timers);

        _apps = new AppsService(() => _bar is null ? 1 : System.Windows.Media.VisualTreeHelper.GetDpi(_bar).DpiScaleX);
        _right = new RightService(Dispatcher, _settings, _log);
        _bar = new BarWindow(_settings, _transparency, _stats, _clock, _apps, _right);
        _bar.PageRequested += _islandController.Toggle;
        _bar.SettingsRequested += ShowSettings;
        _bar.ClockClicked += () => _islandController.Toggle(Core.Island.IslandPage.Calendar);
        _bar.PowerRequested += () => _islandController.Toggle(Core.Island.IslandPage.Controls);
        _demo = new DemoService(Dispatcher, _islandController, _media);
        _bar.DemoRequested += _demo.Toggle;
        _demo.Changed += () => _bar.SetDemoRunning(_demo.IsRunning);
        _bar.QuitRequested += Quit;
        _bar.TimeChanged += _clock.Refresh;
        _bar.TimeChanged += _timers.Reschedule;
        _bar.FullscreenChanged += _islandController.SetFullscreen;
        _bar.PollingChanged += poll =>
        {
            _right.SetPaused(!poll);
            _media.SetPaused(!poll);
            _gili?.SetPaused(!poll);
            _log?.Write(poll ? "Polling resumed" : $"Polling paused: {_bar.PollingReason}");
        };
        _bar.Show();

        // Owned by the bar, so the island stays above it whenever the bar re-asserts its topmost place.
        _gili = new GiliService();
        _islandWindow = new IslandWindow(_islandController, new IslandViewModel(_islandController, _settings), _settings, _gili)
        {
            Owner = _bar,
        };
        _islandWindow.FloatingChanged += _bar.SetIslandFloating;
        _islandWindow.CompactChanged += _bar.SetIslandCompact;
        _islandWindow.Show();
        // A timer or pomodoro phase that ended while Gili was closed shows its card now (plan 2b decision 4).
        _timers.Reschedule();
        _stats.Start();
        _apps.Start();
        _right.Start();
        _ = _media.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _demo?.Stop();
        _backupTimer.Stop();
        _database?.Dispose();
        _stats?.Dispose();
        _apps?.Dispose();
        _right?.Dispose();
        _gili?.Dispose();
        _islandController?.Dispose();
        _media?.Dispose();
        _bar?.ReleaseAppBar();
        _settings?.Flush();
        _transparency?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _bar?.ReleaseAppBar();
        _settings?.Flush();
        base.OnSessionEnding(e);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized) _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(new SettingsViewModel(_settings!, _transparency!.Enabled, Kind, _startup));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        // Show alone can leave it behind the active app, since the bar never takes focus (F-BAR-13).
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Only the installed copy owns the Run entry, so a dev build started with `dotnet run` never registers itself
    /// (F-BAR-04; the first run of the installed copy creates the entry, PRD 9.4). The Store copy uses its StartupTask.
    /// </summary>
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
            _database = Database.Open(Data.Root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException or InvalidOperationException)
        {
            _log?.Write("Could not open gili.db", ex);
            return;
        }
        if (_database.Outcome != OpenOutcome.Normal)
            _log?.Write($"gili.db was corrupt ({_database.Outcome}); the broken file is kept at {_database.CorruptCopy}");
        BackUp();
        _backupTimer.Tick += (_, _) => BackUp();
        _backupTimer.Start();
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
