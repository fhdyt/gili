using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Gili.App.Island;

/// <summary>
/// The Projects page (F-ISL-34): wires the timeline's drags and clicks, the form's keys and the keyboard focus to
/// <see cref="ProjectsViewModel"/>. Esc and horizontal swipes reach it from <see cref="IslandWindow"/>.
/// </summary>
public partial class ProjectsPage : UserControl
{
    private readonly ProjectsViewModel _vm;

    public ProjectsPage(ProjectsViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        foreach (var color in vm.Colors)
        {
            var rgb = color.Rgb;
            var fill = new SolidColorBrush(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));
            fill.Freeze();
            var swatch = new ToggleButton
            {
                Style = (Style)Resources["Swatch"],
                Background = fill,
                ToolTip = color.Name,
                Command = vm.PickColorCommand,
                CommandParameter = color,
            };
            System.Windows.Automation.AutomationProperties.SetName(swatch, color.Label);
            swatch.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(ProjectColorItem.IsPicked)) { Source = color, Mode = BindingMode.OneWay });
            Swatches.Children.Add(swatch);
        }
        Timeline.Moved += vm.Move;
        Timeline.Opened += vm.Open;
        Timeline.Toggled += vm.Toggle;
        Timeline.AddRequested += vm.OpenNew;
        foreach (var field in new[] { TaskTitle, TaskStart, TaskEnd }) field.PreviewKeyDown += OnFormKey;
        vm.FocusRequested += Focus;
        PreviewMouseDown += (_, _) =>
        {
            // A click anywhere but the list or its button closes the list, as clicking outside a menu does.
            if (vm.IsMenuOpen && !Menu.IsMouseOver && !SelectButton.IsMouseOver) vm.CloseMenu();
        };
    }

    /// <summary>Esc on this page: closes what is open here; false when nothing was, so the island closes.</summary>
    public bool Escape() => _vm.Escape();

    /// <summary>A horizontal swipe or wheel tilt over the timeline scrolls its days rather than the pages.</summary>
    public bool ScrollTimeline(int delta)
    {
        if (!Timeline.IsMouseOver) return false;
        Timeline.ScrollBy(delta * 0.75);
        return true;
    }

    private void OnPreviousWeek(object sender, RoutedEventArgs e) => Timeline.ScrollDays(-7);

    private void OnNextWeek(object sender, RoutedEventArgs e) => Timeline.ScrollDays(7);

    private void OnToday(object sender, RoutedEventArgs e) => Timeline.ScrollToToday();

    /// <summary>↑ and ↓ in a date field move it a day.</summary>
    private void OnDateKey(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down)) return;
        _vm.StepDate(ReferenceEquals(sender, TaskEnd), e.Key == Key.Up ? 1 : -1);
        ((TextBox)sender).CaretIndex = ((TextBox)sender).Text.Length;
        e.Handled = true;
    }

    /// <summary>Enter in any field of the form saves it.</summary>
    private void OnFormKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _vm.SaveFormCommand.Execute(null);
        e.Handled = true;
    }

    /// <summary>After the field is shown (its visibility binding applies first), so the focus lands.</summary>
    private void Focus(string target) => Dispatcher.BeginInvoke(() =>
    {
        switch (target)
        {
            case "title":
                TaskTitle.Focus();
                TaskTitle.SelectAll();
                break;
            case "start":
                TaskStart.Focus();
                break;
            case "end":
                TaskEnd.Focus();
                break;
            case "first":
                FirstName.Focus();
                break;
            case "newProject":
                NewProjectName.Focus();
                break;
            case "select":
                SelectButton.Focus();
                break;
            case "bar":
                if (_vm.FocusTarget is { } id) Timeline.FocusBar(id);
                break;
            case "rename":
                if (Find<TextBox>(Menu, box => (box.Tag as string) == "Rename" && box.IsVisible) is { } rename)
                {
                    rename.Focus();
                    rename.SelectAll();
                }
                break;
        }
    }, DispatcherPriority.Loaded);

    private static T? Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T found && match(found)) return found;
            if (Find(child, match) is { } deeper) return deeper;
        }
        return null;
    }
}
