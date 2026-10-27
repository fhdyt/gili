# Fase 2e: Todo dan Calendar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Halaman Calendar (bulan, hari ini, titik, daftar tanggal terpilih, Add task dengan jam opsional) dan halaman Todo (`4/5` Tasks done today, centang, Edit, Delete) bekerja dengan data di gili.db, dan tugas berjam memunculkan kartu pengingat (Snooze 10m / Done, Ping sekali, 10 detik, ditahan selama fokus pomodoro).

**Architecture:** Model, teks, kalender bulan, dan jadwal pengingat ada di `Gili.Core.Todos` dan dites. `Gili.Data.TodoStore` menyimpan tabel `todos` (migrasi v2 menambah `due_time`). `Gili.App.Island.TodoService` memegang daftar, satu timer ke pengingat terdekat, dan memunculkan kartu lewat `IslandController`. `CalendarPage` dan `TodoPage` di pulau memakai service yang sama dan `AlarmService` (alarm di daftar tanggal).

**Tech Stack:** .NET 10 WPF, Microsoft.Data.Sqlite, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.3 (halaman 4 Calendar, 5 Todo, "Menambah todo", "Mengubah dan menghapus todo", "Kartu notifikasi" Pengingat todo, prioritas 2: 10 detik), 6.6 (Ping sekali, dibisukan selama fokus), 7.3 F-ISL-16, 7.5 F-TOOL-06/07, 10 (`todos`); mockup CSS `.cal`, `.cev`, `.cadd`, `.tdl`, `.tlist`, `.tact`, `.chk`, JS `buildCal`, `renderCev`, `renderTodos`, `editTodo`, `delTodo`, `CARDS.todo`.

## Global Constraints

- Teks persis: Calendar `October 2026`, kepala hari `Mo Tu We Th Fr Sa Su`, judul daftar `Today` atau `Tue 28 Oct`, kosong `Nothing scheduled.`, item `15:00 · Prepare meeting slides` / `Task · Send client invoice` / `14:30 · Alarm: Take medicine`, tombol `Add task`, kolom `Add a task for this day` (maks. 60) dan `Time`, konfirmasi `Added to Tue 28 Oct` (`· 15:00` bila berjam). Todo: angka `4/5`, `Tasks done today`, kosong `Nothing due today`, tombol konfirmasi `Delete?`. Kartu: judul = judul tugas, subjudul `Due 15:00 today`, tombol `Snooze 10m`, `Done` (utama).
- Jam mengikuti Settings → 12-hour clock; kolom jam menerima format editor alarm (`15:00`, `1500`, `15.00`).
- Kalender mulai Senin; tanggal hari ini latar hijau `#30D158` teks hitam tebal; terpilih `#3A3A3C`; titik 3 px oranye `#FF9F0A` (hitam di hari ini).
- Todo hari ini = tugas tanpa tanggal + tugas bertanggal hari ini (mockup `isToday`). Selesai: latar `#0F2A17`, judul dicoret `#AEAEB2`, lingkaran centang hijau dengan centang hitam.
- Pengingat: kartu prioritas 2 (Reminder) 10 detik, ikon todo hijau, Ping sekali; ditahan selama fase fokus (sudah di `IslandCoordinator`). Jam yang sudah lewat untuk hari ini tetap disimpan tanpa pengingat, kolom jam bergaris kuning `#FFD60A`.
- Tidak ada polling: satu `DispatcherTimer` ke pengingat terdekat; halaman diperbarui lewat event.

## Keputusan yang dibuat di rencana ini

1. **Bulan sebelum/berikutnya**: PRD memintanya, mockup belum punya. Ditambahkan ke mockup lebih dulu: dua tombol chevron 20 px di kanan baris nama bulan. Kembali ke bulan berjalan saat halaman dibuka lagi.
2. **Alarm di Calendar**: daftar tanggal memuat alarm aktif yang berbunyi pada tanggal itu (sekali: tanggal `NextFireAt`-nya; berulang: hari yang cocok). Titik hanya untuk tanggal yang punya todo atau alarm sekali, karena alarm harian akan memberi titik di setiap tanggal.
3. **`Added to …`** tampil sebagai baris kecil di bawah daftar tanggal selama 2,4 detik (pulau sedang terbuka di Calendar, jadi notifikasi wide tidak terlihat); mockup diubah sama.
4. **Pengingat terlewat**: saat start atau bangun, pengingat yang terlewat ≤ 1 jam tetap muncul; yang lebih lama dibuang tanpa kartu.
5. **Kolom `due_time`** (migrasi v2, lokal `HH:mm`) menyimpan jam tampilan; `remind_at` (UTC) hanya pengingat yang masih menunggu, dikosongkan setelah muncul, saat Done, atau saat jam dikosongkan. Snooze 10m mengisi `remind_at` = sekarang + 10 menit tanpa mengubah jam tampilan.
6. **Tab Todo di Tools** (tanggal, urutan seret, filter Today/All/Done) ditunda ke paket Tools (2g); toast pengingat ikut paket toast.
7. **Pulau tidak menutup sendiri** saat kursor keluar selama kolom teks di dalamnya fokus (sama seperti mockup), dan panah kiri/kanan di kolom teks menggerakkan caret, bukan pindah halaman.

