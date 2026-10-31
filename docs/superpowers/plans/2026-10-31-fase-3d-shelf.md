# Fase 3d: Shelf Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** File bisa diseret ke bar atau pulau, disimpan sebagai referensi di Shelf (maks. 8 item), dan diseret keluar lagi dengan Copy/Move (F-ISL-12, F-LEFT-04, F-ISL-07 Shelf).

**Architecture:** Core: `ShelfService` (aturan, teks, pembersihan) dengan `IShelfStore` dan probe file disuntikkan. Data: `ShelfStore` di tabel `shelf_items` yang sudah ada. App: drop masuk di `BarWindow`/`IslandWindow` lewat `IslandController`, lapisan drop zone, `ShelfPage`, ikon kiri dengan jumlah, dan langkah demo.

**Tech Stack:** .NET 10 WPF (OLE drag-and-drop), Microsoft.Data.Sqlite, xUnit.

**Spec:** [desain Fase 3d](../specs/2026-10-31-fase-3d-shelf-design.md), PRD 6.2, 6.3 "Shelf", 10, 11. Mockup: `.shsum`, `.shgrid`, `.ftile`, `.fi`, `.frm`, `.shempty`, `.dz`, `.dzi`, `addFiles`, `renderShelf`, `enterDrop`, `FILE_COLORS`, `fmtSize`.

## Global Constraints

- Teks persis: `Drop to keep on the Shelf`, `Files stay where they are until you drag them out`, `Shelf is full`, `Drag something out first`, `files on the shelf`, `1 file · 2.4 MB`, `3 files · 12.4 MB`, `12 files`, `Clear all`, `Shelf`, `Shelf · 1 file`, `Shelf · 3 files`, `Remove <name>`, `File moved or deleted`, `Folder`, `Drag files onto the island to hold them here while you find where they go.`
- Maks. 8 item; satu drop = satu item; ukuran desimal (`fmtSize`); jumlah = file (folder = 1).
- Efek drop ke Gili selalu `Copy` (atau `None` saat penuh), tidak pernah `Move`.
- Pemeriksaan file hanya saat halaman Shelf dibuka, setelah seret keluar, dan tiap 60 detik selama Shelf berisi.
- Drop zone: 420 × 140, radius 30 (sudah di `IslandLayout`); garis 1,5 px putus-putus, jarak 10, radius 22.

## Review Focus

1. **Drop file yang sama dua kali** atau drop berisi path ganda: setiap drop tetap satu item; path ganda di satu drop dibuang. Dites di Task 1 (`Duplicate_paths_in_one_drop_count_once`).
2. **Shelf penuh lalu satu item dihapus**: drop berikutnya diterima. Dites di Task 1 (`Full_shelf_refuses_then_accepts_after_remove`).
3. **Bundel dengan sebagian file hilang**: file hilang dibuang, ukuran dan nama (`2 files` → nama file tunggal bila tinggal satu) menyesuaikan. Dites di Task 1 (`Bundle_drops_missing_files_and_renames`).
4. **Database tidak tersedia** (store null): Shelf tetap bekerja di memori untuk sesi itu. Dites di Task 1 (`Works_without_a_store`).
5. **Seret dari aplikasi lain yang bukan file** (teks, URL): drop zone tidak muncul. Dicek di Task 3 (`DataFormats.FileDrop` saja) secara manual.

---

### Task 1: ShelfService dan ShelfStore (Core + Data)

**Files:** Create `src/Gili.Core/Shelf/ShelfModels.cs`, `src/Gili.Core/Shelf/ShelfService.cs`, `src/Gili.Core/Shelf/ShelfText.cs`, `src/Gili.Data/ShelfStore.cs`; Test Create `tests/Gili.Core.Tests/Shelf/ShelfServiceTests.cs`, `tests/Gili.Core.Tests/Shelf/ShelfTextTests.cs`, `tests/Gili.Core.Tests/Data/ShelfStoreTests.cs`.

