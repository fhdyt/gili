using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.App.Island;
using Gili.Core.Timers;

namespace Gili.App.Tools;

/// <summary>A labeled timer in Tools: its time, Start / Pause / Resume, Reset, Edit and a Delete that asks for a second click within 3 s.</summary>
public sealed partial class ToolTimerRowViewModel : ObservableObject
{
    private readonly DispatcherTimer _confirm = new() { Interval = TimeSpan.FromSeconds(3) };

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _buttonText = "Start";
    [ObservableProperty] private string _deleteText = "Delete";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private bool _canReset;

    public ToolTimerRowViewModel(long id, TimerService timers, Action<long> edit)
    {
        Id = id;
        StartCommand = new RelayCommand(() => timers.StartOrPause(Id));
        ResetCommand = new RelayCommand(() => timers.ResetTimer(Id), () => CanReset);
        EditCommand = new RelayCommand(() => edit(Id));
        DeleteCommand = new RelayCommand(() =>
        {
            if (_confirm.IsEnabled)
            {
                Reset();
                timers.DeleteTimer(Id);
                return;
            }
            DeleteText = "Click again to delete";
            _confirm.Start();
        });
        _confirm.Tick += (_, _) => Reset();
    }

    public long Id { get; }

    public IRelayCommand StartCommand { get; }

    public IRelayCommand ResetCommand { get; }

    public IRelayCommand EditCommand { get; }

    public IRelayCommand DeleteCommand { get; }

    public void Show(LabeledTimer timer, DateTimeOffset now)
    {
        var clock = timer.Clock;
        Time = TimerText.Long(clock.Remaining(now));
        Label = timer.Label;
        Detail = TimerText.RowDetail(clock);
        ButtonText = TimerText.Button(clock.IsRunning, clock.IsPaused);
        CanReset = clock.IsRunning || clock.IsPaused;
    }

    public void Reset()
    {
        _confirm.Stop();
        DeleteText = "Delete";
    }
}

/// <summary>The Timer tab in Tools (F-TOOL-01): New timer, the list, and the editor above it while open. Follows <c>TimerService.Changed</c>.</summary>
public sealed partial class TimersTabViewModel : ObservableObject
{
    private readonly TimerService _timers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditor))]
    [NotifyCanExecuteChangedFor(nameof(NewCommand))]
    private TimerEditorViewModel? _editor;

    public TimersTabViewModel(TimerService timers)
    {
        _timers = timers;
        timers.Changed += Refresh;
        Refresh();
    }

    public ObservableCollection<ToolTimerRowViewModel> Rows { get; } = [];

    public bool HasEditor => Editor is not null;

    public bool IsEmpty => Rows.Count == 0;

    public void Detach()
    {
        _timers.Changed -= Refresh;
        foreach (var row in Rows) row.Reset();
    }

    private bool CanAdd() => Editor is null && _timers.CanAddTimer;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void New() => Editor = new TimerEditorViewModel(null, (label, seconds) =>
    {
        Editor = null;
        _timers.AddTimer(label, seconds);
    }, () => Editor = null);

    private void Edit(long id)
    {
        if (_timers.Labeled.FirstOrDefault(t => t.Id == id) is not { } existing) return;
        Editor = new TimerEditorViewModel(existing, (label, seconds) =>
        {
            Editor = null;
            _timers.EditTimer(id, label, seconds);
        }, () => Editor = null);
    }

    private void Refresh()
    {
        var timers = _timers.Labeled;
        if (!Rows.Select(r => r.Id).SequenceEqual(timers.Select(t => t.Id)))
        {
            foreach (var row in Rows) row.Reset();
            Rows.Clear();
            foreach (var timer in timers) Rows.Add(new ToolTimerRowViewModel(timer.Id, _timers, Edit));
            OnPropertyChanged(nameof(IsEmpty));
        }
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < timers.Count; i++) Rows[i].Show(timers[i], now);
        // A timer deleted while it was being edited closes its editor.
        if (Editor?.EditingId is { } editing && timers.All(t => t.Id != editing)) Editor = null;
        NewCommand.NotifyCanExecuteChanged();
    }
}
