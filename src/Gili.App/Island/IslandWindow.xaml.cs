using System.Globalization;
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
using Gili.App.Controls;
using Gili.App.Mascot;
using Gili.Core.Island;
using Gili.Core.Layout;
using Gili.Core.Mascot;
using Gili.Core.Motion;
using Gili.Platform.Interop;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// The island's own transparent topmost window (PRD 9.1), owned by the bar so it always stays above it. The window is
/// an envelope around the island: bar-high while compact, big enough for the floating panel while floating. Width,
/// height, top and corner radius follow springs stepped once per rendered frame, and only while something moves.
/// </summary>
public partial class IslandWindow : Window
{
    private const double CompactEnvelopeWidth = 760;
    private const double FloatingEnvelopeWidth = 1040;
    private const double ShadowRoom = 64;

    private readonly IslandController _island;
    private readonly IslandViewModel _vm;
    private readonly SettingsHub _settings;
    private readonly Spring _width = new(IslandLayout.MinCompactWidth);
    private readonly Spring _height = new(24);
    private readonly Spring _top = new(4);
    private readonly Spring _radius = new(12);
    private readonly Spring _float = new(0);
    private readonly Spring _bubble = new(0);
    private readonly Spring _giliInside = new(1) { Parameters = GiliRules.ShapeSpring };
    private readonly Spring _giliCircle = new(0) { Parameters = GiliRules.ShapeSpring };
    private readonly Spring[] _springs;
    private readonly GiliService _gili;
    private readonly PowerPage _powerPage;
    private readonly TimerViewModel _timerPage;
    private readonly AlarmsViewModel _alarmsPage;
    private readonly CalendarViewModel _calendarPage;
    private readonly TodoViewModel _todoPage;
    private readonly NotesViewModel _notesPage;
    private NotesPage? _notesView;
    private string? _glowTimer;
    private readonly ScaleTransform _giliInsideScale = new(1, 1);
    private readonly TranslateTransform _giliInsideShift = new();
    private readonly ScaleTransform _giliCircleScale = new(0.6, 0.6);
    private bool? _giliInsideOn;
    private bool? _giliCircleOn;
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

    /// <summary>Glow colours per page (PRD 6.3 "Ambient glow"); Media takes the artwork's colours instead.</summary>
    private static readonly Dictionary<IslandPage, Color> PageGlow = new()
    {
        [IslandPage.Alarms] = Color.FromRgb(0xFF, 0xD6, 0x0A),
        [IslandPage.Calendar] = Color.FromRgb(0xFF, 0x45, 0x3A),
        [IslandPage.Todo] = Color.FromRgb(0x0A, 0x84, 0xFF),
        [IslandPage.Notes] = Color.FromRgb(0xFF, 0xD6, 0x0A),
        [IslandPage.Clipboard] = Color.FromRgb(0x66, 0xD4, 0xCF),
        [IslandPage.Shelf] = Color.FromRgb(0x64, 0xD2, 0xFF),
        [IslandPage.Claude] = Color.FromRgb(0xD9, 0x77, 0x57),
        [IslandPage.System] = Color.FromRgb(0xBF, 0x5A, 0xF2),
        [IslandPage.Controls] = Color.FromRgb(0x0A, 0x84, 0xFF),
    };
    private readonly List<(Button Button, IconView Icon, Border Name, LevelMeter? Meter)> _tabs = [];
    private TabName[] _names = [];
    private int _hoveredTab = -1;
    private Point _lastMenuPoint;
    private bool _tight;
    private double _pageHeight;
    private int _wheel;
    private DateTime _wheelPausedUntil;

