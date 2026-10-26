# Fase 1c: Pulau Persis Mockup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pulau dan jam di bar tampil dan bergerak persis seperti mockup: maskot Gili (mata di pulau, lingkaran, di samping menu), artwork musik di slot, menu tab ikon dengan nama yang melebar, pulau terbuka 560 px tanpa baris jam dan setinggi isinya, ambient glow, serta jam dua baris dan tombol Settings di kanan bar.

**Architecture:** Semua aturan yang bisa dihitung (ukuran menu dan tab, geometri pulau, arah pandang dan ekspresi Gili, jadwal kedip, warna artwork, kurva bezier, format jam) masuk `Gili.Core` dan dites unit. `Gili.Platform` hanya membaca kursor, tombol mouse, dan waktu input terakhir. `Gili.App` menggambar: kontrol `GiliEyes`, `GiliService` (pembacaan kursor 30×/detik, pegas mata, ekspresi, kedip), `AmbientGlow`, dan perubahan di `IslandWindow` dan `BarWindow`.

**Tech Stack:** .NET 10, C# WPF, CommunityToolkit.Mvvm, Win32 (`GetCursorPos`, `GetAsyncKeyState`, `GetLastInputInfo`), xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.3 (Keadaan, Isi compact, Maskot Gili, Ekspresi Gili, Halaman expanded, Ambient glow), 6.4 (Jam dan tanggal, Settings), 8.1, 8.3; acuan visual dan gerak: [mockup](../../mockup/top-taskbar-mockup.html) (CSS baris 247–431, JS baris 1291–1545 dan 1778–2010). Pengguna (2026-10-27): **semua yang dibangun harus persis mockup**; perbedaan PRD–mockup dilaporkan, bukan dipilih diam-diam.

## Global Constraints

- Teks UI hanya bahasa Inggris, persis dari PRD/mockup. `Gili.Core` tanpa WPF/WinRT/P/Invoke.
- Kurva "out" mockup `cubic-bezier(.16,1,.3,1)`; kurva ekspresi `cubic-bezier(.32,1.18,.5,1)`; glow `cubic-bezier(.37,0,.63,1)`.
- Pegas: buka 0,82/0,42; tutup 1,0/0,32; compact ↔ wide 0,85/0,30; hover 0,8/0,30; pindah bentuk Gili 0,8/0,45; mata 0,8/0,25 dtk.
- Pulau compact: tinggi = Island height, di tengah tinggi bar, radius = setengah tinggi, lebar = isi + 28 (min 200), hover +16. Pulau mengambang memakai **tepi atas yang sama dengan compact**: (tinggi bar − tinggi pulau) / 2.
- Pulau terbuka: lebar = max(560, kebutuhan menu), maks. lebar layar − 24; tinggi = 58 + tinggi isi halaman + 20, min 256, maks. tinggi layar − 2 × tepi atas; radius 32.
- Menu tab (nilai mockup): kapsul 36 px, 14 px dari tepi atas/kanan, mulai 56 px dari kiri (14 kalau Show Gili mati), radius 18, padding 3, celah 2, latar putih 10%; sorotan putih 14% radius 15; tab ikon 36 px (padding 10,5), tab bernama padding 12 + celah 5; teks 12 px `#8E8E93`, aktif putih semibold dengan ikon `#30D158`; ikon 15 px.
- Gerak menu: nama melebar 300 ms kurva out + fade 160 ms (tab aktif: fade 220 ms mulai 90 ms); sorotan x dan lebar 300 ms kurva out; halaman geser 300 ms kurva out.
- Isi pulau terbuka muncul mulai 120 ms: opacity 220 ms, geser −6 → 0 px dan skala 0,98 → 1 dalam 380 ms kurva out, blur 6 → 0 dalam 260 ms; menghilang 90 ms.
- Gili: lihat tabel bentuk di PRD 6.3; mata putih `#FFFFFF`; tidak ada hook mouse global; kursor dibaca 30×/detik hanya selama Gili tampil dan bar terlihat; `GetLastInputInfo` sekali per detik.
- Ambient glow: maks. 30 fps, hanya selama pulau terbuka; anggaran CPU ≤ 3% (PRD 8.1).
- Animation effects mati (`SystemParameters.ClientAreaAnimation == false`): pegas jadi instan/cross-fade 100 ms, mata diam di tengah, kedip jadi fade 200 ms, tanpa anggukan, glow diam.
- Pesan commit diakhiri `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. Sebelum build app: `Gili.exe --quit`. Jangan menjalankan `scripts/publish.ps1`.

## Keputusan yang dibuat di rencana ini

1. **Nilai mockup menang** atas PRD kalau berbeda (keputusan pengguna 2026-10-27), dan PRD disamakan di Task 1. Perbedaan yang sudah diketahui: latar menu tab putih 10% (PRD `#1C1C1E`), sorotan putih 14% (PRD `#3A3A3C`), latar lingkaran Gili di samping menu putih 10%.
2. **9 tab**: Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, System. Claude tetap tersembunyi sampai integrasinya ada (PRD: halaman hanya ada bila integrasi Claude nyala). Clipboard berisi `Coming in Phase 4`.
3. **Ambient glow untuk semua halaman** dengan warna per halaman dari mockup, walaupun F-ISL-21 untuk halaman selain Media tercatat di Fase 2: halaman placeholder pun harus tampak seperti mockup, dan biayanya sama.
4. **Warna artwork**: dua warna dihitung sekali per lagu dari thumbnail (Task 5). Warna utama dipakai titik sumber di halaman Media, bar level di slot, dan glow; warna kedua untuk gumpalan glow kedua. Tanpa artwork: warna aplikasi lama (`MediaRules.LevelColor`) untuk keduanya.
5. **Gumpalan glow** digambar dengan `RadialGradientBrush` (warna → transparan) tanpa `BlurEffect`, karena efek blur di jendela layered dihitung CPU tiap frame. Kalau hasilnya terlalu tajam dibanding mockup, tambah gradient stop di tengah, bukan blur.
6. **Settings baru tanpa UI dulu**: `ShowSeconds`, `ShowGili`, `AmbientGlow`, `AmbientMotion` (default nyala) masuk `AppSettings` sekarang; saklarnya di jendela Settings dibuat di paket 4 (Settings persis mockup). `BarOpacity` default menjadi 100 sesuai PRD 6.1 dan mockup.
7. **Roda mouse**: satu notch (120) = satu halaman, lalu jeda 380 ms (jeda mockup).

