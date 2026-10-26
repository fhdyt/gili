using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TopBar.App.Controls;
using TopBar.Core.Island;
using TopBar.Core.Motion;
using TopBar.Platform.Interop;
using TopBar.Platform.Shell;

namespace TopBar.App.Island;

/// <summary>
/// The island's own transparent topmost window (PRD 9.1), owned by the bar so it always stays above it. The window is
/// an envelope around the island: bar-high while compact, big enough for the floating panel while floating. Width,
/// height, top and corner radius follow springs stepped once per rendered frame, and only while something moves.
/// </summary>
public partial class IslandWindow : Window
{
    private const double CompactEnvelopeWidth = 760;
    private const double FloatingEnvelopeWidth = 1040;
    private const double FloatingEnvelopeHeight = 600;

    private readonly IslandController _island;
    private readonly IslandViewModel _vm;
    private readonly SettingsHub _settings;
    private readonly Spring _width = new(IslandLayout.MinCompactWidth);
    private readonly Spring _height = new(24);
    private readonly Spring _top = new(4);
    private readonly Spring _radius = new(12);
    private readonly Spring _float = new(0);
    private readonly Spring _bubble = new(0);
    private readonly Spring[] _springs;
    private readonly ScaleTransform _bubbleScale = new(0, 0);
    private readonly RectangleGeometry _clip = new();
    private readonly DispatcherTimer _awayTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private IslandMode _mode = IslandMode.Compact;
    private TimeSpan _lastFrame;
    private bool _animating;
    private bool _hovered;
    private bool _floatingEnvelope;
    private IntPtr _hwnd;
    private IntPtr _previousForeground;
    private static readonly Brush TextBrush = Frozen(0xF5, 0xF5, 0xF7);
    private static readonly Brush SecondaryBrush = Frozen(0x8E, 0x8E, 0x93);
    private static readonly Brush AccentBrush = Frozen(0x30, 0xD1, 0x58);
    private readonly List<(Button Button, IconView Icon, TextBlock Label, LevelMeter? Meter)> _tabs = [];
    private int _wheel;
    private DateTime _wheelPausedUntil;

