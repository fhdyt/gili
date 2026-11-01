# Halaman Projects (revisi 16) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Tab Projects di pulau: timeline project ala Notion dengan task bertanggal yang bisa digeser, ditarik ujungnya, diberi warna note, dan dicentang selesai, ditambah ikon di launcher kiri dan saklarnya (F-ISL-34).

**Architecture:** Model, teks, dan aritmetika tanggal ada di `Gili.Core.Projects` dan dites unit. Penyimpanan memakai dua tabel baru di migrasi 5 lewat `ProjectStore`. Di App, `ProjectService` (pola `TodoService`) memberi data ke `ProjectsViewModel`; `ProjectsPage` menyusun baris atas, form, dan menu, sedangkan `ProjectTimeline` (kontrol gambar sendiri) menggambar grid dan menangani scroll serta drag.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-01-revisi-16-projects-design.md`, PRD 6.3 "Halaman Projects", F-ISL-34. Acuan visual: tab Projects di `docs/mockup/top-taskbar-mockup.html`.

## Global Constraints

- Semua teks app bahasa Inggris, persis seperti PRD: `Plan a project`, `Name it, then add its tasks with dates. Drag them along the timeline as plans change.`, `Project name`, `Create`, `Add task`, `Today`, `Previous week`, `Next week`, `New task name`, `Task name`, `Start date`, `End date`, `Delete?`, `New project`, `Rename`, `Delete`, `No tasks yet`, `New task`, `Click a day to add a task`, `Overdue`, `The end is before the start`, `Mark as done`, `Mark as not done`.
- Kolom hari 30 px, baris task 30 px, kepala timeline 46 px, bar 24 px radius 12 menjorok 2 px, lingkaran centang 16 px.
- Warna task = `NoteStyle.Colors` (0–7). Glow halaman indigo `#5E5CE6`.
- Tanpa timer atau polling. Tidak ada tulisan ke database per frame.
- Tanggal lokal `DateOnly`, disimpan `yyyy-MM-dd`.
- `dotnet format Gili.slnx --verify-no-changes` harus lolos.

---

### Task 1: Tab dan saklar Projects di Core

**Files:**
- Modify: `src/Gili.Core/Island/IslandModels.cs` (enum `IslandPage`)
- Modify: `src/Gili.Core/Island/IslandTabs.cs` (`Of`)
- Modify: `src/Gili.Core/Settings/HiddenRules.cs` (`LeftIcons`, `PageSet`, `PageOf`)
- Modify: `src/Gili.Core/Settings/SettingsCatalog.cs` (`LeftRows`, `Pages`)
- Modify: `src/Gili.Core/Settings/AppSettings.cs` (`ProjectId`)
- Test: `tests/Gili.Core.Tests/Island/IslandTabsTests.cs`, `tests/Gili.Core.Tests/Settings/SettingsCatalogTests.cs`

**Interfaces:**
- Produces: `IslandPage.Projects` (setelah `Todo`), `LeftIcons.Projects = 1024`, `PageSet.Projects = 1024`, `PageSet.All = 2047`, `IslandTabs.Of(page)` lewat nama, `AppSettings.ProjectId : long?`.

- [ ] **Step 1: Tes gagal**

```csharp
[Fact]
public void The_menu_is_the_eleven_pages_without_controls()
{
    Assert.Equal(11, IslandTabs.Menu.Count);
    Assert.Equal(IslandPage.Projects, IslandTabs.Menu[5]);
    Assert.Equal(PageSet.Notes, IslandTabs.Of(IslandPage.Notes));
    Assert.Equal(PageSet.Projects, IslandTabs.Of(IslandPage.Projects));
}

[Fact]
public void Projects_left_switch_hides_its_tab()
{
    var s = SettingsCatalog.Write(new AppSettings(), "L.Projects", false);
    Assert.Equal(false, SettingsCatalog.Read(s, "T.Projects"));
    Assert.Equal(new RowState(false, "Turned off in Left side"), SettingsCatalog.State(s, "T.Projects"));
}
```

Daftar kunci di `SettingsCatalogTests` diperbarui dengan `"L.Projects"` setelah `"L.Todo"` dan `"T.Projects"` setelah `"T.Todo"`. Hitungan di `IslandTabsTests` naik satu (`Hidden_tabs_leave_the_menu` 9, `Temporary_tab_returns_at_its_place` 11).

