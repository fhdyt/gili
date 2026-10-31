using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Alarms;
using Gili.Core.Todos;

namespace Gili.App.Tools;

/// <summary>
/// The task editor in Tools (mockup .aedit.lg): title, date and time, Cancel and Save. Save stays off until the title is not
/// empty and the date and time read as one; a time already gone today gets a yellow edge but can still be saved.
/// </summary>
public sealed partial class TodoEditorViewModel : ObservableObject
{
    private readonly Action<string, DateOnly?, TimeOnly?> _save;
    private readonly Func<DateOnly> _today;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _titleText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDateInvalid), nameof(IsTimePast), nameof(TimeEdge))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _dateText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTimeInvalid), nameof(IsTimePast), nameof(TimeEdge))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _timeText;

    public TodoEditorViewModel(Todo existing, Action<string, DateOnly?, TimeOnly?> save, Action cancel, Func<DateOnly> today)
    {
        _save = save;
        _today = today;
        EditingId = existing.Id;
        _titleText = existing.Title;
        _dateText = TodoText.DateField(existing.DueDate, today());
        _timeText = existing.DueTime is { } time ? time.ToString("HH:mm") : "";
        CancelCommand = new RelayCommand(cancel);
    }

    public string Title => "Edit task";

    public long EditingId { get; }

    public bool IsDateInvalid => !TodoText.TryDate(DateText, _today(), out _);

    public bool IsTimeInvalid => TimeText.Trim().Length > 0 && AlarmText.ParseTime(TimeText) is null;

    /// <summary>A time that has already gone today: saved without a reminder.</summary>
    public bool IsTimePast =>
        TimeText.Trim().Length > 0 && AlarmText.ParseTime(TimeText) is { } time &&
        TodoText.TryDate(DateText, _today(), out var date) &&
        TodoSchedule.IsPastToday(date, time, DateTimeOffset.UtcNow, TimeZoneInfo.Local);

    /// <summary>The Time field edge: red (true) when it is not a time, yellow ("Warn") when it has passed today.</summary>
    public object TimeEdge => IsTimeInvalid ? true : IsTimePast ? "Warn" : false;

    public IRelayCommand CancelCommand { get; }

    private bool CanSave() => TodoText.Title(TitleText) is not null && !IsDateInvalid && !IsTimeInvalid;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        if (TodoText.Title(TitleText) is not { } title || !TodoText.TryDate(DateText, _today(), out var date) || IsTimeInvalid) return;
        _save(title, date, TimeText.Trim().Length == 0 ? null : AlarmText.ParseTime(TimeText));
    }
}