**Interfaces (Produces):**
- `record ShelfItem(long Id, IReadOnlyList<string> Paths, string DisplayName, long SizeBytes, int SortOrder, DateTimeOffset AddedAt)` dengan `int FileCount => Paths.Count`, `bool IsBundle => Paths.Count > 1`.
- `readonly record struct FileFacts(bool IsFolder, long Size)`; probe `Func<string, FileFacts?>` (null = hilang).
- `interface IShelfStore { IReadOnlyList<ShelfItem> All(); ShelfItem Add(ShelfItem item); void Update(ShelfItem item); void Delete(long id); void Clear(); }`.
- `sealed class ShelfService(IShelfStore? store, Func<string, FileFacts?> probe)`: `const int MaxItems = 8`; `IReadOnlyList<ShelfItem> Items`; `bool IsFull`; `int FileCount`; `long TotalBytes`; `ShelfItem? Add(IReadOnlyList<string> paths, DateTimeOffset now)` (null bila penuh/kosong); `void Remove(long id)`; `void Clear()`; `bool Prune()` (true bila berubah); `bool PruneItem(long id)`; `void ShowDemo(ShelfItem? item)` (item demo di depan `Items`, tidak disimpan); `event Action? Changed`.
- `static class ShelfText`: `Size(long bytes)`, `Summary(int files, long bytes)`, `BarTooltip(int files)`, `Badge(ShelfItem item, bool isFolder) → (string Text, string Color)`, `Name(ShelfItem)`.

- [ ] **Step 1: Tes yang gagal**: `One_file_is_one_item_named_after_it`, `Several_files_are_a_bundle_named_by_count` (`12 files`), `Duplicate_paths_in_one_drop_count_once`, `Full_shelf_refuses_then_accepts_after_remove`, `Missing_paths_are_not_added`, `Prune_removes_gone_items`, `Bundle_drops_missing_files_and_renames`, `Clear_empties_and_saves`, `Items_survive_a_new_service` (fake store), `Works_without_a_store`, `Demo_item_shows_first_and_is_never_saved`, `Folder_counts_as_one_file_of_size_zero`; ShelfText: `Size_follows_fmtSize` (999 → `1 KB`, 340_000 → `340 KB`, 12_400_000 → `12.4 MB`, 2_400_000_000 → `2.4 GB`), `Summary_texts`, `Bar_tooltip_texts`, `Badge_colors_and_text` (`report.PDF` → `PDF`/`#FF6961`, `archive.tar.gz` → `GZ`/`#8E8E93`, `Makefile` → `FILE`, bundel 3 → `3`/`#D1D1D6`, `spreadsheet.xlsx` → `XLSX`/`#30D158`, `movie.webm1234` → 4 huruf pertama); ShelfStore: `Add_load_update_delete_and_clear` (file SQLite sementara, path JSON bolak-balik).
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Shelf"` → FAIL.
- [ ] **Step 3:** Implementasi. `paths` disimpan sebagai array JSON (`System.Text.Json`), `added_at` ISO 8601 UTC.
- [ ] **Step 4:** Tes → PASS; semua tes lulus.
- [ ] **Step 5: Commit** `feat(core): Shelf items, bundles, cleanup and texts, stored in shelf_items (F-ISL-12)`.

### Task 2: Demo dan koordinator (Core)

**Files:** Modify `src/Gili.Core/Demo/DemoScript.cs`; Test `tests/Gili.Core.Tests/Demo/DemoTests.cs`.

**Interfaces (Produces):** `record DemoShelf(string Name, long Size) : DemoAction`; skenario `Step(60.5, new DemoShelf("Quarterly report.pdf", 2_400_000))`, `Step(64.5, new DemoClose())`, `DemoEnd` di 65,5.

- [ ] **Step 1:** Tes `Demo_drops_a_file_on_the_shelf` (satu `DemoShelf`, sebelum `DemoEnd`, diikuti `DemoClose`), sesuaikan tes durasi bila ada.
- [ ] **Step 2:** Implementasi; tes lulus.
- [ ] **Step 3: Commit** bersama Task 1 bila kecil, atau `feat(core): demo drops a file on the Shelf (F-BAR-09)`.

### Task 3: Drop masuk, drop zone, ikon kiri (App)

**Files:** Modify `src/Gili.App/Island/IslandController.cs`, `IslandWindow.xaml`, `IslandWindow.xaml.cs`, `src/Gili.App/Bar/BarWindow.xaml(.cs)`, `src/Gili.App/Bar/LeftSide.xaml(.cs)`, `src/Gili.App/App.xaml.cs`, `src/Gili.App/DemoService.cs`, `src/Gili.App/Resources/Icons.xaml` (`Icon.Files`, `Icon.Folder`, `Icon.TrayDown`).

