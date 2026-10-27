using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Alarms;
using Gili.Core.Todos;

namespace Gili.App.Island;

/// <summary>
/// One task on the Todo page (mockup .tlist li): its check, title and time; Edit turns it into fields in place, and
/// Delete asks once with a red "Delete?" for 3 s.
/// </summary>
public sealed partial class TodoRowViewModel : ObservableObject
{
    private readonly TodoService _todos;
    private readonly Action _changed;
    private readonly DispatcherTimer _confirm = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _loading;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string? _time;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private bool _isConfirming;
    [ObservableProperty] private string _editTitle = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditTimeInvalid), nameof(EditTimeState))]
    private string _editTime = "";

    public TodoRowViewModel(long id, TodoService todos, Action changed)
    {
        Id = id;
        _todos = todos;
        _changed = changed;
        _confirm.Tick += (_, _) => ResetDelete();
    }

    public long Id { get; }

    public bool HasTime => Time is not null;

    public bool IsEditTimeInvalid => EditTime.Trim().Length > 0 && AlarmText.ParseTime(EditTime) is null;

    public string? EditTimeState => IsEditTimeInvalid ? "Invalid" : null;

    public void Show(Todo todo, bool use24Hour)
    {
        _loading = true;
        Title = todo.Title;
        Time = todo.DueTime is { } time ? AlarmText.Time(time, use24Hour) : null;
        OnPropertyChanged(nameof(HasTime));
        IsDone = todo.IsDone;
        _loading = false;
    }

    partial void OnIsDoneChanged(bool value)
    {
        if (!_loading) _todos.SetDone(Id, value);
    }

    [RelayCommand]
    private void Edit()
    {
        if (_todos.Todos.FirstOrDefault(t => t.Id == Id) is not { } todo) return;
        EditTitle = todo.Title;
        EditTime = todo.DueTime?.ToString("HH:mm") ?? "";
        ResetDelete();
        IsEditing = true;
        _changed();
    }

    /// <summary>Enter or the check: a blank title or an unreadable time keeps the fields open.</summary>
    [RelayCommand]
    private void Save()
    {
        if (TodoText.Title(EditTitle) is not { } title || IsEditTimeInvalid) return;
        IsEditing = false;
        _todos.Update(Id, title, AlarmText.ParseTime(EditTime));
        _changed();
    }

    [RelayCommand]
    private void Cancel()
    {
        IsEditing = false;
        _changed();
    }

    [RelayCommand]
    private void Delete()
    {
        if (!IsConfirming)
        {
            IsConfirming = true;
            _confirm.Start();
            return;
        }
        ResetDelete();
        _todos.Delete(Id);
    }

    public void ResetDelete()
    {
        _confirm.Stop();
        IsConfirming = false;
    }
}

/// <summary>The Todo page (PRD 6.3 page 5): "4/5 Tasks done today" and every task for today, with no way to add here.</summary>
public sealed partial class TodoViewModel : ObservableObject
{
    private readonly TodoService _todos;
    private readonly Func<bool> _use24Hour;

    [ObservableProperty] private string _count = "0/0";

    public TodoViewModel(TodoService todos, Func<bool> use24Hour)
    {
        _todos = todos;
        _use24Hour = use24Hour;
        todos.Changed += Refresh;
        Refresh();
    }

    public ObservableCollection<TodoRowViewModel> Rows { get; } = [];

    public bool IsEmpty => Rows.Count == 0;

    /// <summary>Raised when the page needs a new height: a row came, went, or opened for editing.</summary>
    public event Action? ContentChanged;

    /// <summary>Today's tasks; rows stay the same objects while the list keeps its order, so checks animate.</summary>
    public void Refresh()
    {
        var today = TodoSchedule.Today(_todos.Todos, _todos.Today);
        Count = TodoText.Count(today);
        if (!Rows.Select(r => r.Id).SequenceEqual(today.Select(t => t.Id)))
        {
            foreach (var row in Rows) row.ResetDelete();
            Rows.Clear();
            foreach (var todo in today) Rows.Add(new TodoRowViewModel(todo.Id, _todos, () => ContentChanged?.Invoke()));
            OnPropertyChanged(nameof(IsEmpty));
            ContentChanged?.Invoke();
        }
        var use24 = _use24Hour();
        for (var i = 0; i < today.Count; i++) Rows[i].Show(today[i], use24);
    }
}
