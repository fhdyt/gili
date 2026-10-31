# Revisi 12: issue #21, #24, #26, #29, #33, #34

Tanggal: 1 November 2026. Pengguna meminta keenam issue dikerjakan sekaligus dan setiap revisi juga memperbarui PRD dan mockup. Sesuai preferensi pengguna (memilih sendiri tanpa tanya-jawab), semua pilihan di bawah adalah **keputusan saya**, dengan alasannya.

## Ringkasan per issue

| Issue | Permintaan | Keputusan singkat | ID PRD |
|---|---|---|---|
| #29 | Pengaturan untuk menyembunyikan jam dan tombol power di kanan | Saklar `Clock and date` sudah ada sejak Fase 3c; ditambah saklar `Power` (default nyala) | F-RIGHT-12 |
| #24 | Deretan aplikasi terbuka di kiri bisa disembunyikan | Saklar `Running apps` di Left side (default nyala) | F-LEFT-07 |
| #26 | Timer bisa custom jam/menit/detik, gaya iOS | Roda pemilih tiga kolom di halaman Timer pulau saat timer siap; preset tetap | F-ISL-29 |
| #34 | Drop ke Shelf punya dua kolom Copy dan Cut | Drop zone dibagi dua: Copy (kiri) dan Cut (kanan); mode tersimpan per item | F-ISL-30 |
| #33 | Direktori sementara supaya file dari dalam zip bisa ditaruh di Shelf | Folder `shelf\` milik Gili untuk file virtual dan file dari folder temp | F-ISL-31 |
| #21 | Clipboard juga menampung gambar dan file | Riwayat Clipboard berisi teks, gambar, dan file; gambar disimpan PNG di folder data | F-ISL-32 (memperluas F-LEFT-03, F-ISL-28) |

Fase: semuanya Fase 5 (revisi sebelum rilis publik pertama), bukan Fase 6.

## #29 Saklar Power

- `RightModules.Power` (flag baru), baris `Power` di Settings → Right side setelah `Clock and date`, keterangan `Sleep, restart and shut down stay in the Start menu`.
- Mati: ikon Power (termasuk titik oranye update Windows) hilang dari bar dengan animasi seperti modul lain; pulau Power tidak bisa dibuka dari bar, jadi sisa baterai juga tidak terlihat (status ngecas tetap di pulau). Alasan: permintaan eksplisit pengguna; Start menu Windows tetap punya Sleep/Restart/Shut down.
- Settings (roda gigi) tetap selalu tampil, karena tanpa itu Settings hanya bisa dibuka lewat klik kanan bar.
- `Clock and date` sudah bisa disembunyikan; tidak ada perubahan selain menyebutnya di laporan.

## #24 Saklar Running apps

- `LeftIcons.Apps` (flag baru), baris `Running apps` paling bawah di Settings → Left side, keterangan `The apps that are open, after the divider`.
- Mati: pemisah, ikon aplikasi, dan `+N` hilang; ikon kiri lain tidak bergeser. Pemisah hanya tampil bila ada ikon di kirinya **dan** Running apps nyala.
- Hemat: selama mati, `AppsService` berhenti membaca daftar jendela (event jendela diabaikan, ikon tidak di-resolve). Pelacak jendela aktif tetap jalan karena dipakai Clipboard (menempel ke jendela terakhir) dan Claude (Done terlihat).
- Tidak punya halaman pulau, jadi tidak memengaruhi tab.

## #26 Roda pemilih timer (gaya iOS)