## Di luar rencana ini

- Paket 2 (bar kiri: Start, launcher, Shelf, Claude, aplikasi berjalan), paket 3 (bar kanan: cuaca, Wi‑Fi, Bluetooth, volume, brightness, tray, Power), paket 4 (mode demo, jendela Settings persis mockup, MSIX). Tombol Settings dibuat di sini; Power di paket 3, jadi sampai saat itu roda gigi paling kanan.
- Ekspresi Fokus, Kaget, Senang, dan mengikuti note (Fase 2); lirik (Fase 4).

## Review Focus

1. **Kursor diam di atas tab yang bergeser** (nama tab lain melebar di bawahnya): tab yang di-hover tidak boleh berganti sendiri. Butuh UI, jadi diperiksa manual di Task 9 Step 3 butir 2.
2. **Artwork rusak atau kosong** (byte tidak bisa di-decode, gambar 1×1, semua piksel hitam/putih): slot tetap memakai kotak `#1C1C1E` + ikon not dan warna cadangan, tanpa exception. Dites di Task 5 (`Grey_black_and_white_are_ignored`, `Empty_pixels_give_nothing`).
3. **Layar sempit** (1366 px dan lebih kecil, DPI 150%): menu tidak muat → `Tight`, nama hover jadi tooltip, pulau maks. lebar layar − 24. Dites di Task 3.
4. **Gili tidur saat musik diputar** lalu pengguna kembali: tidur menang atas musik, bangun dengan kedip dua kali, anggukan lanjut. Dites di Task 4 (`Sleep_beats_enjoy`); bangun dengan kedip dobel diperiksa manual di Task 8.
5. **Jam tepat pada pergantian detik** setelah sleep/resume atau perubahan jam sistem: tidak melompati detik atau menggeser elemen bar. Dites di Task 11 (`Next_second_starts_on_the_boundary`); lebar tetap teks jam diperiksa manual.

---

### Task 1: Samakan PRD dengan keputusan pengguna

**Files:**
- Modify: `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md` (6.3 "Halaman expanded", 14), `CLAUDE.md` ("Sumber kebenaran")

- [ ] **Step 1: CLAUDE.md**

Ganti kalimat `Kalau mockup dan PRD berbeda, PRD yang dipakai.` dengan:

```markdown
Tampilan dan gerak app harus persis mockup (keputusan pengguna 2026-10-27). Kalau mockup dan PRD berbeda, laporkan ke pengguna dan samakan keduanya; jangan memilih salah satu diam-diam.
```

- [ ] **Step 2: PRD 6.3 menu**

Di "Menu halaman": `latar #1C1C1E` → `latar putih 10%`; sorotan `#3A3A3C` → `putih 14%`; lingkaran Gili "dengan latar yang sama dengan menu (`#1C1C1E`)" → "dengan latar yang sama dengan menu (putih 10%)" (juga di tabel bentuk Gili).

- [ ] **Step 3: PRD 14 fase**

Di baris Fase 1 kolom Isi tambahkan di akhir: `; tampilan bar lengkap seperti mockup sejak Fase 1 (keputusan pengguna 2026-10-27): F-LEFT-06 Start, ikon launcher F-LEFT-02 (membuka tabnya; halaman Fase 2 berisi placeholder), F-LEFT-04/05 ikon Shelf dan Claude (tanpa fitur di baliknya), modul cuaca, Wi‑Fi, Bluetooth, volume, brightness, tray, dan Power dengan data asli dan fungsi dasar; pulau kontrol, panel tray lengkap, dan isi halaman tetap di fasenya`.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md docs/superpowers/specs/2026-10-25-top-taskbar-prd.md
git commit -m "docs: the app matches the mockup exactly, and the whole bar shows from Phase 1" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Halaman Clipboard, tepi atas mengambang, dan kurva bezier

**Files:**
- Modify: `src/Gili.Core/Island/IslandModels.cs`, `src/Gili.Core/Island/IslandLayout.cs`
- Create: `src/Gili.Core/Motion/CubicBezier.cs`
- Test: `tests/Gili.Core.Tests/Island/IslandLayoutTests.cs`, `tests/Gili.Core.Tests/Motion/CubicBezierTests.cs`

**Interfaces:**
- Produces: `IslandPage` = `Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, Claude, System`.
- Produces: `public readonly record struct IslandScreen(int BarHeight, int IslandHeight, double Width, double Height)`.
- Produces: `IslandLayout.For(IslandMode mode, IslandScreen screen, double contentWidth, bool hovered, double expandedWidth, double pageHeight) -> IslandGeometry`; konstanta `ExpandedMinHeight = 256`, `PageTop = 58`, `PageBottom = 20`. Konstanta `FloatTop` dihapus.
- Produces: `public sealed class CubicBezier(double x1, double y1, double x2, double y2) { double Ease(double t); static CubicBezier Out; static CubicBezier Expression; static CubicBezier Glow; }`.

- [ ] **Step 1: Tulis test yang gagal**

Ubah `Layout(...)` helper di `IslandLayoutTests` ke signature baru (default `IslandScreen(32, 24, 1920, 1080)`, `expandedWidth: 560`, `pageHeight: 112`), lalu ganti test mengambang:

```csharp
[Fact]
public void Floating_states_keep_the_compact_top_edge()
{
    Assert.Equal(new IslandGeometry(560, 256, 4, 32), Layout(IslandMode.Expanded));
    Assert.Equal(new IslandGeometry(560, 104, 4, 28), Layout(IslandMode.Card));
    Assert.Equal(new IslandGeometry(420, 140, 4, 30), Layout(IslandMode.DropZone));
}

[Fact]
public void Expanded_is_as_tall_as_its_page() =>
    Assert.Equal(58 + 300 + 20, Layout(IslandMode.Expanded, pageHeight: 300).Height);

[Fact]
public void Expanded_stops_at_the_screen_height_less_the_top_gap_twice() =>
    Assert.Equal(1080 - 8, Layout(IslandMode.Expanded, pageHeight: 2000).Height);

[Fact]
public void Expanded_takes_the_menu_width() => Assert.Equal(578, Layout(IslandMode.Expanded, expandedWidth: 578).Width);
```

Hapus test Notes 960 × 500 (Notes kini selebar halaman lain).

`CubicBezierTests`:

