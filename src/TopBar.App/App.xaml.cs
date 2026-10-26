using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using TopBar.App.Bar;
using TopBar.Core;
using TopBar.Core.Diagnostics;
using TopBar.Data;
using TopBar.Platform.Shell;
using TopBar.Platform.Stats;

namespace TopBar.App;

public partial class App : Application
{
    private FileLog? _log;
    private SingleInstance? _instance;
    private SettingsHub? _settings;
    private TransparencyWatcher? _transparency;
    private StatsService? _stats;
    private BarWindow? _bar;
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
            () => Dispatcher.InvokeAsync(() => _bar?.Reassert()),
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
        _transparency = new TransparencyWatcher();

        var cpu = new CpuSampler();
        var memory = new MemorySampler();
        var network = new NetworkSampler();
        _stats = new StatsService(Dispatcher, cpu.Sample, memory.Sample, network.Sample);

        _bar = new BarWindow(_settings, _transparency, _stats);
        _bar.QuitRequested += Quit;
        _bar.Show();
        _stats.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _stats?.Dispose();
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
