# Fase 3a: Pulau Sistem Kecil Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Caps/Num Lock, peringatan baterai (termasuk batas pengisian), titik kamera/mic, kejadian Bluetooth, dan tab pulau serta ikon kiri yang bisa disembunyikan bekerja sesuai PRD 6.2, 6.3, dan 6.5, tanpa hook global dan tanpa polling berat.

**Architecture:** Semua aturan baru ada di Core dan dites: isi wide yang lebih kaya (`WideRun`), `LockKeyRules`, `BatteryAlerts`, `BluetoothRules`, `PrivacyText`/`PrivacyNames`, posisi titik di `IslandLayout`, `IslandTabs`, dan settings `HiddenLeft`/`HiddenPages`. Platform mendapat tiga sumber berbasis event atau baca murah: `LockKeyMonitor` (`GetKeyState` 8×/detik), `PrivacyMonitor` (notifikasi registry `ConsentStore`), dan `BluetoothMonitor` yang diperluas. `IslandController` menyambungkan semuanya ke `IslandCoordinator` lewat `ShowWide` yang sudah ada.

**Tech Stack:** .NET 10 WPF, CommunityToolkit.Mvvm, WinRT (`Windows.Devices.Bluetooth`, `Windows.System.Power`, `Windows.Management.Deployment`), Win32 (`GetKeyState`, `RegNotifyChangeKeyValue`), xUnit.

**Spec:** [desain Fase 3a](../specs/2026-10-31-fase-3a-pulau-sistem-design.md), PRD 6.2 ("Saklar kiri ikut mematikan tab pulau"), 6.3 ("Indikator kamera dan mikrofon", "Kejadian wide", "Tab bisa disembunyikan"), 6.5 (Left side, Island, Right side), 8.1. Mockup: `showWide`, `ACT.caps/bat20/bat80/bt/btOff`, titik privasi di `layout()` (`pCam`, `pMic`), `SET`.

## Global Constraints

- Teks persis (bahasa Inggris): `Caps Lock on` / `Caps Lock off`, `Num Lock on` / `Num Lock off`, `Charging · 64%`, `On battery · 63%`, `Charging on hold · 79%`, `Battery 18% · plug in`, `Battery 10%`, `80% reached · unplug`, `Fully charged`, `Galaxy Buds2 Pro  Connected  80%` (tiga bagian berjarak 8 px), `Galaxy Buds2 Pro  Disconnected`, `Camera in use: Zoom`, `Microphone in use: Discord, Chrome`, `Turned off in Left side`, `Battery alert` dengan help `Also at 10%`, `Unplug reminder` dengan help `Only on laptops that keep charging past it. 100% turns it off`.
- Bagian tebal di wide (semibold, digit tabular) mengikuti mockup: `64%` di Charging/On battery/on hold, `on`/`off` di Caps/Num Lock, persen di Battery, `80%` di pengingat cabut.
- Warna: accent `#30D158`, warn `#FFD60A`, danger `#FF453A`, blue `#0A84FF`, text2 `#8E8E93`, teks `#F5F5F7`; titik kamera `#30D158`, mikrofon `#FF9F0A`, tepi titik 1,5 px `#59000000`.
- Lama wide: Caps/Num 1,5 detik; Charging 3 detik; On battery 2 detik; semua peringatan baterai (termasuk on hold) 5 detik; Bluetooth tersambung 4 detik, terputus 2 detik.
- Titik privasi: 8 px, jarak ke pulau atau lingkaran Gili 6 px, antar-titik 5 px (langkah 13 px), area hover +6 px tiap sisi, muncul/hilang dengan pegas skala 0 → 1 (`GiliRules.ShapeSpring`); Animation effects mati: cross-fade 100 ms.
- Tidak ada hook global, raw input, atau polling baru selain `LockKeyMonitor` (125 ms, ikut dijeda bersama polling lain di `App.xaml.cs` `poll`).
- Pulau terbuka tetap selebar menu 10 tab (`TabMenuLayout.Need` dengan jumlah tab penuh), berapa pun tab yang tersembunyi.

## Keputusan yang dibuat di rencana ini

