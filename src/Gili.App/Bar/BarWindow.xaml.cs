using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Gili.Core.Apps;
using Gili.Core.Island;
using Gili.Core.Settings;
using Gili.Platform.Interop;
using Gili.Platform.Shell;

namespace Gili.App.Bar;

public partial class BarWindow : Window
{
    private static readonly DropShadowEffect TextShadow = CreateTextShadow();

    private readonly SettingsHub _settings;
    private readonly TransparencyWatcher _transparency;
    private readonly StatsService _stats;
    private readonly ClockService _clock;
    private readonly BarViewModel _vm = new();
    private readonly LeftSide _left;
    private double _compactWidth = IslandLayout.MinCompactWidth;
    private bool _giliCircle;
    private AppBarService? _appBar;
    private IntPtr _hwnd;
    private bool _quitting;
    private bool _hidden;
    private bool _suspended;
    private bool _floating;
    private bool _glassOff;

    public BarWindow(SettingsHub settings, TransparencyWatcher transparency, StatsService stats, ClockService clock, AppsService apps)
    {
        _settings = settings;
        _transparency = transparency;
        _stats = stats;
        _clock = clock;
        InitializeComponent();
        DataContext = _vm;
        _left = new LeftSide(apps);
        _left.PageRequested += page => PageRequested?.Invoke(page);
        LeftSlot.Content = _left;
        SizeChanged += (_, _) => UpdateLeftLimit();
        _settings.Changed += OnSettingsChanged;
        _transparency.Changed += _ => Dispatcher.InvokeAsync(ApplyAppearance);
        _stats.Updated += _vm.ApplyStats;
        _clock.Ticked += () => _vm.ApplyClock(_clock.Now, _settings.Current);
        _clock.SecondTicked += () => _vm.ApplySecond(_clock.Now, _settings.Current);
        _clock.Seconds = _settings.Current.ShowSeconds;
        _vm.ApplyClock(_clock.Now, _settings.Current);
#if DEBUG
        var crash = new MenuItem { Header = "Simulate crash", Style = (Style)FindResource("BarMenuItem") };
        crash.Click += (_, _) => throw new InvalidOperationException("Simulated crash from the bar menu");
        BarMenu.Items.Add(crash);
#endif
    }

    public event Action? QuitRequested;

    public event Action? SettingsRequested;

    public event Action? ClockClicked;

    /// <summary>A launcher, Shelf or Claude icon asks for its island page.</summary>
    public event Action<IslandPage>? PageRequested;

    /// <summary>
    /// The compact island's width (without hover) and whether Gili's circle sits beside it: the left side stays 24 px
    /// away from both (PRD 6.1 "Aturan jarak").
    /// </summary>
    public void SetIslandCompact(double width, bool giliCircle)
    {
        _compactWidth = width;
        _giliCircle = giliCircle;
        UpdateLeftLimit();
    }

    private void UpdateLeftLimit()
    {
        if (ActualWidth <= 0) return;
        var s = _settings.Current;
        _left.SetLimit(BarSpacing.LeftLimit(ActualWidth, _compactWidth, s.IslandHeight, _giliCircle, privacyDots: 0));
    }

    /// <summary>The system clock or time zone changed, or the laptop woke up.</summary>
    public event Action? TimeChanged;

    public event Action<bool>? FullscreenChanged;

    public void ReleaseAppBar() => _appBar?.Unregister();

    public void Reassert() => _appBar?.Reposition();

    /// <summary>
    /// While the island floats only the bar's backdrop goes away (PRD 6.3): the tint fades in 220 ms, then the glass is
    /// switched off because the accent policy cannot fade. Content stays with a text shadow, except what the panel covers.
    /// </summary>
    public void SetIslandFloating(bool floating, double islandWidth)
    {
        UpdateCovered(floating ? islandWidth : 0);
        if (floating == _floating) return;
        _floating = floating;
        BarContent.Effect = floating ? TextShadow : null;
        var fade = new DoubleAnimation(floating ? 0 : 1, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 220 : 100));
        if (floating)
        {
            fade.Completed += (_, _) =>
            {
                if (!_floating) return;
                _glassOff = true;
                ApplyAppearance();
            };
        }
        else
        {
            _glassOff = false;
            ApplyAppearance();
        }
        Backdrop.BeginAnimation(OpacityProperty, fade);
    }

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

    private static DropShadowEffect CreateTextShadow()
    {
        var shadow = new DropShadowEffect { Color = Colors.Black, Opacity = 0.55, BlurRadius = 3, ShadowDepth = 1, Direction = 270 };
        shadow.Freeze();
        return shadow;
    }

    /// <summary>Each element under the floating panel (+8 px each side) fades in 160 ms and stops taking clicks.</summary>
    private void UpdateCovered(double islandWidth)
    {
        var left = ActualWidth / 2 - islandWidth / 2;
        var right = ActualWidth / 2 + islandWidth / 2;
        foreach (var element in RightModules.Children.OfType<FrameworkElement>().Concat(_left.Coverable))
        {
            if (!element.IsVisible) continue;
            var x = element.TranslatePoint(new Point(0, 0), this).X;
            var covered = islandWidth > 0 && IslandLayout.Covers(left, right, x, x + element.ActualWidth);
            if (element.IsHitTestVisible == !covered) continue;
            element.IsHitTestVisible = !covered;
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(covered ? 0 : 1, TimeSpan.FromMilliseconds(160)));
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _clock.Seconds = settings.ShowSeconds && !_hidden;
        _vm.ApplyClock(DateTime.Now, settings);
        _appBar?.SetHeight(settings.BarHeight);
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        var look = BarAppearance.Resolve(_settings.Current, _transparency.Enabled);
        BarMaterial.Apply(_hwnd, _glassOff ? GlassLevel.Off : look.Glass);
        _vm.ApplySettings(_settings.Current, look);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _hidden = fullscreen;
        UpdatePolling();
        _clock.Seconds = _settings.Current.ShowSeconds && !fullscreen;
        FullscreenChanged?.Invoke(fullscreen);
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
                TimeChanged?.Invoke();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMSUSPEND:
                _suspended = true;
                UpdatePolling();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _suspended = false;
                UpdatePolling();
                TimeChanged?.Invoke();
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

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnSettingsButton(object sender, MouseButtonEventArgs e) => SettingsRequested?.Invoke();

    private void OnClock(object sender, MouseButtonEventArgs e) => ClockClicked?.Invoke();

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