- [ ] **Step 2: Jalankan, pastikan gagal**: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandTabsTests|FullyQualifiedName~SettingsCatalogTests"` → gagal kompilasi (`IslandPage.Projects` belum ada).

- [ ] **Step 3: Implementasi**

```csharp
public enum IslandPage { Media, Timer, Alarms, Calendar, Todo, Projects, Notes, Clipboard, Shelf, Claude, System, Controls }
// IslandTabs: the page's flag by name, since Projects sits mid-menu but took the next free bit (revision 16).
public static PageSet Of(IslandPage page) => page == IslandPage.Controls ? PageSet.None : Enum.Parse<PageSet>(page.ToString());
// HiddenRules: LeftIcons ... Favorites = 512, Projects = 1024; PageSet ... System = 512, Projects = 1024, All = 2047
// PageOf: LeftIcons.Projects => PageSet.Projects
// SettingsCatalog: LeftRows ("Projects", "Projects") after Todo; Pages "Projects" after "Todo"
// AppSettings: public long? ProjectId { get; init; }
```

- [ ] **Step 4: Jalankan tes yang sama** → lulus. Lalu jalankan semua tes: `dotnet test tests/Gili.Core.Tests`.
- [ ] **Step 5: Commit** `feat(island): Projects page and left icon switches in Core (F-ISL-34)`.

### Task 2: Model, teks, dan aritmetika timeline

**Files:**
- Create: `src/Gili.Core/Projects/Project.cs`, `ProjectText.cs`, `TimelineRange.cs`, `TaskDrag.cs`
- Test: `tests/Gili.Core.Tests/Projects/ProjectTextTests.cs`, `TimelineRangeTests.cs`, `TaskDragTests.cs`

**Interfaces:**
- Produces:
  - `record Project(long Id, string Name, int SortOrder, DateTimeOffset CreatedAt)`
  - `record ProjectTask(long Id, long ProjectId, string Title, DateOnly Start, DateOnly End, int Color, DateTimeOffset? DoneAt, int SortOrder, DateTimeOffset CreatedAt)` dengan `IsDone`, `Days`, `IsOverdue(DateOnly today)`
  - `enum DragEdge { Move, Start, End }`
  - `TaskDrag.Days(double pixels, double dayWidth) : int` dan `TaskDrag.Apply(DragEdge, DateOnly start, DateOnly end, int days) : (DateOnly Start, DateOnly End)`
  - `record struct TimelineSpan(DateOnly First, DateOnly Last)` dengan `Days`
  - `TimelineRange.Of(tasks, today) : TimelineSpan`, `TimelineRange.Home(tasks, today, span, dayWidth, viewWidth) : double`, `TimelineRange.Clamp(offset, span, dayWidth, viewWidth) : double`
  - `ProjectText.Meta(tasks)`, `Short(date)`, `DragLabel(start, end)`, `Accessible(task, today)`, `Month(date, thisYear)`, `NameLimit = 60`, `TaskLimit = 80`

- [ ] **Step 1: Tes gagal**

