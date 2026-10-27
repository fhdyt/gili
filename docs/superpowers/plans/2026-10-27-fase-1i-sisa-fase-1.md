# Fase 1i: Sisa Fase 1 (cuplikan clipboard, jaringan hari ini, jeda polling) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Menutup sisa Fase 1: setiap copy/cut tampil 3 detik di slot pulau (F-ISL-17), tooltip jaringan menampilkan total hari ini, dan polling berhenti saat layar mati (PRD 8.1).

**Architecture:** Aturan teks cuplikan, nama gambar, tenggat 3 detik, total jaringan per hari, dan gerbang polling ada di `Gili.Core` dan dites. `Gili.Platform` membaca clipboard lewat `AddClipboardFormatListener` (event, tanpa polling) dan keadaan layar lewat `RegisterPowerSettingNotification(GUID_CONSOLE_DISPLAY_STATE)`. `Gili.App` menampilkan cuplikan di slot compact dan menghentikan/menjalankan timer.

**Tech Stack:** .NET 10 WPF, Win32 clipboard API, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.3 "Cuplikan clipboard" (baris ±276–292), F-ISL-17, 6.4 Jaringan (`Hover: adapter, total hari ini`), 8.1 (jeda polling, anggaran), 10 (thumbnail clipboard hanya di memori); mockup `showClip`, `ACT.copyText/copyImg/copyFile/cutFile/copySecret`, CSS `.slot`, `.thumb`, `.k2`.

## Global Constraints

- Teks persis: teks `"<baris pertama>"` (maks. 40, `trunc` mockup: lebih dari n → n−1 karakter + `…`); file `3 files` atau nama satu file (maks. 32); gambar `<nama>` (maks. 32) + `1920 × 1080` abu (`#8E8E93`); sensitif `Copied (hidden)`. Tooltip: `Copied: <teks sampai 120>`, `Image copied · <nama> · <w> × <h>`, `Copied: a, b, c`, `Cut: a, b, c`, `Hidden because the app marked it as sensitive`.
- Ikon 14 px: clipboard (teks), file, gunting biru `#0A84FF` (cut), gembok (sensitif). Thumbnail 36 × 20, radius 4, dipotong mengisi (cover), garis 1 px putih 16%. Celah 6 px seperti `.slot`.
- Cuplikan 3 detik; copy baru dalam 3 detik mengganti isi dan menghitung ulang. Pulau tidak terbuka; aktivitas lain pindah ke gelembung (aturan coordinator yang ada). Klik slot membuka halaman Clipboard.
- Isi sensitif (format `ExcludeClipboardContentFromMonitorProcessing` ada, atau `CanIncludeInClipboardHistory` = 0) tidak dibaca isinya sama sekali. Thumbnail dan teks hanya di memori, tidak pernah ke disk atau log.
- Polling dijeda saat layar mati: statistik 1 detik, detik jam, Wi‑Fi 10 detik, tick media. Anggaran: pemantau clipboard dan layar 0% saat diam (event).

## Keputusan yang dibuat di rencana ini

1. **Thumbnail mengikuti mockup** (36 × 20, cover), bukan "rasio dipertahankan" di PRD; PRD 6.3 disamakan (keputusan pengguna 2026-10-27: app persis mockup).
2. **Nama gambar `Screenshot`** bila pemilik clipboard adalah `SnippingTool`, `ScreenClippingHost`, atau `ScreenSketch`, atau bila clipboard hanya berisi bitmap tanpa pemilik (Print Screen).
3. **Total jaringan hari ini** dihitung di memori dari selisih tiap detik dan direset saat tanggal lokal berganti; setelah Gili restart hitungan mulai lagi dari 0 sampai Fase 2 punya SQLite. Tooltip: baris adapter, lalu `Today · ↑ 120 MB  ↓ 1.4 GB` (satuan seperti modul).
4. **Gambar yang dicopy sebagai file** (satu file gambar) dibaca dimensinya dari header file (`BitmapDecoder`, `DelayCreation`) dan thumbnail-nya di-decode setinggi 40 px; file yang tidak bisa dibaca tampil sebagai file biasa.

## Review Focus

1. **Clipboard sedang dikunci aplikasi lain** saat event datang: baca ulang hingga 3× tiap 50 ms, lalu abaikan tanpa exception.
2. **Gambar sangat besar** (screenshot 4K, gambar 50 MP dari browser): hanya thumbnail 40 px yang disimpan; tidak ada lonjakan RAM menetap > 5 MB setelah 3 detik.
3. **Copy beruntun cepat** (Ctrl+C ditahan): satu cuplikan aktif, 3 detik dihitung dari copy terakhir. Dites di Task 1.
4. **Copy saat pulau terbuka atau saat fullscreen**: aturan prioritas yang ada (aktivitas tidak tampil saat fullscreen; saat terbuka cuplikan menunggu di slot). Dites di Task 1.
5. **Layar mati lalu nyala**: semua polling jalan lagi dan jam langsung benar; sleep tidak meninggalkan timer mati.

---

