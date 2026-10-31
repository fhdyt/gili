# Fase 2g: Sisa Jendela Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Empat tab Tools yang masih `Coming in a later version` bekerja: Timer (beberapa timer berlabel jalan bersamaan, F-TOOL-01), Stopwatch (lap, F-TOOL-02), Pomodoro (durasi bisa diubah, F-TOOL-03), dan Todo (tanggal, urutan seret, filter Today / All / Done, F-TOOL-06). Semuanya tetap benar setelah restart.

**Architecture:** Aturan baru ada di Core dan dites: `LabeledTimer`, parser durasi, lap stopwatch, `PomodoroPlan`, teks baris dan kartu, filter dan tanggal todo, serta `ListOrder` (dipindah dari `NoteOrder`). `TimerStore` menyimpan timer berlabel sebagai baris `timers` dengan `label` tidak kosong (baris pulau tetap `label = ''`) dan lap di kolom `stopwatch.laps`; `TodoStore` mendapat `Reorder`. `TimerService` dan `TodoService` tetap satu-satunya pemilik data; tab Tools hanya ViewModel di atasnya, dibuat saat jendela dibuka dan dilepas saat ditutup.

**Tech Stack:** .NET 10 WPF, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 7.5 (Tools, F-TOOL-01/02/03/06), 6.3 ("Isi compact" urutan 4–5, "Kartu notifikasi", "Membuka pulau", "Mengubah dan menghapus todo"), 6.6 (suara timer), 10 (`timers`, `stopwatch`, `todos`). Mockup: CSS `.toolw`, `.aedit`, `.tin`, `.srow`, `.seg`, `.tbtn`; JS `renderTools`, `TOOL_TABS`, `CARDS`, `slotValue`. Rencana sebelumnya: 2b (keputusan 1), 2d (Task 4), 2e (keputusan 6).

## Global Constraints

- Teks persis (bahasa Inggris). Timer: `New timer`, `No timers yet`, editor `New timer` / `Edit timer`, kolom `Label` (maks. 40, placeholder `Timer`) dan `Duration` (placeholder `5:00`), detail baris `Ready · 10:00` / `Running · 10:00` / `Paused · 10:00`, tombol `Start` / `Pause` / `Resume`, `Reset`, `Edit`, `Delete` → `Click again to delete` (3 detik), kartu judul `Tea done` dengan sub `Timer 10:00` dan tombol `Repeat` / `Close`. Stopwatch: `Start` / `Pause` / `Resume`, `Lap`, `Reset`, baris `Lap 3`, `No laps yet`. Pomodoro: sub `Focus · session 1 of 4` (sama dengan pulau), judul bagian `Lengths`, baris `Focus` / `Short break` / `Long break` (`25 min`) dan `Long break after` (`4 sessions`), tombol stepper bernama aksesibel `Decrease Focus` / `Increase Focus` dan seterusnya. Todo: segmen `Today` / `All` / `Done`, kosong `No tasks for today` / `No tasks yet` / `No finished tasks`, catatan `New tasks are added from Calendar in the island.`, editor `Edit task` dengan kolom `Title` (maks. 60), `Date` (placeholder `No date`), `Time` (placeholder `No time`), tanggal baris `Today` / `Tomorrow` / `Tue 28 Oct`, ditambah ` · 15:00` bila ada jam, `No date`, dan `Overdue` (merah `#FF453A`) untuk tugas belum selesai yang tanggalnya sudah lewat.
- Tidak ada tombol tambah todo di Tools: todo hanya dibuat dari Calendar (keputusan final di CLAUDE.md).
- Nilai: maksimal 10 timer berlabel; durasi 1 detik sampai 23:59:59; pomodoro default 25 / 5 / 15 menit dan long break setelah 4 sesi, rentang fokus 1–120 menit, istirahat 1–60 menit, sesi 2–8; maksimal 99 lap.
- Tata letak mengikuti tab Alarms yang sudah ada: judul tab 18 px semibold dengan tombol di kanan, editor `.aedit` di atas daftar (kolom nama 76 px, jarak baris 10), baris latar `#0AFFFFFF` radius 8 padding 12,6 jarak 4, angka besar 44 px bold digit tabular (Stopwatch dan Pomodoro), angka baris 17 px semibold digit tabular.
- Gerak di Tools: baris todo yang diseret terangkat (skala 1,02, bayangan) dan mengikuti pointer; baris lain bergeser 200 ms `BezierEase.Out`; lepas mendarat 200 ms. Animation effects mati: tanpa geser dan tanpa angkat.
- Anggaran PRD 8.1: tidak ada polling baru. Tab Timer dan Pomodoro memakai `TimerService.Changed` (tick 1 detik yang sudah ada, hanya selama ada yang berjalan); tab Stopwatch punya satu `DispatcherTimer` 100 ms yang hanya jalan selama tab itu tampil dan stopwatch berjalan. Semua langganan dilepas saat Tools ditutup.

