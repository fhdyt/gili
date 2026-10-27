# Fase 1f: Jendela Settings Persis Mockup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jendela Settings tampil persis mockup (judul sendiri, navigasi bagian di kiri, baris berlatar). Saklar untuk fitur yang sudah ada di Fase 1 langsung berfungsi; bagian dan saklar fase berikutnya tampil nonaktif dengan `Coming in a later version`.

**Architecture:** Daftar bagian dan baris, beserta pemetaan baris ke `AppSettings`, ada di `Gili.Core` sebagai data yang dites (`SettingsCatalog`). `Gili.App` menggambar daftar itu dengan satu DataTemplate per jenis baris (saklar, segmen, slider) di jendela berbingkai sendiri (`WindowChrome`, sudut bulat DWM).

**Tech Stack:** .NET 10 WPF, CommunityToolkit.Mvvm, `WindowChrome`, `DwmSetWindowAttribute`, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.5, F-BAR-08 (bagian Fase 1: jam, material, tinggi bar dan pulau, autostart), F-BAR-13; mockup CSS baris 761–781 (`.setw` … `.srow`), 448–450 (`.seg`), 466–469 (`.sw`); JS baris 2908–2979 (`SET`, `renderSettings`).

## Global Constraints

- Semua teks persis bahasa Inggris dari mockup/PRD. Judul jendela `Gili Settings`; tombol tutup `Close Settings`.
- Jendela 640 × 460, latar `#202020`, garis tepi putih 8%, radius 8 (sudut bulat DWM), bayangan DWM. Bilah judul 36 px `#2B2B2B`: ikon roda gigi 14 px, `Gili Settings` 12 px `#C7C7CC`, tombol tutup 46 × 36 dengan hover `#C42B1C` dan ikon putih.
- Navigasi 150 px, padding 8, celah 2, garis kanan putih 6%; tombol padding 7 10, radius 6, warna `#8E8E93`; hover putih 6% dan teks `#F5F5F7`; terpilih putih 9%, teks `#F5F5F7` semibold.
- Halaman: padding 16 20, scroll vertikal; judul 18 px semibold, jarak bawah 12. Baris: tinggi min. 46, padding 6 12, radius 8, latar putih 4%, jarak bawah 4, celah 12; keterangan 11,5 px `#8E8E93` 2 px di bawah label. Baris nonaktif: opacity 0,45, tidak bisa diklik atau difokus.
- Saklar 38 × 22 radius 11, mati `#48484A`, nyala `#30D158`; kenop 18 px putih berbayang, geser 16 px dengan pegas `Pop`. Segmen: latar `#1C1C1E` radius 9 padding 2; tombol padding 4 10 radius 7 12 px `#8E8E93`; terpilih `#3A3A3C` teks `#F5F5F7`. Slider lebar 150 warna `#30D158`; angka 38 px rata kanan, digit tabular (`46%`, `32 px`).
- Huruf dasar 13 px Segoe UI Variable. Semua perubahan berlaku langsung tanpa tombol Save. Esc menutup jendela.

## Keputusan yang dibuat di rencana ini

1. **Urutan bagian**: General, Appearance, Left side, Island, Right side, Notifications, Claude, Sounds, Shortcuts, About. Tujuh pertama urutan mockup; Sounds, Shortcuts, dan About ada di PRD 6.5 tapi belum ada di mockup, jadi ditambahkan ke mockup di akhir. Tabel PRD 6.5 diurutkan sama.
2. **Yang berfungsi di Fase 1** (F-BAR-08 bagian Fase 1, plus saklar 1c): `12-hour clock`, `Show seconds`, `Start with Windows`, `Bar opacity`, `Glass`, `Bar height`, `Island height`, `Ambient glow`, `Ambient motion`, `Show Gili`. Selain itu nonaktif dengan keterangan `Coming in a later version` dan menampilkan nilai default PRD.
3. **CPU temperature dihapus dari mockup** (F-RIGHT-06 sudah dihapus). Mockup juga mendapat baris PRD yang belum ada: Claude `Show Claude Code sessions`; Right side `Weather city` dan `Battery alert`.
4. **Notifications** hanya dua baris umum (`Show Windows notifications`, `Show message text`). Baris per aplikasi di mockup adalah contoh data dan baru muncul setelah ada aplikasi yang mengirim notifikasi (Fase 4).
5. **Keterangan**: baris nonaktif menampilkan alasannya, bukan keterangan biasanya (mockup `dis?why:help`). `Start with Windows` di build yang tidak terpasang memakai keterangan mockup `Only the installed copy starts with Windows` (menggantikan teks lama). Saat Transparency effects Windows mati, `Glass` memakai keterangan `Transparency effects are off in Windows, so the bar is drawn solid.`

