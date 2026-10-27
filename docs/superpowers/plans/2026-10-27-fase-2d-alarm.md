# Fase 2d: Alarm dan Jendela Tools Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Alarm bekerja penuh: dibuat dan diedit di jendela Tools (tab Alarms), dinyalakan/dimatikan di halaman Alarms pulau, berbunyi dengan kartu Snooze 5m / Stop tepat waktu (termasuk membangunkan laptop dari sleep), dan alarm yang terlewat tetap berbunyi saat bangun dengan label "Missed".

**Architecture:** Model, teks, dan jadwal alarm ada di `Gili.Core.Alarms` (memakai `AlarmRules.NextFire` dari 2a) dan dites; `Gili.Data.AlarmStore` menyimpan tabel `alarms`. `Gili.App.Island.AlarmService` memegang daftar, menyimpan `next_fire_at` UTC, menjalankan satu timer ke alarm terdekat, memasang wake timer Windows (`Gili.Platform.Power.WakeTimer`), dan memunculkan kartu lewat `IslandController`. Halaman Alarms pulau dan jendela Tools (`Gili.App.Tools.ToolsWindow`, tab Alarms) membaca dan mengubah lewat service yang sama. Desain Tools ditambahkan ke mockup lebih dulu.

**Tech Stack:** .NET 10 WPF, Microsoft.Data.Sqlite, `CreateWaitableTimerExW`/`SetWaitableTimer` (fResume), xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.3 (halaman 3 Alarms, "Kartu notifikasi" Alarm, prioritas 1), 6.6 (Alarm: suara per alarm default Radar, naik bertahap, 5 menit), 7.5 (Tools), F-TOOL-05, risiko wake timer (tabel risiko); mockup HTML halaman Alarms (±1027–1031), CSS `.alist`/`.arow` (462–465), `CARDS.alarm` (cls `pulse shake`), `.setw` (gaya jendela Settings dipakai ulang untuk Tools).

## Global Constraints

- Teks persis: halaman pulau `Next alarm`, jam besar, `Tomorrow · Wake up` / `Today · …` / `Thu · …` (hari 3 huruf bila lebih dari besok), tanpa alarm aktif `No alarms on`; baris `05:30` + `Wake up` / `Mon–Fri · Radar`, `Every day · Radar`, `Once · Soft`, `Sat, Sun · Ping`, `Mon, Wed, Fri · Chime`; label kosong tampil `Alarm`. Tombol `Manage alarms`. Kartu: judul `Alarm 14:30`, subjudul label, tombol `Snooze 5m`, `Stop` (utama); terlewat: judul `Missed alarm 14:30`.
- Jam mengikuti Settings → 12-hour clock (`5:30 AM`); bawaan 24 jam `05:30`.
- Kartu alarm: warna oranye `#FF9F0A`, ikon alarm, denyut + goyang ikon (mockup `shake`: rotasi 0 → −14° → 12° → −8° → 6° → 0 dalam 0,7 s berulang), prioritas Urgent, suara alarm berulang dengan Gradually increase (Settings) dan suara per alarm (default Radar).
- Snooze 5 menit; Stop: alarm sekali menjadi mati, alarm berulang dijadwalkan ke kemunculan berikutnya.
- Waktu: `next_fire_at` UTC; dihitung ulang saat start, bangun, dan perubahan jam/zona (`TimeChanged`). Terlewat > 60 s = Missed (`Scheduler.MissedAfter`), tetap berbunyi.
- Wake timer: satu timer Windows ke alarm aktif terdekat, `fResume` = true; dipasang ulang setiap daftar atau jadwal berubah.
- Tools: jendela gelap 720 × 520, bisa dipindah dan diubah ukurannya (min. 560 × 420), bilah judul dan tab kiri sama seperti Settings; tab `Timer`, `Stopwatch`, `Pomodoro`, `Alarms`, `Todo` (selain Alarms: `Coming in a later version`). Tab Alarms: tombol `New alarm`, baris alarm (jam, label, hari · suara, saklar, `Edit`, `Delete` dengan konfirmasi `Click again to delete` 3 detik), editor (Time `HH:mm`, Label maks. 40, Repeat: `Once` / `Weekdays` / `Every day` + tujuh tombol hari `Mo Tu We Th Fr Sa Su`, Sound segmen lima suara + tombol putar, `Save`, `Cancel`).
- Anggaran PRD 8.1: tidak ada polling; satu `DispatcherTimer` ke tenggat terdekat; Tools dibuat saat dibuka dan dilepas saat ditutup.

## Keputusan yang dibuat di rencana ini

