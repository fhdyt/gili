# Fase 2f: Halaman Notes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Halaman Notes di pulau bekerja dengan data di gili.db: grid kartu masonry ala Keep dalam urutan pengguna, kartu yang tumbuh di tempat menjadi editor 440 × 360, New note, cari, 8 warna, 4 ukuran teks, seret untuk mengurutkan (juga Alt + panah), hapus dengan konfirmasi, dan simpan 0,5 detik setelah berhenti mengetik. Ketikan yang hilang saat laptop mati paksa paling banyak 1 detik.

**Architecture:** Aturan yang bisa dites ada di `Gili.Core.Notes`: model, teks kartu, tabel warna dan ukuran, geometri masonry dan bingkai editor, serta urutan. `Gili.Data.NoteStore` menyimpan tabel `notes` (skema v1 sudah lengkap). `Gili.App.Island.NoteService` memegang daftar dan satu timer simpan. `NotesPage` menggambar grid dan editor; editor digerakkan `Spring` per frame, sama seperti pulau. Kartu yang diseret sejak awal adalah jendela terpisah (`NoteDragWindow`) di atas pulau, supaya paket note mengambang (2h) tinggal menambah robek dan mendarat di luar pulau.

**Tech Stack:** .NET 10 WPF, Microsoft.Data.Sqlite, xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.3 (halaman 6 Notes, "Halaman Notes", "Ukuran teks note", "Tinggi pulau terbuka", "Menutup pulau"), 7.5 F-TOOL-08/09, 8.1 ("Kartu note tumbuh jadi editor", "Ukuran teks note"), 8.3 (radius, "Kartu note tumbuh jadi editor", "Editor menyusut kembali ke kartu", "Kartu note yang diseret", tinggi pulau di Notes), 10 (`notes`). Mockup: CSS `.pg-note`, `.ntop`, `.ncard`, `.ned`, `.nedin`, `.nsw`, `.nfs`, `.nfoot`, `.nsave`, `.ndel`, `.nnew`; JS `layoutNotes`, `edTarget`, `openEditor`, `shutEditor`, `closeEditor`, `newNote`, `moveNote`, `beginDrag`, `endNoteDrag`, `setNoteSize`, `sizeKeys`, `NOTE_COLORS`, `NOTE_SIZES`.

## Global Constraints