## Review Focus

1. **Tinggi bar diturunkan saat Settings terbuka**: batas atas slider `Island height` ikut turun dan nilainya terjepit (bar − 4) tanpa melompat. Dites di Task 1 (`Island_height_range_ends_at_bar_height_minus_4`) dan diperiksa manual di Task 2.
2. **Menggeser slider opacity terus-menerus**: bar berubah langsung; `settings.json` tetap ditulis sekali 300 ms setelah berhenti (SettingsHub). Diperiksa manual.
3. **Membuka Settings saat sudah terbuka** (roda gigi, menu klik kanan, menjalankan `Gili.exe` lagi): jendela yang sama maju ke depan, bagian terpilih tetap (F-BAR-13). Diperiksa manual.
4. **Keyboard saja**: Tab berpindah antar kontrol yang aktif, Space/Enter mengubah saklar dan segmen, panah atas/bawah di navigasi berpindah bagian, Esc menutup; baris nonaktif dilewati. Diperiksa manual.
5. **Layar 1366 × 768 di DPI 150%** (area kerja ±480 DIP): jendela tidak melebihi area kerja; tinggi = min(460, area kerja − 32) dan halaman di-scroll. Diperiksa manual lewat batas di Task 2 Step 3.

---

### Task 1: Katalog Settings (Core) dan penyelarasan mockup/PRD

**Files:**
- Create: `src/Gili.Core/Settings/SettingsCatalog.cs`
- Modify: `docs/mockup/top-taskbar-mockup.html` (objek `SET` baris 2909–2917), `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md` (tabel 6.5)
- Test: `tests/Gili.Core.Tests/Settings/SettingsCatalogTests.cs`

**Interfaces:**
- Produces:
  - `public enum SettingKind { Switch, Segment, Range }`
  - `public sealed record SettingRow(string Key, string Label, SettingKind Kind, string? Help = null, bool Available = false, IReadOnlyList<string>? Options = null, string Unit = "", object? Default = null)` — `Options` untuk Segment; `Default` adalah nilai tampilan baris nonaktif (`bool`, `string` opsi, atau `int`).
  - `public sealed record SettingsSection(string Name, IReadOnlyList<SettingRow> Rows)`
  - `public static class SettingsCatalog` dengan `const string LaterVersion = "Coming in a later version"`, `IReadOnlyList<SettingsSection> Sections`, `object Read(AppSettings s, string key)`, `AppSettings Write(AppSettings s, string key, object value)` (hasilnya sudah `Normalize()`), `(int Min, int Max) Range(AppSettings s, string key)`.
  - Kunci baris aktif: `h12`, `secs`, `auto`, `opacity`, `glass`, `barH`, `islH`, `glow`, `motion`, `gili`. Opsi `glass`: `Off`, `Light`, `Strong` (↔ `GlassLevel`).

