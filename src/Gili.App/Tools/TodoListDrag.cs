using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Gili.App.Controls;

namespace Gili.App.Tools;

/// <summary>
/// Drag to reorder and Alt + arrow on the Todo tab (mockup tdDrag*, tdMove). Press and move 6 px to lift a row (1.02 with a
/// shadow); it follows the pointer, the rows it passes slide aside (200 ms) and on release it lands (200 ms) and the
/// tab is told once. With "Animation effects" off nothing lifts or slides and the drop is decided on release.
/// </summary>
internal sealed class TodoListDrag
{
    private const double LiftDistance = 6;
    private const int Milliseconds = 200;

    private readonly ItemsControl _list;
    private readonly Func<TodoTabViewModel?> _model;
    private Drag? _press;
    private bool _landing;

    public TodoListDrag(ItemsControl list, Func<TodoTabViewModel?> model)
    {
        _list = list;
        _model = model;
        list.PreviewMouseLeftButtonDown += OnDown;
        list.PreviewMouseMove += OnMove;
        list.PreviewMouseLeftButtonUp += OnUp;
        list.LostMouseCapture += OnLostCapture;
        list.PreviewKeyDown += OnKey;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        // A press that never saw its release (let go outside the list) must not outlive the next one.
        _press = null;
        if (_landing || _model() is not { CanReorder: true, HasEditor: false } model || e.ChangedButton != MouseButton.Left) return;
        var source = e.OriginalSource as DependencyObject;
        for (var d = source; d is not null && d != _list; d = Parent(d))
            if (d is ButtonBase) return;
        if (source is null || ItemsControl.ContainerFromElement(_list, source) is not FrameworkElement container) return;
        var from = _list.ItemContainerGenerator.IndexFromContainer(container);
        if (from < 0) return;
        Root(container)?.Focus();
        var containers = new List<FrameworkElement>();
        for (var i = 0; i < model.Rows.Count; i++)
            if (_list.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement c) containers.Add(c);
        if (containers.Count != model.Rows.Count) return;
        _press = new Drag(model, containers, from, model.Rows[from].Id, e.GetPosition(_list).Y, container.ActualHeight);
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_press is not { } drag) return;
        if (e.LeftButton != MouseButtonState.Pressed || !drag.Model.CanReorder || drag.Model.HasEditor)
        {
            // The button came up unseen, or the list changed under the press: drop it.
            _press = null;
            if (drag.Lifted) Land(drag, commit: false);
            return;
        }
        var dy = e.GetPosition(_list).Y - drag.StartY;
        if (!drag.Lifted)
        {
            if (Math.Abs(dy) < LiftDistance) return;
            drag.Lifted = true;
            // Read once: a setting flipped mid-drag must not change which transforms exist.
            drag.Animated = SystemParameters.ClientAreaAnimation;
            if (drag.Animated)
            {
                drag.Attach();
                Lift(drag);
            }
            // Last: taking the capture replays the pointer position into this handler.
            _list.CaptureMouse();
        }
        drag.To = Target(drag, dy);
        if (!drag.Animated) return;
        drag.Move.Y = dy;
        for (var k = 0; k < drag.Containers.Count; k++)
        {
            if (k == drag.From) continue;
            var shift = drag.From < drag.To && k > drag.From && k <= drag.To ? -drag.Height
                : drag.From > drag.To && k < drag.From && k >= drag.To ? drag.Height : 0;
            Slide(drag, k, shift);
        }
        e.Handled = true;
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_press is not { } drag) return;
        _press = null;
        if (!drag.Lifted) return;
        e.Handled = true;
        drag.To = Target(drag, e.GetPosition(_list).Y - drag.StartY);
        Land(drag, commit: true);
    }

    /// <summary>The pointer was taken away (another window, Esc by the system): the row goes back.</summary>
    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        if (e.OriginalSource != _list || _press is not { Lifted: true } drag || _landing) return;
        _press = null;
        Land(drag, commit: false);
    }

    private void Land(Drag drag, bool commit)
    {
        _list.ReleaseMouseCapture();
        var rows = drag.Model.Rows;
        // The list may have changed since the press; then nothing is committed.
        var same = drag.From < rows.Count && rows[drag.From].Id == drag.Id && rows.Count == drag.Containers.Count;
        var moves = commit && same && drag.To != drag.From && drag.To < rows.Count;
        var id = drag.Id;
        var target = moves ? rows[drag.To].Id : id;
        void Done()
        {
            _landing = false;
            drag.Clear();
            if (moves) drag.Model.Drop(id, target);
            if (same) FocusRow(drag.Model, id);
        }
        if (!drag.Animated)
        {
            Done();
            return;
        }
        _landing = true;
        // The other rows keep their offsets until the list changes; the dragged one settles into its slot.
        if (!moves) for (var k = 0; k < drag.Shifts.Count; k++) Slide(drag, k, 0);
        if (same) rows[drag.From].IsLifted = false;
        var slot = moves ? (drag.To - drag.From) * drag.Height : 0;
        var land = BezierEase.To(slot, Milliseconds, BezierEase.Out);
        land.Completed += (_, _) => Done();
        drag.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, BezierEase.To(1, Milliseconds, BezierEase.Out));
        drag.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, BezierEase.To(1, Milliseconds, BezierEase.Out));
        drag.Move.BeginAnimation(TranslateTransform.YProperty, land);
    }

    private static void Lift(Drag drag)
    {
        var row = drag.Containers[drag.From];
        drag.Model.Rows[drag.From].IsLifted = true;
        Panel.SetZIndex(row, 2);
        row.Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 8, Direction = 270, Opacity = 0.5, Color = Colors.Black };
        drag.Scale.ScaleX = drag.Scale.ScaleY = 1.02;
    }

    /// <summary>Row <paramref name="k"/> slides to a new offset; asking for the one it is already heading to does nothing.</summary>
    private static void Slide(Drag drag, int k, double to)
    {
        if (drag.ShiftTargets[k] == to) return;
        drag.ShiftTargets[k] = to;
        drag.Shifts[k].BeginAnimation(TranslateTransform.YProperty, BezierEase.To(to, Milliseconds, BezierEase.Out));
    }

    private static int Target(Drag drag, double dy) =>
        Math.Clamp((int)Math.Round(drag.From + dy / drag.Height, MidpointRounding.AwayFromZero), 0, drag.Containers.Count - 1);

    /// <summary>Alt + up/down on a focused row moves it one place; the row keeps the focus after the list changes.</summary>
    private void OnKey(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) == 0 || key is not (Key.Up or Key.Down)) return;
        if (_model() is not { CanReorder: true, HasEditor: false } model || _press is not null || _landing) return;
        if (Keyboard.FocusedElement is not DependencyObject focused || ItemsControl.ContainerFromElement(_list, focused) is not { } container) return;
        var index = _list.ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0 || index >= model.Rows.Count) return;
        e.Handled = true;
        var id = model.Rows[index].Id;
        if (!model.Move(id, key == Key.Up ? -1 : 1)) return;
        FocusRow(model, id);
    }

    /// <summary>Puts the keyboard focus back on a task's row once the list has settled.</summary>
    private void FocusRow(TodoTabViewModel model, long id) =>
        _list.Dispatcher.InvokeAsync(() =>
        {
            for (var i = 0; i < model.Rows.Count; i++)
            {
                if (model.Rows[i].Id != id || _list.ItemContainerGenerator.ContainerFromIndex(i) is not { } moved) continue;
                Root(moved)?.Focus();
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);

    private static DependencyObject? Parent(DependencyObject d) =>
        d is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);

    private static FrameworkElement? Root(DependencyObject container) =>
        VisualTreeHelper.GetChildrenCount(container) > 0 ? VisualTreeHelper.GetChild(container, 0) as FrameworkElement : null;

    /// <summary>One press on a row, and the offsets its rows carry while it is dragged.</summary>
    private sealed class Drag
    {
        public Drag(TodoTabViewModel model, List<FrameworkElement> containers, int from, long id, double startY, double height)
        {
            Model = model;
            Containers = containers;
            From = To = from;
            Id = id;
            StartY = startY;
            Height = height;
        }

        /// <summary>Gives each row its offset: the dragged one grows and follows the pointer, the others slide.</summary>
        public void Attach()
        {
            for (var k = 0; k < Containers.Count; k++)
            {
                var shift = new TranslateTransform();
                Shifts.Add(shift);
                ShiftTargets.Add(0);
                Containers[k].RenderTransformOrigin = new Point(0.5, 0.5);
                if (k == From)
                {
                    var group = new TransformGroup();
                    group.Children.Add(Scale);
                    group.Children.Add(Move);
                    Containers[k].RenderTransform = group;
                }
                else Containers[k].RenderTransform = shift;
            }
        }

        public TodoTabViewModel Model { get; }

        public List<FrameworkElement> Containers { get; }

        public List<TranslateTransform> Shifts { get; } = [];

        public List<double> ShiftTargets { get; } = [];

        public ScaleTransform Scale { get; } = new(1, 1);

        public TranslateTransform Move { get; } = new();

        public int From { get; }

        /// <summary>The pressed task, to be sure the list still holds it at that place when it lands.</summary>
        public long Id { get; }

        /// <summary>Whether this drag lifts and slides, decided once when it lifts.</summary>
        public bool Animated { get; set; }

        public int To { get; set; }

        public double StartY { get; }

        public double Height { get; }

        public bool Lifted { get; set; }

        /// <summary>Takes every offset, shadow and lift away; the list is about to change or stay as it was.</summary>
        public void Clear()
        {
            foreach (var row in Containers)
            {
                row.RenderTransform = null;
                row.Effect = null;
                Panel.SetZIndex(row, 0);
            }
            if (From < Model.Rows.Count && Model.Rows[From].Id == Id) Model.Rows[From].IsLifted = false;
        }
    }
}