## Keputusan yang dibuat di rencana ini

1. **Pulau tetap satu timer** (disetujui pengguna 2026-10-29). Timer berlabel hanya ada di Tools. Halaman Timer di pulau tidak berubah.
2. **Slot** menampilkan pomodoro bila berjalan (aturan lama). Kalau tidak, slot menampilkan timer yang paling cepat habis di antara timer pulau dan timer berlabel. Klik slot saat yang tampil timer berlabel membuka Tools di tab Timer dan pulau tetap compact; timer pulau tetap membuka halaman Timer. PRD 6.3 "Membuka pulau" diperbarui di Task 1.
3. **Kartu per timer berlabel** dengan id `timer:<id>`, prioritas Urgent, dan suara timer berulang seperti kartu `timer`. Beberapa timer yang habis di detik yang sama memunculkan kartu berurutan, yang paling dulu habis tampil lebih dulu. Mengedit durasi atau menghapus timer yang kartunya tampil menutup kartu itu dan menghentikan suaranya.
4. **Label kosong** disimpan sebagai `Timer`, supaya baris berlabel tidak pernah bentrok dengan baris pulau (`label = ''`). Mengubah label tidak menghentikan timer. Mengubah durasi mengembalikan timer ke keadaan siap.
5. **Stopwatch dan pomodoro di Tools sama dengan yang di pulau** (satu baris di database). Lap hanya di Tools, karena mockup halaman pulau tidak punya tombol Lap. Lap disimpan sebagai waktu total dalam ms, dan Reset menghapus semua lap.
6. **Durasi pomodoro disimpan di `settings.json`** (`AppSettings`), bukan di `timers`. Perubahan berlaku untuk fase berikutnya. Fase yang sedang berjalan atau dijeda tidak berubah, sedangkan fase yang belum dimulai langsung memakai durasi baru. Menurunkan jumlah sesi di bawah sesi yang sedang berjalan membuat fokus itu berakhir dengan long break.
7. **Filter Today** sama dengan halaman Todo pulau (`TodoSchedule.Today`: tanpa tanggal atau jatuh tempo hari ini), **All** berisi semua tugas, **Done** berisi tugas selesai. Semuanya dalam urutan pengguna. Seret dan Alt + ↑/↓ hanya di Today dan All. Memindahkan tugas di daftar yang tersaring menaruhnya di posisi tugas tujuan dalam urutan penuh, sama seperti Notes, sehingga tugas yang tersembunyi tetap di tempatnya.
8. **Kolom Date** menerima `yyyy-MM-dd`, `d MMM`, `d MMM yyyy`, `ddd d MMM` (bentuk yang tampil), `today`, dan `tomorrow`, tanpa membedakan huruf besar/kecil. Tanpa tahun berarti tahun ini, supaya tanggal lewat yang disimpan ulang tidak loncat ke tahun depan. Kosong berarti tanpa tanggal; isi yang tidak valid memberi garis merah dan mematikan Save. Jam memakai parser editor alarm; jam yang sudah lewat hari ini mendapat garis kuning (`TodoSchedule.IsPastToday`).
9. **`NoteOrder` dipindah menjadi `Gili.Core.Layout.ListOrder`**, karena todo memakai aturan yang sama.
10. **Demo**: timer demo diberi label `Tea`, sehingga skenario memperlihatkan kartu berlabel (`Tea done` · `Timer 00:10`). Kartu timer pulau tidak berubah tampilannya selain judul.

## Review Focus