1. **Isi wide menjadi daftar `WideRun`**, bukan `Text` + `Value`, karena Caps Lock, Battery 20%, dan Bluetooth menebalkan atau meredupkan bagian yang berbeda (mockup). Warna ikon dipindah ke `WideTone`. Charger lama ikut dipindah.
2. **Ikon baterai mengikuti mockup**: semua kejadian baterai memakai petir (`Icon.Bolt`); warnanya yang membedakan (rendah warn, kritis danger, on hold/cabut/penuh accent, On battery teks). Desain menyebut ikon baterai merah; mockup menang (CLAUDE.md: tampilan persis mockup), dan desain diperbarui di Task 1.
3. **Ikon baru**: `Icon.Caps` (path `i-caps` mockup), `Icon.NumLock`, `Icon.Headphones` (path `i-headphones` mockup), `Icon.Mouse`, `Icon.Keyboard`, `Icon.Gamepad`, `Icon.Phone`. Yang belum ada di mockup digambar di Task 1 dengan gaya garis 24 × 24, stroke 2, sudut bulat, lalu disalin ke `Icons.xaml`.
4. **Satu `BluetoothMonitor`** dibuat di `App.xaml.cs` dan diberikan ke `RightService` dan `IslandController`.
5. **Settings disimpan sebagai flags** (`LeftIcons`, `PageSet`), supaya kesamaan record `AppSettings` tetap berdasarkan nilai. Halaman yang tersembunyi efektif = `HiddenPages` ∪ halaman milik `HiddenLeft`. `Normalize` membuka Media kalau semua tab tersembunyi.
6. **Demo** memakai aksi demo baru di `DemoScript` (pola yang ada: `DemoPower`, `DemoTimer`), bukan provider palsu: `DemoLockKeys`, `DemoBluetooth`, `DemoPrivacy`, dan `DemoPower` untuk baterai rendah dan on hold.

## Review Focus

1. **Batas pengisian yang naik-turun** (79 → 80 → 79%, `Charging` hidup-mati): `Charging on hold` hanya sekali per colok dan pengingat cabut tidak pernah muncul. Dites di Task 3 (`Hold_flapping_alerts_once_per_plug`).
2. **Caps Lock dan Num Lock berubah di pembacaan yang sama**: dua wide antre, Caps dulu. Dites di Task 2 (`Caps_and_num_together_queue_caps_first`).
3. **Gili mulai tanpa charger di 8%**: hanya `Battery 8%` (kritis), tanpa peringatan rendah; mulai tercolok tidak memunculkan wide apa pun. Dites di Task 3 (`Start_below_both_thresholds_shows_only_critical`, `Start_plugged_in_is_quiet`).
4. **Menyembunyikan halaman yang sedang terbuka, atau semua kecuali satu**: pulau pindah ke tab terdekat, saklar terakhir nonaktif, dan tidak pernah nol tab. Dites di Task 5 (`Fallback_prefers_the_left_neighbour`, `Last_visible_page_cannot_be_turned_off`, `Normalize_never_hides_every_tab`).
5. **Key registry yang aneh** (app paket tanpa `_`, path tanpa `#`, path kosong, nama sangat panjang): nama cadangan dipakai, tanpa crash. Dites di Task 4 (`Names_fall_back_for_odd_keys`).

---

### Task 1: Mockup, PRD, dan desain

**Files:** Modify `docs/mockup/top-taskbar-mockup.html`, `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md`, `docs/superpowers/specs/2026-10-31-fase-3a-pulau-sistem-design.md`, `scripts/mockup-shot.ps1`.

- [ ] **Step 1:** Mockup: aksi `num` (`Num Lock on/off`, ikon `i-num`), `batHold` (`Charging on hold · 79%`, petir accent), `bat10` (`Battery 10%`, petir danger), `batFull` (`Fully charged`), `bat20` memakai persen saat itu, dan tombolnya di panel kontrol. Ikon baru `i-num`, `i-mouse`, `i-keyboard`, `i-gamepad`, `i-phone` dengan gaya ikon yang ada. `SET` mendapat baris `W.unplug` (`Unplug reminder`, 50–100, default 80) dan help `Also at 10%` di `W.bat`; saklar Island untuk halaman yang ikon kirinya mati nonaktif dengan `Turned off in Left side`, dan saklar tab terakhir nonaktif.
- [ ] **Step 2:** PRD: 6.3 tabel "Kejadian wide" (Num Lock, on hold, 10%, penuh, aturan pengingat cabut "melewati ambang sambil masih mengisi", deteksi on hold), 6.5 Right side (`Unplug reminder`), 8.1 (ganti "hook Caps/Num Lock yang sudah ada" dengan "pembacaan status Caps/Num Lock 8×/detik tanpa hook"; tambah empat baris anggaran dari desain), F-ISL-11 (ambang rendah dan pengingat cabut bisa diubah; on hold). Desain: ikon baterai mengikuti mockup (Keputusan 2).
- [ ] **Step 3:** `node --check` pada skrip yang diekstrak lolos; probe Edge tanpa error konsol; tiga state baru di `mockup-shot.ps1` (`wide-numlock`, `wide-hold`, `settings-island-hidden`) menghasilkan PNG.
- [ ] **Step 4: Commit** `docs: lock keys, charge hold, unplug reminder and hidden tab switches in the mockup and PRD`.

### Task 2: Isi wide, Caps/Num Lock, dan Bluetooth (Core)

**Files:** Modify `src/Gili.Core/Island/IslandModels.cs`, `src/Gili.Core/Power/PowerRules.cs`; Create `src/Gili.Core/Devices/LockKeyRules.cs`, `src/Gili.Core/Devices/BluetoothRules.cs`; Test `tests/Gili.Core.Tests/Devices/LockKeyRulesTests.cs`, `BluetoothRulesTests.cs`, `tests/Gili.Core.Tests/Power/*`, `tests/Gili.Core.Tests/Island/IslandCoordinatorTests.cs`.

