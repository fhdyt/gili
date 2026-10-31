using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Gili.App.Tools;

/// <summary>The Todo tab's content (F-TOOL-06).</summary>
public partial class TodoTab : UserControl
{
    public TodoTab()
    {
        InitializeComponent();
        _ = new TodoListDrag(List, () => DataContext as TodoTabViewModel);
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= OnChanged;
            if (e.NewValue is INotifyPropertyChanged now) now.PropertyChanged += OnChanged;
        };
    }

    /// <summary>An opened editor puts the caret in its title, selected, so typing replaces it.</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TodoTabViewModel.Editor) || (DataContext as TodoTabViewModel)?.Editor is null) return;
        Dispatcher.InvokeAsync(() =>
        {
            TitleBox.Focus();
            TitleBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
