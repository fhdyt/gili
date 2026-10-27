using System.Globalization;
using System.IO;
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
    private static readonly bool IsInstalledBuild =
        Environment.ProcessPath is { } exe && AppPaths.IsUnder(exe, AppPaths.InstallDirectory);

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
    private SettingsWindow? _settingsWindow;
    private bool? _autostartApplied;
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
        Directory.CreateDirectory(AppPaths.Root);
        _log = new FileLog(AppPaths.Logs);
        _log.DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
        new CrashHandler(_log, AppPaths.CrashMarker, () => _bar?.ReleaseAppBar()).Attach(this);

        var restarted = e.Args.Contains("--restarted");
        _instance = SingleInstance.Acquire(restarted ? TimeSpan.FromSeconds(5) : TimeSpan.Zero,
            () => Dispatcher.InvokeAsync(ShowSettings),
            () => Dispatcher.InvokeAsync(Quit));
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        var store = new SettingsStore(AppPaths.Root);
        var loaded = store.Load();
        if (loaded.WasCorrupt) _log.Write("settings.json could not be read; renamed to settings.json.bak and using defaults");
        _settings = new SettingsHub(store, loaded.Settings, _log);
        _settings.Changed += SyncAutostart;
        SyncAutostart(_settings.Current);
        _transparency = new TransparencyWatcher();

        var cpu = new CpuSampler();
        var memory = new MemorySampler();
        var network = new NetworkSampler();
        _stats = new StatsService(Dispatcher, cpu.Sample, memory.Sample, network.Sample);

        _clock = new ClockService();
        _media = new MediaService(Dispatcher, _log);
        _islandController = new IslandController(Dispatcher, _media);

        _apps = new AppsService(() => _bar is null ? 1 : System.Windows.Media.VisualTreeHelper.GetDpi(_bar).DpiScaleX);
        _right = new RightService(Dispatcher, _settings, _log);
        _bar = new BarWindow(_settings, _transparency, _stats, _clock, _apps, _right);
        _bar.PageRequested += _islandController.Toggle;
        _bar.SettingsRequested += ShowSettings;
        _bar.ClockClicked += () => _islandController.Toggle(Core.Island.IslandPage.Calendar);
        _bar.PowerRequested += () => _islandController.Toggle(Core.Island.IslandPage.Controls);
        _bar.QuitRequested += Quit;
        _bar.TimeChanged += _clock.Refresh;
        _bar.FullscreenChanged += _islandController.SetFullscreen;
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
        _stats.Start();
        _apps.Start();
        _right.Start();
        _ = _media.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
        _settingsWindow = new SettingsWindow(new SettingsViewModel(_settings!, _transparency!.Enabled, IsInstalledBuild));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        // Show alone can leave it behind the active app, since the bar never takes focus (F-BAR-13).
        _settingsWindow.Activate();
    }

    /// <summary>
    /// Only the installed copy owns the Run entry, so a dev build started with `dotnet run` never registers itself
    /// (F-BAR-04; the first run of the installed copy creates the entry, PRD 9.4).
    /// </summary>
    private void SyncAutostart(AppSettings settings)
    {
        if (!IsInstalledBuild || _autostartApplied == settings.Autostart) return;
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