- [ ] **Step 1: Tes yang gagal** di `SettingsCatalogTests`:
  - `Sections_follow_the_mockup_then_the_prd`: nama bagian = `["General","Appearance","Left side","Island","Right side","Notifications","Claude","Sounds","Shortcuts","About"]`.
  - `Phase_1_rows_are_available`: himpunan `Key` dengan `Available` = sepuluh kunci di Interfaces.
  - `Later_rows_say_coming_in_a_later_version`: setiap baris `!Available` punya `Default` bukan null; `SettingsCatalog.LaterVersion == "Coming in a later version"`.
  - `Cpu_temperature_is_gone`: tidak ada baris berlabel `CPU temperature`.
  - `Right_side_rows_match_the_prd`: label Right side = `["Weather","Network","CPU","RAM","GPU","Disk","Wi‑Fi","Bluetooth","Volume","Brightness","Tray icons","Clock and date","Weather city","Battery alert"]`; `GPU` dan `Disk` default `false`.
  - `Left_side_help_names_the_page`: baris `Timer` punya `Help == "Turning it off also hides the Timer page in the island"`; `Start` tanpa Help.
  - `Twelve_hour_clock_is_the_inverse_of_24_hour`: `Read(new AppSettings(), "h12")` = `false`; `Write(s, "h12", true).Use24HourClock` = `false`.
  - `Glass_reads_and_writes_by_name`: `Read(.., "glass")` = `"Strong"`; `Write(s, "glass", "Light").Glass` = `GlassLevel.Light`.
  - `Island_height_range_ends_at_bar_height_minus_4`: `Range(new AppSettings { BarHeight = 30 }, "islH")` = `(20, 26)`; `Write(new AppSettings { IslandHeight = 26, BarHeight = 32 }, "barH", 28).IslandHeight` = `24`.
  - `Ranges_match_the_prd`: `opacity` (40, 100) unit `%`; `barH` (28, 44) unit ` px`.
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~SettingsCatalogTests"` → gagal kompilasi (tipe belum ada).
- [ ] **Step 3: Implementasi** `SettingsCatalog`. Label, Help, dan Default diambil persis dari `SET` mockup dan PRD 6.5/6.6: General (`Bar position` seg Top/Bottom default `Top`, `Hide Windows taskbar` dengan help mockup, `12-hour clock`, `Show seconds` help `The clock on the bar ticks every second`, `Start with Windows`); Appearance (`Bar opacity`, `Glass`, `Bar height`, `Island height` help `From 20 px up to the bar height minus 4 px`, `Ambient glow`, `Ambient motion` help `The glow drifts slowly while the island is open`); Left side (Start, Timer, Alarms, Todo, Notes, Clipboard, Shelf, Claude; default nyala); Island (`Show Gili` help `Eyes in the idle island that follow the pointer`, `Show lyrics` default mati dengan help mockup, lalu `<Tab> page` untuk sepuluh tab, default nyala); Notifications (`Show Windows notifications` default mati, `Show message text` default nyala, help mockup); Claude (`Show Claude Code sessions` default nyala, `Show 5-hour usage timer` default nyala dengan help mockup); Sounds (`Timer sound` seg Radar/Chime/Bell/Soft/Ping default `Chime`, `Alert volume` range 0–100 `%` default 80, `Gradually increase` default nyala); Shortcuts (`Keyboard shortcuts` default nyala); About (`Updates` default nyala, `Open logs folder` default nyala). `Weather city` seg dengan satu opsi `Jakarta`; `Battery alert` range 5–50 `%` default 20.
- [ ] **Step 4:** tes yang sama → lulus; `dotnet test tests/Gili.Core.Tests` → semua lulus.
- [ ] **Step 5:** mockup `SET`: hapus `['temp','CPU temperature']`; tambah `['W.city','Weather city']` dan `['W.bat','Battery alert']` di Right side, `['clOn','Show Claude Code sessions','sw']` sebelum `limOn`, serta bagian `Sounds`, `Shortcuts`, `About` dengan baris seperti Step 3 (saklar biasa di mockup). PRD 6.5: urutkan tabel seperti Keputusan 1, Right side menyebut `Tray icons` dan `Clock and date`. Buka mockup di browser, klik roda gigi, dan pastikan setiap bagian tampil tanpa error konsol.
- [ ] **Step 6: Commit** `feat(core): Settings sections and rows as data, Phase 1 rows mapped to settings (F-BAR-08)` (Core + tes), lalu `docs: Settings sections in the mockup match the PRD; no CPU temperature` (mockup + PRD).

### Task 2: Jendela Settings persis mockup

**Files:**
- Modify: `src/Gili.App/Settings/SettingsWindow.xaml(.cs)` (tulis ulang), `src/Gili.App/Settings/SettingsViewModel.cs` (tulis ulang), `src/Gili.Platform/Shell/WindowStyles.cs` (+ sudut bulat), `src/Gili.App/App.xaml.cs` (`ShowSettings`), `scripts/mockup-shot.ps1` (+ state `settings` yang mengklik `#mSet`)
- Create: `src/Gili.App/Settings/SettingRowViewModel.cs`

