# Fase 2h: Note Mengambang Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Note bisa keluar dari pulau menjadi jendela kecil always on top yang langsung bisa diketik (F-TOOL-12): diseret dari kartu atau dari grip editor melewati tepi pulau, atau lewat tombol Float on top; dipindah dan diubah ukurannya di dalam area kerja; dikembalikan dengan Back to island; tahan restart; tersembunyi saat fullscreen; maksimal 6. Gili senang saat note masuk ke pulau yang tertutup dan matanya mengikuti note yang bergerak (F-ISL-24 bagian 2h).

**Architecture:** Aturan geometri (posisi awal, jepit ke area kerja, resize dari 8 arah, titik lepas, batas 6) ada di `Gili.Core.Notes.NoteFloat` dan dites. `Note` mendapat `Float` (persegi DIP atau null), `NoteStore` membaca dan menulis `float_*` (kolom sudah ada sejak skema v1). `NoteService` memegang daftar note yang mengambang; grid hanya menampilkan note yang tidak mengambang. `FloatingNotes` (App) membuat satu `FloatingNoteWindow` per note yang mengambang, memulihkannya saat start, menyembunyikannya saat fullscreen, dan menjalankan Back to island. Seretan dari halaman Notes tetap memakai `NoteDragWindow`; dilepas di luar pulau, jendela itu tumbuh ke 260 × 220 dengan pegas lalu jendela note mengambil alih di frame yang sama.

**Tech Stack:** .NET 10 WPF, Microsoft.Data.Sqlite, xUnit.

**Spec:** PRD 6.3 ("Note mengambang", "Ekspresi Gili" baris Senang dan "Arah pandang"), 7.5 F-TOOL-12, F-ISL-24, 8.1 baris "Note mengambang", 8.3 baris "Kartu (atau editor) dilepas jadi note mengambang", 10 (`notes.float_*`). Mockup: CSS `.fnote`, `.fhd`, `.fr`, `.fnote.proxy`, `.nedgrip`, `.npop`, `.island.gulp`, `.geyes.x-happy`; JS `makeFloat`, `floatNote`, `focusFloat`, `returnFloat`, `defaultFloat`, `clampF`, `startTear`, `cancelTear`, `dropBack`, `landTear`, `beginDrag`, `dragPlace`, `dragStep`, `gWatch`, `gMood`.

## Global Constraints

- Teks persis: tombol editor `Float on top` (tooltip dan nama aksesibel), grip editor tooltip `Drag out of the island to float on top`, tombol jendela `Back to island`, header jendela tooltip `Drag to move`, nama aksesibel jendela = judul note atau `Floating note`, area teks `Note text` dengan placeholder `Write something.`, pesan batas `Up to 6 floating notes`.
- Jendela note: awal 260 × 220, batas 200 × 140 sampai 520 × 640, radius 12, latar warna note, garis tepi putih 10% (1 px di luar note), bayangan 0 18 40 −12 hitam 70%; saat dipindah 0 28 56 −14 hitam 80% dengan garis putih 14%. Header 28 px dengan ikon grip 16 px putih 35% (diputar 90°); tombol Back to island 20 × 20 di kanan atas (4 px), radius 8, ikon 12 px putih 55%, tampil saat hover atau fokus, hover latar putih 12% ikon putih. Area teks padding 0 14 12, tinggi baris 1,55, ukuran dari kolom "Note mengambang" (11/12/13/15), teks putih 92% kalau berwarna. Area tarik resize 6 px di tepi, 12 × 12 di sudut, tanpa tanda.
- Posisi Float on top: 24 px dari tepi kanan area kerja dan dari bar, note berikutnya bergeser 24 px ke kiri bawah.
- Gerak: dilepas di luar → ukuran pegas `SpringParameters.Open` per frame, posisi dari ukuran supaya titik lepas tetap, radius 12 (atau 16 → 12 dari editor) mengikuti progres, skala 1,03 → 1 dalam 220 ms ease-out, isi cross-fade 160 ms; memantul masuk area kerja dengan pegas 0,82 / 0,34; Back to island 440 ms cubic-bezier(.55, 0, .75, .2), isi memudar, mengecil ke kapsul hitam setinggi pulau di tengah pulau; pulau menggembung (skala 1,07 × 1,18 di 40%, 420 ms pegas); jendela lahir dari Float on top skala 0,9 → 1 + opacity 420 ms pegas; fokus = "ping" skala 1,035. Animation effects mati: semua jadi cross-fade 100 ms.
- RAM ≤ +5 MB per note mengambang, CPU 0% saat diam (PRD 8.1). Tidak ada timer yang jalan selama note diam.