**Interfaces (Produces):**
- `public enum WideIcon { None, Bolt, Caps, NumLock, Bluetooth, Headphones, Mouse, Keyboard, Gamepad, Phone }`.
- `public enum WideTone { Text, Accent, Warn, Danger, Blue, Muted }` (warna ikon).
- `public enum WideRunStyle { Normal, Strong, Muted, Number }` (Strong = semibold + digit tabular; Number = digit tabular).
- `public sealed record WideRun(string Text, WideRunStyle Style = WideRunStyle.Normal, bool SpaceBefore = false)` (SpaceBefore = jarak 8 px sebelum run).
- `public sealed record WideEvent(string Key, WideIcon Icon, WideTone Tone, IReadOnlyList<WideRun> Runs, TimeSpan Duration)` dengan `string Text` (gabungan run; run ber-`SpaceBefore` didahului satu spasi), dipakai untuk tes dan nama aksesibel.
- `public readonly record struct LockKeys(bool Caps, bool Num)`; `LockKeyRules.WideFor(LockKeys? previous, LockKeys current) → IReadOnlyList<WideEvent>` (kunci `caps` dan `num`, 1,5 detik, `[Run("Caps Lock "), Run("on", Strong)]`, ikon `Caps`/`NumLock`, tone Text; pembacaan pertama → kosong).
- `public enum BluetoothKind { Other, Audio, Mouse, Keyboard, Gamepad, Phone }`; `public sealed record BluetoothChange(string Id, string Name, BluetoothKind Kind, bool Connected, int? Battery)`.
- `BluetoothRules.KindFromClass(uint classOfDevice)`: major `(cod >> 8) & 0x1F`: 2 → Phone, 4 → Audio, 5 → periferal: minor `(cod >> 6) & 0x3` 1 → Keyboard, 2 → Mouse, 3 → Keyboard; selain itu `(cod >> 2) & 0xF` 1 atau 2 → Gamepad; lainnya Other.
- `BluetoothRules.KindFromAppearance(ushort appearance)`: kategori `appearance >> 6`: 1 → Phone; 15 → sub `appearance & 0x3F` 1 → Keyboard, 2 → Mouse, 3 atau 4 → Gamepad; 33 atau 37 → Audio; lainnya Other.
- `BluetoothRules.WideFor(BluetoothChange c) → WideEvent`: kunci `bt:<Id>`; tersambung → ikon jenis (`Other` → `Bluetooth`), tone Blue, runs `[name, ("Connected", Muted, SpaceBefore), ("80%", Number, SpaceBefore) bila ada]`, 4 detik; terputus → tone Muted, `[name, ("Disconnected", Muted, SpaceBefore)]`, 2 detik. Nama kosong → `Bluetooth device`; nama dipotong 32 karakter dengan `…`.
- `PowerRules.WideFor` mengembalikan bentuk baru: `Charging · ` + `64%` Strong, tone Accent, ikon Bolt; `On battery · ` + `63%` Strong, tone Text.

- [ ] **Step 1: Tes yang gagal**: `Lock_keys_first_reading_is_quiet`, `Caps_on_and_off_texts` (`Caps Lock on`, run `on` Strong, 1,5 s), `Caps_and_num_together_queue_caps_first`, `Class_of_device_maps_to_kind` (0x240404 → Audio, 0x002540 → Keyboard, 0x002580 → Mouse, 0x002508 → Gamepad, 0x5A020C → Phone, 0x1F00 → Other), `Appearance_maps_to_kind` (0x03C1 → Keyboard, 0x03C2 → Mouse, 0x03C4 → Gamepad, 0x0941 → Audio, 0x0040 → Phone, 0 → Other), `Connected_text_has_name_state_and_battery` (`Galaxy Buds2 Pro Connected 80%`, tone Blue, 4 s), `Disconnected_is_muted_and_short`, `Unnamed_device_reads_bluetooth_device`, `Charger_wide_uses_runs` (perbarui tes power lama), dan di koordinator `Bluetooth_flapping_keeps_only_the_latest` (sambung, putus, sambung untuk id yang sama dalam 1 s → satu wide, teks `Connected`).
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~LockKey|FullyQualifiedName~Bluetooth|FullyQualifiedName~Power|FullyQualifiedName~IslandCoordinator"` gagal kompilasi.
- [ ] **Step 3:** Implementasi. Pemanggil lama (`IslandViewModel` bagian wide, `IslandWindow.xaml` `WideContent`) diperbaiki secukupnya supaya build lulus; tampilan run lengkap di Task 7.
- [ ] **Step 4:** Semua tes lulus; `dotnet build Gili.slnx` 0 warning.
- [ ] **Step 5: Commit** `feat(core): wide notices in runs, lock key and Bluetooth notices (F-ISL-10, F-ISL-18)`.

### Task 3: Peringatan baterai dan settings-nya (Core + Data)