- Teks persis (bahasa Inggris, dari mockup): kolom `Search notes`, tombol `New note`, placeholder editor `Write something. The first line becomes the title.`, status `Saving…` / `Saved` / `Saved · 19:20`, tombol `Delete` → `Click again to delete` (3 detik), `Done`, kartu kosong `Empty note`, cari tanpa hasil `No notes match.`, nama aksesibel kartu = judul atau `Untitled note`, bulatan `Color: Red` (tooltip `Red`), ukuran `Extra small` / `Small` / `Medium` / `Large`, grid `Notes. Drag a note, or press Alt and an arrow key, to reorder.`, editor `Edit note`, area teks `Note text`.
- Warna note: Default `#1C1C1E`, Red `#5C1F26`, Orange `#643013`, Yellow `#5C470C`, Green `#1D4A31`, Teal `#0F4C4B`, Blue `#1C3C61`, Purple `#42265A`. Kartu berwarna: isi putih 88%, waktu putih 72%; Default: isi `#C7C7CC`, waktu `#8E8E93`.
- Ukuran teks (editor / kartu judul / kartu isi / huruf "A" di pemilih): Extra small 12 / 12 / 11 / 10; Small 13 / 12,5 / 11,5 / 11,5; Medium 14 / 13 / 12 / 13 (default); Large 16 / 14,5 / 13,5 / 15. Note mengambang (11/12/13/15) ikut tabel Core, tetapi baru dipakai di 2h.
- Grid: lebar kolom minimal 140 px, jarak 8 px, kolom = floor((lebar + 8) / 148), urutan pengguna ke kolom terpendek. Kartu: padding 10 × 12, radius 12, garis dalam putih 5%; judul 13 semibold, margin bawah 4; isi maks. 8 baris, tinggi baris 1,45; waktu 11 px, margin atas 8.
- Editor 440 × 360 (dibatasi area grid yang terlihat), radius 12 → 16, padding 12 14 10, teks tinggi baris 1,6; bulatan 20 px jarak 7 (terpilih: cincin 2 px hitam + 2 px `#F5F5F7`); pemilih ukuran kapsul radius 9 latar putih 8% padding 2, tombol 26 × 22 radius 7, terpilih putih 18%; kaki 12 px `#8E8E93` margin atas 6, titik 6 px hijau `#30D158` (oranye `#FF9F0A` berkedip saat menyimpan); Delete padding 4 10 radius 8 (hover latar `#1C1C1E` teks `#FF453A`; konfirmasi latar `#FF453A` teks putih); Done pill kecil 30 px putih teks hitam.
- Baris atas: kolom cari 32 px radius 10 padding 0 12 latar `#1C1C1E`; New note 32 px radius 10 padding 0 12 latar `#F5F5F7` teks hitam 13 semibold, ikon plus 16 px, jarak 6; jarak antar-elemen 8, ke grid 10. Halaman padding 2 24 4.
- Gerak: editor tumbuh dengan `SpringParameters.Open` (0,82 / 0,42), isi muncul 180 ms mulai 140 ms, kartu lain meredup ke 30% dalam 220 ms; editor menyusut dengan `SpringParameters.Close` (1,0 / 0,32), isi hilang 120 ms, note kosong atau dihapus memudar 160 ms mulai 160 ms; kartu bergeser 320 ms `BezierEase.Out`; kartu diseret skala 1,03 dengan bayangan terangkat, mendarat 340 ms cubic-bezier(.34, 1.3, .64, 1). Animation effects mati: semua jadi cross-fade 100 ms.
- Simpan 0,5 detik setelah ketikan terakhir, juga saat editor menutup, saat pulau menutup, dan saat app keluar. Tidak ada polling: satu `DispatcherTimer` sekali jalan per jeda ketik.

## Keputusan yang dibuat di rencana ini

