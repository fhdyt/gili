# Fase 3c: Modul Kanan dan Halaman System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bar kanan bisa diatur dari Settings → Right side (saklar dua belas modul, urutan enam modul sistem), GPU dan Disk tersedia sebagai modul, modul yang mati berhenti membaca datanya, dan halaman System menampilkan grafik 60 detik (F-RIGHT-03, F-RIGHT-04, F-BAR-12 kanan, F-ISL-07 System).

**Architecture:** Core mendapat aturan yang dites: `RightModules` + `ModuleOrder` di `AppSettings`, baris `M.*` di `SettingsCatalog`, `StatsDemand`, `GpuEngines`, `MetricHistory`, `Sparkline`, dan format disk. Platform menambah `GpuSampler` (PDH wildcard) dan `DiskSampler` (`DriveInfo`). App: `StatsService` hanya menjalankan sampler yang diminta, `BarWindow` menyusun dan menyembunyikan modul dengan animasi bersama Left side, Settings bisa mengurutkan baris, dan `SystemPage` baru di pulau.

**Tech Stack:** .NET 10 WPF, PDH (`pdh.dll`), `System.IO.DriveInfo`, CommunityToolkit.Mvvm, xUnit.

**Spec:** [desain Fase 3c](../specs/2026-10-31-fase-3c-modul-kanan-design.md), PRD 6.3 tabel halaman baris 10, 6.4, 6.5, 8.1, 9.2. Mockup: `.sys`, `.tile`, `.sp`, `.dbar`, `renderSys`, `sparkPts`, `S.mods`, `HIDE_ORDER`, `SET['Right side']`.

## Global Constraints

- Teks persis: `GPU`, `C: FREE` (huruf drive mengikuti drive Windows), `Free space on C:`, `106 GB`, `Disk C:`, `106 GB free`, `CPU`, `RAM`, `Download`, `Open Task Manager`, `GPU · 4%`, `Disk · 106 GB free`, nama saklar `Weather`, `Network`, `CPU`, `RAM`, `GPU`, `Disk`, `Wi‑Fi` (U+2011), `Bluetooth`, `Volume`, `Brightness`, `Tray icons`, `Clock and date`.
- Id modul: `weather`, `net`, `cpu`, `ram`, `gpu`, `disk`, `wifi`, `bt`, `vol`, `bri`, `tray`, `clock`. Urutan overflow tetap `weather, net, disk, gpu, ram, cpu`.
- Default: GPU dan Disk mati; urutan `weather, net, cpu, ram, gpu, disk`.
- Satuan GB = 1024³; ≥ 10 GB dibulatkan tanpa desimal, < 10 GB satu desimal.
- Riwayat 60 nilai; skala Download = maks(60 detik, 100 KB/s).
- Halaman System: grid 3 kolom, jarak 8; kotak `#1C1C1E`, radius 12, padding 8,12, tinggi 70; nama 12 px `#8E8E93`; nilai 14 px semibold tabular; grafik 26 px, garis 1,6; warna CPU `#0A84FF`, RAM `#BF5AF2`, GPU `#30D158`, Download `#64D2FF`; bilah disk 6 px radius 3, latar `#3A3A3C`, isi `#8E8E93`.
- Animasi saklar: lebar slot 300 ms ease-out (`BezierEase.Out`), sama dengan Left side; tanpa animasi bila `SystemParameters.ClientAreaAnimation` mati.
- Disk dibaca tiap 10 detik; GPU, CPU, RAM, jaringan tiap 1 detik; semuanya hanya bila diminta.

## Keputusan yang dibuat di rencana ini

1. **Modul yang sedang di "…" tetap dibaca**, karena daftar "…" menampilkan angkanya.
2. **Sampler yang dihentikan dibuat ulang saat diminta lagi** (CPU membuang bacaan pertama yang selalu 0), dan riwayatnya dikosongkan.
3. **Halaman System memakai `StatsService` yang sama** lewat `StatsService.Demand`; tidak ada provider palsu baru karena halaman ini tidak muncul sendiri (sama seperti pulau kontrol di 3b).
4. **Urutan di Settings disimpan saat seret dilepas**, bukan per baris yang dilewati, supaya bar tidak bergerak berkali-kali selama seret.

## Review Focus