```csharp
[Fact] public void Ends_are_fixed() { Assert.Equal(0, CubicBezier.Out.Ease(0)); Assert.Equal(1, CubicBezier.Out.Ease(1)); }
[Fact] public void Out_curve_is_fast_early() => Assert.InRange(CubicBezier.Out.Ease(0.25), 0.80, 0.85);
[Fact] public void Linear_control_points_are_linear() => Assert.Equal(0.4, new CubicBezier(0, 0, 1, 1).Ease(0.4), 3);
[Fact] public void Expression_curve_overshoots() => Assert.True(CubicBezier.Expression.Ease(0.6) > 1);
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandLayoutTests|FullyQualifiedName~CubicBezierTests"`
Expected: build gagal (`IslandScreen`, `CubicBezier` belum ada).

- [ ] **Step 3: Implementasi**

`IslandLayout.For`: top = (BarHeight − IslandHeight) / 2 untuk semua mode; Expanded = (`expandedWidth`, clamp(PageTop + pageHeight + PageBottom, 256, Height − 2 × top), radius 32); kartu dan drop zone tetap min(ukuran, Width − 24). `CubicBezier.Ease`: cari parameter s dengan Newton (8 iterasi) lalu bisection cadangan sampai |x(s) − t| < 1e-6, kembalikan y(s). `Out = (.16,1,.3,1)`, `Expression = (.32,1.18,.5,1)`, `Glow = (.37,0,.63,1)`. Perbaiki pemanggil `IslandLayout.For` di `IslandWindow.xaml.cs` dengan `expandedWidth: 560, pageHeight: 178` sementara (Task 9 menggantinya); tambahkan `IslandPage.Clipboard` ke switch yang ada di Core bila compiler memintanya.

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: build sukses, semua PASS.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(core): Clipboard page, floating island keeps the compact top edge, page-tall expanded island, bezier curves (F-ISL-07, F-ISL-25)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Ukuran menu tab dan posisi sorotan

**Files:**
- Create: `src/Gili.Core/Island/TabMenuLayout.cs`
- Test: `tests/Gili.Core.Tests/Island/TabMenuLayoutTests.cs`

**Interfaces:**
- Produces: `public readonly record struct TabName(double Regular, double Bold)` (lebar nama 12 px regular dan semibold, diukur App).
- Produces: `TabMenuLayout.Need(int tabCount, bool showGili, double longestBold) -> double` = 14 + (showGili ? 42 : 0) + 6 + n × 36 + (n − 1) × 2 + 2 × (8 + longestBold) + 14.
- Produces: `TabMenuLayout.IslandWidth(double need, double screenWidth) -> double` = min(max(560, need), screenWidth − 24); `TabMenuLayout.IsTight(double need, double islandWidth) -> bool` = need > islandWidth.
- Produces: `TabMenuLayout.TabWidths(double menuWidth, IReadOnlyList<TabName> names, int active, int hovered, bool tight) -> double[]` (hovered −1 = tidak ada) dan `TabMenuLayout.Highlight(double[] widths, int active) -> (double X, double Width)`.

- [ ] **Step 1: Tulis test yang gagal**

```csharp
[Fact] public void Nine_tabs_fit_in_560() => Assert.Equal(532, TabMenuLayout.Need(9, showGili: true, longestBold: 50));
[Fact] public void Ten_tabs_widen_the_island() => Assert.Equal(578, TabMenuLayout.IslandWidth(TabMenuLayout.Need(10, true, 54), 1920));
[Fact] public void Without_gili_the_circle_is_not_counted() => Assert.Equal(490, TabMenuLayout.Need(9, showGili: false, longestBold: 50));
[Fact] public void Island_never_narrower_than_560() => Assert.Equal(560, TabMenuLayout.IslandWidth(400, 1920));

[Fact]
public void Narrow_screen_caps_the_width_and_goes_tight()
{
    var width = TabMenuLayout.IslandWidth(578, 600);
    Assert.Equal(576, width);
    Assert.True(TabMenuLayout.IsTight(578, width));
}

[Fact]
public void Leftover_width_is_shared_evenly()
{
    var w = TabMenuLayout.TabWidths(200, [new(30, 40), new(30, 40), new(30, 40)], active: 0, hovered: -1, tight: false);
    Assert.Equal([95.333, 47.333, 47.333], w.Select(x => Math.Round(x, 3)));
}

[Fact]
public void Short_menu_takes_back_in_proportion()
{
    var w = TabMenuLayout.TabWidths(150, [new(30, 40), new(30, 40), new(30, 40)], active: 0, hovered: -1, tight: false);
    Assert.Equal([75.385, 32.308, 32.308], w.Select(x => Math.Round(x, 3)));
}

[Fact]
public void Hovered_tab_opens_with_the_regular_name_unless_tight()
{
    var names = new TabName[] { new(30, 40), new(20, 24), new(30, 40) };
    // Menus exactly as wide as the tabs need, so no leftover is shared out.
    Assert.Equal([84, 64, 36], TabMenuLayout.TabWidths(194, names, active: 0, hovered: 1, tight: false));
    Assert.Equal([84, 36, 36], TabMenuLayout.TabWidths(166, names, active: 0, hovered: 1, tight: true));
}

[Fact]
public void Highlight_sits_on_the_active_tab() =>
    Assert.Equal((3 + 95.5 + 2, 47.5), TabMenuLayout.Highlight([95.5, 47.5, 47.0], active: 1));
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~TabMenuLayoutTests"`
Expected: build gagal (`TabMenuLayout` belum ada).

- [ ] **Step 3: Implementasi** (mockup `placeInd()`, baris 1899)

Lebar dasar tiap tab: aktif = 36 + 8 + Bold; di-hover dan tidak tight = 36 + 8 + Regular; lainnya 36. `inner = menuWidth − 6 − (n − 1) × 2`, `left = inner − sum`; akhir = `left ≥ 0 ? w + left / n : w + left × w / sum`. Highlight X = 3 + Σ(lebar tab sebelumnya + 2).

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~TabMenuLayoutTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Island/TabMenuLayout.cs tests/Gili.Core.Tests/Island/TabMenuLayoutTests.cs
git commit -m "feat(core): icon tab menu width, tab widths and highlight placement (F-ISL-25)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Aturan Gili: arah pandang, ekspresi, kedip, tidur