1. **Tools memakai gaya jendela Settings mockup** (`.setw`, `.setn`, `.srow`) karena mockup belum punya desain Tools; desain ditambahkan ke mockup di Task 4 sebelum app.
2. **Tombol `Manage alarms`** (pill kecil di bawah sub "Next alarm") ditambahkan ke halaman Alarms mockup, karena PRD menyebutnya dan mockup belum.
3. **Kartu yang tidak ditanggapi 5 menit**: suara berhenti, kartu tetap sampai ditanggapi. "Mengecil ke compact dengan tanda merah" butuh desain mockup dan ditunda; toast "Missed alarm" ikut paket toast (MSIX/installer).
4. **Peringatan wake timer mati** di Settings ditunda (perlu membaca kebijakan daya); alarm terlewat tetap berbunyi saat bangun.
5. **Alarm sekali yang lewat saat Gili mati lebih dari 12 jam** tetap berbunyi sebagai Missed sekali lalu mati (tidak menumpuk); alarm berulang yang terlewat beberapa kali hanya memunculkan satu kartu Missed (yang terakhir), lalu dijadwalkan ke depan.
6. **Demo**: kartu alarm demo (alarm palsu 14:30 "Take medicine", tidak disimpan) ditambahkan setelah halaman-halaman.

## Review Focus

1. **Dua alarm di menit yang sama**: dua kartu antre (Urgent, yang lebih dulu dibuat lebih dulu); Stop yang satu menampilkan yang lain. Dites di Task 1 (`Same_minute_alarms_both_fire`).
2. **Mengubah jam alarm yang sedang berbunyi atau di-snooze** di Tools: kartu ditutup, jadwal dihitung dari jam baru. Dites di Task 3 lewat review dan manual.
3. **Menghapus alarm yang sedang tampil kartunya**: kartu ditutup, suara berhenti.
4. **Input jam tidak valid di editor** (`25:00`, `7:5`, kosong): Save nonaktif, kolom bergaris merah; `7:05` dan `0705` diterima. Dites di Task 1 (`Time_input_parses_loosely_and_rejects_nonsense`).
5. **Sleep melewati alarm dengan wake timer diblokir**: saat bangun kartu Missed muncul sekali. Dites di Task 1 (`Missed_while_asleep_rings_once_as_missed`).

---

### Task 1: Model, teks, dan jadwal alarm (Core)

**Files:**
- Create: `src/Gili.Core/Alarms/Alarm.cs`, `src/Gili.Core/Alarms/AlarmText.cs`, `src/Gili.Core/Alarms/AlarmSchedule.cs`
- Test: `tests/Gili.Core.Tests/Alarms/AlarmTextTests.cs`, `AlarmScheduleTests.cs`

**Interfaces:**
- Consumes: `AlarmRules.NextFire`, `Days`, `Scheduler.MissedAfter` (2a), `Voices.Find` (2c).
- Produces:
  - `public sealed record Alarm(long Id, TimeOnly Time, Days Repeat, string Label, string Sound, bool Enabled, DateTimeOffset? NextFireAt)`.
  - `AlarmText.Time(TimeOnly, bool use24Hour)`, `AlarmText.Days(Days)` (`Once`, `Every day`, `Mon–Fri`, `Sat, Sun`, `Mon, Wed, Fri`), `AlarmText.RowDetail(Alarm)` (`Mon–Fri · Radar`), `AlarmText.Label(Alarm)` (`Alarm` bila kosong), `AlarmText.NextHeading(Alarm?, DateTimeOffset now, TimeZoneInfo, bool use24Hour) → (string Big, string Sub)` (`("05:30", "Tomorrow · Wake up")`, tanpa alarm `("--:--", "No alarms on")`), `AlarmText.CardTitle(Alarm, bool missed, bool use24Hour)`, `AlarmText.ParseTime(string) → TimeOnly?`.
  - `AlarmSchedule.Arm(Alarm, DateTimeOffset now, TimeZoneInfo) → Alarm` (aktif → NextFireAt dihitung; mati → null), `AlarmSchedule.Due(IReadOnlyList<Alarm>, DateTimeOffset now) → IReadOnlyList<(Alarm Alarm, bool Missed)>` (satu entri per alarm), `AlarmSchedule.AfterRing(Alarm, DateTimeOffset now, TimeZoneInfo) → Alarm` (sekali → mati, berulang → berikutnya), `AlarmSchedule.Snooze(Alarm, DateTimeOffset now) → Alarm` (+5 menit), `AlarmSchedule.Next(IReadOnlyList<Alarm>) → Alarm?`.