1. **Dua timer berlabel habis di detik yang sama**: dua kartu antre, masing-masing dengan labelnya, dan Close yang pertama menampilkan yang kedua. Dites di Task 2 (`Timers_ending_together_finish_in_end_order`).
2. **Durasi pomodoro diubah saat fase berjalan, atau jumlah sesi diturunkan di sesi 4**: fase berjalan tidak berubah, dan fokus sesi 4 dengan sesi = 3 berakhir dengan long break. Dites di Task 2 (`A_plan_change_leaves_a_running_phase_alone`, `Lowering_rounds_below_the_session_ends_in_a_long_break`).
3. **Mengurutkan di filter Today saat ada tugas hari lain di antaranya**: tugas tersembunyi tidak bergeser relatif satu sama lain. Dites di Task 3 (`Moving_in_a_filtered_view_keeps_hidden_items_in_place`).
4. **Input durasi dan tanggal yang aneh** (`0`, `24:00:00`, `1:60`, `abc`, `31 Feb`, `30 oct`): input yang salah mematikan Save tanpa crash, dan `30 oct` diterima. Dites di Task 2 (`Duration_input_parses_loosely_and_rejects_nonsense`) dan Task 3 (`Date_input_reads_the_shown_form_and_rejects_nonsense`).
5. **Stopwatch dengan lap lewat restart dan lebih dari 99 menit**: lap tetap ada setelah restart, dan `100:00.0` tampil utuh. Dites di Task 4 (`Laps_survive_a_restart`) dan Task 2 (`Stopwatch_past_99_minutes_keeps_every_digit`).

---

### Task 1: Mockup dan PRD

**Files:** Modify `docs/mockup/top-taskbar-mockup.html` (`renderTools` untuk Timer, Stopwatch, Pomodoro, Todo; kartu contoh `Tea done` di `CARDS`; klik slot timer berlabel membuka Tools), `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md` (7.5 isi keempat tab dan Keputusan 1–8; 6.3 "Membuka pulau" Keputusan 2; 6.3 "Kartu notifikasi" kartu timer berlabel), `scripts/mockup-shot.ps1` (+ state `tools-timer`, `tools-stopwatch`, `tools-pomodoro`, `tools-todo`).

- [ ] **Step 1:** Tab Timer di mockup: `New timer` di judul, editor `.aedit` (Label, Duration, Cancel/Save; Save mati bila durasi tidak valid), baris contoh `Tea` 03:12 running, `Laundry` 45:00 paused, dan `Pasta` 10:00 ready. Setiap baris berisi angka, label + detail, Start/Pause/Resume, Reset, Edit, dan Delete dua klik.
- [ ] **Step 2:** Tab Stopwatch (angka 44 px, Start/Lap/Reset, daftar lap terbaru di atas: `Lap n`, split, total) dan tab Pomodoro (sub fase, angka 44 px, Start/Reset, bagian `Lengths` dengan empat baris stepper `−` nilai `+`).
- [ ] **Step 3:** Tab Todo: segmen filter, catatan Calendar, baris (centang, judul, tanggal di kanan, Edit, Delete dua klik), editor `Edit task`, seret baris dan Alt + ↑/↓. Kartu `Tea done` di `CARDS` dan tombol pemicunya di panel kontrol mockup.
- [ ] **Step 4:** `node --check` pada skrip yang diekstrak lolos; probe Edge tanpa error konsol; empat state baru di `mockup-shot.ps1` menghasilkan PNG. PRD diperbarui sesuai Files.
- [ ] **Step 5: Commit** `docs: Tools tabs for timers, stopwatch laps, pomodoro lengths and todos in the mockup and PRD`.

### Task 2: Timer berlabel, lap, dan rencana pomodoro (Core)

**Files:** Modify `src/Gili.Core/Timers/TimerModels.cs`, `TimerText.cs`, `src/Gili.Core/Sound/SoundRules.cs`, `src/Gili.Core/Settings/AppSettings.cs`, `src/Gili.Core/Demo/DemoScript.cs`; Test `tests/Gili.Core.Tests/Timers/TimerModelsTests.cs`, `TimerTextTests.cs`, `tests/Gili.Core.Tests/Sound/*`, `tests/Gili.Core.Tests/Settings/*`, `tests/Gili.Core.Tests/Demo/*`.

