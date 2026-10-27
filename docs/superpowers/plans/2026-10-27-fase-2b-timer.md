# Fase 2b: Timer, Stopwatch, Pomodoro, dan Kartu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Halaman Timer di pulau persis mockup (Timer / Stopwatch / Pomodoro, angka besar, ring, preset, Start/Pause/Reset), aktivitasnya di slot compact, kartu "Timer done" dan fase pomodoro, mode fokus untuk kartu prioritas 2, dan semuanya tetap benar setelah app atau laptop restart.

**Architecture:** Keadaan dan aturan waktu (`CountdownTimer`, `StopwatchState`, `PomodoroState`, teks kartu dan halaman) ada di `Gili.Core.Timers` dan dites; waktu disimpan sebagai UTC lewat tabel `timers` dan `stopwatch` (repository di `Gili.Data`). `Gili.App.Island.TimerService` memegang keadaan, menyimpan setiap perubahan, memasang satu `DispatcherTimer` ke tenggat terdekat (`Scheduler.Next`), dan menaikkan aktivitas serta kartu lewat `IslandController`. Lapisan kartu baru di `IslandWindow` dipakai juga oleh alarm dan todo nanti.

**Tech Stack:** .NET 10 WPF, Microsoft.Data.Sqlite, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.3 (halaman 2 Timer, "Isi compact" urutan 4–5, "Kartu notifikasi", "Prioritas tampilan", glow Timer), F-TOOL-01–04 (bagian pulau), F-ISL-08 (kartu); mockup HTML baris 1012–1022, CSS 447–459 dan 747–760, JS `renderTimerPage` (±2064), `CARDS`/`showCard` (±1733), `slotValue` (±1783), `renderGlow` (±1944).

## Global Constraints

- Teks persis: segmen `Timer` / `Stopwatch` / `Pomodoro`; preset `10 s`, `5 min`, `10 min`, `25 min`; tombol `Start` / `Pause` / `Resume`, `Reset`; sub `Running`, `Paused`, `Pick a duration`, `Stopwatch`, `Focus · session 1 of 4`, `Break · session 1 of 4`; kartu `Timer 10:00 done` (tombol `Repeat`, `Close`), `Focus session done` + `5-minute break · session 1 of 4` (`Skip`, `Start break`), `Break over` + `Session 2 of 4 ready` (`Stop`, `Start focus`). Format `mm:ss` = mockup `mmss` (dibulatkan ke atas); stopwatch di halaman `mm:ss.t`, di slot `mm:ss`.
- Nilai awal: timer 10:00 (600 s), pomodoro 25 / 5 / 15 menit, istirahat panjang setelah sesi ke-4.
- Halaman: kiri (segmen, angka 44 px bold digit tabular, sub 13 px abu, preset chip 12 px radius 12 latar `#1C1C1E` hanya di mode Timer), kanan (ring 104 px stroke 8 latar `#1C1C1E`, warna oranye `#FF9F0A`, merah `#FF453A` untuk pomodoro; tombol kolom celah 8, Start = pill putih). Glow: oranye timer, biru muda `#64D2FF` stopwatch, merah fokus, hijau `#30D158` istirahat.
- Slot: ikon jam pasir oranye (timer), merah (pomodoro fokus), hijau (istirahat), ikon stopwatch oranye; nilai `mm:ss` di kanan, lebar min. 200.
- Kartu (mockup `.L-card`): 560 × 104, padding 0 24, celah 14; lingkaran ikon 44 px warna 22% dengan ikon 22 px dan cincin denyut 1,2 detik (`pulse`); judul 18 px bold; sub 13 px abu; tombol pill (utama putih). Kartu prioritas 1 (Urgent) tetap sampai ditanggapi.
- Fokus pomodoro menahan kartu prioritas 2 (Reminder) sampai fase fokus selesai; prioritas 1 tetap muncul.
- Ticking: 1 detik hanya selama ada yang berjalan; halaman Timer stopwatch 10×/detik hanya selama halaman itu terbuka. Tidak ada timer saat semuanya diam (PRD 8.1).

## Keputusan yang dibuat di rencana ini

1. **Satu timer di pulau** seperti mockup. Banyak timer berlabel (F-TOOL-01) masuk jendela Tools (paket 2g) memakai tabel yang sama (`kind = 'timer'`, baris pulau berlabel kosong).
2. **Suara, toast, dan kartu yang tidak ditanggapi 5 menit** masuk paket 2c (mesin suara). Toast Windows di build biasa butuh shortcut Start Menu dengan AUMID dari installer (PRD 9.4), jadi toast mengikuti paket MSIX/installer; kartu pulau tetap muncul.
3. **Ekspresi fokus Gili** (F-ISL-24 fokus/kaget/senang) menjadi paket tersendiri setelah alarm.
4. **Timer yang habis saat Gili tertutup** memunculkan kartunya saat Gili mulai (Scheduler.Due); pomodoro yang fasenya habis saat tertutup juga memunculkan kartu fase.
5. **Skip** di kartu "Focus session done" menutup kartu dan langsung memulai fokus sesi berikutnya tanpa istirahat; **Stop** di "Break over" mengembalikan pomodoro ke awal (sesi 1, 25:00, berhenti).

