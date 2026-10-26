# Fase 1d: Bar Kiri Persis Mockup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bagian kiri bar tampil dan bergerak persis seperti mockup: tombol Start, lima ikon launcher, ikon Shelf dan Claude, pemisah, lalu ikon asli aplikasi yang sedang berjalan dengan nama aplikasi aktif, nama yang melebar saat hover, dan `+N` saat ruang habis.

**Architecture:** Aturan urutan aplikasi, pengelompokan, dan muat-tidaknya deretan (`fitApps`/`fitBar` mockup) di `Gili.Core` dan dites. `Gili.Platform` membaca jendela taskbar (`EnumWindows` + `SetWinEventHook`), AppUserModelID, nama, dan ikon aplikasi, membawa jendela ke depan, dan membuka Start. `Gili.App` menggambar kontrol `LeftSide` di `BarWindow`.

**Tech Stack:** .NET 10, C# WPF, Win32 (`EnumWindows`, `SetWinEventHook`, `DwmGetWindowAttribute`, `SHGetPropertyStoreForWindow`, `SHCreateItemFromParsingName` + `IShellItemImageFactory`, `SendInput`), xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.1 (aturan jarak), 6.2, 9.2 baris "Aplikasi berjalan dan aktif" dan "Start", 14 (keputusan 2026-10-27); mockup CSS baris 64–100 dan 131–136, JS baris 1556–1589 dan 2995–3041, 3206–3207. Semua yang dibangun harus persis mockup.

## Global Constraints

- `.left` celah 12 px antar-grup; padding bar 10 px; teks 12 px.
- Start: tombol 28 × 24 radius 6, logo 15 px empat kotak `#4CC2FF` (r 1, 8,5 px); hover putih 8%, ditekan putih 14%; tooltip `Start`.
- Launcher (Timer, Alarms, Todo, Notes, Clipboard, Shelf, Claude): tombol 26 × 24 radius 6, celah 2, ikon 16 px `#AEAEB2`; hover putih 8% dan ikon `#F5F5F7`; ditekan putih 14%; tooltip = nama (`Timer`, `Alarms`, `Todo`, `Notes`, `Clipboard`, `Shelf`, `Claude sessions`).
- Pemisah 1 × 14 putih 22%.
- Aplikasi: tombol tinggi 24 radius 6, padding 0 5, opacity 0,7, ikon 16 px. Aktif: opacity 1, padding kiri 6 kanan 9, celah 7, latar putih 10%, nama tampil. Hover: opacity 1, latar putih 8%, nama tampil dengan padding/celah aktif. Nama: lebar 0 ↔ lebar nama (maks. 160) dalam 300 ms kurva out, fade 160 ms (aktif: 220 ms mulai 90 ms). Aplikasi baru: skala 0,6 → 1 dan opacity 0 → 1 dalam 320 ms kurva pop `(.34,1.3,.64,1)`.
- `+N`: kapsul tinggi 22 radius 11, padding 0 8, 12 px semibold, latar putih 8% (hover/terbuka 16%); tooltip berisi nama-nama aplikasinya; klik membuka daftar (latar `#1C1C1E` radius 12 padding 4, lebar min. 210, baris padding 7 × 10 radius 8 dengan ikon + nama, hover `#3A3A3C`).
- Aturan jarak (PRD 6.1): kiri berhenti 24 px sebelum pulau (lebar hover, lingkaran Gili, titik privasi), sesuai `fitBar()`.
- Hover hanya berganti bila kursor benar-benar bergerak.
- Tanpa hook global dan tanpa admin; `SetWinEventHook` out-of-context di thread UI.

## Keputusan yang dibuat di rencana ini

1. **Ikon Claude dan tab Claude tampil sejak Fase 1** (mockup menampilkan keduanya; pengguna: semua mirip). Ikon tanpa titik status sampai Fase 3; tab berisi `Coming in Phase 3`; menu jadi 10 tab sehingga pulau terbuka 576 px seperti mockup. PRD 6.2/6.3 dan catatan 1b disamakan di Task 1.
2. **Nama aplikasi** dari `FileDescription` file exe (Chrome menjadi `Google Chrome`), atau nama tampilan paket untuk aplikasi Store; cadangan nama file tanpa `.exe`.
3. **Shelf** hanya ikon tanpa angka sampai Fase 3; klik membuka tab Shelf (placeholder).

## Review Focus

