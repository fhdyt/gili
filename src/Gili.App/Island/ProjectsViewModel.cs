using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gili.Core.Notes;
using Gili.Core.Projects;

namespace Gili.App.Island;

/// <summary>One row of the project list (mockup .pjmi): the name, done/total, and Rename or a two-click Delete on hover.</summary>
public sealed partial class ProjectMenuItem(long id, string name, string progress, bool isCurrent) : ObservableObject
{
    public long Id { get; } = id;
    public string Name { get; } = name;
    public string Progress { get; } = progress;
    public bool IsCurrent { get; } = isCurrent;

    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renameText = name;
    [ObservableProperty] private bool _isConfirmingDelete;
}

/// <summary>A note colour as a round swatch in the task form (mockup .pjsw).</summary>
public sealed partial class ProjectColorItem(int index, NoteColor color) : ObservableObject
{
    public int Index { get; } = index;
    public string Name { get; } = color.Name;
    public uint Rgb { get; } = color.Rgb;
    public string Label { get; } = "Color: " + color.Name;

    [ObservableProperty] private bool _isPicked;
}

/// <summary>
/// The Projects page (F-ISL-34, mockup renderPj): one project at a time, the project list, the task form and the first-run
/// step. The timeline itself (bars, scrolling, dragging) is <see cref="Controls.ProjectTimeline"/>.
/// </summary>
public sealed partial class ProjectsViewModel : ObservableObject
{
    private readonly ProjectService _projects;
    private readonly Func<long?> _savedId;
    private readonly Action<long?> _save;
    private readonly DispatcherTimer _confirmTask = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DispatcherTimer _confirmProject = new() { Interval = TimeSpan.FromSeconds(3) };
    private long? _currentId;
    private int _lastColor;

    public ProjectsViewModel(ProjectService projects, Func<long?> savedId, Action<long?> save)
    {
        _projects = projects;
        _savedId = savedId;
        _save = save;
        Colors = [.. NoteStyle.Colors.Select((c, i) => new ProjectColorItem(i, c))];
        _confirmTask.Tick += (_, _) =>
        {
            _confirmTask.Stop();
            IsConfirmingDelete = false;
        };
        _confirmProject.Tick += (_, _) =>
        {
            _confirmProject.Stop();
            foreach (var item in MenuItems) item.IsConfirmingDelete = false;
        };
        projects.Changed += Refresh;
        Refresh();
    }

    /// <summary>The page's height can change: tasks added or removed, the form or the list opened or closed.</summary>
    public event Action? ContentChanged;

    /// <summary>The view should move the keyboard focus: "title", "first", "newProject", "rename", "select" or "bar".</summary>
    public event Action<string>? FocusRequested;

    public ObservableCollection<ProjectMenuItem> MenuItems { get; } = [];

    public IReadOnlyList<ProjectColorItem> Colors { get; }

    [ObservableProperty] private bool _hasProject;
    [ObservableProperty] private string _projectName = "";
    [ObservableProperty] private string _meta = "";
    [ObservableProperty] private IReadOnlyList<ProjectTask> _tasks = [];
    [ObservableProperty] private DateOnly _today = ProjectService.Today;

    /// <summary>The project on show; the timeline goes back to its opening scroll when this changes.</summary>
    [ObservableProperty] private long _projectKey;

    [ObservableProperty] private string _firstName = "";
    [ObservableProperty] private bool _isMenuOpen;
    [ObservableProperty] private bool _isAddingProject;
    [ObservableProperty] private string _newProjectName = "";

    [ObservableProperty] private bool _isFormOpen;

    /// <summary>The task the form edits (its bar gets the white ring); null while adding a new one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(SaveLabel), nameof(TitleHint))]
    private long? _selectedId;

    [ObservableProperty] private string _formTitle = "";
    [ObservableProperty] private string _formStart = "";
    [ObservableProperty] private string _formEnd = "";
    [ObservableProperty] private int _formColor;
    [ObservableProperty] private string? _titleState;
    [ObservableProperty] private string? _startState;
    [ObservableProperty] private string? _endState;
    [ObservableProperty] private string _endHint = DateHint;
    [ObservableProperty] private bool _isConfirmingDelete;

    public const string DateHint = "A date like 5 Nov, or ↑ and ↓ a day at a time";

    public bool IsEditing => SelectedId is not null;

    public string SaveLabel => IsEditing ? "Save" : "Add task";

    public string TitleHint => IsEditing ? "Task name" : "New task name";

