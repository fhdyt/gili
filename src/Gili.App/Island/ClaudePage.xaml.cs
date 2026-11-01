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

    /// <summary>Each reset form opens with the caret in its time, the text selected to type over (revisions 18, 19).</summary>
    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        var field = e.PropertyName switch
        {
            nameof(ClaudePageViewModel.IsEditingReset) when _vm.IsEditingReset => ResetTime,
            nameof(ClaudePageViewModel.IsEditingWeekly) when _vm.IsEditingWeekly => WeeklyTime,
            _ => null,
        };
        if (field is null) return;
        Dispatcher.InvokeAsync(() =>
        {
            field.Focus();
            field.SelectAll();
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

    /// <summary>The weekly form, the same way: Enter saves, Esc and a click elsewhere cancel, Up and Down step the day (revision 19).</summary>
    private void OnWeeklyKey(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down)
        {
            var step = e.Key == Key.Up ? _vm.NextDayCommand : _vm.PreviousDayCommand;
            if (step.CanExecute(null)) step.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            _vm.SaveWeekly();
            if (_vm.IsEditingWeekly) WeeklyTime.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm.CancelWeeklyCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnWeeklyFocus(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false && _vm.IsEditingWeekly) _vm.CancelWeeklyCommand.Execute(null);
    }
}