```csharp
public class TaskDragTests
{
    private static readonly DateOnly Mon = new(2026, 11, 2), Fri = new(2026, 11, 6);

    [Theory]
    [InlineData(14, 0)] [InlineData(15, 1)] [InlineData(-15, -1)] [InlineData(61, 2)]
    public void Pixels_round_to_the_nearest_day(double px, int days) => Assert.Equal(days, TaskDrag.Days(px, 30));

    [Fact]
    public void Move_shifts_both_dates() => Assert.Equal((Mon.AddDays(2), Fri.AddDays(2)), TaskDrag.Apply(DragEdge.Move, Mon, Fri, 2));

    [Fact]
    public void Start_never_passes_the_end() => Assert.Equal((Fri, Fri), TaskDrag.Apply(DragEdge.Start, Mon, Fri, 9));

    [Fact]
    public void End_never_passes_the_start() => Assert.Equal((Mon, Mon), TaskDrag.Apply(DragEdge.End, Mon, Fri, -9));
}

public class TimelineRangeTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);
    private static ProjectTask Task(int from, int to) => new(1, 1, "T", Today.AddDays(from), Today.AddDays(to), 0, null, 0, default);

    [Fact]
    public void Empty_project_spans_around_today() =>
        Assert.Equal(new TimelineSpan(Today.AddDays(-35), Today.AddDays(60)), TimelineRange.Of([], Today));

    [Fact]
    public void Span_covers_the_tasks_and_today() =>
        Assert.Equal(new TimelineSpan(Today.AddDays(-45), Today.AddDays(80)), TimelineRange.Of([Task(-10, 2), Task(5, 20)], Today));

    [Fact]
    public void A_project_that_fits_is_centred()
    {
        var tasks = new[] { Task(0, 3) };
        var span = TimelineRange.Of(tasks, Today);
        // Days 0..3 are 4 columns; their middle is 35 + 2 columns from the first day.
        Assert.Equal((35 + 2) * 30 - 250, TimelineRange.Home(tasks, Today, span, 30, 500));
    }

    [Fact]
    public void A_wide_project_puts_today_a_third_in()
    {
        var tasks = new[] { Task(-9, 10) };
        var span = TimelineRange.Of(tasks, Today);
        Assert.Equal(44 * 30 - 565 / 3.0, TimelineRange.Home(tasks, Today, span, 30, 565), 3);
    }

    [Fact]
    public void Scroll_stays_inside_the_span()
    {
        var span = new TimelineSpan(Today, Today.AddDays(99));
        Assert.Equal(0, TimelineRange.Clamp(-40, span, 30, 500));
        Assert.Equal(100 * 30 - 500, TimelineRange.Clamp(99999, span, 30, 500));
    }
}

public class ProjectTextTests
{
    private static readonly DateOnly Today = new(2026, 11, 1);
    private static ProjectTask Task(int from, int to, bool done = false) =>
        new(1, 1, "Timeline page", Today.AddDays(from), Today.AddDays(to), 0, done ? DateTimeOffset.UnixEpoch : null, 0, default);

    [Fact]
    public void Meta_counts_done_and_shows_the_span() =>
        Assert.Equal("1/2 done · 23 Oct – 11 Nov", ProjectText.Meta([Task(-9, -7, done: true), Task(-1, 10)]));

    [Fact]
    public void Meta_without_tasks() => Assert.Equal("No tasks yet", ProjectText.Meta([]));

    [Fact]
    public void Drag_label_for_one_day_and_many() 
    {
        Assert.Equal("Thu 5 Nov", ProjectText.DragLabel(Today.AddDays(4), Today.AddDays(4)));
        Assert.Equal("Thu 5 Nov → Mon 9 Nov · 5 days", ProjectText.DragLabel(Today.AddDays(4), Today.AddDays(8)));
    }

    [Fact]
    public void Accessible_name_says_overdue_or_done()
    {
        Assert.Equal("Timeline page, Sat 31 Oct to Thu 5 Nov", ProjectText.Accessible(Task(-1, 4), Today));
        Assert.Equal("Timeline page, Wed 28 Oct to Sat 31 Oct, overdue", ProjectText.Accessible(Task(-4, -1), Today));
        Assert.Equal("Timeline page, Wed 28 Oct to Sat 31 Oct, done", ProjectText.Accessible(Task(-4, -1, done: true), Today));
    }

    [Fact]
    public void Month_adds_the_year_only_when_it_is_another()
    {
        Assert.Equal("November", ProjectText.Month(new DateOnly(2026, 11, 1), 2026));
        Assert.Equal("January 2027", ProjectText.Month(new DateOnly(2027, 1, 1), 2026));
    }
}
```

- [ ] **Step 2: Jalankan** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Projects"` → gagal kompilasi.
- [ ] **Step 3: Implementasi** (`Gili.Core.Projects`)