    /// <summary>Reads the projects again after any change (here or in the store) and keeps the page's state where it applies.</summary>
    public void Refresh()
    {
        Today = ProjectService.Today;
        var list = _projects.Projects;
        _currentId ??= _savedId();
        if (_currentId is { } id && list.All(p => p.Id != id)) _currentId = null;
        _currentId ??= list.FirstOrDefault()?.Id;
        var current = list.FirstOrDefault(p => p.Id == _currentId);
        var hadProject = HasProject;
        var count = Tasks.Count;
        HasProject = current is not null;
        ProjectName = current?.Name ?? "";
        ProjectKey = current?.Id ?? 0;
        Tasks = current is null ? [] : _projects.TasksOf(current.Id);
        Meta = ProjectText.Meta(Tasks);
        if (SelectedId is { } selected && Tasks.All(t => t.Id != selected)) CloseForm();
        if (current is null)
        {
            IsMenuOpen = false;
            IsFormOpen = false;
        }
        if (IsMenuOpen) FillMenu();
        if (hadProject != HasProject || count != Tasks.Count) ContentChanged?.Invoke();
    }

    /// <summary>Step one: the first project's name; step two opens the task form at once.</summary>
    [RelayCommand]
    private void CreateFirst()
    {
        if (ProjectText.Name(FirstName, ProjectText.NameLimit) is not { } name)
        {
            FocusRequested?.Invoke("first");
            return;
        }
        FirstName = "";
        Create(name);
    }

    [RelayCommand]
    private void ToggleMenu()
    {
        IsMenuOpen = !IsMenuOpen;
        IsAddingProject = false;
        if (IsMenuOpen) FillMenu();
        ContentChanged?.Invoke();
    }

    public void CloseMenu()
    {
        if (!IsMenuOpen) return;
        IsMenuOpen = false;
        IsAddingProject = false;
        ContentChanged?.Invoke();
    }

    [RelayCommand]
    private void Pick(ProjectMenuItem item)
    {
        if (item.Id != _currentId)
        {
            _currentId = item.Id;
            _save(item.Id);
            CloseForm();
        }
        CloseMenu();
        Refresh();
        FocusRequested?.Invoke("select");
    }

    [RelayCommand]
    private void StartRename(ProjectMenuItem item)
    {
        foreach (var other in MenuItems) other.IsRenaming = false;
        IsAddingProject = false;
        item.RenameText = item.Name;
        item.IsRenaming = true;
        FocusRequested?.Invoke("rename");
    }

    [RelayCommand]
    private void SaveRename(ProjectMenuItem item)
    {
        if (ProjectText.Name(item.RenameText, ProjectText.NameLimit) is not { } name)
        {
            FocusRequested?.Invoke("rename");
            return;
        }
        item.IsRenaming = false;
        _projects.RenameProject(item.Id, name);
    }

    /// <summary>Delete asks once: the first click turns the button into a red "Delete?" for 3 seconds.</summary>
    [RelayCommand]
    private void DeleteProject(ProjectMenuItem item)
    {
        if (!item.IsConfirmingDelete)
        {
            foreach (var other in MenuItems) other.IsConfirmingDelete = false;
            item.IsConfirmingDelete = true;
            _confirmProject.Stop();
            _confirmProject.Start();
            return;
        }
        _confirmProject.Stop();
        if (item.Id == _currentId)
        {
            _currentId = null;
            _save(null);
            CloseForm();
        }
        _projects.DeleteProject(item.Id);
        if (_projects.Projects.Count == 0) CloseMenu();
    }

    [RelayCommand]
    private void StartNewProject()
    {
        foreach (var item in MenuItems) item.IsRenaming = false;
        NewProjectName = "";
        IsAddingProject = true;
        ContentChanged?.Invoke();
        FocusRequested?.Invoke("newProject");
    }

    [RelayCommand]
    private void CreateProject()
    {
        if (ProjectText.Name(NewProjectName, ProjectText.NameLimit) is not { } name)
        {
            FocusRequested?.Invoke("newProject");
            return;
        }
        IsAddingProject = false;
        CloseMenu();
        Create(name);
    }

    /// <summary>The Add task button (today) or a click on a day in the empty lane.</summary>
    public void OpenNew(DateOnly day)
    {
        CloseMenu();
        SelectedId = null;
        Fill("", day, day, _lastColor);
        FocusRequested?.Invoke("title");
    }

    [RelayCommand]
    private void AddTask() => OpenNew(Today);

    /// <summary>A click on a bar (or Enter on it): the same form, holding that task.</summary>
    public void Open(long id)
    {
        if (Tasks.FirstOrDefault(t => t.Id == id) is not { } task) return;
        CloseMenu();
        SelectedId = id;
        Fill(task.Title, task.Start, task.End, task.Color);
        FocusRequested?.Invoke("title");
    }

