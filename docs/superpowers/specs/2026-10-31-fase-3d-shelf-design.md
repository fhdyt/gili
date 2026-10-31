# Fase 3d: Shelf (desain)

Tanggal: 2026-10-31. Fitur: F-ISL-12 (Shelf), F-LEFT-04 (ikon Shelf), F-ISL-07 Shelf (halaman 8), dan langkah "drop ke Shelf" di mode demo (F-BAR-09).

Pengguna meminta saya melanjutkan tanpa berhenti dan memilih sendiri ("jika ada pilihan pilih sendiri"). Semua bagian **(keputusan Claude)** adalah pilihan saya. Isi dan tampilan dari PRD 6.2 (ikon Shelf), 6.3 "Shelf" dan tabel halaman baris 8, 10 (`shelf_items`), 11, 12, dan mockup (`.shsum`, `.shgrid`, `.ftile`, `.dz`, `addFiles`, `renderShelf`, `enterDrop`).

## Maksud

Shelf adalah tempat file sementara: pengguna menyeret file ke bar atau pulau, membuka folder tujuan, lalu menyeret file itu keluar dari halaman Shelf (PRD 4, skenario 5). Gili hanya menyimpan referensi ke file asli, jadi tidak ada yang disalin atau dihapus oleh Gili sendiri.

## Tujuan

- Menyeret file ke atas bar atau pulau mengubah pulau menjadi drop zone 420 × 140; melepasnya menambah satu item dan membuka halaman Shelf; menyeret menjauh mengembalikan keadaan sebelumnya.
- Satu drop = satu item: satu file atau folder jadi tile biasa, beberapa jadi bundel (`12 files`). Maksimal 8 item; saat penuh drop zone berkata `Shelf is full` / `Drag something out first` dan tidak menerima file.
- Item tersimpan di `shelf_items` dan tetap ada setelah restart.
- Menyeret tile keluar menawarkan Copy dan Move (tujuan yang memilih; Shift memaksa Move). Item keluar dari Shelf bila filenya sudah tidak ada di tempat asal setelah seret selesai.
- File yang hilang dibuang saat halaman Shelf dibuka dan tiap 1 menit selama Shelf berisi. Bundel kehilangan file yang hilang saja, dan terhapus bila semuanya hilang.
- Seret tile yang filenya sudah hilang dibatalkan; tile menampilkan `File moved or deleted` 2 detik lalu dihapus (PRD 11).
- Ikon Shelf di kiri: jumlah file di sebelahnya, tooltip `Shelf · 3 files`, memantul (skala 1,25, 380 ms) saat item baru masuk.
- Halaman Shelf sesuai mockup: ringkasan di kiri (angka besar, `3 files · 12.4 MB` atau `files on the shelf`, `Clear all`), grid 4 × 2, tile dengan ikon jenis file dan lencana ekstensi, tombol hapus saat hover/fokus, dan teks kosong.
- Mode demo menampilkan drop zone lalu halaman Shelf dengan item demo yang tidak pernah disimpan.

## Bukan tujuan

- Menyalin file ke Shelf (PRD 15).
- Thumbnail gambar di tile: mockup memakai ikon jenis file saja.
- Pesan khusus untuk seret dari aplikasi admin (UIPI): Windows tidak memberi tahu aplikasi biasa bahwa seret diblokir, jadi drop zone memang tidak muncul. Dicatat sebagai keterbatasan.

## Anti-tujuan

- Gili memindahkan atau menghapus file pengguna. Drop ke Gili selalu dilaporkan sebagai Copy ke sumbernya, supaya Explorer tidak pernah menghapus file asal karena mengira Gili sudah memindahkannya.
- Polling saat Shelf kosong.
- Membaca isi folder secara rekursif untuk menghitung ukuran.

## Pendekatan

### Di mana logikanya

| Cara | Untung | Rugi |
|---|---|---|
| **`ShelfService` di Core dengan `IShelfStore` dan probe file yang disuntikkan** | Semua aturan (batas, bundel, pembersihan, teks) dites tanpa disk; sesuai PRD 9.1 yang menaruh `ShelfService` di Core | Satu interface baru |
| Service di App seperti `TodoService` | Pola yang sudah ada | Aturan Shelf tidak bisa dites di `Gili.Core.Tests` tanpa WPF |

Dipilih yang pertama **(keputusan Claude)**. `Gili.Data.ShelfStore` mengimplementasikan `IShelfStore`; App memberi probe `FileFacts? Probe(string path)` (ada, folder atau file, ukuran).

### Drop masuk

`BarWindow` dan `IslandWindow` memakai OLE drag-and-drop WPF (`AllowDrop`, `DataFormats.FileDrop`). `DragEnter` dengan file memanggil `IslandController.DragEntered()`; `DragLeave` dari kedua jendela ditunda 150 ms supaya pindah dari bar ke pulau tidak membuat drop zone berkedip **(keputusan Claude)**. Drop di bar maupun pulau menambah item, karena PRD 6.2 menyebut file bisa diseret ke ikon Shelf "seperti ke bagian bar lain"; mockup hanya menerima drop di pulau, jadi mockup disamakan dengan PRD **(keputusan Claude)**. Saat kursor di atas pulau, drop zone menyala hijau (`hot`).

Efek yang dilaporkan ke sumber: `Copy` (anti-tujuan), `None` saat penuh.

### Seret keluar

Tile memulai `DragDrop.DoDragDrop` setelah kursor bergerak 4 px dengan tombol kiri ditahan, membawa `DataFormats.FileDrop` berisi path item dan efek `Copy | Move`. Hasil `DoDragDrop` tidak dipakai untuk menentukan Move, karena Explorer melakukan *optimized move* dan bisa melaporkan efek yang berbeda dari yang terjadi; setelah seret selesai Gili memeriksa ulang path item, dan item yang semua filenya hilang keluar dari Shelf **(keputusan Claude)**. Selama seret keluar, drop zone tidak muncul untuk seret milik Gili sendiri.

