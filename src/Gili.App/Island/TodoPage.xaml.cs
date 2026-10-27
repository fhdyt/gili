using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Gili.App.Island;

public partial class TodoPage : UserControl
{
    public TodoPage(TodoViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    /// <summary>A focused row: Enter edits it, Delete twice deletes it (PRD 6.3 "Mengubah dan menghapus todo").</summary>
    private void OnRowKey(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox || sender is not FrameworkElement { DataContext: TodoRowViewModel row } || row.IsEditing) return;
        if (e.Key == Key.Enter && ReferenceEquals(e.OriginalSource, sender)) row.EditCommand.Execute(null);
        else if (e.Key == Key.Delete) row.DeleteCommand.Execute(null);
        else return;
        e.Handled = true;
    }

    /// <summary>Enter or the check saves, Esc cancels without closing the island.</summary>
    private void OnEditKey(object sender, KeyEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: TodoRowViewModel row }) return;
        if (e.Key == Key.Enter) row.SaveCommand.Execute(null);
        else if (e.Key == Key.Escape) row.CancelCommand.Execute(null);
        else return;
        e.Handled = true;
    }

    /// <summary>Opening a row for editing puts the caret in its title, selected (mockup editTodo).</summary>
    private void OnEditFieldLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;
        box.IsVisibleChanged += (_, _) =>
        {
            if (!box.IsVisible) return;
            box.Dispatcher.InvokeAsync(() =>
            {
                box.Focus();
                box.SelectAll();
            }, DispatcherPriority.Input);
        };
    }
}