**Files:**
- Create: `src/Gili.Core/Gili/GiliRules.cs`
- Test: `tests/Gili.Core.Tests/Gili/GiliRulesTests.cs`

**Interfaces:**
- Produces: `public enum GiliForm { InIsland, Circle, Menu }`, `public enum GiliMood { Neutral, Enjoy, Sleep, Wide }` (Fase 2 menambah Focus, Surprise, Happy di atas Wide/Sleep sesuai tabel PRD).
- Produces: `public readonly record struct GiliInputs(bool Dragging, TimeSpan Idle, bool MusicPlayingInSlot)`.
- Produces: `static class GiliRules`:
  - `TimeSpan SleepAfter = 5 min`, `TimeSpan RecentreAfter = 8 s`, `double DragThreshold = 6`, `SpringParameters EyeSpring = (0.8, 0.25)`, `SpringParameters ShapeSpring = (0.8, 0.45)`.
  - `GiliMood Mood(GiliInputs inputs)`: Wide > Sleep (Idle ≥ 5 menit) > Enjoy > Neutral.
  - `(double X, double Y) Aim(double pointerX, double pointerY, double eyesCentreX, GiliForm form, double screenWidth, double screenHeight)`: X = clamp((px − cx) / span, −1, 1) dengan span = 0,4 × lebar layar untuk InIsland dan 400 untuk Circle/Menu; Y = clamp((py − H/2) / (0,4 × H), −1, 1).
  - `(double X, double Y) Offset(double aimX, double aimY, GiliForm form, double islandWidth, double islandHeight, double eyesWidth, double eyesHeight)`: InIsland X × max(0, (lebar − 18 − lebar mata) / 2), Y × max(0, (tinggi − tinggi mata) / 2 − 3); Circle × 2 px; Menu × 3 px.
  - `bool CanBlink(GiliMood mood)`: false untuk Sleep dan Wide.
  - `(TimeSpan Wait, bool Double) NextBlink(Random random)`: dobel bila `NextDouble() < 0.2`; wait = 3000 + `NextDouble()` × 3000 ms, +350 ms bila dobel.
  - `GiliEyeSize Eyes(GiliForm form, int islandHeight)` → `record struct GiliEyeSize(double Side, double Radius, double Gap)`: InIsland (0,42 × tinggi pulau, 3, 2); Circle (6, 2, 2); Menu (9, 3, 3).

- [ ] **Step 1: Tulis test yang gagal**

```csharp
[Fact] public void Dragging_beats_everything() => Assert.Equal(GiliMood.Wide, GiliRules.Mood(new(true, TimeSpan.FromHours(1), true)));
[Fact] public void Sleep_beats_enjoy() => Assert.Equal(GiliMood.Sleep, GiliRules.Mood(new(false, TimeSpan.FromMinutes(5), true)));
[Fact] public void Under_five_minutes_is_awake() => Assert.Equal(GiliMood.Enjoy, GiliRules.Mood(new(false, TimeSpan.FromSeconds(299), true)));
[Fact] public void Nothing_going_on_is_neutral() => Assert.Equal(GiliMood.Neutral, GiliRules.Mood(new(false, TimeSpan.Zero, false)));
[Fact] public void Sleeping_and_wide_eyes_do_not_blink() { Assert.False(GiliRules.CanBlink(GiliMood.Sleep)); Assert.False(GiliRules.CanBlink(GiliMood.Wide)); Assert.True(GiliRules.CanBlink(GiliMood.Enjoy)); }

[Fact]
public void Island_eyes_reach_the_end_at_40_percent_of_the_screen()
{
    Assert.Equal((1.0, 0.0), GiliRules.Aim(960 + 768, 540, 960, GiliForm.InIsland, 1920, 1080));
    Assert.Equal((-0.5, 0.0), GiliRules.Aim(960 - 384, 540, 960, GiliForm.InIsland, 1920, 1080));
}

[Fact] public void Circle_eyes_lean_fully_at_400_px() => Assert.Equal(1, GiliRules.Aim(500, 540, 100, GiliForm.Circle, 1920, 1080).X);
[Fact] public void Pointer_at_the_top_looks_up() => Assert.Equal(-1, GiliRules.Aim(960, 0, 960, GiliForm.InIsland, 1920, 1080).Y);

[Fact]
public void Island_eyes_travel_to_9_px_from_the_edge_and_3_px_from_top_and_bottom()
{
    var (x, y) = GiliRules.Offset(1, 1, GiliForm.InIsland, islandWidth: 200, islandHeight: 24, eyesWidth: 22.16, eyesHeight: 10.08);
    Assert.Equal(79.92, x, 2);
    Assert.Equal(3.96, y, 2);
}

[Fact] public void Circle_and_menu_eyes_lean_2_and_3_px() { Assert.Equal((2.0, -2.0), GiliRules.Offset(1, -1, GiliForm.Circle, 0, 0, 0, 0)); Assert.Equal((3.0, 3.0), GiliRules.Offset(1, 1, GiliForm.Menu, 0, 0, 0, 0)); }
[Fact] public void Island_eyes_are_42_percent_of_the_island() => Assert.Equal(new GiliEyeSize(10.08, 3, 2), GiliRules.Eyes(GiliForm.InIsland, 24));

[Fact]
public void Blinks_come_every_3_to_6_seconds_one_in_five_double()
{
    var random = new Random(7);
    var plans = Enumerable.Range(0, 2000).Select(_ => GiliRules.NextBlink(random)).ToList();
    Assert.All(plans, p => Assert.InRange(p.Wait.TotalMilliseconds, 3000, 6350));
    Assert.InRange(plans.Count(p => p.Double) / 2000.0, 0.17, 0.23);
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~GiliRulesTests"`
Expected: build gagal (`GiliRules` belum ada).

- [ ] **Step 3: Implementasi** sesuai Interfaces.

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~GiliRulesTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Gili tests/Gili.Core.Tests/Gili
git commit -m "feat(core): Gili gaze, expressions, blinks and sleep rules (F-ISL-19, F-ISL-24)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Warna artwork

**Files:**
- Create: `src/Gili.Core/Media/ArtworkPalette.cs`
- Test: `tests/Gili.Core.Tests/Media/ArtworkPaletteTests.cs`

**Interfaces:**
- Produces: `public readonly record struct Rgb(byte R, byte G, byte B) { string Hex { get; } }` dan `ArtworkPalette.From(ReadOnlySpan<byte> bgra, int width, int height) -> (Rgb Primary, Rgb Secondary)?` (null bila tidak ada warna yang cukup jenuh).