    public IslandWindow(IslandController island, IslandViewModel vm, SettingsHub settings)
    {
        _island = island;
        _vm = vm;
        _settings = settings;
        _springs = [_width, _height, _top, _radius, _float, _bubble];
        InitializeComponent();
        DataContext = vm;
        Body.Clip = _clip;
        Bubble.RenderTransform = _bubbleScale;
        ExpandedLayer.Width = Math.Min(680, SystemParameters.PrimaryScreenWidth - IslandLayout.ScreenMargin);
        BuildTabs();
        BuildPages();
        ShowPage(animate: false);
        vm.PropertyChanged += OnViewModelChanged;
        TabStrip.SizeChanged += (_, _) => UpdateTabs(animate: false);
        island.ViewChanged += OnViewChanged;
        settings.Changed += _ =>
        {
            if (!_floatingEnvelope) SetEnvelope(false);
            Relayout();
        };
        CompactContent.SizeChanged += (_, _) => Relayout();
        WideContent.SizeChanged += (_, _) => Relayout();
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.PrimaryScreenWidth)) SetEnvelope(_floatingEnvelope);
        };
        _awayTimer.Tick += (_, _) =>
        {
            _awayTimer.Stop();
            if (_mode == IslandMode.Expanded && !Body.IsMouseOver) _island.Close();
        };
        Deactivated += (_, _) =>
        {
            if (_mode == IslandMode.Expanded) _island.Close();
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Whether the island floats, and the panel's width; the bar fades its backdrop and covered elements (PRD 6.3).</summary>
    public event Action<bool, double>? FloatingChanged;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeNonActivatingToolWindow(_hwnd);
        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);
        SetEnvelope(false);
        OnViewChanged(_island.View);
        Relayout(snap: true);
    }

    private static bool Floats(IslandMode mode) => mode is IslandMode.Expanded or IslandMode.Card or IslandMode.DropZone;

    private void OnViewChanged(IslandView view)
    {
        if (_hwnd == IntPtr.Zero) return;
        var previous = _mode;
        _mode = view.Mode;
        UpdateFocus(view.Mode, previous);
        if (view.Mode == IslandMode.Hidden)
        {
            Hide();
            return;
        }
        if (!IsVisible) Show();
        if (Floats(view.Mode) && !_floatingEnvelope) SetEnvelope(true);
        _hovered = view.Mode == IslandMode.Compact && Body.IsMouseOver;
        ShowLayers(view.Mode);
        Relayout(Floats(view.Mode) ? SpringParameters.Open : Floats(previous) ? SpringParameters.Close : SpringParameters.Resize);
        var s = _settings.Current;
        var width = Floats(view.Mode)
            ? IslandLayout.For(view.Mode, view.Page, 0, false, s.BarHeight, s.IslandHeight, SystemParameters.PrimaryScreenWidth).Width
            : 0;
        FloatingChanged?.Invoke(Floats(view.Mode), width);
    }

    /// <summary>Sets the springs' targets for the current mode. Without explicit parameters, a floating island keeps its own.</summary>
    private void Relayout(SpringParameters? parameters = null, bool snap = false)
    {
        if (_hwnd == IntPtr.Zero) return;
        var s = _settings.Current;
        var content = _mode == IslandMode.Wide ? WideContent.ActualWidth : CompactContent.ActualWidth;
        var target = IslandLayout.For(_mode, _island.View.Page, content, _hovered, s.BarHeight, s.IslandHeight, SystemParameters.PrimaryScreenWidth);
        var spring = parameters ?? (Floats(_mode) ? null : SpringParameters.Resize);
        var instant = snap || !SystemParameters.ClientAreaAnimation;
        Retarget(_width, target.Width, spring, instant);
        Retarget(_height, target.Height, spring, instant);
        Retarget(_top, target.Top, spring, instant);
        Retarget(_radius, target.CornerRadius, spring, instant);
        Retarget(_float, Floats(_mode) ? 1 : 0, spring, instant);
        Retarget(_bubble, _island.View.Bubble is null ? 0 : 1, SpringParameters.Pop, instant);
        if (!instant)
        {
            StartAnimating();
            return;
        }
        Apply();
        if (!Floats(_mode) && _floatingEnvelope) SetEnvelope(false);
    }

    private static void Retarget(Spring spring, double target, SpringParameters? parameters, bool instant)
    {
        if (parameters is { } p) spring.Parameters = p;
        if (instant) spring.SnapTo(target);
        else spring.Target = target;
    }

    private void StartAnimating()
    {
        if (_animating) return;
        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastFrame) return; // Rendering can be raised more than once per frame.
        var seconds = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        foreach (var spring in _springs) spring.Step(seconds);
        Apply();
        if (!_springs.All(s => s.IsAtRest)) return;
        CompositionTarget.Rendering -= OnFrame;
        _animating = false;
        if (!Floats(_mode) && _floatingEnvelope) SetEnvelope(false);
    }

    /// <summary>The window spans the island's reach: bar-high while compact, panel-sized while floating; always centred at the top.</summary>
    private void SetEnvelope(bool floating)
    {
        _floatingEnvelope = floating;
        var screen = SystemParameters.PrimaryScreenWidth;
        Width = Math.Min(screen, floating ? FloatingEnvelopeWidth : CompactEnvelopeWidth);
        Height = floating ? FloatingEnvelopeHeight : _settings.Current.BarHeight;
        Left = (screen - Width) / 2;
        Top = 0;
        Apply();
    }

    private void Apply()
    {
        var w = _width.Value;
        var h = _height.Value;
        var r = Math.Max(0, _radius.Value);
        var left = (Width - w) / 2;
        foreach (var plate in new[] { Body, ShadowPlate })
        {
            Canvas.SetLeft(plate, left);
            Canvas.SetTop(plate, _top.Value);
            plate.Width = w;
            plate.Height = h;
            plate.CornerRadius = new CornerRadius(r);
        }
        _clip.Rect = new Rect(0, 0, w, h);
        _clip.RadiusX = r;
        _clip.RadiusY = r;
        var floating = Math.Clamp(_float.Value, 0, 1);
        ShadowPlate.Opacity = floating;
        TopHighlight.CornerRadius = new CornerRadius(r);
        TopHighlight.Opacity = floating;

        var s = _settings.Current;
        Bubble.Width = s.IslandHeight;
        Bubble.Height = s.IslandHeight;
        Bubble.CornerRadius = new CornerRadius(s.IslandHeight / 2.0);
        Canvas.SetLeft(Bubble, Width / 2 + w / 2 + IslandLayout.BubbleGap);
        Canvas.SetTop(Bubble, (s.BarHeight - s.IslandHeight) / 2.0);
        var scale = Math.Max(0, _bubble.Value);
        _bubbleScale.ScaleX = scale;
        _bubbleScale.ScaleY = scale;
        Bubble.IsHitTestVisible = _bubble.Target > 0;
    }

    private void ShowLayers(IslandMode mode)
    {
        Fade(CompactLayer, mode == IslandMode.Compact);
        Fade(WideLayer, mode == IslandMode.Wide);
        Reveal(ExpandedLayer, mode == IslandMode.Expanded);
    }

    /// <summary>Slot content cross-fades: out in 90 ms, in over 180 ms once the old content is gone (PRD 8.3).</summary>
    private static void Fade(UIElement layer, bool show)
    {
        if (layer.IsHitTestVisible == show) return;
        layer.IsHitTestVisible = show;
        var animated = SystemParameters.ClientAreaAnimation;
        var fade = new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(!animated ? 100 : show ? 180 : 90))
        {
            BeginTime = show && animated ? TimeSpan.FromMilliseconds(90) : TimeSpan.Zero,
        };
        layer.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>The open island's content arrives 120 ms after the panel starts growing: opacity, blur 6 → 0 and scale 0.98 → 1 over 220 ms.</summary>
    private static void Reveal(FrameworkElement layer, bool show)
    {
        if (layer.IsHitTestVisible == show) return;
        layer.IsHitTestVisible = show;
        var animated = SystemParameters.ClientAreaAnimation;
        if (!show || !animated)
        {
            layer.Effect = null;
            layer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(animated ? 90 : 100)));
            return;
        }
        var delay = TimeSpan.FromMilliseconds(120);
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var blur = new BlurEffect { Radius = 6 };
        var scale = new ScaleTransform(0.98, 0.98);
        layer.Effect = blur;
        layer.RenderTransformOrigin = new Point(0.5, 0);
        layer.RenderTransform = scale;
        layer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { BeginTime = delay, EasingFunction = ease });
        var sharpen = new DoubleAnimation(6, 0, duration) { BeginTime = delay, EasingFunction = ease };
        // An effect costs work on every frame; drop it once the content is sharp.
        sharpen.Completed += (_, _) =>
        {
            if (ReferenceEquals(layer.Effect, blur)) layer.Effect = null;
        };
        blur.BeginAnimation(BlurEffect.RadiusProperty, sharpen);
        var grow = new DoubleAnimation(0.98, 1, duration) { BeginTime = delay, EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>The open island takes focus so Esc and the arrow keys reach it; the previous window gets it back on close.</summary>
    private void UpdateFocus(IslandMode mode, IslandMode previous)
    {
        if (mode == IslandMode.Expanded && previous != IslandMode.Expanded)
        {
            _previousForeground = ForegroundWindow.Get();
            WindowStyles.SetNoActivate(_hwnd, false);
            Activate();
        }
        else if (mode != IslandMode.Expanded && previous == IslandMode.Expanded)
        {
            _awayTimer.Stop();
            WindowStyles.SetNoActivate(_hwnd, true);
            if (IsActive) ForegroundWindow.Restore(_previousForeground);
            _previousForeground = IntPtr.Zero;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_mode != IslandMode.Expanded) return;
        switch (e.Key)
        {
            case Key.Escape:
                _island.Close();
                break;
            case Key.Left:
                StepPage(-1);
                break;
            case Key.Right:
                StepPage(1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private void OnBodyClick(object sender, MouseButtonEventArgs e)
    {
        if (_mode is IslandMode.Compact or IslandMode.Wide) _island.OpenFromIsland();
    }

    private void OnBodyEnter(object sender, MouseEventArgs e)
    {
        _awayTimer.Stop();
        if (_mode != IslandMode.Compact || _hovered) return;
        _hovered = true;
        Relayout(SpringParameters.Hover);
    }

    private void OnBodyLeave(object sender, MouseEventArgs e)
    {
        if (_mode == IslandMode.Expanded) _awayTimer.Start();
        if (!_hovered) return;
        _hovered = false;
        Relayout(SpringParameters.Hover);
    }

    private void OnBubbleClick(object sender, MouseButtonEventArgs e)
    {
        _island.OpenBubble();
        e.Handled = true;
    }

    private void OnPlayPause(object sender, RoutedEventArgs e) => _ = _island.Media.TogglePlayPauseAsync();

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void BuildTabs()
    {
        foreach (var tab in _vm.Tabs)
        {
            var icon = new IconView { Glyph = tab.Glyph, Width = 15, Height = 15 };
            var meter = tab.Page == IslandPage.Media ? new LevelMeter { BarBrush = AccentBrush, Visibility = Visibility.Collapsed } : null;
            var glyph = new Grid { MinWidth = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
            glyph.Children.Add(icon);
            if (meter is not null) glyph.Children.Add(meter);
            var label = new TextBlock { Text = tab.Name, FontSize = 12, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(glyph);
            content.Children.Add(label);
            var button = new Button { Style = (Style)FindResource("TabButton"), Content = content };
            AutomationProperties.SetName(button, tab.Name);
            button.Click += (_, _) => _island.Open(tab.Page);
            TabStrip.Children.Add(button);
            _tabs.Add((button, icon, label, meter));
        }
    }

    private void BuildPages()
    {
        foreach (var tab in _vm.Tabs)
        {
            PageTrack.Children.Add(new Border { Width = ExpandedLayer.Width, Height = 164, Child = CreatePage(tab) });
        }
    }

    /// <summary>Real pages replace their placeholders here as they are built (Media in Task 11, the rest in Phases 2–3).</summary>
    private FrameworkElement CreatePage(IslandTab tab) => Placeholder(tab);

    private static FrameworkElement Placeholder(IslandTab tab)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = tab.Name, FontSize = 19, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = tab.Placeholder ?? "",
            FontSize = 13,
            Foreground = SecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        });
        return panel;
    }

    /// <summary>
    /// One highlight slides to the open page's tab (320 ms); that tab's text turns white and semibold and its icon
    /// green. While music plays, the Media icon becomes the level meter. On a narrow screen only the open tab keeps its name.
    /// </summary>
    private void UpdateTabs(bool animate)
    {
        if (_tabs.Count == 0 || TabStrip.ActualWidth <= 0) return;
        var selected = _vm.SelectedTab;
        var narrow = ExpandedLayer.Width < 680;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var (button, icon, label, meter) = _tabs[i];
            var active = i == selected;
            if (active) button.Foreground = TextBrush;
            else button.ClearValue(ForegroundProperty);
            if (active) icon.Foreground = AccentBrush;
            else icon.ClearValue(IconView.ForegroundProperty);
            label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            label.Visibility = active || !narrow ? Visibility.Visible : Visibility.Collapsed;
            var playing = meter is not null && _vm.MusicPlaying;
            icon.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
            if (meter is null) continue;
            meter.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
            meter.IsPlaying = playing;
        }
        var cell = TabStrip.ActualWidth / _tabs.Count;
        TabHighlight.Width = cell - 2;
        Slide(TabHighlightShift, selected * cell + 1, 320, animate, new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    private void ShowPage(bool animate) =>
        Slide(PageShift, -_vm.SelectedTab * ExpandedLayer.Width, 300, animate,
            new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 7 });

    private static void Slide(TranslateTransform shift, double x, int milliseconds, bool animate, IEasingFunction ease)
    {
        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.X = x;
            return;
        }
        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IslandViewModel.SelectedTab):
                // Opening the island lands on its page; only switching while open slides.
                var animate = _mode == IslandMode.Expanded;
                UpdateTabs(animate);
                ShowPage(animate);
                break;
            case nameof(IslandViewModel.MusicPlaying):
                UpdateTabs(animate: false);
                break;
        }
    }

    private void StepPage(int direction)
    {
        var next = Math.Clamp(_vm.SelectedTab + direction, 0, _vm.Tabs.Count - 1);
        if (next != _vm.SelectedTab) _island.Open(_vm.Tabs[next].Page);
    }

    private void OnPagesWheel(object sender, MouseWheelEventArgs e)
    {
        Scroll(-e.Delta);
        e.Handled = true;
    }

    /// <summary>Wheel and touchpad deltas add up to one page per notch (120), then pause while the page slides.</summary>
    private void Scroll(int delta)
    {
        if (DateTime.UtcNow < _wheelPausedUntil) return;
        _wheel += delta;
        if (Math.Abs(_wheel) < 120) return;
        StepPage(Math.Sign(_wheel));
        _wheel = 0;
        _wheelPausedUntil = DateTime.UtcNow.AddMilliseconds(300);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Horizontal touchpad swipes and wheel tilts arrive only as WM_MOUSEHWHEEL; WPF has no event for them.
        if (msg == WindowMessages.WM_MOUSEHWHEEL && _mode == IslandMode.Expanded)
        {
            Scroll((short)((wParam.ToInt64() >> 16) & 0xFFFF));
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnFooterClick(object sender, RoutedEventArgs e) => _island.Open(IslandPage.Calendar);
}