1. **Cakupan.** 2f hanya halaman Notes. Note mengambang (F-TOOL-12) dan ekspresi Gili yang bergantung padanya (senang, mengikuti note) menjadi paket 2h; jendela Tools tetap 2g seperti di rencana 2b/2e. Selama 2f, tombol Float on top dan grip di tepi atas editor belum tampil, dan kartu yang dibawa keluar pulau tetap seretan pengurutan biasa (dilepas di luar = mendarat kembali ke slotnya).
2. **F-TOOL-09.** `Database` beralih ke `PRAGMA synchronous=FULL`. Dengan `NORMAL` di mode WAL, tulisan sejak checkpoint terakhir bisa hilang saat listrik putus. `FULL` menambah satu fsync per commit; penulis paling sering adalah note (paling banyak satu commit per 0,5 detik), jadi biayanya kecil. Satu koneksi, jadi pragma berlaku untuk semua tabel.
3. **Seret = jendela sejak terangkat** (PRD 6.3 "Satu benda"). `NoteDragWindow` (transparan, topmost, tidak mengaktifkan, tembus klik) dibuat saat pointer bergeser 6 px dan dibuang setelah mendarat. Pulau tetap memegang mouse capture, jadi `Deactivated` tidak menutupnya.
4. **Note kosong dibuang saat editor menutup**, baik note baru maupun note lama yang isinya dihapus habis (perilaku mockup `closeEditor`; PRD hanya menyebut note baru, disamakan di Task 3). Note baru langsung ditulis ke database (body kosong) supaya id-nya tetap; note kosong yang tertinggal karena crash dibuang saat app mulai.
5. **Urutan.** Note baru mendapat `sort_order` = MIN − 1 (paling atas). Pengurutan menulis ulang `sort_order` 0..n−1 dalam satu transaksi. Mengedit tidak mengubah urutan.
6. **Waktu di kartu** (mockup `19:20`, `Yesterday`, `Mon`, `12 Oct`): hari ini = jam (mengikuti 12-hour clock), kemarin = `Yesterday`, 2–6 hari lalu = nama hari singkat, tahun yang sama = `12 Oct`, tahun lain = `12 Oct 2025`. Ditambahkan ke PRD di Task 3.
7. **Belum ada note sama sekali**: grid menampilkan `No notes yet.` (mockup menampilkan `No notes match.` untuk dua keadaan itu). Ditambahkan ke mockup dan PRD di Task 3.
8. **`hint()` mockup tidak dibawa.** `Note deleted` diganti gerak memudar dan merapat. Selama pencarian aktif, kartu tidak terangkat (klik tetap membuka). Mockup diubah sama di Task 3.
9. **Hover kartu.** WPF tidak punya `filter: brightness(1.15)`, jadi lapisan putih 6% di atas kartu saat hover dipakai sebagai gantinya (dibandingkan lewat potret di Task 5).
10. **Roda mouse.** Selama grid (atau area teks editor) bisa di-scroll, roda di atasnya menggulir isi itu dan tidak pindah halaman. Aturan ini berlaku untuk semua halaman: `OnPagesWheel` tidak pindah halaman kalau `ScrollViewer` di bawah pointer masih bisa bergerak ke arah itu.
11. **Esc.** Di editor: menutup editor, fokus ke kartunya; Esc berikutnya menutup pulau (PRD). Di kolom cari: mengosongkan teks; kalau sudah kosong, menutup pulau.
12. **Pulau menutup saat editor terbuka**: isi disimpan, editor tertutup tanpa animasi (note kosong dibuang), dan pulau dibuka lagi dengan grid saja (PRD 6.4: ikon Notes membuka grid dengan editor tertutup).

## Review Focus

1. **Mengetik lalu pulau menutup dalam 0,5 detik** (klik di luar, Esc dua kali, jauh 3 detik tidak berlaku saat fokus): ketikan terakhir tersimpan. Dites di Task 4 (`Closing_flushes_a_pending_save`).
2. **Note yang semua teksnya dihapus lalu ditutup**: dibuang, grid merapat, tidak ada kartu `Empty note` yang tertinggal. Dites di Task 4 (`A_note_emptied_and_closed_is_discarded`).
3. **Note sangat panjang atau satu kata tanpa spasi 500 karakter**: kartu tetap 8 baris isi, teks dibungkus di tengah kata, kolom tidak melebar. Manual di Task 5, kasus `Long_words_wrap` dengan potret.
4. **Grid lebih tinggi dari layar** (30 note): pulau berhenti di tinggi maksimal, hanya grid yang di-scroll, editor tetap di dalam area grid yang terlihat termasuk untuk kartu di paling bawah. Dites di Task 1 (`Editor_stays_inside_the_visible_grid`), manual di Task 5.
5. **Mengurutkan di tepi** (Alt + panah kiri di kartu pertama, Alt + panah kanan di kartu terakhir, seret ke slot sendiri): tidak ada perubahan, tidak ada tulisan ke database. Dites di Task 1 (`Moving_past_either_end_changes_nothing`).

---

### Task 1: Model, teks, gaya, geometri, dan urutan (Core)

**Files:** Create `src/Gili.Core/Notes/Note.cs`, `NoteText.cs`, `NoteStyle.cs`, `NoteGrid.cs`, `NoteOrder.cs`; Test `tests/Gili.Core.Tests/Notes/NoteTextTests.cs`, `NoteStyleTests.cs`, `NoteGridTests.cs`, `NoteOrderTests.cs`.

