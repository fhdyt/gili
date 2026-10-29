using System.Windows.Threading;
using Gili.Core.Todos;
using Gili.Data;

namespace Gili.App.Island;

/// <summary>
/// The tasks (F-ISL-16, F-TOOL-06/07): kept in gili.db, made only from the Calendar page, with one timer to the next
/// reminder. Reminders are worked out again on start, on waking and when the clock or zone changes. Raised on the UI
/// thread.
/// </summary>
public sealed class TodoService
{
    private readonly TodoStore? _store;
    private readonly DispatcherTimer _deadline = new();
    private List<Todo> _todos;

    public TodoService(TodoStore? store)
    {
        _store = store;
        _todos = [.. store?.All() ?? []];
        _deadline.Tick += (_, _) => Reschedule();
    }

    public IReadOnlyList<Todo> Todos => _todos;

    public DateOnly Today => TodoSchedule.LocalDate(Now, TimeZoneInfo.Local);

    public event Action? Changed;

    /// <summary>A task's reminder came: its card shows for 10 s (PRD 6.3 priority 2).</summary>
    public event Action<Todo>? Reminding;

    /// <summary>A task was changed, done or deleted: a reminder card it raised no longer applies.</summary>
    public event Action<long>? Stale;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    /// <summary>Adds a task for the day at the end of the list; a time still ahead gets a reminder.</summary>
    public Todo Add(string title, DateOnly date, TimeOnly? time)
    {
        var todo = TodoSchedule.Arm(new Todo(0, title, date, time, null, null, 0, Now), Now, TimeZoneInfo.Local);
        todo = _store?.Add(todo) ?? todo with
        {
            Id = _todos.Count == 0 ? 1 : _todos.Max(t => t.Id) + 1,
            SortOrder = _todos.Count == 0 ? 0 : _todos.Max(t => t.SortOrder) + 1,
        };
        _todos.Add(todo);
        Saved();
        return todo;
    }

    /// <summary>A new title and time; clearing the time removes the reminder (PRD 6.3).</summary>
    public void Update(long id, string title, TimeOnly? time)
    {
        if (Find(id) is not { } todo) return;
        Replace(TodoSchedule.Arm(todo with { Title = title, DueTime = time }, Now, TimeZoneInfo.Local));
        Stale?.Invoke(id);
    }

    /// <summary>Title, day and time from Tools; the reminder is armed again for the new time and the old card closes.</summary>
    public void Edit(long id, string title, DateOnly? date, TimeOnly? time)
    {
        if (Find(id) is not { } todo) return;
        Replace(TodoSchedule.Arm(todo with { Title = title, DueDate = date, DueTime = time }, Now, TimeZoneInfo.Local));
        Stale?.Invoke(id);
    }

    /// <summary>
    /// Saves a new order; <c>ids</c> must hold every task. Tasks missing from it keep their relative place at the end.
    /// </summary>
    public void Reorder(IReadOnlyList<long> ids)
    {
        var place = ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i);
        _todos = [.. _todos.OrderBy(t => place.GetValueOrDefault(t.Id, int.MaxValue))
            .Select((t, i) => t with { SortOrder = i })];
        _store?.Reorder([.. _todos.Select(t => t.Id)]);
        Saved();
    }

    public void SetDone(long id, bool done)
    {
        if (Find(id) is not { } todo || todo.IsDone == done) return;
        Replace(TodoSchedule.Arm(todo with { DoneAt = done ? Now : null }, Now, TimeZoneInfo.Local));
        if (done) Stale?.Invoke(id);
    }

    public void Delete(long id)
    {
        _store?.Delete(id);
        _todos.RemoveAll(t => t.Id == id);
        Stale?.Invoke(id);
        Saved();
    }

    /// <summary>Snooze 10m on the reminder card.</summary>
    public void Snooze(long id)
    {
        if (Find(id) is { } todo && !todo.IsDone) Replace(TodoSchedule.Snooze(todo, Now));
    }

    /// <summary>
    /// Shows reminders that came (at most an hour late), drops older ones, then waits for the next. Called at start, on
    /// waking and when the clock or time zone changes.
    /// </summary>
    public void Reschedule()
    {
        var now = Now;
        var due = TodoSchedule.Due(_todos, now);
        foreach (var todo in due.Concat(TodoSchedule.Stale(_todos, now))) Replace(todo with { RemindAt = null }, notify: false);
        Saved();
        foreach (var todo in due) Reminding?.Invoke(todo);
    }

    private Todo? Find(long id) => _todos.FirstOrDefault(t => t.Id == id);

    private void Replace(Todo todo, bool notify = true)
    {
        var index = _todos.FindIndex(t => t.Id == todo.Id);
        if (index < 0) return;
        _todos[index] = todo;
        _store?.Update(todo);
        if (notify) Saved();
    }

    /// <summary>Points the timer at the next reminder and says the list changed.</summary>
    private void Saved()
    {
        _deadline.Stop();
        if (TodoSchedule.Next(_todos)?.RemindAt is { } at)
        {
            var wait = at - Now;
            _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
            _deadline.Start();
        }
        Changed?.Invoke();
    }

    public void Stop() => _deadline.Stop();
}