**Interfaces (Produces):**
- `public sealed record LabeledTimer(long Id, string Label, CountdownTimer Clock)` dengan `const int MaxCount = 10`, `const int LabelLimit = 40`, dan `static string CleanLabel(string text)` (trim, maks. 40, kosong → `Timer`).
- `public static class TimerSchedule`: `IReadOnlyList<long> Finished(IReadOnlyList<LabeledTimer> timers, DateTimeOffset now)` (id yang sudah habis, urut `EndsAt` lalu id) dan `CountdownTimer? Soonest(IEnumerable<CountdownTimer> running, DateTimeOffset now)` (yang `EndsAt`-nya paling awal; null bila tidak ada yang berjalan).
- `StopwatchState(DateTimeOffset? StartedAt, long AccumulatedMs, IReadOnlyList<long> Laps)` dengan `const int MaxLaps = 99`, `Lap(DateTimeOffset now)` (hanya saat berjalan dan < 99; menambah total ms), `bool CanLap`; `Reset()` mengosongkan lap. Kesamaan record membandingkan isi lap (`SequenceEqual`).
- `public sealed record PomodoroPlan(int FocusMinutes, int BreakMinutes, int LongBreakMinutes, int Rounds)` dengan `Default = (25, 5, 15, 4)`, `Normalize()` (rentang di Global Constraints), dan `TimeSpan Length(PomodoroPhase)`.
- `PomodoroState`: konstanta statis panjang dan `Rounds` dihapus; diganti `static PomodoroState Initial(PomodoroPlan)`, `Reset(PomodoroPlan)`, `NextPhase(DateTimeOffset now, bool startNow, PomodoroPlan plan)`, `Skip(DateTimeOffset now, PomodoroPlan plan)`, dan `WithPlan(PomodoroPlan plan)` (Keputusan 6: fase yang tidak berjalan dan tidak dijeda mendapat panjang baru; `Round` dijepit ke `plan.Rounds`).
- `TimerText`: `PomodoroSub`, `FocusDone`, dan `BreakOver` mendapat parameter `PomodoroPlan plan`; tambahan `Long(TimeSpan)` (`h:mm:ss` mulai 1 jam, selain itu `Clock`), `int? ParseDuration(string)`, `DurationInput(int seconds)` (`5:00`, `1:00:00`), `RowDetail(CountdownTimer)` (`Ready · 10:00` dan seterusnya, total lewat `Long`), `LabeledDone(string label)` (`Tea done`), `LabeledSub(int totalSeconds)` (`Timer 10:00`), `LapName(int n)`, `Minutes(int)` (`25 min`), dan `Sessions(int)` (`4 sessions`).
- `SoundRules.ForCard`: id berawalan `timer:` diperlakukan seperti `"timer"`.
- `AppSettings`: `PomodoroFocus = 25`, `PomodoroBreak = 5`, `PomodoroLongBreak = 15`, `PomodoroRounds = 4`, properti `PomodoroPlan Pomodoro`, dan `Normalize()` yang menjepit lewat `PomodoroPlan.Normalize`.
- `DemoTimer(int Seconds, string? Label = null)`; langkah demo memakai `Label = "Tea"`.