1. **settings.json lama tanpa `HiddenModules`/`ModuleOrder`, atau `ModuleOrder` rusak** (id ganda, id tak dikenal, id hilang): GPU/Disk mati, urutan dipulihkan tanpa crash. Dites di Task 2 (`Old_settings_hide_gpu_and_disk`, `Order_repairs_duplicates_unknown_and_missing`).
2. **Semua modul sistem dimatikan dan halaman System ditutup**: tidak ada sampler yang jalan; membuka halaman System menyalakan kelimanya. Dites di Task 3 (`Nothing_shown_samples_nothing`, `System_page_asks_for_everything`).
3. **Mesin tanpa counter GPU (VM, driver dasar)**: modul dan kotak GPU menampilkan `—` dengan alasan, bar tetap jalan. Dites di Task 3 (`No_engines_reads_zero`) dan Task 4 (sampler melempar → `ModuleSampler` memberi Error).
4. **Satu engine dipakai banyak proses sampai jumlahnya > 100**: dibatasi 100. Dites di Task 3 (`Engine_sum_is_capped_at_100`).
5. **Memindahkan baris pertama ke atas atau baris terakhir ke bawah** (Alt + panah di tepi): urutan tidak berubah. Dites di Task 2 (`Move_at_the_edges_does_nothing`).

---

### Task 1: PRD, mockup, desain

**Files:** Modify `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md`, `docs/mockup/top-taskbar-mockup.html`; Create (sudah) `docs/superpowers/specs/2026-10-31-fase-3c-modul-kanan-design.md`.

- [ ] **Step 1:** PRD 6.4: kalimat "Tampil/sembunyi" menyebut pegangan seret di enam baris modul sistem dan Alt + panah atas/bawah; "Sampai Fase 3" butir CPU/RAM → `Coming in Phase 3` ditandai selesai di 3c; tabel modul Disk menyebut huruf drive Windows dan tooltip `Free space on C:`. PRD 9.2 baris CPU/GPU/disk: GPU lewat PDH `GPU Engine(*)` (jumlah per engine, maksimum), disk lewat `GetDiskFreeSpaceEx` tiap 10 detik; semuanya hanya saat modul nyala atau halaman System terbuka. PRD 8.1: tiga baris anggaran dari desain (Modul GPU, Modul disk, Halaman System).
- [ ] **Step 2:** Mockup: baris `M.weather` … `M.disk` di Settings → Right side mendapat pegangan `i-grip` di kiri label; seret baris (pointer) dan Alt + panah atas/bawah pada saklarnya mengubah `S.order` lalu `layout()` menyusun `.mod[data-m]` sistem mengikuti `S.order`. Default `S.order=['weather','net','cpu','ram','gpu','disk']`.
- [ ] **Step 3:** `node --check` pada skrip mockup (ekstrak isi `<script>` ke file sementara) lolos.
- [ ] **Step 4: Commit** `docs: phase 3c right modules design, PRD and mockup`.

### Task 2: Saklar dan urutan modul di settings (Core)

**Files:** Create `src/Gili.Core/Settings/RightModuleRules.cs`; Modify `src/Gili.Core/Settings/AppSettings.cs`, `src/Gili.Core/Settings/SettingsCatalog.cs`; Test `tests/Gili.Core.Tests/Settings/AppSettingsTests.cs`, `SettingsCatalogTests.cs`, Create `tests/Gili.Core.Tests/Settings/RightModuleRulesTests.cs`.

**Interfaces (Produces):**
- `[Flags] public enum RightModules { None = 0, Weather = 1, Network = 2, Cpu = 4, Ram = 8, Gpu = 16, Disk = 32, Wifi = 64, Bluetooth = 128, Volume = 256, Brightness = 512, Tray = 1024, Clock = 2048 }`.
- `AppSettings.HiddenModules` (default `Gpu | Disk`), `AppSettings.ModuleOrder : string` (id dipisah koma, default `weather,net,cpu,ram,gpu,disk`) dan `[JsonIgnore] AppSettings.Order : IReadOnlyList<string>`. String, bukan daftar, karena `AppSettings` adalah record yang dibandingkan nilainya (temuan saat implementasi).
- `static class RightModuleRules`: `DefaultOrder = ["weather","net","cpu","ram","gpu","disk"]`; `RightModules Of(string id)` (id bar → flag, 12 id); `IReadOnlyList<string> Repair(IEnumerable<string>? order)`; `IReadOnlyList<string> Move(IReadOnlyList<string> order, string id, int delta)`; `IReadOnlyList<string> MoveTo(IReadOnlyList<string> order, string id, int index)`.
- Kunci baris Settings `M.Weather`, `M.Network`, `M.CPU`, `M.RAM`, `M.GPU`, `M.Disk`, `M.Wi‑Fi`, `M.Bluetooth`, `M.Volume`, `M.Brightness`, `M.Tray icons`, `M.Clock and date` (label yang ada) jadi `Available: true`. `SettingsCatalog.ModuleId(string key) → string?` (id untuk enam modul sistem, null untuk lainnya). `SettingsCatalog.RowsOf(SettingsSection section, AppSettings s) → IReadOnlyList<SettingRow>`: Right side menyusun enam baris sistem menurut `ModuleOrder`, bagian lain apa adanya.