```csharp
public sealed record Project(long Id, string Name, int SortOrder, DateTimeOffset CreatedAt);

public sealed record ProjectTask(long Id, long ProjectId, string Title, DateOnly Start, DateOnly End, int Color,
    DateTimeOffset? DoneAt, int SortOrder, DateTimeOffset CreatedAt)
{
    public bool IsDone => DoneAt is not null;
    public int Days => End.DayNumber - Start.DayNumber + 1;
    public bool IsOverdue(DateOnly today) => !IsDone && End < today;
}

public enum DragEdge { Move, Start, End }

public static class TaskDrag
{
    public static int Days(double pixels, double dayWidth) => (int)Math.Round(pixels / dayWidth, MidpointRounding.AwayFromZero);

    public static (DateOnly Start, DateOnly End) Apply(DragEdge edge, DateOnly start, DateOnly end, int days) => edge switch
    {
        DragEdge.Start => (Min(start.AddDays(days), end), end),
        DragEdge.End => (start, Max(end.AddDays(days), start)),
        _ => (start.AddDays(days), end.AddDays(days)),
    };
}

public readonly record struct TimelineSpan(DateOnly First, DateOnly Last) { public int Days => Last.DayNumber - First.DayNumber + 1; }

public static class TimelineRange
{
    public const int Before = 35, After = 60;
    public static TimelineSpan Of(IReadOnlyCollection<ProjectTask> tasks, DateOnly today) => new(
        Min(tasks.Count == 0 ? today : tasks.Min(t => t.Start), today).AddDays(-Before),
        Max(tasks.Count == 0 ? today : tasks.Max(t => t.End), today).AddDays(After));
    public static double X(TimelineSpan span, DateOnly day, double dayWidth) => (day.DayNumber - span.First.DayNumber) * dayWidth;
    public static double Home(...)  // fits (≤ view − 60): centre; else today at a third
    public static double Clamp(double offset, TimelineSpan span, double dayWidth, double viewWidth) =>
        Math.Clamp(offset, 0, Math.Max(0, span.Days * dayWidth - viewWidth));
}
```

`ProjectText` memakai `TodoText.Day` untuk `Thu 5 Nov`, dan `d MMM` (bahasa Inggris) untuk `Short`.

- [ ] **Step 4: Jalankan tes** → lulus.
- [ ] **Step 5: Commit** `feat(projects): task model, timeline range, drag arithmetic and text (F-ISL-34)`.

### Task 3: Penyimpanan

**Files:**
- Modify: `src/Gili.Data/Migrations.cs` (langkah 5)
- Create: `src/Gili.Data/ProjectStore.cs`
- Test: `tests/Gili.Core.Tests/Data/ProjectStoreTests.cs`, `DatabaseTests.cs` (versi 5, tabel baru)

**Interfaces:**
- Produces: `ProjectStore(Database)` dengan `Projects()`, `Tasks()`, `AddProject(Project) : Project`, `RenameProject(long, string)`, `DeleteProject(long)`, `AddTask(ProjectTask) : ProjectTask`, `UpdateTask(ProjectTask)`, `DeleteTask(long)`.

- [ ] **Step 1: Tes gagal**: tambah project, tambah dua task, buka ulang database, semuanya kembali sama; `UpdateTask` mengubah semua kolom; `DeleteProject` ikut menghapus tasknya; `DeleteTask` hanya satu; urutan `sort_order` per project naik berurutan; `DatabaseTests` versi 5 dan tabel `projects`, `project_tasks`.
- [ ] **Step 2: Jalankan** → gagal.
- [ ] **Step 3: Implementasi** langkah 5:

```sql
CREATE TABLE projects (id INTEGER PRIMARY KEY AUTOINCREMENT, name TEXT NOT NULL, sort_order INTEGER NOT NULL, created_at TEXT NOT NULL);
CREATE TABLE project_tasks (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    project_id INTEGER NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
    title TEXT NOT NULL, start_date TEXT NOT NULL, end_date TEXT NOT NULL,
    color INTEGER NOT NULL DEFAULT 0, done_at TEXT, sort_order INTEGER NOT NULL, created_at TEXT NOT NULL);
CREATE INDEX project_tasks_by_project ON project_tasks (project_id, sort_order);
```

`ProjectStore` meniru `TodoStore`. Baris dengan tanggal yang tidak bisa diurai dilewati; `end_date` sebelum `start_date` ditukar; warna dijepit dengan `NoteStyle.ClampColor`.
- [ ] **Step 4: Jalankan** → lulus.
- [ ] **Step 5: Commit** `feat(data): projects and project_tasks tables, migration 5 (F-ISL-34)`.

### Task 4: ProjectService dan ProjectsViewModel

**Files:**
- Create: `src/Gili.App/Island/ProjectService.cs`, `src/Gili.App/Island/ProjectsViewModel.cs`
- Modify: `src/Gili.App/App.xaml.cs`, `src/Gili.App/Island/IslandController.cs` (properti `Projects`)