**Files:** Modify `src/Gili.Core/Power/PowerRules.cs`, `src/Gili.Core/Settings/AppSettings.cs`, `src/Gili.Core/Settings/SettingsCatalog.cs`; Create `src/Gili.Core/Power/BatteryAlerts.cs`; Test `tests/Gili.Core.Tests/Power/BatteryAlertsTests.cs`, `tests/Gili.Core.Tests/Settings/*`, `tests/Gili.Core.Tests/Data/SettingsStoreTests.cs`.

**Interfaces (Produces):**
- `PowerSnapshot` mendapat `bool OnHold => HasBattery && PluggedIn && !Charging && Percent < 100`.
- `public sealed class BatteryAlerts` dengan `IReadOnlyList<WideEvent> Update(PowerSnapshot current, int lowAt, int unplugAt)`; menyimpan snapshot sebelumnya dan tanda sudah-muncul per siklus. Kunci `charger` untuk wide colok/cabut dan on hold saat dicolok; kunci `battery` untuk peringatan.
- `AppSettings.BatteryAlert = 20` (dijepit 5–50), `AppSettings.UnplugReminder = 80` (dijepit 50–100).
- `SettingsCatalog`: `W.bat` tersedia (help `Also at 10%`) dan dipetakan ke `BatteryAlert`; baris baru `W.unplug` (`Unplug reminder`, Range, `%`, help di Global Constraints, rentang 50–100) setelah `W.bat`.

Aturan `Update` (urutan hasil = urutan tampil):
- Tanpa baterai: kosong. Pembacaan pertama: tanpa wide charger; tercolok → tandai on hold, cabut (bila persen > ambang), dan penuh (bila 100) sebagai sudah muncul, kosong; tidak tercolok → peringatan kritis (≤ 10) atau rendah (≤ `lowAt`) sekali.
- Dicolok: atur ulang semua tanda; `OnHold` → satu wide `Charging on hold · ` + `79%` (kunci `charger`, accent, 5 s) dan tandai on hold; selain itu wide `Charging · 64%`; persen 100 saat dicolok → tandai penuh.
- Dicabut: atur ulang tanda on hold, cabut, penuh; wide `On battery · 63%`; lalu cek rendah/kritis.
- Tidak tercolok: ≤ 10 dan kritis belum → `Battery ` + `8%` (danger), tandai kritis dan rendah; ≤ `lowAt` dan rendah belum → `Battery ` + `18%` + ` · plug in` (warn).
- Tercolok: `OnHold` dan on hold belum → wide on hold (kunci `battery`); `Charging` dan persen > `unplugAt` dan `unplugAt` < 100 dan cabut belum → `80%` + ` reached · unplug` (accent, angka = `unplugAt`); persen 100 dan penuh belum → `Fully charged` (accent).

- [ ] **Step 1: Tes yang gagal**: `Low_alert_once_per_discharge`, `Critical_replaces_low_below_ten`, `Start_below_both_thresholds_shows_only_critical`, `Start_plugged_in_is_quiet`, `Plugging_in_resets_low_alerts`, `Plug_in_on_hold_says_on_hold_once` (dicolok di 79% tidak mengisi → satu wide `Charging on hold · 79%`, bukan `Charging`), `Hold_flapping_alerts_once_per_plug`, `Unplug_reminder_waits_until_past_the_threshold` (80% mengisi → tidak ada; 81% → `80% reached · unplug`), `Unplug_reminder_off_at_100`, `Fully_charged_once_per_plug`, `No_battery_is_quiet`, `Battery_settings_are_clamped` (`BatteryAlert` 2 → 5, 70 → 50; `UnplugReminder` 30 → 50, 120 → 100), `Unplug_reminder_row_reads_and_writes`, dan di Data `Battery_settings_survive_a_restart`.
- [ ] **Step 2:** `--filter "FullyQualifiedName~Battery|FullyQualifiedName~Settings|FullyQualifiedName~Power"` gagal.
- [ ] **Step 3:** Implementasi. `PowerRules.WideFor` tetap dipakai `BatteryAlerts` untuk teks colok/cabut.
- [ ] **Step 4:** Semua tes lulus; build 0 warning.
- [ ] **Step 5: Commit** `feat(core): battery alerts that know a charge limit, and their settings (F-ISL-11)`.

### Task 4: Titik privasi (Core)

**Files:** Create `src/Gili.Core/Devices/Privacy.cs`; Modify `src/Gili.Core/Island/IslandLayout.cs`, `src/Gili.Core/Layout/BarMetrics.cs` (bila `BarSpacing.LeftLimit` perlu langkah titik); Test `tests/Gili.Core.Tests/Devices/PrivacyTests.cs`, `tests/Gili.Core.Tests/Island/IslandLayoutTests.cs`.

