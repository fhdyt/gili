# Revisi 14: issue #22, #25, #27, #28, #30, #31, #32, #35, #36, #38

Tanggal: 1 November 2026. Pengguna meminta semua issue yang open dikerjakan tanpa berhenti, memilih sendiri pilihan terbaik, lalu memasang ulang versi terbaru di laptop, commit, push, dan menutup issue. Sesuai preferensi itu, semua pilihan di bawah adalah **keputusan saya**, dengan alasannya. Setiap revisi juga memperbarui PRD dan mockup.

## Ringkasan per issue

| Issue | Permintaan | Keputusan singkat | ID PRD |
|---|---|---|---|
| #38 | File yang sudah diseret keluar dari Shelf hilang dari Shelf | Setiap seret keluar yang diterima tujuan mengeluarkan item dari Shelf, Copy maupun Cut | F-ISL-12 (ubah), F-ISL-30 (ubah) |
| #32 | Ikon aplikasi memakai maskot Gili berbentuk lingkaran | Lingkaran hitam dengan dua mata putih, proporsi lingkaran Gili di bar | F-PUB-03 (ikon), 9.4 |
| #25 | Cara menutup Gili dari klik kanan area kosong bar atau ikon tray | Ikon Gili di notification area Windows dengan menu yang sama dengan menu bar; klik kanan pulau compact juga membuka menu; item `Quit Gili` | F-BAR-17 (baru), 6.1 |
| #35 | Pilihan warna bar siap pakai + kode warna custom | Baris `Bar color`: 8 warna siap pakai + `Custom` dengan kode hex | F-BAR-14 (baru) |
| #22 | Opacity bar saat pulau terbuka (sekarang transparan penuh) | Slider `Opacity while open` 0–100%, default 0% (perilaku lama) | F-BAR-15 (baru), 6.3 |
| #30 | Di desktop, transparansi bar mengikuti opacity saat pulau terbuka | Saklar `Same on the desktop` (default nyala) | F-BAR-16 (baru) |
| #31 | Musik/video dari browser menampilkan thumbnail | Video juga menampilkan thumbnail 16:9 di slot, gelembung, dan halaman Media; thumbnail dibaca ulang setiap kali sesi media mengubah propertinya | F-ISL-04, F-ISL-05 (ubah) |
| #36 | Favorite app di kiri, sederet Notes/Clipboard/Shelf/Claude | Deretan Favorites setelah Claude, sebelum pemisah; ditambah dari klik kanan aplikasi berjalan atau Settings | F-LEFT-08 (baru), 6.2 |
| #27 | Auto update dari GitHub tanpa menghilangkan data | Saklar `Install updates automatically` (default nyala): update yang sudah terverifikasi dipasang saat komputer tidak dipakai; data tetap di folder data dan salinan `.gili` | F-PUB-04 (ubah) |
| #28 | Notifikasi WA dll; saat diklik pulau membuka sebesar kartu timer/alarm | Spesifikasi di PRD dan mockup: klik notifikasi di pulau membuka kartu notifikasi; implementasi tetap Fase 6 bersama F-ISL-27 | F-ISL-27 (ubah) |

Fase: semuanya revisi sebelum rilis publik pertama (Fase 5), kecuali #28 yang menempel pada fitur Fase 6.

## #38 Seret keluar mengeluarkan item

- Setelah `DoDragDrop` selesai dengan efek apa pun selain None (tujuan menerima file), item keluar dari Shelf: Copy meninggalkan file asli di tempatnya, Cut memindahkannya. Seret yang dibatalkan (Esc, dilepas di tempat yang menolak) membiarkan item tetap ada. Pemeriksaan ulang lama tetap berjalan untuk efek None, karena Explorer kadang melaporkan None untuk move yang dioptimalkan.
- Item yang filenya disimpan di folder Shelf milik Gili (dari zip atau folder temp): subfoldernya **tidak** langsung dihapus, karena Explorer menyalin file di thread latar belakang setelah `DoDragDrop` kembali, dan menghapus sumbernya akan menggagalkan salinan itu. Subfolder dihapus 30 menit kemudian (pemeriksaan Shelf tiap menit) atau saat Gili mulai lagi, mana yang lebih dulu.
- Alasan bukan saklar: pengguna minta perilaku ini, dan Shelf memang tempat sementara. Menaruh file lagi cukup dengan menyeretnya kembali.
- Core: `ShelfService.TakeOut(id, now)` dan `SweepLeftovers(now)`; unit test.

