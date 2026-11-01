using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Gili.App.Controls;
using ModuleSet = Gili.Core.Settings.RightModules;
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
    private int _privacyDots;
    private AppBarService? _appBar;
    private TrayIcon? _tray;
    private IntPtr _hwnd;
    private bool _quitting;
    private readonly PollingGate _polling = new();
    private DisplayMonitor? _display;
    private bool _floating;
    private bool _glassOff;
    private bool _desktop;
    private double _backdrop = 1;

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
        _left.ApplyHidden(_settings.Current.ShownHiddenLeft, animate: false);
        // Favorites (F-LEFT-08): the right-click menus change the list; the bar shows whatever the settings hold.
        _left.FavoritesChanged += change => _settings.Update(s => s.WithFavorites(change(s.FavoriteList)));
        Loaded += (_, _) => ShowFavorites(_settings.Current);
        Loaded += (_, _) => ApplyModules(_settings.Current);
        SizeChanged += (_, _) => UpdateLeftLimit();
        right.Changed += () =>
        {
            _vm.ApplyRight(_right, Glyph);
            Dispatcher.InvokeAsync(FitRight, System.Windows.Threading.DispatcherPriority.Loaded);
        };
        _vm.ApplyRight(_right, Glyph);
        _settings.Changed += OnSettingsChanged;
        // Same on the desktop (F-BAR-16): the foreground the strip already follows says when the desktop is in front.
        apps.ForegroundChanged += hwnd =>
        {
            var desktop = AppBarService.IsShellDesktop(hwnd);
            if (desktop == _desktop) return;
            _desktop = desktop;
            if (Dispatcher.CheckAccess()) ApplyBackdrop();
            else Dispatcher.InvokeAsync(ApplyBackdrop);
        };
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

    /// <summary>The Wi‑Fi, Bluetooth, Volume, Brightness and Power icons toggle their control islands (PRD 6.4).</summary>
    public event Action<ControlKind>? ControlRequested;

    /// <summary>A launcher, Shelf or Claude icon asks for its island page.</summary>
    public event Action<IslandPage>? PageRequested;

    /// <summary>
    /// The compact island's width (without hover) and whether Gili's circle sits beside it: the left side stays 24 px
    /// away from both (PRD 6.1 "Aturan jarak").
    /// </summary>
    public void SetIslandCompact(double width, bool giliCircle, int privacyDots)
    {
        _compactWidth = width;
        _giliCircle = giliCircle;
        _privacyDots = privacyDots;
        UpdateLeftLimit();
    }

    private void UpdateLeftLimit()
    {
        if (ActualWidth <= 0) return;
        var s = _settings.Current;
        _left.SetLimit(BarSpacing.LeftLimit(ActualWidth, _compactWidth, s.IslandHeight, _giliCircle, _privacyDots));
        FitRight();
    }

    /// <summary>
    /// The right side as Settings → Right side sets it (F-RIGHT-04, F-BAR-12): the system modules in their order, the
    /// switched-off ones gone, the ones without a reading hidden; and what still doesn't fit 24 px from the island moves
    /// into "…" in PRD 6.4's order (mockup <c>fitBar()</c>). A switch flipped in Settings animates its module; overflow
    /// and readings coming or going don't.
    /// </summary>
    private void FitRight() => FitRight(ModuleSet.None);

    private void FitRight(ModuleSet switched)
    {
        if (ActualWidth <= 0) return;
        var modules = new List<(string Id, double Width)>();
        foreach (var element in RightModules.Children.OfType<FrameworkElement>())
        {
            if (element == MoreModule || element is Popup) continue;
            if (element.Tag is string id)
            {
                if (!Wanted(id)) continue;
                var width = _slots.SettledWidth(element);
                modules.Add((id, _moduleWidths.Width(id, width, _hiddenModules.Contains(id))));
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
            var show = Wanted(id) && !hidden.Contains(id);
            _slots.Show(element, show, animate: switched.HasFlag(Core.Settings.RightModuleRules.Of(id)));
        }
        MoreModule.Visibility = hidden.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _hiddenModules = hidden;
    }

    /// <summary>Switched on in Settings, and with something to show.</summary>
    private bool Wanted(string id) =>
        !_settings.Current.HiddenModules.HasFlag(Core.Settings.RightModuleRules.Of(id)) && id switch
        {
            "weather" => _vm.HasWeather,
            "wifi" => _vm.HasWifi,
            "bt" => _vm.HasBluetooth,
            "vol" => _vm.HasVolume,
            "bri" => _vm.HasBrightness,
            _ => true,
        };

    /// <summary>
    /// Puts the system modules in the order set in Settings, before "…"; the ones that moved glide from where they were.
    /// </summary>
    private void ArrangeModules(IReadOnlyList<string> order)
    {
        var system = order.Select(id => RightModules.Children.OfType<FrameworkElement>().First(e => e.Tag as string == id)).ToList();
        var current = RightModules.Children.OfType<FrameworkElement>().Where(e => system.Contains(e)).ToList();
        if (current.SequenceEqual(system)) return;
        var before = system.ToDictionary(e => e, e => e.IsVisible ? e.TranslatePoint(new Point(0, 0), RightModules).X : double.NaN);
        foreach (var element in system) RightModules.Children.Remove(element);
        var at = RightModules.Children.IndexOf(MoreModule);
        for (var i = 0; i < system.Count; i++) RightModules.Children.Insert(at + i, system[i]);
        RightModules.UpdateLayout();
        foreach (var element in system)
        {
            if (double.IsNaN(before[element]) || !element.IsVisible) continue;
            SlotAnimation.Slide(element, element.TranslatePoint(new Point(0, 0), RightModules).X - before[element]);
        }
    }

    private void ApplyModules(AppSettings settings)
    {
        var switched = _shownModules is { } last ? last ^ settings.HiddenModules : ModuleSet.None;
        _shownModules = settings.HiddenModules;
        ArrangeModules(settings.Order);
        FitRight(switched);
    }

    private ModuleSet? _shownModules;
    private readonly SlotAnimation _slots = new();
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
                "gpu" => $"GPU · {_vm.GpuText}",
                "disk" => $"Disk · {_vm.DiskFreeText}",
                _ => id,
            };
            MoreModulesItems.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 4, 0, 4) });
        }
        MoreModulesList.IsOpen = true;
    }

    /// <summary>
    /// The island that a file dragged over the bar turns into the drop zone (PRD 6.3 "Shelf"), and whose update check
    /// puts the orange dot on Power (F-RIGHT-08).
    /// </summary>
    public Island.IslandController? Island
    {
        get => _island;
        set
        {
            _island = value;
            if (value is null) return;
            value.UpdatesChanged += ShowUpdates;
            ShowUpdates();
        }
    }

    private Island.IslandController? _island;

    private void ShowUpdates()
    {
        _vm.UpdatesPending = _island?.UpdatesPending == true;
        _vm.PowerTooltip = _vm.UpdatesPending ? "Power · updates are ready to install" : "Power";
    }

    public void ShowShelf(int files, bool bounce) => _left.ShowShelf(files, bounce);

    public void ShowClaude(bool waiting, bool working, bool unseen, string tooltip) => _left.ShowClaude(waiting, working, unseen, tooltip);

    /// <summary>Files dragged over the bar open the drop zone; dropping them on the bar keeps them too (PRD 6.2).</summary>
    private void OnFileDrag(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (Island is not { DraggingOut: false } island || !Gili.App.Island.ShelfDragData.Has(e.Data))
        {
            e.Effects = DragDropEffects.None;
            return;
        }
        island.DragEntered(overIsland: false);
        // Copy, never Move: the source must not delete a file Gili only points at (plan 3d).
        e.Effects = island.Shelf.IsFull ? DragDropEffects.None : DragDropEffects.Copy;
    }

    private void OnFileDragLeave(object sender, DragEventArgs e) => Island?.DragLeft();

    private void OnFileDrop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        // Dropped on the bar rather than one of the island's columns: a Copy (F-ISL-30).
        e.Effects = Island is { } island && Gili.App.Island.ShelfDragData.Drop(island, e.Data, Core.Shelf.ShelfMode.Copy)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        if (e.Effects == DragDropEffects.None) Island?.DragLeft();
    }

    private Geometry Glyph(string key) => (Geometry)FindResource(key);

    private void OnWifi(object sender, MouseButtonEventArgs e) => ControlRequested?.Invoke(ControlKind.Wifi);

    private void OnBluetooth(object sender, MouseButtonEventArgs e) => ControlRequested?.Invoke(ControlKind.Bluetooth);

    private void OnVolume(object sender, MouseButtonEventArgs e) => ControlRequested?.Invoke(ControlKind.Volume);

    private void OnBrightness(object sender, MouseButtonEventArgs e) => ControlRequested?.Invoke(ControlKind.Brightness);

    /// <summary>A taskbar Gili hid is shown first, since Windows opens the hidden icons from it (design 3f).</summary>
    private void OnHiddenIcons(object sender, MouseButtonEventArgs e)
    {
        Taskbar?.Reveal();
        SystemActions.HiddenIcons();
    }

    /// <summary>The Windows taskbar, for the Tray button while Hide Windows taskbar is on.</summary>
    public TaskbarHider? Taskbar { get; set; }

    /// <summary>Explorer restarted (TaskbarCreated).</summary>
    public event Action? ShellRestarted;

    private void OnPower(object sender, MouseButtonEventArgs e) => ControlRequested?.Invoke(ControlKind.Power);

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

    /// <summary>Every way out releases the AppBar space and takes Gili's tray icon away with it.</summary>
    public void ReleaseAppBar()
    {
        _appBar?.Unregister();
        _tray?.Dispose();
    }

    public void Reassert() => _appBar?.Reposition();

    /// <summary>
    /// While the island floats only the bar's backdrop changes (PRD 6.3); content stays, except what the panel covers.
    /// </summary>
    public void SetIslandFloating(bool floating, double islandWidth)
    {
        UpdateCovered(floating ? islandWidth : 0);
        if (floating == _floating) return;
        _floating = floating;
        ApplyBackdrop();
    }

    /// <summary>
    /// The backdrop fades in 220 ms to Opacity while open while the island floats, or the desktop is in front with Same on
    /// the desktop (F-BAR-15, F-BAR-16), and back to full after. At 0 the glass is switched off once the tint has faded,
    /// because the accent policy cannot fade. A text shadow keeps the content readable while less than half shows.
    /// </summary>
    private void ApplyBackdrop()
    {
        var target = BarAppearance.Backdrop(_settings.Current, _floating, _desktop);
        BarContent.Effect = target < 0.5 ? TextShadow : null;
        if (Math.Abs(target - _backdrop) < 0.001) return;
        _backdrop = target;
        var fade = new DoubleAnimation(target, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 220 : 100));
        if (target == 0)
        {
            fade.Completed += (_, _) =>
            {
                if (_backdrop != 0) return;
                _glassOff = true;
                ApplyAppearance();
            };
        }
        else if (_glassOff)
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
        _appBar.ShellRestarted += () =>
        {
            // Explorer forgot every tray icon when it restarted.
            if (_tray?.Shown == true) _tray.Show();
            ShellRestarted?.Invoke();
        };
        _tray = new TrayIcon(_hwnd);
        _tray.Clicked += () => SettingsRequested?.Invoke();
        _tray.MenuRequested += () => ShowMenu(fromTray: true);
        if (_settings.Current.TrayIcon) _tray.Show();
        ApplyEdge(_settings.Current);
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

    private string? _favorites;

    private void ShowFavorites(AppSettings settings)
    {
        if (settings.FavoriteApps == _favorites) return;
        _favorites = settings.FavoriteApps;
        _left.ShowFavorites(settings.FavoriteList);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _left.ApplyHidden(settings.ShownHiddenLeft, animate: true);
        ShowFavorites(settings);
        ApplyModules(settings);
        _clock.Seconds = settings.ShowSeconds && _polling.ShouldPoll;
        _vm.ApplyClock(DateTime.Now, settings);
        ApplyEdge(settings);
        _appBar?.SetHeight(settings.BarHeight);
        if (settings.TrayIcon != _tray?.Shown)
        {
            if (settings.TrayIcon) _tray?.Show();
            else _tray?.Remove();
        }
        ApplyAppearance();
        ApplyBackdrop();
    }

    /// <summary>
    /// At the bottom the hairline moves to the bar's top edge and the Tray chevron points up (F-BAR-10, mockup
    /// <c>body.bottom</c>); the bar moves at once and the island follows with its springs.
    /// </summary>
    private void ApplyEdge(AppSettings settings)
    {
        var bottom = settings.BarPosition == BarPosition.Bottom;
        Backdrop.BorderThickness = bottom ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 0, 1);
        TrayChevron.RenderTransform = bottom ? new RotateTransform(180) : Transform.Identity;
        _appBar?.SetEdge(bottom);
    }

    private void ApplyAppearance()
    {
        var look = BarAppearance.Resolve(_settings.Current, _transparency.Enabled);
        BarMaterial.Apply(_hwnd, _glassOff ? GlassLevel.Off : look.Glass, look.Color);
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
        if (_appBar?.HandleMessage(msg, wParam, lParam) == true || _tray?.HandleMessage(msg, lParam) == true)
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

    /// <summary>
    /// The bar's menu at the pointer: from Gili's tray icon, and from a right-click on the compact island, which is its
    /// own window (F-BAR-17). From the tray Gili comes to the front first, or the menu would not close on a click outside.
    /// </summary>
    public void ShowMenu(bool fromTray = false)
    {
        if (fromTray) ForegroundWindow.Restore(_hwnd);
        BarMenu.Placement = PlacementMode.MousePoint;
        BarMenu.PlacementTarget = this;
        BarMenu.IsOpen = true;
    }

    /// <summary>The menu's Demo mode item shows a check while the demo plays.</summary>
    public void SetDemoRunning(bool running) => DemoItem.IsChecked = running;

    public void ShowGiliUpdate(bool waiting)
    {
        _vm.GiliUpdate = waiting;
        _vm.SettingsTooltip = waiting ? "Settings · a new version of Gili is ready" : "Settings";
    }

    private void OnSettingsButton(object sender, MouseButtonEventArgs e) => SettingsRequested?.Invoke();

    private void OnClock(object sender, MouseButtonEventArgs e) => ClockClicked?.Invoke();

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