**Interfaces:**
- Produces: `ProjectService` (`Projects`, `TasksOf(long)`, `Today`, `AddProject(string) : Project`, `RenameProject`, `DeleteProject`, `AddTask(long projectId, string title, DateOnly start, DateOnly end, int color) : ProjectTask`, `UpdateTask(long id, string title, DateOnly start, DateOnly end, int color)`, `Move(long id, DateOnly start, DateOnly end)`, `SetDone(long id, bool)`, `SetColor(long id, int)`, `DeleteTask(long id)`, event `Changed`).
- `ProjectsViewModel` memegang project terpilih (disimpan ke `AppSettings.ProjectId`), status daftar project (terbuka, rename, konfirmasi hapus), dan status form (task yang diedit, isi kolom, warna, konfirmasi hapus, kolom yang tidak valid). Event `ContentChanged` dipakai untuk tinggi pulau.

- [ ] Implementasi, build `dotnet build Gili.slnx`, commit `feat(projects): project service and page view model (F-ISL-34)`.

### Task 5: ProjectTimeline

**Files:**
- Create: `src/Gili.App/Controls/ProjectTimeline.cs`

Perilaku (PRD 6.3 "Halaman Projects"):
- `OnRender`: latar putih 3,5% radius 12; kolom Sabtu–Minggu putih 3,5%; nama bulan di tanggal 1 dengan garis kiri; nama bulan menempel di kiri, didorong nama bulan berikutnya; angka tanggal 11,5 px (akhir pekan `#636366`, lain `#8E8E93`); lingkaran hijau hari ini; garis 1 px putih 10% di y = 42; grid putih 7% tegak per hari dan mendatar per baris mulai y = 46; garis hari ini hijau 55% 2 px. Hanya kolom yang terlihat yang digambar.
- Bar: `Border` anak (radius 12, latar warna task, garis putih 14%) berisi lingkaran centang dan nama; kelas keadaan: terlambat (garis merah 1,5 px, tooltip `Overdue`), selesai (centang hijau, teks 50%, dicoret), terpilih (garis putih 2 px), nama di luar bar bila tidak muat.
- Scroll: `OnMouseWheel` menggulir dan `e.Handled = true`. `ScrollBy(days)` dianimasi 380 ms ease-out (`--out`). `Today()` membawa hari ini ke sepertiga kiri.
- Drag: mouse capture pada timeline; tepi 7/9 px; ambang 4 px; bar mengikuti pointer, bayangan putus-putus di kolom tujuan, label tanggal di atas; scroll otomatis 8 px per frame di 32 px tepi; lepas → event `Moved(taskId, start, end)` lalu bar mendarat dengan pegas `SpringParameters.Resize`.
- Klik tanpa geser → `Opened(taskId)`. Klik lingkaran → `Toggled(taskId)`. Klik baris kosong → `AddRequested(date)`.
- Keyboard per bar (fokus): Space, Enter, Alt+←/→, Alt+Shift+←/→, ↑/↓.
- Animation effects mati: tanpa pegas, tanpa animasi scroll.

- [ ] Implementasi, build, commit `feat(projects): timeline control with grid, scroll and drag (F-ISL-34)`.

### Task 6: ProjectsPage, tab, ikon, demo

**Files:**
- Create: `src/Gili.App/Island/ProjectsPage.xaml`, `ProjectsPage.xaml.cs`
- Modify: `src/Gili.App/Resources/Icons.xaml` (`Icon.Gantt`: `M4 6h9 M8 12h10 M6 18h7`), `src/Gili.App/Island/IslandViewModel.cs` (tab), `src/Gili.App/Island/IslandWindow.xaml.cs` (`CreatePage`, `PageGlow`, `ListUnder`, isi berubah → tinggi), `src/Gili.App/Bar/LeftSide.xaml` (tombol `Tag="Projects"` setelah Todo), `src/Gili.Core/Demo/DemoScript.cs` (`DemoOpen(IslandPage.Projects)`), `tests/Gili.Core.Tests/Demo/*` bila waktunya diuji.

- [ ] Implementasi sesuai mockup: keadaan kosong, baris atas, menu project, form dengan baris warna, dan Esc bertingkat.
- [ ] Build, jalankan semua tes, `dotnet format`, commit `feat(island): Projects page, launcher icon and demo step (F-ISL-34)`.

### Task 7: Verifikasi di app

- [ ] `Gili.exe --quit` di salinan terpasang, `dotnet run --project src/Gili.App`. Cek di pulau tanpa input sintetis (memori "No synthetic input"): screenshot keadaan halaman, lalu minta pengguna mencoba drag.
- [ ] `pwsh scripts/publish.ps1` untuk salinan harian.
- [ ] Commit dokumen (PRD, mockup, spec, rencana) bersama atau sebelum kode.