- [ ] **Step 1: Tes yang gagal**: `Old_settings_hide_gpu_and_disk` (JSON tanpa kedua field → `HiddenModules == Gpu|Disk`, `ModuleOrder` = default), `Order_repairs_duplicates_unknown_and_missing` (`["cpu","cpu","x","weather"]` → `["cpu","weather","net","ram","gpu","disk"]`), `Move_down_and_up`, `Move_at_the_edges_does_nothing`, `MoveTo_places_at_index`, `Module_switch_reads_and_writes` (`M.GPU` false awalnya; tulis true → flag Gpu hilang dari `HiddenModules`), `Right_side_rows_follow_order` (urutan `["disk","cpu",...]` → baris pertama `M.Disk`), `Module_id_of_rows` (`M.Network` → `net`, `M.Wi‑Fi` → null), `Weather_city_stays_unavailable`.
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~RightModuleRules|FullyQualifiedName~SettingsCatalog|FullyQualifiedName~AppSettings"` → FAIL (tipe belum ada).
- [ ] **Step 3:** Implementasi. `Normalize` memanggil `Repair`. `Write` untuk `M.*` membalik flag dari `Of(ModuleId)`; baris non-sistem dipetakan lewat label (`Wi‑Fi` → Wifi, `Tray icons` → Tray, `Clock and date` → Clock).
- [ ] **Step 4:** Tes yang sama → PASS; seluruh tes Core lulus.
- [ ] **Step 5: Commit** `feat(core): right module switches and order in settings (F-RIGHT-04, F-BAR-12)`.

### Task 3: Aturan statistik (Core)

**Files:** Create `src/Gili.Core/Modules/StatsRules.cs`; Modify `src/Gili.Core/Modules/Readings.cs`, `src/Gili.Core/Formatting/StatText.cs`, `src/Gili.Core/Demo/DemoScript.cs`; Test Create `tests/Gili.Core.Tests/Modules/StatsRulesTests.cs`, Modify `tests/Gili.Core.Tests/Formatting/StatTextTests.cs`, `tests/Gili.Core.Tests/Demo/DemoTests.cs`.

**Interfaces (Produces):**
- `public sealed record GpuReading(double Percent)`; `public sealed record DiskReading(string Drive, ulong FreeBytes, ulong TotalBytes)` (`Drive` = `"C:"`).
- `[Flags] public enum StatsDemand { None = 0, Cpu = 1, Memory = 2, Network = 4, Gpu = 8, Disk = 16, All = 31 }`; `static class StatsDemands { StatsDemand For(RightModules hidden, bool systemPageOpen) }`.
- `static class GpuEngines { double Utilization(IEnumerable<(string Instance, double Value)> samples) }`.
- `sealed class MetricHistory(int capacity = 60) { void Add(double v); void Clear(); IReadOnlyList<double> Values; }`.
- `static class Sparkline { IReadOnlyList<(double X, double Y)> Points(IReadOnlyList<double> values, double max, double width, double height, int capacity = 60); double DownloadMax(IReadOnlyList<double> bytesPerSecond) }` — titik terakhir di `X = width`, langkah `width / (capacity − 1)`, `Y = height − min(1, v/max) × (height − 2) − 1` (mockup `sparkPts`: tinggi 26 → 25 − v × 23).
- `StatText.Disk(ulong bytes) → string` (`106 GB`, `8.4 GB`); `StatText.DiskFree(ulong bytes) → "106 GB free"`.
- Demo: `Step(28.5, new DemoOpen(IslandPage.System))`.