- [ ] **Step 1: Tes yang gagal**: `Timers_ending_together_finish_in_end_order`, `Soonest_is_the_earliest_end_and_ignores_paused`, `Empty_label_reads_timer_and_long_labels_are_cut`, `Duration_input_parses_loosely_and_rejects_nonsense` (`"5"` → 300, `"90"` → 5400, `"1:30"` → 90, `"1:00:00"` → 3600, `" 2:05 "` → 125; `""`, `"0"`, `"0:00"`, `"1:60"`, `"24:00:00"`, `"abc"`, `"-5"` → null), `Duration_input_round_trips`, `Long_shows_hours_from_one_hour`, `Row_detail_says_ready_running_or_paused`, `Labeled_card_texts`, `Lap_adds_the_total_and_only_while_running`, `Laps_stop_at_99`, `Reset_clears_laps`, `Stopwatch_past_99_minutes_keeps_every_digit` (`100:00.0`), `Plan_lengths_drive_each_phase`, `A_plan_change_leaves_a_running_phase_alone`, `A_plan_change_resizes_an_unstarted_phase`, `Lowering_rounds_below_the_session_ends_in_a_long_break`, `Plan_is_clamped_to_its_ranges`, `Pomodoro_texts_follow_the_plan` (`Focus · session 1 of 3`, `20-minute break · session 3 of 3`), `Labeled_timer_cards_ring_like_the_timer`, `Settings_keep_pomodoro_lengths_and_clamp_them`; perbarui tes demo agar langkah timer berlabel `Tea`.
- [ ] **Step 2:** Jalankan `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Timers|FullyQualifiedName~Sound|FullyQualifiedName~Settings|FullyQualifiedName~Demo"`. Hasil yang diharapkan: gagal kompilasi/tes.
- [ ] **Step 3:** Implementasi sesuai Interfaces. Perbaiki pemanggil lama (`TimerService`, `TimerViewModel`, `IslandController`, `IslandViewModel`) dengan `PomodoroPlan.Default` sementara; Task 5 menyambungkannya ke settings.
- [ ] **Step 4:** Semua tes lulus; `dotnet build Gili.slnx` 0 warning.
- [ ] **Step 5: Commit** `feat(core): labeled timers, stopwatch laps and pomodoro lengths (F-TOOL-01..03)`.

### Task 3: Filter, tanggal, dan urutan todo (Core)

**Files:** Move `src/Gili.Core/Notes/NoteOrder.cs` → `src/Gili.Core/Layout/ListOrder.cs` (perbarui `NoteDrag`, `NotesViewModel`, dan tesnya); Modify `src/Gili.Core/Todos/TodoText.cs`, `TodoSchedule.cs`; Test `tests/Gili.Core.Tests/Layout/ListOrderTests.cs` (dari `NoteOrderTests`), `tests/Gili.Core.Tests/Todos/*`.

**Interfaces (Produces):**
- `Gili.Core.Layout.ListOrder.Move` / `Neighbor`: perilaku `NoteOrder` yang sama.
- `public enum TodoFilter { Today, All, Done }`; `TodoSchedule.Filter(IReadOnlyList<Todo>, TodoFilter, DateOnly today) → IReadOnlyList<Todo>` (Keputusan 7, urut `SortOrder` lalu id).
- `TodoText.TryDate(string text, DateOnly today, out DateOnly? date) → bool` (Keputusan 8), `DateInput(DateOnly? date)` (`Thu 30 Oct` atau kosong), `Due(Todo todo, DateOnly today, bool use24Hour)` (`Today · 15:00`, `Tomorrow`, `Tue 28 Oct`, `No date`), `IsOverdue(Todo, DateOnly today)`, `Empty(TodoFilter)`, dan konstanta `CalendarNote`.

- [ ] **Step 1: Tes yang gagal**: `Moving_in_a_filtered_view_keeps_hidden_items_in_place` (urutan penuh `[1,2,3,4,5]`, terlihat `[1,3,5]`, pindahkan 5 ke posisi 3 → `[1,2,5,3,4]`), `Filters_split_today_all_and_done`, `Date_input_reads_the_shown_form_and_rejects_nonsense` (`"2026-10-30"`, `"30 Oct"`, `"30 oct"`, `"Thu 30 Oct"`, `"30 Oct 2027"`, `"today"`, `"Tomorrow"` diterima; `"31 Feb"`, `"abc"`, `"30/10"` → false; `""` → true dan null), `A_date_without_a_year_stays_in_this_year` (`"20 Oct"` pada 29 Okt 2026 → 2026-10-20), `Due_reads_today_tomorrow_or_the_day_with_its_time`, `Overdue_only_for_open_tasks_before_today`, `Empty_texts_per_filter`; tes `NoteOrder` lama dipindah apa adanya.
- [ ] **Step 2:** Jalankan `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Todos|FullyQualifiedName~Layout|FullyQualifiedName~Notes"`. Hasil yang diharapkan: gagal.
- [ ] **Step 3:** Implementasi. Pemindahan di daftar tersaring memakai `ListOrder.Move(full, id, full.IndexOf(target))`, cara yang sama seperti `NotesViewModel`. Cara ini benar ke dua arah: `[1,2,3,4,5]`, 1 ke posisi 3 → `[2,3,1,4,5]`. Tambahkan kasus ke atas dan ke bawah itu ke tes.
- [ ] **Step 4:** Semua tes lulus; build 0 warning.
- [ ] **Step 5: Commit** `feat(core): todo filters, due dates typed loosely, and one list order for notes and todos (F-TOOL-06)`.