    /// <summary>
    /// Enter or the check. A blank name, a date it can't read or an end before the start marks that field red and keeps the
    /// form. Adding keeps the form open for the next task, from the day after this one ends, in the same colour.
    /// </summary>
    [RelayCommand]
    private void SaveForm()
    {
        var title = ProjectText.Name(FormTitle, ProjectText.TaskLimit);
        var start = ProjectText.ParseField(FormStart, Today);
        var end = ProjectText.ParseField(FormEnd, Today);
        var backwards = start is { } s && end is { } e && e < s;
        TitleState = title is null ? "Invalid" : null;
        StartState = start is null ? "Invalid" : null;
        EndState = end is null || backwards ? "Invalid" : null;
        EndHint = backwards ? "The end is before the start" : DateHint;
        if (title is null || start is null || end is null || backwards)
        {
            FocusRequested?.Invoke(title is null ? "title" : start is null ? "start" : "end");
            return;
        }
        _lastColor = FormColor;
        if (SelectedId is { } id)
        {
            _projects.UpdateTask(id, title, start.Value, end.Value, FormColor);
            CloseForm();
            FocusRequested?.Invoke("bar");
            return;
        }
        if (_currentId is not { } project) return;
        _projects.AddTask(project, title, start.Value, end.Value, FormColor);
        var next = end.Value.AddDays(1);
        Fill("", next, next, FormColor);
        FocusRequested?.Invoke("title");
    }

    public void CloseForm()
    {
        if (!IsFormOpen && SelectedId is null) return;
        IsFormOpen = false;
        SelectedId = null;
        IsConfirmingDelete = false;
        _confirmTask.Stop();
        ContentChanged?.Invoke();
    }

    [RelayCommand]
    private void DeleteTask()
    {
        if (SelectedId is not { } id) return;
        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            _confirmTask.Stop();
            _confirmTask.Start();
            return;
        }
        CloseForm();
        _projects.DeleteTask(id);
    }

    /// <summary>A swatch: the open task's bar takes the colour at once, as a note's card does.</summary>
    [RelayCommand]
    private void PickColor(ProjectColorItem color)
    {
        FormColor = color.Index;
        if (SelectedId is { } id) _projects.SetColor(id, color.Index);
    }

    partial void OnFormColorChanged(int value)
    {
        foreach (var color in Colors) color.IsPicked = color.Index == value;
    }

    partial void OnFormTitleChanged(string value) => TitleState = null;

    partial void OnFormStartChanged(string value) => StartState = null;

    partial void OnFormEndChanged(string value)
    {
        EndState = null;
        EndHint = DateHint;
    }

    /// <summary>↑/↓ in a date field: a day later or earlier; the end never goes before the start.</summary>
    public void StepDate(bool endField, int days)
    {
        var start = ProjectText.ParseField(FormStart, Today) ?? Today;
        var end = ProjectText.ParseField(FormEnd, Today) ?? start;
        if (endField)
        {
            end = end.AddDays(days);
            if (end < start) end = start;
        }
        else
        {
            start = start.AddDays(days);
            if (end < start) end = start;
        }
        FormStart = ProjectText.Field(start, Today);
        FormEnd = ProjectText.Field(end, Today);
    }

    public void Toggle(long id)
    {
        if (Tasks.FirstOrDefault(t => t.Id == id) is { } task) _projects.SetDone(id, !task.IsDone);
    }

    /// <summary>Dragged or nudged to new days; an open form on that task follows.</summary>
    public void Move(long id, DateOnly start, DateOnly end)
    {
        _projects.Move(id, start, end);
        if (SelectedId == id)
        {
            FormStart = ProjectText.Field(start, Today);
            FormEnd = ProjectText.Field(end, Today);
        }
    }

    /// <summary>
    /// Esc closes what is open on the page first: a rename or new-project field, then the project list, then the form.
    /// False when nothing was open, so the island closes.
    /// </summary>
    public bool Escape()
    {
        if (MenuItems.FirstOrDefault(i => i.IsRenaming) is { } renaming)
        {
            renaming.IsRenaming = false;
            FocusRequested?.Invoke("select");
            return true;
        }
        if (IsAddingProject)
        {
            IsAddingProject = false;
            ContentChanged?.Invoke();
            return true;
        }
        if (IsMenuOpen)
        {
            CloseMenu();
            FocusRequested?.Invoke("select");
            return true;
        }
        if (IsFormOpen)
        {
            var editing = IsEditing;
            CloseForm();
            if (editing) FocusRequested?.Invoke("bar");
            return true;
        }
        return false;
    }

    /// <summary>The focused bar after Esc or Save: the task the form held.</summary>
    public long? FocusTarget { get; private set; }

    private void Create(string name)
    {
        var project = _projects.AddProject(name);
        _currentId = project.Id;
        _save(project.Id);
        Refresh();
        OpenNew(Today);
    }

    private void Fill(string title, DateOnly start, DateOnly end, int color)
    {
        FocusTarget = SelectedId;
        FormTitle = title;
        FormStart = ProjectText.Field(start, Today);
        FormEnd = ProjectText.Field(end, Today);
        FormColor = color;
        OnFormColorChanged(color);
        TitleState = StartState = EndState = null;
        EndHint = DateHint;
        IsConfirmingDelete = false;
        var opened = !IsFormOpen;
        IsFormOpen = true;
        if (opened) ContentChanged?.Invoke();
    }

    private void FillMenu()
    {
        MenuItems.Clear();
        foreach (var project in _projects.Projects)
            MenuItems.Add(new ProjectMenuItem(project.Id, project.Name, ProjectText.Progress(_projects.TasksOf(project.Id)), project.Id == _currentId));
    }
}