- [ ] **Step 1: Tulis test yang gagal**

```csharp
[Fact] public void A_single_colour_is_both_colours() { var p = ArtworkPalette.From(Fill(16, 16, 0xF9, 0x73, 0x16), 16, 16)!.Value; Assert.Equal("#F97316", p.Primary.Hex); Assert.Equal(p.Primary, p.Secondary); }
[Fact] public void The_larger_area_is_primary() { var p = ArtworkPalette.From(Split(16, 16, top: (0x0E, 0xA5, 0xE9), bottom: (0x9F, 0x12, 0x39), topRows: 11), 16, 16)!.Value; Assert.Equal("#0EA5E9", p.Primary.Hex); Assert.Equal("#9F1239", p.Secondary.Hex); }
[Fact] public void Grey_black_and_white_are_ignored() => Assert.Null(ArtworkPalette.From(Split(8, 8, (0, 0, 0), (0xFF, 0xFF, 0xFF), 4), 8, 8));
[Fact] public void Empty_pixels_give_nothing() => Assert.Null(ArtworkPalette.From([], 0, 0));
[Fact] public void Grey_with_a_coloured_corner_picks_the_colour() { var px = Fill(16, 16, 0x80, 0x80, 0x80); Paint(px, 16, 0, 0, 4, 4, 0x30, 0xD1, 0x58); Assert.Equal("#30D158", ArtworkPalette.From(px, 16, 16)!.Value.Primary.Hex); }
```

(`Fill`, `Split`, `Paint` adalah helper BGRA di kelas test.)

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~ArtworkPaletteTests"`
Expected: build gagal (`ArtworkPalette` belum ada).

- [ ] **Step 3: Implementasi**

Ambil sampel paling banyak 32 × 32 piksel merata. Buang piksel dengan saturasi HSV < 0,2 atau value < 0,15 atau value > 0,97 dan saturasi < 0,1. Kelompokkan ke 12 ember hue (30°). Primary = rata-rata warna ember terbesar; Secondary = rata-rata ember terbesar kedua yang hue-nya ≥ 30° dari primary, bila jumlahnya ≥ 10% dari ember terbesar; selain itu sama dengan Primary. Tidak ada piksel lolos → null.

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~ArtworkPaletteTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Media/ArtworkPalette.cs tests/Gili.Core.Tests/Media/ArtworkPaletteTests.cs
git commit -m "feat(core): two colours from the track artwork (F-ISL-04, F-ISL-21)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Pembacaan kursor, tombol mouse, dan waktu input

**Files:**
- Create: `src/Gili.Platform/Input/PointerState.cs`
- Modify: `src/Gili.Platform/Interop/Win32.cs` (`GetCursorPos`, `GetAsyncKeyState`, `GetLastInputInfo`, `LASTINPUTINFO`, `POINT` bila belum ada)

**Interfaces:**
- Produces: `public readonly record struct PointerSample(int X, int Y, bool Left, bool Right)` dan `static class PointerState { PointerSample Read(); TimeSpan IdleTime(); }` (koordinat piksel fisik; `IdleTime` dari `Environment.TickCount64 − dwTime` dengan wrap 32-bit).

- [ ] **Step 1: Implementasi** sesuai Interfaces (P/Invoke murni, tanpa logika).
- [ ] **Step 2: Build**

Run: `dotnet build Gili.slnx`
Expected: Build succeeded, 0 warning.

- [ ] **Step 3: Commit**

```bash
git add src/Gili.Platform
git commit -m "feat(platform): read the pointer, mouse buttons and idle time without hooks (F-ISL-19, F-ISL-24)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Kontrol mata `GiliEyes` dan `GiliService`

**Files:**
- Create: `src/Gili.App/Gili/GiliEyes.cs`, `src/Gili.App/Gili/GiliService.cs`
- Modify: `src/Gili.Core/Settings/AppSettings.cs` (+ `ShowGili`, `ShowSeconds`, `AmbientGlow`, `AmbientMotion` = true; `BarOpacity` = 100), `tests/Gili.Core.Tests/Settings/AppSettingsTests.cs`, `tests/Gili.Core.Tests/Data/SettingsStoreTests.cs` (default baru)

**Interfaces:**
- Consumes: `GiliRules`, `GiliForm`, `GiliMood`, `CubicBezier.Expression` (Task 2, 4), `PointerState` (Task 6).
- Produces: `GiliEyes : FrameworkElement` dengan DP `Form` (`GiliForm`), `IslandHeight` (int), `Mood` (`GiliMood`), `OffsetX`, `OffsetY` (double), dan metode `Blink(bool twice)`. Menggambar dua kotak putih membulat (ukuran dari `GiliRules.Eyes`) dan `z` saat tidur (hanya InIsland).
- Produces: `GiliService` (satu per app): `GiliService(SettingsHub settings, Func<bool> barVisible)`, event `Changed` (mood/offset), `Register(GiliEyes eyes)`, `SetMusicPlayingInSlot(bool)`, `SetVisible(bool)`; mengatur timer 30 Hz, pegas mata, kedip acak dan kedip klik, tidur.

- [ ] **Step 1: Test default settings (gagal)**

Di `AppSettingsTests` tambahkan:

```csharp
[Fact]
public void New_defaults_match_the_mockup()
{
    var s = new AppSettings();
    Assert.Equal(100, s.BarOpacity);
    Assert.True(s.ShowGili && s.ShowSeconds && s.AmbientGlow && s.AmbientMotion);
}
```

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~AppSettingsTests"` → Expected: build gagal (properti belum ada).

- [ ] **Step 2: Tambah properti settings, jalankan lagi**

Expected: PASS (perbaiki test `SettingsStoreTests` yang mengharapkan 78).

- [ ] **Step 3: `GiliEyes`**

Gambar di `OnRender` dua `RoundedRectangle` putih dengan ukuran dan celah dari `GiliRules.Eyes`, ditengah kontrol plus `OffsetX/OffsetY`. Bentuk per mood (dianimasikan lewat `ScaleTransform` dan radius, bukan gambar ulang tiap frame):