- Muncul di halaman Timer pulau, mode Timer, saat timer **siap** (tidak berjalan dan tidak dijeda). Menggantikan angka besar dan baris `Pick a duration`. Saat berjalan atau dijeda, angka besar dan ring tampil seperti sebelumnya.
- Tiga kolom: jam 0–23, menit 0–59, detik 0–59, masing-masing berputar melingkar (setelah 59 kembali ke 0, seperti iOS). Label tetap `hours`, `min`, `sec` di kanan angka baris tengah.
- Tinggi 5 baris × 24 px = 120 px. Pita pilihan di baris tengah: kapsul selebar ketiga kolom, tinggi 30 px, radius 9 px, putih 10%. Angka tengah 20 px semibold putih; baris ±1 15 px abu-abu 60%; baris ±2 13 px 30%. Digit tabular.
- Interaksi: roda mouse satu notch = satu langkah; seret vertikal dengan inersia lalu menepi ke baris terdekat (pegas, damping 1, response 0,35 s, bisa disela); klik baris lain memutar ke baris itu; keyboard ↑/↓ (Tab pindah kolom), PageUp/PageDown 5 langkah. Roda mouse di atas roda pemilih tidak memindah halaman. Nama aksesibel `Hours`, `Minutes`, `Seconds`.
- Preset (`10 s`, `5 min`, `10 min`, `25 min`) tetap, dan menekannya memutar roda ke nilai itu dengan pegas.
- Durasi 0:00:00 mematikan Start. Nilai roda adalah durasi timer pulau (memutar roda = preset dengan durasi itu) dan diingat setelah restart lewat timer pulau yang sudah tersimpan.
- Jendela Tools tetap memakai kolom Duration teks, yang sudah menerima `h:mm:ss`.
- Logika roda (pecah/gabung detik, langkah melingkar, offset → indeks, target menepi) di Core `TimerWheel` dengan unit test; animasi memakai `Spring` Core di `CompositionTarget.Rendering` hanya selama roda bergerak.
- Alternatif yang ditolak: stepper `− +` per satuan (bukan gaya iOS), kolom teks (sudah ada di Tools, pengguna minta gaya iOS).

## #34 Drop zone Copy / Cut

- Drop zone (420 × 140 px) berisi dua kotak bergaris putus-putus berdampingan, celah 8 px, masing-masing ikon dalam lingkaran 36 px, judul 15 px bold, keterangan 12 px:
  - kiri **Copy**: `Keeps the original where it is` (ikon salin);
  - kanan **Cut**: `Moves it when you drag it out` (ikon gunting).
- Kotak di bawah pointer menyala hijau (kotak Copy) atau oranye `#FF9F0A` (kotak Cut) dengan latar 8%, lingkaran berwarna dan ikon hitam, sedikit membesar (1,06). Selama file masih di atas bar, keduanya abu-abu; melepas di bar sama dengan Copy.
- Shelf penuh: kedua kotak diganti satu pesan kuning seperti sebelumnya.
- Mode disimpan per item (`shelf_items.mode`). Tile item Cut menampilkan lencana gunting kecil di pojok kiri atas ikon, dan tooltip-nya diawali `Cut · moves when you drag it out`.
- Seret keluar: item Copy hanya menawarkan Copy (tetap di Shelf); item Cut hanya menawarkan Move, jadi file pindah ke tujuan dan item keluar dari Shelf setelah pengecekan ulang (seperti sekarang). Shift tidak lagi mengubah apa pun.
- Sumber seret tetap selalu menerima efek Copy dari Gili, jadi file asli tidak pernah dihapus saat drop (keputusan Fase 3d tetap).
- Alasan memakai "Cut = pindah saat diseret keluar" (bukan memindahkan file asli ke folder Gili saat drop): sama dengan arti Cut di Explorer (file baru pindah saat ditempel), dan Gili tidak pernah memindahkan file pengguna tanpa tujuan yang dipilih pengguna.

## #33 Folder Shelf milik Gili