## Review Focus

1. **Restart di tengah timer** (Gili ditutup 3 menit lalu dibuka): sisa waktu = ends_at − sekarang. Dites di Task 1 dan diperiksa manual.
2. **Jam sistem berubah saat timer berjalan**: timer memakai UTC absolut, jadi pergantian zona tidak mengubah sisa; `TimeChanged` memicu penjadwalan ulang. Dites di Task 1.
3. **Kartu muncul saat halaman lain terbuka atau saat fullscreen**: kartu Urgent menggantikan halaman dan kembali sesudahnya; saat fullscreen hanya prioritas 1 (aturan coordinator yang ada).
4. **Dua hal selesai bersamaan** (timer dan fokus pomodoro di detik yang sama): dua kartu antre, yang lebih tua dulu.
5. **Klik Start berulang cepat**: tidak membuat dua timer; status dari model, bukan dari teks tombol.

---

### Task 1: Model timer, stopwatch, pomodoro (Core)

**Files:**
- Create: `src/Gili.Core/Timers/TimerModels.cs`, `src/Gili.Core/Timers/TimerText.cs`
- Modify: `src/Gili.Core/Island/IslandCoordinator.cs` (+ `SetFocus(bool)`)
- Test: `tests/Gili.Core.Tests/Timers/TimerModelsTests.cs`, `TimerTextTests.cs`, `tests/Gili.Core.Tests/Island/IslandCoordinatorTests.cs`

**Interfaces:**
- Produces (semua record tak berubah; method mengembalikan keadaan baru):
  - `CountdownTimer(int TotalSeconds, DateTimeOffset? EndsAt, int? PausedRemaining)`: `Start(now)`, `Pause(now)`, `Reset()`, `WithTotal(int)`, `Remaining(now) → TimeSpan`, `IsRunning`, `IsPaused`, `IsDone(now)`; `static Default` = 600 s.
  - `StopwatchState(DateTimeOffset? StartedAt, long AccumulatedMs)`: `Start(now)`, `Pause(now)`, `Reset()`, `Elapsed(now)`, `IsRunning`.
  - `enum PomodoroPhase { Focus, Break, LongBreak }`; `PomodoroState(PomodoroPhase Phase, int Round, CountdownTimer Clock)`: `Default`, `Start/Pause(now)`, `Reset()`, `NextPhase(now, bool startNow)` (Focus → Break atau LongBreak di sesi 4; Break → Focus sesi+1; LongBreak → Focus sesi 1), `Skip(now)` (Focus → Focus sesi+1 langsung jalan), `static FocusLength = 25 min, BreakLength = 5 min, LongBreakLength = 15 min, Rounds = 4`.
  - `TimerText.Clock(TimeSpan)` (`mmss`, ke atas), `TimerText.Stopwatch(TimeSpan, bool tenths)`, `TimerText.PageSub(...)`, `TimerText.Button(bool running, bool started)`, `TimerText.TimerDone(int totalSeconds)` (`Timer 10:00 done`), `TimerText.FocusDone(PomodoroState)` (`5-minute break · session 1 of 4` / `15-minute break · session 4 of 4`), `TimerText.BreakOver(PomodoroState)` (`Session 2 of 4 ready`).
  - `IslandCoordinator.SetFocus(bool)`: selama fokus, kartu `Reminder` tidak tampil dan waktunya tidak berjalan; setelah fokus selesai kartu tertahan tampil.
- [ ] **Step 1: Tes yang gagal**: `Timer_counts_down_from_its_end`, `Paused_timer_keeps_its_remaining`, `Resume_sets_a_new_end`, `Reset_goes_back_to_the_total`, `Remaining_ignores_a_time_zone_change` (sama `ends_at`, zona berbeda), `Stopwatch_adds_up_runs`, `Pomodoro_goes_focus_break_focus`, `Fourth_focus_leads_to_a_long_break`, `Long_break_starts_over_at_session_1`, `Skip_starts_the_next_focus`, `Clock_rounds_up` (59,2 s → `01:00`… `mmss` mockup: 59,2 → `01:00`), `Stopwatch_shows_tenths_on_the_page`, `Card_texts_match_the_mockup`, coordinator `Focus_holds_reminder_cards_until_it_ends`, `Focus_never_holds_urgent_cards`.
- [ ] **Step 2–4:** gagal → implementasi → lulus.
- [ ] **Step 5: Commit** `feat(core): countdown, stopwatch and pomodoro rules, texts and focus hold (F-TOOL-01..04)`.

### Task 2: Penyimpanan timer (Data)

**Files:**
- Create: `src/Gili.Data/TimerStore.cs`
- Test: `tests/Gili.Core.Tests/Data/TimerStoreTests.cs`