### Task 4: Penyimpanan (Data)

**Files:** Modify `src/Gili.Data/TimerStore.cs`, `src/Gili.Data/TodoStore.cs`; Test `tests/Gili.Core.Tests/Data/TimerStoreTests.cs`, `TodoStoreTests.cs`, `SettingsStoreTests.cs`.

**Interfaces (Produces):**
- `TimerStore`: `IReadOnlyList<LabeledTimer> LoadLabeled()` (baris `kind = 'timer' AND label <> ''`, urut id), `LabeledTimer AddLabeled(string label, CountdownTimer clock)`, `void UpdateLabeled(LabeledTimer)`, `void DeleteLabeled(long id)`. `LoadStopwatch` dan `SaveStopwatch` membaca dan menulis `laps` sebagai array JSON angka (`System.Text.Json`); JSON rusak dibaca sebagai tanpa lap. `LoadPomodoro(PomodoroPlan plan)` memakai `PomodoroState.Initial(plan)` bila belum ada baris.
- `TodoStore.Reorder(IReadOnlyList<long> ids)`: menulis `sort_order` 0..n−1 dalam satu transaksi, seperti `NoteStore.Reorder`.

- [ ] **Step 1: Tes yang gagal**: `Labeled_timers_survive_a_restart`, `Island_timer_and_labeled_timers_stay_apart` (menyimpan timer pulau tidak menghapus baris berlabel, dan sebaliknya), `Update_and_delete_a_labeled_timer`, `Laps_survive_a_restart`, `Broken_laps_read_as_none`, `Todo_reorder_writes_every_place`, `Pomodoro_lengths_survive_a_restart` (settings).
- [ ] **Step 2–4:** Gagal, lalu implementasi, lalu lulus (`--filter "FullyQualifiedName~Data"`).
- [ ] **Step 5: Commit** `feat(data): labeled timers, stopwatch laps and todo order kept in gili.db (F-TOOL-01, F-TOOL-02, F-TOOL-06)`.

### Task 5: TimerService, TodoService, dan pulau

**Files:** Modify `src/Gili.App/Island/TimerService.cs`, `TodoService.cs`, `IslandController.cs`, `IslandViewModel.cs`, `TimerViewModel.cs`, `TodoViewModel.cs`, `src/Gili.App/DemoService.cs`, `src/Gili.App/App.xaml.cs`.

**Interfaces (Produces):**
- `TimerService(TimerStore? store, Func<PomodoroPlan> plan)`: `IReadOnlyList<LabeledTimer> Labeled`, `bool CanAddTimer`, `LabeledTimer? AddTimer(string label, int seconds)`, `void EditTimer(long id, string label, int seconds)` (Keputusan 4), `void DeleteTimer(long id)`, `void StartOrPause(long id)`, `void ResetTimer(long id)`, `void RepeatTimer(long id)`, `void Lap()`, `void StartOrPauseStopwatch()`, `void ResetStopwatch()`, `void StartOrPausePomodoro()`, `void ResetPomodoro()`, `void ApplyPlan()` (dipanggil saat settings berubah), `CountdownTimer? SoonestTimer` dan `LabeledTimer? SoonestLabeled` (untuk slot dan klik), dan `event Action<string>? Stale` (id kartu yang tidak berlaku lagi). `Finished` mengirim `timer:<id>` untuk timer berlabel. `AnythingRunning` dan `Arm` ikut menghitung timer berlabel. Timer demo berlabel `Tea` tidak pernah disimpan.
- `TodoService`: `void Edit(long id, string title, DateOnly? date, TimeOnly? time)` (menjadwal ulang pengingat dan memicu `Stale`) dan `void Reorder(IReadOnlyList<long> ids)`.
- `IslandController`: kartu `timer:<id>` (ikon jam pasir oranye, judul `LabeledDone`, sub `LabeledSub`, `Repeat` / `Close`); `Timers.Stale` → `DismissCard`; aktivitas Timer berjalan bila ada timer berlabel yang berjalan; klik slot saat `SoonestLabeled` tampil → `ToolsRequested("Timer")` tanpa membuka pulau (Keputusan 2).

