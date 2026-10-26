using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TopBar.Core.Settings;
using TopBar.Platform.Interop;
using TopBar.Platform.Shell;

namespace TopBar.App.Bar;

public partial class BarWindow : Window
{
    private readonly SettingsHub _settings;
    private readonly TransparencyWatcher _transparency;
    private readonly StatsService _stats;
    private readonly BarViewModel _vm = new();
    private AppBarService? _appBar;
    private IntPtr _hwnd;
    private bool _quitting;
    private bool _hidden;
    private bool _suspended;

    public BarWindow(SettingsHub settings, TransparencyWatcher transparency, StatsService stats)
    {
        _settings = settings;
        _transparency = transparency;
        _stats = stats;
        InitializeComponent();
        DataContext = _vm;
        _settings.Changed += OnSettingsChanged;
        _transparency.Changed += _ => Dispatcher.InvokeAsync(ApplyAppearance);
        _stats.Updated += _vm.ApplyStats;
#if DEBUG
        var crash = new MenuItem { Header = "Simulate crash", Style = (Style)FindResource("BarMenuItem") };
        crash.Click += (_, _) => throw new InvalidOperationException("Simulated crash from the bar menu");
        BarMenu.Items.Add(crash);
#endif
    }

    public event Action? QuitRequested;

    public void ReleaseAppBar() => _appBar?.Unregister();

    public void Reassert() => _appBar?.Reposition();

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeNonActivatingToolWindow(_hwnd);
        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);
        _appBar = new AppBarService(_hwnd);
        _appBar.FullscreenChanged += OnFullscreenChanged;
        _appBar.Register(_settings.Current.BarHeight);
        ApplyAppearance();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Whatever closes the bar from outside also quits the app, so no process is left running without a bar.
        if (!_quitting)
        {
            _quitting = true;
            QuitRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _appBar?.SetHeight(settings.BarHeight);
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        var look = BarAppearance.Resolve(_settings.Current, _transparency.Enabled);
        BarMaterial.Apply(_hwnd, look.Glass);
        _vm.ApplySettings(_settings.Current, look);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _hidden = fullscreen;
        UpdatePolling();
        if (fullscreen)
        {
            Hide();
            return;
        }
        Show();
        _appBar?.Reposition();
    }

    private void UpdatePolling() => _stats.Paused = _hidden || _suspended;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_appBar?.HandleMessage(msg, wParam, lParam) == true)
        {
            handled = true;
            return IntPtr.Zero;
        }
        switch (msg)
        {
            case WindowMessages.WM_TIMECHANGE:
                // Also sent when the time zone changes; DateTime.Now caches the zone.
                TimeZoneInfo.ClearCachedData();
                _vm.RefreshClock();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMSUSPEND:
                _suspended = true;
                UpdatePolling();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _suspended = false;
                UpdatePolling();
                _vm.RefreshClock();
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>Until the System page exists (Phase 3), CPU and RAM open Task Manager (PRD 6.3).</summary>
    private void OnOpenTaskManager(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // The user declined the UAC prompt.
        }
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
