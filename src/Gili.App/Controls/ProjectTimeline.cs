using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using Gili.Core.Motion;
using Gili.Core.Notes;
using Gili.Core.Projects;

namespace Gili.App.Controls;

/// <summary>
/// The Projects timeline (F-ISL-34, mockup .pjtl): a 30 px column per day, a month row and a day row, then one 30 px lane
/// per task and an empty lane to add one. Only the days in view are drawn; scrolling moves an offset and redraws, and the
/// bars are the only child elements. A bar dragged by its middle moves both days, by an end only that day; it lands on
/// whole days with the compact ↔ wide spring. Nothing runs while nothing moves.
/// </summary>
public sealed class ProjectTimeline : FrameworkElement
{
    public const double DayWidth = 30, Row = 30, Head = 46;
    private const double EdgeStart = 7, EdgeEnd = 9, DragThreshold = 4, AutoScrollZone = 32, AutoScrollStep = 8;
    private static readonly TimeSpan ScrollTime = TimeSpan.FromMilliseconds(380);

    internal static readonly Typeface Regular = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    internal static readonly Typeface Medium = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Medium, FontStretches.Normal);
    internal static readonly Typeface SemiBold = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
    internal static readonly Typeface Bold = new(new FontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
    internal static readonly Brush Text = Frozen(0xFF, 0xF5, 0xF5, 0xF7);
    internal static readonly Brush Text2 = Frozen(0xFF, 0x8E, 0x8E, 0x93);
    internal static readonly Brush Text3 = Frozen(0xFF, 0x63, 0x63, 0x66);
    internal static readonly Brush Accent = Frozen(0xFF, 0x30, 0xD1, 0x58);
    private static readonly Brush Surface = Frozen(9, 0xFF, 0xFF, 0xFF);
    private static readonly Brush Weekend = Frozen(9, 0xFF, 0xFF, 0xFF);
    private static readonly Brush GridLine = Frozen(18, 0xFF, 0xFF, 0xFF);
    private static readonly Brush RuleLine = Frozen(26, 0xFF, 0xFF, 0xFF);
    private static readonly Brush TodayLine = Frozen(140, 0x30, 0xD1, 0x58);
    private static readonly Brush MonthLine = Frozen(0xFF, 0x3A, 0x3A, 0x3C);
    private static readonly Brush TipFill = Frozen(0xFF, 0x3A, 0x3A, 0x3C);
    private static readonly Brush StickyFade = Fade();

    public static readonly DependencyProperty TasksProperty = DependencyProperty.Register(
        nameof(Tasks), typeof(IReadOnlyList<ProjectTask>), typeof(ProjectTimeline),
        new FrameworkPropertyMetadata(Array.Empty<ProjectTask>(), FrameworkPropertyMetadataOptions.AffectsMeasure, (d, _) => ((ProjectTimeline)d).SyncBars()));

    public static readonly DependencyProperty TodayProperty = DependencyProperty.Register(
        nameof(Today), typeof(DateOnly), typeof(ProjectTimeline),
        new FrameworkPropertyMetadata(DateOnly.FromDateTime(DateTime.Now), (d, _) => ((ProjectTimeline)d).SyncBars()));

    public static readonly DependencyProperty SelectedIdProperty = DependencyProperty.Register(
        nameof(SelectedId), typeof(long?), typeof(ProjectTimeline), new FrameworkPropertyMetadata(null, (d, _) => ((ProjectTimeline)d).SyncBars()));

    /// <summary>The project on show: a new one opens at its home scroll.</summary>
    public static readonly DependencyProperty ProjectKeyProperty = DependencyProperty.Register(
        nameof(ProjectKey), typeof(long), typeof(ProjectTimeline), new FrameworkPropertyMetadata(0L, (d, _) => ((ProjectTimeline)d)._home = true));

    private readonly Lanes _lanes;
    private readonly Overlay _overlay;
    private readonly Dictionary<long, TimelineBar> _bars = [];
    private TimelineSpan _span;
    private double _offset;
    private double _laneOffset;
    private bool _home = true;
    private bool _ticking;
    private DateTime _lastFrame;
    private (double From, double To, DateTime Start)? _scroll;
    private Drag? _drag;
    private DateOnly? _hoverDay;

    public ProjectTimeline()
    {
        _lanes = new Lanes(this);
        _overlay = new Overlay(this);
        AddVisualChild(_lanes);
        AddVisualChild(_overlay);
        Focusable = false;
        ClipToBounds = true;
        Cursor = Cursors.Arrow;
    }

    public IReadOnlyList<ProjectTask> Tasks
    {
        get => (IReadOnlyList<ProjectTask>)GetValue(TasksProperty);
        set => SetValue(TasksProperty, value);
    }

    public DateOnly Today
    {
        get => (DateOnly)GetValue(TodayProperty);
        set => SetValue(TodayProperty, value);
    }

    public long? SelectedId
    {
        get => (long?)GetValue(SelectedIdProperty);
        set => SetValue(SelectedIdProperty, value);
    }

    public long ProjectKey
    {
        get => (long)GetValue(ProjectKeyProperty);
        set => SetValue(ProjectKeyProperty, value);
    }

    /// <summary>A bar was dragged or nudged to new days.</summary>
    public event Action<long, DateOnly, DateOnly>? Moved;

    /// <summary>A bar was clicked without dragging, or Enter was pressed on it: open the form on that task.</summary>
    public event Action<long>? Opened;

    /// <summary>A bar's tick circle was clicked, or Space pressed on it.</summary>
    public event Action<long>? Toggled;

    /// <summary>A day in the empty lane under the last task was clicked.</summary>
    public event Action<DateOnly>? AddRequested;

    private static bool Animate => SystemParameters.ClientAreaAnimation;

    private double ViewWidth => Math.Max(1, ActualWidth);

    private double LanesHeight => (Tasks.Count + 1) * Row + 6;

    /// <summary>The lanes are taller than the room the island gives them: the vertical wheel scrolls them instead.</summary>
    public bool LanesOverflow => ActualHeight > 0 && Head + LanesHeight > ActualHeight + 0.5;

    internal TimelineSpan Span => _span;

    internal double Offset => _offset;

    internal double LaneOffset => _laneOffset;

    internal DateOnly? HoverDay => _hoverDay;

    internal Drag? Dragging => _drag;

    internal double ScreenX(DateOnly day) => TimelineRange.X(_span, day, DayWidth) - _offset;

    /// <summary>‹ and ›: a week back or forward, sliding over 380 ms.</summary>
    public void ScrollDays(int days) => ScrollTo(_offset + days * DayWidth, Animate);

    /// <summary>Today a third of the way in.</summary>
    public void ScrollToToday() => ScrollTo(TimelineRange.Today(Today, _span, DayWidth, ViewWidth), Animate);

    /// <summary>A horizontal swipe or tilt (WM_MOUSEHWHEEL) over the timeline.</summary>
    public void ScrollBy(double pixels) => ScrollTo(_offset + pixels, animate: false);

    public void FocusBar(long id)
    {
        if (_bars.TryGetValue(id, out var bar)) Keyboard.Focus(bar);
    }

    protected override int VisualChildrenCount => 2;

    protected override Visual GetVisualChild(int index) => index == 0 ? _lanes : _overlay;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 520 : availableSize.Width;
        var height = Head + LanesHeight;
        _lanes.Measure(new Size(width, LanesHeight));
        _overlay.Measure(new Size(width, height));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_home && finalSize.Width > 1)
        {
            _home = false;
            _span = TimelineRange.Of(Tasks.ToList(), Today);
            _offset = TimelineRange.Home(Tasks.ToList(), Today, _span, DayWidth, finalSize.Width);
            _laneOffset = 0;
            _scroll = null;
            foreach (var bar in _bars.Values) bar.Snap(BarX(bar.Task), BarWidth(bar.Task), BarY(bar.Lane));
        }
        _offset = TimelineRange.Clamp(_offset, _span, DayWidth, finalSize.Width);
        _laneOffset = Math.Clamp(_laneOffset, 0, Math.Max(0, Head + LanesHeight - finalSize.Height));
        _lanes.Arrange(new Rect(0, Head, finalSize.Width, Math.Max(0, finalSize.Height - Head)));
        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height), 12, 12));
        dc.DrawRectangle(Surface, null, new Rect(0, 0, width, height));
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var (first, last) = VisibleDays();
        for (var day = first; day <= last; day = day.AddDays(1))
        {
            var x = ScreenX(day);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) dc.DrawRectangle(Weekend, null, new Rect(x, 0, DayWidth, height));
            if (day.Day == 1)
            {
                dc.DrawRectangle(MonthLine, null, new Rect(x, 5, 1, 12));
                dc.DrawText(Label(ProjectText.Month(day, Today.Year), 11, SemiBold, Text2, dpi), new Point(x + 7, 4));
            }
            var number = Label(day.Day.ToString(CultureInfo.InvariantCulture), 11.5, day == Today ? Bold : Medium,
                day == Today ? Brushes.Black : day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? Text3 : Text2, dpi);
            if (day == Today) dc.DrawEllipse(Accent, null, new Point(x + DayWidth / 2, 30), 10, 10);
            dc.DrawText(number, new Point(x + (DayWidth - number.Width) / 2, 30 - number.Height / 2));
        }
        // The month of the first day in view stays at the left, pushed out by the next month's label as it arrives.
        var firstDay = _span.First.AddDays((int)Math.Floor(_offset / DayWidth));
        var sticky = Label(ProjectText.MonthYear(firstDay), 11, SemiBold, Text, dpi);
        var next = new DateOnly(firstDay.Year, firstDay.Month, 1).AddMonths(1);
        var stickyWidth = sticky.Width + 21;
        var shift = Math.Min(0, ScreenX(next) - stickyWidth);
        dc.DrawRectangle(StickyFade, null, new Rect(shift, 0, stickyWidth, 20));
        dc.DrawText(sticky, new Point(shift + 9, 4));
        dc.DrawRectangle(RuleLine, null, new Rect(0, 42, width, 1));
        dc.Pop();
    }

    internal (DateOnly First, DateOnly Last) VisibleDays()
    {
        if (_span.Days == 0) return (Today, Today.AddDays(-1));
        var first = _span.First.AddDays(Math.Max(0, (int)Math.Floor(_offset / DayWidth)));
        var last = _span.First.AddDays(Math.Min(_span.Days - 1, (int)Math.Ceiling((_offset + ViewWidth) / DayWidth)));
        return (first, last);
    }

    internal static FormattedText Label(string text, double size, Typeface face, Brush brush, double dpi) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, size, brush, dpi);

    /// <summary>Behind the month that stays at the left: the timeline's colour over the island, fading out on its right.</summary>
    private static LinearGradientBrush Fade()
    {
        var fade = new LinearGradientBrush(new GradientStopCollection
        {
            new(Color.FromRgb(0x15, 0x15, 0x17), 0),
            new(Color.FromRgb(0x15, 0x15, 0x17), 0.75),
            new(Color.FromArgb(0, 0x15, 0x15, 0x17), 1),
        }, 0);
        fade.Freeze();
        return fade;
    }

    private static SolidColorBrush Frozen(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    private double BarX(ProjectTask task) => TimelineRange.X(_span, task.Start, DayWidth) + 2;

    private static double BarWidth(ProjectTask task) => task.Days * DayWidth - 4;

    private static double BarY(int lane) => lane * Row + 3;

    /// <summary>
    /// Brings the bars in line with the tasks: new ones appear in place, changed ones spring to their new days and lanes,
    /// gone ones leave. The range around the project grows or shrinks while the days at the left edge stay put.
    /// </summary>
    private void SyncBars()
    {
        var tasks = Tasks;
        var oldFirst = _span.First;
        var hadSpan = _span.Days > 0;
        _span = TimelineRange.Of(tasks.ToList(), Today);
        if (hadSpan && !_home) _offset += (oldFirst.DayNumber - _span.First.DayNumber) * DayWidth;
        var keep = new HashSet<long>();
        for (var lane = 0; lane < tasks.Count; lane++)
        {
            var task = tasks[lane];
            keep.Add(task.Id);
            if (!_bars.TryGetValue(task.Id, out var bar))
            {
                bar = new TimelineBar(task, lane);
                _bars[task.Id] = bar;
                _lanes.Add(bar);
                bar.Snap(BarX(task), BarWidth(task), BarY(lane));
            }
            else if (_drag?.Bar != bar)
            {
                bar.Lane = lane;
                bar.Task = task;
                bar.MoveTo(BarX(task), BarWidth(task), BarY(lane), Animate && !_home);
            }
            bar.Update(task, Today, SelectedId == task.Id);
        }
        foreach (var gone in _bars.Keys.Where(id => !keep.Contains(id)).ToList())
        {
            _lanes.Remove(_bars[gone]);
            _bars.Remove(gone);
        }
        StartTicking();
        InvalidateMeasure();
        InvalidateArrange();
        InvalidateVisual();
        _lanes.InvalidateVisual();
    }

    private void ScrollTo(double offset, bool animate)
    {
        offset = TimelineRange.Clamp(offset, _span, DayWidth, ViewWidth);
        if (animate)
        {
            _scroll = (_offset, offset, DateTime.UtcNow);
            StartTicking();
            return;
        }
        _scroll = null;
        SetOffset(offset);
    }

    private void SetOffset(double offset)
    {
        _offset = TimelineRange.Clamp(offset, _span, DayWidth, ViewWidth);
        InvalidateVisual();
        _lanes.InvalidateArrange();
        _lanes.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    private void StartTicking()
    {
        if (_ticking) return;
        _ticking = true;
        _lastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += OnFrame;
    }

    /// <summary>One frame: the week slide, the bars' springs and the scroll while a drag holds an edge; stops when all rest.</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var seconds = (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        var busy = false;
        if (_scroll is { } scroll)
        {
            var t = Math.Min(1, (now - scroll.Start).TotalMilliseconds / ScrollTime.TotalMilliseconds);
            SetOffset(scroll.From + (scroll.To - scroll.From) * CubicBezier.Out.Ease(t));
            if (t >= 1) _scroll = null;
            else busy = true;
        }
        if (_drag is { Moved: true } drag)
        {
            var left = drag.Pointer.X < AutoScrollZone ? -AutoScrollStep : drag.Pointer.X > ActualWidth - AutoScrollZone ? AutoScrollStep : 0;
            if (left != 0)
            {
                SetOffset(_offset + left);
                DragTo(drag.Pointer);
            }
            busy = true;
        }
        var moving = false;
        foreach (var bar in _bars.Values) moving |= bar.Step(seconds);
        if (moving)
        {
            _lanes.InvalidateArrange();
            busy = true;
        }
        if (busy) return;
        _ticking = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var point = e.GetPosition(this);
        if (e.OriginalSource is TimelineBar bar)
        {
            var inBar = e.GetPosition(bar);
            var edge = inBar.X < EdgeStart ? DragEdge.Start : inBar.X > bar.ActualWidth - EdgeEnd ? DragEdge.End : DragEdge.Move;
            var check = edge == DragEdge.Move && bar.IsOnCheck(inBar);
            _drag = new Drag(bar, edge, point, _offset, check);
            CaptureMouse();
            e.Handled = true;
            return;
        }
        if (LaneDay(point) is { } day)
        {
            AddRequested?.Invoke(day);
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var point = e.GetPosition(this);
        if (_drag is { } drag)
        {
            drag.Pointer = point;
            if (!drag.Moved && Math.Abs(point.X - drag.Start.X + (_offset - drag.StartOffset)) < DragThreshold) return;
            if (!drag.Moved)
            {
                drag.Moved = true;
                drag.Bar.Lifted = true;
                _lanes.BringToFront(drag.Bar);
                StartTicking();
            }
            DragTo(point);
            return;
        }
        if (e.OriginalSource is TimelineBar bar)
        {
            var inBar = e.GetPosition(bar);
            bar.Cursor = !bar.IsOnCheck(inBar) && (inBar.X < EdgeStart || inBar.X > bar.ActualWidth - EdgeEnd) ? Cursors.SizeWE : Cursors.Hand;
        }
        var day = LaneDay(point);
        if (day != _hoverDay)
        {
            _hoverDay = day;
            Cursor = day is null ? Cursors.Arrow : Cursors.Hand;
            _lanes.InvalidateVisual();
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverDay is null) return;
        _hoverDay = null;
        _lanes.InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_drag is not { } drag) return;
        _drag = null;
        ReleaseMouseCapture();
        e.Handled = true;
        var id = drag.Original.Id;
        if (!drag.Moved)
        {
            if (drag.OnCheck) Toggled?.Invoke(id);
            else Opened?.Invoke(id);
            return;
        }
        Land(drag);
        if (drag.NewStart != drag.Original.Start || drag.NewEnd != drag.Original.End) Moved?.Invoke(id, drag.NewStart, drag.NewEnd);
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_drag is not { } drag) return;
        _drag = null;
        if (!drag.Moved) return;
        // Lost to another window mid-drag: the bar goes back to where it was.
        drag.NewStart = drag.Original.Start;
        drag.NewEnd = drag.Original.End;
        Land(drag);
    }

    private void Land(Drag drag)
    {
        var bar = drag.Bar;
        bar.Lifted = false;
        var landed = drag.Original with { Start = drag.NewStart, End = drag.NewEnd };
        bar.MoveTo(BarX(landed), BarWidth(landed), BarY(bar.Lane), Animate);
        bar.Update(landed, Today, SelectedId == landed.Id);
        StartTicking();
        _lanes.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    /// <summary>The bar follows the pointer 1:1 when moved whole, or its end snaps a day at a time when resized.</summary>
    private void DragTo(Point point)
    {
        if (_drag is not { } drag) return;
        var bar = drag.Bar;
        // Always from the days the drag began on: the bar shows the days it would land on as it goes.
        var task = drag.Original;
        var pixels = point.X - drag.Start.X + (_offset - drag.StartOffset);
        (drag.NewStart, drag.NewEnd) = TaskDrag.Apply(drag.Edge, task.Start, task.End, TaskDrag.Days(pixels, DayWidth));
        var landed = task with { Start = drag.NewStart, End = drag.NewEnd };
        if (drag.Edge == DragEdge.Move) bar.Snap(BarX(task) + pixels, BarWidth(task), BarY(bar.Lane));
        else bar.Snap(BarX(landed), BarWidth(landed), BarY(bar.Lane));
        bar.Update(landed, Today, SelectedId == task.Id);
        _lanes.InvalidateArrange();
        _lanes.InvalidateVisual();
        _overlay.InvalidateVisual();
    }

    /// <summary>The day under the pointer when it is in the empty lane under the last task.</summary>
    private DateOnly? LaneDay(Point point)
    {
        var lane = point.Y - Head + _laneOffset;
        if (point.Y < Head || lane < Tasks.Count * Row || lane >= (Tasks.Count + 1) * Row) return null;
        return _span.First.AddDays((int)Math.Floor((point.X + _offset) / DayWidth));
    }

    /// <summary>
    /// The wheel scrolls the days and never turns the page. While the lanes are taller than the island allows, the vertical
    /// wheel scrolls them instead and Shift + wheel scrolls the days.
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        e.Handled = true;
        if (LanesOverflow && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            _laneOffset = Math.Clamp(_laneOffset - e.Delta / 3.0, 0, Head + LanesHeight - ActualHeight);
            _lanes.InvalidateArrange();
            _lanes.InvalidateVisual();
            return;
        }
        ScrollTo(_offset - e.Delta * 0.75, animate: false);
    }

    /// <summary>On a focused bar: Space ticks it, Enter opens it, Alt + ←/→ moves it a day, Alt + Shift + ←/→ its end, ↑/↓ the next bar.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.OriginalSource is not TimelineBar bar) return;
        var task = bar.Task;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        switch (key)
        {
            case Key.Space:
                Toggled?.Invoke(task.Id);
                break;
            case Key.Enter:
                Opened?.Invoke(task.Id);
                break;
            case Key.Left or Key.Right when alt:
                var day = key == Key.Right ? 1 : -1;
                var (start, end) = shift ? TaskDrag.Apply(DragEdge.End, task.Start, task.End, day) : TaskDrag.Apply(DragEdge.Move, task.Start, task.End, day);
                Moved?.Invoke(task.Id, start, end);
                break;
            case Key.Up or Key.Down when !alt:
                var next = Tasks.Count == 0 ? -1 : Math.Clamp(bar.Lane + (key == Key.Down ? 1 : -1), 0, Tasks.Count - 1);
                if (next >= 0) FocusBar(Tasks[next].Id);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <summary>A drag in progress: the bar, which part was grabbed, where, and the days it would land on now.</summary>
    internal sealed class Drag(TimelineBar bar, DragEdge edge, Point start, double startOffset, bool onCheck)
    {
        public TimelineBar Bar { get; } = bar;
        public ProjectTask Original { get; } = bar.Task;
        public DragEdge Edge { get; } = edge;
        public Point Start { get; } = start;
        public double StartOffset { get; } = startOffset;
        public bool OnCheck { get; } = onCheck;
        public Point Pointer { get; set; } = start;
        public bool Moved { get; set; }
        public DateOnly NewStart { get; set; } = bar.Task.Start;
        public DateOnly NewEnd { get; set; } = bar.Task.End;
    }

    /// <summary>
    /// The lanes under the header: the grid, the today line, the dashed day under the pointer in the empty lane and the
    /// landing ghost behind a dragged bar, then the bars. Clipped, so scrolled lanes slide under the header.
    /// </summary>
    private sealed class Lanes : FrameworkElement
    {
        private static readonly Pen Dashed = DashedPen();
        private readonly ProjectTimeline _owner;
        private readonly VisualCollection _children;

        public Lanes(ProjectTimeline owner)
        {
            _owner = owner;
            _children = new VisualCollection(this);
            ClipToBounds = true;
        }

        public void Add(TimelineBar bar) => _children.Add(bar);

        public void Remove(TimelineBar bar) => _children.Remove(bar);

        /// <summary>A lifted bar is drawn over the others (the last child is the topmost).</summary>
        public void BringToFront(TimelineBar bar)
        {
            if (ReferenceEquals(_children[^1], bar)) return;
            _children.Remove(bar);
            _children.Add(bar);
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index) => _children[index];

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (TimelineBar bar in _children) bar.Measure(new Size(double.PositiveInfinity, 24));
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (TimelineBar bar in _children)
                bar.Arrange(new Rect(bar.X - _owner.Offset, bar.Y - _owner.LaneOffset, Math.Max(0, bar.Length), 24));
            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            var width = ActualWidth;
            var height = ActualHeight;
            // Transparent, so the empty lane and the gaps between bars still take the pointer.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
            var (first, last) = _owner.VisibleDays();
            for (var day = first; day <= last.AddDays(1); day = day.AddDays(1))
                dc.DrawRectangle(GridLine, null, new Rect(_owner.ScreenX(day) - 1, 0, 1, height));
            var lanes = _owner.Tasks.Count + 1;
            for (var lane = 0; lane <= lanes; lane++)
            {
                var y = lane * Row - _owner.LaneOffset;
                if (y >= 0 && y <= height) dc.DrawRectangle(GridLine, null, new Rect(0, y, width, 1));
            }
            dc.DrawRoundedRectangle(TodayLine, null, new Rect(_owner.ScreenX(_owner.Today) + 14, 0, 2, height), 1, 1);
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var emptyLane = _owner.Tasks.Count * Row + 3 - _owner.LaneOffset;
            if (_owner.HoverDay is { } hover)
            {
                var x = _owner.ScreenX(hover);
                dc.DrawRoundedRectangle(null, Dashed, new Rect(x + 2.75, emptyLane + 0.75, 24.5, 22.5), 11.25, 11.25);
                var label = Label(_owner.Tasks.Count == 0 ? "Click a day to add a task" : "New task", 12, Regular, Text2, dpi);
                dc.DrawText(label, new Point(x + 36, emptyLane + 12 - label.Height / 2));
            }
            else if (_owner.Tasks.Count == 0)
            {
                var label = Label("Click a day to add a task", 12, Regular, Text2, dpi);
                dc.DrawText(label, new Point(_owner.ScreenX(_owner.Today) + 36, emptyLane + 12 - label.Height / 2));
            }
            if (_owner.Dragging is { Moved: true, Edge: DragEdge.Move } drag)
            {
                var x = TimelineRange.X(_owner.Span, drag.NewStart, DayWidth) + 2 - _owner.Offset;
                var w = (drag.NewEnd.DayNumber - drag.NewStart.DayNumber + 1) * DayWidth - 4;
                var ghost = new Rect(x + 0.75, drag.Bar.Y - _owner.LaneOffset + 0.75, w - 1.5, 22.5);
                dc.DrawRoundedRectangle(GhostFill, GhostPen, ghost, 11.25, 11.25);
            }
        }

        private static readonly Brush GhostFill = Frozen(13, 0xFF, 0xFF, 0xFF);
        private static readonly Pen GhostPen = FrozenPen(Frozen(89, 0xFF, 0xFF, 0xFF), 1.5);

        private static Pen DashedPen()
        {
            var pen = new Pen(Frozen(77, 0xFF, 0xFF, 0xFF), 1.5) { DashStyle = new DashStyle([3, 2], 0) };
            pen.Freeze();
            return pen;
        }

        private static Pen FrozenPen(Brush brush, double thickness)
        {
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            return pen;
        }
    }

    /// <summary>The label over a dragged bar: its days, "Thu 5 Nov → Mon 9 Nov · 5 days".</summary>
    private sealed class Overlay : FrameworkElement
    {
        private readonly ProjectTimeline _owner;

        public Overlay(ProjectTimeline owner)
        {
            _owner = owner;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (_owner.Dragging is not { Moved: true } drag) return;
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var text = Label(ProjectText.DragLabel(drag.NewStart, drag.NewEnd), 11.5, Regular, Text, dpi);
            var bar = drag.Bar;
            var centre = bar.X - _owner.Offset + bar.Length / 2;
            var top = Head + bar.Y - _owner.LaneOffset - 25;
            var box = new Rect(centre - text.Width / 2 - 8, Math.Max(0, top), text.Width + 16, text.Height + 6);
            dc.DrawRoundedRectangle(TipFill, null, box, 7, 7);
            dc.DrawText(text, new Point(box.X + 8, box.Y + 3));
        }
    }
}