**Interfaces (Produces):**
- `public sealed record PrivacyUse(IReadOnlyList<string> CameraApps, IReadOnlyList<string> MicrophoneApps)` dengan `static PrivacyUse None`, `bool Camera`, `bool Microphone`, `int Count` (jumlah titik aktif), dan kesamaan isi (`SequenceEqual`).
- `PrivacyText.Camera(IReadOnlyList<string> apps)` → `Camera in use: Zoom` / `Camera in use: Zoom, Discord`; `PrivacyText.Microphone(...)` serupa.
- `PrivacyNames.IsInUse(long start, long stop) => start > 0 && stop == 0`.
- `PrivacyNames.ExePath(string keyName)` → `#` diganti `\` (key `NonPackaged`); `PrivacyNames.Fallback(string keyName, bool packaged)`: paket → bagian sebelum `_` lalu setelah titik terakhir (`Microsoft.Copilot_8wekyb3d8bbwe` → `Copilot`); exe → nama file tanpa ekstensi (`C:#Users#…#Zoom.exe` → `Zoom`); hasil kosong → `An app`; dipotong 40 karakter.
- `public readonly record struct DotPosition(double X, double Y)`; `IslandLayout.PrivacyDots(double islandLeft, double islandTop, double islandHeight, double circleSpace, bool floating, bool circleBesideFloating, int count) → IReadOnlyList<DotPosition>` (kiri-atas tiap titik 8 px, urutan kamera dulu): compact `X = islandLeft − 14 − circleSpace − k·13`, `Y = islandTop + islandHeight/2 − 4`; mengambang `X = islandLeft − 14`, `Y = islandTop + (circleBesideFloating ? islandHeight + 8 : 20) + k·13`. `circleSpace` = (tinggi pulau + 6) × kemajuan pegas lingkaran Gili.

- [ ] **Step 1: Tes yang gagal**: `In_use_needs_a_start_and_no_stop`, `Tooltip_lists_every_app`, `Names_fall_back_for_odd_keys` (`Copilot`, `Zoom`, `Contoso_abc` → `Contoso`, `NoHashes.exe` → `NoHashes`, `""` → `An app`, nama 60 karakter dipotong 40), `Dots_line_up_left_of_the_island_camera_first` (compact, pulau kiri 860, atas 4, tinggi 24, tanpa lingkaran, 2 titik → (846, 12) dan (833, 12)), `Dots_make_room_for_gilis_circle` (circleSpace 30 → titik pertama X 816), `Floating_dots_stack_down_the_panel_edge` (atas 4 → Y 24 lalu 37; dengan lingkaran di samping → Y 36), dan perbarui tes `BarSpacing.LeftLimit` untuk `privacyDots` 2.
- [ ] **Step 2:** `--filter "FullyQualifiedName~Privacy|FullyQualifiedName~IslandLayout|FullyQualifiedName~BarSpacing"` gagal.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** Semua tes lulus; build 0 warning.
- [ ] **Step 5: Commit** `feat(core): privacy dot texts, app names and positions (F-ISL-14)`.

### Task 5: Tab dan ikon kiri yang bisa disembunyikan (Core + Data)

**Files:** Modify `src/Gili.Core/Settings/AppSettings.cs`, `src/Gili.Core/Settings/SettingsCatalog.cs`; Create `src/Gili.Core/Island/IslandTabs.cs`; Test `tests/Gili.Core.Tests/Island/IslandTabsTests.cs`, `tests/Gili.Core.Tests/Settings/*`, `tests/Gili.Core.Tests/Data/SettingsStoreTests.cs`.

**Interfaces (Produces):**
- `[Flags] public enum LeftIcons { None = 0, Start = 1, Timer = 2, Alarms = 4, Todo = 8, Notes = 16, Clipboard = 32, Shelf = 64, Claude = 128 }` dan `[Flags] public enum PageSet { None = 0, Media = 1, Timer = 2, Alarms = 4, Calendar = 8, Todo = 16, Notes = 32, Clipboard = 64, Shelf = 128, Claude = 256, System = 512, All = 1023 }` di `Gili.Core.Settings`.
- `AppSettings.HiddenLeft = LeftIcons.None`, `AppSettings.HiddenPages = PageSet.None`, `PageSet HiddenTabs` (gabungan efektif), `Normalize()` membuka `Media` di `HiddenPages` kalau `HiddenTabs == All`.
- `IslandTabs.Menu` (10 halaman urut menu, tanpa Controls), `IslandTabs.Of(IslandPage) → PageSet`, `IslandTabs.Visible(PageSet hidden, IslandPage? temporary) → IReadOnlyList<IslandPage>` (temporary disisipkan di posisi aslinya), `IslandTabs.Step(IReadOnlyList<IslandPage> visible, IslandPage current, int direction) → IslandPage` (dijepit di ujung; halaman di luar daftar → tetap), `IslandTabs.Fallback(PageSet hidden, IslandPage page) → IslandPage` (terdekat di kiri, lalu kanan).
- `SettingsCatalog`: baris `L.*` dan `T.*` tersedia. `L.<n>` baca `!HiddenLeft.HasFlag`; tulis true → hapus bit kiri dan bit halaman bernama sama; tulis false → pasang bit kiri. `T.<n>` baca `!HiddenTabs.HasFlag`; tulis → ubah bit `HiddenPages`.
- `public readonly record struct RowState(bool Enabled, string? Note)`; `SettingsCatalog.State(AppSettings s, string key) → RowState`: `T.<n>` yang ikon kirinya mati → (false, `Turned off in Left side`); `T.<n>` yang satu-satunya tab terlihat → (false, null); `L.<n>` yang halamannya satu-satunya tab terlihat → (false, null); lainnya (true, null).