## Keputusan yang dibuat di rencana ini

1. **Cakupan.** F-TOOL-12 lengkap, ditambah ekspresi Senang dan arah pandang ke note (F-ISL-24 bagian note). Ekspresi Kaget dan Fokus tidak bergantung pada note dan tetap menjadi paket sendiri.
2. **Dua jendela per note mengambang.** Jendela layered menangkap klik di setiap piksel yang alpha-nya di atas nol, dan `HTTRANSPARENT` hanya meneruskan klik ke jendela di thread yang sama, jadi bayangan di jendela note akan menelan klik ke aplikasi di bawahnya. Karena itu note digambar di jendela seukuran note (sudut transparan tembus klik dengan sendirinya), dan bayangan serta garis tepinya di jendela kedua yang tembus klik (`WS_EX_TRANSPARENT`), selalu diletakkan tepat di bawah jendela note dalam urutan z. Anggaran 8.1 tetap per note.
3. **Serah terima saat mendarat.** Selama seretan dan tumbuh, `NoteDragWindow` (tembus klik, tidak mengaktifkan) yang tampil; saat pegas ukuran selesai, jendela note interaktif muncul di persegi yang sama dan `NoteDragWindow` ditutup setelah frame berikutnya tergambar (pola yang sama dengan editor → kartu di 2f). Pengguna melihat satu benda. Kalimat PRD "jendela yang sama (tidak dibuat ulang)" diganti menjadi "jendela note mengambil alih di frame yang sama".
4. **Satuan.** Posisi dan ukuran disimpan dalam DIP layar utama di `float_x/y/w/h`. Area kerja = `SystemParameters.WorkArea` (sudah memperhitungkan ruang AppBar Gili dan taskbar). Saat start, perubahan resolusi/DPI, dan perubahan posisi bar, setiap note dijepit masuk area kerja.
5. **Kapan ditulis.** Posisi dan ukuran ditulis saat seret atau resize selesai, bukan setiap frame. Teks lewat jeda 0,5 detik yang sama dengan editor.
6. **Pesan batas.** Mockup memakai `hint()` (toast milik mockup). Di app, `Up to 6 floating notes` tampil 2,4 detik di bagian bawah halaman Notes, sebagai kapsul kecil (latar `#1C1C1E`, teks 12 px `#F5F5F7`). Muncul saat Float on top ditekan dan saat kartu ketujuh melewati tepi pulau (sekali per seretan); kartu itu tetap seretan pengurutan biasa.
7. **Back to island ke halaman lain yang terbuka.** Seperti mockup: masuk ke slot kartunya hanya saat halaman Notes terbuka; selain itu mengecil ke kapsul di tengah pulau. Pulau menggembung dan Gili senang hanya saat pulau tidak terbuka.
8. **Esc** di jendela note melepas fokus dari area teks (mockup `ta.blur()`); jendela tetap aktif.
9. **Alt+Tab tanpa taskbar.** `ShowInTaskbar = false` di WPF memberi jendela pemilik tersembunyi, jadi note tidak tampil di taskbar tetapi tetap ada di Alt+Tab. Jendela bayangan adalah tool window, jadi tidak muncul di keduanya.
10. **Pulau di atas note.** Saat pulau dibuka, `IslandWindow` memasang ulang `HWND_TOPMOST` untuk bar lalu dirinya, jadi pulau berada di atas note yang terakhir diklik.

## Review Focus

1. Seret kartu keluar lalu masuk lagi berkali-kali sebelum dilepas: kartu kembali ke grid setiap kali, tidak ada note yang hilang atau ganda, urutan benar.
2. Note mengambang lalu Gili restart: jendela muncul di tempat dan ukuran yang sama; posisi di luar layar (resolusi berubah) dijepit masuk.
3. Note dikosongkan di jendela mengambang lalu Back to island: dibuang, tidak ada kartu `Empty note`.
4. Klik di area bayangan note mengenai jendela di bawahnya.
5. Note ketujuh: tidak bisa dilepas, pesan tampil, tidak ada perubahan data.

---

### Task 1: Aturan dan data note mengambang (Core, Data)

**Files:** Create `src/Gili.Core/Notes/NoteFloat.cs`; Modify `Note.cs`, `src/Gili.Data/NoteStore.cs`; Test `tests/Gili.Core.Tests/Notes/NoteFloatTests.cs`, tes `NoteStore` yang ada.