**Interfaces:**
- Consumes: `SettingsCatalog.Sections/Read/Write/Range`, `SettingsCatalog.LaterVersion` (Task 1); `SettingsHub.Current/Update/Changed`.
- Produces:
  - `WindowStyles.RoundCorners(IntPtr hwnd)` — `DwmSetWindowAttribute(hwnd, 33 /*DWMWA_WINDOW_CORNER_PREFERENCE*/, 2 /*DWMWCP_ROUND*/)`; HRESULT gagal diabaikan.
  - `SettingsViewModel(SettingsHub hub, bool transparencyEnabled, bool installedBuild)` dengan `IReadOnlyList<SettingsSection> Sections`, `[ObservableProperty] SettingsSection SelectedSection` (awal `General`), `IReadOnlyList<SettingRowViewModel> Rows` (baris bagian terpilih).
  - `SettingRowViewModel` (ObservableObject): `Label`, `Kind`, `Note` (Help, alasan nonaktif, atau keterangan Keputusan 5), `IsEnabled`, `bool IsOn`, `string Choice`, `double Value`, `double Min`, `double Max`, `string ValueText` (`46%`, `32 px`), `IReadOnlyList<string> Options`. Mengubah `IsOn/Choice/Value` memanggil `hub.Update(s => SettingsCatalog.Write(s, key, v))`; semua baris memuat ulang dari `hub.Current` saat `hub.Changed` (nilai terjepit dan batas `Island height` ikut).

- [ ] **Step 1:** jendela: `WindowStyle="None"`, `ResizeMode="NoResize"`, `WindowChrome` (`CaptionHeight="36"`, `ResizeBorderThickness="0"`, `GlassFrameThickness="0,0,0,1"` supaya DWM tetap memberi bayangan), `RoundCorners` di `SourceInitialized`, `ShowInTaskbar="True"`, `WindowStartupLocation="CenterScreen"`. Bilah judul, navigasi (`ListBox` gaya tab, panah atas/bawah berpindah bagian), dan halaman (`ScrollViewer` + `ItemsControl` dengan `DataTemplateSelector` per `SettingKind`) sesuai Global Constraints. Saklar = `ToggleButton` bergaya; segmen = `ItemsControl` berisi `RadioButton` bergaya; slider = `Slider` dengan `IsSnapToTickEnabled`. Tombol tutup memakai `WindowChrome.IsHitTestVisibleInChrome="True"`.
- [ ] **Step 2:** `SettingsViewModel` dan `SettingRowViewModel` sesuai Interfaces; hapus properti lama (`BarOpacity`, `GlassLevels`, dll.) yang digantikan baris.
- [ ] **Step 3:** ukuran: `Width=640`, `Height = Math.Min(460, SystemParameters.WorkArea.Height - 32)`. Esc menutup (`PreviewKeyDown`). `App.ShowSettings` tidak berubah selain memakai konstruktor baru; jendela yang sudah ada di-`Activate()` (dan `WindowState = Normal` bila diminimalkan).
- [ ] **Step 4:** `dotnet build Gili.slnx` 0 warning; `dotnet test tests/Gili.Core.Tests` lulus.
- [ ] **Step 5:** jalankan app, buka Settings dari roda gigi, potret jendela; `pwsh scripts/mockup-shot.ps1 -State settings -Out <scratchpad>\mock-settings.png`; bandingkan General, Appearance, dan satu bagian nonaktif berdampingan. Periksa Review Focus 1–4: turunkan Bar height ke 28 saat Island height 28 (pulau jadi 24, batas slider 24); geser opacity; klik roda gigi lagi dan jalankan `Gili.exe` lagi (jendela yang sama maju); Tab/Space/panah/Esc.
- [ ] **Step 6: Commit** `feat(app): Settings window as in the mockup, Phase 1 rows live and later ones marked (F-BAR-08, F-BAR-13)`.

### Task 3: Verifikasi

- [ ] **Step 1:** `dotnet build Gili.slnx -c Release` 0 warning; `dotnet test tests/Gili.Core.Tests -c Release` lulus; `dotnet format Gili.slnx --verify-no-changes` exit 0.
- [ ] **Step 2:** `pwsh scripts/publish.ps1`, buka Settings di salinan terpasang: `Start with Windows` tanpa keterangan, mematikannya menghapus entri Gili di `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` dan menyalakannya mengembalikannya (`reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run`). Tutup Settings, tunggu 30 detik, catat private working set dibanding sebelum Settings dibuka.
- [ ] **Step 3:** catat hasil di ledger; tidak ada commit kalau tidak ada perubahan.