- [ ] **Step 1: Tes yang gagal**: `Hidden_tabs_leave_the_menu`, `Temporary_tab_returns_at_its_place` (Timer tersembunyi dibuka langsung → muncul di indeks 1), `Arrows_skip_hidden_tabs` (Media → Alarms bila Timer tersembunyi; ujung kanan dijepit), `Fallback_prefers_the_left_neighbour` (Calendar disembunyikan saat terbuka, Alarms terlihat → Alarms; Media → tab terlihat pertama di kanan), `Left_icon_off_hides_its_page_and_locks_the_switch`, `Left_icon_on_brings_its_page_back`, `Last_visible_page_cannot_be_turned_off`, `Normalize_never_hides_every_tab`, `Start_switch_has_no_page`, dan di Data `Hidden_tabs_survive_a_restart` (JSON string enum, misalnya `"hiddenPages": "timer, notes"`).
- [ ] **Step 2:** `--filter "FullyQualifiedName~IslandTabs|FullyQualifiedName~Settings"` gagal.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** Semua tes lulus; build 0 warning.
- [ ] **Step 5: Commit** `feat(core): hideable island tabs and left icons, linked like the PRD says (F-ISL-23, F-BAR-12)`.

### Task 6: Sumber data di Platform

**Files:** Create `src/Gili.Platform/Devices/LockKeyMonitor.cs`, `src/Gili.Platform/Devices/PrivacyMonitor.cs`; Modify `src/Gili.Platform/Modules/BluetoothMonitor.cs`.

**Interfaces (Produces):**
- `public sealed class LockKeyMonitor : IDisposable` — `LockKeys Current`, `event Action<LockKeys>? Changed` (thread pool, hanya saat berubah), `void SetPaused(bool paused)` (timer berhenti; saat lanjut, baca ulang tanpa memicu `Changed` untuk perubahan selama jeda), timer `System.Threading.Timer` 125 ms, `GetKeyState(0x14) & 1` dan `GetKeyState(0x90) & 1` (`DllImport` seperti `ClipboardMonitor`).
- `public sealed class PrivacyMonitor : IDisposable` — `PrivacyUse Current`, `event Action<PrivacyUse>? Changed` (thread latar), `void Start()`. Satu thread latar menunggu dua `RegNotifyChangeKeyValue` (subtree, `REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET`, asinkron dengan event) pada `HKCU\Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\webcam` dan `\microphone`, ditambah event berhenti untuk `Dispose`. Setiap notifikasi: daftar ulang (subkey langsung = paket, subkey `NonPackaged` = exe; `LastUsedTimeStart`/`LastUsedTimeStop` QWORD), daftarkan ulang notifikasi, terbitkan bila isinya berubah. Nama: paket → `PackageManager.FindPackagesForUser("", family).FirstOrDefault()?.DisplayName`, exe → `FileVersionInfo.FileDescription`; kosong atau gagal → `PrivacyNames.Fallback`; cache per key. Exe Gili sendiri dan keluarga paket Gili dilewati. Key yang tidak ada → `PrivacyUse.None`, tanpa crash.
- `BluetoothMonitor` mendapat `event Action<BluetoothChange>? DeviceChanged` (thread latar). Tiap watcher dibuat dengan properti tambahan `{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2` (baterai, byte 0–100). `Added`/`Removed` sebelum `EnumerationCompleted` watcher itu hanya mengisi daftar. Setelahnya `Added` → `Connected = true`, `Removed` → `false` (nama dari daftar). Jenis: perangkat klasik `BluetoothDevice.FromIdAsync(id)` → `ClassOfDevice.RawValue` → `KindFromClass`; LE `BluetoothLEDevice.FromIdAsync(id)` → `Appearance.RawValue` → `KindFromAppearance`; gagal → `Other`. Saat radio berubah ke Off, `Removed` yang menyusul tidak diterbitkan sampai radio On lagi.

- [ ] **Step 1:** Implementasi ketiganya. Tidak ada tes otomatis (Win32/WinRT); logika yang bisa dites sudah di Task 2 dan 4.
- [ ] **Step 2:** Build 0 warning. Cek manual dengan program kecil sekali pakai di scratchpad atau lewat Task 7: toggle Caps Lock memicu `Changed` ≤ 125 ms; membuka Camera Windows memunculkan kamera dengan nama `Camera` ≤ 2 detik dan hilang ≤ 2 detik setelah ditutup.
- [ ] **Step 3: Commit** `feat(platform): lock key, camera and microphone, and Bluetooth device events (F-ISL-10, F-ISL-14, F-ISL-18)`.