**Interfaces:**
- Produces: `TimerStore(Database db)` dengan `CountdownTimer LoadTimer()`, `SaveTimer(CountdownTimer)`, `PomodoroState LoadPomodoro()`, `SavePomodoro(PomodoroState)`, `StopwatchState LoadStopwatch()`, `SaveStopwatch(StopwatchState)`; baris pulau `timers` (`kind='timer'`, label kosong; `kind='pomodoro'` dengan `pomodoro_phase`, `pomodoro_round`), `stopwatch` satu baris. Tanpa baris → `Default`.
- [ ] **Step 1: Tes yang gagal**: `Running_timer_survives_a_restart` (simpan, buka database baru dari file yang sama, `EndsAt` sama), `Paused_timer_survives_a_restart`, `Pomodoro_phase_and_round_survive`, `Stopwatch_survives`, `Nothing_saved_gives_defaults`, `Saving_twice_keeps_one_row`.
- [ ] **Step 2–4:** gagal → implementasi → lulus.
- [ ] **Step 5: Commit** `feat(data): timer, pomodoro and stopwatch state kept in gili.db (F-TOOL-01..03)`.

### Task 3: TimerService, slot, kartu, dan halaman Timer (App)

**Files:**
- Create: `src/Gili.App/Island/TimerService.cs`, `src/Gili.App/Island/TimerPage.xaml(.cs)`, `src/Gili.App/Island/CardView.xaml(.cs)`
- Modify: `IslandController.cs`, `IslandViewModel.cs`, `IslandWindow.xaml(.cs)` (halaman Timer menggantikan placeholder, lapisan kartu, glow Timer dinamis, slot dan gelembung timer/stopwatch), `src/Gili.App/Resources/Icons.xaml` (+ `Icon.Stopwatch`), `App.xaml.cs`, `DemoScript`/`DemoService` (+ langkah timer 10 detik yang selesai dan memunculkan kartu), tes demo

**Interfaces:**
- Consumes: Task 1, Task 2, `Scheduler`, `IslandCoordinator.ShowCard/DismissCard/SetActivity/SetFocus`.
- Produces:
  - `TimerService(Dispatcher, TimerStore?)`: `Timer`, `Pomodoro`, `Stopwatch`, `Mode` (`timer`/`stopwatch`/`pomodoro`, diingat selama app jalan), `Start/Pause/Reset()` untuk mode aktif, `Preset(int seconds)`, `Repeat()`, `StartBreak()`, `SkipBreak()`, `StopPomodoro()`, `StartFocus()`, `event Action? Changed`, `event Action<string>? Finished` (`"timer"`, `"pomo-focus"`, `"pomo-break"`), `Reschedule()` (dipanggil saat `TimeChanged`/bangun).
  - Kartu: `IslandCard` dengan Id `timer`, `pomo-focus`, `pomo-break`, Priority `Urgent`; isi kartu dari `CardContent(Glyph, Color, Title, Subtitle, Buttons[(Label, Action, Primary)])` di `IslandViewModel`.
- [ ] **Step 1:** `TimerService` dengan penjadwalan satu `DispatcherTimer` (tenggat terdekat) + tick 1 s selama ada yang berjalan; menyimpan setiap perubahan lewat `TimerStore`; saat start App memanggil `Reschedule()` yang memunculkan kartu untuk yang sudah habis (Keputusan 4).
- [ ] **Step 2:** `IslandController`: aktivitas `Timer` (timer atau pomodoro berjalan), `Stopwatch`; `SetFocus` saat pomodoro fase fokus berjalan; kartu dari `Finished`; tombol kartu memanggil aksi `TimerService` lalu `DismissCard`. `OpenFor(Timer/Stopwatch)` membuka halaman Timer dengan mode yang sesuai.
- [ ] **Step 3:** `TimerPage.xaml` dan `CardView.xaml` sesuai Global Constraints; ring sebagai `Path` busur (bukan Storyboard) diperbarui tiap tick; denyut ikon kartu `DoubleAnimation` skala 1 → 1,65 dan opacity 0,8 → 0 selama 1,2 s berulang, berhenti saat kartu hilang dan saat "Animation effects" mati.
- [ ] **Step 4:** demo: setelah langkah copy, preset 10 s dan Start; kartu "Timer 00:10 done" muncul lalu ditutup demo 3 detik kemudian; tes demo diperbarui (durasi 25–50 s).
- [ ] **Step 5:** build 0 warning, tes lulus; manual: potret halaman Timer (tiga mode) dan kartu berdampingan dengan mockup (`mockup-shot.ps1` + state baru `timer-page`, `timer-card`), restart Gili di tengah timer, preset 10 s sampai kartu muncul, pomodoro dipercepat lewat tes manual (ubah sementara tidak perlu: pakai preset dan kartu dari Task 1 tes), CPU saat timer berjalan dengan pulau tertutup ≤ anggaran (< 1%).
- [ ] **Step 6: Commit** `feat(island): Timer page, timer slot and cards for timer and pomodoro (F-TOOL-01..04, F-ISL-08)`.

### Task 4: Verifikasi

- [ ] **Step 1:** Release build 0 warning, tes, format; publish; CPU diam < 1%.
- [ ] **Step 2:** catat di ledger.