**Interfaces (Produces):**
- `public enum NoteTextSize { ExtraSmall, Small, Medium, Large }` (nilai 0–3 = kolom `text_size`).
- `public sealed record Note(long Id, string Body, int Color, NoteTextSize Size, int SortOrder, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)`.
- `NoteText`: `Title(string body) → string` (baris pertama, trim), `Rest(string body) → string` (baris berikutnya, trim), `IsBlank(string body) → bool`, `Label(string body) → string` (judul atau `Untitled note`), `Matches(string body, string query) → bool` (tanpa beda huruf besar/kecil, query kosong/spasi = true), `Edited(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone, bool use24) → string` (Keputusan 6, jam lewat `ClockText.Time`), `Saved(DateTimeOffset at, TimeZoneInfo zone, bool use24) → string` (`Saved · 19:20`), konstanta `Empty = "Empty note"`, `NoMatch = "No notes match."`, `NoNotes = "No notes yet."`.
- `NoteStyle`: `record NoteColor(string Name, uint Rgb)`, `IReadOnlyList<NoteColor> Colors` (8, urutan PRD), `record NoteSize(string Name, double Editor, double Floating, double CardTitle, double CardBody, double Glyph)`, `NoteSize Size(NoteTextSize)`, `NoteTextSize Step(NoteTextSize, int delta)` (dijepit 0–3), `NoteTextSize Default = Medium`, `int ClampColor(int)` (di luar 0–7 → 0).
- `public readonly record struct NoteRect(double X, double Y, double Width, double Height)`; `enum GrowX { Left, Center, Right }`, `enum GrowY { Down, Up }` (arah tumbuh).
- `NoteGrid`: konstanta `MinColumn = 140`, `Gap = 8`, `EditorWidth = 440`, `EditorHeight = 360`; `int Columns(double width)`; `double ColumnWidth(double width)`; `NoteLayout Place(IReadOnlyList<double> heights, double width)` dengan `record NoteLayout(IReadOnlyList<NoteRect> Slots, double Height)` (tinggi = kolom tertinggi tanpa jarak di bawah kartu terakhir; daftar kosong = 0); `int? HitTest(IReadOnlyList<NoteRect> slots, double x, double y, int except)`; `(NoteRect Frame, GrowX X, GrowY Y) Editor(NoteRect from, NoteRect visibleGrid)` (algoritma mockup `edTarget`: lebar/tinggi = min(440/360, grid); sepertiga kiri/tengah/kanan dari pusat kartu; ke bawah kalau muat, kalau tidak tepi bawah tetap; lalu dijepit ke grid. `GrowY.Up` bila tepi atas frame berada di atas tepi atas kartu, supaya isi menempel ke bawah seperti PRD; mockup selalu menempel ke atas); `NoteRect NewCard(NoteRect visibleGrid, double columnWidth)` (kanan atas, tinggi 64).
- `NoteOrder`: `IReadOnlyList<long> Move(IReadOnlyList<long> order, long id, int toIndex)` (indeks sekarang, di luar rentang, atau id tidak ada → daftar yang sama, `ReferenceEquals`); `long? Neighbor(IReadOnlyList<long> visible, long id, int delta)` (Alt + panah; kiri/atas = −1).