| Mood | Skala Y | Origin | Geser Y | Radius | Durasi |
|---|---|---|---|---|---|
| Neutral | 1 | tengah | 0 | normal | 260 ms `CubicBezier.Expression` |
| Enjoy | 0,5 | bawah | + anggukan | normal | 260 ms |
| Wide | 1,3 (X juga) | tengah | 0 | 50% | 260 ms, radius 200 ms |
| Sleep | 0,16 | tengah | +1 px | 2 px | 900 ms; `z` (8 px bold `#8E8E93`) fade 400 ms mulai 600 ms, naik 2 px dalam 900 ms kurva out |

Kedip: keyframe skala Y 230 ms (1 → 0,1 pada 39% dengan ease-in, → 1 ease-out); dobel 580 ms (0,1 pada 15,5%, 1 pada 39,7–60,3%, 0,1 pada 75,9%, 1 pada 100%). Animation effects mati: kedip = opacity 1 → 0,2 → 1 dalam 200 ms, tanpa transisi bentuk. Anggukan (Enjoy): siklus 480 ms, geser Y 0 → 0,22 × sisi mata pada 25% (`CubicBezier(.3,0,.6,1)`), kembali pada 60% (`CubicBezier(.4,0,.2,1)`), diam sampai 100%; `Timeline.DesiredFrameRate = 30`.

- [ ] **Step 4: `GiliService`**

- `DispatcherTimer` 33 ms hanya selama `SetVisible(true)`, Show Gili nyala, `barVisible()`, bukan fullscreen. Tiap tik: `PointerState.Read()`, konversi ke DIP; posisi berubah → `GiliRules.Aim` ke bentuk yang tampil; tombol kiri/kanan baru ditekan → kedip klik (kalau `CanBlink`; saat tidur → bangun); kiri ditahan dan bergeser ≥ 6 px → Dragging sampai dilepas.
- Pegas mata (`Spring` × 2, `EyeSpring`) di-step di `CompositionTarget.Rendering` hanya selama belum diam; set `OffsetX/Y` hanya bila berubah ≥ 0,25 px.
- `DispatcherTimer` 1 detik: `PointerState.IdleTime()` → mood; kursor diam ≥ 8 detik → target (0, 0).
- Kedip acak dengan `GiliRules.NextBlink`; bangun dari tidur = kedip dobel.
- Animation effects mati: tanpa mengikuti kursor (offset 0).

- [ ] **Step 5: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua PASS.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat(app): Gili's eyes, blinks, nods and sleep; new appearance settings (F-ISL-19, F-ISL-24)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Pulau compact: Gili, artwork musik, gelembung

**Files:**
- Modify: `src/Gili.App/Island/IslandWindow.xaml(.cs)`, `src/Gili.App/Island/IslandViewModel.cs`, `src/Gili.App/App.xaml.cs`

**Interfaces:**
- Consumes: `GiliEyes`, `GiliService` (Task 7), `ArtworkPalette` (Task 5).
- Produces: `IslandViewModel.Artwork` (`ImageSource?`, ukuran slot 2×), `HasArtwork`, `AccentBrush` (warna utama artwork atau warna aplikasi), `GlowPrimary`, `GlowSecondary` (`Color`), `ShowGiliInIsland` (`bool`: Show Gili dan compact tanpa aktivitas), `ShowGiliCircle` (Show Gili, bukan fullscreen, compact/wide dengan aktivitas atau wide). `ShowClock` dan `Time`/`Date` dihapus dari view model pulau.

- [ ] **Step 1: View model**

Saat cover berganti: decode sekali ke `Artwork` (lebar decode = 2 × (tinggi pulau − 6)) dan `Cover` (224 px), hitung `ArtworkPalette.From` dari `Cover` (`FormatConvertedBitmap` ke Bgra32, `CopyPixels`). `MediaApp` titik sumber dan `LevelBrush` memakai `AccentBrush`.

- [ ] **Step 2: Isi compact** (mockup `.slot`, `.art`, `.viz`, `.pp`)

Hapus jam dari pulau. Musik: urutan **artwork, judul, bar level, play/pause**, celah 6 px, slot selebar pulau − 28 dengan isi direntang ujung ke ujung. Artwork (tinggi pulau − 6) persegi, radius 5, garis dalam 0,5 px putih 14%; tanpa artwork: kotak `#1C1C1E` dengan ikon not 11 px `#8E8E93`. Bar level memakai `AccentBrush`. Video tetap ikon film oranye + judul + garis 36 px. Isi slot berganti dengan fade masuk 180 ms (`slotIn`).

- [ ] **Step 3: Gili di pulau dan lingkaran**

- Di dalam pulau: `GiliEyes Form=InIsland` di area dengan inset 9 px kiri-kanan. Saat ada aktivitas: opacity 0 (200 ms), geser −12 px, skala 0,5 dengan pegas `ShapeSpring`; saat kembali, opacity mulai 60 ms.
- Lingkaran: `Border` hitam bulat setinggi pulau di tepi atas compact, `GiliEyes Form=Circle` di dalamnya; tampil: skala 0,6 → 1 (`ShapeSpring`), opacity 160 ms, kiri dari (tepi kiri pulau − tinggi pulau / 2) ke (tepi kiri pulau − 6 − tinggi pulau). Posisinya ikut lebar pulau tiap frame (hover dan perubahan lebar). Disembunyikan saat mengambang dan fullscreen. Klik lingkaran = klik pulau.
- Gelembung musik: artwork bulat (gelembung − 6) menggantikan ikon not.
- Pasang `GiliService` di `App.xaml.cs`; `SetMusicPlayingInSlot` dari `Slot == Media && MusicPlaying`.

- [ ] **Step 4: Build, test, periksa**

Run: `dotnet build Gili.slnx`, `dotnet test tests/Gili.Core.Tests` → sukses, PASS. Lalu `dotnet run --project src/Gili.App`:
1. Tanpa aktivitas: pulau 200 px berisi dua mata 10 px yang berjalan mengikuti kursor sampai 9 px dari ujung, naik-turun mengikuti tinggi kursor, berkedip tiap 3–6 detik dan setiap klik.
2. Spotify diputar: mata mengecil keluar ke kiri, lingkaran menetes ke 6 px di kiri pulau; slot = artwork, `Judul – Artis`, bar level berwarna artwork, tombol pause; mata setengah terpejam dan mengangguk tiap 0,48 detik.
3. Menahan klik kiri dan menyeret ≥ 6 px: mata membesar bulat sampai dilepas.
4. Bandingkan tangkapan bar dengan mockup (Task 12, keadaan `idle` dan `music`).