1. **Jendela yang tidak tampil di taskbar** (tool window, jendela ber-owner, jendela cloaked di virtual desktop lain, jendela Gili sendiri) tidak boleh muncul. Dites di Task 2 (`TaskbarFilter`).
2. **Aplikasi dengan banyak jendela** (dua jendela Chrome) tampil satu ikon; menutup satu jendela tidak menghapus ikonnya. Dites di Task 2 (`Two_windows_of_one_app_are_one_icon`, `Closing_one_of_two_windows_keeps_the_app`).
3. **Banyak aplikasi di layar sempit**: aplikasi aktif tidak pernah masuk `+N`. Dites di Task 2 (`Active_app_never_goes_into_more`).
4. **Ikon gagal diambil** (proses terlindungi, exe hilang): ikon jendela generik, tanpa exception. Ditangani Task 3, diperiksa manual.
5. **Aplikasi Store** (Settings, Calculator) dalam `ApplicationFrameHost`: dikenali dari AppUserModelID, bukan `ApplicationFrameHost.exe`. Diperiksa manual di Task 5.

---

### Task 1: Dokumen: ikon dan tab Claude sejak Fase 1

**Files:** Modify `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md`

- [ ] **Step 1:** Di PRD 6.3 ganti kalimat `tab Claude baru muncul di Fase 3 bersama integrasinya.` menjadi `sampai Fase 3 ikon dan tab Claude tampil sebagai placeholder (keputusan pengguna 2026-10-27: tampilan seperti mockup); mulai Fase 3 keduanya mengikuti saklar integrasi Claude.` Di tabel 6.2 baris Claude tambahkan kalimat yang sama.
- [ ] **Step 2: Commit** `docs: Claude icon and tab show as placeholders until Phase 3`.

---

### Task 2: Aturan deretan aplikasi (Core)

**Files:**
- Create: `src/Gili.Core/Apps/AppStrip.cs`
- Test: `tests/Gili.Core.Tests/Apps/AppStripTests.cs`

**Interfaces:**
- Produces: `public sealed record TaskbarWindow(nint Handle, string AppKey, bool Visible, bool Tool, bool HasOwner, bool AppWindow, bool Cloaked, bool Own)` dan `static class TaskbarFilter { bool Shows(TaskbarWindow w); }` (tampil: Visible, bukan Cloaked, bukan Own, bukan Tool, dan tanpa owner atau AppWindow).
- Produces: `public sealed class AppStrip` dengan `IReadOnlyList<string> Apps` (kunci aplikasi, urutan pertama dibuka), `string? Active`, `bool Update(IEnumerable<TaskbarWindow> windows)` (true bila daftar berubah; aplikasi baru di ujung kanan; aplikasi tanpa jendela hilang), `bool SetActive(string? appKey)` (hanya aplikasi yang ada di daftar).
- Produces: `public readonly record struct StripFit(int Hidden, double ActiveNameMax, double HoverNameMax, bool Tight)` dan `AppStrip.Fit(double available, int count, int activeIndex, double activeNameWidth) -> StripFit`, dengan konstanta mockup ICON 28, ACT 36, MORE 38, sisa minimal 60, nama maks. 160, kapsul nama 12, hover < 40 = Tight.
- Produces: `static class BarSpacing { double LeftLimit(double screenWidth, double compactWidth, double islandHeight, bool giliCircle, int privacyDots); double RightLimit(double screenWidth, double compactWidth, double islandHeight, int bubbles); }` = lebar layar / 2 − max(lebar compact / 2 + 8 + samping, 150) − 24 − 10, samping kiri = titik × 13 + (lingkaran ? tinggi pulau + 6 : 0), samping kanan = gelembung × (tinggi pulau + 6).

- [ ] **Step 1: Tulis test yang gagal**