- [ ] **Step 1: Tes yang gagal**: `Days_read_like_the_mockup`, `Row_detail_joins_days_and_sound`, `Empty_label_reads_alarm`, `Next_heading_says_today_tomorrow_or_the_day`, `No_alarm_on_reads_no_alarms_on`, `Twelve_hour_times`, `Card_titles_for_ringing_and_missed`, `Time_input_parses_loosely_and_rejects_nonsense` (`"7:05"`, `"0705"`, `"07.05"` → 07:05; `"25:00"`, `"7:5"`, `""`, `"ab"` → null), `Arming_an_enabled_alarm_sets_its_next_time`, `Disabled_alarm_has_no_next_time`, `Once_alarm_turns_off_after_ringing`, `Repeating_alarm_moves_to_its_next_day`, `Snooze_is_five_minutes_from_now`, `Same_minute_alarms_both_fire`, `Missed_while_asleep_rings_once_as_missed` (alarm berulang harian dengan NextFireAt 3 hari lalu → satu entri Missed), `Next_is_the_soonest_enabled`.
- [ ] **Step 2–4:** gagal → implementasi → lulus.
- [ ] **Step 5: Commit** `feat(core): alarm model, texts and schedule (F-TOOL-05)`.

### Task 2: Penyimpanan alarm (Data)

**Files:**
- Create: `src/Gili.Data/AlarmStore.cs`
- Test: `tests/Gili.Core.Tests/Data/AlarmStoreTests.cs`

**Interfaces:**
- Produces: `AlarmStore(Database)`: `IReadOnlyList<Alarm> All()` (urut jam), `Alarm Add(Alarm)` (mengembalikan dengan Id baru), `void Update(Alarm)`, `void Delete(long id)`. Kolom tabel `alarms` 2a: `time_of_day` `HH:mm`, `repeat_days` bitmask, `sound` huruf kecil (`radar`), `enabled` 0/1, `next_fire_at` UTC `o`.
- [ ] **Step 1: Tes yang gagal**: `Added_alarm_survives_a_restart`, `Update_changes_every_field`, `Delete_removes_it`, `All_is_sorted_by_time`, `Sound_is_stored_lowercase_and_read_by_name`.
- [ ] **Step 2–4.** **Step 5: Commit** `feat(data): alarms kept in gili.db (F-TOOL-05)`.

### Task 3: AlarmService, kartu alarm, wake timer, halaman Alarms

**Files:**
- Create: `src/Gili.App/Island/AlarmService.cs`, `src/Gili.App/Island/AlarmsPage.xaml(.cs)`, `src/Gili.App/Island/AlarmsViewModel.cs`, `src/Gili.Platform/Power/WakeTimer.cs`
- Modify: `IslandController.cs` (kartu `alarm:<id>`, aksi Snooze/Stop, `missed`), `CardContent.cs` (+ `bool Shake`), `CardView.xaml(.cs)` (goyang ikon), `IslandWindow.xaml.cs` (halaman Alarms menggantikan placeholder), `IslandViewModel.cs` (tab Alarms tanpa placeholder; glow kuning sudah ada), `SoundService.cs` (`PlayForCard` menerima suara per alarm dan naik bertahap), `src/Gili.Core/Sound/SoundRules.cs` (+ kartu `alarm:*` → suara alarm, loop, ramp) dan tesnya, `App.xaml.cs`, demo (`DemoAlarm`) dan tesnya
- **Interfaces:**
  - `WakeTimer`: `Set(DateTimeOffset? atUtc)` (`CreateWaitableTimerExW` sekali, `SetWaitableTimer` waktu absolut FILETIME, `fResume=true`; null → `CancelWaitableTimer`), `Dispose()`.
  - `AlarmService(AlarmStore?, WakeTimer)`: `Alarms`, `Changed`, `Add/Update/Delete(Alarm)`, `SetEnabled(long id, bool)`, `Snooze(long id)`, `Stop(long id)`, `Reschedule()` (start, bangun, `TimeChanged`), `event Action<Alarm, bool>? Ringing` (bool = missed); memakai `TimeZoneInfo.Local`.
  - `SoundRules.ForCard("alarm:<id>", timerSound)` → loop + ramp; suaranya diambil dari alarm oleh `IslandController` (kartu alarm memanggil `SoundService.PlayAlarm(string voice)`).
