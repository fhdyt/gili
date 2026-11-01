using Gili.Core.Notes;
using Gili.Core.Projects;
using Gili.Data;

namespace Gili.App.Island;

/// <summary>
/// The projects and their tasks (F-ISL-34), kept in gili.db. Without a database they live in memory until Gili quits, so
/// the bar keeps working. No timers: nothing happens while the Projects page is closed. Raised on the UI thread.
/// </summary>
public sealed class ProjectService
{
    private readonly ProjectStore? _store;
    private readonly List<Project> _projects;
    private readonly List<ProjectTask> _tasks;

    public ProjectService(ProjectStore? store)
    {
        _store = store;
        _projects = [.. store?.Projects() ?? []];
        _tasks = [.. store?.Tasks() ?? []];
    }

    public IReadOnlyList<Project> Projects => _projects;

    /// <summary>Today as a local day: the timeline's green circle and what counts as overdue.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public event Action? Changed;

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;

    public IReadOnlyList<ProjectTask> TasksOf(long projectId) =>
        [.. _tasks.Where(t => t.ProjectId == projectId).OrderBy(t => t.SortOrder).ThenBy(t => t.Id)];

    public Project AddProject(string name)
    {
        var project = new Project(0, name, 0, Now);
        project = _store?.AddProject(project) ?? project with
        {
            Id = _projects.Count == 0 ? 1 : _projects.Max(p => p.Id) + 1,
            SortOrder = _projects.Count == 0 ? 0 : _projects.Max(p => p.SortOrder) + 1,
        };
        _projects.Add(project);
        Saved();
        return project;
    }

    public void RenameProject(long id, string name)
    {
        var index = _projects.FindIndex(p => p.Id == id);
        if (index < 0) return;
        _store?.RenameProject(id, name);
        _projects[index] = _projects[index] with { Name = name };
        Saved();
    }

    /// <summary>The project and all its tasks.</summary>
    public void DeleteProject(long id)
    {
        _store?.DeleteProject(id);
        _projects.RemoveAll(p => p.Id == id);
        _tasks.RemoveAll(t => t.ProjectId == id);
        Saved();
    }

    /// <summary>Adds the task as the project's last row.</summary>
    public ProjectTask AddTask(long projectId, string title, DateOnly start, DateOnly end, int color)
    {
        var task = new ProjectTask(0, projectId, title, start, end, NoteStyle.ClampColor(color), null, 0, Now);
        var siblings = _tasks.Where(t => t.ProjectId == projectId).ToList();
        task = _store?.AddTask(task) ?? task with
        {
            Id = _tasks.Count == 0 ? 1 : _tasks.Max(t => t.Id) + 1,
            SortOrder = siblings.Count == 0 ? 0 : siblings.Max(t => t.SortOrder) + 1,
        };
        _tasks.Add(task);
        Saved();
        return task;
    }

    public void UpdateTask(long id, string title, DateOnly start, DateOnly end, int color) =>
        Change(id, t => t with { Title = title, Start = start, End = end, Color = NoteStyle.ClampColor(color) });

    /// <summary>New days from dragging the bar or the keyboard.</summary>
    public void Move(long id, DateOnly start, DateOnly end) => Change(id, t => t with { Start = start, End = end });

    public void SetDone(long id, bool done) => Change(id, t => t.IsDone == done ? t : t with { DoneAt = done ? Now : null });

    public void SetColor(long id, int color) => Change(id, t => t with { Color = NoteStyle.ClampColor(color) });

    public void DeleteTask(long id)
    {
        _store?.DeleteTask(id);
        _tasks.RemoveAll(t => t.Id == id);
        Saved();
    }

    private void Change(long id, Func<ProjectTask, ProjectTask> change)
    {
        var index = _tasks.FindIndex(t => t.Id == id);
        if (index < 0) return;
        var task = change(_tasks[index]);
        if (task == _tasks[index]) return;
        _tasks[index] = task;
        _store?.UpdateTask(task);
        Saved();
    }

    private void Saved() => Changed?.Invoke();
}
