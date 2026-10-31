using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Gili.App.Controls;
using Gili.Core.Motion;
using Gili.Core.Notes;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// One floating note (PRD 6.3 "Note mengambang", mockup makeFloat): an always-on-top window the size of the note,
/// with 12 px corners drawn by itself, a 28 px header to move it, Back to island at its top right, and the text to
/// type in. Every edge and corner resizes it without a visible handle. Its shadow is a separate click-through window
/// kept right below it. Bounds are in DIPs and always inside the work area.
/// </summary>
public sealed class FloatingNoteWindow : Window
{
    private const int WM_WINDOWPOSCHANGED = 0x0047;
    private static readonly Brush Plain = Frozen(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Brush Tinted = Frozen(Color.FromArgb(0xEB, 0xFF, 0xFF, 0xFF));

    private readonly NoteService _notes;
    private readonly Func<NoteRect> _workArea;
    private readonly FloatingNoteShadow _shadow = new();
    private readonly SolidColorBrush _fill = new(Color.FromRgb(0x1C, 0x1C, 0x1E));
    private readonly Border _note;
    private readonly Grid _content;
    private readonly Border _header;
    private readonly Button _back;
    private readonly TextBox _text;
    private readonly TextBlock _placeholder;
    private readonly ScaleTransform _scale = new(1, 1);
    private IntPtr _hwnd;
    private NoteRect _bounds;
    private bool _loading;
    private bool _hidden;
    // A move by the header or a resize from an edge: where it started, in screen DIPs.
    private (Point From, NoteRect Start, ResizeEdges Edges)? _hold;
    private EventHandler? _motion;
    private TaskCompletionSource? _flown;

    public FloatingNoteWindow(Note note, NoteService notes, Func<NoteRect> workArea)
    {
        Id = note.Id;
        _notes = notes;
        _workArea = workArea;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        // Off the taskbar, but in Alt+Tab, so the keyboard can reach it (plan 2h decision 9).
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        UseLayoutRounding = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        Foreground = Plain;

        _header = new Border { Height = 28, Background = Brushes.Transparent, Cursor = Cursors.Hand, ToolTip = "Drag to move", VerticalAlignment = VerticalAlignment.Top };
        var grip = new IconView
        {
            Glyph = (Geometry)Application.Current.FindResource("Icon.Grip"),
            Filled = true,
            Width = 16,
            Height = 16,
            Foreground = Frozen(Color.FromArgb(0x59, 0xFF, 0xFF, 0xFF)),
            LayoutTransform = new RotateTransform(90),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        _back = new Button
        {
            Style = (Style)Application.Current.FindResource("FloatingNoteBack"),
            ToolTip = "Back to island",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 4, 4, 0),
            Opacity = 0,
        };
        AutomationProperties.SetName(_back, "Back to island");
        _back.Click += (_, _) => BackRequested?.Invoke(this);
        var head = new Grid { Height = 28, VerticalAlignment = VerticalAlignment.Top };
        head.Children.Add(_header);
        head.Children.Add(grip);
        head.Children.Add(_back);
        _header.MouseLeftButtonDown += OnHeaderDown;

        _text = new TextBox { Style = (Style)Application.Current.FindResource("FloatingNoteText") };
        AutomationProperties.SetName(_text, "Note text");
        _placeholder = new TextBlock
        {
            Text = "Write something.",
            Foreground = Frozen(Color.FromArgb(0x73, 0xFF, 0xFF, 0xFF)),
            Margin = new Thickness(14, 0, 14, 0),
            TextWrapping = TextWrapping.Wrap,
            IsHitTestVisible = false,
        };
        var body = new Grid { Margin = new Thickness(0, 28, 0, 0) };
        body.Children.Add(_placeholder);
        body.Children.Add(_text);
        _text.TextChanged += OnTextChanged;

        _content = new Grid();
        _content.Children.Add(body);
        _content.Children.Add(head);
        _note = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = _fill,
            Child = _content,
            Focusable = true,
            FocusVisualStyle = null,
        };
        var root = new Grid { RenderTransform = _scale, RenderTransformOrigin = new Point(0.5, 0.5) };
        root.Children.Add(_note);
        root.Children.Add(Bands());
        Content = root;

        MouseEnter += (_, _) => ShowBack();
        MouseLeave += (_, _) => ShowBack();
        IsKeyboardFocusWithinChanged += (_, _) => ShowBack();
        PreviewKeyDown += OnKey;
        Show(note);
    }

