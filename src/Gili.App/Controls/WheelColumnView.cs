using System.Globalization;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Input;
using System.Windows.Media;
using Gili.Core.Timers;

namespace Gili.App.Controls;

/// <summary>
/// One column of the Timer page's iOS-style picker (F-ISL-29, mockup <c>.wheel</c>): five rows of 24 px, the middle
/// one large and white, the others smaller, fainter and squashed as if on a drum, with the unit beside the middle row.
/// The wheel, a drag (with a fling) or a click on a row turns it, and ↑/↓ from the keyboard. It draws only while it
/// moves (PRD 8.1).
/// </summary>
public sealed class WheelColumnView : FrameworkElement
{
    public const double RowHeight = 24;
    private const int Rows = 5;
    private const double NumberRight = 34;
    private const double LabelLeft = 40;
    private const double ClickSlop = 3;

    private static readonly Typeface Semibold = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    private static readonly Brush Text = Frozen(Color.FromRgb(0xF5, 0xF5, 0xF7));
    private static readonly Pen FocusPen = FrozenPen(Color.FromRgb(0x0A, 0x84, 0xFF), 1.5);

    private WheelColumn _column = new(60, 0);
    private int _reported;
    private int _wheel;
    private bool _animating;
    private TimeSpan _lastFrame;
    private Point _pressed;
    private bool _moved;
    private bool _held;
    private readonly List<(long Ticks, double Y)> _samples = [];

    public WheelColumnView()
    {
        Focusable = true;
        FocusVisualStyle = null;
        Width = 72;
        Height = Rows * RowHeight;
        ClipToBounds = true;
        Cursor = Cursors.Hand;
        IsKeyboardFocusedChanged += (_, _) => InvalidateVisual();
    }

