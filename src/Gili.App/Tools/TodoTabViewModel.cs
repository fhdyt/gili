using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.App.Island;
using Gili.Core.Todos;

namespace Gili.App.Tools;

/// <summary>A task in Tools: check, title, when it is due (or Overdue), Edit and a Delete that asks for a second click within 3 s.</summary>
public sealed partial class ToolTodoRowViewModel : ObservableObject
{
    private readonly TodoService _todos;
    private bool _loading;

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _due = "";
    [ObservableProperty] private string _dueTip = "";
    [ObservableProperty] private bool _isOverdue;
    [ObservableProperty] private bool _isLifted;
    [ObservableProperty] private bool _isDone;

    public ToolTodoRowViewModel(long id, TodoService todos, Action<long> edit)
    {
        Id = id;
        _todos = todos;
        EditCommand = new RelayCommand(() => edit(Id));
        Delete = new DeleteConfirm(() => todos.Delete(Id));
    }

    public long Id { get; }

    public IRelayCommand EditCommand { get; }

    public DeleteConfirm Delete { get; }

    public void Show(Todo todo, DateOnly today, bool use24Hour)
    {
        _loading = true;
        Title = todo.Title;
        IsDone = todo.IsDone;
        IsOverdue = TodoText.IsOverdue(todo, today);
        DueTip = TodoText.Due(todo, today, use24Hour);
        Due = IsOverdue ? "Overdue" : DueTip;
        _loading = false;
    }

    public void Reset() => Delete.Reset();

    partial void OnIsDoneChanged(bool value)
    {
        if (!_loading) _todos.SetDone(Id, value);
    }
}

/// <summary>
/// The Todo tab in Tools (F-TOOL-06): filter, the tasks, and the editor above them while open. Tasks are only made from
/// Calendar in the island, so there is no add. Follows <c>TodoService.Changed</c>.
/// </summary>
public sealed partial class TodoTabViewModel : ObservableObject
{
    private readonly TodoService _todos;
    private readonly Func<bool> _use24Hour;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyText), nameof(CanReorder))]
    private TodoFilter _filter = TodoFilter.Today;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditor))]
    private TodoEditorViewModel? _editor;

    public TodoTabViewModel(TodoService todos, Func<bool> use24Hour)
    {
        _todos = todos;
        _use24Hour = use24Hour;
        todos.Changed += Refresh;
        Refresh();
    }

    public IReadOnlyList<TodoFilter> Filters { get; } = [TodoFilter.Today, TodoFilter.All, TodoFilter.Done];

    public ObservableCollection<ToolTodoRowViewModel> Rows { get; } = [];

    public string Note => TodoText.CalendarNote;

    public string EmptyText => TodoText.Empty(Filter);

    public bool IsEmpty => Rows.Count == 0;

    public bool HasEditor => Editor is not null;

    /// <summary>Drag and Alt + arrow work in Today and All, not in Done.</summary>
    public bool CanReorder => TodoTabRules.CanReorder(Filter);

    public void Detach()
    {
        _todos.Changed -= Refresh;
        foreach (var row in Rows) row.Reset();
    }

    /// <summary>Alt + arrow: the task trades places with its neighbour in this list (up is -1). False at either end or in Done.</summary>
    public bool Move(long id, int delta)
    {
        if (TodoTabRules.Step(_todos.Todos, [.. Rows.Select(r => r.Id)], Filter, id, delta) is not { } order) return false;
        _todos.Reorder(order);
        return true;
    }

    /// <summary>A row dropped on another: it takes that task's place in the whole order, so hidden tasks stay where they are.</summary>
    public void Drop(long id, long target)
    {
        if (!CanReorder || TodoTabRules.Drop(_todos.Todos, id, target) is not { } order) return;
        _todos.Reorder(order);
    }

    partial void OnFilterChanged(TodoFilter value)
    {
        Editor = null;
        Refresh();
    }

    private void Edit(long id)
    {
        if (_todos.Todos.FirstOrDefault(t => t.Id == id) is not { } existing) return;
        Editor = new TodoEditorViewModel(existing, (title, date, time) =>
        {
            Editor = null;
            _todos.Edit(id, title, date, time);
        }, () => Editor = null, () => _todos.Today);
    }

    private void Refresh()
    {
        var today = _todos.Today;
        var visible = TodoSchedule.Filter(_todos.Todos, Filter, today);
        // Rows keep their identity: gone ones leave, new ones come in, and the rest slide to their place.
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            if (visible.Any(t => t.Id == Rows[i].Id)) continue;
            Rows[i].Reset();
            Rows.RemoveAt(i);
        }
        for (var i = 0; i < visible.Count; i++)
        {
            var at = IndexOfRow(visible[i].Id);
            if (at < 0) Rows.Insert(i, new ToolTodoRowViewModel(visible[i].Id, _todos, Edit));
            else if (at != i) Rows.Move(at, i);
        }
        var use24Hour = _use24Hour();
        for (var i = 0; i < visible.Count; i++) Rows[i].Show(visible[i], today, use24Hour);
        OnPropertyChanged(nameof(IsEmpty));
        // A task deleted while it was being edited closes its editor.
        if (Editor is { } editor && _todos.Todos.All(t => t.Id != editor.EditingId)) Editor = null;
    }

    private int IndexOfRow(long id)
    {
        for (var i = 0; i < Rows.Count; i++)
            if (Rows[i].Id == id) return i;
        return -1;
    }
}