    public long Id { get; }

    /// <summary>Where the note is, in DIPs.</summary>
    public NoteRect Bounds => _bounds;

    public event Action<FloatingNoteWindow>? BackRequested;

    /// <summary>The note is being moved by its header (true) or was let go (false): Gili watches it meanwhile.</summary>
    public event Action<FloatingNoteWindow, bool>? Moving;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(_hwnd)?.AddHook(Hook);
    }

    /// <summary>The note's look and text size; its text only when it differs, so typing never moves the caret.</summary>
    public void Show(Note note)
    {
        var rgb = NoteStyle.Colors[NoteStyle.ClampColor(note.Color)].Rgb;
        if (_motion is null) _fill.Color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        var font = NoteStyle.Size(note.Size).Floating;
        _text.FontSize = font;
        TextBlock.SetLineHeight(_text, font * 1.55);
        _text.Foreground = note.Color > 0 ? Tinted : Plain;
        _placeholder.FontSize = font;
        _placeholder.LineHeight = font * 1.55;
        _placeholder.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        if (NoteText.Stored(_text.Text) != note.Body)
        {
            _loading = true;
            _text.Text = note.Body;
            _loading = false;
        }
        _placeholder.Visibility = _text.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        var label = NoteText.Title(note.Body) is { Length: > 0 } title ? title : "Floating note";
        Title = label;
        AutomationProperties.SetName(this, label);
    }

    /// <summary>Puts the note here (DIPs) in one step; its shadow follows.</summary>
    public void Place(NoteRect rect)
    {
        _bounds = rect;
        if (_hwnd == IntPtr.Zero) new WindowInteropHelper(this).EnsureHandle();
        var dpi = VisualTreeHelper.GetDpi(this);
        var x = (int)Math.Round(rect.X * dpi.DpiScaleX);
        var y = (int)Math.Round(rect.Y * dpi.DpiScaleY);
        WindowStyles.Place(_hwnd, x, y, (int)Math.Round(rect.Right * dpi.DpiScaleX) - x, (int)Math.Round(rect.Bottom * dpi.DpiScaleY) - y);
    }

    /// <summary>Shows the note and its shadow at its place without taking focus.</summary>
    public void ShowAt(NoteRect rect)
    {
        Place(rect);
        Show();
        Place(rect);
        _shadow.Show();
        FollowShadow();
    }

    /// <summary>Fullscreen hides every floating note with the bar, and they come back after it (PRD 6.3).</summary>
    public void SetHidden(bool hidden)
    {
        if (_hidden == hidden) return;
        _hidden = hidden;
        if (hidden)
        {
            _shadow.Hide();
            Hide();
            return;
        }
        Show();
        _shadow.Show();
        FollowShadow();
    }

    /// <summary>The text takes the keyboard with the caret at its end, and the note pings once (mockup focusFloat).</summary>
    public void FocusText()
    {
        if (_hidden) return;
        Activate();
        _text.Focus();
        _text.CaretIndex = _text.Text.Length;
        Pop(from: 1, peak: 1.035);
    }

    /// <summary>Float on top: the note appears at 0.9 and fading in, 420 ms on the spring curve (mockup fBorn).</summary>
    public void Born()
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            Fade(this, 0, 1, 100);
            return;
        }
        Fade(this, 0, 1, 420);
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = BezierEase.Expression });
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.9, 1, TimeSpan.FromMilliseconds(420)) { EasingFunction = BezierEase.Expression });
    }

    /// <summary>
    /// Released partly outside the work area: the note springs in (damping 0.82, response 0.34 s). Nothing moves when it
    /// is already inside; without Windows animations it is there at once.
    /// </summary>
    public void SettleInside()
    {
        var inside = NoteFloat.Clamp(_bounds, _workArea());
        if (inside == _bounds) return;
        if (!SystemParameters.ClientAreaAnimation)
        {
            Place(inside);
            Save();
            return;
        }
        var parameters = new SpringParameters(0.82, 0.34);
        var x = new Spring(_bounds.X) { Parameters = parameters, Target = inside.X };
        var y = new Spring(_bounds.Y) { Parameters = parameters, Target = inside.Y };
        var last = TimeSpan.Zero;
        Run((_, e) =>
        {
            var now = ((RenderingEventArgs)e).RenderingTime;
            if (now == last) return false;
            var seconds = last == TimeSpan.Zero ? 1.0 / 60 : (now - last).TotalSeconds;
            last = now;
            x.Step(seconds);
            y.Step(seconds);
            Place(inside with { X = x.Value, Y = y.Value });
            if (!x.IsAtRest || !y.IsAtRest) return false;
            Save();
            return true;
        });
    }

    /// <summary>
    /// Back to island (mockup returnFloat): the content fades in 100 ms while the note flies to <paramref name="to"/>,
    /// 440 ms on cubic-bezier(.55, 0, .75, .2), speeding up like something pulled in. Its corners and fill go to
    /// <paramref name="radius"/> and <paramref name="fill"/>; the window fades out over the last quarter when
    /// <paramref name="vanish"/>. Without Windows animations it fades out in 100 ms where it is.
    /// </summary>
    public Task FlyAsync(NoteRect to, double radius, Color fill, bool vanish)
    {
        _flown?.TrySetResult();
        var flown = _flown = new TaskCompletionSource();
        IsHitTestVisible = false;
        if (!SystemParameters.ClientAreaAnimation)
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(100));
            fade.Completed += (_, _) => flown.TrySetResult();
            BeginAnimation(OpacityProperty, fade);
            _shadow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(100)));
            return flown.Task;
        }
        Fade(_content, 1, 0, 100);
        // The shadow goes first: a pill at the island has none.
        _shadow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(220)));
        var from = _bounds;
        var fromFill = _fill.Color;
        var ease = new CubicBezier(0.55, 0, 0.75, 0.2);
        var clock = Stopwatch.StartNew();
        Run((_, _) =>
        {
            var t = Math.Min(1, clock.Elapsed.TotalMilliseconds / 440);
            var k = ease.Ease(t);
            Place(new NoteRect(
                from.X + (to.X - from.X) * k,
                from.Y + (to.Y - from.Y) * k,
                Math.Max(1, from.Width + (to.Width - from.Width) * k),
                Math.Max(1, from.Height + (to.Height - from.Height) * k)));
            _note.CornerRadius = new CornerRadius(12 + (radius - 12) * k);
            _fill.Color = Mix(fromFill, fill, k);
            if (vanish) Opacity = t < 0.75 ? 1 : 1 - (t - 0.75) / 0.25;
            if (t < 1) return false;
            flown.TrySetResult();
            return true;
        });
        return flown.Task;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopMotion();
        _flown?.TrySetResult();
        _shadow.Close();
        base.OnClosed(e);
    }

    /// <summary>The note's own shadow window stays right below it whenever it moves, sizes or comes to the front.</summary>
    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_WINDOWPOSCHANGED) FollowShadow();
        return IntPtr.Zero;
    }

    private void FollowShadow()
    {
        if (_hwnd == IntPtr.Zero || !_shadow.IsVisible) return;
        var (left, top, width, height) = WindowStyles.Bounds(_hwnd);
        _shadow.Follow(_hwnd, left, top, width, height);
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _placeholder.Visibility = _text.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_loading) return;
        _notes.Edit(Id, _text.Text);
        var label = NoteText.Title(_text.Text) is { Length: > 0 } title ? title : "Floating note";
        Title = label;
        AutomationProperties.SetName(this, label);
    }

    /// <summary>Esc lets go of the text (mockup ta.blur()); Ctrl+= / Ctrl+- / Ctrl+0 step the text size as in the island.</summary>
    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Keyboard.Focus(_note);
            e.Handled = true;
            return;
        }
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != ModifierKeys.Control) return;
        int? step = e.Key switch
        {
            Key.OemPlus or Key.Add => 1,
            Key.OemMinus or Key.Subtract => -1,
            Key.D0 or Key.NumPad0 => 0,
            _ => null,
        };
        if (step is not { } delta || _notes.Notes.FirstOrDefault(n => n.Id == Id) is not { } note) return;
        _notes.SetSize(Id, delta == 0 ? NoteStyle.Default : NoteStyle.Step(note.Size, delta));
        e.Handled = true;
    }

    /// <summary>Back to island shows while the pointer is over the note or it has the keyboard (mockup .fhd button), in 150 ms.</summary>
    private void ShowBack()
    {
        var on = IsMouseOver || IsKeyboardFocusWithin;
        _back.BeginAnimation(OpacityProperty, new DoubleAnimation(on ? 1 : 0, TimeSpan.FromMilliseconds(150)));
    }

    /// <summary>Moving by the header follows the pointer 1:1 from where it was grabbed, kept inside the work area.</summary>
    private void OnHeaderDown(object sender, MouseButtonEventArgs e)
    {
        if (_motion is not null) return;
        _hold = (ScreenPoint(e), _bounds, ResizeEdges.None);
        _header.CaptureMouse();
        _header.MouseMove += OnHoldMove;
        _header.LostMouseCapture += OnHoldEnd;
        _header.MouseLeftButtonUp += OnHoldUp;
        _shadow.SetHeld(true);
        Moving?.Invoke(this, true);
        e.Handled = true;
    }

    /// <summary>The 6 px bands along the edges and the 12 px squares at the corners (mockup .fr), drawn clear.</summary>
    private Grid Bands()
    {
        var bands = new Grid();
        void Add(ResizeEdges edges, Cursor cursor, HorizontalAlignment h, VerticalAlignment v, double width, double height, Thickness margin)
        {
            var band = new Border
            {
                Background = Brushes.Transparent,
                Cursor = cursor,
                HorizontalAlignment = h,
                VerticalAlignment = v,
                Width = width,
                Height = height,
                Margin = margin,
            };
            band.MouseLeftButtonDown += (_, e) =>
            {
                if (_motion is not null) return;
                _hold = (ScreenPoint(e), _bounds, edges);
                band.CaptureMouse();
                band.MouseMove += OnHoldMove;
                band.LostMouseCapture += OnHoldEnd;
                band.MouseLeftButtonUp += OnHoldUp;
                e.Handled = true;
            };
            bands.Children.Add(band);
        }
        const double edge = NoteFloat.EdgeBand;
        const double corner = NoteFloat.CornerBand;
        var stretch = double.NaN;
        Add(ResizeEdges.Top, Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Top, stretch, edge, new Thickness(corner, 0, corner, 0));
        Add(ResizeEdges.Bottom, Cursors.SizeNS, HorizontalAlignment.Stretch, VerticalAlignment.Bottom, stretch, edge, new Thickness(corner, 0, corner, 0));
        Add(ResizeEdges.Left, Cursors.SizeWE, HorizontalAlignment.Left, VerticalAlignment.Stretch, edge, stretch, new Thickness(0, corner, 0, corner));
        Add(ResizeEdges.Right, Cursors.SizeWE, HorizontalAlignment.Right, VerticalAlignment.Stretch, edge, stretch, new Thickness(0, corner, 0, corner));
        Add(ResizeEdges.Left | ResizeEdges.Top, Cursors.SizeNWSE, HorizontalAlignment.Left, VerticalAlignment.Top, corner, corner, default);
        Add(ResizeEdges.Right | ResizeEdges.Bottom, Cursors.SizeNWSE, HorizontalAlignment.Right, VerticalAlignment.Bottom, corner, corner, default);
        Add(ResizeEdges.Right | ResizeEdges.Top, Cursors.SizeNESW, HorizontalAlignment.Right, VerticalAlignment.Top, corner, corner, default);
        Add(ResizeEdges.Left | ResizeEdges.Bottom, Cursors.SizeNESW, HorizontalAlignment.Left, VerticalAlignment.Bottom, corner, corner, default);
        return bands;
    }

    private void OnHoldMove(object sender, MouseEventArgs e)
    {
        if (_hold is not { } hold) return;
        var at = ScreenPoint(e);
        var (dx, dy) = (at.X - hold.From.X, at.Y - hold.From.Y);
        var work = _workArea();
        Place(hold.Edges == ResizeEdges.None
            ? NoteFloat.Clamp(hold.Start with { X = hold.Start.X + dx, Y = hold.Start.Y + dy }, work)
            : NoteFloat.Resize(hold.Start, hold.Edges, dx, dy, work));
    }

    private void OnHoldUp(object sender, MouseButtonEventArgs e) => ((UIElement)sender).ReleaseMouseCapture();

    private void OnHoldEnd(object sender, MouseEventArgs e)
    {
        var held = (UIElement)sender;
        held.MouseMove -= OnHoldMove;
        held.LostMouseCapture -= OnHoldEnd;
        held.MouseLeftButtonUp -= OnHoldUp;
        if (_hold is not { } hold) return;
        _hold = null;
        Save();
        if (hold.Edges != ResizeEdges.None) return;
        _shadow.SetHeld(false);
        Moving?.Invoke(this, false);
    }

    private void Save() => _notes.MoveFloat(Id, _bounds);

    /// <summary>The pointer in screen DIPs, so the note moving under it never changes where it is.</summary>
    private Point ScreenPoint(MouseEventArgs e)
    {
        var device = PointToScreen(e.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Point(device.X / dpi.DpiScaleX, device.Y / dpi.DpiScaleY);
    }

    private void Pop(double from, double peak)
    {
        if (!SystemParameters.ClientAreaAnimation) return;
        var pop = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(420) };
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(from, KeyTime.FromPercent(0)));
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(peak, KeyTime.FromPercent(0.5)) { EasingFunction = BezierEase.Expression });
        pop.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromPercent(1)) { EasingFunction = BezierEase.Expression });
        _scale.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
        _scale.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
    }

    /// <summary>One motion at a time, stepped once per rendered frame until <paramref name="step"/> says it is done.</summary>
    private void Run(Func<object?, EventArgs, bool> step)
    {
        StopMotion();
        _motion = (sender, e) =>
        {
            if (step(sender, e)) StopMotion();
        };
        CompositionTarget.Rendering += _motion;
    }

    private void StopMotion()
    {
        if (_motion is null) return;
        CompositionTarget.Rendering -= _motion;
        _motion = null;
    }

    private static void Fade(UIElement element, double from, double to, int milliseconds) =>
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds)));

    private static Color Mix(Color a, Color b, double k) => Color.FromArgb(
        (byte)Math.Round(a.A + (b.A - a.A) * k),
        (byte)Math.Round(a.R + (b.R - a.R) * k),
        (byte)Math.Round(a.G + (b.G - a.G) * k),
        (byte)Math.Round(a.B + (b.B - a.B) * k));

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