- [ ] **Step 5: Commit**

```bash
git add src
git commit -m "feat(app): Gili in the compact island and beside it, track artwork in the slot (F-ISL-04, F-ISL-19, F-ISL-24)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Pulau terbuka: menu tab ikon, Gili di samping menu, tinggi mengikuti isi

**Files:**
- Modify: `src/Gili.App/Island/IslandWindow.xaml(.cs)`, `src/Gili.App/Island/IslandViewModel.cs`
- Create: `src/Gili.App/Controls/BezierEase.cs` (`IEasingFunction` di atas `CubicBezier`)

**Interfaces:**
- Consumes: `TabMenuLayout` (Task 3), `IslandLayout.For` + `IslandScreen` (Task 2), `GiliEyes` (Task 7), `BezierEase`.
- Produces: `IslandViewModel.Tabs` = 9 tab (Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard `Coming in Phase 4`, Shelf, System; ikon Clipboard dari mockup `i-clipboard`).

- [ ] **Step 1: Menu**

- Hapus `TabHighlight`/`UniformGrid` dan baris jam bawah (`FooterButton`, `OnFooterClick`).
- Menu: `Border` putih 10% radius 18 padding 3 di (56, 14) sampai 14 px dari kanan, tinggi 36; tab dalam `StackPanel` horizontal celah 2, lebar tiap tab dari `TabMenuLayout.TabWidths`.
- Tab: ikon 15 px + nama 12 px (lebar dianimasikan 0 ↔ lebar nama, 300 ms `BezierEase(CubicBezier.Out)`; opacity 160 ms, tab aktif 220 ms mulai 90 ms); padding 10,5 ↔ 12, celah 0 ↔ 5 dengan gerak yang sama. Warna `#8E8E93`, hover/aktif `#F5F5F7`, aktif semibold dan ikon `#30D158`; tab Media saat musik = `LevelMeter` hijau 15 × 13.
- Sorotan putih 14% radius 15: X dan lebar dari `TabMenuLayout.Highlight`, 300 ms kurva out.
- Lebar nama diukur sekali per weight dengan `FormattedText` (Segoe UI Variable Text 12 px).
- Hover hanya berganti bila posisi layar kursor berbeda dari `MouseMove` sebelumnya; fokus keyboard juga membuka nama; tight → nama hover jadi `ToolTip`.
- Gili: `Border` bulat 36 px putih 10% di (14, 14) dengan `GiliEyes Form=Menu`; Show Gili mati → tanpa lingkaran dan menu mulai 14 px.

- [ ] **Step 2: Ukuran dan isi**

- Lebar pulau = `TabMenuLayout.IslandWidth(Need(9, ShowGili, bold terpanjang), lebar layar)`; tinggi dari `IslandLayout.For` dengan `pageHeight` = tinggi `DesiredSize` halaman aktif setelah `Measure(lebar, ∞)`; area halaman di Y 58 setinggi tinggi pulau − 78, halaman padding kiri-kanan 32.
- Pindah halaman: geser 300 ms kurva out; tinggi pulau berubah dengan pegas buka.
- Isi muncul: opacity 220 ms mulai 120 ms, `TranslateTransform` Y −6 → 0 dan skala 0,98 → 1 dalam 380 ms kurva out, blur 6 → 0 dalam 260 ms.
- Bayangan saat mengambang: dua plate (Y 22 blur 44 hitam 70%, Y 8 blur 18 hitam 50%) + garis atas putih 9%.
- Roda: 120 per halaman, jeda 380 ms.

- [ ] **Step 3: Build, test, periksa**

Run: `dotnet build Gili.slnx`, `dotnet test tests/Gili.Core.Tests` → sukses, PASS. `dotnet run --project src/Gili.App`, klik pulau:
1. Pulau 560 px, tepi atas tetap 4 px; lingkaran Gili 36 px di kiri menu, matanya mengikuti kursor ±3 px.
2. 9 ikon tersebar; hanya Media bernama. Hover tab lain: namanya melebar, tab lain merapat, lebar pulau tetap; kursor diam di atas tab yang bergeser tidak memicu tab lain.
3. Klik Notes: sorotan dan nama pindah bersama dalam 300 ms, halaman bergeser.
4. Tidak ada baris jam di bawah; tinggi 256 untuk Media.

- [ ] **Step 4: Commit**

```bash
git add src
git commit -m "feat(app): icon tab menu with sliding names, Gili beside it, island sized by menu and page (F-ISL-07, F-ISL-19, F-ISL-25)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Ambient glow

**Files:**
- Create: `src/Gili.App/Island/AmbientGlow.cs`
- Modify: `src/Gili.App/Island/IslandWindow.xaml(.cs)`

**Interfaces:**
- Consumes: `IslandViewModel.GlowPrimary/GlowSecondary` (Task 8), `AppSettings.AmbientGlow/AmbientMotion` (Task 7), `CubicBezier.Glow` (Task 2).
- Produces: `AmbientGlow : Canvas` dengan `SetColors(Color first, Color second, bool dim)`, `Start(double width, double height)`, `Pause()`, `Reset()`.

- [ ] **Step 1: Implementasi** (mockup `.glow`, `glowLeg()`, `renderGlow()`)

- Dua `Ellipse` 75% lebar × 190% tinggi pulau dengan `RadialGradientBrush` (warna → transparan): b1 di (−18% lebar, −30% tinggi), b2 di kanan −20%, bawah −70%; dipotong radius pulau.
- Warna: halaman Media = (primary alpha 0,55, secondary alpha 0,45), atau alpha 0,32 untuk yang kedua bila warnanya sama; × 0,5 saat musik dijeda. Halaman lain: Timer `#FF9F0A`, Alarms `#FFD60A`, Calendar `#FF453A`, Todo `#0A84FF`, Notes `#FFD60A`, Clipboard `#66D4CF`, Shelf `#64D2FF`, System `#BF5AF2`. Ganti warna 600 ms; glow fade masuk 400 ms saat pulau terbuka.
- Gerak: tiap gumpalan memilih titik acak ±25% lebar, ±30% tinggi, durasi 10–16 detik, `BezierEase(CubicBezier.Glow)`, lalu titik berikutnya; bernapas skala 0,92 ↔ 1,08, 9 detik bolak-balik (b2 mulai di tengah siklus). Semua `Timeline.DesiredFrameRate = 30`.
- Berhenti total saat pulau menutup (kembali ke posisi diam), diam di tempat saat musik dijeda di Media, Ambient motion mati, atau Animation effects mati. Ambient glow mati → tidak digambar.

