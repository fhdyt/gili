using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Alarms;

namespace Gili.App.Island;

/// <summary>One alarm on the Alarms page and in Tools: time, label and detail, and its switch (mockup .arow, .srow).</summary>
public partial class AlarmRowViewModel(long id, Action<long, bool> setEnabled) : ObservableObject
{
    // Set while an alarm is copied in, so only the user's flip reaches the service.
    private bool _loading;

    public long Id { get; } = id;

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private bool _isOn;

    partial void OnIsOnChanged(bool value)
    {
        if (!_loading) setEnabled(Id, value);
    }

    /// <summary>Copies an alarm in without reaching the service.</summary>
    public void Show(Alarm alarm, bool use24Hour)
    {
        _loading = true;
        Time = AlarmText.Time(alarm.Time, use24Hour);
        Label = AlarmText.Label(alarm);
        Detail = AlarmText.RowDetail(alarm);
        IsOn = alarm.Enabled;
        _loading = false;
    }
}

/// <summary>The Alarms page (PRD 6.3 page 3): the next alarm in large type and every alarm with its switch.</summary>
public sealed partial class AlarmsViewModel : ObservableObject
{
    private readonly AlarmService _alarms;
    private readonly Func<bool> _use24Hour;

    [ObservableProperty] private string _big = "";
    [ObservableProperty] private string _sub = "";

    public AlarmsViewModel(AlarmService alarms, Func<bool> use24Hour, Action manage)
    {
        _alarms = alarms;
        _use24Hour = use24Hour;
        ManageCommand = new RelayCommand(manage);
        alarms.Changed += Refresh;
        Refresh();
    }

    public ObservableCollection<AlarmRowViewModel> Rows { get; } = [];

    public IRelayCommand ManageCommand { get; }

    /// <summary>Copies the alarms in; rows stay the same objects while the list keeps its order, so switches animate.</summary>
    public void Refresh()
    {
        var use24 = _use24Hour();
        (Big, Sub) = AlarmText.NextHeading(_alarms.Next, DateTimeOffset.Now, TimeZoneInfo.Local, use24);
        var alarms = _alarms.Alarms;
        if (!Rows.Select(r => r.Id).SequenceEqual(alarms.Select(a => a.Id)))
        {
            Rows.Clear();
            foreach (var alarm in alarms) Rows.Add(new AlarmRowViewModel(alarm.Id, _alarms.SetEnabled));
        }
        for (var i = 0; i < alarms.Count; i++) Rows[i].Show(alarms[i], use24);
    }
}