- [ ] **Step 1: Tes yang gagal**: `Nothing_shown_samples_nothing` (hidden = semua enam sistem, false → None), `System_page_asks_for_everything` (→ All), `Modules_ask_for_their_own` (default settings → Cpu|Memory|Network; Weather tidak memengaruhi), `Engines_sum_per_engine_and_take_the_max` (`pid_1_luid_0x0_0x1_phys_0_eng_0_engtype_3D`=2, `pid_2_…eng_0…`=3, `pid_1_…eng_1_engtype_Copy`=4 → 5), `Engine_sum_is_capped_at_100`, `No_engines_reads_zero`, `History_keeps_the_last_60`, `Sparkline_ends_at_the_right_edge` (3 nilai, width 100 → X 100, 100−100/59, …), `Download_scale_has_a_floor` (semua 0 → 102400), `Disk_text` (113702154240 → `106 GB`; 9019431321 → `8.4 GB`), dan di DemoTests `Demo_opens_the_system_page`.
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~StatsRules|FullyQualifiedName~StatText|FullyQualifiedName~Demo"` → FAIL.
- [ ] **Step 3:** Implementasi. `GpuEngines`: kunci = bagian setelah `pid_<n>_` (instance tanpa awalan itu dipakai utuh).
- [ ] **Step 4:** Tes → PASS.
- [ ] **Step 5: Commit** `feat(core): GPU engine sums, stat demand, 60 s history and disk text (F-RIGHT-03, F-ISL-07)`.

### Task 4: Sampler GPU dan disk, StatsService sesuai permintaan (Platform + App)

**Files:** Create `src/Gili.Platform/Stats/GpuSampler.cs`, `src/Gili.Platform/Stats/DiskSampler.cs`; Modify `src/Gili.App/Bar/StatsService.cs`, `src/Gili.App/App.xaml.cs`.

**Interfaces:**
- Consumes: `GpuReading`, `DiskReading`, `StatsDemand`, `MetricHistory`, `GpuEngines` (Task 3).
- Produces: `GpuSampler : IDisposable { GpuReading Sample() }` (PDH `PdhOpenQueryW`, `PdhAddEnglishCounterW(@"\GPU Engine(*)\Utilization Percentage")`, `PdhCollectQueryData`, `PdhGetFormattedCounterArrayW` dengan `PDH_FMT_DOUBLE | PDH_FMT_NOCAP100`; bacaan pertama setelah dibuat = 0; gagal → lempar dan buang query); `DiskSampler { DiskReading Sample() }` (drive dari `Path.GetPathRoot(Environment.SystemDirectory)`).
- `StatsSnapshot` mendapat `ModuleState<GpuReading> Gpu`, `ModuleState<DiskReading> Disk`, dan `StatsHistory History` (`record StatsHistory(IReadOnlyList<double> Cpu, Ram, Gpu, Down)`, salinan). `StatsService.Demand { get; set; }` (thread-safe; berubah → sampler yang berhenti dibuang lewat factory, riwayatnya dikosongkan). Konstruktor menerima factory `Func<Func<T>>` per metrik, bukan delegate langsung, supaya sampler bisa dibuat ulang; disk hanya tiap tick ke-10.

- [ ] **Step 1:** Implementasi sampler dan `StatsService`; `App.xaml.cs` membuat factory dan menyetel `Demand = StatsDemands.For(settings.HiddenModules, systemOpen: false)` saat start dan saat settings berubah.
- [ ] **Step 2:** `dotnet build Gili.slnx` → 0 warning, 0 error.
- [ ] **Step 3:** Uji cepat di scratchpad (console yang mereferensi `Gili.Platform`): `GpuSampler.Sample()` 5 kali per detik memberi angka 0–100 dan sama dengan `Get-Counter` ±5 poin; `DiskSampler.Sample()` = `DriveInfo`.
- [ ] **Step 4: Commit** `feat(platform): GPU and disk samplers, and stats read only what is shown (F-RIGHT-03, PRD 8.1)`.

### Task 5: Bar kanan mengikuti Settings (App)

**Files:** Create `src/Gili.App/Controls/SlotAnimation.cs`; Modify `src/Gili.App/Bar/BarWindow.xaml`, `BarWindow.xaml.cs`, `BarViewModel.cs`, `LeftSide.xaml.cs`, `RightService.cs`.

