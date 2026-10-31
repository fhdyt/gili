using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Gili.App.Controls;
using Gili.Platform.Shell;

namespace Gili.App.Settings;

/// <summary>The Settings window as in the mockup (.setw): its own title bar, sections on the left, rows on the right.</summary>
public partial class SettingsWindow : Window
{
    private static readonly SolidColorBrush Lifted = Freeze(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x2E)));

    private readonly SettingsViewModel _vm;
    private Drag? _drag;

    public SettingsWindow(SettingsViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        // Stays inside a small screen's work area (1366 × 768 at 150 %); the page scrolls instead.
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 32);
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => (Nav.ItemContainerGenerator.ContainerFromItem(Nav.SelectedItem) as UIElement)?.Focus();
        Closed += (_, _) => vm.Detach();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowStyles.RoundCorners(new WindowInteropHelper(this).Handle);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }
        // Alt + Up/Down on a system module's switch moves its row (PRD 6.4); handled, so Alt never opens the window menu.
        if (e.Key == Key.System && e.SystemKey is Key.Up or Key.Down
            && (e.OriginalSource as FrameworkElement)?.DataContext is SettingRowViewModel { CanReorder: true } row)
        {
            e.Handled = true;
            var label = row.Label;
            _vm.Move(row, e.SystemKey == Key.Up ? -1 : 1);
            Dispatcher.InvokeAsync(() => FocusSwitch(label), DispatcherPriority.Loaded);
        }
    }

    /// <summary>The rows are listed again after a move, so the focus goes back to the moved row's switch.</summary>
    private void FocusSwitch(string label)
    {
        foreach (var row in _vm.Rows.Where(r => r.Label == label))
        {
            if (RowList.ItemContainerGenerator.ContainerFromItem(row) is not DependencyObject container) continue;
            FindSwitch(container)?.Focus();
        }
    }

    private static ToggleButton? FindSwitch(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ToggleButton { IsVisible: true } toggle) return toggle;
            if (FindSwitch(child) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// Dragging a grip lifts its row and slides the other module rows aside (mockup <c>.srow.mv.lift</c>); the order is
    /// saved once, when the row is let go (plan 3c decision 4).
    /// </summary>
    private void OnGripDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SettingRowViewModel row } grip) return;
        var rows = ModuleRows();
        var from = rows.FindIndex(r => r.Row == row);
        if (from < 0) return;
        var lifted = rows[from].Container;
        var drag = _drag = new Drag(row, rows, from, e.GetPosition(RowList).Y, lifted.ActualHeight + 4);
        Panel.SetZIndex(lifted, 1);
        lifted.RenderTransform = new TranslateTransform();
        if (FindRowBorder(lifted) is { } border)
        {
            drag.Background = border.Background;
            border.Background = Lifted;
        }
        grip.CaptureMouse();
        e.Handled = true;
    }

    private void OnGripMove(object sender, MouseEventArgs e)
    {
        if (_drag is not { } drag) return;
        var dy = e.GetPosition(RowList).Y - drag.StartY;
        ((TranslateTransform)drag.Rows[drag.From].Container.RenderTransform).Y = dy;
        var to = Math.Clamp(drag.From + (int)Math.Round(dy / drag.Step), 0, drag.Rows.Count - 1);
        if (to == drag.To) return;
        drag.To = to;
        for (var i = 0; i < drag.Rows.Count; i++)
        {
            if (i == drag.From) continue;
            var shift = i > drag.From && i <= to ? -drag.Step : i < drag.From && i >= to ? drag.Step : 0;
            if (drag.Rows[i].Container.RenderTransform is not TranslateTransform move)
            {
                move = new TranslateTransform();
                drag.Rows[i].Container.RenderTransform = move;
            }
            var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromMilliseconds(200) : TimeSpan.Zero;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(shift, duration) { EasingFunction = BezierEase.Out });
        }
    }

    private void OnGripUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement grip) grip.ReleaseMouseCapture();
    }

    /// <summary>Letting go, or losing the mouse to another window, ends the drag where the row was let go.</summary>
    private void OnGripLost(object sender, MouseEventArgs e)
    {
        if (_drag is not { } drag) return;
        _drag = null;
        foreach (var (_, container) in drag.Rows)
        {
            container.RenderTransform = Transform.Identity;
            Panel.SetZIndex(container, 0);
            if (container == drag.Rows[drag.From].Container && FindRowBorder(container) is { } border) border.Background = drag.Background;
        }
        if (drag.To != drag.From) _vm.MoveTo(drag.Row, drag.To);
    }

    /// <summary>The system module rows, in the order they are listed, with their containers.</summary>
    private List<(SettingRowViewModel Row, FrameworkElement Container)> ModuleRows() =>
        [.. _vm.Rows.Where(r => r.CanReorder)
            .Select(r => (Row: r, Container: RowList.ItemContainerGenerator.ContainerFromItem(r) as FrameworkElement))
            .Where(r => r.Container is not null)
            .Select(r => (r.Row, r.Container!))];

    private static Border? FindRowBorder(DependencyObject container) =>
        VisualTreeHelper.GetChildrenCount(container) > 0 ? VisualTreeHelper.GetChild(container, 0) as Border : null;

    private static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }

    private sealed class Drag(SettingRowViewModel row, List<(SettingRowViewModel Row, FrameworkElement Container)> rows, int from, double startY, double step)
    {
        public SettingRowViewModel Row { get; } = row;
        public List<(SettingRowViewModel Row, FrameworkElement Container)> Rows { get; } = rows;
        public int From { get; } = from;
        public double StartY { get; } = startY;
        public double Step { get; } = step;
        public int To { get; set; } = from;
        public Brush? Background { get; set; }
    }
}
