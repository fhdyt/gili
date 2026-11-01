using Gili.Core.Projects;
using Gili.Data;
using Microsoft.Data.Sqlite;

namespace Gili.Core.Tests.Data;

public sealed class ProjectStoreTests : IDisposable
{
    private static readonly DateTimeOffset Created = new(2026, 11, 1, 5, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 1);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "gili-projects-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Project Release => new(0, "Gili 0.3 release", 0, Created);

    private static ProjectTask Task(long project, string title, int from, int to, int color = 0) =>
        new(0, project, title, Today.AddDays(from), Today.AddDays(to), color, null, 0, Created);

    [Fact]
    public void Projects_and_tasks_survive_a_restart()
    {
        Project project;
        ProjectTask spec, page;
        using (var db = Database.Open(_folder))
        {
            var store = new ProjectStore(db);
            project = store.AddProject(Release);
            spec = store.AddTask(Task(project.Id, "Write the spec", -9, -7, color: 6));
            page = store.AddTask(Task(project.Id, "Timeline page", -1, 4));
        }
        SqliteConnection.ClearAllPools();
        using var reopened = Database.Open(_folder);
        var again = new ProjectStore(reopened);
        Assert.True(project.Id > 0);
        Assert.Equal([project], again.Projects());
        Assert.Equal([spec, page], again.Tasks());
        Assert.Equal([0, 1], again.Tasks().Select(t => t.SortOrder));
    }

    [Fact]
    public void Projects_and_their_tasks_are_numbered_in_order()
    {
        using var db = Database.Open(_folder);
        var store = new ProjectStore(db);
        var first = store.AddProject(Release);
        var second = store.AddProject(Release with { Name = "Kitchen renovation" });
        store.AddTask(Task(first.Id, "A", 0, 0));
        var other = store.AddTask(Task(second.Id, "B", 0, 0));
        Assert.Equal([0, 1], store.Projects().Select(p => p.SortOrder));
        Assert.Equal(0, other.SortOrder);
    }

    [Fact]
    public void Update_changes_every_field()
    {
        using var db = Database.Open(_folder);
        var store = new ProjectStore(db);
        var project = store.AddProject(Release);
        var added = store.AddTask(Task(project.Id, "Timeline page", -1, 4));
        var changed = added with { Title = "Drag to move dates", Start = Today.AddDays(2), End = Today.AddDays(6), Color = 4, DoneAt = Created.AddHours(3) };
        store.UpdateTask(changed);
        Assert.Equal([changed], store.Tasks());
    }

    [Fact]
    public void Rename_keeps_the_rest()
    {
        using var db = Database.Open(_folder);
        var store = new ProjectStore(db);
        var project = store.AddProject(Release);
        store.RenameProject(project.Id, "Gili 0.4");
        Assert.Equal([project with { Name = "Gili 0.4" }], store.Projects());
    }

    [Fact]
    public void Deleting_a_project_deletes_its_tasks_only()
    {
        using var db = Database.Open(_folder);
        var store = new ProjectStore(db);
        var gone = store.AddProject(Release);
        var kept = store.AddProject(Release with { Name = "Kitchen renovation" });
        store.AddTask(Task(gone.Id, "A", 0, 1));
        var stays = store.AddTask(Task(kept.Id, "B", 0, 1));
        store.DeleteProject(gone.Id);
        Assert.Equal([kept], store.Projects());
        Assert.Equal([stays], store.Tasks());
    }

    [Fact]
    public void Delete_task_removes_one()
    {
        using var db = Database.Open(_folder);
        var store = new ProjectStore(db);
        var project = store.AddProject(Release);
        var gone = store.AddTask(Task(project.Id, "A", 0, 1));
        var stays = store.AddTask(Task(project.Id, "B", 2, 3));
        store.DeleteTask(gone.Id);
        Assert.Equal([stays], store.Tasks());
    }

    [Fact]
    public void Rows_edited_by_hand_are_read_safely()
    {
        using var db = Database.Open(_folder);
        var store = new ProjectStore(db);
        var project = store.AddProject(Release);
        using (var command = db.Connection.CreateCommand())
        {
            command.CommandText =
                "INSERT INTO project_tasks (project_id, title, start_date, end_date, color, sort_order, created_at) VALUES " +
                $"({project.Id}, 'Swapped', '2026-11-05', '2026-11-02', 42, 0, '2026-11-01T05:00:00.0000000+00:00'), " +
                $"({project.Id}, 'Broken', 'soon', '2026-11-02', 0, 1, '2026-11-01T05:00:00.0000000+00:00')";
            command.ExecuteNonQuery();
        }
        var task = Assert.Single(store.Tasks());
        Assert.Equal((new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 5), 0), (task.Start, task.End, task.Color));
    }
}
