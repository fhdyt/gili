using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Gili.App.Island;

public partial class ClaudePage : UserControl
{
    private readonly ClaudePageViewModel _vm;

    public ClaudePage(ClaudePageViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        vm.PropertyChanged += OnChanged;
        Unloaded += (_, _) => vm.PropertyChanged -= OnChanged;
    }

    /// <summary>The reset form opens with the caret in the time, its text selected to type over (revision 18).</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ClaudePageViewModel.IsEditingReset) || !_vm.IsEditingReset) return;
        Dispatcher.InvokeAsync(() =>
        {
            ResetTime.Focus();
            ResetTime.SelectAll();
        }, DispatcherPriority.Input);
    }

    /// <summary>Enter saves (a time that can't be keeps the caret in it), Esc cancels without closing the island.</summary>
    private void OnResetKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.SaveReset();
            if (_vm.IsEditingReset) ResetTime.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm.CancelResetCommand.Execute(null);
            e.Handled = true;
        }
    }

    /// <summary>A click anywhere else cancels, like Esc.</summary>
    private void OnResetFocus(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && _vm.IsEditingReset) _vm.CancelResetCommand.Execute(null);
    }
}