### Task 1: Aturan cuplikan clipboard (Core)

**Files:**
- Create: `src/Gili.Core/Clipboard/ClipboardSnippet.cs`
- Modify: `src/Gili.Core/Island/IslandCoordinator.cs`
- Test: `tests/Gili.Core.Tests/Clipboard/ClipboardSnippetTests.cs`, `tests/Gili.Core.Tests/Island/IslandCoordinatorTests.cs`

**Interfaces:**
- Produces:
  - `public enum SnippetKind { Text, Image, Files, Cut, Hidden }`
  - `public sealed record ClipboardContent(bool Sensitive, string? Text, IReadOnlyList<string> Files, bool Cut, bool HasImage, int ImageWidth, int ImageHeight, string? Html, string? Url, string? OwnerProcess)` — yang dibaca Platform.
  - `public sealed record ClipboardSnippet(SnippetKind Kind, string Text, string? Detail, string Tooltip)` (`Detail` = dimensi gambar) dan `public static ClipboardSnippet? ClipboardSnippets.For(ClipboardContent c, Func<string, (int W, int H)?> imageFileSize)` — null bila tidak ada yang bisa ditampilkan (teks kosong/spasi saja, clipboard kosong).
  - `public static string ClipboardSnippets.ImageName(ClipboardContent c)`.
  - `IslandCoordinator.ShowClipboard()` (aktivitas `ClipboardSnippet` selama `ClipboardSnippets.Duration` = 3 detik, dihitung ulang tiap panggilan, ikut `NextDeadline`) dan `PageFor(ClipboardSnippet) = IslandPage.Clipboard`.

- [ ] **Step 1: Tes yang gagal**: `Text_shows_the_first_line_in_quotes_cut_at_40` (`"Meeting at 3 in the Merapi room, bring a charger\nsecond"` → `"Meeting at 3 in the Merapi room, bring …"`), `Text_tooltip_stops_at_120`, `Blank_text_shows_nothing`, `Several_files_show_a_count` (`3 files`, tooltip `Copied: a.pdf, b.xlsx, c.png`), `One_file_shows_its_name_cut_at_32`, `Cut_files_say_cut` (Kind Cut, tooltip `Cut: …`), `One_image_file_shows_like_an_image` (imageFileSize mengembalikan 1920×1080 → Kind Image, Detail `1920 × 1080`), `Sensitive_content_is_hidden` (Text `Copied (hidden)`, apa pun isinya), `Image_name_prefers_the_file_then_the_url_then_screenshot_then_image` (empat kasus Keputusan 2, nama dari URL `https://x/y/bali-sunset.jpg?w=1` → `bali-sunset.jpg`, dari `HTML Format` `<img src="https://x/a/b.png">` → `b.png`); coordinator: `Clipboard_snippet_holds_the_slot_for_3_seconds`, `A_new_copy_restarts_the_3_seconds`, `A_snippet_moves_media_to_the_bubble`, `Clicking_a_snippet_opens_the_clipboard_page`, `No_snippet_in_fullscreen`.
- [ ] **Step 2:** jalankan → gagal. **Step 3:** implementasi. **Step 4:** suite lulus.
- [ ] **Step 5: Commit** `feat(core): clipboard snippet texts, image names and the 3 s slot (F-ISL-17)`.

### Task 2: Pemantau clipboard (Platform) dan slot pulau (App)

**Files:**
- Create: `src/Gili.Platform/Clipboard/ClipboardMonitor.cs`
- Modify: `src/Gili.App/Island/IslandController.cs`, `IslandViewModel.cs`, `IslandWindow.xaml(.cs)`, `src/Gili.App/Resources/Icons.xaml` (+ `Icon.File`, `Icon.Scissors`, `Icon.Lock`; `Icon.Clipboard` sudah ada), `src/Gili.Core/Demo/DemoScript.cs` + tes demo, `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md` (Keputusan 1)

**Interfaces:**
- Consumes: `ClipboardContent`, `ClipboardSnippets.For`, `IslandCoordinator.ShowClipboard` (Task 1).
- Produces:
  - `ClipboardMonitor(IntPtr hwnd)` mendaftar `AddClipboardFormatListener` pada HWND pesan sendiri (`HwndSource` tak terlihat), `event Action<ClipboardContent, BitmapSource?>? Copied` di thread UI (thumbnail 40 px tinggi, frozen), `Dispose()` → `RemoveClipboardFormatListener`. Membaca: format sensitif dulu (berhenti di situ bila ada), `CF_UNICODETEXT`, `CF_HDROP` + `Preferred DropEffect` (2 = cut), gambar (`Clipboard.GetImage` → `TransformedBitmap` ke 40 px), `HTML Format`, `UniformResourceLocatorW`, pemilik (`GetClipboardOwner` → `GetWindowThreadProcessId` → nama proses).
  - `IslandController.Clipboard` (`ClipboardSnippet?` + thumbnail) dan `ClipboardChanged` event; `ShowDemoClipboard(ClipboardSnippet)` untuk demo.
  - `DemoAction` baru `DemoCopy(string Text)` di awal skenario (0,3 s), langkah lain bergeser +3,2 s; tes demo diperbarui (`Lasts_about_30_seconds` jadi 25–38 s, `Music_starts_first` → langkah kedua).