- [ ] **Step 1: Tes gagal.** Termasuk:
  - `Columns_follow_the_width`: `Columns(512) == 3`, `Columns(287) == 1`, `Columns(288) == 2`, `Columns(0) == 1`; `ColumnWidth(512)` ≈ 165,333.
  - `Cards_go_to_the_shortest_column`: tinggi `[100, 40, 40, 30]` di 512 → slot ke-4 di kolom 2 (`X` ≈ 173,33, `Y == 48`); `Height == 100`.
  - `Edited_reads_like_the_mockup`: now = Selasa 27 Okt 2026 20:00 WIB; 19:20 hari itu → `19:20` (`7:20 PM` bila 12 jam); 26 Okt → `Yesterday`; 26 Okt 23:59 saat now 27 Okt 00:01 → `Yesterday`; 21 Okt → `Wed`; 12 Okt → `12 Oct`; 12 Okt 2025 → `12 Oct 2025`.
  - `Editor_grows_toward_the_free_side`: grid (0,0,512,600); kartu kiri (0,0,165,80) → X Left, frame.X 0; kartu tengah (173,0,165,80) → Center, frame.X == 35,5; kartu kanan (346,0,165,80) → Right, frame.X + 440 == 511.
  - `Editor_stays_inside_the_visible_grid`: grid (0,0,512,300) dan kartu di (0,250,165,50) → tinggi 300, Y 0, `GrowY.Up`; kartu (0,0,165,50) di grid (0,0,400,600) → lebar 400, `GrowY.Down`.
  - `Moving_past_either_end_changes_nothing`: `Neighbor([1,2,3], 1, -1) == null`, `Neighbor([1,2,3], 3, 1) == null`, `ReferenceEquals(Move(order, 2, 1), order)` dan `ReferenceEquals(Move(order, 2, 5), order)` untuk `order = [1,2,3]`; `Move([1,2,3], 1, 2)` == `[2,3,1]`.
  - `Search_ignores_case_and_blank_queries`, `Blank_bodies_are_blank` (`"  \n \t"`), `Title_is_the_first_line`, `Untitled_note_label`, `Size_steps_stop_at_the_ends` (`Step(Large, 1) == Large`, `Step(ExtraSmall, -1) == ExtraSmall`), `Size_table_matches_the_prd` (Medium 14/13/13/12/13; Large 16/15/14,5/13,5/15), `Unknown_color_is_default` (`ClampColor(9) == 0`).
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Notes"` → FAIL (tipe belum ada).
- [ ] **Step 3:** Implementasi sesuai Interfaces.
- [ ] **Step 4:** Tes yang sama → PASS.
- [ ] **Step 5:** Commit `feat(core): note model, card texts, colours, sizes, masonry and editor geometry (F-TOOL-08)`.

### Task 2: Penyimpanan note dan sync penuh (Data)

**Files:** Create `src/Gili.Data/NoteStore.cs`; Modify `src/Gili.Data/Database.cs` (`synchronous=FULL`, ringkasan kelas menyebut alasannya); Test `tests/Gili.Core.Tests/Data/NoteStoreTests.cs`, `DatabaseTests.cs`.

**Interfaces:**
- Consumes: `Note`, `NoteTextSize`, `NoteStyle.ClampColor` (Task 1).
- Produces: `NoteStore(Database db)`: `IReadOnlyList<Note> All()` (urut `sort_order, id`), `Note Add(Note note)` (`sort_order` = COALESCE(MIN, 1) − 1, mengembalikan id dan urutan), `void Update(Note note)` (hanya `body`, `color`, `text_size`, `updated_at`; tidak menyentuh `sort_order` maupun `float_*`), `void Reorder(IReadOnlyList<long> ids)` (satu transaksi, 0..n−1), `void Delete(long id)`, `int DeleteBlank()` (body kosong/spasi saja; dipanggil saat mulai). Waktu UTC `o`, seperti `TodoStore`.

- [ ] **Step 1: Tes gagal:** `Added_note_goes_first_and_survives_a_restart` (dua note, yang kedua ditambah lebih dulu di `All()`, body/color/size/waktu sama setelah dibuka ulang), `Update_keeps_order_and_float_columns` (isi `float_x` lewat SQL langsung, `Update`, `float_x` dan `sort_order` tetap), `Reorder_writes_the_new_order`, `Delete_removes_it`, `Blank_notes_are_dropped_on_start` (body `""` dan `" \n "` terhapus, `"a"` tetap, hasil 2), `Database_syncs_every_commit` (`PRAGMA synchronous` == 2).
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~NoteStoreTests|FullyQualifiedName~DatabaseTests"` → FAIL.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** Tes yang sama → PASS, lalu seluruh `dotnet test tests/Gili.Core.Tests` → PASS.
- [ ] **Step 5:** Commit `feat(data): notes kept in gili.db, synced on every commit (F-TOOL-08, F-TOOL-09)`.

