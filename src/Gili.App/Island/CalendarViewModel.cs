using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Alarms;
using Gili.Core.Todos;

namespace Gili.App.Island;

/// <summary>One cell of the month grid: blank before the 1st, or a day that can be picked (mockup .cal button).</summary>
public sealed partial class CalendarDayViewModel(DateOnly? date, Action<DateOnly> pick) : ObservableObject
{
    public DateOnly? Date { get; } = date;

    public string Text => Date?.Day.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";

    public bool IsDay => Date is not null;

    public string Name => Date?.ToString("d MMMM", System.Globalization.CultureInfo.GetCultureInfo("en-US")) ?? "";

    [ObservableProperty] private bool _isToday;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _hasDot;

    [RelayCommand]
    private void Pick()
    {
        if (Date is { } date) pick(date);
    }
}

/// <summary>One item in the day's list: a task or an alarm, crossed out when the task is done (mockup .cev .e).</summary>
public sealed record CalendarEntry(string Text, bool Done);

/// <summary>
/// The Calendar page (PRD 6.3 page 4): the month with today and dots, the picked day's tasks and alarms, and Add task,
/// the only place tasks are made (F-ISL-16).
/// </summary>
public sealed partial class CalendarViewModel : ObservableObject
{
    private readonly TodoService _todos;
    private readonly AlarmService _alarms;
    private readonly Func<bool> _use24Hour;
    private readonly DispatcherTimer _addedTimer = new() { Interval = TimeSpan.FromSeconds(2.4) };
    private int _year;
    private int _month;

    [ObservableProperty] private string _monthTitle = "";
    [ObservableProperty] private DateOnly _selected;
    [ObservableProperty] private string _heading = "";
    [ObservableProperty] private IReadOnlyList<CalendarEntry> _entries = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenAddCommand))]
    private bool _isAdding;

    [ObservableProperty] private string _addTitle = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddTimePast), nameof(IsAddTimeInvalid), nameof(AddTimeState))]
    private string _addTime = "";

    [ObservableProperty] private string? _added;

    public CalendarViewModel(TodoService todos, AlarmService alarms, Func<bool> use24Hour)
    {
        _todos = todos;
        _alarms = alarms;
        _use24Hour = use24Hour;
        _addedTimer.Tick += (_, _) =>
        {
            _addedTimer.Stop();
            Added = null;
        };
        todos.Changed += Refresh;
        alarms.Changed += Refresh;
        ShowToday();
    }

    public ObservableCollection<CalendarDayViewModel> Days { get; } = [];

    public static IReadOnlyList<string> DayNames { get; } = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];

    public bool IsEmpty => Entries.Count == 0;

    /// <summary>A time today that has already gone: still saved, without a reminder, and the field turns yellow.</summary>
    public bool IsAddTimePast => AlarmText.ParseTime(AddTime) is { } time &&
        TodoSchedule.IsPastToday(Selected, time, DateTimeOffset.UtcNow, TimeZoneInfo.Local);

    public bool IsAddTimeInvalid => AddTime.Trim().Length > 0 && AlarmText.ParseTime(AddTime) is null;

    /// <summary>The time field's edge: "Invalid" red, "Past" yellow (the Field style's Tag).</summary>
    public string? AddTimeState => IsAddTimeInvalid ? "Invalid" : IsAddTimePast ? "Past" : null;

    /// <summary>Raised when the page needs a new height: rows, the form or the Added line came or went.</summary>
    public event Action? ContentChanged;

    /// <summary>The page opens on the current month with today picked.</summary>
    public void ShowToday()
    {
        var today = _todos.Today;
        (_year, _month) = (today.Year, today.Month);
        IsAdding = false;
        Selected = today;
        BuildMonth();
    }

    [RelayCommand]
    private void PreviousMonth() => StepMonth(-1);

    [RelayCommand]
    private void NextMonth() => StepMonth(1);

    private bool CanOpenAdd() => !IsAdding;

    [RelayCommand(CanExecute = nameof(CanOpenAdd))]
    private void OpenAdd()
    {
        AddTitle = "";
        AddTime = "";
        IsAdding = true;
        ContentChanged?.Invoke();
    }

    [RelayCommand]
    private void CancelAdd()
    {
        IsAdding = false;
        ContentChanged?.Invoke();
    }

    /// <summary>Enter or the check: a blank title or an unreadable time keeps the form open (mockup cAdd submit).</summary>
    public bool Save()
    {
        if (TodoText.Title(AddTitle) is not { } title || IsAddTimeInvalid) return false;
        var time = AlarmText.ParseTime(AddTime);
        _todos.Add(title, Selected, time);
        IsAdding = false;
        if (Selected != _todos.Today)
        {
            Added = TodoText.Added(Selected, time, _use24Hour());
            _addedTimer.Stop();
            _addedTimer.Start();
        }
        ContentChanged?.Invoke();
        return true;
    }

    [RelayCommand]
    private void SaveAdd() => Save();

    partial void OnSelectedChanged(DateOnly value)
    {
        IsAdding = false;
        Added = null;
        foreach (var day in Days) day.IsSelected = day.Date == value;
        Refresh();
    }

    partial void OnAddedChanged(string? value) => ContentChanged?.Invoke();

    partial void OnEntriesChanged(IReadOnlyList<CalendarEntry> value)
    {
        OnPropertyChanged(nameof(IsEmpty));
        ContentChanged?.Invoke();
    }

    private void StepMonth(int by)
    {
        (_year, _month) = CalendarMonth.Step(_year, _month, by);
        var today = _todos.Today;
        Selected = today.Year == _year && today.Month == _month ? today : new DateOnly(_year, _month, 1);
        BuildMonth();
    }

    private void BuildMonth()
    {
        MonthTitle = TodoText.Month(_year, _month);
        var (leading, count) = CalendarMonth.Cells(_year, _month);
        Days.Clear();
        for (var i = 0; i < leading; i++) Days.Add(new CalendarDayViewModel(null, Pick));
        for (var d = 1; d <= count; d++) Days.Add(new CalendarDayViewModel(new DateOnly(_year, _month, d), Pick));
        Refresh();
        ContentChanged?.Invoke();
    }

    private void Pick(DateOnly date) => Selected = date;

    /// <summary>Today's mark, the dots and the picked day's list follow the tasks, the alarms and the clock.</summary>
    private void Refresh()
    {
        var today = _todos.Today;
        var zone = TimeZoneInfo.Local;
        var use24 = _use24Hour();
        foreach (var day in Days)
        {
            if (day.Date is not { } date) continue;
            day.IsToday = date == today;
            day.IsSelected = date == Selected;
            day.HasDot = TodoSchedule.HasDot(_todos.Todos, _alarms.Alarms, date, zone);
        }
        Heading = TodoText.Heading(Selected, today);
        var tasks = TodoSchedule.On(_todos.Todos, Selected).Select(t => (t.DueTime, Entry: new CalendarEntry(TodoText.CalendarItem(t, use24), t.IsDone)));
        var alarms = TodoSchedule.AlarmsOn(_alarms.Alarms, Selected, zone).Select(a => ((TimeOnly?)a.Time, Entry: new CalendarEntry(TodoText.AlarmItem(a, use24), false)));
        var entries = tasks.Concat(alarms).OrderBy(e => e.Item1 ?? TimeOnly.MaxValue).Select(e => e.Entry).ToList();
        if (!entries.SequenceEqual(Entries)) Entries = entries;
    }
}
