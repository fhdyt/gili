using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Alarms;
using Gili.Core.Sound;
using DayFlags = Gili.Core.Time.Days;

namespace Gili.App.Tools;

/// <summary>One day button in the editor (mockup .day).</summary>
public sealed partial class DayToggleViewModel(string name, string fullName, DayFlags day, Action changed) : ObservableObject
{
    public string Name { get; } = name;

    public string FullName { get; } = fullName;

    public DayFlags Day { get; } = day;

    [ObservableProperty] private bool _isOn;

    partial void OnIsOnChanged(bool value) => changed();
}

/// <summary>
/// The alarm editor in Tools (mockup .aedit): time, label, repeat (Once, Weekdays, Every day or any days), the sound
/// with a play button, Cancel and Save. Save stays off until the time reads as a time.
/// </summary>
public sealed partial class AlarmEditorViewModel : ObservableObject
{
    public const int LabelLimit = 40;
    private static readonly string[] Quick = ["Once", "Weekdays", "Every day"];

    private readonly Alarm? _existing;
    private readonly Action<Alarm> _save;
    private readonly Action<string> _preview;
    private bool _settingDays;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimeInvalid))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _timeText;

    [ObservableProperty] private string _label;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QuickRepeat))]
    private DayFlags _repeat;

    [ObservableProperty] private string _sound;

    public AlarmEditorViewModel(Alarm? existing, Action<Alarm> save, Action cancel, Action<string> preview)
    {
        _existing = existing;
        _save = save;
        _preview = preview;
        _timeText = (existing?.Time ?? new TimeOnly(7, 0)).ToString("HH:mm");
        _label = existing?.Label ?? "";
        _sound = existing?.Sound ?? "Radar";
        Days =
        [
            new("Mo", "Monday", DayFlags.Monday, DaysChanged), new("Tu", "Tuesday", DayFlags.Tuesday, DaysChanged),
            new("We", "Wednesday", DayFlags.Wednesday, DaysChanged), new("Th", "Thursday", DayFlags.Thursday, DaysChanged),
            new("Fr", "Friday", DayFlags.Friday, DaysChanged), new("Sa", "Saturday", DayFlags.Saturday, DaysChanged),
            new("Su", "Sunday", DayFlags.Sunday, DaysChanged),
        ];
        _repeat = existing?.Repeat ?? DayFlags.None;
        OnRepeatChanged(_repeat);
        CancelCommand = new RelayCommand(cancel);
        PlayCommand = new RelayCommand(() => _preview(Sound));
    }

    public string Title => _existing is null ? "New alarm" : "Edit alarm";

    public long? EditingId => _existing?.Id;

    public IReadOnlyList<DayToggleViewModel> Days { get; }

    public IReadOnlyList<string> QuickChoices => Quick;

    public IReadOnlyList<string> Sounds { get; } = [.. Voices.All.Select(v => v.Name)];

    public bool IsTimeInvalid => AlarmText.ParseTime(TimeText) is null;

    /// <summary>The segment that matches the chosen days; none when they are some other set.</summary>
    public string? QuickRepeat
    {
        get => Repeat switch
        {
            DayFlags.None => Quick[0],
            DayFlags.Weekdays => Quick[1],
            DayFlags.Every => Quick[2],
            _ => null,
        };
        set
        {
            if (value is null) return;
            Repeat = value == Quick[1] ? DayFlags.Weekdays : value == Quick[2] ? DayFlags.Every : DayFlags.None;
        }
    }

    public IRelayCommand CancelCommand { get; }

    public IRelayCommand PlayCommand { get; }

    partial void OnRepeatChanged(DayFlags value)
    {
        _settingDays = true;
        foreach (var day in Days) day.IsOn = value.HasFlag(day.Day);
        _settingDays = false;
    }

    /// <summary>Picking a sound plays it once, as in Settings (PRD 6.6).</summary>
    partial void OnSoundChanged(string value) => _preview(value);

    private void DaysChanged()
    {
        if (_settingDays) return;
        Repeat = Days.Where(d => d.IsOn).Aggregate(DayFlags.None, (all, d) => all | d.Day);
    }

    private bool CanSave() => !IsTimeInvalid;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var label = Label.Trim();
        if (label.Length > LabelLimit) label = label[..LabelLimit];
        _save(new Alarm(_existing?.Id ?? 0, AlarmText.ParseTime(TimeText)!.Value, Repeat, label, Sound, true, null));
    }
}