- [ ] **Step 1:** `ClipboardMonitor` sesuai Interfaces, dengan retry Review Focus 1; event pertama setelah start diabaikan (isi lama bukan copy baru).
- [ ] **Step 2:** `IslandController` → `ClipboardSnippets.For(content, path => ukuran header)` → `ShowClipboard()`; `IslandViewModel` mengekspos `ShowClipboardSlot`, `ClipGlyph`, `ClipBrush`, `ClipText`, `ClipDetail`, `ClipThumb`, `ClipTooltip`; `ShowClipboardSlot` = `Slot == ClipboardSnippet`.
- [ ] **Step 3:** `IslandWindow.xaml`: baris cuplikan di `CompactContent` (ikon/thumbnail, teks dengan elipsis maks. 260 px, detail abu), tooltip slot = `ClipTooltip`.
- [ ] **Step 4:** demo: `DemoCopy` → `IslandController.ShowDemoClipboard(ClipboardSnippets.For(new ClipboardContent(... Text ...)))`. PRD 6.3: thumbnail 36 × 20 cover (Keputusan 1).
- [ ] **Step 5:** build 0 warning, tes lulus. Manual: copy teks di Notepad, copy 3 file dan cut 1 file di Explorer, copy gambar dari browser, screenshot `Win+Shift+S`, copy dari KeePass/1Password bila ada (kalau tidak ada: catat), Ctrl+C beruntun; potret slot tiap kasus.
- [ ] **Step 6: Commit** `feat(island): clipboard snippets in the island slot (F-ISL-17)`.

### Task 3: Jaringan hari ini dan jeda polling saat layar mati

**Files:**
- Create: `src/Gili.Core/Modules/NetworkToday.cs`, `src/Gili.Core/Modules/PollingGate.cs`, `src/Gili.Platform/Power/DisplayMonitor.cs`
- Modify: `src/Gili.App/Bar/StatsService.cs`, `BarViewModel.cs`, `BarWindow.xaml.cs`, `src/Gili.App/Bar/RightService.cs`, `src/Gili.App/Island/MediaService.cs`, `src/Gili.App/App.xaml.cs`
- Test: `tests/Gili.Core.Tests/Modules/NetworkTodayTests.cs`, `PollingGateTests.cs`

**Interfaces:**
- Produces:
  - `NetworkToday.Add(double upBytes, double downBytes, DateTime localNow)`, `(double Up, double Down) Total`; berganti tanggal → mulai dari 0. `NetworkToday.Tooltip(string adapters)` → `<adapters>\nToday · ↑ <up>  ↓ <down>` (format satuan seperti `RateText` modul tanpa `/s`).
  - `PollingGate` dengan `SetScreenOn(bool)`, `SetFullscreen(bool)`, `bool ShouldPoll`, `event Action<bool>? Changed` (hanya saat nilainya berubah).
  - `DisplayMonitor(IntPtr hwnd)`: `RegisterPowerSettingNotification(GUID_CONSOLE_DISPLAY_STATE)`, `event Action<bool>? ScreenOnChanged` (0 = mati; 1 dan 2 (redup) = nyala); butuh hook `WM_POWERBROADCAST`/`PBT_POWERSETTINGCHANGE` di BarWindow.

- [ ] **Step 1: Tes yang gagal**: `Today_adds_up_each_second`, `A_new_day_starts_from_zero`, `Tooltip_reads_adapters_then_today` (`"Wi‑Fi\nToday · ↑ 1.2 MB  ↓ 45.0 MB"` untuk 1 258 291 / 47 185 920 byte, format persis mengikuti fungsi satuan yang ada), `Polling_stops_when_the_screen_is_off`, `Polling_resumes_only_when_screen_on_and_not_fullscreen`, `Changed_fires_once_per_change`.
- [ ] **Step 2:** gagal → implementasi → lulus.
- [ ] **Step 3:** App: `PollingGate.Changed` → `StatsService` stop/start, `ClockService.Seconds`, `RightService` Wi‑Fi timer, `MediaService` tick; jam langsung diperbarui saat nyala. Fullscreen yang sudah ada ikut lewat gate yang sama.
- [ ] **Step 4:** build, tes; manual: matikan layar (`powercfg` tidak perlu: `(Add-Type user32 SendMessage HWND_BROADCAST WM_SYSCOMMAND SC_MONITORPOWER 2)`), ukur CPU 30 detik saat layar mati (< 0,2%), nyalakan lagi dengan gerak mouse, cek jam dan statistik jalan.
- [ ] **Step 5: Commit** `feat(bar): today's network total in the tooltip; polling pauses while the screen is off (F-RIGHT-01, PRD 8.1)`.

### Task 4: Verifikasi

- [ ] **Step 1:** Release build 0 warning, tes, format; CPU diam < 1%.
- [ ] **Step 2:** catat di ledger.