## Review Focus

1. **Tengah malam saat pulau terbuka**: `Today`, tanda hari ini, dan daftar Todo pindah ke tanggal baru saat halaman dibuka lagi atau waktu berubah (`TimeChanged`). Dites di Task 1 (`Today_follows_the_clock`).
2. **Judul kosong atau spasi saja**: Enter tidak menyimpan dan fokus tetap di judul. Dites di Task 1 (`Blank_titles_are_refused`).
3. **Jam yang sudah lewat hari ini**: tersimpan, tanpa pengingat, kolom kuning. Dites di Task 1 (`A_past_time_today_has_no_reminder`).
4. **Menghapus atau menyelesaikan tugas yang kartunya tampil**: kartu ditutup. Manual di Task 4.
5. **Bulan dengan 31 hari mulai Minggu (6 baris)**: grid tidak terpotong dan pulau memanjang. Dites di Task 1 (`A_month_starting_on_sunday_needs_six_rows`).

---

### Task 1: Model, teks, kalender, dan jadwal pengingat (Core)

**Files:** Create `src/Gili.Core/Todos/Todo.cs`, `TodoText.cs`, `TodoSchedule.cs`, `CalendarMonth.cs`; Modify `src/Gili.Core/Time/AlarmRules.cs` (+ `public static DateTimeOffset At(DateOnly, TimeOnly, TimeZoneInfo)`, `public static Days DayOf(DayOfWeek)`); Test `tests/Gili.Core.Tests/Todos/*`.

**Interfaces (Produces):**
- `public sealed record Todo(long Id, string Title, DateOnly? DueDate, TimeOnly? DueTime, DateTimeOffset? RemindAt, DateTimeOffset? DoneAt, int SortOrder, DateTimeOffset CreatedAt)`; `bool IsDone`.
- `TodoText`: `Title(string) → string?` (trim, null bila kosong, maks. 60), `Day(DateOnly)` (`Tue 28 Oct`), `Heading(DateOnly date, DateOnly today)` (`Today` / `Tue 28 Oct`), `Month(int year, int month)` (`October 2026`), `CalendarItem(Todo, bool use24)` (`15:00 · …` / `Task · …`), `AlarmItem(Alarm, bool use24)` (`14:30 · Alarm: …`), `Added(DateOnly, TimeOnly?, bool use24)`, `Count(IReadOnlyList<Todo> today)` (`4/5`), `CardSubtitle(Todo, DateOnly today, bool use24)` (`Due 15:00 today` / `Due 15:00 Tue 28 Oct`).
- `CalendarMonth.Cells(int year, int month) → (int Leading, int Days)` (Senin pertama), `Rows(...)`.
- `TodoSchedule`: `Arm(Todo, DateTimeOffset now, TimeZoneInfo)` (berjam dan di masa depan → `RemindAt`; lainnya null), `IsPastToday(DateOnly?, TimeOnly?, now, zone)`, `Due(IReadOnlyList<Todo>, now) → IReadOnlyList<Todo>` (≤ 1 jam terlewat), `Stale(..., now)` (lebih lama dari 1 jam), `Snooze(Todo, now)` (+10 menit), `Today(IReadOnlyList<Todo>, DateOnly today)` (urut `SortOrder`), `On(IReadOnlyList<Todo>, DateOnly)`, `Next(IReadOnlyList<Todo>)`, `AlarmsOn(IReadOnlyList<Alarm>, DateOnly, TimeZoneInfo)`, `HasDot(...)`.
- [ ] Tes gagal → implementasi → lulus, termasuk `Today_follows_the_clock`, `Blank_titles_are_refused`, `A_past_time_today_has_no_reminder`, `A_month_starting_on_sunday_needs_six_rows`, `Reminders_late_by_more_than_an_hour_are_dropped`, `Done_todos_never_remind`, `Repeating_alarms_list_on_matching_days_but_give_no_dot`.
- [ ] Commit `feat(core): todo model, calendar month, texts and reminder schedule (F-ISL-16, F-TOOL-07)`.