### Task 7: Wide dan peringatan di pulau, plus demo (App)

**Files:** Modify `src/Gili.App/Island/IslandController.cs`, `IslandViewModel.cs`, `IslandWindow.xaml`, `IslandWindow.xaml.cs`, `src/Gili.App/Resources/Icons.xaml`, `src/Gili.App/Bar/RightService.cs`, `src/Gili.App/App.xaml.cs`, `src/Gili.App/Demo/*` (pemutar demo), `src/Gili.Core/Demo/DemoScript.cs`; Test `tests/Gili.Core.Tests/Demo/DemoTests.cs`.

**Interfaces:**
- Consumes: Task 2 (`WideEvent`, `LockKeyRules`, `BluetoothRules`), Task 3 (`BatteryAlerts`, `AppSettings.BatteryAlert/UnplugReminder`), Task 6 (monitor).
- Produces: `IslandController(…, BluetoothMonitor bluetooth, Func<AppSettings> settings)`; `void SetPaused(bool paused)` (meneruskan ke `LockKeyMonitor`); demo: `SetDemoLockKeys(LockKeys)`, `ShowDemoBluetooth(BluetoothChange)`, `SetDemoPrivacy(PrivacyUse)`; `PrivacyUse Privacy` dan `event Action? PrivacyChanged` untuk Task 8. Aksi demo baru: `DemoLockKeys(LockKeys Keys)`, `DemoBluetooth(BluetoothChange Change)`, `DemoPrivacy(PrivacyUse Use)`.

- [ ] **Step 1:** Wide: `IslandWindow` membangun `Inlines` dari `WideRun` (Strong = SemiBold + `NumeralAlignment.Tabular`, Muted = `#8E8E93`, Number = Tabular, `SpaceBefore` = run kosong dengan margin 8 lewat `InlineUIContainer` atau `Run` dengan spasi lebar tetap, pilih yang lebarnya tepat 8 DIP). Ikon dari `WideIcon` → `Icon.*`, warna dari `WideTone`. Nama aksesibel wide = `WideEvent.Text`. `Icons.xaml` mendapat ikon Keputusan 3.
- [ ] **Step 2:** `IslandController`: `BatteryAlerts.Update(snapshot, s.BatteryAlert, s.UnplugReminder)` menggantikan `PowerRules.WideFor` di `ApplyPower`; `LockKeyMonitor.Changed` → `LockKeyRules.WideFor` → `ShowWide`; `BluetoothMonitor.DeviceChanged` → `BluetoothRules.WideFor` → `ShowWide`; `PrivacyMonitor.Changed` → `Privacy`/`PrivacyChanged`. Selama demo, kejadian nyata ditahan seperti power. `App.xaml.cs`: satu `BluetoothMonitor` untuk `RightService` dan controller (Keputusan 4); `SetPaused` ikut di blok `poll` baris 144.
- [ ] **Step 3:** Demo: setelah langkah 42 tambahkan `52.5 DemoLockKeys(Caps on)`, `54.5 DemoBluetooth(Galaxy Buds2 Pro, Audio, connected, 80)`, `59 DemoBluetooth(… disconnected)`, `61.5 DemoPower(tanpa charger, 20%)`, `67 DemoPrivacy(Zoom kamera, Discord mic)`, `71 DemoPower(tercolok, tidak mengisi, 79%)`, `73 DemoPrivacy(None)`, akhir `76 DemoEnd`; `EndDemo` mengembalikan lock keys, privasi, dan power nyata tanpa wide. Tes demo: urutan langkah naik dan aksi baru ada.
- [ ] **Step 4:** Build 0 warning, tes lulus. Manual: Caps Lock dan Num Lock menampilkan wide sesuai mockup; Bluetooth earbuds/mouse pengguna tersambung dan terputus; mode demo memperlihatkan semua kejadian. Bandingkan potret jendela (PrintWindow) dengan state mockup `wide-*`.
- [ ] **Step 5: Commit** `feat(island): lock keys, battery alerts and Bluetooth devices in the wide island, and in the demo (F-ISL-10, F-ISL-11, F-ISL-18)`.

### Task 8: Titik kamera/mic di pulau (App)

**Files:** Modify `src/Gili.App/Island/IslandWindow.xaml`, `IslandWindow.xaml.cs`, `IslandViewModel.cs`, `src/Gili.App/Bar/BarWindow.xaml.cs`, `src/Gili.App/App.xaml.cs`.

**Interfaces:** Consumes Task 4 (`IslandLayout.PrivacyDots`, `PrivacyText`) dan Task 7 (`IslandController.Privacy`, `PrivacyChanged`).