### Ukuran dan teks

- Ukuran tampil seperti mockup `fmtSize` (desimal): `2.4 GB`, `12.4 MB`, `340 KB` (minimal `1 KB`).
- Ukuran item disimpan saat drop (`size_bytes`); folder dihitung 0 dan tile-nya memakai ikon folder tanpa lencana.
- Tooltip tile: satu baris per file `name · size` (folder: `name · Folder`).
- Lencana: ekstensi huruf besar maks. 4 huruf dengan warna mockup (`pdf #FF6961`, `jpg/png #BF5AF2`, `zip #FFD60A`, `docx #64D2FF`, `xlsx #30D158`, `mp4/mkv #FF9F0A`, lainnya `#8E8E93`); tanpa ekstensi `FILE`; bundel menampilkan jumlah file dengan latar `#D1D1D6` dan ikon tumpukan.
- Nama bundel: `12 files`. Ringkasan: `1 file · 2.4 MB` / `3 files · 12.4 MB`; kosong `files on the shelf`. Ikon kiri: `Shelf · 1 file`, `Shelf · 3 files`, atau `Shelf` saat kosong. Jumlah = jumlah file, folder dihitung satu.

### Drop zone

Lapisan baru di `IslandWindow` sesuai `.dz`: garis putus-putus 1,5 px `#48484A` dengan jarak 10 px dari tepi dan radius 22, lingkaran 48 px `#1C1C1E` berisi ikon `traydown` 24 px `#8E8E93`, judul `Drop to keep on the Shelf` dan keterangan `Files stay where they are until you drag them out`. Hot: garis dan lingkaran `#30D158`, latar garis `#1430D158`, ikon hitam, lingkaran naik 2 px dan membesar 1,06. Penuh: garis dan ikon kuning `#FFD60A` (`--warn` mockup), teks `Shelf is full` / `Drag something out first`.

## Mode demo

`DemoShelf` menyalakan drop zone 1,5 detik (hot), lalu menaruh satu item demo (`Quarterly report.pdf`, 2,4 MB) di atas isi Shelf yang tampil dan membuka halaman Shelf. Item demo hanya ada di memori dan hilang saat demo berakhir, seperti alarm demo **(keputusan Claude)**. Supaya demo tetap di bawah satu menit, langkahnya di 29 detik, langsung setelah halaman System, dan pulau Power menggantikan halaman Shelf di 31 detik.

## Anggaran (PRD 8.1)

| Fitur | Biaya | Anggaran | Cadangan |
|---|---|---|---|
| Shelf (Fase 3d) | Event drag-and-drop OLE; `File.Exists`/`Directory.Exists` untuk ≤ 8 item saat halaman dibuka dan tiap 60 detik hanya selama Shelf berisi; ikon dari geometri yang sudah ada | CPU 0% saat diam; RAM ≤ +1 MB | — |

## Yang diserahkan ke pembangun

Nama kelas dan susunan XAML selama tampilan sesuai mockup, cara mendeteksi ambang seret, dan cara menyimpan urutan (`sort_order` = urutan masuk).

## Temuan saat implementasi

- **Tabel `shelf_items` sudah ada** sejak migrasi pertama, dan mode `DropZone` (420 × 140, radius 30) sudah ada di koordinator dan layout sejak Fase 1, jadi tidak perlu migrasi baru.
- **Bar perlu latar yang hampir bening** (`#01000000`) di grid akarnya: saat drop zone muncul, backdrop bar memudar ke 0, dan area yang benar-benar bening tidak menerima seret, sehingga drop zone akan menutup sendiri lalu muncul lagi.
- **Warna penuh** mengikuti `--warn` mockup (`#FFD60A`), bukan oranye seperti draf desain.
- **Demo tetap di bawah satu menit**: langkah Shelf ada di 29 detik (setelah halaman System), dan pulau Power menggantikannya di 31 detik.
- **Teks keadaan kosong** harus di `DockPanel`, bukan `StackPanel` horizontal, supaya membungkus di lebar 340 px.
- **Belum teruji dengan input asli**: menyeret file dari Explorer ke bar/pulau (drop zone, hot, penuh), menyeret tile keluar ke folder (Copy, Move dengan Shift), pantulan ikon, dan demo mode (menu klik kanan bar tidak terjangkau UI Automation). Semua itu butuh seret mouse sungguhan, yang tidak dikirim secara sintetis di laptop pengguna.

## Hasil verifikasi

- Release 0 warning, 730 tes lulus, format bersih.
- Item yang dimasukkan ke `shelf_items` (PDF 2,4 MB, bundel tiga file, folder) tampil sesuai mockup: `5`, `5 files · 2.8 MB`, lencana `PDF` merah, bundel berlencana `3`, ikon folder; ikon kiri menampilkan `5`.
- `Remove Photos` (UI Automation Invoke) menghapus item; menghapus `notes.txt` dari disk lalu membuka ulang halaman mengubah bundel menjadi `2 files` (388 000 byte) di layar dan database; `Clear all` mengosongkan database dan angka di ikon kiri.
- Halaman Shelf terbuka di salinan GitHub (`publish.ps1`) dan MSIX (`package.ps1 -Install`).
- CPU tidak bisa diukur dalam keadaan diam: pengguna memakai laptop selama pengukuran (121 dari 121 detik ada input), dan angka 1,3–1,5% itu termasuk mata Gili yang mengikuti kursor. Shelf yang kosong tidak menjalankan timer apa pun; timer 60 detik hanya jalan saat Shelf berisi.