### Task 3: Mockup dan PRD

- [ ] Mockup: `No notes yet.` untuk grid tanpa note (cari tanpa hasil tetap `No notes match.`); hapus `hint('Note deleted')` dan `hint('Clear the search to reorder notes')` (selama cari, kartu tidak terangkat); waktu kartu memakai aturan Keputusan 6 (contoh data tetap). `node --check` pada skrip yang diekstrak dan potret `notes-page` lewat `scripts/mockup-shot.ps1`.
- [ ] PRD 6.3 "Halaman Notes": teks kosong `No notes yet.`, format waktu kartu, note lama yang dikosongkan juga dibuang saat ditutup, selama cari kartu tidak bisa diangkat; F-TOOL-09 menyebut `synchronous=FULL` di 10.
- [ ] Commit `docs: empty notes, card times and the Notes search rules in the mockup and PRD`.

### Task 4: NoteService dan halaman Notes (grid, cari, editor, warna, ukuran, hapus)

**Files:** Create `src/Gili.Core/Notes/NoteSession.cs`, `tests/Gili.Core.Tests/Notes/NoteSessionTests.cs`, `src/Gili.App/Island/NoteService.cs`, `NotesViewModel.cs`, `NotesPage.xaml(.cs)`, `NoteCardView.xaml(.cs)`; Modify `IslandController.cs` (+ `NoteService Notes`), `IslandWindow.xaml.cs` (`CreatePage`, `PageContentChanged(IslandPage.Notes)`, away-timer, Esc kedua, editor ditutup saat pulau keluar dari Expanded), `IslandViewModel.cs` (tab Notes tanpa placeholder), `App.xaml.cs` (buat `NoteService`, `DeleteBlank()` saat mulai, `Flush()` di jalur keluar yang sama dengan `_todos?.Stop()`).

**Interfaces:**
- Consumes: Task 1 dan Task 2.
- Produces: `NoteService(NoteStore? store, Func<DateTimeOffset>? clock = null)`: `IReadOnlyList<Note> Notes`, `event Action? Changed`, `event Action<bool, DateTimeOffset?>? SaveState` (sibuk, waktu simpan terakhir), `Note Create()` (paling atas, body kosong, Medium, Default), `void Edit(long id, string body)` (memori langsung, simpan 0,5 detik kemudian), `void SetColor(long id, int color)`, `void SetSize(long id, NoteTextSize size)` (langsung tersimpan), `void Reorder(IReadOnlyList<long> ids)` (tanpa tulisan bila urutan sama), `void Delete(long id)`, `bool Close(long id)` (flush; true = dibuang karena kosong), `void Flush()`. Tanpa store (database gagal dibuka) semuanya bekerja di memori, seperti `TodoService`.
- `NoteSession` (Core, tanpa timer): `void Edit(long id, string body, DateTimeOffset now)`, `DateTimeOffset? DueAt` (ketikan terakhir + 500 ms), `IReadOnlyList<(long Id, string Body)> Take()` (semua yang tertunda, lalu kosong), `static bool Discards(string body)` (= `NoteText.IsBlank`).
- `NotesViewModel`: `event Action? ContentChanged`, `bool IsEditing`, `bool IsDragging` (Task 5), `void CloseEditorNow()`.
- `NotesPage` melaporkan `ContentChanged`; tinggi isi = 6 + 42 + max(tinggi grid, editor terbuka ? 360 : 0) lewat `MeasureOverride`, jadi `IslandWindow.PageHeight()` tidak berubah.