**Interfaces:**
- Consumes: `RightModules`, `RightModuleRules.Of`, `AppSettings.ModuleOrder` (Task 2); `StatsSnapshot.Gpu/Disk`, `StatText.Disk` (Tasks 3–4).
- Produces: `SlotAnimation` dengan `void Show(FrameworkElement element, bool show, bool animate)` dan `bool IsLeaving(FrameworkElement)` (isi `LeftSide.ShowIcon` dipindah ke sini; `LeftSide` memakainya). `RightService.SetHidden(RightModules hidden)`: Weather mati → timer cuaca berhenti; nyala lagi → ambil langsung bila pembacaan terakhir > 30 menit atau belum ada; Wi‑Fi mati → `_wifiTimer` berhenti (tetap berhenti saat `SetPaused(false)`).
- `BarViewModel`: `GpuText`, `GpuTooltip`, `DiskLabel` (`C: FREE`), `DiskText`, `DiskTooltip` (`Free space on C:` atau alasan gagal), `DiskFreeText` (`106 GB free`).

- [ ] **Step 1:** XAML: modul GPU (`Tag="gpu"`, dua baris, klik `OnSystem`) dan Disk (`Tag="disk"`) setelah RAM; `Tag` untuk Wi‑Fi `wifi`, Bluetooth `bt`, Volume `vol`, Brightness `bri`, Tray `tray`, Jam `clock`.
- [ ] **Step 2:** `BarWindow`: saat settings berubah, susun ulang enam modul sistem mengikuti `ModuleOrder` (pindah anak `RightModules`, "…" tetap setelah modul sistem); `FitRight` melewatkan modul yang mati di Settings dan yang tanpa data; saklar yang berubah memakai `SlotAnimation.Show(..., animate: true)`; overflow tetap instan. Daftar "…" menambah `GPU · {GpuText}` dan `Disk · {DiskFreeText}`.
- [ ] **Step 3:** `dotnet build Gili.slnx` → bersih; tes Core tetap lulus.
- [ ] **Step 4:** Jalankan app (`dotnet run --project src/Gili.App`, tutup instance lama dengan `--quit`), ubah `settings.json` lewat Settings (UIA Toggle, tanpa input sintetis): GPU nyala → modul tampil dengan angka; CPU mati → modul hilang; urutan dibalik → bar mengikuti. Cek lewat UIA (`AutomationElement` nama/BoundingRectangle) dan screenshot.
- [ ] **Step 5: Commit** `feat(bar): right modules follow their switches and order, with GPU and disk (F-RIGHT-03, F-RIGHT-04, F-BAR-12)`.

### Task 6: Urutan di Settings (App)

**Files:** Modify `src/Gili.App/Settings/SettingsWindow.xaml`, `SettingsWindow.xaml.cs`, `SettingsViewModel.cs`, `SettingRowViewModel.cs`; Modify `src/Gili.App/Resources/Icons.xaml` bila `Icon.Grip` belum ada.

**Interfaces:**
- Consumes: `SettingsCatalog.RowsOf`, `SettingsCatalog.ModuleId`, `RightModuleRules.Move/MoveTo` (Task 2).
- Produces: `SettingRowViewModel.CanReorder` (bool, true bila `ModuleId` ada), `SettingsViewModel.Move(SettingRowViewModel row, int delta)`, `SettingsViewModel.MoveTo(SettingRowViewModel row, int index)`; `SettingsViewModel` membangun ulang baris saat `ModuleOrder` berubah.

- [ ] **Step 1:** Template baris: pegangan `Icon.Grip` 14 px `#8E8E93` di kiri label bila `CanReorder`, kursor `SizeNS`. Seret: tekan pegangan, gerak vertikal; baris diangkat (latar `#14FFFFFF`) dan baris lain bergeser; lepas → `MoveTo` sekali (keputusan 4). Alt + ↑/↓ pada saklar baris → `Move` ±1, fokus tetap di saklar baris itu. `AutomationProperties.HelpText` saklar: `Alt+Up or Alt+Down to reorder`.
- [ ] **Step 2:** `dotnet build Gili.slnx` → bersih.
- [ ] **Step 3:** Jalankan, buka Settings → Right side; via UIA: fokus saklar `GPU` lalu kirim Alt+Up hanya lewat `UIA`… — karena tombol keyboard termasuk input sintetis, pengecekan Alt + panah dan seret diserahkan ke pengguna; yang dicek sendiri: baris tampil berurutan sesuai `settings.json` yang diubah langsung (`ModuleOrder`), dan pegangan hanya di enam baris.
- [ ] **Step 4: Commit** `feat(settings): reorder the system modules by dragging or Alt+arrows (F-RIGHT-04)`.