/// <summary>
/// One task's bar (mockup .pjbar): a pill in the task's note colour with a tick circle and the name, which sits after the
/// bar when the bar is too short for it. Overdue has a red ring, done a green tick and struck-through grey text.
/// Focusable for the keyboard; its accessible name says the days and the state.
/// </summary>
public sealed class TimelineBar : FrameworkElement
{
    private const double Height24 = 24;
    private static readonly Brush Rim = Freeze(new SolidColorBrush(Color.FromArgb(36, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush RimHover = Freeze(new SolidColorBrush(Color.FromArgb(82, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush Danger = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)));
    private static readonly Brush FocusRing = Freeze(new SolidColorBrush(Color.FromRgb(0x0A, 0x84, 0xFF)));
    private static readonly Brush CheckRing = Freeze(new SolidColorBrush(Color.FromArgb(179, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush DoneText = Freeze(new SolidColorBrush(Color.FromArgb(128, 0xFF, 0xFF, 0xFF)));
    private static readonly Brush Shadow = Freeze(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)));
    private static readonly Geometry Tick = Geometry.Parse("M -4.2 0 L -1.2 3 L 4.2 -2.6");
    private readonly Spring _x = new(0) { Parameters = SpringParameters.Resize };
    private readonly Spring _width = new(0) { Parameters = SpringParameters.Resize };
    private readonly Spring _y = new(0) { Parameters = SpringParameters.Resize };
    private DateOnly _today;
    private bool _selected;
    private bool _lifted;

    public TimelineBar(ProjectTask task, int lane)
    {
        Task = task;
        Lane = lane;
        Focusable = true;
        FocusVisualStyle = null;
        Cursor = Cursors.Hand;
    }

    public ProjectTask Task { get; set; }

    public int Lane { get; set; }

    public double X => _x.Value;

    public double Y => _y.Value;

    /// <summary>The pill's length now (it springs when the days change).</summary>
    public double Length => _width.Value;

    public bool Lifted
    {
        get => _lifted;
        set
        {
            _lifted = value;
            InvalidateVisual();
        }
    }

    public void Snap(double x, double width, double y)
    {
        _x.SnapTo(x);
        _width.SnapTo(width);
        _y.SnapTo(y);
        InvalidateVisual();
    }

    public void MoveTo(double x, double width, double y, bool animate)
    {
        if (!animate)
        {
            Snap(x, width, y);
            return;
        }
        _x.Target = x;
        _width.Target = width;
        _y.Target = y;
    }

    /// <summary>One frame of the landing spring; true while it still moves.</summary>
    public bool Step(double seconds)
    {
        if (_x.IsAtRest && _width.IsAtRest && _y.IsAtRest) return false;
        _x.Step(seconds);
        _width.Step(seconds);
        _y.Step(seconds);
        InvalidateVisual();
        return true;
    }

    public void Update(ProjectTask task, DateOnly today, bool selected)
    {
        Task = task;
        _today = today;
        _selected = selected;
        AutomationProperties.SetName(this, ProjectText.Accessible(task, today));
        ToolTip = task.IsOverdue(today) ? "Overdue" : null;
        InvalidateVisual();
    }

    /// <summary>The 16 px tick circle, 4 px in from the left.</summary>
    public bool IsOnCheck(Point point) => point.X >= 2 && point.X <= 22;

    protected override Size MeasureOverride(Size availableSize) => new(Math.Max(0, Length), Height24);

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        InvalidateVisual();
    }

    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        InvalidateVisual();
    }

    /// <summary>Only the pill takes the pointer, not the name drawn after a short bar.</summary>
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) =>
        new Rect(0, 0, Math.Max(0, Length), Height24).Contains(hitTestParameters.HitPoint) ? new PointHitTestResult(this, hitTestParameters.HitPoint) : null;

    protected override void OnRender(DrawingContext dc)
    {
        var width = Math.Max(Height24, Length);
        var pill = new Rect(0, 0, width, Height24);
        var task = Task;
        var overdue = task.IsOverdue(_today);
        if (_lifted)
        {
            dc.PushTransform(new TranslateTransform(0, -1));
            dc.DrawRoundedRectangle(Shadow, null, new Rect(1, 6, width - 2, Height24), 12, 12);
        }
        var fill = Freeze(new SolidColorBrush(ColorOf(task.Color)));
        dc.DrawRoundedRectangle(fill, null, pill, 12, 12);
        if (IsKeyboardFocused) dc.DrawRoundedRectangle(null, new Pen(FocusRing, 2), Inflate(pill, 1), 13, 13);
        else if (_selected) dc.DrawRoundedRectangle(null, new Pen(Brushes.White, 2), Inflate(pill, 1), 13, 13);
        else if (overdue) dc.DrawRoundedRectangle(null, new Pen(Danger, 1.5), Inflate(pill, -0.75), 11.25, 11.25);
        else dc.DrawRoundedRectangle(null, new Pen(IsMouseOver || _lifted ? RimHover : Rim, 1), Inflate(pill, -0.5), 11.5, 11.5);
        var centre = new Point(12, 12);
        if (task.IsDone)
        {
            dc.DrawEllipse(ProjectTimeline.Accent, null, centre, 8, 8);
            dc.PushTransform(new TranslateTransform(centre.X, centre.Y));
            dc.DrawGeometry(null, new Pen(Brushes.Black, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Tick);
            dc.Pop();
        }
        else dc.DrawEllipse(null, new Pen(CheckRing, 1.5), centre, 7.25, 7.25);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var name = ProjectTimeline.Label(task.Title, 12.5, ProjectTimeline.Medium, task.IsDone ? DoneText : Brushes.White, dpi);
        var outside = width < name.Width + 40;
        if (outside) name.SetForegroundBrush(task.IsDone ? ProjectTimeline.Text2 : ProjectTimeline.Text);
        else
        {
            name.MaxTextWidth = Math.Max(1, width - 26 - 10);
            name.Trimming = TextTrimming.CharacterEllipsis;
            name.MaxLineCount = 1;
        }
        if (task.IsDone) name.SetTextDecorations(TextDecorations.Strikethrough);
        dc.DrawText(name, new Point(outside ? width + 8 : 26, 12 - name.Height / 2));
        // The end grip shows on hover on a bar long enough to hold it.
        if (IsMouseOver && width >= 60 && !outside) dc.DrawRoundedRectangle(RimHover, null, new Rect(width - 8, 7, 3, 10), 1.5, 1.5);
        if (_lifted) dc.Pop();
    }

    private static Rect Inflate(Rect rect, double by)
    {
        rect.Inflate(by, by);
        return rect;
    }

    private static Color ColorOf(int index)
    {
        var rgb = NoteStyle.Colors[NoteStyle.ClampColor(index)].Rgb;
        return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