### Task 2: Penyimpanan todo (Data)

**Files:** Modify `src/Gili.Data/Migrations.cs` (+ step 2 `ALTER TABLE todos ADD COLUMN due_time TEXT`); Create `src/Gili.Data/TodoStore.cs`; Test `tests/Gili.Core.Tests/Data/TodoStoreTests.cs`, migrasi.
- `TodoStore(Database)`: `All()` (urut `sort_order, id`), `Add(Todo) → Todo` (sort_order = maks + 1), `Update(Todo)`, `Delete(long)`. `due_date` `yyyy-MM-dd`, `due_time` `HH:mm`, waktu UTC `o`.
- [ ] Tes: `Added_todo_survives_a_restart`, `Update_changes_every_field`, `Delete_removes_it`, `New_todos_go_last`, `A_version_1_database_gains_due_time`.
- [ ] Commit `feat(data): todos kept in gili.db with their local time (F-TOOL-06)`.

### Task 3: Mockup dan PRD

- [ ] Mockup: tombol bulan sebelum/berikutnya (`#calPrev`, `#calNext`, `aria-label` `Previous month` / `Next month`), Calendar bisa menampilkan bulan lain (hari ini hanya ditandai di bulan berjalan), baris `Added to …` di `.cev` 2,4 detik menggantikan `hint()`. `node --check`, potret `calendar-page` dan `todo-page` di `mockup-shot.ps1`.
- [ ] PRD 6.3 halaman 4 (tombol bulan dan baris `Added to`), PRD 10 (`due_time`).
- [ ] Commit `docs: calendar month buttons and the Added line in the mockup and PRD`.

### Task 4: TodoService, halaman Calendar dan Todo, kartu pengingat

**Files:** Create `src/Gili.App/Island/TodoService.cs`, `CalendarPage.xaml(.cs)`, `CalendarViewModel.cs`, `TodoPage.xaml(.cs)`, `TodoViewModel.cs`; Modify `IslandController.cs` (kartu `todo:<id>` Reminder 10 s), `SoundRules.cs` (+ `todo:*` → Ping sekali) dan tesnya, `IslandWindow.xaml.cs` (halaman, tinggi mengikuti isi, kolom teks dan Esc, away-timer), `IslandViewModel.cs` (tab tanpa placeholder), `App.xaml.cs` (`TimeChanged` → `Reschedule`), demo (`DemoReminder`) dan tesnya, `Icons.xaml` (+ Pencil, Trash, Plus, ChevronLeft, ChevronRight).
- `TodoService(TodoStore?)`: `Todos`, `Changed`, `Add(string title, DateOnly date, TimeOnly? time) → Todo`, `Update(long id, string title, TimeOnly? time)`, `SetDone(long, bool)`, `Delete(long)`, `Snooze(long)`, `Reschedule()`, `event Action<Todo>? Reminding`, `event Action<long>? Stale`.
- [ ] Tes Core: `Todo_reminders_ping_once` (`SoundRules.ForCard("todo:3", …) == ("Ping", false, false)`), demo `A_todo_reminder_shows_and_closes_itself`.
- [ ] Halaman Calendar (mockup `.cal` 7 × 30 px, jarak 2, tombol hari 19 px radius 6, 11 px; `.cev` 270 px: judul 14 px semibold, item `#1C1C1E` radius 10 padding 8 12 12 px, `Add task` pill kecil 30 px dengan ikon plus 14 px; form: kolom 32 px radius 16, kolom jam 86 px, tombol centang putih 32 px).
- [ ] Halaman Todo (`.tdl` 250 px: 46 px tebal, 15 px abu; `.tlist` 290 px: baris min. 44 px radius 12 padding 0 12 15 px, jarak 6; centang 24 px garis 2 `#636366`; jam 12 px abu; tombol pensil/sampah 28 px muncul saat hover/fokus; `Delete?` merah 12 px semibold 3 detik; edit di tempat).
- [ ] Manual: tambah tugas hari ini berjam 2 menit lagi → muncul di Todo, kartu pengingat tepat waktu dengan Ping, menutup sendiri setelah 10 detik; Snooze 10m; Done mencentang; tugas besok → `Added to …` dan titik; edit dan hapus; bulan berikutnya/sebelumnya; potret dibandingkan dengan mockup (PrintWindow jendela Gili saja).
- [ ] Commit `feat(island): Calendar and Todo pages with Add task, edit, delete and reminder cards (F-ISL-07, F-ISL-16, F-TOOL-07)`.

### Task 5: Verifikasi

- [ ] Release build 0 warning, tes, format; publish dan `package.ps1 -Install`; CPU diam < 1%; catat di ledger.
