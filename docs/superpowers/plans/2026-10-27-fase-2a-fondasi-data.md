# Fase 2a: Fondasi Data dan Waktu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gili punya `gili.db` (SQLite WAL) dengan migrasi, backup harian 7 salinan, dan pemulihan dari database rusak, serta aturan waktu `Scheduler` di Core, supaya paket Fase 2 berikutnya (timer, alarm, todo, notes) tinggal memakainya.

**Architecture:** `Gili.Data.Database` membuka/membuat `gili.db` di folder data (`DataFolders.Root`), menyalakan WAL, menjalankan migrasi berurutan yang dicatat di `schema_version`, dan membuat skema v1 dari PRD 10. `Gili.Data.Backups` menyalin database lewat backup API SQLite ke `backups\gili-YYYY-MM-DD.db` dan menyisakan 7. `Gili.Core.Time.Scheduler` dan `AlarmRules` adalah fungsi murni waktu (UTC di penyimpanan, zona lokal untuk jam alarm). `App` membuka database saat start dan menjadwalkan backup harian.

**Tech Stack:** .NET 10, Microsoft.Data.Sqlite, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 9.3 (alur data, `Scheduler`), 10 (tabel, backup 7 salinan, `schema_version`), 11 (database rusak, bangun dari sleep), 13 (unit test `Scheduler`), F-TOOL-05 (snooze 5 menit, alarm terlewat "Missed"), F-TOOL-09 (kehilangan ≤ 1 detik).

## Global Constraints

- Semua waktu jadwal disimpan UTC (ISO 8601 `o`); jam alarm disimpan sebagai jam lokal (`HH:mm`) plus bitmask hari (bit 0 = Senin … bit 6 = Minggu, 0 = sekali).
- `PRAGMA journal_mode=WAL`, `PRAGMA synchronous=NORMAL`, `PRAGMA foreign_keys=ON`.
- Skema v1 persis kolom PRD 10: `notes`, `todos`, `alarms`, `timers`, `stopwatch`, `clipboard_history`, `claude_window`, `shelf_items` (+ `schema_version(version INTEGER NOT NULL)`).
- Backup: `backups\gili-YYYY-MM-DD.db` (tanggal lokal), sekali per hari, 7 terbaru disimpan.
- Database rusak (`PRAGMA quick_check` ≠ `ok` atau gagal dibuka): pindahkan ke `gili.corrupt-<yyyyMMdd-HHmmss>.db`, pulihkan dari backup terbaru yang lolos `quick_check`; kalau tidak ada, buat baru. Hasilnya dilaporkan ke pemanggil (toast menyusul bersama kartu di 2b; sementara log).
- Tidak ada data clipboard, thumbnail, atau sesi Claude ke database selain tabel yang disebut PRD.

## Keputusan yang dibuat di rencana ini

1. **Terlewat = lebih dari 60 detik lewat dari jadwal** saat diperiksa (bangun dari sleep, app mulai). Yang ≤ 60 detik dianggap tepat waktu.
2. **Jam DST yang tidak ada** (lompatan maju) bergeser ke menit pertama setelah lompatan; **jam ganda** (mundur) memakai kemunculan pertama. Indonesia tanpa DST, tapi zona lain diuji.
3. **Scheduler tidak punya timer sendiri.** Ia menjawab "apa yang jatuh tempo" dan "kapan berikutnya"; App (paket 2b) menjalankan satu `DispatcherTimer` ke tenggat terdekat dan memanggil ulang saat start, bangun, dan perubahan jam/zona (`TimeChanged` yang sudah ada).

## Review Focus

1. **Dua instance membuka database** (salinan biasa dan MSIX terpasang bersamaan): mutex F-BAR-05 mencegahnya; tidak ada penanganan tambahan. Diperiksa lewat review.
2. **Laptop dimatikan paksa saat menulis**: WAL + `synchronous=NORMAL` menjaga database konsisten; transaksi terakhir bisa hilang (sesuai F-TOOL-09). Dites di Task 1 (`Database_survives_an_unclosed_connection`).
3. **Folder backup berisi file lain** (mis. salinan manual pengguna): pemangkasan hanya menyentuh `gili-YYYY-MM-DD.db`. Dites di Task 2.
4. **Jam sistem mundur** (backup "hari ini" sudah ada untuk tanggal di masa depan): tidak menimpa, tidak crash; backup berikutnya dibuat saat tanggal belum ada. Dites di Task 2.
5. **Zona waktu berganti** saat alarm berulang menunggu: `NextFire` dihitung ulang dengan zona baru dan tetap jatuh di jam lokal yang sama. Dites di Task 3.

---

### Task 1: Database, migrasi, dan skema v1 (Data)

**Files:**
- Modify: `src/Gili.Data/Gili.Data.csproj` (+ `Microsoft.Data.Sqlite`)
- Create: `src/Gili.Data/Database.cs`, `src/Gili.Data/Migrations.cs`
- Test: `tests/Gili.Core.Tests/Data/DatabaseTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class Database : IDisposable` dengan `static Database Open(string folder)` (membuat folder dan `gili.db`, pragma, migrasi), `SqliteConnection Connection`, `int Version`, `string FilePath`.
  - `internal static class Migrations` dengan `IReadOnlyList<string> Steps` (indeks + 1 = versi); `Apply(SqliteConnection)` menjalankan langkah yang belum ada dalam satu transaksi per langkah.