- [ ] **Step 2: Build, test, periksa**

Run: `dotnet build Gili.slnx`, `dotnet test tests/Gili.Core.Tests` → sukses, PASS. Buka pulau saat Spotify memutar lagu: cahaya berwarna cover melayang pelan; jeda musik → berhenti dan meredup; tutup pulau → Task Manager menunjukkan CPU Gili kembali < 1%; selama terbuka ≤ 3%.

- [ ] **Step 3: Commit**

```bash
git add src
git commit -m "feat(app): ambient glow that wanders at random behind the open island (F-ISL-21)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Jam dua baris dan tombol Settings di kanan bar

**Files:**
- Modify: `src/Gili.Core/Formatting/ClockText.cs`, `src/Gili.App/ClockService.cs`, `src/Gili.App/Bar/BarWindow.xaml(.cs)`, `src/Gili.App/Bar/BarViewModel.cs`, `src/Gili.App/App.xaml.cs`, `src/Gili.App/Resources/Icons.xaml` (+ `Icon.Gear`, `Icon.Clipboard` dari mockup)
- Test: `tests/Gili.Core.Tests/Formatting/ClockTextTests.cs`

**Interfaces:**
- Produces: `ClockText.BarTime(DateTime local, bool use24Hour, bool seconds)` → `14:32:05` / `2:32:05 PM` / `14:32` / `2:32 PM`; `ClockText.BarDate(DateTime)` → `SAT 25 OCT`; `ClockText.LongDate(DateTime)` → `Saturday, 25 October 2026`; `ClockText.UntilNextSecond(DateTime)`.
- Produces: `ClockService.SecondTicked` (hanya berjalan saat Show seconds nyala), `ClockService.Ticked` tetap per menit.
- Produces: `BarWindow.ClockClicked`, `BarWindow.SettingsRequested` (sudah ada, dipakai tombol roda gigi).

- [ ] **Step 1: Tulis test yang gagal**

```csharp
static readonly DateTime At = new(2026, 10, 25, 14, 32, 5, 400);
[Fact] public void Bar_time_has_seconds() => Assert.Equal("14:32:05", ClockText.BarTime(At, true, true));
[Fact] public void Bar_time_12_hour() => Assert.Equal("2:32:05 PM", ClockText.BarTime(At, false, true));
[Fact] public void Bar_time_without_seconds() => Assert.Equal("14:32", ClockText.BarTime(At, true, false));
[Fact] public void Bar_date_is_upper_case() => Assert.Equal("SUN 25 OCT", ClockText.BarDate(At));
[Fact] public void Long_date_for_the_tooltip() => Assert.Equal("Sunday, 25 October 2026", ClockText.LongDate(At));
[Fact] public void Next_second_starts_on_the_boundary() => Assert.Equal(TimeSpan.FromMilliseconds(600), ClockText.UntilNextSecond(At));
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~ClockTextTests"`
Expected: build gagal (metode belum ada).

- [ ] **Step 3: Implementasi Core**, jalankan test → PASS.

- [ ] **Step 4: Bar**

- Modul jam (gaya `ClickableModule`) setelah RAM: dua baris, tanggal `BarDate` sebagai label, waktu `BarTime` sebagai nilai dengan `MinWidth` = lebar teks `00:00:00` (atau `00:00:00 PM` untuk 12 jam) supaya bar tidak bergeser; MinWidth blok 46. Tooltip `LongDate`. Klik → `IslandController.Toggle(IslandPage.Calendar)` dan tidak dihitung sebagai klik di luar pulau.
- `ClockService`: timer detik dengan interval `UntilNextSecond + 15 ms`, dijadwalkan ulang tiap tik; hanya teks waktu yang berubah tiap detik.
- Tombol Settings: modul ikon roda gigi 16 px, tooltip `Settings`, klik → `SettingsRequested`; paling kanan sampai Power ada.
- Elemen baru ikut aturan memudar saat tertimpa pulau (`IslandLayout.Covers`).

- [ ] **Step 5: Build, test, periksa**

Run: `dotnet build Gili.slnx`, `dotnet test tests/Gili.Core.Tests` → sukses, PASS. Jalankan: jam `TUE 27 OCT` / `00:49:47` berdetak tepat bersamaan dengan jam Windows tanpa menggeser modul; klik jam membuka pulau Calendar dan klik lagi menutupnya; roda gigi membuka Settings.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "feat(bar): two-line clock with seconds and a Settings button on the right (F-RIGHT-11, F-BAR-13)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Bandingkan dengan mockup dan verifikasi

**Files:**
- Create: `scripts/mockup-shot.ps1`

**Interfaces:**
- Produces: `pwsh scripts/mockup-shot.ps1 -State <idle|music|media-page|charging> -Out <png>` yang membuat salinan mockup di folder temp dengan CSS `*{transition:none!important;animation:none!important}` dan skrip yang menekan tombol mockup untuk keadaan itu (`No activity (Gili)`, `Media playing`, `Media` lalu `Open / close`, `Charger`), lalu memotret dengan Edge headless 1920 × 1080.

- [ ] **Step 1: Tulis skrip** dan jalankan untuk keempat keadaan.
- [ ] **Step 2: Potret app** dalam keadaan yang sama (Spotify untuk musik, colok charger), potong 1920 × 34 untuk bar dan area pulau untuk halaman Media; gabungkan berdampingan per keadaan di folder scratchpad.
- [ ] **Step 3: Catat setiap perbedaan** ukuran, warna, jarak, atau gerak di ledger, perbaiki yang ada di cakupan rencana ini (Task 8–11), dan laporkan sisanya ke pengguna bersama gambar perbandingan.
- [ ] **Step 4: Verifikasi lengkap**

Run:

```bash
dotnet build Gili.slnx -c Release
dotnet test tests/Gili.Core.Tests
dotnet format Gili.slnx --verify-no-changes
```

Expected: sukses, semua PASS, format bersih. Lalu Task Manager: CPU Gili < 1% diam dengan pulau tertutup (kedip saja), ≤ 3% dengan pulau terbuka dan glow bergerak; RAM private < 120 MB.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-shot.ps1 src
git commit -m "chore: mockup screenshot script and fixes from the side-by-side check" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
