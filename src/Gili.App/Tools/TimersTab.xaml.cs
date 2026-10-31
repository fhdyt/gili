using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Gili.App.Tools;

/// <summary>The Timer tab's content (F-TOOL-01).</summary>
public partial class TimersTab : UserControl
{
    public TimersTab()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged old) old.PropertyChanged -= OnChanged;
            if (e.NewValue is INotifyPropertyChanged now) now.PropertyChanged += OnChanged;
        };
    }

    /// <summary>An opened editor puts the caret in its label, selected, so typing replaces it.</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimersTabViewModel.Editor) || (DataContext as TimersTabViewModel)?.Editor is null) return;
        Dispatcher.InvokeAsync(() =>
        {
            LabelBox.Focus();
            LabelBox.SelectAll();
        }, DispatcherPriority.Input);
    }
}
