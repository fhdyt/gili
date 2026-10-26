using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using TopBar.Core.Island;
using TopBar.Core.Motion;
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
            System.Windows.Controls.Canvas.SetLeft(plate, left);
            System.Windows.Controls.Canvas.SetTop(plate, _top.Value);
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
        System.Windows.Controls.Canvas.SetLeft(Bubble, Width / 2 + w / 2 + IslandLayout.BubbleGap);
        System.Windows.Controls.Canvas.SetTop(Bubble, (s.BarHeight - s.IslandHeight) / 2.0);
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
        if (_mode != IslandMode.Expanded || e.Key != Key.Escape) return;
        _island.Close();
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
}