- Folder `shelf\` di folder data (`%LOCALAPPDATA%\Gili\shelf\`, versi Store di `LocalState\shelf\`), satu subfolder per drop.
- Drop yang **disalin ke folder ini**:
  1. File virtual: data seret tanpa daftar path biasa (`CF_HDROP`) tetapi dengan `FileGroupDescriptorW` + `FileContents`. Ini yang dikirim folder zip di Explorer, lampiran Outlook, dan beberapa browser. Folder di dalam zip ikut dibuat ulang.
  2. Path yang berada di folder temp pengguna (`%TEMP%`): tempat 7-Zip, WinRAR, dan Explorer mengekstrak file dari arsip, dan yang dihapus lagi setelah arsipnya ditutup.
- Item lain tetap disimpan sebagai referensi (tanpa salinan).
- Item yang disimpan di folder Gili diperlakukan sama: Copy menyalin dari folder Gili, Cut memindahkannya keluar. Menghapus item, Clear all, atau file yang sudah dipindah keluar menghapus subfoldernya. Saat start, subfolder yang tidak dirujuk item mana pun dihapus.
- Tooltip tile item tersimpan diakhiri `Kept by Gili until you drag it out`. Ringkasan dan aturan 8 item tidak berubah.
- Batas: drop virtual lebih dari 4 GB ditolak dengan pesan wide `Too big for the Shelf · 4 GB at most`, supaya disk tidak penuh diam-diam. Isi file virtual dibaca saat drop (OLE mewajibkannya selama drop); file temp disalin di thread latar belakang.
- Drop virtual dibaca lewat `System.Runtime.InteropServices.ComTypes.IDataObject` (DataObject WPF meneruskannya ke objek OLE asli), media `ISTREAM` atau `HGLOBAL`.
- Risiko: zip besar membuat UI berhenti sebentar selama drop. Dicatat di PRD 12.

## #21 Riwayat Clipboard dengan gambar dan file

- Riwayat (maks. 50 entri, terbaru di atas) berisi tiga jenis: teks, gambar, file.
  - **Gambar** (bitmap yang dicopy: screenshot, Copy image di browser): disimpan sebagai PNG di folder data `clipboard\<hash>.png`. Paling banyak 20 gambar; yang ke-21 membuang gambar tertua. Gambar di atas 25 megapiksel tidak disimpan (tetap cuplikan).
  - **File** (copy atau cut di Explorer): daftar path sebagai referensi. Satu file gambar memakai thumbnail filenya.
- Duplikat: teks sama, daftar file sama, atau gambar dengan hash piksel sama naik ke atas.
- Baris: gambar menampilkan thumbnail 40 × 28 px (radius 6 px, garis putih 16%) lalu nama (`Screenshot`, `photo.jpg`, `Image`) + `1920 × 1080`; file menampilkan ikon file (atau thumbnail untuk satu file gambar) lalu nama file atau `3 files`. Baris kedua tetap aplikasi sumber + waktu. Tooltip: daftar path untuk file.
- Menempel: gambar ditaruh di clipboard sebagai bitmap (dan PNG), file sebagai daftar file (efek Copy), lalu Ctrl+V seperti teks. Gili tidak memunculkan cuplikan untuk tulisannya sendiri.
- File yang sudah tidak ada: entri file yang semua filenya hilang dibuang saat halaman dibuka; yang sebagian hilang hanya menempel yang masih ada.
- Cari: teks, nama file, dan nama gambar.
- Hapus/Clear all menghapus PNG-nya; saat start, PNG yang tidak dirujuk dihapus.
- Privasi: gambar riwayat ditulis ke disk (sebelumnya hanya teks). Thumbnail cuplikan di slot tetap hanya di memori. Isi sensitif tetap tidak pernah masuk. PRD 8.2, 10, privacy policy, dan CLAUDE.md diperbarui.
- Alasan menyimpan ke disk, bukan memori: memori berarti beberapa screenshot 4K bisa memakan puluhan MB RAM, melanggar permintaan "sangat ringan"; teks riwayat juga sudah di disk.

## Anggaran (PRD 8.1)

| Fitur | Anggaran |
|---|---|
| Roda timer | CPU hanya selama roda bergerak; 0 saat diam |
| Drop zone dua kolom | Tidak terukur |
| Folder Shelf | Disk sebesar file yang disimpan; tidak ada polling baru |
| Gambar dan file di Clipboard | RAM ≤ +1 MB saat halaman terbuka (thumbnail 80 px); 0 saat tertutup; disk ≤ 20 PNG; encode PNG di thread latar belakang |
| Running apps mati | Menghemat pembacaan daftar jendela |

## Demo (F-BAR-09)

Drop zone demo menyalakan kotak Copy lalu Cut sebelum item masuk; demo clipboard menambah satu cuplikan gambar (sudah ada). Roda timer tidak butuh langkah demo karena tampil setiap halaman Timer dibuka saat timer siap.

## Urutan pengerjaan

#29 → #24 → #26 → #34 → #33 → #21; setiap issue: PRD + mockup, lalu kode dan test, lalu build/test/format, commit, dan `publish.ps1`.