**Interfaces:**
- Consumes: `ShelfService` (Task 1), `IslandCoordinator.SetDropZone`.
- Produces: `IslandController.Shelf` (`ShelfService`), `DragEntered()`, `DragLeft()` (tertunda 150 ms), `bool DropFiles(IReadOnlyList<string> paths)` (menambah, membuka `IslandPage.Shelf`, menutup drop zone; false bila penuh), `bool DraggingOut { get; set; }`, `event Action? ShelfAdded` (untuk pantulan ikon). `IslandWindow.SetDropHot(bool)` dan tampilan penuh dari `Shelf.IsFull`.

- [ ] **Step 1:** Drop zone layer, `AllowDrop` + handler di kedua jendela (abaikan bila `DraggingOut` atau data tanpa `FileDrop`; efek `Copy`/`None`).
- [ ] **Step 2:** Ikon Shelf: teks jumlah, tooltip, pantulan; `App` membuat `ShelfService` dengan `ShelfStore` (atau null) dan probe `File`/`Directory`.
- [ ] **Step 3:** Demo: `DemoShelf` → drop zone hot 1,5 detik lalu `ShowDemo` + buka halaman Shelf; akhir demo `ShowDemo(null)`.
- [ ] **Step 4:** `dotnet build Gili.slnx` bersih; jalankan, cek drop zone lewat demo mode (UIA: tidak bisa membuka menu bar; demo dipicu lewat instance kedua? — tidak ada; cek dengan meminta pengguna) dan screenshot.
- [ ] **Step 5: Commit** `feat(island): drop files on the bar or island to keep them on the Shelf (F-ISL-12, F-LEFT-04)`.

### Task 4: Halaman Shelf dan seret keluar (App)

**Files:** Create `src/Gili.App/Island/ShelfPage.xaml(.cs)`, `ShelfViewModel.cs`; Modify `IslandWindow.xaml.cs` (`CreatePage`), `IslandViewModel.cs` (tab tanpa placeholder).

**Interfaces:** Consumes `ShelfService`, `ShelfText`, `IslandController.DraggingOut`. Produces `ShelfViewModel` (`CountText`, `Summary`, `HasItems`, `Tiles` dengan `Name`, `Tooltip`, `Badge`, `BadgeBrush`, `Glyph`, `Gone` (teks `File moved or deleted`), `RemoveCommand`, `ClearCommand`).

- [ ] **Step 1:** XAML sesuai Global Constraints dan mockup (ringkasan 150 px; grid 4 kolom × 2 baris 64 px, jarak 8; tile `#1C1C1E` radius 12, hover `#2C2C2E`; ikon 24 × 28; lencana 8 px bold; nama 12 px; tombol hapus 20 px di pojok kanan atas, tampil saat hover/fokus).
- [ ] **Step 2:** Seret keluar: ambang 4 px → cek path (`PruneItem`; hilang → `Gone` 2 detik lalu hapus) → `DraggingOut = true` → `DoDragDrop(FileDrop, Copy | Move)` → `DraggingOut = false` → `PruneItem`.
- [ ] **Step 3:** `Prune()` saat halaman Shelf dibuka dan timer 60 detik selama `Items.Count > 0` dan polling tidak dijeda.
- [ ] **Step 4:** Build bersih; jalankan dengan item di database (tambahkan lewat `ShelfStore` dari skrip uji di scratchpad, file sementara), buka halaman lewat UIA Invoke tab `Shelf`, screenshot; hapus satu item lewat UIA Invoke `Remove <name>`; hapus file sementara lalu buka ulang halaman → item hilang.
- [ ] **Step 5: Commit** `feat(island): Shelf page with tiles that drag out to copy or move (F-ISL-12, F-ISL-07)`.

### Task 5: PRD, mockup, verifikasi

- [ ] **Step 1:** PRD 8.1 baris Shelf; PRD 6.3 Shelf: drop di bar juga diterima, efek Copy, Move dideteksi dari file yang hilang; PRD 12 baris UIPI: tanpa pesan (Windows tidak memberi tahu). Mockup: drop di bar diterima. `node --check`.
- [ ] **Step 2:** Release 0 warning, tes lulus, format bersih; CPU diam dengan 3 item 60 detik; salinan GitHub dan MSIX: tambah item lewat database, halaman tampil.
- [ ] **Step 3:** Temuan dan hasil di desain; **Commit** `docs: phase 3d findings and verification`.