- [ ] **Step 1:** `TimerService` dan `TodoService` sesuai Interfaces. `Reschedule` memakai `TimerSchedule.Finished` untuk timer berlabel; habis → clock di-`Reset`, disimpan, lalu `Finished`. `ApplyPlan` dipasang ke `SettingsHub.Changed` di `App.xaml.cs`.
- [ ] **Step 2:** Pulau: slot memakai `SoonestTimer` (pomodoro tetap menang); kartu dan klik slot sesuai Interfaces; teks pomodoro di halaman dan kartu memakai plan dari settings; demo memakai label `Tea`.
- [ ] **Step 3:** Build 0 warning, semua tes lulus. Manual: buat dua timer berlabel lewat kode sementara atau setelah Task 6. Timer 10 detik dan 12 detik memunculkan dua kartu berurutan dengan suara timer; slot menampilkan yang paling cepat habis; mode demo menampilkan `Tea done`.
- [ ] **Step 4: Commit** `feat(island): labeled timers in the slot with their own cards, and pomodoro lengths from settings (F-TOOL-01, F-TOOL-03)`.

### Task 6: Tab Timer, Stopwatch, dan Pomodoro di Tools

**Files:** Create `src/Gili.App/Tools/TimersTabViewModel.cs` (+ `TimerEditorViewModel`, `ToolTimerRowViewModel`), `StopwatchTabViewModel.cs`, `PomodoroTabViewModel.cs`; Modify `ToolsViewModel.cs` (properti per tab, `IsTimer` / `IsStopwatch` / `IsPomodoro` / `IsTodo`, dan `Detach` untuk semuanya), `ToolsWindow.xaml(.cs)` (isi tab dan fokus editor seperti Alarms), `App.xaml.cs` (`ShowTools` membuat semua tab).

**Interfaces:**
- Consumes: `TimerService` (Task 5), `TimerText` (Task 2), `SettingsHub.Update` untuk durasi pomodoro.
- `TimerEditorViewModel(LabeledTimer? existing, Action<string, int> save, Action cancel)`: `Title` (`New timer` / `Edit timer`), `Label`, `DurationText`, `IsDurationInvalid`, `SaveCommand` (aktif bila durasi valid), `CancelCommand`. `New timer` mati saat editor terbuka atau saat sudah ada 10 timer.
- `ToolTimerRowViewModel`: `Time` (`TimerText.Long(Remaining)`), `Label`, `Detail`, `ButtonText`, `StartCommand`, `ResetCommand`, `EditCommand`, `DeleteCommand` (dua klik, pola `ToolAlarmRowViewModel`).
- `StopwatchTabViewModel`: `Time` (`mm:ss.t`), `ButtonText`, `CanLap`, `Laps` (terbaru di atas: nama, split, total), `IsEmpty`, `SetVisible(bool)` (menyalakan tick 100 ms hanya saat tampil dan berjalan).
- `PomodoroTabViewModel`: `Sub`, `Time`, `ButtonText`, empat baris stepper (`Name`, `ValueText`, `DecreaseCommand`, `IncreaseCommand` yang mati di batas rentang).

- [ ] **Step 1:** Tab Timer: daftar, editor di atas daftar, dan tombol per baris sesuai mockup `tools-timer`. Waktu diperbarui dari `TimerService.Changed`.
- [ ] **Step 2:** Tab Stopwatch dan Pomodoro sesuai mockup `tools-stopwatch` dan `tools-pomodoro`. Stepper menulis settings; halaman Timer di pulau langsung mengikuti.
- [ ] **Step 3:** Build 0 warning, tes lulus. Manual: tiga timer berlabel jalan bersamaan, jeda, lanjut, reset, edit label saat berjalan (tetap jalan), edit durasi (kembali siap), hapus saat kartunya tampil (kartu dan suara berhenti), lalu restart Gili (sisa waktu benar). Lap lima kali, restart, lap masih ada. Fokus diubah ke 1 menit saat fase belum dimulai, Start, lalu kartu `Focus session done` muncul setelah 1 menit. Bandingkan potret jendela Gili (PrintWindow) dengan mockup ketiga state. Tools ditutup → tick 100 ms berhenti.
- [ ] **Step 4: Commit** `feat(app): Timer, Stopwatch and Pomodoro tabs in Tools (F-TOOL-01..03)`.