- [ ] **Step 1: Tes gagal** di `tests/Gili.Core.Tests/Notes/NoteSessionTests.cs`. Aturan jeda ketik ada di `NoteSession`; `NoteService` hanya menambah `DispatcherTimer` ke `DueAt` dan store. Tes: `Typing_waits_half_a_second_after_the_last_key` (edit di t=0 dan t=0,3 → `DueAt` == t=0,8), `Closing_flushes_a_pending_save` (`Take()` sebelum `DueAt` mengembalikan isi terakhir, `Take()` berikutnya kosong), `A_note_emptied_and_closed_is_discarded` (`Discards(" 
")` true, `Discards("a")` false).
- [ ] **Step 2:** `--filter "FullyQualifiedName~NoteSessionTests"` → FAIL; implementasi `NoteSession`; → PASS.
- [ ] **Step 3:** `NoteService` + penyambungan di `App` dan `IslandController`.
- [ ] **Step 4:** `NoteCardView` (Global Constraints: kartu; lapisan hover 6%; `Opacity` 0,3 saat redup; `AutomationProperties.Name` = `NoteText.Label`; fokus Tab, Enter/Space membuka).
- [ ] **Step 5:** `NotesPage`: baris atas, `ScrollViewer` grid (hanya bisa di-scroll setelah pulau mencapai tinggi maksimal; scrollbar tipis), `Canvas` kartu dengan `TranslateTransform` per kartu (bergeser 320 ms), ukur tinggi kartu di lebar kolom lalu `NoteGrid.Place`. Panel editor di atas grid: bingkai dari 4 `Spring` (x, y, w, h) + radius, di-step lewat `CompositionTarget.Rendering` hanya selama bergerak; isi ditata sekali di ukuran akhir dan ditempel ke sudut asal tumbuh (`GrowX`/`GrowY`); membuka saat bingkai masih menyusut melanjutkan dari posisi yang sedang tampil. Slot kartu yang dibuka kosong; setelah menyusut, editor diganti kartunya di frame yang sama. New note: kosongkan cari, scroll ke atas, `Create()`, editor tumbuh dari `NoteGrid.NewCard`. Delete dua klik (3 detik), Done, Esc, klik di grid yang redup = tutup. Bulatan warna (pola `aria-pressed`: `AutomationProperties` + `ToggleButton`), pemilih ukuran sebagai radiogroup (panah kiri/kanan), `Ctrl+=` / `Ctrl+-` / `Ctrl+0` di area teks, kaki `Saving…` / `Saved · HH:mm`. Cari menutup editor lalu menyaring. Animation effects mati: semua perpindahan jadi cross-fade 100 ms.
- [ ] **Step 6:** `IslandWindow`: halaman Notes, away-timer tidak menutup selama editor fokus (sudah: `TextBox`) atau selama seretan (Task 5), Esc di halaman Notes mengikuti Keputusan 11, `NotesViewModel.CloseEditorNow()` saat pulau keluar dari Expanded (Keputusan 12).
- [ ] **Step 7: Manual** (`dotnet run --project src/Gili.App`): note baru → ketik → tutup pulau dalam < 0,5 detik → buka lagi, isi ada; tutup app lewat `Gili.exe --quit` saat baru mengetik → isi ada; New note tanpa isi lalu Done → hilang tanpa sisa; warna dan ukuran tersimpan setelah restart; cari menyaring dan `No notes match.`; tinggi pulau mengikuti kolom tertinggi dan membesar dulu saat editor dibuka; editor dari kartu kiri/tengah/kanan dan kartu paling bawah tumbuh ke arah yang benar.
- [ ] **Step 8:** `dotnet build Gili.slnx` 0 warning, tes PASS, `dotnet format Gili.slnx --verify-no-changes`; commit `feat(island): Notes page with a Keep-style grid, an editor grown from its card, colours, sizes, search and delete (F-ISL-07, F-TOOL-08, F-TOOL-09)`.

### Task 5: Seret untuk mengurutkan, Alt + panah, dan roda mouse

**Files:** Create `src/Gili.App/Island/NoteDragWindow.cs`; Modify `NotesPage.xaml.cs`, `NotesViewModel.cs`, `IslandWindow.xaml.cs` (`OnPagesWheel` Keputusan 10; away-timer selama `NotesViewModel.IsDragging`).