```csharp
[Fact] public void Tool_owned_cloaked_and_own_windows_are_hidden() { ... Assert.False(TaskbarFilter.Shows(W(tool: true))); Assert.False(TaskbarFilter.Shows(W(owner: true))); Assert.True(TaskbarFilter.Shows(W(owner: true, appWindow: true))); Assert.False(TaskbarFilter.Shows(W(cloaked: true))); Assert.False(TaskbarFilter.Shows(W(own: true))); Assert.False(TaskbarFilter.Shows(W(visible: false))); }
[Fact] public void Apps_keep_the_order_they_were_first_opened() { strip.Update([W(1,"chrome"), W(2,"code")]); strip.Update([W(3,"spotify"), W(1,"chrome"), W(2,"code")]); Assert.Equal(["chrome","code","spotify"], strip.Apps); }
[Fact] public void Two_windows_of_one_app_are_one_icon() { strip.Update([W(1,"chrome"), W(2,"chrome")]); Assert.Equal(["chrome"], strip.Apps); }
[Fact] public void Closing_one_of_two_windows_keeps_the_app() { strip.Update([W(1,"chrome"), W(2,"chrome")]); Assert.False(strip.Update([W(2,"chrome")])); Assert.Equal(["chrome"], strip.Apps); }
[Fact] public void Closing_the_last_window_removes_the_app() { strip.Update([W(1,"chrome"), W(2,"code")]); strip.Update([W(2,"code")]); Assert.Equal(["code"], strip.Apps); }
[Fact] public void Only_listed_apps_become_active() { strip.Update([W(1,"chrome")]); Assert.False(strip.SetActive("explorer-desktop")); Assert.True(strip.SetActive("chrome")); Assert.Equal("chrome", strip.Active); }
[Fact] public void Everything_fits_on_a_wide_bar() => Assert.Equal(new StripFit(0, 160, 160, false), AppStrip.Fit(600, 5, 0, 50));
[Fact] public void Inactive_icons_drop_into_more_from_the_right() => Assert.Equal(2, AppStrip.Fit(200, 6, 0, 50).Hidden);
[Fact] public void Active_app_never_goes_into_more() => Assert.Equal(4, AppStrip.Fit(60, 5, 4, 50).Hidden);
[Fact] public void Little_room_left_makes_hover_names_tooltips() => Assert.True(AppStrip.Fit(200, 4, 0, 60).Tight);
[Fact] public void Left_limit_keeps_24_from_the_hovered_island() => Assert.Equal(768 - 108 - 24 - 10, BarSpacing.LeftLimit(1536, 200, 24, giliCircle: false, privacyDots: 0));
[Fact] public void Gili_circle_pushes_the_left_side_away() => Assert.Equal(768 - (108 + 30) - 24 - 10, BarSpacing.LeftLimit(1536, 200, 24, giliCircle: true, privacyDots: 0));
```

Hitung angka `Fit` dari rumus mockup (`need = (n − hid − 1) × 28 + 36 + (hid ? 38 : 0)`; sembunyikan dari kanan selama `avail − need < 60`, lewati yang aktif; `free = max(0, avail − need)`; `activeMax = min(160, free)`; `hover = min(160, free − min(160, free, activeNameWidth) − 12)`; `Tight = hover < 40`) dan tulis nilai yang tepat di test sebelum implementasi.

- [ ] **Step 2: Jalankan test, pastikan gagal** — `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~AppStripTests"` → build gagal.
- [ ] **Step 3: Implementasi** sesuai Interfaces.
- [ ] **Step 4: Jalankan test, pastikan lulus.**
- [ ] **Step 5: Commit** `feat(core): running app strip order, overflow and spacing rules (F-LEFT-01)`.

---

### Task 3: Platform: jendela taskbar, info aplikasi, ke depan, Start

**Files:**
- Create: `src/Gili.Platform/Apps/TaskbarWindows.cs`, `src/Gili.Platform/Apps/AppInfo.cs`, `src/Gili.Platform/Shell/StartMenu.cs`
- Modify: `src/Gili.Platform/Interop/Win32.cs`

**Interfaces:**
- Consumes: `TaskbarWindow`, `TaskbarFilter` (Task 2).
- Produces: `TaskbarWindows : IDisposable` — `IReadOnlyList<TaskbarWindow> Read()`; event `Changed` (show/hide/destroy/nama berubah, dikoalesi 100 ms oleh App); event `ForegroundChanged(nint hwnd)`; `string AppKeyOf(nint hwnd)`. Hook `EVENT_OBJECT_SHOW`, `EVENT_OBJECT_HIDE`, `EVENT_OBJECT_DESTROY`, `EVENT_OBJECT_CLOAKED/UNCLOAKED`, `EVENT_SYSTEM_FOREGROUND` dengan `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`, hanya `OBJID_WINDOW`.
- AppKey: AppUserModelID dari `SHGetPropertyStoreForWindow` (`PKEY_AppUserModel_ID`) bila ada; selain itu path exe dari `QueryFullProcessImageName` (lowercase). Jendela `ApplicationFrameWindow` memakai AUMID dari jendela anak `Windows.UI.Core.CoreWindow`.
- Produces: `AppInfo.Resolve(string appKey, nint sampleWindow, int pixelSize) -> (string Name, BitmapSource? Icon)` (App memakai WPF `BitmapSource`, jadi kelas ini di Platform boleh bergantung ke `PresentationCore`, seperti `MediaSessionMonitor`): ikon dari `IShellItemImageFactory.GetImage` pada path exe atau `shell:AppsFolder\<AUMID>`, cadangan `WM_GETICON`/`GCLP_HICONSM`; nama dari `FileVersionInfo.FileDescription`, nama tampilan item shell untuk AUMID, cadangan nama file.
- Produces: `TaskbarWindows.BringToFront(nint hwnd)` (restore bila minimized lalu `SetForegroundWindow`) dan `StartMenu.Toggle()` (`SendInput` LWin turun-naik).

