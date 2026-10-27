using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Gili.App.Island;

public partial class CalendarPage : UserControl
{
    private readonly CalendarViewModel _vm;

    public CalendarPage(CalendarViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        vm.PropertyChanged += OnChanged;
    }

    /// <summary>Add task puts the caret in the title (mockup: $('#cAddIn').focus()).</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CalendarViewModel.IsAdding) || !_vm.IsAdding) return;
        Dispatcher.InvokeAsync(() => AddTitle.Focus(), DispatcherPriority.Input);
    }

    /// <summary>Enter saves (a blank title keeps the caret in it), Esc cancels without closing the island, Tab moves to the time.</summary>
    private void OnAddKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (!_vm.Save()) AddTitle.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm.CancelAddCommand.Execute(null);
            e.Handled = true;
        }
    }
}