### Task 7: Tab Todo di Tools

**Files:** Create `src/Gili.App/Tools/TodoTabViewModel.cs` (+ `TodoEditorViewModel`, `ToolTodoRowViewModel`), `src/Gili.App/Tools/TodoListDrag.cs` (seret baris); Modify `ToolsWindow.xaml(.cs)`, `ToolsViewModel.cs`, `App.xaml.cs`.

**Interfaces:**
- Consumes: `TodoService.Edit` / `SetDone` / `Delete` / `Reorder` (Task 5), `TodoSchedule.Filter`, `TodoText` (Task 3), dan `ListOrder` (Task 3).
- `TodoTabViewModel`: `Filter` (`TodoFilter`, segmen), `Rows`, `EmptyText`, `IsEmpty`, `CanReorder` (Today/All), `Editor`, `bool Move(long id, int delta)` (Alt + ↑/↓), dan `void Drop(long id, long target)`.
- `TodoEditorViewModel(Todo existing, Action<string, DateOnly?, TimeOnly?> save, Action cancel, Func<DateOnly> today)`: `TitleText`, `DateText`, `TimeText`, `IsDateInvalid`, `IsTimeInvalid`, `IsTimePast` (garis kuning), dan `SaveCommand` (aktif bila judul tidak kosong, tanggal valid, dan jam valid).
- `ToolTodoRowViewModel`: `Title`, `Due`, `IsOverdue`, `IsDone` (centang memanggil `SetDone`), `EditCommand`, `DeleteCommand` (dua klik).

- [ ] **Step 1:** Daftar dengan filter, centang, Edit di editor atas daftar, dan Delete dua klik. Perubahan dari pulau (centang, edit, tugas baru dari Calendar) langsung tampil di Tools, dan sebaliknya.
- [ ] **Step 2:** Seret baris di Today dan All: tekan lalu geser 6 px untuk mengangkat, baris lain bergeser saat pointer melewati baris lain, dan saat dilepas `Reorder` dipanggil sekali (gerak di Global Constraints). Alt + ↑/↓ pada baris yang fokus memindahkannya satu langkah. Di filter Done, seret dan Alt + panah tidak melakukan apa-apa.
- [ ] **Step 3:** Build 0 warning, tes lulus. Manual: ubah tanggal tugas hari ini ke besok (hilang dari Today dan dari halaman Todo pulau, titik muncul di Calendar besok), isi jam yang lewat (garis kuning, tanpa pengingat), isi tanggal `31 Feb` (Save mati), seret di Today dengan tugas hari lain di antaranya lalu cek urutan di All, dan Alt + ↑ pada baris pertama tidak melakukan apa-apa. Bandingkan potret dengan mockup `tools-todo`.
- [ ] **Step 4: Commit** `feat(app): Todo tab in Tools with due dates, filters and drag to reorder (F-TOOL-06)`.

### Task 8: Verifikasi

- [ ] **Step 1:** `dotnet build Gili.slnx -c Release` 0 warning, `dotnet test tests/Gili.Core.Tests` lulus, dan `dotnet format Gili.slnx --verify-no-changes` bersih.
- [ ] **Step 2:** `pwsh scripts/publish.ps1` dan `pwsh scripts/package.ps1 -Install`; tiga timer berlabel dan stopwatch dengan lap bertahan setelah restart di kedua bentuk.
- [ ] **Step 3:** Anggaran 8.1: CPU diam < 1% dengan Tools tertutup; dengan tab Stopwatch terbuka dan berjalan, CPU tetap di anggaran; setelah siklus buka-tutup Tools pertama, RAM diam tidak naik > 5 MB per siklus berikutnya; 30 detik setelah Tools ditutup memori turun (GC penuh). Biaya sekali waktu dari pembukaan pertama (JIT, cache WPF) tidak bisa dikembalikan, jadi RAM tidak kembali persis ke angka sebelum Tools dibuka.
