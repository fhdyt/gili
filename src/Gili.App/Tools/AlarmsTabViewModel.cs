using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.App.Island;

namespace Gili.App.Tools;

/// <summary>An alarm in Tools: the page's row plus Edit and a Delete that asks for a second click within 3 s.</summary>
public sealed partial class ToolAlarmRowViewModel : AlarmRowViewModel
{
    public ToolAlarmRowViewModel(long id, AlarmService alarms, Action<long> edit) : base(id, alarms.SetEnabled)
    {
        EditCommand = new RelayCommand(() => edit(Id));
        Delete = new DeleteConfirm(() => alarms.Delete(Id));
    }

    public IRelayCommand EditCommand { get; }

    public DeleteConfirm Delete { get; }

    public void Reset() => Delete.Reset();
}

/// <summary>The Alarms tab in Tools (F-TOOL-05): New alarm, the list, and the editor above it while open.</summary>
public sealed partial class AlarmsTabViewModel : ObservableObject
{
    private readonly AlarmService _alarms;
    private readonly Func<bool> _use24Hour;
    private readonly Action<string> _preview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditor))]
    [NotifyCanExecuteChangedFor(nameof(NewCommand))]
    private AlarmEditorViewModel? _editor;

    public AlarmsTabViewModel(AlarmService alarms, Func<bool> use24Hour, Action<string> preview)
    {
        _alarms = alarms;
        _use24Hour = use24Hour;
        _preview = preview;
        alarms.Changed += Refresh;
        Refresh();
    }

    public ObservableCollection<ToolAlarmRowViewModel> Rows { get; } = [];

    public bool HasEditor => Editor is not null;

    public bool IsEmpty => Rows.Count == 0;

    public void Detach()
    {
        _alarms.Changed -= Refresh;
        foreach (var row in Rows) row.Reset();
    }

    private bool CanAdd() => Editor is null;

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void New() => Editor = new AlarmEditorViewModel(null, alarm =>
    {
        Editor = null;
        _alarms.Add(alarm);
    }, () => Editor = null, _preview);

    private void Edit(long id)
    {
        if (_alarms.Alarms.FirstOrDefault(a => a.Id == id) is not { } existing) return;
        Editor = new AlarmEditorViewModel(existing, alarm =>
        {
            Editor = null;
            _alarms.Update(alarm);
        }, () => Editor = null, _preview);
    }

    private void Refresh()
    {
        var alarms = _alarms.Alarms;
        if (!Rows.Select(r => r.Id).SequenceEqual(alarms.Select(a => a.Id)))
        {
            foreach (var row in Rows) row.Reset();
            Rows.Clear();
            foreach (var alarm in alarms) Rows.Add(new ToolAlarmRowViewModel(alarm.Id, _alarms, Edit));
            OnPropertyChanged(nameof(IsEmpty));
        }
        var use24 = _use24Hour();
        for (var i = 0; i < alarms.Count; i++) Rows[i].Show(alarms[i], use24);
        // An alarm deleted while it was being edited closes its editor.
        if (Editor?.EditingId is { } editing && alarms.All(a => a.Id != editing)) Editor = null;
    }
}