    public IslandWindow(IslandController island, IslandViewModel vm, SettingsHub settings, GiliService gili)
    {
        _island = island;
        _vm = vm;
        _settings = settings;
        _gili = gili;
        _springs = [_width, _height, _top, _radius, _float, _bubble, _giliInside, _giliCircle];
        InitializeComponent();
        DataContext = vm;
        Body.Clip = _clip;
        Bubble.RenderTransform = _bubbleScale;
        GiliInside.RenderTransform = new TransformGroup { Children = { _giliInsideScale, _giliInsideShift } };
        GiliCircle.RenderTransform = _giliCircleScale;
        gili.Register(GiliInside);
        gili.Register(GiliCircleEyes);
        gili.Register(MenuGiliEyes);
        _timerPage = new TimerViewModel(island.Timers);
        _alarmsPage = new AlarmsViewModel(island.Alarms, () => settings.Current.Use24HourClock, island.ManageAlarms);
        _calendarPage = new CalendarViewModel(island.Todos, island.Alarms, () => settings.Current.Use24HourClock);
        _todoPage = new TodoViewModel(island.Todos, () => settings.Current.Use24HourClock);
        _notesPage = new NotesViewModel(island.Notes, () => settings.Current.Use24HourClock, island.Close);
        settings.Changed += _ =>
        {
            _alarmsPage.Refresh();
            _todoPage.Refresh();
            _notesPage.Refresh();
        };
        // The island grows and shrinks with what its page lists (PRD 6.3: alarms, the day's items, today's tasks).
        island.Alarms.Changed += () => PageContentChanged(IslandPage.Alarms);
        _calendarPage.ContentChanged += () => PageContentChanged(IslandPage.Calendar);
        _todoPage.ContentChanged += () => PageContentChanged(IslandPage.Todo);
        _notesPage.ContentChanged += () => PageContentChanged(IslandPage.Notes);
        // The Timer page's glow follows the segment and the pomodoro phase.
        island.Timers.Changed += () =>
        {
            var key = $"{island.Timers.Mode}{island.Timers.Pomodoro.Phase}";
            if (key == _glowTimer) return;
            _glowTimer = key;
            UpdateGlow();
        };
        BuildTabs();
        BuildPages();
        ControlLayer.Child = _powerPage = new PowerPage(island);
        SizeExpanded();
        vm.PropertyChanged += OnViewModelChanged;
        island.ViewChanged += OnViewChanged;
        settings.Changed += _ =>
        {
            if (!_floatingEnvelope) SetEnvelope(false);
            SizeExpanded();
            Relayout();
            UpdateGili();
            UpdateGlow();
        };
        CompactContent.SizeChanged += (_, _) => Relayout();
        WideContent.SizeChanged += (_, _) => Relayout();
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SystemParameters.PrimaryScreenWidth)) return;
            SizeExpanded();
            SetEnvelope(_floatingEnvelope);
        };
        _awayTimer.Tick += (_, _) =>
        {
            _awayTimer.Stop();
            // Not while typing in the island or dragging a note (mockup armLeave).
            if (_mode == IslandMode.Expanded && !Body.IsMouseOver && Keyboard.FocusedElement is not TextBox && !_notesPage.IsDragging) _island.Close();
        };
        Deactivated += (_, _) =>
        {
            if (_mode == IslandMode.Expanded) _island.Dismiss();
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Whether the island floats, and the panel's width; the bar fades its backdrop and covered elements (PRD 6.3).</summary>
    public event Action<bool, double>? FloatingChanged;

    /// <summary>The compact island's width without hover, and whether Gili's circle shows beside it.</summary>
    public event Action<double, bool>? CompactChanged;

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

    private IslandScreen Screen()
    {
        var s = _settings.Current;
        return new(s.BarHeight, s.IslandHeight, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
    }

    private (double Width, bool Circle) _compactSent;

    private void NotifyCompact(double content)
    {
        var width = Math.Max(IslandLayout.MinCompactWidth, content + IslandLayout.CompactPadding);
        var sent = (width, _giliCircleOn == true);
        if (sent == _compactSent) return;
        _compactSent = sent;
        CompactChanged?.Invoke(sent.width, sent.Item2);
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
        if (ShowsControl) _powerPage.Refresh();
        _hovered = view.Mode == IslandMode.Compact && Body.IsMouseOver;
        ShowLayers(view.Mode);
        Relayout(Floats(view.Mode) ? SpringParameters.Open : Floats(previous) ? SpringParameters.Close : SpringParameters.Resize);
        UpdateGili();
        UpdateGlow();
        FloatingChanged?.Invoke(Floats(view.Mode), Floats(view.Mode) ? Target(0, false).Width : 0);
    }

    /// <summary>The control island is open: the island shows one control instead of the menu and its pages.</summary>
    private bool ShowsControl => _mode == IslandMode.Expanded && _island.View.Page == IslandPage.Controls;

    private IslandGeometry Target(double content, bool hovered)
    {
        var page = _island.View.Page;
        var height = page == IslandPage.Controls ? ControlHeight() : _pageHeight;
        return IslandLayout.For(_mode, Screen(), content, hovered, ExpandedLayer.Width, height, page);
    }

    /// <summary>The control's full height at the control island's inner width.</summary>
    private double ControlHeight()
    {
        _powerPage.Measure(new Size(IslandLayout.ControlWidth - 2 * IslandLayout.ControlPadding, double.PositiveInfinity));
        return _powerPage.DesiredSize.Height;
    }

    /// <summary>Sets the springs' targets for the current mode. Without explicit parameters, a floating island keeps its own.</summary>
    private void Relayout(SpringParameters? parameters = null, bool snap = false)
    {
        if (_hwnd == IntPtr.Zero) return;
        var content = _mode == IslandMode.Wide ? WideContent.ActualWidth : CompactContent.ActualWidth;
        var target = Target(content, _hovered);
        if (ShowsControl)
        {
            ControlLayer.Width = target.Width;
            ControlLayer.Height = target.Height;
        }
        else if (_mode == IslandMode.Expanded)
        {
            SizePages(target.Height);
        }
        else if (_mode == IslandMode.Card)
        {
            CardLayer.Width = target.Width;
            CardLayer.Height = target.Height;
        }
        NotifyCompact(content);
        if (_floatingEnvelope) GrowEnvelope(target.Top + target.Height);
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
        Height = floating ? Math.Min(SystemParameters.PrimaryScreenHeight, IslandLayout.ExpandedMinHeight + ShadowRoom) : _settings.Current.BarHeight;
        Left = (screen - Width) / 2;
        Top = 0;
        Apply();
    }

    /// <summary>A tall page makes the floating window taller, never more than it needs, since a layered window costs per pixel.</summary>
    private void GrowEnvelope(double bottom)
    {
        var needed = Math.Min(SystemParameters.PrimaryScreenHeight, bottom + ShadowRoom);
        if (Height < needed) Height = needed;
    }

    private void Apply()
    {
        var w = _width.Value;
        var h = _height.Value;
        var r = Math.Max(0, _radius.Value);
        var left = (Width - w) / 2;
        Canvas.SetLeft(Body, left);
        Canvas.SetTop(Body, _top.Value);
        Body.Width = Math.Max(0, w);
        Body.Height = Math.Max(0, h);
        Body.CornerRadius = new CornerRadius(r);
        Canvas.SetLeft(Shadow, left);
        Canvas.SetTop(Shadow, _top.Value);
        Shadow.Width = Math.Max(0, w);
        Shadow.Height = Math.Max(0, h);
        Shadow.CornerRadius = r;
        _clip.Rect = new Rect(0, 0, w, h);
        _clip.RadiusX = r;
        _clip.RadiusY = r;
        var floating = Math.Clamp(_float.Value, 0, 1);
        Shadow.Opacity = floating;
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

        // Gili's eyes shrink and slide out of the island's left end while the circle drips from that end to 6 px
        // beside it; both follow the island's width every frame, hover included (PRD 6.3 "Berpindah bentuk").
        var inside = _giliInside.Value;
        _giliInsideScale.ScaleX = _giliInsideScale.ScaleY = 1 - 0.5 * (1 - inside);
        _giliInsideShift.X = -12 * (1 - inside);
        var circle = _giliCircle.Value;
        var islandLeft = (Width - w) / 2;
        GiliCircle.Width = GiliCircle.Height = s.IslandHeight;
        GiliCircle.CornerRadius = new CornerRadius(s.IslandHeight / 2.0);
        Canvas.SetLeft(GiliCircle, islandLeft - s.IslandHeight / 2.0 + circle * (-IslandLayout.BubbleGap - s.IslandHeight / 2.0));
        Canvas.SetTop(GiliCircle, (s.BarHeight - s.IslandHeight) / 2.0);
        _giliCircleScale.ScaleX = _giliCircleScale.ScaleY = 0.6 + 0.4 * circle;
    }

    /// <summary>
    /// The glow takes the open page's colour and wanders only while the island is open with Ambient motion and Windows
    /// animations on; on Media it holds still at half strength while the music is paused (PRD 6.3).
    /// </summary>
    private void UpdateGlow()
    {
        var s = _settings.Current;
        Glow.Visibility = s.AmbientGlow ? Visibility.Visible : Visibility.Collapsed;
        var page = _island.View.Page;
        var paused = page == IslandPage.Media && !_vm.IsPlaying;
        if (page == IslandPage.Media) Glow.SetColors(_vm.GlowPrimary, _vm.GlowSecondary, paused);
        else if (page == IslandPage.Timer) Glow.SetColors(TimerGlow(), TimerGlow(), dim: false);
        else Glow.SetColors(PageGlow[page], PageGlow[page], dim: false);
        if (_mode != IslandMode.Expanded || !s.AmbientGlow)
        {
            Glow.Hide();
            return;
        }
        Glow.Show(_width.Target, _height.Target, s.AmbientMotion && SystemParameters.ClientAreaAnimation && !paused);
    }

    /// <summary>
    /// Gili lives in the idle compact island, in the circle beside the island while something else shows (and in the
    /// wide island), and beside the tab menu while open; it is gone while a card or the drop zone floats or in fullscreen.
    /// </summary>
    private void UpdateGili()
    {
        var show = _settings.Current.ShowGili && _mode != IslandMode.Hidden;
        var insideOn = show && _mode == IslandMode.Compact && !_vm.HasActivity;
        var circleOn = show && ((_mode == IslandMode.Compact && _vm.HasActivity) || _mode == IslandMode.Wide);
        var menuOn = show && _mode == IslandMode.Expanded && !ShowsControl;
        var instant = !SystemParameters.ClientAreaAnimation;
        if (insideOn != _giliInsideOn)
        {
            _giliInsideOn = insideOn;
            Retarget(_giliInside, insideOn ? 1 : 0, null, instant);
            // Fading in waits 60 ms, so the circle has started merging back before the eyes reappear.
            GiliInside.BeginAnimation(OpacityProperty, new DoubleAnimation(insideOn ? 1 : 0, TimeSpan.FromMilliseconds(instant ? 100 : 200))
            {
                BeginTime = TimeSpan.FromMilliseconds(insideOn && !instant ? 60 : 0),
            });
        }
        if (circleOn != _giliCircleOn)
        {
            _giliCircleOn = circleOn;
            Retarget(_giliCircle, circleOn ? 1 : 0, null, instant);
            GiliCircle.IsHitTestVisible = circleOn;
            GiliCircle.BeginAnimation(OpacityProperty, new DoubleAnimation(circleOn ? 1 : 0, TimeSpan.FromMilliseconds(instant ? 100 : 160)));
        }
        if (instant) Apply();
        else StartAnimating();
        NotifyCompact(CompactContent.ActualWidth);
        _gili.SetActiveForm(menuOn ? GiliForm.Menu : circleOn ? GiliForm.Circle : GiliForm.InIsland);
        _gili.SetVisible(insideOn || circleOn || menuOn);
        _gili.SetMusicPlayingInSlot(_vm.ShowMediaSlot && _vm.MusicPlaying);
    }

    private void ShowLayers(IslandMode mode)
    {
        Reveal(CardLayer, mode == IslandMode.Card);
        CardLayer.SetPulsing(mode == IslandMode.Card, _vm.CardShake);
        _timerPage.SetPageOpen(mode == IslandMode.Expanded && _island.View.Page == IslandPage.Timer);
        Fade(CompactLayer, mode == IslandMode.Compact);
        Fade(WideLayer, mode == IslandMode.Wide);
        Reveal(ExpandedLayer, mode == IslandMode.Expanded && !ShowsControl);
        Reveal(ControlLayer, ShowsControl);
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

    /// <summary>
    /// The open island's content arrives 120 ms after the panel starts growing (mockup .L-exp): opacity over 220 ms,
    /// down 6 px and scale 0.98 → 1 over 380 ms on the out curve, and blur 6 → 0 over 260 ms.
    /// </summary>
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
        var blur = new BlurEffect { Radius = 6 };
        var scale = new ScaleTransform(0.98, 0.98);
        var drop = new TranslateTransform(0, -6);
        layer.Effect = blur;
        layer.RenderTransformOrigin = new Point(0.5, 0);
        layer.RenderTransform = new TransformGroup { Children = { scale, drop } };
        layer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            BeginTime = TimeSpan.FromMilliseconds(120),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        });
        var sharpen = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(260)) { BeginTime = TimeSpan.FromMilliseconds(120) };
        // An effect costs work on every frame; drop it once the content is sharp.
        sharpen.Completed += (_, _) =>
        {
            if (ReferenceEquals(layer.Effect, blur)) layer.Effect = null;
        };
        blur.BeginAnimation(BlurEffect.RadiusProperty, sharpen);
        var grow = BezierEase.To(1, 380, BezierEase.Out, 120);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        drop.BeginAnimation(TranslateTransform.YProperty, BezierEase.To(0, 380, BezierEase.Out, 120));
    }

    /// <summary>The open island takes focus so Esc and the arrow keys reach it; the previous window gets it back on close.</summary>
    private void UpdateFocus(IslandMode mode, IslandMode previous)
    {
        if (mode == IslandMode.Expanded && previous != IslandMode.Expanded)
        {
            // Card times move on while the island is closed ("19:20" becomes "Yesterday").
            if (_island.View.Page == IslandPage.Notes) _notesPage.Refresh();
            _previousForeground = ForegroundWindow.Get();
            WindowStyles.SetNoActivate(_hwnd, false);
            Activate();
        }
        else if (mode != IslandMode.Expanded && previous == IslandMode.Expanded)
        {
            // A dragged card is let go, what was typed is written, and the island opens on the grid next time (plan 2f decision 12).
            _notesView?.EndDrag();
            _notesPage.CloseEditorNow();
            _island.Notes.Flush();
            _awayTimer.Stop();
            WindowStyles.SetNoActivate(_hwnd, true);
            if (IsActive) ForegroundWindow.Restore(_previousForeground);
            _previousForeground = IntPtr.Zero;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_mode != IslandMode.Expanded) return;
        // The Notes page answers Esc in its editor and search, and the editor's own keys, first (plan 2f decision 11).
        if (_island.View.Page == IslandPage.Notes && _notesView?.HandleKey(e) == true)
        {
            e.Handled = true;
            return;
        }
        // A text field keeps its arrows, and its page decides what Esc does (cancel the form, not close the island).
        if (e.OriginalSource is TextBox) return;
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

    /// <summary>Each tab is an icon with its name beside it, clipped to nothing until the tab is active or hovered.</summary>
    private void BuildTabs()
    {
        _names = MeasureNames();
        for (var i = 0; i < _vm.Tabs.Count; i++)
        {
            var tab = _vm.Tabs[i];
            var index = i;
            var icon = new IconView { Glyph = tab.Glyph, Width = 15, Height = 15 };
            var meter = tab.Page == IslandPage.Media
                ? new LevelMeter { BarBrush = AccentBrush, Visibility = Visibility.Collapsed, Width = 15, Height = 13 }
                : null;
            var glyph = new Grid { Width = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
            glyph.Children.Add(icon);
            if (meter is not null) glyph.Children.Add(meter);
            var text = new TextBlock { Text = tab.Name, FontSize = 12, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var name = new Border { Width = 0, Opacity = 0, ClipToBounds = true, Child = text, VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            content.Children.Add(glyph);
            content.Children.Add(name);
            var button = new Button
            {
                Style = (Style)FindResource("TabButton"),
                Content = content,
                Width = TabMenuLayout.IconTab,
                Margin = new Thickness(i == 0 ? 0 : TabMenuLayout.Gap, 0, 0, 0),
            };
            AutomationProperties.SetName(button, tab.Name);
            button.Click += (_, _) => _island.Open(tab.Page);
            // Keyboard focus opens the name like hover does.
            button.GotKeyboardFocus += (_, _) => HoverTab(index);
            button.LostKeyboardFocus += (_, _) => HoverTab(-1);
            TabStrip.Children.Add(button);
            _tabs.Add((button, icon, name, meter));
        }
    }

    /// <summary>Every tab name measured once at 12 px, regular for hover and semibold for the active tab.</summary>
    private TabName[] MeasureNames()
    {
        var family = new FontFamily("Segoe UI Variable Text, Segoe UI");
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        double Measure(string name, FontWeight weight) => new FormattedText(name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), 12, Brushes.White, dpi).WidthIncludingTrailingWhitespace;
        return [.. _vm.Tabs.Select(t => new TabName(Math.Ceiling(Measure(t.Name, FontWeights.Normal)), Math.Ceiling(Measure(t.Name, FontWeights.SemiBold))))];
    }

    /// <summary>The open island is as wide as its menu needs, never under 560 px or over the screen less 24 (PRD 6.3).</summary>
    private void SizeExpanded()
    {
        var showGili = _settings.Current.ShowGili;
        var need = TabMenuLayout.Need(_tabs.Count, showGili, _names.Max(n => n.Bold));
        var width = TabMenuLayout.IslandWidth(need, SystemParameters.PrimaryScreenWidth);
        _tight = TabMenuLayout.IsTight(need, width);
        ExpandedLayer.Width = width;
        MenuGili.Visibility = showGili ? Visibility.Visible : Visibility.Collapsed;
        Menu.Margin = new Thickness(showGili ? 56 : 14, 14, 14, 0);
        Menu.Width = width - Menu.Margin.Left - 14;
        // Too narrow for a hovered name: it shows as a tooltip instead.
        foreach (var (button, _, _, _) in _tabs) button.ToolTip = _tight ? AutomationProperties.GetName(button) : null;
        foreach (Border page in PageTrack.Children) page.Width = width;
        _pageHeight = PageHeight();
        ShowPage(animate: false);
        UpdateTabs(animate: false);
    }

    private void BuildPages()
    {
        foreach (var tab in _vm.Tabs)
        {
            PageTrack.Children.Add(new Border { Child = CreatePage(tab) });
        }
    }

    /// <summary>A page's list or form changed: once its bindings settle, the island fits it again if it is showing.</summary>
    private void PageContentChanged(IslandPage page) => Dispatcher.InvokeAsync(() =>
    {
        if (_island.View.Page != page) return;
        _pageHeight = PageHeight();
        if (_mode == IslandMode.Expanded) Relayout(SpringParameters.Open);
    }, DispatcherPriority.Loaded);

    /// <summary>The open page's full content height, so the island grows to show it without scrolling (PRD 6.3).</summary>
    private double PageHeight()
    {
        if (PageTrack.Children[_vm.SelectedTab] is not Border { Child: FrameworkElement page }) return 0;
        page.Measure(new Size(ExpandedLayer.Width, double.PositiveInfinity));
        return page.DesiredSize.Height;
    }

    /// <summary>The page area fills the island below the menu; each page centres its content in it.</summary>
    private void SizePages(double islandHeight)
    {
        ExpandedLayer.Height = islandHeight;
        var area = Math.Max(0, islandHeight - IslandLayout.PageTop - IslandLayout.PageBottom);
        PageArea.Width = ExpandedLayer.Width;
        PageArea.Height = area;
        foreach (Border page in PageTrack.Children) page.Height = area;
    }

    /// <summary>Real pages replace their placeholders here as they are built (Media in Task 11, the rest in Phases 2–3).</summary>
    private FrameworkElement CreatePage(IslandTab tab) => tab.Page switch
    {
        IslandPage.Media => new MediaPage(_island.Media),
        IslandPage.Timer => new TimerPage(_timerPage),
        IslandPage.Alarms => new AlarmsPage(_alarmsPage),
        IslandPage.Calendar => new CalendarPage(_calendarPage),
        IslandPage.Todo => new TodoPage(_todoPage),
        IslandPage.Notes => _notesView = new NotesPage(_notesPage),
        _ => Placeholder(tab),
    };

    /// <summary>Orange for the timer, light blue for the stopwatch, red while focusing and green on a break (PRD 6.3 glow).</summary>
    private Color TimerGlow() => _island.Timers.Mode switch
    {
        TimerMode.Stopwatch => Color.FromRgb(0x64, 0xD2, 0xFF),
        TimerMode.Pomodoro => _island.Timers.Pomodoro.Phase == Gili.Core.Timers.PomodoroPhase.Focus
            ? Color.FromRgb(0xFF, 0x45, 0x3A) : Color.FromRgb(0x30, 0xD1, 0x58),
        _ => Color.FromRgb(0xFF, 0x9F, 0x0A),
    };

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
    /// The active tab and the hovered one show their names: the name slides out of the icon over 300 ms on the out curve
    /// and fades in (160 ms; the active tab 220 ms after 90 ms), the other tabs give way, and the highlight moves to where
    /// the active tab will end up with the same curve, so it stays on it the whole way (mockup <c>placeInd()</c>).
    /// </summary>
    private void UpdateTabs(bool animate)
    {
        if (_tabs.Count == 0) return;
        var selected = _vm.SelectedTab;
        var widths = TabMenuLayout.TabWidths(Menu.Width, _names, selected, _hoveredTab, _tight);
        animate &= SystemParameters.ClientAreaAnimation;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var (button, icon, name, meter) = _tabs[i];
            var active = i == selected;
            var open = active || (i == _hoveredTab && !_tight);
            if (active || i == _hoveredTab) button.Foreground = TextBrush;
            else button.ClearValue(ForegroundProperty);
            if (active) icon.Foreground = AccentBrush;
            else icon.ClearValue(IconView.ForegroundProperty);
            ((TextBlock)name.Child).FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            To(name, WidthProperty, open ? 5 + (active ? _names[i].Bold : _names[i].Regular) : 0, animate);
            name.BeginAnimation(OpacityProperty, animate
                ? new DoubleAnimation(open ? 1 : 0, TimeSpan.FromMilliseconds(active ? 220 : 160)) { BeginTime = TimeSpan.FromMilliseconds(active ? 90 : 0) }
                : null);
            if (!animate) name.Opacity = open ? 1 : 0;
            To(button, WidthProperty, widths[i], animate);
            var playing = meter is not null && _vm.MusicPlaying;
            icon.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
            if (meter is null) continue;
            meter.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
            meter.IsPlaying = playing;
        }
        var (x, width) = TabMenuLayout.Highlight(widths, selected);
        // Highlight measures X from the menu's outer edge; the strip already sits inside the 3 px padding.
        To(TabHighlightShift, TranslateTransform.XProperty, x - TabMenuLayout.Padding, animate);
        To(TabHighlight, WidthProperty, width, animate);
    }

    private static void To(IAnimatable target, DependencyProperty property, double value, bool animate)
    {
        if (!animate)
        {
            target.BeginAnimation(property, null);
            ((DependencyObject)target).SetValue(property, value);
            return;
        }
        target.BeginAnimation(property, BezierEase.To(value, 300, BezierEase.Out));
    }

    /// <summary>
    /// Only a pointer that really moves picks the hovered tab, so tabs sliding under a resting pointer don't set off a
    /// chain of openings (PRD 6.3).
    /// </summary>
    private void OnMenuMouseMove(object sender, MouseEventArgs e)
    {
        var at = PointToScreen(e.GetPosition(this));
        if (at == _lastMenuPoint) return;
        _lastMenuPoint = at;
        HoverTab(_tabs.FindIndex(t => t.Button.IsMouseOver));
    }

    private void OnMenuMouseLeave(object sender, MouseEventArgs e)
    {
        _lastMenuPoint = default;
        HoverTab(-1);
    }

    private void HoverTab(int index)
    {
        if (index == _hoveredTab) return;
        _hoveredTab = index;
        UpdateTabs(animate: _mode == IslandMode.Expanded);
    }

    private void ShowPage(bool animate) =>
        Slide(PageShift, -_vm.SelectedTab * ExpandedLayer.Width, 300, animate, BezierEase.Out);

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
                // Opening the island lands on its page; only switching while open slides, and the height follows.
                var animate = _mode == IslandMode.Expanded;
                UpdateTabs(animate);
                ShowPage(animate);
                // Today and Tomorrow move on at midnight; the Calendar opens on the current month.
                if (_island.View.Page == IslandPage.Alarms) _alarmsPage.Refresh();
                if (_island.View.Page == IslandPage.Calendar) _calendarPage.ShowToday();
                if (_island.View.Page == IslandPage.Todo) _todoPage.Refresh();
                if (_island.View.Page == IslandPage.Notes) _notesPage.Refresh();
                // Leaving Notes writes what waits (mockup goPage flushNote).
                else _island.Notes.Flush();
                _pageHeight = PageHeight();
                if (animate) Relayout(SpringParameters.Open);
                UpdateGlow();
                break;
            case nameof(IslandViewModel.CardShake):
                CardLayer.SetPulsing(_mode == IslandMode.Card, _vm.CardShake);
                break;
            case nameof(IslandViewModel.IsPlaying) or nameof(IslandViewModel.GlowPrimary) or nameof(IslandViewModel.GlowSecondary):
                UpdateGlow();
                break;
            case nameof(IslandViewModel.MusicPlaying):
                UpdateTabs(animate: false);
                UpdateGili();
                break;
            case nameof(IslandViewModel.HasActivity):
                UpdateGili();
                break;
            case nameof(IslandViewModel.Slot) or nameof(IslandViewModel.MediaSlotTitle) or nameof(IslandViewModel.IsMusic):
                // New slot content fades in over 180 ms (mockup slotIn).
                if (SystemParameters.ClientAreaAnimation)
                {
                    CompactContent.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
                }
                UpdateGili();
                break;
        }
    }

    /// <summary>Arrow keys and swipes walk the menu; a control island has none (PRD 6.4), and a dragged note holds the page.</summary>
    private void StepPage(int direction)
    {
        if (ShowsControl || _notesPage.IsDragging) return;
        var next = Math.Clamp(_vm.SelectedTab + direction, 0, _vm.Tabs.Count - 1);
        if (next != _vm.SelectedTab) _island.Open(_vm.Tabs[next].Page);
    }

    /// <summary>
    /// The wheel over a page (plan 2f decision 10): a list under the pointer that can still move that way scrolls, and
    /// only otherwise does the page switch; on Notes the vertical wheel only ever scrolls the cards (PRD 6.4 "Pindah
    /// halaman"). Decided as the wheel tunnels down, because a ScrollViewer takes it even when it cannot move.
    /// </summary>
    private void OnPagesWheel(object sender, MouseWheelEventArgs e)
    {
        if (CanScrollUnder(e.OriginalSource as DependencyObject, e.Delta)) return;
        e.Handled = true;
        if (_island.View.Page != IslandPage.Notes) Scroll(-e.Delta);
    }

    private bool CanScrollUnder(DependencyObject? element, int delta)
    {
        for (; element is not null && !ReferenceEquals(element, PageArea); element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element))
        {
            if (element is ScrollViewer list && WheelScroll.CanScroll(list.VerticalOffset, list.ScrollableHeight, delta)) return true;
        }
        return false;
    }

    /// <summary>Wheel and touchpad deltas add up to one page per notch (120), then pause while the page slides.</summary>
    private void Scroll(int delta)
    {
        if (DateTime.UtcNow < _wheelPausedUntil) return;
        _wheel += delta;
        if (Math.Abs(_wheel) < 120) return;
        StepPage(Math.Sign(_wheel));
        _wheel = 0;
        _wheelPausedUntil = DateTime.UtcNow.AddMilliseconds(380);
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
}