**Interfaces:**
- Consumes: `NoteGrid.HitTest`, `NoteOrder.Move`/`Neighbor` (Task 1), `NoteService.Reorder` (Task 4), `NoteCardView` (Task 4), `WindowStyles.MakeNonActivatingToolWindow` (Platform).
- Produces: `NoteDragWindow(Note note, double width, double height)`: `void MoveTo(Point screenTopLeft)`, `void SetLifted(bool lifted)` (skala 1,03 + bayangan dalam 220 ms), `Task LandAsync(Rect screenSlot)` (340 ms cubic-bezier .34, 1.3, .64, 1; tanpa animasi saat Animation effects mati). Jendela transparan, topmost, `WS_EX_TRANSPARENT`, margin bayangan di sekelilingnya, satu per seretan. Di 2h jendela ini menjadi note mengambang.

- [ ] **Step 1:** Seret: tekan di kartu, setelah bergeser 6 px (dan cari kosong, editor tertutup) buat `NoteDragWindow` di posisi kartu, sembunyikan kartunya di frame yang sama, dan ikuti pointer 1:1 dari titik pegang (mouse capture di halaman). Saat pointer di atas slot kartu lain → `NoteOrder.Move`, kartu lain bergeser 320 ms, lalu jeda 260 ms sebelum pertukaran berikutnya. Lepas di mana pun → `LandAsync` ke slot, kartu tampil lagi di frame yang sama, jendela ditutup, `Reorder` sekali. Tekan tanpa bergeser 6 px = buka editor.
- [ ] **Step 2:** Alt + panah pada kartu fokus → `NoteOrder.Neighbor` di daftar yang terlihat, `Reorder`, fokus tetap di kartu yang sama setelah bergeser. `Key.System` sudah dilewati `IslandWindow.OnPreviewKeyDown`; pastikan Alt tidak membuka menu sistem (`e.Handled = true`).
- [ ] **Step 3:** `OnPagesWheel`: cari `ScrollViewer` terdekat dari `e.OriginalSource` yang masih bisa bergerak ke arah roda (`VerticalOffset` > 0 untuk naik, < `ScrollableHeight` untuk turun) → biarkan ia menggulir, jangan pindah halaman.
- [ ] **Step 4: Manual:** seret kartu pertama ke posisi ketiga lalu restart → urutan tetap; seret melewati tepi pulau lalu lepas → mendarat kembali ke slot; jendela seret tidak pernah terpotong tepi pulau; Alt + panah di kedua ujung tidak berbuat apa-apa; 30 note → roda menggulir grid, di halaman lain roda tetap pindah halaman; pulau tidak menutup sendiri selama seretan; `Long_words_wrap`: note berisi 500 karakter tanpa spasi.
- [ ] **Step 5:** Potret halaman Notes (grid, editor terbuka dari kartu kiri, Delete konfirmasi, seretan) lewat PrintWindow jendela Gili saja dan dibandingkan dengan potret mockup dari Task 3; perbedaan ukuran/warna/jarak diperbaiki di sini.
- [ ] **Step 6:** Build, tes, format; commit `feat(island): drag notes to reorder from a window above the island, Alt+arrows, and wheel scrolling in long pages (F-TOOL-08)`.

### Task 6: Verifikasi

- [ ] Release build 0 warning, `dotnet test`, `dotnet format --verify-no-changes`; `pwsh scripts/publish.ps1` dan `pwsh scripts/package.ps1 -Install`, lalu cek di kedua bentuk bahwa note tersimpan di folder data masing-masing (`%LOCALAPPDATA%\Gili\gili.db` dan `LocalState`).
- [ ] CPU diam < 1% dengan halaman Notes terbuka tanpa input; editor tumbuh/menyusut < 5% selama animasi (PRD 8.1); RAM tidak naik setelah 20 kali buka/tutup editor dan 10 seretan (jendela seret dibuang).
- [ ] Catat hasil dan sisa perbedaan dengan mockup di ledger, laporkan ke pengguna.