## #32 Ikon maskot lingkaran

- `scripts/package-assets.ps1` menggambar lingkaran hitam penuh (bukan lagi kotak membulat) dengan dua mata putih kotak membulat: sisi mata 25% diameter, radius sepertiga sisi mata, celah 1/12 diameter, di tengah lingkaran, sama dengan lingkaran Gili 24 px (mata 6 px, celah 2 px) dan 36 px di samping menu (mata 9 px, celah 3 px). Ukuran 16 px memakai mata 28% supaya tetap terbaca.
- Menghasilkan `packaging/Gili.ico` (16–256 px) dan logo MSIX. `Gili.exe`, installer, shortcut Start Menu, entri Apps, dan ikon tray (#25) memakai ikon ini.

## #25 Menutup Gili

- **Ikon tray**: Gili memasang ikonnya sendiri di notification area Windows (`Shell_NotifyIcon`), tooltip `Gili`. Klik kiri membuka Settings; klik kanan membuka menu yang sama dengan menu bar: `Open Tools`, `Settings`, `Demo mode`, pemisah, `Quit Gili`. Saat taskbar Windows disembunyikan, ikon ini ada di flyout ikon tersembunyi (tombol Tray Gili). Ikon didaftar ulang saat `TaskbarCreated` dan dilepas di semua jalur keluar.
- **Saklar**: Settings → General → `Icon in the notification area` (default nyala), keterangan `Right-click it to quit Gili`.
- **Pulau compact**: klik kanan pulau compact (bukan saat terbuka) juga membuka menu bar.
- Item menu `Quit` diganti `Quit Gili` di kedua menu, supaya jelas yang ditutup adalah Gili.
- Alasan: menu bar sudah ada sejak Fase 1, tetapi tidak terlihat; ikon tray adalah tempat yang dicari pengguna Windows untuk menutup aplikasi latar belakang.

## #35 Warna bar

- Settings → Appearance → `Bar color`, tepat di atas `Bar opacity`: deretan 8 lingkaran warna 20 px plus lingkaran `Custom`:
  `Graphite #161618` (default), `Black #000000`, `Slate #1C2530`, `Navy #15203D`, `Forest #13281C`, `Plum #2A1733`, `Wine #331820`, `Cocoa #2B2018`.
- Lingkaran yang dipilih bercincin putih 2 px (jarak 2 px). `Custom` membuka kolom `#RRGGBB` di baris itu (menerima 3 atau 6 digit, dengan atau tanpa `#`); Enter atau keluar dari kolom menyimpan, kode yang tidak valid ditolak dengan `Use a code like #1E2A44`.
- Warna terang: kalau kontras teks putih terhadap warna itu di bawah 4,5:1, keterangan baris menjadi `Light colors make the bar's text hard to read`; warnanya tetap dipakai.
- Warna dipakai untuk tint bar (dengan opacity bar) dan gradient acrylic. Pulau tetap `#000000`.
- Disimpan sebagai `BarColor` (`#RRGGBB`) di `settings.json`. Core: `BarColors` (preset, parse, kontras) dengan unit test.
- Alasan 8 warna gelap: teks dan ikon bar putih, jadi semua preset gelap dengan kontras ≥ 4,5:1; warna terang tetap bisa lewat Custom.

## #22 Opacity saat pulau terbuka

- Settings → Appearance → `Opacity while open` (0–100%, default 0%), keterangan `How much of the bar stays while the island is open`.
- Saat pulau mengambang, latar bar memudar ke nilai ini (dikali opacity bar), bukan selalu ke 0. Kaca tetap nyala selama nilainya di atas 0; di 0 kaca dimatikan setelah fade seperti sekarang. Bayangan teks dipakai selama nilainya di bawah 50%.
- Default 0% menjaga perilaku lama (keputusan pengguna 2026-10-27: latar bar transparan penuh).

## #30 Sama di desktop

- Settings → Appearance → `Same on the desktop` (default nyala), keterangan `The bar looks like this while the desktop is in front`.
- Selama desktop berada di depan (jendela foreground adalah desktop shell, `Progman` atau `WorkerW`, misalnya setelah Win+D atau klik wallpaper), latar bar memakai `Opacity while open`, dengan fade yang sama (220 ms). Begitu jendela lain ke depan, latar kembali.
- Dibaca dari event foreground yang sudah dipakai untuk aplikasi aktif; tanpa polling.
- Core: `BarBackdrop.Opacity(...)` memutuskan opacity latar dari pulau mengambang, desktop, dan pengaturan; unit test.

## #31 Thumbnail media dari browser

- **Thumbnail dibaca ulang**: browser mengganti judul lebih dulu lalu thumbnail video baru sesaat kemudian, dan cache lama (per judul) bisa memegang gambar video sebelumnya atau tidak ada gambar. Sekarang thumbnail dibaca ulang setiap kali sesi mengirim `MediaPropertiesChanged`, tidak saat posisi atau status putar berubah.
- **Video punya gambar** (menggantikan "Video tidak menampilkan gambar"):
  - Slot compact: thumbnail 16:9 setinggi pulau − 6 px (32 × 18 px pada pulau 24 px), radius 4 px, garis putih 14%, dipotong mengisi kotaknya, di tempat ikon film. Tanpa thumbnail: kotak `#1C1C1E` dengan ikon film abu-abu di ukuran yang sama.
  - Gelembung (video sebagai aktivitas kedua): thumbnail sebagai lingkaran, seperti artwork musik.
  - Halaman Media: thumbnail 16:9 di kiri (199 × 112 px, radius 12 px, sejajar dengan cover musik yang 112 px), judul dan progres di kanannya; tanpa thumbnail tetap tampilan lama (judul dan progres saja). Ambient glow mengambil warna dari thumbnail.
  - Mode demo memutar video dengan thumbnail di slot selama ±7 detik setelah pengingat todo.
- Musik dari browser (YouTube di Chrome/Edge dilaporkan sebagai musik oleh Chromium) sudah memakai artwork; dengan pembacaan ulang, thumbnail video YouTube yang benar tampil.
- Ringan: satu decode per perubahan properti, sama seperti artwork musik (PRD 8.1).

## #36 Favorite apps

- **Tempat**: deretan ikon setelah Claude dan sebelum pemisah, dengan celah 2 px seperti launcher. Ikon asli aplikasinya 16 px berwarna (bukan abu-abu seperti launcher, supaya terlihat beda), tombol 26 × 24 px dengan hover yang sama. Tooltip nama aplikasinya. Maksimal 8.
- **Klik**: kalau aplikasinya sedang jalan, jendela terakhirnya dibawa ke depan (sama dengan klik ikon di deretan aplikasi berjalan); kalau tidak, aplikasinya dijalankan: aplikasi Store lewat AppUserModelID (`shell:AppsFolder\<AUMID>`), aplikasi desktop lewat path exe-nya.
- **Menambah**: klik kanan ikon aplikasi berjalan → `Add to Favorites`; atau Settings → Left side → `Add app…` (pilih `.exe` atau shortcut `.lnk`). **Menghapus**: klik kanan favorit → `Remove from Favorites`, atau tombol hapus di daftar Settings. **Urutan**: klik kanan favorit → `Move left` / `Move right`.
- **Settings → Left side**: saklar `Favorites` (default nyala; keterangan `Right-click an open app to add it`) setelah Claude, lalu satu baris per favorit (ikon, nama, tombol `Remove`), lalu tombol `Add app…`.
- Favorit yang exe-nya sudah tidak ada: ikonnya abu-abu 40% dengan tooltip `<nama> · not found`; klik tidak melakukan apa pun.
- Disimpan di `settings.json` sebagai daftar `{ Key, Name }` (`Key` = `aumid:<AUMID>` atau `exe:<path>`, sama dengan kunci deretan aplikasi). Core: `Favorites` (tambah tanpa duplikat, hapus, geser, batas 8) dengan unit test.
- Ruang: favorit dihitung sebagai ikon tetap di kiri, jadi aturan 24 px dan `+N` tetap berlaku; yang dipindah ke `+N` tetap aplikasi berjalan.
- Alasan memakai klik kanan aplikasi berjalan sebagai jalan utama: pengguna biasanya ingin memfavoritkan aplikasi yang sedang dibuka, dan cara ini bekerja juga untuk aplikasi Store (WhatsApp) yang tidak punya exe yang bisa dipilih.

## #27 Update otomatis tanpa kehilangan data

- **Saklar** Settings → About → `Install updates automatically` (default nyala), keterangan `Gili installs a new version while you're away. Your notes and settings stay`.
- **Alur**: cek update tetap seperti Fase 5d. Kalau ada versi baru dan saklarnya nyala, Gili memeriksa tiap menit apakah saatnya memasang; saat itu tiba, installer diunduh, diverifikasi (tanda tangan dari penerbit yang sama), lalu dijalankan senyap, persis seperti tombol Install update. Saatnya: **komputer tidak dipakai 10 menit** (`GetLastInputInfo`, sudah dibaca untuk tidur Gili), tidak ada aplikasi fullscreen, pulau compact (tidak ada kartu atau halaman terbuka), dan tidak ada alarm atau timer yang habis dalam 15 menit ke depan. Tombol `Install update` tetap memasang seketika.
- **Data**: installer tidak menyentuh `%LOCALAPPDATA%\Gili\` dan `%USERPROFILE%\.gili\`; `--quit` menjalankan jalur Quit biasa, yang menulis salinan `gili.db` dan `settings.json` ke `.gili\backups`; versi baru menyalin database ke `gili-before-v<N>.db` sebelum migrasi (desain data reinstall). Jadi note, todo, alarm, Shelf, dan pengaturan tetap ada.
- **Setelah update**: start pertama versi baru menampilkan kejadian wide `Updated to Gili 0.2.0` (ikon panah putar, hijau, 4 detik), dari `LastRunVersion` di `settings.json`; run pertama yang belum punya nilai itu tidak menampilkan apa pun.
- Mati: perilaku Fase 5d (titik biru dan tombol Install update).
- Batasan sekarang: cek update hanya menyala di `Gili.exe` bertanda tangan, dan sertifikat OV masih ditunda, jadi salinan harian di laptop ini belum memeriksa. Logika keputusan kapan memasang ada di Core (`UpdateRules.ShouldInstallNow`) dengan unit test.

## #28 Klik notifikasi membuka kartu

- Notifikasi Windows (F-ISL-27) tetap tampil dulu di pulau compact yang memanjang. **Klik pulau** selama notifikasi tampil tidak lagi langsung membawa aplikasi ke depan, tetapi membuka **kartu notifikasi** seukuran kartu alarm/timer (560 px, tinggi mengikuti isi 104–168 px, radius 28 px, tepi atas tetap): ikon aplikasi 20 px + nama aplikasi + `now`, judul semibold 15 px, isi lengkap sampai 4 baris (dipotong "…"), lalu tombol `Open <App>` (utama) dan `Dismiss`.
- Kartu dibuka pengguna, jadi diperlakukan seperti pulau yang dibuka pengguna: di atas prioritas 4–7 dan di bawah 1–3; menutup saat kursor pergi (2 detik), Esc, `Dismiss`, atau `Open <App>` (yang membawa aplikasi ke depan seperti klik lama). Notifikasi berikutnya di antrean menunggu sampai kartu tertutup (maks. 10 detik seperti sekarang).
- **Implementasi tetap Fase 6** bersama F-ISL-27: butuh `UserNotificationListener`, yang di versi GitHub perlu sparse package bertanda tangan sertifikat OV (masih ditunda), dan CLAUDE.md melarang fitur Fase 6 sebelum Fase 5 selesai. Issue #28 dibiarkan open dengan komentar yang menjelaskannya.

## Anggaran (PRD 8.1)

| Fitur | Biaya | Anggaran |
|---|---|---|
| Ikon tray (#25) | Satu ikon, pesan hanya saat diklik | Tidak terukur |
| Warna dan opacity bar (#35, #22, #30) | Brush baru hanya saat pengaturan berubah; desktop dari event foreground yang sudah ada | Tidak terukur |
| Thumbnail video (#31) | Satu decode per perubahan properti media | RAM ≤ +1 MB |
| Favorites (#36) | Ikon di-resolve sekali per aplikasi dan di-cache seperti deretan aplikasi | RAM ≤ +0,5 MB |
| Update otomatis (#27) | Satu unduhan installer per versi; cek idle ikut timer satu jam yang sudah ada, diperketat tiap menit hanya selama installer siap | Tidak terukur |