- [ ] **Step 1: Tes yang gagal (Core)**: `Alarm_cards_ring_on_repeat_with_the_ramp`; perbarui demo `An_alarm_card_shows_and_is_stopped`. Jalankan → gagal; implementasi → lulus.
- [ ] **Step 2:** `WakeTimer`, `AlarmService` (satu `DispatcherTimer` ke `AlarmSchedule.Next`; saat tenggat `Due` → `Ringing` untuk tiap alarm, lalu `AfterRing` untuk yang berbunyi; Snooze menyimpan NextFireAt +5 menit; mengubah atau menghapus alarm yang kartunya tampil menutup kartunya).
- [ ] **Step 3:** kartu: `IslandController` mengubah `Ringing` menjadi kartu Urgent `alarm:<id>` (judul `Alarm 14:30`/`Missed alarm 14:30`, subjudul label, Snooze 5m / Stop, warna oranye, ikon alarm, `Shake`), suara alarm per alarm dengan ramp.
- [ ] **Step 4:** halaman Alarms (mockup: kiri sub `Next alarm`, jam 44 px bold, sub; pill kecil `Manage alarms` 30 px; kanan daftar 300 px baris `#1C1C1E` radius 12 padding 7 12, jam 17 px semibold lebar 52, teks 12 px abu tinggi baris 1,3, saklar); pulau memanjang mengikuti jumlah alarm (halaman diukur seperti halaman lain).
- [ ] **Step 5:** build 0 warning, tes lulus. Manual: tambah alarm lewat Tools (Task 4) atau langsung lewat `AlarmStore` sementara; alarm 1 menit lagi berbunyi dengan kartu dan suara naik; Snooze lalu berbunyi lagi 5 menit kemudian; Stop pada alarm sekali mematikannya; saklar di halaman pulau; sleep 2 menit melewati alarm (`rundll32 powrprof.dll,SetSuspendState 0,1,0` tidak dipakai: uji dengan menutup Gili, memajukan NextFireAt ke masa lalu, lalu menjalankannya → kartu Missed).
- [ ] **Step 6: Commit** `feat(island): alarms ring with a card, snooze and stop, wake the laptop, and show on the Alarms page (F-TOOL-05, F-ISL-08)`.

### Task 4: Jendela Tools dan tab Alarms

**Files:**
- Modify: `docs/mockup/top-taskbar-mockup.html` (jendela `#toolsw` dengan gaya `.setw`, tab dan editor alarm; tombol `Manage alarms` di halaman Alarms membukanya), `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md` (7.5: tata letak Tools mengikuti Settings), `scripts/mockup-shot.ps1` (+ state `tools-alarms`)
- Create: `src/Gili.App/Tools/ToolsWindow.xaml(.cs)`, `src/Gili.App/Tools/AlarmsTabViewModel.cs`, `src/Gili.App/Tools/AlarmEditorViewModel.cs`
- Modify: `App.xaml.cs` (`ShowTools(string tab)`; satu jendela, dibawa ke depan bila sudah terbuka), `AlarmsPage` (Manage alarms → `ShowTools("Alarms")`), menu klik kanan bar (+ `Open Tools` di atas Settings, PRD 6.1)
- **Interfaces:**
  - `AlarmEditorViewModel(Alarm? existing, Action<Alarm> save, Action cancel, Action<string> preview)`: `TimeText`, `IsTimeValid`, `Label`, `Repeat` (Days), `QuickRepeat` (`Once`/`Weekdays`/`Every day`/null), `Sound`, `SaveCommand` (aktif bila jam valid), `CancelCommand`, `PlayCommand`.
- [ ] **Step 1:** mockup: jendela Tools (640 × 460 mockup, isi tab Alarms dengan baris contoh dan editor), Esc/× menutup; `node --check` lolos dan probe Edge tanpa error. PRD 7.5 diperbarui.
- [ ] **Step 2:** `ToolsWindow` dengan chrome Settings (`WindowChrome`, sudut bulat), bisa diubah ukurannya (min. 560 × 420); tab kiri; tab selain Alarms menampilkan `Coming in a later version`.
- [ ] **Step 3:** tab Alarms: daftar dari `AlarmService`, `New alarm` membuka editor di atas daftar, `Edit`, `Delete` dua klik (teks `Click again to delete`, kembali setelah 3 detik), saklar.
- [ ] **Step 4:** build 0 warning, tes lulus. Manual: buat alarm sekali dan berulang, edit jam, hapus, putar suara; bandingkan dengan potret mockup `tools-alarms` (PrintWindow, hanya jendela Gili); alarm baru langsung tampil di halaman pulau.
- [ ] **Step 5: Commit** `feat(app): Tools window with the Alarms tab to add, edit and delete alarms (F-TOOL-05)`.

### Task 5: Verifikasi

- [ ] **Step 1:** Release build 0 warning, tes, format; publish dan `package.ps1 -Install`; CPU diam < 1%, RAM diam tidak naik > 5 MB; Tools ditutup → memori turun dalam 30 detik.
- [ ] **Step 2:** catat di ledger.
