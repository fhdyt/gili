using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gili.App.Tools;

/// <summary>
/// The two-click Delete of a Tools row: the first click turns the text into "Click again to delete" for 3 s, the second
/// runs the delete. Shared by the alarm, timer and todo rows.
/// </summary>
public sealed partial class DeleteConfirm : ObservableObject
{
    private readonly DispatcherTimer _confirm = new() { Interval = TimeSpan.FromSeconds(3) };

    [ObservableProperty] private string _text = "Delete";

    public DeleteConfirm(Action delete)
    {
        Command = new RelayCommand(() =>
        {
            if (_confirm.IsEnabled)
            {
                Reset();
                delete();
                return;
            }
            Text = "Click again to delete";
            _confirm.Start();
        });
        _confirm.Tick += (_, _) => Reset();
    }

    public IRelayCommand Command { get; }

    public void Reset()
    {
        _confirm.Stop();
        Text = "Delete";
    }
}
