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
using Gili.Core.Modules;
using System.Windows.Controls.Primitives;
using Gili.Core.Settings;
using Gili.Platform.Interop;
using Gili.Platform.Power;
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
    private readonly RightService _right;
    private double _compactWidth = IslandLayout.MinCompactWidth;
    private bool _giliCircle;
    private AppBarService? _appBar;
    private IntPtr _hwnd;
    private bool _quitting;
    private readonly PollingGate _polling = new();
    private DisplayMonitor? _display;
    private bool _floating;
    private bool _glassOff;

    public BarWindow(SettingsHub settings, TransparencyWatcher transparency, StatsService stats, ClockService clock, AppsService apps, RightService right)
    {
        _right = right;
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
        right.Changed += () =>
        {
            _vm.ApplyRight(_right, Glyph);
            Dispatcher.InvokeAsync(FitRight, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        _vm.ApplyRight(_right, Glyph);
        _settings.Changed += OnSettingsChanged;
        _transparency.Changed += _ => Dispatcher.InvokeAsync(ApplyAppearance);
        _stats.Updated += _vm.ApplyStats;
        _polling.Changed += OnPollingChanged;
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

    /// <summary>Demo mode in the bar menu starts or stops the demo (F-BAR-09).</summary>
    public event Action? DemoRequested;

    public event Action? SettingsRequested;

    /// <summary>Open Tools in the bar menu (PRD 6.1).</summary>
    public event Action? ToolsRequested;

    public event Action? ClockClicked;

    /// <summary>The Power icon toggles the Power island (PRD 6.4).</summary>
    public event Action? PowerRequested;

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
        FitRight();
    }

    /// <summary>
    /// Weather, network, RAM and CPU move into "…" (in that order) when the right side would come within 24 px of the
    /// island (PRD 6.4, mockup <c>fitBar()</c>).
    /// </summary>
    private void FitRight()
    {
        if (ActualWidth <= 0) return;
        var modules = new List<(string Id, double Width)>();
        foreach (var element in RightModules.Children.OfType<FrameworkElement>())
        {
            if (element == MoreModule || element is Popup) continue;
            if (element.Tag is string id)
            {
                if (id == "weather" && !_vm.HasWeather) continue;
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                modules.Add((id, _moduleWidths.Width(id, element.DesiredSize.Width, _hiddenModules.Contains(id))));
            }
            else if (element.Visibility == Visibility.Visible)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                modules.Add(("fixed", element.DesiredSize.Width));
            }
        }
        var hidden = RightOverflow.Hide(modules, BarSpacing.RightLimit(ActualWidth, _compactWidth, _settings.Current.IslandHeight, bubbles: 0));
        foreach (var element in RightModules.Children.OfType<FrameworkElement>())
        {
            if (element.Tag is not string id) continue;
            var show = !hidden.Contains(id) && (id != "weather" || _vm.HasWeather);
            element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }
        MoreModule.Visibility = hidden.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _hiddenModules = hidden;
    }

    private IReadOnlyList<string> _hiddenModules = [];
    private readonly ModuleWidths _moduleWidths = new();

    /// <summary>"…" lists what moved there, with its current reading.</summary>
    private void OnMoreModules(object sender, MouseButtonEventArgs e)
    {
        MoreModulesItems.Children.Clear();
        foreach (var id in _hiddenModules)
        {
            var text = id switch
            {
                "weather" => $"Weather · {_vm.WeatherLabel} {_vm.WeatherValue}",
                "net" => $"Network · {_vm.UpText}  {_vm.DownText}",
                "cpu" => $"CPU · {_vm.CpuText}",
                "ram" => $"RAM · {_vm.RamText}",
                _ => id,
            };
            MoreModulesItems.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 4) });
        }
        MoreModulesList.IsOpen = true;
    }

    private Geometry Glyph(string key) => (Geometry)FindResource(key);

    private void OnQuickSettings(object sender, MouseButtonEventArgs e) => SystemActions.QuickSettings();

    private void OnHiddenIcons(object sender, MouseButtonEventArgs e) => SystemActions.HiddenIcons();

    private void OnPower(object sender, MouseButtonEventArgs e) => PowerRequested?.Invoke();

    private void OnSystem(object sender, MouseButtonEventArgs e) => PageRequested?.Invoke(IslandPage.System);

    private void OnBrightnessTooltip(object sender, ToolTipEventArgs e)
    {
        if (_right.ReadBrightness() is { } level) _vm.BrightnessTooltip = $"Brightness {level}%";
    }

    /// <summary>The system clock or time zone changed, or the laptop woke up.</summary>
    public event Action? TimeChanged;

    public event Action<bool>? FullscreenChanged;

    /// <summary>False while the screen is off, a fullscreen app hides the bar, or the laptop sleeps (PRD 8.1).</summary>
    public event Action<bool>? PollingChanged;

    /// <summary>What holds polling right now, for the log.</summary>
    public string PollingReason => _polling.Reason;

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
        _display = new DisplayMonitor(_hwnd);
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
        _clock.Seconds = settings.ShowSeconds && _polling.ShouldPoll;
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
        _polling.SetFullscreen(fullscreen);
        FullscreenChanged?.Invoke(fullscreen);
        if (fullscreen)
        {
            Hide();
            return;
        }
        Show();
        _appBar?.Reposition();
    }

    /// <summary>Every timer on the bar and island stops while the screen is off, the bar is hidden, or the laptop sleeps.</summary>
    private void OnPollingChanged(bool poll)
    {
        _stats.Paused = !poll;
        _clock.Seconds = _settings.Current.ShowSeconds && poll;
        if (poll) TimeChanged?.Invoke();
        PollingChanged?.Invoke(poll);
    }

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
                _polling.SetSuspended(true);
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _polling.SetSuspended(false);
                TimeChanged?.Invoke();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == DisplayMonitor.PBT_POWERSETTINGCHANGE:
                if (DisplayMonitor.TryRead(lParam, out var screenOn)) _polling.SetScreenOn(screenOn);
                break;
        }
        return IntPtr.Zero;
    }

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnTools(object sender, RoutedEventArgs e) => ToolsRequested?.Invoke();

    private void OnDemo(object sender, RoutedEventArgs e) => DemoRequested?.Invoke();

    /// <summary>The menu's Demo mode item shows a check while the demo plays.</summary>
    public void SetDemoRunning(bool running) => DemoItem.IsChecked = running;

    private void OnSettingsButton(object sender, MouseButtonEventArgs e) => SettingsRequested?.Invoke();

    private void OnClock(object sender, MouseButtonEventArgs e) => ClockClicked?.Invoke();

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