- [ ] **Step 1: Tes yang gagal**: `Opening_creates_every_prd_table` (nama tabel dari `sqlite_master` ⊇ delapan tabel + `schema_version`), `Version_is_1_after_the_first_open`, `Opening_twice_runs_no_step_again` (versi tetap 1, tidak ada error), `Journal_is_wal`, `Database_survives_an_unclosed_connection` (tulis 1 baris `notes`, buang koneksi tanpa `Dispose` lewat `SqliteConnection.ClearAllPools()` setelah salin file, buka lagi → baris ada), `Notes_columns_match_the_prd` (`PRAGMA table_info(notes)` = id, body, color, text_size, sort_order, created_at, updated_at, float_x, float_y, float_w, float_h).
- [ ] **Step 2:** `dotnet add src/Gili.Data package Microsoft.Data.Sqlite`; tes → gagal kompilasi. **Step 3:** implementasi. **Step 4:** suite lulus.
- [ ] **Step 5: Commit** `feat(data): gili.db with WAL, schema_version migrations and the PRD tables (PRD 10)`.

### Task 2: Backup harian dan pemulihan (Data)

**Files:**
- Create: `src/Gili.Data/Backups.cs`
- Modify: `src/Gili.Data/Database.cs` (pemulihan saat buka)
- Test: `tests/Gili.Core.Tests/Data/BackupsTests.cs`

**Interfaces:**
- Produces:
  - `public static class Backups` dengan `bool BackUpIfDue(Database db, string folder, DateOnly today)` (true bila membuat salinan baru), `IReadOnlyList<string> List(string folder)` (terbaru dulu, hanya pola `gili-YYYY-MM-DD.db`), `const int Keep = 7`.
  - `public enum OpenOutcome { Normal, RestoredFromBackup, CreatedNew }` dan `Database.Open` mengisi `Database.Outcome` serta `Database.CorruptCopy` (path file rusak yang disimpan, atau null).
- [ ] **Step 1: Tes yang gagal**: `One_backup_per_day`, `Only_seven_backups_are_kept` (10 hari berturut → 7 file terbaru), `Other_files_in_backups_are_left_alone`, `A_backup_dated_in_the_future_is_not_overwritten` (jam mundur), `Corrupt_database_is_restored_from_the_latest_good_backup` (tulis sampah ke `gili.db` → `Outcome == RestoredFromBackup`, data backup ada, file rusak disimpan), `Corrupt_database_without_backups_starts_new`.
- [ ] **Step 2–4:** gagal → implementasi → lulus.
- [ ] **Step 5: Commit** `feat(data): daily database backups, seven kept, and recovery from a corrupt file (PRD 10, 11)`.

### Task 3: Aturan waktu (Core)

**Files:**
- Create: `src/Gili.Core/Time/Scheduler.cs`, `src/Gili.Core/Time/AlarmRules.cs`
- Test: `tests/Gili.Core.Tests/Time/SchedulerTests.cs`, `AlarmRulesTests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ScheduledItem(string Id, DateTimeOffset FireAtUtc)`; `public sealed record DueItem(ScheduledItem Item, bool Missed)`.
  - `Scheduler.Due(IEnumerable<ScheduledItem> items, DateTimeOffset now) → IReadOnlyList<DueItem>` (urut waktu; `Missed` bila now − FireAt > `Scheduler.MissedAfter` = 60 detik), `Scheduler.Next(items, now) → DateTimeOffset?` (paling awal yang > now).
  - `[Flags] public enum Days { None = 0, Monday = 1, …, Sunday = 64, Weekdays = 31, Every = 127 }`
  - `AlarmRules.NextFire(TimeOnly time, Days repeat, DateTimeOffset afterUtc, TimeZoneInfo zone) → DateTimeOffset` (UTC, > afterUtc; sekali = kemunculan berikutnya), `AlarmRules.Snooze(DateTimeOffset now) → now + 5 menit`, `AlarmRules.Remaining(DateTimeOffset endsAtUtc, DateTimeOffset now) → TimeSpan` (≥ 0).
- [ ] **Step 1: Tes yang gagal**: `Weekday_alarm_skips_the_weekend` (Jumat 06:00 lewat → Senin 05:30 untuk `05:30 Weekdays`), `Once_alarm_fires_today_or_tomorrow`, `Snooze_is_five_minutes`, `Missed_while_asleep_is_marked_missed`, `Due_on_time_is_not_missed`, `Restarted_timer_keeps_its_end` (`Remaining` dari `ends_at` setelah "restart" = sisa sebenarnya), `Next_is_the_earliest_future_item`, `Time_zone_change_keeps_the_local_alarm_time` (zona +07 → +09: 05:30 lokal tetap 05:30 lokal), `Spring_forward_gap_moves_to_the_first_valid_minute` (zona `America/New_York`, 2026-03-08 02:30 → 03:00 lokal), `Fall_back_uses_the_first_occurrence`.
- [ ] **Step 2–4:** gagal → implementasi → lulus.
- [ ] **Step 5: Commit** `feat(core): scheduler and alarm time rules in UTC with local alarm times (PRD 9.3, F-TOOL-05)`.

### Task 4: Database di app dan verifikasi

**Files:**
- Modify: `src/Gili.App/App.xaml.cs`

- [ ] **Step 1:** `App.OnStartup`: setelah settings, `Database.Open(Data.Root)`; `Outcome` selain `Normal` ditulis ke log dengan path salinan rusak. `Backups.BackUpIfDue` saat start dan lewat `DispatcherTimer` tiap jam (murah; hanya membuat file saat tanggal berganti). `OnExit` menutup database.
- [ ] **Step 2:** Release build 0 warning, tes, format; jalankan: `gili.db`, `gili.db-wal`, dan `backups\gili-<hari ini>.db` ada di `%LOCALAPPDATA%\Gili\`; CPU diam < 1%; private RAM bertambah ≤ 5 MB.
- [ ] **Step 3: Commit** `feat(app): open gili.db at start and back it up daily (PRD 10)`.