### Task 7: Halaman System (App)

**Files:** Create `src/Gili.App/Island/SystemPage.xaml`, `SystemPage.xaml.cs`, `SystemViewModel.cs`; Modify `src/Gili.App/Island/IslandWindow.xaml.cs`, `IslandViewModel.cs`, `src/Gili.App/App.xaml.cs`, `src/Gili.Platform/Shell/SystemActions.cs`.

**Interfaces:**
- Consumes: `StatsSnapshot` dengan `History` (Task 4), `Sparkline.Points/DownloadMax`, `StatText.*` (Task 3), `StatsDemands.For(hidden, systemPageOpen)`.
- Produces: `SystemViewModel : ObservableObject` dengan `CpuText`, `RamText`, `GpuText`, `DownText`, tooltip error per kotak, `PointCollection CpuPoints/RamPoints/GpuPoints/DownPoints` (lebar grafik diberikan halaman lewat `SetChartWidth(double)`), `DiskTitle` (`Disk C:`), `DiskText` (`106 GB free`), `DiskUsed` (0–1); `void Apply(StatsSnapshot s)` hanya dipanggil saat halaman terlihat; `IRelayCommand OpenTaskManagerCommand`. `SystemActions.TaskManager()` menjalankan `taskmgr.exe` (`UseShellExecute = true`).
- Tab System tidak lagi `Phase3` di `IslandViewModel.Tabs`.

- [ ] **Step 1:** `SystemPage.xaml` sesuai Global Constraints, grid 3 kolom (kotak Disk `Grid.ColumnSpan=2`), margin halaman sama dengan halaman lain (32 px kiri-kanan seperti `AlarmsPage`); seluruh halaman `Button` tanpa gaya dengan `AutomationProperties.Name="Open Task Manager"`, `Cursor="Hand"`; klik menutup pulau lalu `SystemActions.TaskManager()`.
- [ ] **Step 2:** `IslandWindow.CreatePage` → `new SystemPage(...)`; `App` menyetel `StatsService.Demand` ulang saat `IslandController.ViewChanged` (System terlihat = `View.Mode == Expanded && View.Page == IslandPage.System`) dan meneruskan snapshot ke `SystemViewModel.Apply` hanya saat terlihat.
- [ ] **Step 3:** `dotnet build Gili.slnx` → bersih; tes lulus.
- [ ] **Step 4:** Jalankan, buka halaman System lewat demo mode atau UIA Invoke pada modul CPU (bila tidak ada peer, lewat demo mode: menu bar via UIA ExpandCollapse/Invoke); screenshot: lima kotak sesuai mockup, grafik bergerak tiap detik, nilai cocok `Get-Counter` ±5 poin.
- [ ] **Step 5: Commit** `feat(island): System page with 60-second charts and free disk space (F-ISL-07)`.

### Task 8: Verifikasi dan temuan

**Files:** Modify `docs/superpowers/specs/2026-10-31-fase-3c-modul-kanan-design.md` (bagian "Temuan saat implementasi" dan "Hasil verifikasi").

- [ ] **Step 1:** `dotnet build Gili.slnx -c Release` 0 warning; `dotnet test tests/Gili.Core.Tests` semua lulus; `dotnet format Gili.slnx --verify-no-changes` bersih.
- [ ] **Step 2:** Ukur CPU proses Gili 60 detik diam (GPU/Disk mati), 60 detik dengan GPU/Disk nyala, 30 detik dengan halaman System terbuka; RAM sebelum/sesudah. Bandingkan dengan anggaran desain.
- [ ] **Step 3:** `pwsh scripts/publish.ps1` dan `pwsh scripts/package.ps1 -Install`: GPU counter terbaca di kedua bentuk (PDH tidak butuh capability, tetapi dicek).
- [ ] **Step 4:** Tulis temuan dan hasil ke desain; **Commit** `docs: phase 3c findings and verification`.