- [x] `Note` mendapat parameter terakhir `NoteRect? Float = null`.
- [x] `NoteFloat`: konstanta `Width 260`, `Height 220`, `MinWidth 200`, `MinHeight 140`, `MaxWidth 520`, `MaxHeight 640`, `Max 6`, `Cascade 24`, `EdgeBand 6`, `CornerBand 12`; `bool CanFloat(int floating)`; `NoteRect Default(int floating, NoteRect work)`; `NoteRect Clamp(NoteRect rect, NoteRect work)` (ukuran dijepit ke batas dan ke area kerja, lalu posisi masuk); `NoteRect Released(double px, double py, double rx, double ry, NoteRect work)` (260 × 220 di sekitar titik lepas, sebelum dijepit); `ResizeEdges HitEdge(double x, double y, double w, double h)` (`[Flags] enum ResizeEdges { None, Left, Top, Right, Bottom }`); `NoteRect Resize(NoteRect start, ResizeEdges edges, double dx, double dy, NoteRect work)` (sisi seberang tetap, batas min/max dan area kerja, algoritma mockup); `double Radius(double from, double progress)` (lerp ke 12).
- [x] `NoteStore.All()` membaca `float_*` (keempatnya ada → persegi, selain itu null); `SetFloat(long id, NoteRect? rect)`.
- [x] Tes: posisi Float on top ke-1/ke-2, jepit di keempat sisi dan ukuran lebih besar dari area kerja, resize dari 8 arah termasuk batas, hit edge di tepi/sudut/tengah, titik lepas tetap, `CanFloat(5/6)`, store pulang-pergi termasuk null.
- [x] Build, test, format, publish, commit.

### Task 2: Jendela note mengambang, Float on top, Back to island, restart, fullscreen (App)

**Files:** Create `src/Gili.App/Island/FloatingNoteWindow.cs`, `FloatingNoteShadow.cs`, `FloatingNotes.cs`; Modify `NoteService.cs`, `NotesViewModel.cs`, `NotesPage.xaml(.cs)`, `IslandWindow.xaml.cs`, `App.xaml.cs`, `SoftShadow.cs`, `IconView`/ikon.

- [x] `NoteService`: `Float(id, rect) → bool` (batas 6, tulis), `MoveFloat(id, rect)` (tulis), `Return(id) → bool dropped` (flush, float null, kosong dibuang), event `Floated`, `Returned`; `Notes` tetap semua note.
- [x] `NotesViewModel.Refresh` hanya note tanpa `Float`; `FloatEditing()` untuk tombol; `Hint` 2,4 detik.
- [x] `FloatingNoteWindow`: header seret (dijepit ke area kerja), resize 8 arah lewat `NoteFloat`, area teks dengan simpan 0,5 detik dan `Ctrl+=`/`Ctrl+-`/`Ctrl+0`, Esc, Back to island, lahir/ping, pantul masuk.
- [x] `FloatingNoteShadow` tembus klik, ikut posisi dan urutan z jendela note.
- [x] `FloatingNotes`: pulihkan saat start, jepit saat area kerja berubah, sembunyikan saat fullscreen, Float on top (posisi bertingkat), Back to island ke slot kartu (Notes terbuka) atau kapsul di tengah pulau, gembung pulau.
- [x] Tombol Float on top di editor; `IslandWindow` memasang ulang topmost saat membuka.
- [x] Build, test, format, uji di app, publish, commit.

### Task 3: Seret kartu keluar pulau (robek, batal, mendarat)

**Files:** Modify `NotesPage.xaml.cs`, `NoteDragWindow.cs`, `FloatingNotes.cs`.

- [x] Pointer di luar persegi pulau → robek: kartu keluar dari grid (kartu lain merapat), bayangan makin dalam 220 ms; kembali ke dalam → batal.
- [x] Dilepas di luar: pulau menutup, ukuran pegas ke 260 × 220 di sekitar titik lepas, isi cross-fade ke tampilan jendela, lalu jendela note mengambil alih dan fokus di akhir teks; dijepit/pantul ke area kerja.
- [x] Note ketujuh: tidak robek, pesan batas sekali.
- [x] Build, test, format, uji di app, publish, commit.

### Task 4: Seret editor dari grip

**Files:** Modify `NotesPage.xaml(.cs)`, `NoteDragWindow.cs`.