- [ ] **Step 1:** Dua titik di kanvas `IslandWindow` (Ellipse 8 × 8, isi warna Global Constraints, tepi `#59000000` 1,5) dalam `Border` transparan 20 × 20 untuk area hover, tooltip dari `PrivacyText`, nama aksesibel sama. Posisi dihitung tiap frame pegas dari `IslandLayout.PrivacyDots` dengan `circleSpace = (IslandHeight + 6) × _giliCircle.Value`; skala lewat pegas baru per titik (`GiliRules.ShapeSpring`); Animation effects mati → opacity 100 ms. Tersembunyi saat mode `Hidden` (fullscreen).
- [ ] **Step 2:** Bar: `CompactChanged` membawa jumlah titik (`Action<double, bool, int>`), dan `BarSpacing.LeftLimit(…, privacyDots: n)` memakai angka itu, jadi isi kiri bar tidak pernah tertimpa. Fade elemen tertimpa saat mengambang memperhitungkan titik di sisi kiri panel.
- [ ] **Step 3:** Build 0 warning, tes lulus. Manual: buka app Camera Windows dan Voice Recorder (atau rekam di app apa pun): titik muncul ≤ 2 detik dengan tooltip nama app dan hilang ≤ 2 detik setelah ditutup; buka pulau (titik bersusun di kiri panel); cek di atas jendela putih (Notepad maksimal) bahwa tepi titik terlihat. Bandingkan potret dengan mockup tombol Privacy.
- [ ] **Step 4: Commit** `feat(island): camera and microphone dots beside the island (F-ISL-14)`.

### Task 9: Saklar tab dan ikon kiri (App)

**Files:** Modify `src/Gili.App/Island/IslandViewModel.cs`, `IslandWindow.xaml.cs`, `IslandController.cs`, `src/Gili.App/Bar/LeftSide.xaml`, `LeftSide.xaml.cs`, `src/Gili.App/Bar/BarWindow.xaml.cs`, `src/Gili.App/Settings/SettingRowViewModel.cs`, `SettingsViewModel.cs`, `src/Gili.App/App.xaml.cs`.

**Interfaces:** Consumes Task 5 (`IslandTabs`, `AppSettings.HiddenLeft/HiddenPages/HiddenTabs`, `SettingsCatalog.State`).

- [ ] **Step 1:** Pulau: `IslandViewModel.Tabs` dihitung dari `IslandTabs.Visible(settings.HiddenTabs, temporary)` dengan temporary = halaman terbuka yang tersembunyi; `IslandWindow.BuildTabs` dibangun ulang saat daftar berubah; lebar dari `TabMenuLayout.Need(IslandTabs.Menu.Count, …)`; panah/geser memakai `IslandTabs.Step`. Saat settings menyembunyikan halaman yang sedang terbuka, controller membuka `IslandTabs.Fallback`.
- [ ] **Step 2:** Bar: `LeftSide.ApplyHidden(LeftIcons hidden, bool animate)`; ikon yang dimatikan mengecil ke 0,6 dan memudar 160 ms seperti `Leave`, sambil lebarnya beranimasi ke 0 dalam 300 ms `BezierEase.Out` lalu `Collapsed`; menyala kembali dengan `Pop` 320 ms. Pemisah hanya tampil bila ada ikon di kirinya. Animation effects mati → langsung.
- [ ] **Step 3:** Settings: `SettingRowViewModel` mendapat `IsEnabled` dan `Note` yang bisa berubah; `Reload` menerapkan `SettingsCatalog.State` (catatan mesin dari `NoteFor`/`CanChange` tetap didahulukan).
- [ ] **Step 4:** Build 0 warning, tes lulus. Manual: matikan ikon Timer (ikon hilang tanpa celah, tab Timer hilang, saklar Timer page nonaktif dengan `Turned off in Left side`); klik slot timer saat tab tersembunyi (tab sementara muncul di posisinya, hilang setelah pindah halaman); matikan halaman yang sedang terbuka; matikan sampai sisa satu (saklarnya nonaktif); restart Gili, pilihan tetap. Bandingkan potret Settings dengan mockup `settings-island-hidden`.
- [ ] **Step 5: Commit** `feat(app): hide island tabs and left icons from Settings (F-ISL-23, F-BAR-12)`.

### Task 10: Verifikasi

- [ ] **Step 1:** `dotnet build Gili.slnx -c Release` 0 warning, `dotnet test tests/Gili.Core.Tests` lulus, `dotnet format Gili.slnx --verify-no-changes` bersih.
- [ ] **Step 2:** `pwsh scripts/publish.ps1` dan `pwsh scripts/package.ps1 -Install`: di kedua bentuk, Caps Lock wide muncul, titik kamera muncul saat app Camera dibuka (registry HKCU terbaca dari paket), saklar tab tersimpan setelah restart, dan `Unplug reminder` tersimpan.
- [ ] **Step 3:** Anggaran 8.1: CPU diam dengan Gili berjalan < 1% dan tidak naik dibanding sebelum fase ini lebih dari 0,05 poin (ukur 60 detik, Tools tertutup); RAM private tidak naik > 2 MB dibanding sebelum fase ini.