- [ ] **Step 1: Implementasi** sesuai Interfaces; tandai jendela Gili sendiri (`Own`) dari PID proses ini.
- [ ] **Step 2: Build** — `dotnet build Gili.slnx` → 0 warning, 0 error.
- [ ] **Step 3: Commit** `feat(platform): taskbar windows, app names and icons, bring to front, Start (F-LEFT-01, F-LEFT-06)`.

---

### Task 4: Bagian kiri bar

**Files:**
- Create: `src/Gili.App/Bar/LeftSide.xaml(.cs)`, `src/Gili.App/Bar/AppsService.cs`
- Modify: `src/Gili.App/Bar/BarWindow.xaml(.cs)`, `src/Gili.App/App.xaml.cs`, `src/Gili.App/Resources/Icons.xaml` (+ `Icon.Claude` (isi), `Icon.Start` empat kotak), `src/Gili.App/Island/IslandViewModel.cs` (tab Claude, Task 1)

**Interfaces:**
- Consumes: `AppStrip`, `BarSpacing` (Task 2), `TaskbarWindows`, `AppInfo`, `StartMenu` (Task 3), `IslandController.Toggle` dan `IslandController.View`.
- Produces: `AppsService` (memegang `AppStrip`, cache `(Name, Icon)` per AppKey, event `Changed`); `LeftSide : UserControl` dengan event `PageRequested(IslandPage)`.

- [ ] **Step 1:** Tab Claude kembali ke menu pulau (`Coming in Phase 3`), jadi 10 tab.
- [ ] **Step 2:** `LeftSide` sesuai Global Constraints: Start, launcher (Timer, Alarms, Todo, Notes, Clipboard → halamannya; Shelf → Shelf; Claude → Claude) lewat `IslandController.Toggle`, pemisah, deretan aplikasi, `+N` dan daftarnya.
- [ ] **Step 3:** Deretan aplikasi: lebar tersedia = `BarSpacing.LeftLimit` − lebar Start dan launcher − 26 (mockup `fitApps`); `AppStrip.Fit` menentukan `+N`, lebar maks. nama aktif dan hover; nama melebar/menyusut dengan gerak Global Constraints; aplikasi baru muncul dengan pop; klik → `BringToFront` jendela teratas aplikasi itu.
- [ ] **Step 4:** Elemen kiri ikut aturan memudar saat tertimpa pulau mengambang (`IslandLayout.Covers`, satu per satu).
- [ ] **Step 5: Build, test, periksa**: `dotnet build Gili.slnx`, `dotnet test tests/Gili.Core.Tests`; jalankan app: ikon Chrome, VS Code, Spotify, terminal tampil asli dan tajam; aplikasi aktif bernama dalam kapsul; pindah aplikasi (Alt+Tab) memindahkan nama ≤ 200 ms; hover ikon melebarkan nama; ikon launcher membuka/menutup tabnya; Start membuka dan menutup Start menu.
- [ ] **Step 6: Commit** `feat(bar): Start, launcher, Shelf and Claude icons, and running apps on the left (F-LEFT-01, F-LEFT-02, F-LEFT-04, F-LEFT-05, F-LEFT-06)`.

---

### Task 5: Bandingkan dengan mockup dan verifikasi

- [ ] **Step 1:** `pwsh scripts/mockup-shot.ps1 -State idle -Out <scratchpad>\mock-idle.png`, potret bar app, gabungkan berdampingan 1920 × 40, catat dan perbaiki perbedaan di bagian kiri.
- [ ] **Step 2:** Aplikasi Store (Settings atau Calculator) dibuka: satu ikon dengan logonya dan nama `Settings`/`Calculator`, bukan `ApplicationFrameHost`.
- [ ] **Step 3:** `dotnet build Gili.slnx -c Release`, `dotnet test tests/Gili.Core.Tests`, `dotnet format Gili.slnx --verify-no-changes` → sukses; CPU diam tetap < 1%.
- [ ] **Step 4: Commit** perbaikan (bila ada) `fix(bar): left side matches the mockup`.