- [x] Grip 16 px di tepi atas editor (ikon grip putih 30%, 65% saat hover, tooltip). Seret menyimpan dulu, jendela seret seukuran editor dengan teks editor dan radius 16.
- [x] Dilepas di dalam: kembali ke bingkai editor, editor fokus. Di luar: editor tertutup tanpa animasi, note mengambang (radius 16 → 12), pulau menutup.
- [x] Build, test, format, uji di app, publish, commit.

### Task 5: Gili senang dan mengikuti note

**Files:** Modify `src/Gili.Core/Mascot/GiliRules.cs`, `GiliService.cs`, `GiliEyes.cs`, `FloatingNotes.cs`; Test `GiliRulesTests`.

- [x] `GiliMood.Happy` (prioritas di atas Wide, tidak berkedip; rupa: lengkung, skala tinggi 0,42 dari bawah, sudut atas bulat) selama 0,9 detik saat note masuk ke pulau tertutup.
- [x] `GiliService.Watch(Func<Point?> centre, TimeSpan)`: mata mengikuti note yang dipindah dari header, yang terbang pulang, dan 1,5 detik setelah dilepas di luar pulau.
- [x] Demo: tidak ada keadaan pulau baru selain ekspresi; skenario demo mendapat satu "note masuk" (senang + gembung).
- [x] Build, test, format, uji di app, publish, commit.

### Task 6: PRD, mockup, dan verifikasi akhir

- [x] PRD 6.3/8.1 mengikuti keputusan 2, 3, dan 6. Checklist manual PRD 13 untuk note mengambang dijalankan sejauh bisa tanpa input sintetis; sisanya dicatat untuk pengguna.
- [x] Publish, commit.

## Temuan dan verifikasi (31 Oktober 2026)

11. **Penerbangan pulang di jendela sendiri** (keputusan tambahan saat Task 5). Back to island semula mengubah ukuran jendela note setiap frame; rekaman layar per 50 ms menunjukkan frame macet (307–407 ms), sudut persegi di tengah jalan, dan kapsul yang masih hijau di pulau. Jendela layered yang di-resize setiap frame tertinggal dari animasinya. Penerbangan sekarang digambar di `NoteFlightWindow`: tembus klik, ukurannya ditetapkan sekali (gabungan posisi awal dan tujuan), hanya bentuk di dalamnya yang bergerak, dan isi note memudar dari potretnya (`RenderTargetBitmap`). Rekaman ulang: mengecil dan menggelap mulus, hilang sekitar 550 ms, lalu pulau menggembung. PRD 6.3 dan 8.1 diperbarui.
12. **Crash saat keluar (bug lama Fase 3h).** `ClaudeService.Dispose()` mengosongkan sesi, `ShowClaude` memperbarui pulau, dan `IslandWindow` memanggil `Show()` pada jendela yang sudah ditutup, jadi `--quit` berakhir crash lalu Gili restart sendiri. `App.OnExit` sekarang melepas `Changed` sebelum dispose. Diverifikasi: `--quit` tiga kali tanpa entri `Unhandled exception` baru.
13. **Satuan dan DPI.** Laptop ini 1920 px pada DPI 125% (area kerja 1536 DIP). Note uji yang disimpan di x = 1400 DIP dijepit ke tepi kanan saat start, sesuai `NoteFloat.Clamp`.

Diverifikasi tanpa input sintetis (UI Automation Invoke dan potret layar), memakai satu note uji yang ditulis langsung ke `gili.db` lalu dihapus lagi (cadangan database di scratchpad sesi):

- Note mengambang dipulihkan setelah restart di tempatnya, sudut 12 px, cincin putih 10% di tepi, bayangan menggelapkan latar di bawahnya (piksel 14 → 25), jendela bayangan tepat di bawah jendela note dalam urutan z dan bergaya `WS_EX_TRANSPARENT`.
- Back to island dengan pulau tertutup: jendela dan bayangannya hilang, `float_*` kosong lagi, pulau menggembung.
- RAM: +3,7 MB memori privat untuk satu note 260 × 220 (anggaran 5 MB).
- 854 tes unit lulus, `dotnet format` bersih.

Belum diverifikasi, butuh mouse pengguna (checklist PRD 13): seret kartu keluar lalu masuk lagi sebelum dilepas, lepas di luar (tumbuh, pulau menutup, langsung mengetik), seret dari grip editor (lepas di dalam dan di luar, termasuk note baru kosong), Float on top, pindah dari header, resize dari keempat tepi dan sudut sampai batas, note ketujuh, Back to island saat halaman Notes terbuka, fullscreen YouTube, dan mata Gili mengikuti note yang dipindah.