    public static readonly DependencyProperty CountProperty = DependencyProperty.Register(
        nameof(Count), typeof(int), typeof(WheelColumnView), new PropertyMetadata(60, (d, _) => ((WheelColumnView)d).Rebuild()));

    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(WheelColumnView), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>How many values the column loops through: 24 for hours, 60 for minutes and seconds.</summary>
    public int Count
    {
        get => (int)GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    /// <summary>The unit beside the middle row: hours, min, sec.</summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>The value the column is settling on.</summary>
    public int Value => _column.Value;

    public bool IsMoving => _column.IsMoving;

    /// <summary>The value it heads for changed (a scroll, a drag, a click or <see cref="SetTo"/>).</summary>
    public event Action? ValueChanged;

    /// <summary>The column came to rest.</summary>
    public event Action? Settled;

    /// <summary>Turns to <paramref name="value"/> the short way round; with Windows animations off it jumps.</summary>
    public void SetTo(int value, bool animate)
    {
        _column.SetValue(value, animate && SystemParameters.ClientAreaAnimation);
        Changed();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var middle = ActualHeight / 2;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var position = _column.Position;
        for (var row = (int)Math.Floor(position) - 3; row <= (int)Math.Ceiling(position) + 3; row++)
        {
            var distance = row - position;
            var away = Math.Abs(distance);
            if (away > 2.6) continue;
            var y = middle + distance * RowHeight;
            var text = new FormattedText(WheelColumn.Wrap(row, _column.Count).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Semibold, Size(away), Text, dpi);
            // The rows bend away like a drum: squashed and fainter the further they are from the middle.
            dc.PushTransform(new ScaleTransform(1, Math.Cos(Math.Min(away, 2.5) * 0.38), NumberRight, y));
            dc.PushOpacity(Fade(away));
            dc.DrawText(text, new Point(NumberRight - text.WidthIncludingTrailingWhitespace, y - text.Height / 2));
            dc.Pop();
            dc.Pop();
        }
        if (Unit.Length > 0)
        {
            var unit = new FormattedText(Unit, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Semibold, 13, Text, dpi);
            dc.PushOpacity(0.85);
            dc.DrawText(unit, new Point(LabelLeft, middle - unit.Height / 2));
            dc.Pop();
        }
        if (IsKeyboardFocused) dc.DrawRoundedRectangle(null, FocusPen, new Rect(1, middle - 15, ActualWidth - 2, 30), 9, 9);
    }

    /// <summary>20 px in the middle, 15 px one row away, 13 px two rows away.</summary>
    private static double Size(double away) => away <= 1 ? 20 - 5 * away : Math.Max(11, 15 - 2 * (away - 1));

    /// <summary>White in the middle, 60% one row away, 30% two rows away, gone past that.</summary>
    private static double Fade(double away) => away <= 1 ? 1 - 0.4 * away : away <= 2 ? 0.6 - 0.3 * (away - 1) : Math.Max(0, 0.3 - 0.5 * (away - 2));

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        e.Handled = true;
        _wheel += e.Delta;
        var steps = _wheel / 120;
        if (steps == 0) return;
        _wheel -= steps * 120;
        Scroll(-steps);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        Focus();
        _held = true;
        CaptureMouse();
        _pressed = e.GetPosition(this);
        _moved = false;
        _samples.Clear();
        _samples.Add((Environment.TickCount64, _pressed.Y));
        _column.BeginDrag();
        Animate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var y = e.GetPosition(this).Y;
        var last = _samples[^1].Y;
        if (Math.Abs(y - _pressed.Y) > ClickSlop) _moved = true;
        _column.DragBy(-(y - last) / RowHeight);
        _samples.Add((Environment.TickCount64, y));
        if (_samples.Count > 8) _samples.RemoveAt(0);
        Changed();
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;
        e.Handled = true;
        var y = e.GetPosition(this).Y;
        _held = false;
        ReleaseMouseCapture();
        if (!_moved)
        {
            // A click picks the row under it.
            _column.EndDrag(0);
            _column.SetValue(WheelColumn.Wrap(_column.RowAt((y - ActualHeight / 2) / RowHeight), _column.Count), SystemParameters.ClientAreaAnimation);
        }
        else
        {
            _column.EndDrag(SystemParameters.ClientAreaAnimation ? Velocity(y) : 0);
        }
        Changed();
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        // Capture taken away mid-drag (another window, Alt+Tab): settle where it is.
        if (!_held) return;
        _held = false;
        _column.EndDrag(0);
        Changed();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var steps = e.Key switch
        {
            Key.Up => -1,
            Key.Down => 1,
            Key.PageUp => -5,
            Key.PageDown => 5,
            _ => 0,
        };
        if (e.Key == Key.Home)
        {
            SetTo(0, animate: true);
            e.Handled = true;
        }
        if (steps == 0) return;
        e.Handled = true;
        Scroll(steps);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <summary>Rows per second over the last 80 ms of the drag, upwards being towards higher values.</summary>
    private double Velocity(double y)
    {
        var now = Environment.TickCount64;
        var first = _samples.FirstOrDefault(s => now - s.Ticks <= 80, _samples[^1]);
        var seconds = Math.Max(0.016, (now - first.Ticks) / 1000.0);
        return -(y - first.Y) / RowHeight / seconds;
    }

    private void Scroll(int steps)
    {
        if (SystemParameters.ClientAreaAnimation) _column.Scroll(steps);
        else _column.SetValue(WheelColumn.Wrap(_column.Value + steps, _column.Count), animate: false);
        Changed();
    }

    private void Changed()
    {
        if (_column.Value != _reported)
        {
            _reported = _column.Value;
            ValueChanged?.Invoke();
        }
        Animate();
        InvalidateVisual();
    }

    private void Rebuild()
    {
        _column = new WheelColumn(Count, _column.Value);
        _reported = _column.Value;
        InvalidateVisual();
    }

    /// <summary>Steps the spring once per frame, only while the column moves.</summary>
    private void Animate()
    {
        if (_animating || !_column.IsMoving) return;
        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var time = ((RenderingEventArgs)e).RenderingTime;
        if (_lastFrame != TimeSpan.Zero) _column.Step((time - _lastFrame).TotalSeconds);
        _lastFrame = time;
        InvalidateVisual();
        if (_column.IsMoving) return;
        CompositionTarget.Rendering -= OnFrame;
        _animating = false;
        Settled?.Invoke();
    }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color, double thickness)
    {
        var pen = new Pen(Frozen(color), thickness);
        pen.Freeze();
        return pen;
    }

    /// <summary>A spinner for screen readers: its name (Hours, Minutes, Seconds) and the value, which it can also set.</summary>
    private sealed class Peer(WheelColumnView owner) : FrameworkElementAutomationPeer(owner), IRangeValueProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;

        protected override string GetClassNameCore() => nameof(WheelColumnView);

        public override object GetPattern(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);

        public double Value => owner.Value;

        public bool IsReadOnly => false;

        public double Minimum => 0;

        public double Maximum => owner.Count - 1;

        public double SmallChange => 1;

        public double LargeChange => 5;

        public void SetValue(double value) => owner.SetTo((int)Math.Clamp(Math.Round(value), 0, owner.Count - 1), animate: true);
    }
}
