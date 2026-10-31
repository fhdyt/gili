# PRD: Gili, top taskbar bergaya Dynamic Island untuk Windows

| | |
|---|---|
| Tanggal | 27 Oktober 2026 (revisi 8, dari issue GitHub: ikon Claude seragam dengan ikon lain, tombol Add in Calendar dihapus, ambient glow bergerak acak tanpa mengikuti kursor, halaman Clipboard yang menempel ke posisi kursor, saklar kiri ikut mematikan tab pulau, artwork musik di pulau compact, ukuran teks per note, note diseret keluar pulau sebagai satu benda tanpa patah, tetap seukuran kartunya sampai dilepas, dan bisa diseret dari editor, lirik lagu dari LRCLIB, notifikasi Windows di pulau, sudut note mengambang 12 px sama dengan kartu note; revisi 7: menu tab pulau ikon saja dengan nama di tab aktif dan saat hover, pulau terbuka 560 px; revisi 6: Gili dirilis gratis untuk umum lewat Microsoft Store dan GitHub, suhu CPU dihapus; revisi 5: editor note tumbuh dari kartunya, note mengambang di luar pulau, ekspresi Gili; revisi 4: maskot Gili, kontrol sistem, posisi bar, pengaturan tampil/sembunyi; nama aplikasi TopBar diganti Gili) |
| Status | Disetujui saat brainstorming, menunggu review dokumen |
| Platform | Windows 11 (laptop dan desktop), monitor utama |
| Pengguna | Publik: gratis, kode tertutup. Laptop pengembang tetap laptop uji utama |
| Distribusi | Microsoft Store (MSIX) dan installer bertanda tangan di GitHub Releases (9.4, Fase 5) |
| Teknologi | C# WPF, .NET 10 LTS |
| Bahasa UI aplikasi | Inggris saja |
| Mockup | [`docs/mockup/top-taskbar-mockup.html`](../../mockup/top-taskbar-mockup.html) |

Catatan: dokumen ini berbahasa Indonesia, tetapi semua teks yang tampil di aplikasi (label, notifikasi, tombol) berbahasa Inggris dan ditulis apa adanya di dokumen ini.

## 1. Ringkasan

Gili adalah bar kaca di tepi atas layar (bisa dipindah ke bawah), mirip panel desktop Linux, dengan "pulau" hitam pekat di tengah yang meniru Dynamic Island iPhone. Bar dibagi tiga: kiri untuk konteks kerja (tombol Start, launcher, dan aplikasi aktif), tengah untuk maskot **Gili** dan aktivitas yang sedang berjalan (media, timer, copy clipboard, progres file, status sesi Claude, batas pemakaian Claude 5 jam, ngecas), kanan untuk status sistem (cuaca, jaringan, CPU, RAM, Wi‑Fi), ikon Bluetooth, volume, dan brightness (masing-masing membuka pulau kontrolnya sendiri), ikon tray, jam dan tanggal, Settings, dan power.

Saat tidak ada aktivitas, pulau berisi sepasang mata Gili yang mengikuti kursor dan berkedip. Saat ada aktivitas, Gili berpindah ke kiri pulau sebagai lingkaran kecil, dan pulau menampilkan aktivitasnya.

Saat dibuka, pulau lepas dari bar menjadi panel hitam mengambang sedikit di bawah tepi atas. Latar bar menjadi transparan, isi kiri dan kanan tetap tampil, dan hanya elemen yang tertimpa panel yang memudar. Pulau punya halaman untuk media, timer, alarm, kalender, todo, note, riwayat clipboard (pilih satu teks dan teks itu langsung ditempel di posisi kursor), Shelf (tempat file sementara), sesi Claude, dan statistik sistem. Note ditulis langsung di pulau, dan satu note bisa diseret keluar menjadi jendela kecil yang selalu di atas jendela lain. Jendela Tools terpisah dipakai untuk pengelolaan: daftar alarm, edit todo, dan Settings.

Aplikasi jalan otomatis saat login, ringan, dan semua note, todo, alarm, serta isi Shelf tetap tersimpan setelah restart.

Gili dirilis gratis untuk umum (kode tetap tertutup) lewat Microsoft Store dan installer di GitHub Releases, keduanya bertanda tangan sehingga Windows tidak memperingatkan atau memblokirnya.

## 2. Masalah

- Taskbar Windows tidak menampilkan CPU, RAM, atau kecepatan internet tanpa membuka Task Manager.
- Kontrol musik dan video tersembunyi di flyout media Windows.
- Banyak laptop tidak punya lampu Caps Lock.
- Timer, alarm, todo, dan catatan tersebar di aplikasi berbeda (Clock, Microsoft To Do, Sticky Notes).
- Menyalin teks, gambar, atau file tidak memberi tanda apa pun.
- Memindahkan file antarfolder berarti bolak-balik di dua jendela Explorer.
- Saat Claude bekerja di jendela lain, tidak ada tanda kapan ia berhenti menunggu jawaban atau sudah selesai, jadi sesi sering diam menunggu tanpa disadari.

## 3. Tujuan dan bukan tujuan

**Tujuan**
1. Satu bar di atas layar yang menampilkan status sistem dan aktivitas berjalan tanpa perlu diklik.
2. Satu klik atau satu hotkey untuk media, timer, alarm, todo, note, dan Shelf.
3. Notifikasi waktu (alarm, timer, pengingat todo) yang sulit terlewat.
4. Data pribadi tersimpan lokal dan tahan restart atau mati listrik mendadak.
5. Sangat ringan: RAM di bawah 120 MB, CPU saat diam rata-rata di bawah 1%, termasuk saat mata maskot mengikuti kursor (8.1).
6. Status sesi Claude Code (bekerja, butuh jawaban, selesai) dan sisa jendela pemakaian 5 jam terlihat tanpa membuka jendelanya.
7. Bisa menggantikan taskbar Windows sehari-hari: tombol Start, ikon tray, dan kontrol Wi‑Fi/Bluetooth/volume/brightness ada di bar, jadi taskbar Windows boleh disembunyikan.
8. Setiap bagian bar dan setiap halaman pulau bisa ditampilkan atau disembunyikan di Settings.
9. Bisa dipasang orang lain tanpa peringatan SmartScreen atau deteksi antivirus: lewat Microsoft Store atau installer bertanda tangan di GitHub, tanpa perlu memasang .NET atau memberi hak admin.

**Bukan tujuan**
- Membuat ulang isi Start menu, notification area, atau Quick Settings Windows. Gili membuka Start menu bawaan, menampilkan ikon tray milik aplikasi lain, dan punya pulau kontrol untuk Wi‑Fi, Bluetooth, volume, brightness, dan power; sisanya tetap di Windows.
- Lebih dari satu monitor.
- Sinkronisasi cloud, aplikasi mobile, atau akun pengguna.
- Versi berbayar, langganan, iklan, atau kode sumber terbuka.
- Telemetri dan analitik pemakaian.
- Windows 10.
- Bahasa UI selain Inggris.
- Riwayat clipboard untuk gambar/file; note berformat kaya.

## 4. Skenario pengguna

1. **Pagi hari**: laptop menyala, bar langsung muncul. Ring progres di pulau menunjukkan 2 dari 6 todo hari ini selesai.
2. **Bekerja sambil mendengar musik**: Spotify jalan, pulau menampilkan artwork kecil lagunya, judul lagu, dan tombol pause. Klik membuka player lengkap; pulau menutup sendiri saat mouse pergi.
3. **Menonton kuliah**: video diputar di VLC, pulau menampilkan judul dan garis progres tipis.
4. **Menyalin**: Ctrl+C pada screenshot, pulau sedikit memanjang dan menampilkan thumbnail gambarnya selama 3 detik. Menyalin folder 4 GB di Explorer membuat pulau sedikit memanjang dan menampilkan progres 0–100%.
5. **Memindahkan file**: pengguna menyeret tiga file dari Downloads ke pulau, membuka folder tujuan, lalu menyeret file-file itu keluar dari halaman Shelf.
6. **Fokus**: pomodoro 25 menit dimulai, pulau menghitung mundur, lalu kartu "Focus session done" muncul saat habis.
7. **Ide mendadak**: Ctrl+Alt+N membuka halaman Notes dengan note baru kosong dan kursor siap mengetik. Tulisan tersimpan otomatis; Esc menutup pulau.
8. **Alarm**: jam 14:30 pulau terbuka dengan kartu alarm (Snooze 5m / Stop), suara berbunyi, toast masuk Notification Center.
9. **Bekerja dengan Claude**: pengguna memberi tugas di Claude Desktop lalu pindah ke browser. Pulau menampilkan nama sesi dan waktu berjalan. Saat Claude bertanya, kartu `Claude needs your input` muncul dengan cuplikan pertanyaan dan bunyi Ping; **Open Claude** membawa Claude Desktop ke depan. Saat selesai, pulau menampilkan `Done · 4m 12s`, lalu gelembung hijau menunggu sampai Claude Desktop dibuka.
10. **Batas pemakaian Claude**: prompt pertama pagi itu memulai jendela 5 jam. Pulau menghitung mundur `Resets in 3:12:40`; saat batas tercapai, slot berubah kuning `Limit reached`. Saat jendela berakhir, kartu `Claude usage reset` muncul dengan bunyi seperti timer.
11. **Meja kosong**: tidak ada yang berjalan. Pulau hanya berisi dua mata Gili yang melirik ke arah kursor dan sesekali berkedip. Begitu musik diputar, Gili meluncur ke kiri menjadi lingkaran kecil dan pulau menampilkan judul lagu.
12. **Menempel ulang**: pengguna sedang mengetik email dan butuh alamat yang dicopy tadi pagi. Klik ikon Clipboard di kiri bar (atau `Ctrl+Alt+V`), pilih alamat itu, dan teksnya langsung muncul di posisi kursor di email; pulau menutup sendiri.

## 5. Keputusan desain utama

| Topik | Keputusan |
|---|---|
| Bentuk bar | Bar selebar monitor utama, didaftarkan sebagai AppBar (memesan 32 px; jendela maximize mulai di bawahnya). Posisi atas (default) atau bawah; posisi bawah hanya bisa dipilih setelah taskbar Windows disembunyikan |
| Material bar | Default opacity 100% (tint pekat di atas kaca). Opacity dan kekuatan blur bisa diatur |
| Maskot | Gili: dua mata putih di pulau saat tidak ada aktivitas, lingkaran kecil di kiri pulau saat ada aktivitas. Mata mengikuti kursor dan berkedip |
| Material pulau | Hitam pekat di semua keadaan |
| Pulau saat terbuka | Panel mengambang dengan sudut membulat penuh; tepi atasnya tetap di tempat tepi atas pulau compact (tidak turun), lalu membesar ke bawah. Hanya latar bar (warna dan kaca) yang menjadi transparan; isi kiri dan kanan tetap tampil kecuali yang tertimpa panel |
| Taskbar Windows | Default tidak diubah. Pengaturan **Hide Windows taskbar** menyembunyikannya; Gili lalu menyediakan tombol Start, ikon tray, dan pulau kontrol (Wi‑Fi, Bluetooth, volume, brightness, power). Taskbar Windows dipulihkan di semua jalur keluar |
| Tampil/sembunyi | Tombol Start, tiap ikon launcher, Shelf, Claude, tiap halaman pulau, tiap modul kanan (termasuk Wi‑Fi, Bluetooth, Volume, Brightness), tray, dan jam bisa dinyalakan/dimatikan di Settings |
| Monitor | Monitor utama saja, juga di rilis publik pertama |
| Distribusi | Gratis untuk umum, kode tertutup. Microsoft Store (paket MSIX yang ditandatangani Microsoft, update lewat Store) dan installer di GitHub Releases yang ditandatangani sertifikat code signing OV milik pengembang. Windows 11 saja. Tanpa komponen yang butuh hak admin |
| Aksi cepat vs pengelolaan | Aksi cepat, menulis note, riwayat clipboard, dan Shelf ada di pulau; daftar alarm, edit todo, dan Settings ada di jendela Tools |
| Notifikasi | Kartu di pulau + toast Windows (toast tanpa suara supaya bunyi tidak dobel) |
| Integrasi Claude | Status sesi Claude Code (tab Code di Claude Desktop dan CLI di terminal) dibaca lewat hook Claude Code dan helper kecil; opt-in di Settings. Tab Chat Claude Desktop tidak didukung |
| Bahasa UI | Inggris saja, tanpa pilihan bahasa |
| Teknologi | C# WPF di .NET 10 LTS (.NET 8 habis dukungan 10 Nov 2026) |
| Penyimpanan | SQLite + `settings.json` di folder data lokal Gili (lokasinya per jenis instalasi, bagian 10) |

## 6. Tata letak dan perilaku

### 6.1 Bar

- Tinggi default 32 px logis (ikut skala DPI), selebar monitor utama.
- **Tinggi bisa diatur** di Settings:
  - **Bar height**: 28–44 px, default 32 px.
  - **Island height** (tinggi pulau compact dan wide): 20 px sampai tinggi bar − 4 px, default 24 px. Pulau selalu berada di tengah tinggi bar; radius sudutnya setengah tinggi pulau; ukuran teks di pulau mengikuti (setengah tinggi pulau, 11–15 px). Gelembung aktivitas ikut setinggi pulau.
  - Perubahan langsung berlaku dengan animasi pegas; ruang AppBar didaftarkan ulang dengan tinggi baru.
- Terdaftar sebagai AppBar (`SHAppBarMessage`) di tepi atas atau bawah, jadi area kerja Windows berkurang setinggi bar.
- **Material**: warna gelap di atas latar yang di-blur. Default: tint `#161618` dengan **opacity 100%**, kaca "Strong" (kaca baru terlihat setelah opacity diturunkan). Pengaturan:
  - **Bar opacity**: 40–100%, default 100%.
  - **Glass**: Off (tint polos), Light (blur-behind), Strong (acrylic). Windows tidak menyediakan pengaturan radius blur bebas, jadi tiga tingkat ini dipetakan ke efek blur-behind dan acrylic bawaan sistem.
  - Kalau "Transparency effects" Windows mati, bar digambar polos apa pun pengaturannya.
- Pulau selalu `#000000` pekat, jadi terlihat sebagai benda paling padat di bar.
- Disembunyikan saat aplikasi fullscreen aktif di monitor utama (`ABN_FULLSCREENAPP`), muncul lagi setelah keluar.
- Klik kanan di area kosong bar membuka menu: Open Tools, Settings, Demo mode, Quit.
- **Aturan jarak**: isi kiri dan kanan tidak boleh lebih dekat dari 24 px ke pulau, termasuk lingkaran Gili, titik kamera/mic, dan gelembung aktivitas di sampingnya.

**Posisi bar** (Settings → General → **Bar position**: `Top` / `Bottom`, default Top)
- Bottom memindahkan AppBar ke tepi bawah (`ABE_BOTTOM`). Pilihan ini nonaktif selama taskbar Windows masih tampil, dengan keterangan `Hide the Windows taskbar first`, karena dua bar di tepi bawah akan bertumpuk.
- Di bawah, semua yang mengambang dicerminkan secara vertikal: pulau yang terbuka, kartu, dan drop zone menjaga tepi **bawah** pulau compact dan membesar ke atas; panel tray terbuka ke atas. Isi pulau (menu tab di atas, halaman di bawahnya) tidak dibalik.
- Pindah posisi berlaku langsung: bar keluar dari tepi lama dan masuk dari tepi baru (pegas compact ↔ wide), lalu ruang AppBar didaftarkan ulang.
- Kalau taskbar Windows dimunculkan lagi saat bar di bawah, bar otomatis pindah ke atas dan Settings menampilkan `Moved to the top because the Windows taskbar is back`.

**Menyembunyikan taskbar Windows** (Settings → General → **Hide Windows taskbar**, default mati)
- Saat nyala: taskbar Windows (`Shell_TrayWnd`) diubah ke auto-hide lalu disembunyikan, jadi ruangnya kembali ke jendela lain dan tidak muncul saat kursor menyentuh tepi bawah. Tombol Windows di keyboard tetap membuka Start menu.
- Status asli taskbar (auto-hide atau tidak) disimpan sebelum diubah dan dikembalikan saat saklar dimatikan, saat Quit, saat crash (handler exception), dan saat Gili mulai lagi setelah mati paksa.
- Saat Explorer restart (`TaskbarCreated`), taskbar disembunyikan lagi.
- Toast Windows tetap muncul di pojok kanan bawah seperti biasa. Jarak ini dihitung dari ukuran pulau saat di-hover (6.3), supaya isi kiri dan kanan tidak bergeser ketika kursor berada di atas pulau. Modul kanan yang tidak muat pindah ke menu "…" (6.4); di kiri, ikon aplikasi yang tidak aktif pindah ke `+N` lalu nama aplikasi aktif dipotong dengan "…" (6.2).

### 6.2 Bagian kiri

Urutan dari kiri: tombol Start, ikon launcher, ikon Shelf, ikon Claude, **pemisah** (garis vertikal tipis), lalu ikon aplikasi yang sedang berjalan. Indikator kamera/mic tidak di sini, tetapi di samping pulau (6.3).

| Elemen | Perilaku |
|---|---|
| Start | Paling kiri: logo Windows 16 px dalam tombol 28 × 24 px. Klik membuka Start menu Windows (sama seperti menekan tombol Windows); klik lagi saat Start terbuka menutupnya. Hover: `Start`. Default tampil; bisa disembunyikan di Settings. Start menu muncul di posisi bawaan Windows |
| Launcher | Lima ikon, urut seperti menu tab pulau: Timer, Alarms, Todo, Notes, Clipboard. Masing-masing membuka halamannya di pulau (Timer, Alarms, Todo, Notes, Clipboard). Kalau pulau sudah terbuka, klik pindah ke halaman itu; klik ikon halaman yang sedang terbuka menutup pulau. Pengelolaan lengkap tetap di jendela Tools (tombol Edit / Manage di halaman pulau). |
| Shelf | Ikon tray tepat setelah launcher, selalu tampil. Kalau Shelf berisi, diikuti jumlah file (`3`). Ikon memantul kecil (skala 1,25, pegas 380 ms) saat file baru masuk. Klik membuka halaman Shelf di pulau; hover menampilkan `Shelf · 3 files`; file bisa diseret ke ikon ini seperti ke bagian bar lain. |
| Claude | Glyph Claude tepat setelah Shelf, selalu tampil saat integrasi Claude nyala. Warnanya sama dengan ikon launcher lain (abu-abu terang `#AEAEB2`, putih saat hover), bukan oranye Claude, supaya deretan ikon kiri seragam; warna status hanya ada di titiknya. Status mengikuti sesi Claude Code (F-CLD): titik kuning di pojok saat ada sesi yang butuh input, glyph berdenyut pelan saat ada sesi bekerja, titik hijau saat ada sesi selesai yang belum dilihat. Klik membuka halaman Claude Sessions di pulau; hover menampilkan ringkasan (`Claude · 1 needs input`). Sampai Fase 3 ikon ini tampil tanpa status sebagai placeholder dan membuka tab Claude yang berisi `Coming in Phase 3` (keputusan pengguna 2026-10-27). |
| Pemisah | Garis 1 × 14 px (putih 22%) yang memisahkan grup launcher + Shelf + Claude dari aplikasi. |
| Aplikasi berjalan | Semua aplikasi yang punya jendela di taskbar Windows tampil sebagai **ikon asli** 16 px, urut sesuai waktu pertama dibuka. **Aplikasi aktif selalu menampilkan nama**, dalam kapsul tipis di belakang ikonnya; aplikasi lain ikon saja (opacity 70%). **Hover langsung menampilkan nama** tanpa jeda tooltip: nama melebar dari ikon dalam kapsul seperti aplikasi aktif, ikon di kanannya bergeser, lalu nama menyusut lagi saat kursor keluar. Aplikasi yang di-hover hanya berganti saat kursor benar-benar bergerak, bukan karena ikon bergeser di bawah kursor yang diam. Nama hover hanya memakai ruang sisa setelah nama aplikasi aktif (dipotong "…" kalau perlu), jadi aturan jarak 24 px tetap berlaku; kalau sisanya kurang dari 40 px, nama tampil sebagai tooltip biasa. Klik ikon membawa aplikasi itu ke depan. Kalau ikon tidak bisa diambil, tampil ikon jendela generik. |

**Tampil/sembunyi** (Settings → Left side): Start, Timer, Alarms, Todo, Notes, Clipboard, Shelf, dan Claude masing-masing punya saklar (default semua nyala; Claude hanya saat integrasinya nyala). Pemisah hanya tampil kalau ada ikon di kirinya. Ikon yang disembunyikan tidak menyisakan celah; deretan merapat dengan gerak yang sama seperti aplikasi yang ditutup.

**Saklar kiri ikut mematikan tab pulau** (revisi 8): Timer, Alarms, Todo, Notes, Clipboard, Shelf, dan Claude punya halaman pulau dengan nama yang sama. Mematikan salah satunya di Settings → Left side juga mematikan tab halaman itu di menu pulau (saklarnya di Settings → Island ikut mati), dan menyalakannya lagi menyalakan keduanya. Selama ikon kirinya mati, saklar tab itu di Settings → Island nonaktif dengan keterangan `Turned off in Left side`. Sebaliknya tidak berlaku: mematikan tab di Settings → Island tidak menyembunyikan ikon kirinya. Start tidak punya halaman, jadi tidak memengaruhi tab. Halaman yang tabnya mati tetap bisa terbuka lewat slot, hotkey, atau kartu dengan tab sementara (6.3 "Tab bisa disembunyikan"). Kalau mematikan ikon akan menghilangkan tab terakhir yang masih nyala, ikonnya tetap hilang tetapi tabnya tetap tampil, karena minimal satu tab harus nyala.

**Gerak saat berpindah aplikasi**: nama aplikasi lama menyusut (lebar ke 0, fade 160 ms) sementara nama aplikasi baru melebar dari ikonnya (300 ms ease-out, fade masuk mulai 90 ms), dan kapsul latarnya berpindah bersamaan; ikon lain ikut bergeser mulus. Aplikasi yang baru dibuka muncul di ujung kanan deretan dengan pegas (skala 0,6 → 1, 320 ms); aplikasi yang ditutup mengecil lalu hilang dan ikon lain merapat.

**Ruang sempit / banyak aplikasi terbuka**: ikon aplikasi tidak aktif dari ujung kanan pindah ke penanda `+N` (kapsul kecil, angka tabular); aplikasi aktif selalu tampil, dan namanya baru dipotong "…" setelah tidak ada ikon lain yang bisa dipindahkan. Hover `+N` menampilkan nama-nama aplikasinya sebagai tooltip; **klik `+N` membuka daftar** (panel kecil seperti panel tray: ikon + nama per baris, urutan sama dengan deretan) dan klik satu baris membawa aplikasi itu ke depan. Aplikasi yang dibawa ke depan dari daftar menjadi aplikasi aktif dan tampil di deretan, sementara ikon tidak aktif paling kanan bergantian masuk ke `+N`. Jumlah `+N` berubah dengan gerak angka yang sama seperti jumlah Shelf.

Cuplikan clipboard (copy/cut teks, gambar, file) dan progres copy, move, dan delete dari Explorer tidak tampil di bagian kiri, tetapi di slot pulau (6.3).

### 6.3 Pulau tengah

#### Keadaan

| Keadaan | Posisi | Isi | Bagian bar lainnya |
|---|---|---|---|
| Compact | Di dalam bar, kapsul hitam setinggi pengaturan "Tinggi pulau" (default 24 px), di tengah tinggi bar | Mata Gili saat tidak ada aktivitas; saat ada aktivitas hanya aktivitas itu, dan Gili pindah ke lingkaran di kiri pulau. Lebar mengikuti isinya, minimal 200 px | Tampil |
| Wide | Di dalam bar, kapsul lebih panjang | Kejadian singkat: Caps/Num Lock, charger, peringatan baterai, dan notifikasi Windows (sampai 560 px, lihat "Notifikasi Windows") | Tampil; hanya notifikasi Windows yang memudarkan elemen yang tertimpa |
| Expanded | Panel mengambang dengan tepi atas sama dengan tepi atas pulau compact (lihat "Posisi saat mengambang"), **lebar mengikuti menu tab** (lihat "Lebar pulau terbuka"), sama untuk semua halaman termasuk Notes; **tinggi mengikuti isi halaman** (lihat "Tinggi pulau terbuka"), minimal 256 px, radius sudut 32 px di semua halaman | Lingkaran Gili dan menu tab (ikon; nama di tab aktif dan tab yang di-hover), lalu halaman | Latar transparan; elemen tertimpa memudar |
| Kontrol | Panel mengambang dengan tepi atas sama dengan pulau compact, lebar 360 px (Volume, Brightness, Power) atau 400 px (Wi‑Fi, Bluetooth), tinggi mengikuti isi, radius 28 px | Satu kontrol saja, tanpa menu tab (lihat "Pulau kontrol" di 6.4) | Latar transparan; elemen tertimpa memudar |
| Kartu notifikasi | Panel mengambang sekitar 560 × 104 px, radius 28 px, tepi atas sama dengan pulau compact | Satu notifikasi dengan tombol aksi | Latar transparan; elemen tertimpa memudar |
| Drop zone | Panel mengambang sekitar 420 × 140 px, radius 30 px, tepi atas sama dengan pulau compact | Target "Drop to keep on the Shelf" saat file diseret ke atas bar | Latar transparan; elemen tertimpa memudar |

**Posisi saat mengambang**
- Tepi atas pulau tidak bergerak saat pulau membuka. Expanded, pulau kontrol, kartu, dan drop zone memakai tepi atas yang sama dengan pulau compact, yaitu (tinggi bar − tinggi pulau) / 2 dari tepi atas layar (4 px pada bar 32 px dan pulau 24 px). Pulau hanya melebar ke samping dan memanjang ke bawah, jadi terlihat tumbuh dari pulau kecil, bukan turun.
- Di posisi bar bawah, yang tetap adalah tepi bawahnya, dan pulau memanjang ke atas.

**Bar saat pulau mengambang**
- Latar bar (warna, blur kaca, garis bawah) memudar sampai transparan penuh, jadi wallpaper terlihat di area bar.
- Isi kiri dan kanan **tetap tampil dan tetap bisa diklik**. Teksnya diberi bayangan tipis (hitam 55%, blur 3 px) supaya tetap terbaca di atas wallpaper terang.
- Hanya elemen yang posisinya tertimpa panel pulau, ditambah jarak 8 px di kiri dan kanan panel, yang memudar (opacity 0, 160 ms) dan tidak bisa diklik. Elemen dinilai satu per satu (tiap ikon launcher, aplikasi aktif, tiap modul kanan), jadi di layar lebar biasanya tidak ada yang hilang.
- Saat pulau kembali compact, latar bar dan semua elemen muncul lagi.

Keadaan mengambang memakai bayangan lembut dan garis cahaya tipis di tepi atas, supaya panel hitam tetap terpisah dari jendela gelap di belakangnya.

#### Isi compact

- **Saat tidak ada aktivitas**: mata Gili (lihat "Maskot Gili" di bawah). Jam dan tanggal tidak lagi di pulau; keduanya ada di bagian kanan bar (6.4).
- **Saat ada aktivitas**: pulau hanya berisi satu aktivitas dengan prioritas tertinggi, dan Gili pindah ke lingkaran di kiri pulau. **Lebar pulau mengikuti isi aktivitasnya, tetapi tidak pernah kurang dari 200 px** (selebar notifikasi `Charging · 64%`), dan berubah dengan pegas saat isinya berganti. Lebar minimal ini berlaku juga saat tidak ada aktivitas dan untuk timer, pomodoro, dan stopwatch; aktivitas yang lebih pendek direntang dari ujung ke ujung (ikon di kiri, nilai di kanan). Gili kembali ke dalam pulau saat aktivitas terakhir selesai.
- Urutan prioritas aktivitas:
  1. Cuplikan clipboard, 3 detik (lihat "Cuplikan clipboard" di bawah)
  2. Sesi Claude yang butuh jawaban, atau hasil `Done` selama 4 detik (lihat "Sesi Claude" di bawah)
  3. Operasi file Explorer (copy, move, delete): ikon berwarna + nama file + garis progres 44 px + persen (lihat "Operasi file Explorer" di bawah)
  4. Timer atau pomodoro berjalan: ikon + sisa waktu `12:04`. Pomodoro yang berjalan menang; kalau tidak ada, slot menampilkan timer yang paling cepat habis di antara timer pulau dan timer berlabel dari jendela Tools (7.5, Keputusan 2)
  5. Stopwatch berjalan: ikon + waktu berjalan
  6. Sesi Claude yang sedang bekerja: ikon Claude berdenyut + nama sesi + waktu berjalan `2:14`
  7. Sesi media:
     - Musik: **artwork lagu** + judul dan artis (maks. 36 karakter) + bar level bergerak + tombol play/pause (lihat "Artwork musik" di bawah)
     - Video: ikon film + judul (maks. 32 karakter) + garis progres 36 px
  8. Sedang dicas: ikon petir + persen
  9. Jendela pemakaian Claude 5 jam yang sedang berjalan: ikon jam pasir oranye Claude + `Resets in` + `3:12:40` (lihat "Batas pemakaian Claude"). Saat batas sudah tercapai, aktivitas ini naik ke posisi tepat setelah sesi Claude yang butuh jawaban (posisi 2)
- Media yang di-pause tetap memegang slot selama 2 menit setelah sebelumnya diputar, lalu slot dilepas; session yang ditemukan dalam keadaan pause saat Gili mulai tidak memegang slot.
- **Artwork musik** (revisi 8): selama sesi musik tampil di slot, ujung kiri slot menampilkan artwork lagu yang sedang diputar dari pemutar mana pun yang mengirimnya ke Windows (Spotify, YouTube Music dan YouTube di Chrome/Edge, Apple Music, Media Player, dan lainnya).
  - Ukuran tinggi pulau − 6 px (18 × 18 px pada pulau 24 px), jadi sisa 3 px di atas dan bawah; radius 5 px seperti sampul album kecil, dengan garis tepi tipis putih 14% supaya artwork gelap tetap terpisah dari pulau hitam. Artwork berada di dalam padding slot, di luar lengkung sudut pulau, jadi tidak memakai radius konsentris.
  - Berganti dengan cross-fade 180 ms saat lagu berganti; warna bar level di slot diambil dari warna dominan artwork (sama dengan ambient glow halaman Media).
  - Tanpa artwork (pemutar tidak mengirimnya, iklan, siaran radio): kotak `#1C1C1E` berisi ikon not abu-abu dengan ukuran yang sama, jadi lebar pulau tidak melompat.
  - Saat musik menjadi aktivitas kedua, gelembungnya di kanan pulau berisi artwork yang sama sebagai lingkaran (gelembung − 6 px), bukan ikon not.
  - Video tidak menampilkan gambar di slot (tetap ikon film), sama seperti di halaman Media.
- Isi Shelf bukan aktivitas pulau; jumlahnya tampil di ikon Shelf di bagian kiri bar (6.2), jadi Gili tidak pindah hanya karena ada file di Shelf.
- Aktivitas berjalan kedua tampil sebagai gelembung bundar kecil di kanan pulau (ikon saja). Klik gelembung membuka halaman aktivitas itu. Gelembung hijau Claude "selesai, belum dilihat" (lihat "Sesi Claude") selalu paling kanan dan bukan aktivitas, jadi Gili tetap di dalam pulau.
- **Hover**: saat kursor berada di atas pulau compact, pulau memanjang 16 px (8 px di tiap sisi) dengan pegas, lalu kembali ke ukuran semula saat kursor pergi. Ini tanda bahwa pulau bisa diklik. Lingkaran Gili, gelembung aktivitas, dan titik kamera/mic ikut bergeser. Hover tidak berlaku saat pulau sedang wide atau mengambang.

#### Maskot Gili

Maskot ini memakai nama aplikasinya, Gili: sepasang mata putih yang hidup di pulau (sketsa pengguna: `MASKOT GILI.jpg`, gerak mata: `PERGERAKAN MATA GILI.jpg`). Gili punya tiga bentuk.

| Bentuk | Kapan | Rupa |
|---|---|---|
| Di dalam pulau | Compact tanpa aktivitas | Dua kotak putih membulat, masing-masing 10 × 10 px (radius 3 px, celah 2 px) pada tinggi pulau 24 px; ukuran ikut tinggi pulau (sisi mata = 42% tinggi pulau). Saat diam di tengah pulau; mengikuti kursor, mata **berjalan sepanjang pulau** dari ujung kiri sampai ujung kanan (sisa jarak 9 px ke tepi). Pulau hitam selebar 200 px |
| Lingkaran | Compact dengan aktivitas, dan wide | Lingkaran hitam setinggi pulau (24 × 24 px), 6 px di kiri pulau, berisi dua mata 6 × 6 px (radius 2 px, celah 2 px) |
| Di samping menu | Pulau terbuka dengan menu tab (bukan pulau kontrol) | Lingkaran 36 × 36 px, setinggi menu tab, dengan latar yang sama dengan menu (putih 10%), 14 px dari tepi atas dan kiri pulau sehingga konsentris dengan sudut pulau seperti menu (18 + 14 = 32 px). Menu tab mulai 6 px di kanannya. Dua mata 9 × 9 px (radius 3 px, celah 3 px) |

- **Mata mengikuti kursor (di dalam pulau)**: posisi mendatar kedua mata mengikuti posisi kursor di layar relatif terhadap tengah pulau. Kursor di tengah layar = mata di tengah pulau; kursor 40% lebar layar (atau lebih) ke kiri = mata di ujung kiri pulau, 9 px dari tepi; ke kanan = ujung kanan. Di antaranya sebanding. Saat pulau melebar karena hover, jangkauannya ikut melebar.
- **Mata naik dan turun**: posisi tegak mengikuti tinggi kursor di layar: kursor di bagian atas layar = mata naik, di tengah layar = mata di tengah, di bagian bawah = mata turun (penuh pada 40% tinggi layar dari tengah). Di dalam pulau mata bisa bergeser sampai menyisakan 3 px ke tepi atas/bawah pulau (4 px pada tinggi pulau 24 px). Acuannya layar, bukan pulau, supaya mata tidak selalu menatap ke bawah karena bar ada di tepi atas.
- **Mata mengikuti kursor (lingkaran)**: lingkaran terlalu kecil untuk berjalan, jadi mata hanya melirik: maksimal 2 px ke kiri/kanan (penuh saat kursor 400 px atau lebih dari lingkaran) dan 2 px ke atas/bawah (aturan tinggi layar yang sama). Lingkaran di samping menu memakai aturan yang sama dengan jangkauan 3 px, jadi Gili tetap mengikuti kursor selama pulau terbuka.
- Kedua bentuk diperhalus dengan pegas (damping 0,8, response 0,25 dtk), jadi mata menyusul kursor dengan lembut, tidak meloncat. Saat kursor diam lebih dari 8 detik, mata pelan-pelan kembali ke tengah.
- **Berkedip**: tiap 3–6 detik (acak), kedua mata menutup lalu membuka: tinggi mata 100% → 10% dalam 90 ms (ease-in), lalu kembali ke 100% dalam 140 ms (ease-out), jadi kedipan terasa lembut, tidak patah. Sesekali (1 dari 5 kedipan) kedip dua kali berturut-turut dengan jeda 120 ms. Kedipan tidak terjadi saat Gili sedang berpindah bentuk, tidur, kaget, senang, atau bermata besar karena menyeret (lihat "Ekspresi Gili").
- **Kedip saat klik**: setiap klik tombol kiri atau kanan mouse, di mana pun di layar, membuat Gili langsung berkedip sekali dengan kedipan yang sama, mulai saat tombol ditekan. Klik beruntun memulai ulang kedipan. Klik saat Gili tidur membangunkannya (kedip dua kali). Pengecualiannya sama dengan kedipan acak.
- **Berpindah bentuk**: saat aktivitas pertama muncul, mata mengecil dan meluncur ke kiri keluar dari pulau, lalu lingkaran Gili "menetes" dari ujung kiri pulau ke posisinya 6 px di kiri (pegas damping 0,8, response 0,45 dtk; lingkaran mulai dari skala 0,6 dan menempel di tepi pulau). Bersamaan, isi aktivitas muncul di pulau. Saat aktivitas terakhir selesai, gerak dibalik: lingkaran merapat ke pulau, menyatu, dan matanya membesar kembali di ujung kiri pulau. Semua perpindahan bisa disela dari posisi yang sedang tampil.
- **Saat pulau mengambang** (expanded, kartu, drop zone): lingkaran Gili di kiri pulau mengecil ke skala 0 dan muncul lagi saat pulau kembali compact. Di pulau terbuka dengan menu tab, Gili pindah ke dalam pulau, di kiri menu, dan muncul bersama isi pulau (gerak "Isi pulau muncul" di 8.3); di pulau kontrol tidak ada Gili. Pengecualian: di samping kartu alarm, timer, pomodoro, dan pengingat todo, lingkaran tetap tampil 6 px di kiri atas kartu dengan mata kaget (lihat "Ekspresi Gili"); titik kamera/mic turun ke bawah lingkaran.
- **Klik Gili** (di pulau maupun lingkaran): sama dengan klik pulau saat tidak ada aktivitas, yaitu membuka halaman terakhir yang dilihat.
- **Titik kamera/mic** berada di kiri lingkaran Gili (jarak 6 px), bukan langsung di kiri pulau, selama lingkaran tampil.
- **Animation effects mati**: mata tidak mengikuti kursor dan tetap di tengah; kedipan menjadi fade tinggi mata 100 ms; perpindahan bentuk menjadi cross-fade 100 ms.
- **Ringan**: posisi kursor dibaca dengan `GetCursorPos` 30 kali per detik hanya selama Gili tampil dan bar terlihat; mata digambar ulang hanya kalau posisi targetnya berubah ≥ 0,25 px. Saat layar mati, sleep, atau fullscreen, pembacaan berhenti. Tanpa hook mouse global.
- Settings → Island → **Show Gili** (default nyala). Kalau mati, pulau tanpa aktivitas berisi kapsul hitam kosong 200 px dan tidak ada lingkaran di kiri pulau.

**Ekspresi Gili**

Mata Gili berubah bentuk mengikuti apa yang terjadi di pulau. Hanya satu ekspresi yang tampil, yaitu yang paling atas di tabel yang sedang berlaku; ekspresi berlaku untuk kedua bentuk (di dalam pulau dan lingkaran). Arah pandang diatur terpisah di bawah tabel, jadi mata yang sedang berekspresi tetap bisa melirik.

| # | Ekspresi | Kapan | Rupa |
|---|---|---|---|
| 1 | Kaget | Kartu alarm, timer, pomodoro (fokus atau istirahat selesai), atau pengingat todo sedang tampil | Mata membesar 130% dan membulat penuh. Lingkaran Gili tetap tampil di kiri atas kartu, bukan mengecil. Tidak berkedip. Kembali saat kartu ditanggapi |
| 2 | Senang | 0,9 detik saat note mengambang masuk kembali ke pulau yang tertutup ("Note mengambang") | Mata menjadi lengkung (setengah bawah hilang, sudut atas membulat), seperti tersenyum dengan mata. Tidak berkedip |
| 3 | Mata besar (menyeret) | Tombol kiri mouse ditahan lalu kursor bergerak ≥ 6 px, di mana pun di layar (menyeret file, jendela, teks, kartu note, apa saja) | Mata membesar 130% dan menjadi lingkaran penuh, sama dengan rupa ekspresi Kaget. Tidak berkedip. Kembali saat tombol dilepas |
| 4 | Tidur | Tidak ada gerakan kursor **dan** tidak ada ketikan selama 5 menit | Mata terpejam menjadi garis setinggi 16% (turun 1 px) dan berhenti mengikuti kursor; di dalam pulau muncul huruf `z` kecil abu-abu (8 px) di kanan atas mata. Tidak berkedip dan **tidak ada animasi berjalan** selama tidur. Gerakan kursor atau tombol apa pun membangunkannya: mata terbuka lalu kedip dua kali |
| 5 | Fokus | Aktivitas di slot adalah timer, stopwatch, atau fase fokus pomodoro yang berjalan | Mata menyipit ke 70% tinggi. Setiap kali angka menit berganti (sisa menit timer/pomodoro, atau menit stopwatch), mata melirik ke angka di slot selama 1,2 detik lalu kembali mengikuti kursor; paling sering sekali per 4 detik |
| 6 | Menikmati musik | Aktivitas di slot adalah musik yang sedang diputar (bukan video, bukan saat dijeda) | Mata setengah terpejam (setengah atas hilang) dan **mengangguk-angguk**: setiap 0,48 detik kedua mata turun cepat sejauh 22% ukuran mata (sekitar 2 px di pulau, 1,3 px di lingkaran) lalu naik lagi, seirama dengan bar level di slot; di antara ketukan mata diam |
| — | Netral | Selain di atas | Seperti biasa: mengikuti kursor dan berkedip |

- **Fase istirahat pomodoro** memakai ekspresi netral. Saat musik dan timer berjalan bersamaan, ekspresi mengikuti aktivitas yang tampil di slot (urutan aktivitas di 6.3), jadi timer menang atas musik.
- **Tidur menang atas fokus dan musik**: pengguna yang pergi saat musik atau timer berjalan membuat Gili tertidur, dan anggukan musik ikut berhenti. Alarm yang berbunyi saat Gili tidur langsung membuatnya kaget.
- **Tidur dihitung dari kursor dan keyboard**, bukan kursor saja, supaya Gili tidak tertidur saat pengguna sedang mengetik tanpa menyentuh mouse.
- **Arah pandang** (urutan menang): (1) note mengambang yang sedang bergerak, yaitu saat jendelanya diseret dari bagian atasnya atau saat terbang kembali ke pulau, diikuti terus sampai berhenti; (2) setelah note dilepas keluar pulau, pulau menutup dan mata menatap note itu selama 1,5 detik; (3) lirikan menit pada ekspresi Fokus; (4) kursor. Selama tidur mata diam di tengah.
- Ekspresi berganti dengan pegas membuka pulau (bentuk mata) dan 200 ms untuk radius; tidur memejam pelan dalam 900 ms, lalu `z` muncul 600 ms kemudian.
- **Animation effects mati**: ekspresi berganti tanpa animasi, tidak ada anggukan musik, lirikan langsung pindah tanpa pegas.
- **Ringan**: waktu input terakhir dibaca dengan `GetLastInputInfo` sekali per detik (tanpa hook). Klik dan seret dibaca dengan `GetAsyncKeyState` untuk tombol kiri dan kanan di pembacaan kursor 30 kali per detik yang sudah ada, bukan dengan hook mouse global; bit "ditekan sejak pembacaan terakhir" menangkap klik yang lebih singkat dari satu pembacaan. Karena ikut pembacaan kursor, klik hanya terbaca selama Gili tampil. ekspresi hanya dihitung ulang saat aktivitas, kartu, atau status tidur berubah. Satu-satunya gerak berulang adalah anggukan musik, dan itu hanya berjalan selama musik diputar dan Gili terlihat.

#### Lirik lagu (revisi 8)

Lirik lagu yang sedang diputar tampil di pulau, baris demi baris tepat pada waktunya.

**Sumber lirik**
- Spotify tidak menyediakan lirik untuk aplikasi lain: Web API-nya tidak punya endpoint lirik, dan endpoint lirik internal yang dipakai aplikasi Spotify butuh cookie login pengguna serta melanggar ketentuan Spotify. Gili tidak memakainya, sesuai aturan bahwa Gili tidak membaca data aplikasi lain secara tidak resmi.
- Gili memakai **LRCLIB** (`lrclib.net`): layanan lirik terbuka, gratis, tanpa API key, yang menyediakan lirik bersinkron dalam format LRC (setiap baris punya stempel waktu).
- Lagu dicari dari metadata sesi media Windows (judul, artis, album, durasi), jadi lirik bekerja untuk **pemutar musik mana pun**: Spotify, YouTube Music dan YouTube di Chrome/Edge, Apple Music, dan Media Player. Lirik Gili bisa berbeda dari lirik yang tampil di Spotify, karena keduanya dari sumber berbeda.
- Cara mencari: `GET https://lrclib.net/api/get` dengan judul, artis, album, dan durasi. Kalau tidak ketemu, `GET /api/search` dengan judul + artis, lalu dipilih hasil dengan durasi berselisih ≤ 2 detik. Sebelum mencari, embel-embel judul seperti `(Remastered 2011)`, `- Live`, `(feat. …)` dibuang. Permintaan dikirim dengan header `User-Agent` `Gili/<versi>` seperti yang diminta LRCLIB.

**Nyala/mati dan privasi**
- **Opt-in, default mati**, karena judul lagu dikirim ke layanan luar. Dinyalakan dari tombol **Lyrics** di halaman Media atau Settings → Island → **Show lyrics** (keterangan `Synced lyrics from lrclib.net. Only the song title, artist, album and length are sent`); keduanya saklar yang sama. Saat pertama dinyalakan dari tombol, pulau menampilkan keterangan yang sama satu kali.
- Yang dikirim hanya judul, artis, album, dan durasi lagu. Lirik disimpan di memori untuk 20 lagu terakhir, tidak ditulis ke disk atau log, jadi daftar lagu yang didengar tidak tersimpan.

**Di slot compact**
- Selama musik diputar dan ada baris lirik yang sedang berjalan, teks judul–artis di slot diganti **baris lirik saat ini** (maks. 44 karakter, dipotong "…"; tooltip berisi baris lengkap). Artwork, bar level, dan tombol play/pause tetap.
- Baris baru naik 5 px sambil fade dalam 260 ms. Lebar pulau mengikuti panjang baris dengan pegas compact ↔ wide, tetap minimal 200 px.
- Slot kembali ke judul–artis sebelum baris pertama, selama jeda instrumental (baris kosong di LRC), saat dijeda, dan saat lirik tidak ada.

**Di halaman Media**
- Tombol **Lyrics** (ikon gelembung kata, 34 px) di kiri tombol previous; hijau aksen saat nyala.
- Saat nyala, di bawah baris musik ada blok lirik setinggi tetap, dipisah garis tipis: baris sebelumnya (13 px, abu gelap), **baris saat ini** (17 px semibold, putih), baris berikutnya (13 px, abu), lalu `Lyrics from LRCLIB` (10,5 px). Baris yang terlalu panjang dipotong "…". Saat baris berganti, blok bergulir naik 10 px dalam 300 ms. Jeda instrumental tampil sebagai `♪`.
- Pulau memanjang sekali saat lirik dinyalakan. Tinggi blok tetap, jadi pulau tidak berubah ukuran setiap baris.
- Keadaan lain di baris tengah (abu, 14 px): `Searching for lyrics…`, `No lyrics for this song`, `Lyrics aren't synced for this song` (LRCLIB hanya punya lirik tanpa waktu; lirik tanpa waktu tidak ditampilkan), `Lyrics unavailable offline`.
- Video, podcast, dan iklan tidak punya lirik, jadi blok lirik tidak tampil.

**Sinkron**: posisi lagu diambil dari `GetTimelineProperties` sesi media lalu diteruskan dengan jam lokal selama lagu diputar, karena Spotify hanya mengirim posisi saat status berubah atau saat lagu digeser. Posisi dikoreksi setiap kali `TimelinePropertiesChanged` atau `PlaybackInfoChanged` datang. Baris berganti dalam ±250 ms dari stempel waktunya; seek langsung pindah ke baris yang benar.

#### Cuplikan clipboard

Setiap kali isi clipboard berubah, slot pulau menampilkan cuplikannya selama 3 detik. Pulau tidak terbuka; ia tetap compact dengan lebar yang mengikuti isi cuplikan (minimal 200 px), dan aktivitas yang sedang berjalan pindah sementara ke gelembung di kanan pulau.

| Isi clipboard | Di slot |
|---|---|
| Teks | Ikon clipboard + `"rapat jam 3 di ruang Merapi, bawa…"` (baris pertama, maks. 40 karakter) |
| Gambar | **Thumbnail gambar yang dicopy** (36 × 20 px, dipotong mengisi kotaknya, sudut 4 px, garis tipis putih 16%; mockup `.thumb`) + **nama file** (maks. 32 karakter) + `1920 × 1080` |
| File (copy) | Ikon file + `3 files`; kalau hanya satu file, namanya (maks. 32 karakter). Satu file gambar tampil seperti baris Gambar, dengan thumbnail |
| File (cut, Ctrl+X) | Ikon gunting + `3 files`; dibedakan dari copy lewat format clipboard `Preferred DropEffect` |
| Isi sensitif | Ikon gembok + `Copied (hidden)` |

- **Nama file gambar** diambil berurutan dari: (1) path file, kalau file gambar dicopy dari Explorer; (2) nama di URL gambar, kalau "Copy image" dari browser (dibaca dari format clipboard `HTML Format` atau `UniformResourceLocatorW`); (3) `Screenshot`, kalau sumbernya Snipping Tool atau Print Screen; (4) `Image` kalau tidak diketahui.
- Tooltip slot menampilkan detail lengkap: teks sampai 120 karakter, nama file lengkap, atau daftar nama file.
- Klik slot membuka halaman Clipboard di pulau (lihat "Halaman Clipboard"); cuplikan gambar dan file juga, walaupun riwayatnya hanya berisi teks.
- Copy berikutnya dalam 3 detik mengganti cuplikan dan menghitung ulang 3 detik.
- Isi clipboard yang ditandai sensitif oleh aplikasi sumber (`ExcludeClipboardContentFromMonitorProcessing` atau `CanIncludeInClipboardHistory = 0`, dipakai password manager) tidak ditampilkan dan tidak disimpan.

#### Operasi file Explorer

Saat Explorer menyalin, memindahkan, atau menghapus file, progresnya tampil sebagai aktivitas pulau. Pulau tetap compact dengan lebar yang mengikuti isi progres (minimal 200 px); pulau tidak terbuka dan tidak mengambang karena operasi file.

| Operasi | Ikon dan warna | Di slot saat berjalan | Di slot setelah selesai (2 detik) |
|---|---|---|---|
| Copy | Folder, hijau `#30D158` | Ikon + `Project-Final.zip` + garis progres + `42%` | Centang + `Project-Final.zip` + `Copied` |
| Cut / Move (Cut + Paste, atau seret untuk pindah) | Gunting, biru `#0A84FF` | Ikon + `Photos 2025` + garis progres + `42%` | Centang + `Photos 2025` + `Moved` |
| Delete ke Recycle Bin | Tempat sampah, merah `#FF453A` | Ikon + `1,204 items` + garis progres + `42%` | Centang + `1,204 items` + `Deleted` |
| Delete permanen (Shift+Delete) | Tempat sampah, merah | Ikon + `1,204 items` + garis progres + `42%` | Centang + `1,204 items` + `Deleted` |

- **Nama yang tampil**: nama file kalau hanya satu item, nama folder kalau satu folder, atau jumlah item (`1,204 items`) kalau banyak. Maksimal 28 karakter, dipotong "…"; pulau memanjang mengikuti nama itu.
- Tujuan dan ukuran ada di tooltip, misalnya `Copying Project-Final.zip · 4.2 GB` atau `Deleting permanently 1,204 items`.
- Klik slot membawa dialog progres Explorer ke depan, tidak membuka pulau.
- Progres hanya tampil kalau Explorer membuka dialog progresnya (biasanya untuk operasi yang lebih lama dari sekitar 1 detik). Operasi yang selesai instan tidak memunculkan apa pun.
- Dibatalkan pengguna: ikon silang + nama + `Cancelled` berwarna abu-abu selama 2 detik.
- **Konflik file**: saat Explorer berhenti dan menunggu keputusan, pulau terbuka sebagai **kartu** (prioritas 1, tanpa suara) sehingga keputusan bisa diambil langsung dari pulau:
  - File sudah ada di tujuan: `Replace or skip files` · `Project-Final.zip already exists in D:\Backup`, tombol **Skip** dan **Replace**. Kalau ada beberapa file bentrok: `3 files already exist in D:\Backup`.
  - File sedang dipakai: `File in use` · `budget.xlsx is open in Excel`, tombol **Skip** dan **Try again**.
  - Butuh izin admin: `Needs administrator permission` · nama folder tujuan, tombol **Open dialog** (tombol Continue di Explorer memicu UAC, jadi diselesaikan di dialog Explorer).
  - Pilihan di kartu diteruskan ke tombol yang sama di dialog Explorer lewat UI Automation, lalu kartu menutup dan progres berlanjut di slot. Kalau tombol tidak bisa ditekan, kartu menampilkan **Open dialog** yang membawa dialog Explorer ke depan.
  - Selama kartu ditunda oleh kartu lain (misalnya alarm), slot menampilkan ikon peringatan + nama + `Needs attention` berwarna kuning; klik slot membuka kartu konflik lagi.
- Beberapa operasi bersamaan: persen gabungan dengan ikon operasi yang paling lama tersisa; tooltip mendaftar tiap operasi.
- Aktivitas lain yang sedang berjalan (misalnya musik) pindah ke gelembung di kanan pulau selama operasi file berlangsung.

#### Sesi Claude

Gili menampilkan status sesi Claude Code: tab **Code** di Claude Desktop dan `claude` di terminal (termasuk wmux). Tab Chat Claude Desktop tidak didukung karena tidak mengirim sinyal apa pun ke luar aplikasinya. Integrasi ini mati secara default dan dinyalakan di Settings (F-CLD-01); sumber datanya di 9.2.

**Status tiap sesi**

| Status | Kapan |
|---|---|
| Working | Sejak prompt dikirim sampai Claude berhenti |
| Needs input | Claude berhenti dan menunggu: pertanyaan (`AskUserQuestion`), persetujuan rencana (`ExitPlanMode`), atau izin menjalankan tool. Kembali ke Working setelah dijawab |
| Done | Claude berhenti normal setelah mengerjakan prompt |
| Idle | Sesi terbuka tapi tidak bekerja: setelah Done dilihat, atau setelah dihentikan dengan Esc (tanpa tanda Done) |

**Di slot pulau**

Semua sesi digabung menjadi **satu aktivitas Claude** yang statusnya mengikuti sesi terpenting: Needs input, lalu Working.

| Keadaan | Ikon dan warna | Di slot |
|---|---|---|
| Needs input | Ikon Claude, kuning `#FFD60A` | Ikon + `Integrasi taskbar dengan…` (nama sesi, maks. 28 karakter) + `Needs input`. Beberapa sesi: `2 sessions need input` |
| Working | Ikon Claude, oranye `#D97757`, berdenyut pelan | Ikon + nama sesi + waktu berjalan `2:14`. Beberapa sesi: `2 sessions working` |
| Selesai (4 detik) | Centang, hijau `#30D158` | Centang + nama sesi + `Done · 4m 12s` (di bawah 1 menit: `Done · 38s`) |

- **Ikon Claude** memakai logo Claude resmi. Di pulau (slot, kartu, gelembung, halaman Claude) dipakai glyph logonya dalam satu warna yang mengikuti status. Ikon aplikasi di bagian kiri bar memakai logo paket Claude Desktop (`Square44x44Logo`), sama seperti aplikasi aktif lain (9.2).
- **Nama sesi** diambil dari judul sesi di Claude Desktop. Kalau tidak ada (misalnya sesi terminal tanpa judul), dipakai nama foldernya (`top-taskbar`).
- Tooltip slot: nama sesi lengkap, folder, dan aplikasinya (`Claude Desktop` atau nama terminal); untuk Needs input juga cuplikan pertanyaannya.
- Lama di `Done` dihitung dari prompt dikirim sampai Claude berhenti, termasuk waktu menunggu jawaban.
- Selama aktivitas Claude tampil (di slot atau di gelembung aktivitas), sesi lain yang selesai dan belum dilihat ditandai titik hijau 5 px di ikon Claude, bukan gelembung hijau terpisah, supaya Claude hanya muncul di satu tempat.
- **Klik slot**: kalau hanya satu sesi yang terlibat, jendela sesi itu dibawa ke depan dan pulau tidak terbuka. Kalau lebih dari satu, pulau terbuka di halaman Claude.
- Aktivitas lain yang sedang berjalan (misalnya musik) pindah ke gelembung selama slot dipakai Claude.

**Kartu "Needs input"**

Saat sebuah sesi mulai menunggu, pulau membuka kartu (prioritas 2, seperti pengingat todo) dan Ping berbunyi sekali.

| Jenis | Judul | Baris kedua | Baris ketiga |
|---|---|---|---|
| Pertanyaan | `Claude needs your input` | Nama sesi | Cuplikan pertanyaan pertama, maks. 90 karakter |
| Izin tool | `Claude needs permission` | Nama sesi | Pesan dari Claude, misalnya `Claude needs your permission to use Bash` |
| Rencana | `Plan ready for review` | Nama sesi | — |

- Tombol: **Later** (kartu langsung mengecil ke slot) dan **Open Claude** (untuk sesi terminal: **Open terminal**), yang membawa jendela sesi ke depan dan menutup kartu.
- Tanpa ditanggapi, kartu mengecil ke slot kuning setelah 8 detik. Slot kuning bertahan sampai pertanyaan dijawab di Claude. Kartu tidak muncul lagi untuk pertanyaan yang sama.
- Pertanyaan baru dari sesi lain saat kartu masih terbuka mengganti isi kartu dan menghitung ulang 8 detik; slot tetap menghitung semua sesi yang menunggu.
- Selama fase fokus pomodoro, kartu dan Ping ditahan (F-TOOL-04) dan hanya slot kuning yang tampil. Kalau sesi masih menunggu saat istirahat dimulai, kartu muncul saat itu.
- Gili **tidak mengirim toast** untuk kejadian Claude, supaya tidak dobel dengan notifikasi Claude Desktop sendiri. Karena itu saat aplikasi fullscreen aktif, kartu Claude dilewati.

**Setelah selesai**
- Kalau jendela sesi itu sedang aktif saat Claude selesai, cukup tampilan `Done` 4 detik.
- Kalau tidak, setelah 4 detik muncul **gelembung hijau** berikon Claude di kanan pulau, paling kanan dari semua gelembung (kecuali aktivitas Claude sedang tampil; lihat titik hijau di atas). Gelembung ini bukan aktivitas, jadi Gili tetap di dalam pulau.
- Gelembung hilang saat jendela sesi itu menjadi aktif, saat gelembung diklik (membawa jendela itu ke depan), atau saat prompt baru dikirim di sesi itu. Kalau lebih dari satu sesi selesai dan belum dilihat, gelembung diberi angka (`2`) dan klik membuka halaman Claude.
- Untuk sesi Claude Desktop, "jendela aktif" berarti jendela Claude Desktop di depan, karena Gili tidak tahu percakapan mana yang sedang dilihat di dalamnya.
- Selesai tidak berbunyi.

#### Batas pemakaian Claude (jendela 5 jam)

Paket langganan Claude membatasi pemakaian per jendela 5 jam yang dimulai saat pesan pertama dikirim. Gili menghitung mundur jendela itu dan memberi tahu saat jendela berakhir, supaya pengguna tahu kapan bisa lanjut bekerja penuh. Fitur ini bagian dari integrasi Claude (mati bila integrasi mati) dan punya saklar sendiri: Settings → Claude → **Show 5-hour usage timer** (default nyala).

**Sumber waktu** (urutan dipakai; diverifikasi di awal Fase 3, lihat bagian 12):
1. Waktu reset resmi dari Claude Code bila tersedia di data yang bisa dibaca Gili (misalnya field `rate_limits.five_hour.resets_at` dan `used_percentage`, atau pesan batas di file sesi yang memuat waktu reset). Dengan sumber ini, persentase terpakai ikut ditampilkan.
2. Perkiraan dari hook: jendela dimulai pada `UserPromptSubmit` pertama dari sesi mana pun ketika tidak ada jendela yang berjalan, dibulatkan ke bawah ke jam penuh, dan berakhir 5 jam kemudian. Tooltip menambahkan `Estimated`.

**Di slot pulau**

| Keadaan | Ikon dan warna | Di slot |
|---|---|---|
| Jendela berjalan | Jam pasir, oranye Claude `#D97757` | Ikon + `Resets in` + `3:12:40` (j:mm:ss, digit tabular). Prioritas aktivitas 9, jadi biasanya tampil sebagai gelembung saat ada aktivitas lain |
| Batas tercapai | Jam pasir, kuning `#FFD60A` | Ikon + `Limit reached` + `1:04:12`. Naik ke prioritas tepat setelah sesi Claude yang butuh jawaban |
| Tidak ada jendela | — | Tidak tampil |

- Tooltip: `Claude 5-hour window · resets at 19:00` (ditambah `· 62% used` bila sumber 1 tersedia, atau `· Estimated` untuk sumber 2).
- Klik slot atau gelembungnya membuka halaman Claude.
- Halaman Claude menampilkan baris ringkasan di atas daftar sesi: ring kecil sisa waktu, `5-hour window`, `Resets 19:00`, dan sisa waktu; bila sumber 1 tersedia, ring menunjukkan persen terpakai.

**Saat jendela berakhir**
- Kartu notifikasi (prioritas 1, seperti timer habis): ikon jam pasir oranye, `Claude usage reset`, baris kedua `Your 5-hour limit is available again`. Tombol **Close** dan **Open Claude**.
- Suara mengikuti timer (default Chime, berulang sampai ditanggapi, maksimal 5 menit) dan toast tanpa suara dikirim ke Notification Center, sama seperti timer.
- Selama fase fokus pomodoro kartu ditahan sampai istirahat, seperti kartu prioritas 2; toast tetap dikirim.
- Jendela baru tidak dimulai otomatis; baru dimulai saat prompt berikutnya dikirim.

**Jendela sesi**: Gili menelusuri proses induk dari proses `claude` sebuah sesi sampai menemukan jendela pertama yang terlihat: Claude Desktop untuk sesi Desktop, atau jendela terminal (wmux, Windows Terminal) untuk sesi CLI. Membawa ke depan berarti memulihkan jendela kalau diminimize, lalu memfokuskannya. Membuka percakapan tertentu di dalam Claude Desktop di luar cakupan.

#### Indikator kamera dan mikrofon

- Saat kamera atau mikrofon dipakai aplikasi mana pun, **titik berwarna 8 px tanpa latar** muncul tepat di **kiri pulau** (atau di kiri lingkaran Gili kalau sedang tampil), berjarak 6 px, seperti titik privasi di iPhone. Kamera hijau (`#30D158`), mikrofon oranye (`#FF9F0A`).
- Saat compact, titik berjajar horizontal dan sejajar tengah dengan pulau; kalau keduanya aktif, titik kamera paling dekat ke pulau (jarak antar-titik 5 px).
- Setiap titik punya garis tepi gelap tipis (1,5 px, hitam 35%) supaya tetap terlihat di atas jendela berlatar terang. Area hover diperlebar 6 px di semua sisi.
- Titik muncul dengan pegas (skala 0 → 1) dan hilang dengan cara sebaliknya ≤ 2 detik setelah perangkat berhenti dipakai.
- Titik **tetap tampil di semua keadaan pulau**. Saat pulau mengambang, titik bergeser ke sisi kiri panel dan bersusun vertikal mulai sejajar menu halaman. Indikator privasi tidak boleh tersembunyi oleh pulau yang sedang terbuka.
- Hover menampilkan nama aplikasinya (`Camera in use: Zoom`, `Microphone in use: Discord`).
- Saat aplikasi fullscreen aktif, titik ikut tersembunyi bersama bar; indikator mikrofon bawaan Windows di system tray tetap ada.

#### Kejadian wide

| Kejadian | Isi | Lama |
|---|---|---|
| Caps Lock / Num Lock | `Caps Lock on` / `off`, `Num Lock on` / `off` | 1,5 detik |
| Charger dicolok | Petir + `Charging · 64%` | 3 detik |
| Charger dicabut | `On battery · 63%` | 2 detik |
| Peringatan baterai | `Battery 20% · plug in` (persen saat itu, petir kuning), `Battery 10%` (petir merah), `Charging on hold · 79%`, `80% reached · unplug`, `Fully charged` (petir hijau) | 5 detik |
| Perangkat Bluetooth tersambung | Ikon jenis perangkat (biru) + nama (`Galaxy Buds2 Pro`) + `Connected` + baterai perangkat (`80%`) kalau perangkat melaporkannya | 4 detik |
| Perangkat Bluetooth terputus | Ikon abu-abu + nama + `Disconnected` | 2 detik |

Perubahan volume dan brightness tidak ditampilkan di pulau; flyout bawaan Windows sudah menanganinya. Mengatur keduanya bisa lewat pulau Volume dan Brightness (6.4).

**Baterai**: peringatan rendah muncul sekali per siklus pakai (sejak charger dicabut) di ambang `Battery alert` (default 20%) dan di 10%; kalau keduanya terlewati sekaligus, hanya 10% yang tampil. Laptop yang membatasi pengisian sendiri (Lenovo Vantage, ASUS, Dell, Surface) dikenali dari charger yang tercolok tetapi pengisian berhenti di bawah 100%: Gili menampilkan `Charging on hold · 79%` sekali per colok (kalau sudah ditahan saat dicolok, wide charger langsung berbunyi begitu, bukan `Charging`), dan tidak pernah `unplug` atau `Fully charged`. Laptop tanpa batas mendapat `80% reached · unplug` saat persen **melewati** ambang `Unplug reminder` (default 80%) sambil masih mengisi, lalu `Fully charged` di 100%; keduanya sekali per colok. Gili yang mulai di bawah ambang tanpa charger memberi peringatan sekali; mulai dengan charger tidak memunculkan apa pun.

**Bluetooth**: berlaku untuk perangkat audio (TWS, headphone, speaker), mouse, keyboard, controller, dan ponsel, dengan ikon sesuai jenisnya. Hanya kejadian sambung/putus yang ditampilkan, bukan perangkat yang sudah tersambung saat Gili mulai. Kalau perangkat yang sama tersambung-putus berulang dalam 10 detik, hanya kejadian terakhir yang ditampilkan.

#### Notifikasi Windows (revisi 8)

Notifikasi baru dari aplikasi lain (pesan WhatsApp, email Outlook, chat Teams atau Telegram) ikut tampil sebentar di pulau, dengan ikon aplikasinya dan isinya. Contoh pesan WhatsApp: ikon WhatsApp, nama kontak `Ayu Wulandari`, `WhatsApp · now`, lalu isi pesannya.

**Nyala/mati dan izin**
- **Opt-in, default mati**, karena Gili membaca isi notifikasi aplikasi lain: Settings → Notifications → **Show Windows notifications**. Saat dinyalakan, Gili meminta izin lewat dialog Windows (`UserNotificationListener.RequestAccessAsync`). Kalau ditolak, saklar kembali mati dan Settings menampilkan `Gili can't read notifications` dengan tombol **Open Windows settings** (`ms-settings:privacy-notifications`).
- **Show message text** (default nyala): kalau mati, pulau hanya menampilkan ikon aplikasi dan judul, dengan isi `New notification`.
- Daftar saklar per aplikasi: aplikasi muncul di daftar saat pertama kali mengirim notifikasi (terbaru di atas), semuanya default nyala.
- Isi notifikasi hanya ada di memori selama tampil dan selama di antrean; tidak pernah ditulis ke disk atau log.

**Di pulau kecil yang memanjang** (dipilih pengguna 27 Okt 2026, menggantikan banner mengambang)
- Notifikasi tampil **di pulau compact itu sendiri**, bukan panel mengambang: pulau tetap setinggi pengaturan Island height (default 24 px) dan tetap di dalam bar, hanya **memanjang ke samping** seperti kejadian wide, dengan pegas compact ↔ wide. Pulau tidak pernah terbuka ke bawah karena notifikasi.
- Isi dalam satu baris, dari kiri ke kanan:
  - **ikon aplikasi** 16 px (logo dari `AppInfo.DisplayInfo.GetLogo`);
  - **judul notifikasi** semibold (biasanya nama kontak atau grup; maks. 160 px, dipotong "…");
  - angka kecil (`2`, `3`) bila ada beberapa pesan dari chat yang sama;
  - **isi notifikasi** (putih 85%), sisa lebar, satu baris, dipotong "…". Untuk grup, isinya apa adanya dari aplikasi (misalnya `Rani: Nanti aku telat`).
  Ukuran teks sama dengan teks pulau compact (setengah tinggi pulau, 11–15 px). Nama aplikasi tidak ditulis karena ikonnya sudah menunjukkannya; tooltip berisi `WhatsApp · Ayu Wulandari: <isi lengkap>`.
- Teks diambil dari binding `ToastGeneric`: teks pertama adalah judul, teks berikutnya digabung sebagai isi. Gambar di notifikasi (foto kontak, gambar besar) tidak ditampilkan, karena API Windows tidak memberikannya ke aplikasi lain.
- **Panjang pulau** mengikuti isi, maksimal 560 px (atau lebar layar − 24 px). Kalau pulau yang memanjang menutupi isi kiri atau kanan bar (termasuk ruang lingkaran Gili dan titik kamera/mic di kirinya), elemen yang tertimpa ditambah 8 px memudar (opacity 0, 160 ms) selama notifikasi tampil, dengan aturan yang sama seperti saat pulau mengambang, lalu muncul lagi. Bar tidak menata ulang isinya (tidak ada ikon yang pindah ke `+N` atau modul yang pindah ke "…") karena notifikasi hanya sebentar.
- Lingkaran Gili tetap di kiri pulau seperti saat ada aktivitas. Aktivitas yang sedang berjalan sementara tidak terlihat, sama seperti saat kejadian wide.
- **Lama**: 5 detik, lalu pulau kembali ke compact. Selama kursor di atas pulau, waktunya berhenti, dan notifikasi hilang 2 detik setelah kursor pergi.
- **Klik pulau** selama notifikasi tampil: membawa jendela aplikasi itu ke depan (dicari dari AppUserModelID notifikasi seperti ikon aplikasi di kiri bar; kalau tidak ada jendela, aplikasinya dijalankan lewat AppUserModelID), lalu notifikasi berikutnya tampil atau pulau kembali compact. Aksi di dalam notifikasi (membuka chat tertentu, tombol Reply) tidak bisa dijalankan Gili, karena API Windows tidak memberikannya. Tidak ada tombol tutup; notifikasinya tetap ada di Notification Center Windows.
- **Beberapa notifikasi**: pesan baru dengan aplikasi dan judul yang sama selama notifikasinya tampil menggantikan isinya, menambah angka di samping judul, dan mengulang hitungan 5 detik. Notifikasi lain masuk antrean: yang sedang tampil memendek menjadi paling lama 2,5 detik lagi, lalu isi berikutnya masuk dengan cross-fade 180 ms dan panjang pulau menyesuaikan dengan pegas compact ↔ wide. Selama antrean berisi, tiap notifikasi tampil 2,5 detik.
- Kejadian wide lain (Caps Lock, charger, Bluetooth) tidak memotong notifikasi yang sedang tampil dan dilewati, karena notifikasi lebih tinggi prioritasnya. Kartu (alarm, timer, pengingat) dan drop zone langsung menggantikannya.
- Tanpa suara; Windows sudah membunyikan notifikasinya sendiri.
- Aksesibilitas: isi pulau adalah `status` dengan nama `WhatsApp, Ayu Wulandari: <isi>`, jadi pembaca layar membacanya tanpa memindah fokus.

**Kapan tidak tampil**
- Notifikasi dari Gili sendiri, dan dari Claude Desktop selama integrasi Claude nyala (pulau sudah menampilkan sesi Claude).
- Aplikasi yang saklarnya dimatikan, dan notifikasi tanpa teks.
- Saat Do Not Disturb Windows nyala (dibaca dengan `SHQueryUserNotificationState`, best-effort, lihat bagian 12), selama fase fokus pomodoro, dan saat aplikasi fullscreen aktif. Notifikasinya tetap ada di Notification Center dan tidak ditampilkan belakangan.
- Saat pulau sedang terbuka (halaman atau pulau kontrol), saat kartu atau drop zone tampil: notifikasi menunggu di antrean dan tampil setelah pulau kembali compact, asal umurnya belum 10 detik. Yang lebih tua dibuang dari antrean.

**Toast Windows tetap muncul**: Gili tidak bisa menyembunyikan toast aplikasi lain di pojok kanan bawah. Pengguna yang ingin melihatnya di pulau saja bisa mematikan banner (pop-up) aplikasi itu di Windows Settings → System → Notifications (notifikasi tetap masuk Notification Center dan tetap terbaca Gili; diverifikasi di Fase 4). Settings Gili menampilkan tips ini di bawah saklar utama.

#### Prioritas tampilan

Kalau beberapa kejadian bersamaan, `IslandCoordinator` menampilkan yang paling tinggi dan menahan sisanya di antrean:

1. Alarm berbunyi / timer habis / fase pomodoro selesai / jendela Claude 5 jam berakhir / konflik file Explorer (kartu, tetap tampil sampai ditanggapi)
2. Pengingat todo (kartu selama 10 detik lalu menutup; toast-nya tetap di Notification Center) dan kartu Claude "Needs input" (8 detik lalu mengecil ke slot, tanpa toast)
3. Drop zone saat file diseret ke atas bar
4. Notifikasi Windows di pulau compact yang memanjang (5 detik; revisi 8)
5. Kejadian wide
6. Aktivitas berjalan (slot compact)
7. Gili saja (tanpa aktivitas)

Pulau yang dibuka pengguna (expanded atau pulau kontrol) berada di atas prioritas 4–7 dan di bawah 1–3; notifikasi Windows yang datang saat pulau terbuka menunggu sampai pulau menutup (maks. 10 detik). Alarm menggantikan halaman atau kontrol yang sedang terbuka, lalu keduanya kembali setelah alarm ditanggapi. Saat aplikasi fullscreen aktif, hanya prioritas 1 yang muncul (sebagai overlay); prioritas 2 lewat toast saja (kartu Claude dilewati karena tidak punya toast), prioritas 3–7 dilewati.

#### Halaman expanded

**Menu halaman** (di semua halaman; dipilih user 2026-10-26, menggantikan header judul, tombol ‹ › dan titik halaman):
- Kapsul menu di bagian atas pulau, berjarak **14 px dari tepi atas dan kanan**, tinggi 36 px, radius penuh 18 px, latar putih 10%. Di kirinya ada lingkaran Gili setinggi menu (36 px, 14 px dari tepi kiri), dan menu mulai 6 px di kanan lingkaran itu lalu mengisi sisa lebar pulau. Kalau Show Gili mati, menu mulai 14 px dari tepi kiri dan selebar pulau.
- **Sudut konsentris**: radius menu + jaraknya ke tepi = radius pulau (18 + 14 = 32 px), jadi lengkung menu sejajar dengan lengkung pulau dan celahnya sama rata di sisi maupun sudut. Sorotan tab mengikuti aturan yang sama di dalam menu (15 + 3 = 18 px). Lingkaran Gili di kiri menu mengikuti aturan yang sama (radius 18 px pada jarak 14 px).
- Berisi 10 tab: Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, Claude, System (Clipboard ditambahkan di revisi 8). **Tab menampilkan ikon saja** (ikon 15 px, abu-abu); nama tab (teks 12 px, 5 px dari ikon) hanya tampil di **tab aktif** dan di **tab yang di-hover** atau difokus keyboard (revisi 7, menggantikan ikon + nama di semua tab).
- **Lebar tab**: tab ikon saja 36 px (10,5 + 15 + 10,5). Tab yang menampilkan nama selebar 12 px + ikon + 5 px + nama + 12 px, jadi nama tidak pernah menempel ke tepi sorotan. Sisa lebar menu dibagi rata ke semua tab, jadi ikon tetap tersebar sepanjang menu; saat nama terbuka, tab lain merapat sedikit tanpa mengubah lebar pulau.
- **Nama muncul dengan gerak**: saat kursor masuk ke tab, namanya melebar keluar dari ikon (lebar 300 ms ease-out, fade masuk 160 ms) dan tab lain bergeser mulus; saat kursor keluar dari menu, nama menyusut dengan gerak yang sama. Tab yang di-hover hanya berganti saat kursor benar-benar bergerak, bukan karena tab bergeser di bawah kursor yang diam (aturan yang sama dengan aplikasi di bagian kiri, 6.2). Nama tetap ada di tombol walau tersembunyi, jadi pembaca layar tetap membaca nama setiap tab.
- **Tab bisa disembunyikan** (Settings → Island pages, saklar per halaman, default semua nyala, minimal satu tetap nyala). Mematikan ikon di Settings → Left side juga mematikan tab halaman yang sama (6.2 "Saklar kiri ikut mematikan tab pulau"). Tab yang disembunyikan keluar dari menu dan dari urutan geser/panah. Halaman tersembunyi tetap terbuka kalau sesuatu menunjuk langsung ke sana (klik aktivitas di slot, ikon bar, hotkey, kartu); selama terbuka dengan cara itu, tabnya tampil sementara di posisi aslinya dan hilang lagi setelah pindah halaman.
- Tab aktif: sorotan putih 14% yang bergeser ke tab yang dipilih **dan melebar mengikuti tab itu**, teks putih semibold, ikon hijau aksen. Ikon tab Claude mengikuti aturan yang sama dengan tab lain (abu-abu, hijau saat aktif), bukan oranye Claude. Saat pindah halaman, nama tab lama menyusut dan nama tab baru melebar bersamaan dengan sorotan.
- Saat musik diputar, ikon tab Media berganti menjadi bar level hijau yang bergerak (selebar ikon, 15 px), jadi musik tetap terlihat dari halaman mana pun.
- Sorotan tetap pas dengan tabnya selama nama membuka atau menutup dan selama pulau berubah ukuran: posisi dan lebar akhirnya dihitung dari keadaan akhir semua tab, lalu bergerak dengan kurva yang sama dengan nama.

**Lebar pulau terbuka**
- **Sama untuk semua halaman**, termasuk Notes: 560 px, atau selebar yang dibutuhkan menu kalau lebih (lihat di bawah); halaman tidak punya lebar sendiri (revisi 7, sebelumnya mengikuti menu ikon + nama, 929 px untuk 9 tab). Calendar dan Todo menyempitkan kolom kanannya (daftar acara dan daftar tugas) mengikuti lebar ini; judul yang terlalu panjang dipotong "…".
- Lebar yang dibutuhkan menu = 14 px + lingkaran Gili 36 px + 6 px + menu (3 px padding di kiri dan kanan, n ikon 36 px dengan celah 2 px, ditambah ruang untuk **dua** nama: tab aktif dan tab yang di-hover) + 14 px, dengan n = jumlah tab yang tampil. Pada 100% DPI 9 tab butuh ≈ 530 px (pulau tetap 560 px), sedangkan 10 tab dengan nama terpanjang `Clipboard` butuh ≈ 576 px, jadi **pulau terbuka 576 px selama semua tab nyala** dan kembali 560 px kalau satu tab dimatikan. Pulau hanya lebih lebar dari 560 px kalau menu membutuhkannya. Membuka nama saat hover tidak pernah mengubah lebar pulau.
- **Maksimal lebar layar − 24 px**. Kalau menu tidak muat di lebar itu, tab yang di-hover tidak membuka namanya (nama tampil sebagai tooltip); tab aktif tetap ikon + nama.
- Show Gili mati: lingkaran dan 6 px jaraknya tidak dihitung.
- Menyalakan atau mematikan tab di Settings mengubah lebar pulau hanya kalau kebutuhan menu melewati 560 px (misalnya dari 10 ke 9 tab: 576 → 560 px, dengan pegas compact ↔ wide); ikon yang tersisa tersebar ulang dengan pegas compact ↔ wide.
- **Tanpa baris jam di dalam pulau**: jam dan tanggal sudah selalu tampil di kanan bar (6.4), jadi baris jam di bawah pulau (revisi 3) dihapus; ruangnya dipakai halaman.
- Nama bulan dan tahun tampil di atas grid halaman Calendar. Aksesibilitas: menu adalah `tablist`, tiap tab `tab` dengan `aria-selected`.

**Tinggi pulau terbuka**
- Tinggi pulau mengikuti isi halaman yang sedang tampil, supaya isi tidak perlu di-scroll: 14 px + menu 36 px + 8 px + tinggi isi halaman + 20 px.
- **Minimal** 256 px untuk semua halaman, jadi halaman yang isinya sedikit tidak menyusut. Notes tidak punya minimal sendiri: tingginya mengikuti kartu, dan selama editor terbuka setinggi editor kalau editor lebih tinggi dari kartu.
- Tinggi isi dihitung lengkap, termasuk padding atas dan bawah halaman, jadi **tidak ada scroll selama pulau belum setinggi layar**, termasuk di Notes (scrollbar tidak boleh muncul hanya karena selisih beberapa piksel).
- **Maksimal setinggi layar**: tinggi layar dikurangi jarak tepi atas pulau di kedua ujung, yaitu (tinggi bar − tinggi pulau) / 2 di atas dan jarak yang sama di bawah (760 px pada layar 768 px dengan bar 32 px dan pulau 24 px). Pulau boleh menutupi taskbar Windows. Baru setelah batas ini isi halaman bisa di-scroll; di Notes yang di-scroll hanya grid kartu, kolom cari dan menu tetap di tempat.
- Contoh: Todo dengan 9 tugas menampilkan kesembilannya sekaligus (baris 44 px + jarak 6 px); Calendar menampilkan semua item tanggal terpilih (tanpa `+N more`); Alarms menampilkan semua alarm; Claude menampilkan semua sesi; Notes memanjang mengikuti kolom kartu tertinggi.
- Tinggi berubah dengan pegas yang sama seperti membuka pulau, bersamaan dengan geser halaman. Saat isi bertambah atau berkurang (centang, tambah tugas, hapus), tinggi menyesuaikan dengan pegas compact ↔ wide. Lebar tidak berubah.
- Di posisi bar bawah aturannya dicerminkan: tepi bawah tetap, pulau memanjang ke atas sampai jarak yang sama dari tepi atas layar.

**Ambient glow**
- Di belakang isi pulau terbuka ada sapuan warna lembut dari halaman yang sedang tampil: warna cover album di Media, oranye di Timer (merah saat fokus pomodoro, hijau saat istirahat, biru muda untuk stopwatch), kuning di Alarms dan Notes, merah di Calendar, biru di Todo, hijau mint `#66D4CF` di Clipboard, biru muda di Shelf, oranye Claude di Claude, ungu di System. Pindah halaman menyilangkan warna dalam 600 ms.
- **Bergerak acak dan pelan** (revisi 8, menggantikan lintasan elips dan condong ke kursor): tiap gumpalan memilih titik acak di sekitar posisi diamnya, paling jauh 25% lebar pulau ke kiri/kanan dan 30% tinggi pulau ke atas/bawah, lalu melayang ke sana dalam 10–16 detik (acak per perjalanan) dengan kurva ease-in-out yang lembut, kemudian memilih titik berikutnya. Kedua gumpalan memilih titik dan waktunya sendiri-sendiri, jadi geraknya tidak berulang dan tidak seirama. Kecepatannya sekitar 10 px per detik, jadi hampir tidak terasa, seperti cahaya yang hidup, dan tidak mengganggu baca. Keduanya juga "bernapas" pelan (skala 0,92–1,08, periode 9 detik).
- **Tidak mengikuti kursor**: gerakan mouse tidak memengaruhi glow sama sekali.
- Pulau dibuka: gumpalan mulai dari posisi diamnya lalu langsung berangkat ke titik acak pertama. Saat pulau menutup, posisinya kembali ke posisi diam. Di Media saat musik dijeda, gumpalan berhenti di tempatnya dan cahaya meredup ke 50%; saat musik diputar lagi, gerak berlanjut dari situ.
- Seluruh glow berasal dari dua gumpalan yang bergerak itu (tidak ada lapisan gradasi diam di belakangnya), jadi gerakannya terlihat jelas.
- Hanya berjalan selama pulau terbuka dan terlihat; berhenti total saat pulau menutup, saat layar mati, dan saat Animation effects mati (cahaya tetap ada tapi diam). Digambar maksimal 30 fps (8.1).
- Settings → Appearance → **Ambient glow** (default nyala) dan **Ambient motion** (default nyala).

**Halaman** (urutan tetap):

| # | Halaman | Isi | Aksi |
|---|---|---|---|
| 1 | Media | **Musik**: cover, judul, artis, aplikasi sumber, progress bar dengan waktu. **Video**: hanya judul, aplikasi sumber, dan progress bar dengan waktu berjalan dan sisa waktu, tanpa gambar dan tanpa tombol | Musik: ⏮ ⏯ ⏭, klik progress bar untuk seek bila sumber mendukung |
| 2 | Timer | Pilihan mode Timer / Stopwatch / Pomodoro, angka besar, ring sisa waktu | Start, Pause, Reset, preset 10 dtk / 5 / 10 / 25 menit, fase pomodoro |
| 3 | Alarms | Alarm berikutnya dalam huruf besar ("Tomorrow 05:30"), semua alarm dengan saklar (pulau memanjang mengikuti jumlahnya) | Nyala/mati alarm; "Manage alarms" membuka Tools |
| 4 | Calendar | Nama bulan dan tahun di atas grid dengan tombol bulan sebelum/berikutnya (chevron 20 px di kanannya), bulan berjalan saat dibuka, hari ini ditandai, titik pada tanggal yang punya todo/alarm; memilih tanggal menampilkan semua itemnya di kanan (pulau memanjang bila perlu) | Bulan sebelum/berikutnya; **Add task** untuk tanggal terpilih, dengan jam opsional (satu-satunya tempat membuat todo) |
| 5 | Todo | Angka besar `4/5` + "Tasks done today", semua tugas hari ini tanpa scroll (yang selesai dicoret dengan latar hijau gelap; jam tampil di kanan baris bila diatur) | Centang; **Edit** dan **Delete** per baris (lihat "Mengubah dan menghapus todo"). Tanpa tombol tambah: todo dibuat dari Calendar |
| 6 | Notes | Lebar sama dengan halaman lain; tinggi mengikuti kartu. Atas: kolom cari dan tombol New note. Di bawahnya **grid kartu ala Google Keep** selebar penuh (lihat "Halaman Notes" di bawah). Tidak ada kolom editor: note yang dibuka **tumbuh di tempat dari kartunya** menjadi editor | Buat, klik kartu untuk edit, cari, **seret kartu untuk mengatur urutan** (Alt + panah dari keyboard), **seret kartu keluar pulau untuk melepasnya** (lihat "Note mengambang"), hapus (klik dua kali untuk konfirmasi), Done |
| 7 | Clipboard | Kolom cari dan tombol Clear all, lalu daftar 50 teks terakhir yang dicopy, terbaru di atas (lihat "Halaman Clipboard" di bawah) | Klik baris = tempel teks itu di posisi kursor terakhir; cari; hapus per baris; Clear all (klik dua kali) |
| 8 | Shelf | Grid tetap 4 × 2 (maksimal 8 item, **tanpa scroll**). Tile ringkas: ikon jenis file dan nama satu baris; bundel menampilkan ikon tumpukan dengan jumlah file. Ringkasan jumlah file dan total ukuran di kiri | Seret tile keluar ke folder/aplikasi, hapus satu, Clear all |
| 9 | Claude | Baris ringkasan jendela pemakaian 5 jam (bila fiturnya nyala), lalu satu baris per sesi yang hidup, urut Needs input → Working → Done (belum dilihat) → Idle: ikon status, nama sesi, folder, dan waktu (berjalan, lama selesai, atau sejak kapan idle). Baris Needs input menampilkan cuplikan pertanyaan di baris kedua; sesi Idle redup; pulau memanjang mengikuti jumlah sesi. Tanpa sesi: `No Claude sessions`. Halaman hanya ada bila integrasi Claude menyala | Klik baris membawa jendela sesi ke depan dan menandai Done sebagai sudah dilihat |
| 10 | System | Grafik mini 60 detik untuk CPU, RAM, GPU, unduhan; sisa ruang C: | Klik membuka Task Manager |

Tab untuk halaman yang belum dibangun tetap tampil dengan isi `Coming in Phase 2` atau `Coming in Phase 3`; sampai Fase 3 ikon dan tab Claude tampil sebagai placeholder (keputusan pengguna 2026-10-27: tampilan seperti mockup); mulai Fase 3 keduanya mengikuti saklar integrasi Claude.

**Todo hari ini**: todo yang jatuh tempo hari ini, ditambah todo tanpa tanggal yang belum selesai, ditambah todo tanpa tanggal yang diselesaikan hari ini. Rasio dan ring = jumlah selesai / total daftar itu. Kalau daftar kosong tampil `Nothing due today` saja, tanpa tombol.

**Halaman Notes**
- **Ukuran**: lebar pulau sama dengan halaman lain ("Lebar pulau terbuka"); hanya tingginya yang mengikuti isi: pulau memanjang mengikuti kolom kartu tertinggi (6.3 "Tinggi pulau terbuka"). Saat note dibuka dan editor (360 px) lebih tinggi dari kartu, pulau memanjang dulu untuk memberi ruang, lalu kembali ke tinggi kartu setelah editor menutup. Jumlah kolom mengikuti lebar (lebar kolom minimal 140 px; 3 kolom pada pulau 560 px), juga saat note sedang diedit (editor tidak mengambil kolom).
- **Grid kartu ala Google Keep**: setiap note jadi kartu (judul tebal = baris pertama, isi maksimal 8 baris, waktu terakhir diedit). Format waktu: hari ini = jam (mengikuti pengaturan 12-hour clock), kemarin = `Yesterday`, 2–6 hari lalu = nama hari singkat (`Mon`), tahun yang sama = `12 Oct`, tahun lain = `12 Oct 2025`. Grid tanpa note menampilkan `No notes yet.`; pencarian tanpa hasil menampilkan `No notes match.` Lebar kolom minimal 140 px dan jarak antar-kartu 8 px. Tinggi kartu mengikuti isinya, dan kartu ditempatkan berurutan ke kolom yang paling pendek (masonry), jadi susunannya rapat seperti Keep. Pulau memanjang mengikuti kolom tertinggi (6.3 "Tinggi pulau terbuka"); grid baru bisa di-scroll setelah pulau setinggi layar.
- **Kartu menjadi editor**: klik kartu (atau Enter pada kartu yang fokus) membuat **kartu itu sendiri tumbuh di tempat** menjadi editor 440 × 360 px, dibatasi area grid yang terlihat. Grid tetap selebar penuh dan tidak menata ulang; kartu lain meredup ke opacity 30% supaya perhatian ada di note yang dibuka. Slot kartu itu kosong selama editor terbuka, karena editornya adalah kartu itu.
- **Arah tumbuh mengikuti posisi kartu**:
  - Mendatar: kartu di sepertiga kiri grid tumbuh ke kanan (tepi kirinya tetap), di sepertiga kanan tumbuh ke kiri (tepi kanannya tetap), di sepertiga tengah melebar ke dua sisi sama banyak.
  - Tegak: tumbuh ke bawah (tepi atas tetap); kalau ruang di bawahnya kurang, tumbuh ke atas (tepi bawah tetap).
  - Editor selalu berada di dalam area grid yang terlihat; kalau tidak muat, digeser masuk.
  - Isi editor (teks, bulatan warna, tombol) sudah ditata di ukuran akhir dan menempel di sudut asal tumbuh, jadi teks tidak berpindah baris selama animasi. Isi muncul 140 ms setelah kartu mulai tumbuh.
- **Note baru** (**New note** atau `Ctrl+Alt+N`): kartu kosong lahir di kanan atas grid, tepat di bawah tombol New note, lalu tumbuh ke bawah dan ke kiri menjadi editor. Note baru menempati slot pertama (kiri atas), jadi kartu lain bergeser satu slot bersamaan; grid di-scroll ke atas dulu bila perlu.
- **Isi editor**: area teks (baris pertama = judul), baris bulatan warna dengan pemilih **ukuran teks** di ujung kanannya, lalu baris bawah: status `Saved`, tombol ikon **Float on top** (lihat "Note mengambang"), **Delete**, **Done**. Latar editor = warna note.
- **Ukuran teks note** (revisi 8): tiap note punya ukuran teks sendiri, dipilih saat menambah atau mengedit note. Pemilihnya kapsul kecil (radius 9 px, latar putih 8%) berisi empat tombol "A" 26 × 22 px yang hurufnya makin besar (10 / 11,5 / 13 / 15 px); tombol terpilih berlatar putih 18% (radius 7 px, konsentris dengan jarak 2 px). Tooltip dan nama aksesibel: `Extra small`, `Small`, `Medium`, `Large`. Note baru memakai Medium. Pilihan langsung berlaku di area teks (tanpa animasi) dan tersimpan seperti teks note.

| Ukuran | Editor di pulau | Note mengambang | Kartu: judul / isi |
|---|---|---|---|
| Extra small | 12 px | 11 px | 12 / 11 px |
| Small | 13 px | 12 px | 12,5 / 11,5 px |
| Medium (default) | 14 px | 13 px | 13 / 12 px |
| Large | 16 px | 15 px | 14,5 / 13,5 px |

- Kartu di grid ikut ukuran notenya, jadi tinggi kartu berubah dan grid masonry menata ulang dengan gerak biasa. Batas 8 baris isi kartu tetap berlaku.
- Keyboard ukuran teks: panah kiri/kanan di pemilih (pola `radiogroup`), dan saat mengetik di editor maupun di note mengambang `Ctrl+=` / `Ctrl+-` menaikkan/menurunkan satu tingkat, `Ctrl+0` kembali ke Medium.
- **Menutup editor**: tombol **Done**, Esc di editor (Esc berikutnya menutup pulau), atau klik di grid yang redup. Editor menyusut kembali ke slot kartunya (note baru: ke slot pertama), lalu diganti kartunya di frame yang sama tanpa kedip, dan kartu lain kembali terang. Note yang ditutup dalam keadaan kosong dibuang, seperti di Keep: note baru menyusut kembali ke tempat lahirnya sambil memudar; note lama yang isinya dihapus habis menyusut ke slotnya sambil memudar. **Delete** menyusutkan editor ke slotnya sambil memudar, lalu kartu lain merapat.
- **Bisa disela**: mengeklik kartu lagi saat editor masih menyusut membuat editor tumbuh lagi dari ukuran yang sedang tampil.
- **Mengatur urutan**: seret kartu. Kartu terangkat (bayangan dan skala 3%) dan mengikuti pointer 1:1 dari titik pegang; saat melewati kartu lain, urutannya bertukar dan kartu lain bergeser dengan animasi; saat dilepas, kartu mendarat ke slotnya dengan pegas. Dari keyboard: fokus kartu lalu Alt + panah (kiri/atas = lebih awal, kanan/bawah = lebih akhir). Selama pencarian aktif, hanya seretan yang tidak bisa: kartu tidak terangkat dan seretan tidak dimulai (klik tanpa geser tetap membuka editor). Alt + panah tetap bisa: kartu pindah ke tempat kartu yang tampil di sebelahnya di dalam urutan lengkap, dan urutan note yang tersembunyi oleh pencarian tidak berubah.
- **Cari**: menyaring kartu sesuai teks; editor yang terbuka ditutup dulu.
- **Warna note**: 8 pilihan, dipilih dari baris bulatan warna di bawah area teks saat note diedit. Warna dipakai untuk latar kartu dan latar area teks; note baru memakai Default. Semua warna sengaja gelap supaya teks putih tetap kontras minimal 4,5:1 (teks isi putih 88%, waktu putih 72%).

| Nama | Nilai |
|---|---|
| Default | `#1C1C1E` |
| Red | `#5C1F26` |
| Orange | `#643013` |
| Yellow | `#5C470C` |
| Green | `#1D4A31` |
| Teal | `#0F4C4B` |
| Blue | `#1C3C61` |
| Purple | `#42265A` |

**Note mengambang**

Note bisa dikeluarkan dari pulau menjadi jendela kecil yang selalu di atas jendela lain dan langsung bisa diedit, seperti sticky note di layar. Note yang keluar benar-benar meninggalkan pulau: kartunya tidak ada lagi di grid sampai note itu dikembalikan. Beberapa note bisa mengambang bersamaan.

- **Melepas dengan menyeret**: seret kartu seperti saat mengatur urutan, lalu bawa pointer melewati tepi pulau. Saat itu note "robek" dari pulau: **kartunya hilang dari grid** (kartu lain merapat menutup celahnya), seolah note itu keluar dari pulau, tetapi note yang diseret **tetap seukuran dan setampilan kartunya** dan mengikuti pointer 1:1. **Baru saat dilepas di luar pulau** note tumbuh menjadi jendela 260 × 220 px. Kalau pointer kembali ke dalam pulau sebelum dilepas, kartunya kembali ke grid dan seretan menjadi pengurutan biasa.
- **Satu benda dari awal sampai akhir** (revisi 8, supaya seretan tidak terasa patah): begitu kartu terangkat (setelah pointer bergeser 6 px), yang diseret adalah **satu jendela note** yang dibuat saat itu di atas pulau dan semua jendela lain, bukan kartu di dalam `IslandWindow`. Kartu aslinya disembunyikan di frame yang sama dan slotnya tetap dipakai untuk pengurutan. Akibatnya:
  - Tepi pulau tidak pernah memotong note yang diseret, jadi tidak ada momen kartu terpotong lalu tiba-tiba berganti jendela.
  - Selama diseret, di dalam maupun di luar pulau, jendela itu tampil persis seperti kartunya (ukuran, warna, radius 12 px, isi kartu) dengan skala 1,03 dan bayangan terangkat.
  - Saat pointer melewati tepi pulau, **ukuran dan isinya tidak berubah** (revisi 8): hanya kartunya di grid yang hilang dan bayangannya makin dalam, tanda note sudah keluar. Ukuran tidak berubah selama pointer masih ditekan, jadi **titik yang dipegang tetap di bawah pointer di setiap frame**: tidak ada lompatan posisi saat robek.
  - Kembali ke dalam pulau sebelum dilepas: kartunya kembali ke grid (kartu lain bergeser memberi tempat) dan bayangannya kembali ke bayangan terangkat.
  - Dilepas di dalam pulau: jendela terbang ke slot kartunya (pegas 340 ms dengan sedikit pantulan) lalu diganti kartunya di frame yang sama.
  - Dilepas di luar pulau: jendela yang sama langsung menjadi note mengambang (tidak dibuat ulang) dan **baru saat itu tumbuh** dari ukuran kartu ke 260 × 220 px dengan pegas membuka pulau, di sekitar titik tempat note dilepas (titik itu tetap di tempat selama tumbuh), dengan radius tetap 12 px, skala 1,03 → 1, dan isinya cross-fade 160 ms dari tampilan kartu ke tampilan jendela (bagian atas dengan grip dan area teks). Ukuran dihitung per frame lalu posisi dihitung dari ukuran itu. Setelah selesai tumbuh, jendela yang sebagian berada di luar area kerja memantul masuk. Pulau menutup sejak note dilepas.
- **Melepas dari editor** (revisi 8): note yang sedang diedit, termasuk note baru dari New note atau `Ctrl+Alt+N`, bisa langsung diseret keluar tanpa menutup editornya dulu. Pegangannya **grip di tepi atas editor**: strip setinggi 16 px dengan ikon grip abu-abu (putih 30%, 65% saat hover), kursor tangan, tooltip `Drag out of the island to float on top`. Seret dari grip mengikuti aturan "Satu benda" di atas dengan editor sebagai titik awal (ukuran editor, radius 16 px, teks editor), dan tulisan terakhir disimpan dulu. Melewati tepi pulau: ukuran dan teksnya tetap seperti editor. Dilepas di luar: baru saat itu jendela menyusut dari ukuran editor ke 260 × 220 px di sekitar titik lepas, radius 16 → 12 px, editor tertutup tanpa animasi menyusut (karena note-nya sudah keluar), note mengambang, pulau menutup, dan kursor teks berada di akhir note. Dilepas di dalam pulau: jendela kembali ke bingkai editor dan editor fokus lagi. Note baru yang masih kosong boleh dilepas; seperti note lain yang kosong, ia dibuang saat dikembalikan ke pulau. Di dalam editor, area teks tetap untuk memilih teks, bukan menyeret.
- **Dilepas di luar pulau**: jendela tinggal di posisi itu, **pulau langsung menutup**, dan kursor teks berada di akhir note, jadi pengguna bisa langsung mengetik. Untuk melepas note berikutnya, buka lagi halaman Notes dan seret kartu lain.
- **Melepas tanpa menyeret**: tombol ikon **Float on top** di editor (tooltip `Float on top`). Note muncul di kanan atas area kerja (24 px dari tepi kanan dan dari bar; setiap note mengambang berikutnya bergeser 24 px ke kiri bawah), kartunya keluar dari grid, pulau menutup, dan kursor teks pindah ke jendela note.
- **Jendela note** (dibuat sebersih mungkin):
  - Ukuran awal 260 × 220 px. Ukurannya bisa diubah dengan menarik **tepi atau sudut mana pun** (keempat sisi dan keempat sudut), tetapi tanpa ikon atau pegangan yang terlihat supaya tetap bersih: area tariknya 6 px di sepanjang tepi dan 12 × 12 px di sudut, dan hanya kursor yang berubah menjadi panah resize saat berada di sana. Sisi seberang tetap di tempat selama ditarik. Batas 200 × 140 sampai 520 × 640 px, dan jendela tidak bisa diperbesar keluar dari area kerja. Teks yang lebih panjang di-scroll di dalam jendela.
  - Latar warna note, **sudut 12 px sama dengan kartu note di pulau** (revisi 8, issue #23), jadi note yang keluar tetap terlihat sebagai benda yang sama. Windows 11 hanya menyediakan sudut 8 px (`DWMWCP_ROUND`), jadi jendela ini transparan dan menggambar sendiri sudut, garis tepi putih 10%, dan bayangannya di margin transparan di sekeliling note. Margin bayangan tidak menangkap klik (klik di sana tembus ke jendela di bawahnya), dan area tarik resize diukur dari tepi note yang terlihat, bukan dari tepi margin. Tidak tampil di taskbar Windows, tetapi ada di Alt+Tab supaya bisa dicapai dengan keyboard.
  - Bagian atas setinggi 28 px untuk menyeret jendela (ikon grip), dengan tombol **Back to island** di kanan (ikon panah ke atas masuk ke kapsul pulau, tooltip `Back to island`) yang muncul saat hover atau saat jendela fokus. Tidak ada tombol ×, karena note tidak pernah ditutup atau dihapus dari sini, hanya dikembalikan.
  - Sisanya area teks yang langsung bisa diedit, 13 px pada ukuran Medium (mengikuti ukuran teks note, lihat "Ukuran teks note"; `Ctrl+=` / `Ctrl+-` juga bekerja di sini). Baris pertama tetap menjadi judul. Tersimpan 0,5 detik setelah berhenti mengetik, sama dengan editor pulau. Esc melepas fokus dari jendela.
  - Hanya bisa dipindah di dalam area kerja (tidak menutupi bar Gili maupun taskbar Windows). Kalau dilepas sebagian di luar, jendela memantul masuk dengan pegas.
- **Back to island**: jendela terbang kembali ke pulau dan "masuk" ke dalamnya.
  - Pulau tertutup (compact atau wide): isi jendela memudar, lalu jendela mengecil menjadi kapsul hitam setinggi pulau sambil bergerak makin cepat ke tengah pulau, dan lenyap di sana. Pulau menggembung sekali saat note masuk, tanda note sudah diterima.
  - Halaman Notes sedang terbuka: jendela terbang ke slot kartunya di grid dan berubah menjadi kartu itu; kartu lain bergeser memberi tempat.
  - Note kembali ke posisinya semula dalam urutan pengguna (urutan tidak berubah selama mengambang). Note yang kosong saat dikembalikan dibuang.
- **Di pulau**: note yang mengambang tidak ada di grid dan tidak muncul di hasil cari. Menghapus note dilakukan di pulau setelah note dikembalikan.
- **Urutan tumpuk**: note mengambang berada di atas jendela biasa. Pulau yang dibuka selalu di atas note mengambang (`IslandWindow` memasang ulang posisi topmost-nya setiap kali membuka), kecuali note yang sedang diseret: selama seretan, jendelanya berada di atas pulau. Di antara note mengambang, yang terakhir diklik berada paling depan.
- **Fullscreen**: semua note mengambang disembunyikan saat aplikasi fullscreen aktif, sama seperti bar, lalu muncul lagi setelahnya.
- **Batas**: maksimal 6 note mengambang. Note ketujuh tidak bisa dilepas dan pulau menampilkan `Up to 6 floating notes`.
- **Tahan restart**: posisi dan ukuran tiap note mengambang tersimpan. Setelah Gili restart atau login, jendelanya muncul lagi di tempat yang sama; kalau resolusi, DPI, atau posisi bar berubah, jendela digeser masuk ke area kerja.

**Halaman Clipboard** (revisi 8)

Riwayat teks yang dicopy, di pulau. Tujuannya menempel ulang tanpa berpindah jendela: pengguna sedang mengetik di mana pun, membuka halaman ini, memilih satu teks, dan teks itu langsung muncul di posisi kursor.

- **Isi**: baris atas berisi kolom cari (`Search clipboard`) dan tombol **Clear all**. Di bawahnya daftar 50 teks terakhir, terbaru di atas. Tiap baris 44 px (radius 12 px, latar `#1C1C1E`): teks satu baris (13,5 px, spasi dan baris baru diringkas jadi satu spasi, dipotong "…") dan di bawahnya aplikasi sumber + waktu (`Chrome · 38 min`, `now`, `2 h`, `Yesterday`). Tooltip baris menampilkan teks sampai 400 karakter.
- **Tinggi**: pulau memanjang mengikuti daftar seperti halaman lain (6.3 "Tinggi pulau terbuka"); setelah pulau setinggi layar, hanya daftar yang di-scroll, kolom cari dan menu tetap di tempat.
- **Menempel**: klik baris (atau Enter pada baris yang disorot) →
  1. teks itu ditaruh di clipboard dan barisnya naik ke atas daftar;
  2. pulau menutup;
  3. Gili mengembalikan fokus ke jendela yang terakhir aktif sebelum pulau dibuka (aplikasi aktif di kiri bar), lalu mengirim Ctrl+V, jadi teks muncul di posisi kursor di jendela itu.
  Teks ditempel sebagai teks biasa. Isi clipboard tidak dikembalikan ke isi sebelumnya; teks yang dipilih tetap ada di clipboard, sama seperti `Win+V`. Karena Gili sendiri yang menulis clipboard, tidak ada cuplikan clipboard di slot.
- **Tanpa jendela tujuan** (desktop, jendela sudah ditutup) atau **jendela admin** (Windows memblokir input dari proses biasa ke proses admin): teks hanya ditaruh di clipboard dan pulau menampilkan wide `Copied · press Ctrl+V to paste` selama 2 detik.
- **Keyboard**: `Ctrl+Alt+V` membuka halaman ini dengan kolom cari fokus dan baris pertama tersorot; mengetik langsung menyaring daftar, ↑/↓ memindah sorotan, Enter menempel, Delete menghapus baris yang disorot (saat fokus di daftar), Esc menutup pulau tanpa menempel.
- **Hapus**: hover baris menampilkan tombol × (`Remove`) di kanannya; satu klik menghapus baris itu (baris menyusut, yang lain merapat). **Clear all** butuh dua klik: klik pertama mengubahnya menjadi `Clear all?` merah selama 3 detik.
- **Kosong**: `Nothing copied yet` dengan baris kecil `Text you copy shows up here`. Hasil cari kosong: `No matches`.
- **Yang masuk riwayat**: hanya teks (gambar dan file tetap hanya cuplikan di slot, 6.3 "Cuplikan clipboard"). Isi sensitif (password manager) tidak pernah masuk. Teks yang sama yang dicopy lagi naik ke atas, tidak menjadi baris kedua. Maksimal 50; yang paling lama dibuang.
- Halaman ini menggantikan tab Clipboard di jendela Tools (7.5).

**Menambah todo (hanya lewat Calendar)**
- Todo **hanya dibuat dari halaman Calendar**: pilih tanggal, lalu **Add task** di bawah daftar tanggal itu. Kolom input muncul di tempat: judul tugas, lalu kolom **jam** kecil di kanannya (placeholder `Time`, format 24 jam `15:00`, opsional). Enter menyimpan, Esc membatalkan, Tab berpindah dari judul ke jam.
- **Jam**: tugas dengan jam tampil sebagai `15:00 · Prepare meeting slides` di Calendar dan dengan jam di kanan baris Todo. Pada jam itu, kartu pengingat todo muncul (6.3 "Kartu notifikasi"). Tugas tanpa jam tidak punya pengingat. Jam yang sudah lewat untuk hari ini tetap disimpan, tanpa pengingat, dan kolomnya diberi garis kuning sebagai tanda.
- Halaman Todo tidak punya kolom input maupun tombol untuk menambah (tombol **Add in Calendar** dihapus di revisi 8). Jalan pintas dari mana pun tetap `Ctrl+Alt+T` (F-TOOL-10), yang membuka Calendar di hari ini dengan kolom Add task terbuka dan fokus.
- **Umpan balik**: tugas untuk hari ini langsung muncul di daftar Todo dengan animasi. Tugas untuk tanggal lain tidak masuk daftar hari ini; pulau menampilkan konfirmasi singkat `Added to Tue 28 Oct`, dan tanggal itu di Calendar mendapat titik. Karena pulau sedang terbuka di Calendar, konfirmasi itu tampil sebagai baris hijau kecil di bawah daftar tanggal selama 2,4 detik.

**Mengubah dan menghapus todo** (di halaman Todo)
- Hover atau fokus baris menampilkan dua tombol ikon 28 px di kanan baris: **Edit** (pensil) dan **Delete** (tempat sampah). Jam tugas bergeser ke kiri tombol itu.
- **Edit**: baris berubah menjadi kolom judul + kolom jam di tempat (baris memanjang dengan pegas); Enter atau tombol centang menyimpan, Esc membatalkan. Mengosongkan jam menghapus pengingatnya.
- **Delete**: klik pertama mengubah tombol menjadi `Delete?` merah selama 3 detik; klik kedua menghapus baris (baris menyusut dan lenyap, baris lain merapat, pulau memendek). Sama seperti hapus note, tanpa dialog.
- Dari keyboard: fokus baris, `Enter` = edit, `Delete` dua kali = hapus.
- Pengelolaan lengkap (tanggal jatuh tempo, urutan, filter All/Done) tetap di jendela Tools.

**Membuka pulau**
- Klik pulau compact. Halaman yang dibuka sesuai aktivitas di slot (Media, Timer); kalau tidak ada, halaman terakhir yang dilihat. Slot Claude punya aturan sendiri (lihat "Sesi Claude").
- Kalau slot menampilkan **timer berlabel** (timer dari tab Timer di Tools, bukan timer pulau), klik slot membuka jendela Tools di tab Timer dan pulau tetap compact, karena halaman Timer di pulau hanya mengenal satu timer. Timer pulau tetap membuka halaman Timer.
- Klik Gili saat tidak ada aktivitas: halaman terakhir yang dilihat.
- Klik jam atau tanggal di kanan bar: halaman Calendar (sebelum Fase 2: halaman terakhir), dengan aturan buka/tutup yang sama seperti ikon bar lain.
- Klik CPU/RAM/GPU/disk di kanan: halaman System (sebelum Fase 3 berisi `Coming in Phase 3`).
- Modul Wi‑Fi, ikon Bluetooth, Volume, Brightness, dan tombol Power: pulau kontrolnya masing-masing, tanpa menu tab ("Pulau kontrol" di 6.4).
- Ikon Timer / Alarms / Todo / Clipboard di launcher: halaman Timer / Alarms / Todo / Clipboard. `Ctrl+Alt+V`: halaman Clipboard dengan kolom cari langsung fokus (Fase 4).
- Semua ikon bar yang membuka halaman pulau (launcher, Shelf, Claude, modul sistem, jam, Wi‑Fi, Bluetooth, Volume, Brightness, dan Power di kanan) bekerja sebagai tombol buka/tutup: saat pulau terbuka, klik berpindah ke halaman atau kontrolnya; klik saat halamannya sedang terbuka menutup pulau.
- Ikon Notes di launcher: halaman Notes dengan grid kartu (editor tertutup). `Ctrl+Alt+N`: halaman Notes dengan note baru kosong dan editor langsung terbuka dan fokus (Fase 4).
- `Ctrl+Alt+Space` (Fase 4).

**Pindah halaman**: scroll horizontal touchpad, roda mouse di area halaman (kecuali di atas daftar yang bisa di-scroll, termasuk daftar Clipboard, dan di seluruh halaman Notes, tempat roda mouse vertikal hanya men-scroll kartu), klik tab di menu halaman, atau tombol ← →.

**Menutup pulau**: klik di luar, Esc, `Ctrl+Alt+Space`, atau mouse berada di luar pulau selama 3 detik. Penutupan otomatis ditunda selama kolom teks atau editor note fokus. Note yang sedang ditulis disimpan sebelum pulau menutup. Note mengambang tetap tampil saat pulau menutup.

#### Kartu notifikasi

| Kartu | Isi | Tombol |
|---|---|---|
| Alarm | Ikon alarm, jam, label | Snooze 5m, Stop |
| Timer habis | `Timer 10:00 done`, label | Repeat, Close |
| Timer berlabel habis | Judul `<label> done` (contoh `Tea done`), baris kedua `Timer 10:00`; ikon jam pasir oranye seperti kartu timer | Repeat, Close |
| Jendela Claude 5 jam berakhir | `Claude usage reset`, `Your 5-hour limit is available again` | Close, Open Claude |
| Pengingat todo | Judul tugas, waktu jatuh tempo | Snooze 10m, Done |
| Fokus selesai | `Focus session done`, `5-minute break · session 1 of 4` | Skip, Start break |
| Istirahat selesai | `Break over`, `Session 2 of 4 ready` | Stop, Start focus |
| Konflik file Explorer | `Replace or skip files`, `Project-Final.zip already exists in D:\Backup` | Skip, Replace |
| File sedang dipakai | `File in use`, `budget.xlsx is open in Excel` | Skip, Try again |
| Butuh izin admin | `Needs administrator permission`, nama folder tujuan | Open dialog |
| Claude butuh jawaban | `Claude needs your input`, `Claude needs permission`, atau `Plan ready for review`; nama sesi; cuplikan pertanyaan | Later, Open Claude (sesi terminal: Open terminal) |

Suara tiap kartu diatur di 6.6. Kalau alarm atau timer tidak ditanggapi dalam 5 menit, suara berhenti, kartu mengecil ke compact dengan tanda merah, dan toast "Missed alarm 14:30" tetap di Notification Center.

#### Shelf (tempat file sementara)

- Menyeret file (dari Explorer, desktop, atau aplikasi apa pun yang bisa drag file) ke atas bar atau pulau mengubah pulau menjadi drop zone. Melepas file menambahkannya ke Shelf lalu membuka halaman Shelf; menyeret menjauh tanpa melepas mengembalikan keadaan sebelumnya.
- **Satu kali drop = satu item.** Satu file menjadi tile file biasa; beberapa file yang dilepas bersamaan menjadi satu **bundel** (`12 files`) yang diseret keluar sekaligus. Hover tile menampilkan nama dan ukuran file di dalamnya.
- Maksimal **8 item**, cukup untuk grid 4 × 2 tanpa scroll. Kalau Shelf penuh, drop zone menampilkan `Shelf is full · drag something out first` dan file tidak ditambahkan.
- Shelf menyimpan **referensi** ke file asli, bukan salinan; tetap ada setelah restart.
- Menyeret tile keluar menawarkan Copy dan Move ke tujuan (Explorer yang memilih; Shift memaksa Move). Setelah Move berhasil, item keluar dari Shelf; setelah Copy, item tetap.
- File yang sudah tidak ada dibuang dari Shelf saat halaman Shelf dibuka dan setiap 1 menit. Bundel yang semua filenya hilang ikut terhapus.
- Folder yang dilepas masuk sebagai satu item.
- Drop di bar maupun di pulau sama-sama masuk Shelf; drop zone menyala hijau saat kursor di atas pulau (Fase 3d). Gili selalu melaporkan efek Copy ke sumber seret, supaya sumber tidak pernah menghapus file asli. Setelah tile diseret keluar, Gili memeriksa ulang file-nya: yang sudah tidak ada di tempat asal (dipindahkan) keluar dari Shelf.

### 6.4 Bagian kanan

**Gaya dua baris** (dipilih user 2026-10-26, mirip iStat Menus): tiap modul menampilkan label kecil di atas dan nilainya di bawah.

- Label: 8.5 px, semibold, huruf kapital, jarak huruf 0.06 em, warna `#9A9AA0`.
- Nilai: 11.5 px, semibold, digit tabular, warna teks utama.
- Jaringan memakai dua baris nilai tanpa label: upload di atas, download di bawah (9.5 px), lebar minimum 58 px supaya bar tidak bergeser saat angkanya berubah.
- Ikon (cuaca, Wi‑Fi) berada di kiri blok dua baris; ikon power berdiri sendiri.
- Tinggi area klik modul = tinggi bar − 6 px (minimal 24 px). Di tinggi bar terkecil (28 px) dua baris tetap muat.
- Jarak antar-modul rapat dan rata, seperti deretan ikon toolbar: 10 px dari isi satu modul ke isi modul berikutnya (padding area klik 4 px di tiap sisi + celah 2 px; dirapatkan user 2026-10-26). Area klik modul ikon minimal 24 px lebar.

Urutan default dari kiri ke kanan:

| Modul | Default | Tampilan (label / nilai) | Interaksi |
|---|---|---|---|
| Cuaca | Nyala | Ikon cuaca + `CLOUDY` / `29°` (label = kondisi singkat) | Hover: kondisi, tertinggi/terendah hari ini, kota |
| Jaringan | Nyala | `↑ 0.3 MB/s` / `↓ 2.1 MB/s` (satuan B/KB/MB otomatis) | Hover: adapter, total hari ini |
| CPU | Nyala | `CPU` / `12%` | Klik: halaman System |
| RAM | Nyala | `RAM` / `54%` | Hover: `8.6 / 16 GB`. Klik: halaman System |
| GPU | Mati | `GPU` / `4%` | Klik: halaman System |
| Disk | Mati | `C: FREE` / `120 GB` (huruf drive Windows; satuan GB seperti Explorer) | Hover: `Free space on C:`. Klik: halaman System |
| Wi‑Fi | Nyala | Ikon sinyal + kekuatan sinyal / nama jaringan (`STRONG` / `Home_5G`, nama maks. 16 karakter) | Klik: pulau Wi‑Fi (lihat "Pulau kontrol") |
| Bluetooth | Nyala | Ikon Bluetooth saja; titik biru kecil di pojok saat ada perangkat tersambung | Klik: pulau Bluetooth. Hover: `Bluetooth · Galaxy Buds2 Pro` atau `Bluetooth · Off` |
| Volume | Nyala | Ikon speaker yang mengikuti keadaan: mute, pelan (< 34%), sedang, keras (≥ 67%) | Klik: pulau Volume. Hover: `Volume 46%` atau `Muted` |
| Brightness | Nyala | Ikon matahari; disembunyikan kalau layar tidak mendukung pengaturan brightness | Klik: pulau Brightness. Hover: `Brightness 70%` |
| Tray | Nyala | Ikon chevron (`^` saat bar di atas: menunjuk ke bawah; saat bar di bawah: ke atas) | Klik: panel tray (di bawah). Hover: `Show hidden icons` |
| Jam dan tanggal | Nyala | Gaya dua baris: tanggal di atas sebagai label (`SAT 25 OCT`), jam di bawah sebagai nilai dengan detik (`14:32:05`, atau `2:32:05 PM` bila 12 jam), digit tabular dan lebar tetap supaya bar tidak bergeser setiap detik. Detik bisa dimatikan (Settings → General → **Show seconds**, default nyala), menjadi `14:32` | Klik: halaman Calendar. Hover: `Saturday, 25 October 2026` |
| Settings | Selalu | Ikon roda gigi, tepat di kiri Power | Klik: jendela Settings. Hover: `Settings` |
| Power | Selalu, paling kanan | Ikon power; titik oranye kecil di pojoknya kalau ada update Windows yang menunggu dipasang | Klik: pulau Power |

- Ikon baterai tidak ditampilkan di bar; status ngecas tampil di pulau, dan sisa baterai ada di pulau Power.
- **Tampil/sembunyi** (Settings → Right side): Cuaca, Jaringan (bandwidth), CPU, RAM, GPU, Disk, Wi‑Fi, Bluetooth, Volume, Brightness, Tray, dan Jam masing-masing punya saklar, dan modul sistem (Cuaca, Jaringan, CPU, RAM, GPU, Disk) bisa diurutkan dengan seret lewat pegangan titik-titik di kiri barisnya, atau Alt + panah atas/bawah pada saklarnya (Fase 3c). Modul yang sedang di "…" tetap dibaca karena daftarnya menampilkan angka. Settings dan Power selalu tampil. GPU dan disk tetap tersedia di halaman System walaupun modulnya mati. Modul yang dimatikan juga berhenti mengambil datanya (misalnya cuaca tidak lagi mengakses internet, counter CPU berhenti dibaca kalau CPU, RAM, dan halaman System semuanya tidak tampil).
- Kalau modul tidak muat dalam aturan jarak 24 px, modul pindah ke menu "…" dengan urutan: Cuaca, Jaringan, Disk, GPU, RAM, CPU. Wi‑Fi, Bluetooth, Volume, Brightness, Tray, Jam, Settings, dan Power selalu tampil.
- **Sampai Fase 3** (rencana 1e, keputusan pengguna 2026-10-27: tampilan bar lengkap dengan data asli dan fungsi dasar):
  - Cuaca memakai kota default Jakarta (−6,2088, 106,8456) sampai Settings punya pilihan kota; nama kota tampil di tooltip.
  - Klik Wi‑Fi, Bluetooth, Volume, dan Brightness membuka Quick Settings Windows (`Win+A`) sampai Fase 3b, yang menggantinya dengan pulau kontrol; klik Tray membuka ikon tersembunyi Windows (`Win+B` lalu `Enter`). Pulau kontrolnya dan panel tray menyusul di Fase 3.
  - Power membuka pulau Power tanpa item `Update and …`, karena deteksi update yang menunggu baru diverifikasi di Fase 3 (selesai di Fase 3e: kunci `RebootRequired` atau Windows Update Agent, keduanya cocok di laptop pengguna; tooltip ikon `Power · updates are ready to install`).
  - Tanpa izin Location, Wi‑Fi hanya menampilkan ikon dengan tooltip `Allow location access for Gili to show the Wi‑Fi name (Settings > Privacy & security > Location)`.
  - Klik CPU dan RAM membuka tab System yang berisi `Coming in Phase 3`, seperti mockup (selesai di Fase 3c: halaman System berisi grafik).

**Panel tray** (ikon aplikasi latar belakang)
- Panel kecil yang turun dari tombol Tray (latar `#1C1C1E`, radius 12 px, bayangan lembut), berisi grid ikon notification area **semua** aplikasi latar belakang (yang di taskbar Windows tampil langsung maupun yang tersembunyi di overflow), 16 px dalam sel 32 × 32 px, maksimal 6 kolom.
- Klik kiri, klik kanan (menu konteks aplikasinya), dan klik dua kali diteruskan ke aplikasi pemilik ikon, jadi perilakunya sama seperti di taskbar Windows. Hover menampilkan tooltip aplikasi.
- Ikon diperbarui langsung saat aplikasi menambah, mengubah, atau menghapus ikonnya. Ikon berkedip/animasi dari aplikasi ditampilkan apa adanya.
- Menutup dengan klik di luar, Esc, atau setelah aksi dijalankan. Bisa dipakai dengan keyboard (panah, Enter, Shift+F10 untuk menu konteks).
- Kosong: `No background apps`.

**Pulau kontrol** (pengganti Quick Settings dan menu power; dipilih user 2026-10-26, menggantikan halaman Controls gabungan)

Wi‑Fi, Bluetooth, volume, brightness, dan power masing-masing punya **pulau sendiri**. Klik ikonnya di bar membuat pulau membesar **hanya berisi kontrol itu**, tanpa menu tab dan tanpa kontrol lain. Ukuran pulau mengikuti isi kontrolnya, jadi pulau Volume kecil dan pulau Wi‑Fi memanjang mengikuti daftar jaringannya.

- **Gerak dan posisi** sama dengan pulau terbuka: tepi atasnya sama dengan pulau compact, pegas buka/tutup yang sama, latar bar transparan, elemen yang tertimpa memudar (6.3). Radius sudut 28 px, isi diberi jarak 16 px dari tepi (radius dalam 12 px).
- **Buka/tutup**: klik ikon yang sama, Esc, klik di luar pulau, atau mouse di luar pulau selama 3 detik menutupnya (ditunda selama slider sedang digeser). Klik ikon kontrol lain saat pulau kontrol terbuka langsung mengganti isinya; pulau berubah ukuran dengan pegas, isi lama memudar 90 ms dan isi baru muncul 180 ms.
- **Tidak ada menu tab**: panah kiri/kanan dan geser touchpad tidak berpindah halaman. Pulau kontrol bukan halaman, jadi tidak muncul di menu halaman dan tidak bisa disembunyikan lewat Settings → Island (ikonnya yang disembunyikan lewat Settings → Right side).
- **Prioritas** sama dengan pulau yang dibuka pengguna: di atas notifikasi Windows, kejadian wide, dan aktivitas, di bawah kartu dan drop zone. Kartu alarm menggantikannya sementara, lalu pulau kontrol kembali setelah kartu ditanggapi.
- Ambient glow biru `#0A84FF` untuk semua pulau kontrol.
- Pembacaan status (daftar Wi‑Fi, perangkat Bluetooth, perangkat audio) hanya berjalan selama pulau kontrolnya terbuka. Nilai volume, mute, dan status Bluetooth untuk ikon bar dibaca dari event sistem, bukan polling.

| Pulau | Ukuran | Isi |
|---|---|---|
| Volume | 360 px, tinggi ikut isi (± 112 px) | Baris judul: ikon speaker (klik = mute/unmute), `Volume`, persen di kanan. Slider 0–100. Baris perangkat output (`Speakers (Realtek(R) Audio)` + `›`); klik membuka daftar perangkat output di dalam pulau (centang di perangkat aktif, klik = jadikan default), pulau memanjang mengikutinya |
| Brightness | 360 px, ± 88 px | Baris judul: ikon matahari, `Brightness`, persen. Slider 0–100 |
| Wi‑Fi | 400 px, tinggi ikut daftar (maks. 6 jaringan, sisanya di-scroll) | Baris judul: ikon Wi‑Fi, `Wi‑Fi`, saklar. Baris **Airplane mode** dengan saklar (nyala mematikan Wi‑Fi dan Bluetooth). Daftar jaringan dari sinyal terkuat: tersambung paling atas dengan `Connected`, lalu `Saved`, `Secured`, atau `Open` (tanpa keamanan); satu baris per nama jaringan, jaringan tersembunyi tidak tampil. Klik jaringan tersimpan langsung menyambung (`Connecting…` lalu `Connected`); jaringan lain membuka daftar jaringan Windows (`ms-availablenetworks:`), karena Gili tidak menangani password atau profil baru. Baris terakhir **More Wi‑Fi settings** (`ms-settings:network-wifi`). Saat Wi‑Fi mati, daftar diganti `Wi‑Fi is off`; saat Airplane mode nyala, `Airplane mode is on`. Airplane mode memakai API radio resmi: nyala mematikan semua radio, mati menyalakan Wi‑Fi dan Bluetooth; saklarnya nyala bila flag airplane Windows nyala atau kedua radio mati. Kalau Windows menolak menyalakan radio karena airplane sistem masih nyala, Gili membuka `ms-settings:network-airplanemode` (Fase 3b) |
| Bluetooth | 400 px, tinggi ikut daftar (maks. 6 perangkat) | Baris judul: ikon Bluetooth, `Bluetooth`, saklar. Daftar perangkat ter-pair dengan ikon jenisnya dan status `Connected` / `Paired` (plus baterai bila dilaporkan); klik perangkat audio menyambung/memutus langsung (`Connecting…`; gagal: `Couldn't connect` selama 3 detik), klik perangkat lain membuka `ms-settings:bluetooth`, karena Windows tidak punya API publik untuk menyambung perangkat non-audio. Baris terakhir **Add device** (`ms-settings:bluetooth`). Saat Bluetooth mati: `Bluetooth is off` |
| Power | 360 px, tinggi ikut isi | Baris judul: ikon power, `Power`, dan baterai di kanan (`64% · 2 h 10 min`, `Charging · 64%`, atau kosong tanpa baterai). Tombol berurutan seperti Start menu Windows 11 (tabel di bawah), lalu baris bawah dengan garis di atasnya: tombol **Windows Quick Settings** (panel bawaan Windows untuk hal lain seperti Night light, Cast, Accessibility) dan **All settings** (`ms-settings:`). Menekan tombol mana pun menutup pulau lalu menjalankan aksinya |

| Tombol Power | Kapan tampil | Aksi |
|---|---|---|
| Sleep | Selalu | Laptop tidur |
| Update and shut down | Hanya saat ada update yang menunggu | Pasang update, lalu matikan |
| Shut down | Selalu | Matikan |
| Update and restart | Hanya saat ada update yang menunggu | Pasang update, lalu restart |
| Restart | Selalu | Restart |

- Tombol update dan titik di ikon Power memakai titik oranye. Tidak ada konfirmasi tambahan untuk Sleep/Shut down/Restart; seperti Start menu, butuh dua klik (buka pulau, pilih tombol). Kalau ada aplikasi dengan pekerjaan belum disimpan, Windows sendiri yang menampilkan layar "These apps are preventing shutdown".
- Slider berlaku langsung saat digeser; flyout volume/brightness bawaan Windows boleh ikut muncul. Scroll di atas slider mengubah nilainya 2 poin.
- Semua isi bisa dipakai dengan keyboard: Tab berpindah antar-kontrol, panah mengubah slider, Enter/Spasi menekan tombol dan saklar.
- Provider yang gagal (tanpa adapter Wi‑Fi, tanpa speaker) menampilkan `Not available` di tempat daftar atau slider (PRD 11).

### 6.5 Settings

Jendela Settings dibuka dari tombol roda gigi di kanan bar, dari menu klik kanan bar, atau dengan menjalankan Gili saat sudah berjalan. Jendela gelap 640 × 460 dengan bilah judul sendiri dan bagian di kiri, urutannya seperti tabel di bawah (mockup). Semua perubahan berlaku langsung tanpa tombol Save.

| Bagian | Isi |
|---|---|
| General | Bar position (Top / Bottom), Hide Windows taskbar, 12-hour clock (default mati), Show seconds, Start with Windows |
| Appearance | Bar opacity (default 100%), Glass (Off / Light / Strong), Bar height, Island height, Ambient glow, Ambient motion |
| Left side | Saklar Start, Timer, Alarms, Todo, Notes, Clipboard, Shelf, Claude. Tiap saklar selain Start juga menyalakan/mematikan tab halaman yang sama di pulau, dengan keterangan `Turning it off also hides the <name> page in the island` |
| Island | Show Gili; Show lyrics (default mati); saklar tiap halaman (Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, Claude, System); saklar halaman yang ikon kirinya mati nonaktif dengan keterangan `Turned off in Left side` |
| Right side | Saklar dan urutan Weather, Network, CPU, RAM, GPU, Disk; saklar Wi‑Fi, Bluetooth, Volume, Brightness, Tray icons, Clock and date; **Weather city**; **Battery alert** (ambang peringatan rendah 5–50%, keterangan `Also at 10%`); **Unplug reminder** (50–100%, default 80%, keterangan `Only on laptops that keep charging past it. 100% turns it off`) |
| Notifications | Show Windows notifications (default mati; meminta izin Windows), Show message text (default nyala), saklar per aplikasi yang pernah mengirim notifikasi, baris status izin, dan tips mematikan banner aplikasi di Windows (revisi 8) |
| Claude | Show Claude Code sessions (F-CLD-01), Show 5-hour usage timer, baris status integrasi |
| Sounds | Timer sound, Alert volume, Gradually increase, contoh suara |
| Shortcuts | Hotkey (Fase 4) |
| About | Versi dan jenis instalasi (`Microsoft Store` / `GitHub`), baris update (`Up to date` atau `Update available · <versi>` dengan Install update; 9.4), tautan Privacy policy dan Release notes, tombol Open logs folder, dan Prepare to uninstall (versi Store, 9.4) |

- Bagian yang belum ada fasenya tetap tampil tapi nonaktif dengan keterangan `Coming in a later version` sampai fasenya selesai.
- Saklar yang menyembunyikan sesuatu langsung menganimasikan bar (elemen mengecil dan deretan merapat), jadi hasilnya terlihat sambil mengatur.

### 6.6 Suara

**Suara bawaan**: lima file suara pendek yang ditanam di aplikasi. Semuanya dibuat sendiri atau berlisensi CC0, jadi aman dipakai tanpa izin tambahan.

| Nama | Karakter | Default untuk |
|---|---|---|
| Radar | Tiga beep cepat yang naik, berulang tiap 1,2 detik | Alarm |
| Chime | Dua nada lonceng tinggi, berulang tiap 2 detik | Timer habis |
| Bell | Satu dentang lonceng yang dalam | Fase pomodoro selesai |
| Soft | Tiga nada lembut yang naik | Pilihan tambahan untuk alarm/timer |
| Ping | Satu nada pendek | Pengingat todo, Claude butuh jawaban |

**Kapan berbunyi**

| Kejadian | Suara | Cara bunyi |
|---|---|---|
| Alarm | Dipilih per alarm (default Radar) | Berulang sampai Snooze atau Stop, maksimal 5 menit; volume naik bertahap |
| Timer habis | Dipilih di Settings (default Chime) | Berulang sampai Repeat atau Close, maksimal 5 menit; langsung di volume penuh |
| Jendela Claude 5 jam berakhir | Sama dengan suara timer | Berulang sampai Close atau Open Claude, maksimal 5 menit; ditahan selama fase fokus pomodoro |
| Fase pomodoro selesai | Bell | Sekali |
| Pengingat todo | Ping | Sekali; dibisukan selama fase fokus pomodoro (ikut F-TOOL-04) |
| Claude butuh jawaban | Ping | Sekali per pertanyaan; dibisukan selama fase fokus pomodoro |
| Kejadian lain (wide, Shelf, clipboard, charger, Claude bekerja atau selesai) | — | Tanpa suara |

**Volume**
- Suara Gili mengikuti volume Windows. Settings punya **Alert volume** (0–100%, default 80%) yang hanya mengatur volume sesi audio Gili, bukan volume sistem.
- **Gradually increase** (default nyala): alarm mulai di 30% dari Alert volume, lalu naik linear ke 100% dalam 30 detik. Tidak berlaku untuk timer.
- Tombol apa pun di kartu (Snooze, Stop, Repeat, Close, Done, Skip) menghentikan suara seketika dengan fade 100 ms.
- Settings dan editor alarm di Tools punya tombol putar untuk mendengar contoh tiap suara.

**Windows di-mute atau volumenya terlalu kecil**
- Sebelum berbunyi, Gili memeriksa perangkat output default. Kalau di-mute atau volumenya di bawah 5%, kartu menampilkan baris peringatan kuning `Sound is off · Windows volume is muted` dan tombol tambahan **Unmute**.
- Gili **tidak pernah** mengubah volume atau mute Windows tanpa tindakan pengguna. Unmute hanya terjadi saat pengguna menekan tombol itu, lalu suara alarm langsung mulai.
- Animasi kartu (pulse/shake) tetap jalan, jadi alarm tetap terlihat walaupun tanpa suara.

**Perangkat dan keadaan lain**
- Suara keluar di perangkat output default Windows. Kalau perangkat berganti saat berbunyi (misalnya headset dicabut), suara pindah ke perangkat default yang baru tanpa berhenti.
- Do Not Disturb Windows tidak membisukan alarm dan timer (seperti alarm di ponsel), karena suaranya diputar Gili sendiri, bukan lewat toast.
- Saat aplikasi fullscreen aktif, alarm dan timer tetap berbunyi (prioritas 1).
- Kalau tidak ada perangkat audio sama sekali, kartu tetap muncul dan toast dikirim **dengan** suara notifikasi Windows sebagai cadangan.

## 7. Kebutuhan fungsional

ID dipakai untuk melacak di rencana implementasi. Kolom Fase merujuk ke bagian 14.

### 7.1 Bar dan sistem

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-BAR-01 | Bar terdaftar sebagai AppBar di monitor utama | Jendela maximize mulai tepat di bawah bar; taskbar Windows tidak berubah kecuali Hide Windows taskbar dinyalakan (F-BAR-11) | 1 |
| F-BAR-02 | Bar sembunyi saat fullscreen | YouTube fullscreen dan game fullscreen tidak tertutup; bar kembali ≤ 1 detik setelah keluar | 1 |
| F-BAR-03 | Bar pulih dari kejadian sistem | Setelah Explorer restart, resolusi/DPI berubah, atau bangun dari sleep, bar kembali di posisi benar dan area kerja tetap berkurang 32 px | 1 |
| F-BAR-04 | Autostart saat login | Setelah restart, bar muncul ≤ 2 detik setelah desktop tampil; bisa dimatikan di Settings | 1 |
| F-BAR-05 | Hanya satu instance | Menjalankan app kedua kalinya hanya memfokuskan instance yang sudah jalan | 1 |
| F-BAR-06 | Material bar | Opacity dan tingkat kaca mengubah bar langsung; Transparency effects Windows mati memaksa bar polos; pulau tetap hitam pekat | 1 |
| F-BAR-07 | Jarak dan overflow | Di lebar 1366 px dan 1920 px, isi kiri dan kanan berjarak minimal 24 px dari pulau di semua keadaan pulau yang masih menampilkan bar | 1 |
| F-BAR-08 | Settings | Jendela Settings dengan bagian di 6.5: jam 12/24, opacity bar (default 100%), tingkat kaca, tinggi bar (28–44 px), tinggi pulau (20 px – tinggi bar − 4), ambient glow/motion, Gili, modul kanan (nyala/urutan), ambang baterai, kota cuaca, hotkey, autostart | 1 (jam, material, tinggi bar dan pulau, autostart), 3 (modul, baterai), 4 (cuaca, hotkey) |
| F-BAR-09 | Mode demo | "Demo mode" memutar skenario palsu (media, ngecas, notifikasi, copy, drop ke Shelf, Gili berpindah bentuk, dan mulai Fase 3 sesi Claude serta jendela 5 jam) untuk mengecek UI | 1 |
| F-BAR-10 | Posisi bar atas/bawah | Bottom hanya bisa dipilih saat taskbar Windows disembunyikan; di bawah, jendela maximize berakhir tepat di atas bar dan semua panel mengambang terbuka ke atas sesuai 6.1; memunculkan taskbar Windows lagi memindahkan bar ke atas | 3 |
| F-BAR-11 | Sembunyikan taskbar Windows | Saklar menyembunyikan taskbar Windows dan ruangnya kembali ke jendela lain; status asli dipulihkan saat saklar mati, Quit, crash, dan start berikutnya setelah mati paksa; tetap tersembunyi setelah Explorer restart; tombol Windows tetap membuka Start | 3 |
| F-BAR-12 | Tampil/sembunyi | Setiap saklar di 6.5 (Left side, Island, Right side) langsung menampilkan/menyembunyikan elemennya dengan animasi; saklar Left side selain Start juga mematikan/menyalakan tab pulau yang sama dan menonaktifkan saklar tab itu selama mati, tanpa pernah menyisakan nol tab; elemen yang mati tidak menyisakan celah dan berhenti mengambil data; pilihan tersimpan setelah restart | 3 |
| F-BAR-13 | Tombol Settings | Roda gigi selalu tampil tepat di kiri Power dan membuka jendela Settings; kalau Settings sudah terbuka, jendelanya dibawa ke depan | 1 |

### 7.2 Bagian kiri

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-LEFT-01 | Aplikasi berjalan dan aplikasi aktif | Ikon asli semua aplikasi yang punya jendela di taskbar tampil setelah pemisah; aplikasi aktif selalu menampilkan nama dan aplikasi lain menampilkan namanya inline begitu di-hover (6.2); perpindahan nama dimulai ≤ 200 ms setelah fokus pindah dengan animasi 6.2; aplikasi yang dibuka/ditutup masuk/keluar dari deretan; klik ikon membawa aplikasi ke depan; overflow ke `+N`; berlaku untuk aplikasi desktop (.exe) dan Store (UWP/MSIX); ikon tajam di DPI 100–200% | 1 |
| F-LEFT-02 | Launcher | Timer, Alarms, Todo, Notes, dan Clipboard (Fase 4) membuka halamannya di pulau; saat pulau terbuka klik berpindah halaman tanpa menutup-membuka ulang; klik ikon halaman yang sedang terbuka menutup pulau | 3 |
| F-LEFT-03 | Ikon Clipboard dan riwayat | Ikon Clipboard di launcher setelah Notes membuka halaman Clipboard; 50 teks terakhir tersimpan (tahan restart), terbaru di atas, teks yang sama dicopy lagi naik ke atas tanpa duplikat; isi sensitif tidak pernah masuk | 4 |
| F-LEFT-04 | Ikon Shelf | Selalu tampil tepat setelah launcher; jumlah file sesuai isi Shelf; memantul saat file baru masuk; klik membuka halaman Shelf | 3 |
| F-LEFT-06 | Tombol Start | Paling kiri; klik membuka Start menu Windows dan klik kedua menutupnya, baik saat taskbar Windows tampil maupun disembunyikan | 3 |
| F-LEFT-05 | Ikon Claude | Tampil setelah Shelf saat integrasi Claude nyala; status (titik kuning, denyut, titik hijau) sesuai sesi Claude Code dalam ≤ 1 detik; klik membuka halaman Claude Sessions | 3 |

### 7.3 Pulau tengah

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-ISL-01 | Pulau tanpa aktivitas | Pulau berisi mata Gili saat tidak ada aktivitas dan aktivitas menggantikannya saat muncul; lebar pulau mengikuti isinya dengan minimal 200 px di semua keadaan compact dan wide. (Jam dan tanggal pindah ke F-RIGHT-11) | 1 |
| F-ISL-02 | Keadaan pulau dan gerak | Transisi compact ↔ wide ↔ expanded ↔ kartu ↔ drop zone sesuai 8.3; selama pulau mengambang latar bar transparan, isi bar tetap tampil, dan hanya elemen yang tertimpa panel (+8 px) yang memudar | 1 |
| F-ISL-03 | Prioritas dan antrean | Aturan di 6.3 terpenuhi; dites unit | 1 |
| F-ISL-04 | Musik | Spotify dan YouTube (Chrome/Edge) tampil di slot dan halaman Media; artwork lagu tampil di slot compact (dan di gelembung saat musik jadi aktivitas kedua) sesuai "Artwork musik" di 6.3, dengan cadangan ikon not bila tidak ada; tombol kontrol berfungsi; judul dan artwork berganti ≤ 1 detik setelah lagu ganti | 1 |
| F-ISL-05 | Video | Media Player, Movies & TV, dan video di browser tampil sebagai video (judul + progres saja); VLC sesuai tabel risiko | 1 |
| F-ISL-06 | Status ngecas | Colok/cabut tampil sebagai wide; slot menampilkan petir + persen selama dicas | 1 |
| F-ISL-07 | Halaman expanded | Menu tab ikon selebar pulau (nama di tab aktif dan tab yang di-hover), perpindahan halaman, dan aturan buka/tutup di 6.3; setiap halaman bisa dibuka dengan satu klik | 1 (kerangka + Media), 2 (Timer, Alarms, Calendar, Todo, Notes), 3 (Shelf, Claude, System), 4 (Clipboard) |
| F-ISL-08 | Kartu notifikasi | Kartu alarm, timer, todo, dan pomodoro muncul bersama toast Windows tanpa suara | 2 |
| F-ISL-10 | Caps/Num Lock | Menekan tombol menampilkan status baru | 3 |
| F-ISL-11 | Peringatan baterai | Muncul sekali per siklus di ambang rendah (default 20%) dan 10%, saat pengisian ditahan oleh batas pengisian laptop (`Charging on hold`), saat melewati pengingat cabut (default 80%) di laptop tanpa batas, dan di 100%; ambang rendah dan pengingat cabut bisa diubah | 3 |
| F-ISL-12 | Shelf | Perilaku di 6.3 "Shelf": drop zone, satu drop = satu item (bundel untuk banyak file), maksimal 8 item di grid 4 × 2 tanpa scroll, pesan saat penuh, referensi, seret keluar dengan Copy/Move, pembersihan file yang hilang, tersimpan setelah restart | 3 |
| F-ISL-14 | Titik kamera/mic | Titik muncul di kiri pulau ≤ 2 detik setelah kamera/mic mulai dipakai dan hilang ≤ 2 detik setelah berhenti; tetap tampil saat pulau mengambang; terlihat di atas latar gelap maupun terang; tidak pernah tumpang-tindih dengan isi bagian kiri bar | 3 |
| F-ISL-15 | Progres operasi file Explorer | Copy dan move file > 1 GB, serta delete > 1.000 item (ke Recycle Bin maupun permanen), tampil di slot pulau sesuai tabel di 6.3; pulau tetap compact dan tidak pernah terbuka atau mengambang karena operasi file; hasil akhir tampil 2 detik; batal menampilkan "Cancelled"; konflik file membuka kartu di pulau (Replace/Skip, Try again/Skip, atau Open dialog) dan pilihannya diteruskan ke dialog Explorer; klik slot membawa dialog Explorer ke depan | 4 |
| F-ISL-16 | Todo dibuat lewat Calendar | Todo hanya bisa dibuat dari Add task di halaman Calendar, dengan tanggal yang dipilih dan jam opsional; tugas berjam memunculkan kartu pengingat tepat pada jamnya; halaman Todo tidak punya tombol Add in Calendar; tugas hari ini muncul di daftar Todo, tugas tanggal lain memunculkan `Added to …` dan titik di Calendar; semua bisa dilakukan dengan keyboard | 2 |
| F-ISL-19 | Maskot Gili | Mata di pulau saat tanpa aktivitas, lingkaran di kiri pulau saat ada aktivitas, dan lingkaran 36 px di kiri menu tab saat pulau terbuka (bukan pulau kontrol), sesuai "Maskot Gili" di 6.3; mata mengikuti kursor di seluruh layar dengan pegas; kedipan lembut tiap 3–6 detik; perpindahan bentuk mulus dan bisa disela; mati saat Animation effects mati sesuai aturannya; bisa dimatikan; anggaran CPU 8.1 terpenuhi | 1 |
| F-ISL-20 | Tinggi pulau mengikuti isi | Tinggi pulau terbuka sesuai "Tinggi pulau terbuka" di 6.3: minimal 256 px untuk semua halaman termasuk Notes, maksimal tinggi layar dikurangi jarak tepi pulau di atas dan bawah (760 px di layar 768 px); tanpa scroll sebelum batas itu, termasuk di Notes; Todo dengan 10 tugas, Calendar dengan 6 item, dan Claude dengan 6 sesi tampil penuh tanpa scroll di layar 1080 px; perubahan tinggi beranimasi pegas | 2 |
| F-ISL-21 | Ambient glow bergerak | Warna per halaman sesuai 6.3; cahaya melayang pelan ke titik acak (10–16 detik per perjalanan, tidak pernah mengikuti kursor) saat pulau terbuka dan berhenti total saat pulau menutup; diam saat Animation effects mati; bisa dimatikan terpisah (glow dan motion); anggaran CPU 8.1 terpenuhi | 1 (Media), 2 (halaman lain) |
| F-ISL-22 | Edit dan hapus todo di pulau | Tombol Edit dan Delete per baris di halaman Todo; edit judul dan jam di tempat; hapus dengan konfirmasi klik kedua; pulau memendek setelah hapus; bisa dengan keyboard | 2 |
| F-ISL-24 | Ekspresi Gili | Ekspresi sesuai "Ekspresi Gili" di 6.3 dengan urutan prioritasnya: kaget di samping kartu alarm/timer/pomodoro/pengingat (lingkaran tetap tampil di kiri atas kartu), senang saat note masuk kembali, mata besar 130% selama tombol kiri mouse ditahan dan kursor bergerak ≥ 6 px, tidur setelah 5 menit tanpa gerakan kursor maupun ketikan (bangun dengan kedip dua kali saat ada input), kedip sekali setiap klik kiri atau kanan di mana pun di layar, fokus dengan lirikan ke angka tiap menit berganti, menikmati musik dengan mengangguk seirama; mata mengikuti note yang bergerak; tanpa animasi saat tidur; anggaran CPU 8.1 terpenuhi | 1 (netral, tidur, musik), 2 (fokus, kaget, senang, mengikuti note) |
| F-ISL-25 | Menu tab ikon dan lebar pulau terbuka | Tab menampilkan ikon saja; nama tampil di tab aktif dan di tab yang di-hover atau difokus keyboard, melebar dari ikon dengan gerak di 8.3 dan menyusut lagi saat kursor keluar; tab yang di-hover tidak berganti karena tab bergeser di bawah kursor diam; sorotan tetap pas dengan tab aktif di setiap frame selama nama membuka/menutup; pulau terbuka 560 px untuk semua halaman termasuk Notes sesuai "Lebar pulau terbuka" di 6.3, dan hover tidak pernah mengubah lebarnya; isi semua halaman muat di 560 px tanpa meluber; nama tab tidak terpotong dan tidak menempel ke tepi sorotan di DPI 100–150%; layar yang terlalu sempit tidak membuka nama tab yang di-hover; pembaca layar membaca nama setiap tab | 1 |
| F-ISL-23 | Tab pulau bisa disembunyikan | Tab yang dimatikan, langsung atau lewat ikonnya di Settings → Left side, hilang dari menu dan urutan geser; ikon yang tersisa tersebar ulang dan pulau tetap 560 px; halaman tersembunyi tetap terbuka dari slot, ikon bar, hotkey, atau kartu dengan tab sementara | 3 |
| F-ISL-18 | Bluetooth tersambung/terputus | Menyambungkan earbuds atau perangkat Bluetooth lain menampilkan wide dengan ikon jenis, nama, `Connected`, dan baterai bila tersedia ≤ 2 detik setelah Windows menyatakan tersambung; memutus menampilkan `Disconnected`; perangkat yang sudah tersambung saat start tidak memicu apa pun | 3 |
| F-ISL-26 | Lirik lagu | Sesuai "Lirik lagu" di 6.3: mati secara default dan hanya dinyalakan dari tombol Lyrics atau Settings; lirik bersinkron dari LRCLIB untuk lagu dari Spotify dan YouTube Music di browser; baris saat ini menggantikan judul di slot compact dan tampil dengan baris sebelum/sesudahnya di halaman Media; baris berganti ≤ 250 ms dari stempel waktunya, juga setelah seek dan jeda; lagu tanpa lirik, offline, dan video ditangani sesuai 6.3 tanpa error; hanya judul, artis, album, dan durasi yang dikirim; tidak ada yang ditulis ke disk | 4 |
| F-ISL-27 | Notifikasi Windows di pulau | Sesuai "Notifikasi Windows" di 6.3: mati secara default dan meminta izin Windows saat dinyalakan; notifikasi baru dari WhatsApp, Outlook, Teams, dan Telegram tampil ≤ 1 detik setelah masuk Notification Center di pulau compact yang memanjang (tetap setinggi pulau, tidak pernah terbuka ke bawah), dengan ikon aplikasi, judul (nama kontak), dan isi satu baris yang dipotong "…"; pulau maksimal 560 px dan elemen bar yang tertimpa memudar lalu kembali; 5 detik lalu kembali compact, berhenti selama di-hover; pesan dari chat yang sama digabung dengan angka; antrean dan aturan 10 detik saat pulau terbuka; klik membawa aplikasinya ke depan; tidak tampil untuk Gili, Claude Desktop (saat integrasi Claude nyala), aplikasi yang dimatikan, Do Not Disturb, fase fokus, dan fullscreen; isi tidak pernah ditulis ke disk; bekerja di versi Store dan GitHub | 4 |
| F-ISL-28 | Halaman Clipboard dan tempel ke kursor | Halaman Clipboard sesuai "Halaman Clipboard" di 6.3: daftar 50 teks terakhir, cari, hapus per baris, Clear all dengan konfirmasi; klik baris atau Enter menempelkan teks itu di posisi kursor jendela yang terakhir aktif sebelum pulau dibuka (Notepad, VS Code, kolom teks Chrome/Edge, Word, Windows Terminal, wmux) ≤ 300 ms setelah klik dan pulau menutup; teks itu juga menjadi isi clipboard dan naik ke atas daftar tanpa memunculkan cuplikan; jendela admin hanya menerima clipboard dan wide `Copied · press Ctrl+V to paste`; `Ctrl+Alt+V` membuka halaman dengan kolom cari fokus dan bisa dipakai tanpa mouse | 4 |
| F-ISL-17 | Cuplikan clipboard di pulau | Teks, gambar, file (copy dan cut), dan isi sensitif tampil 3 detik di slot pulau sesuai tabel 6.3; gambar tampil sebagai thumbnail asli dengan nama file dan dimensinya; pulau tidak terbuka; isi sensitif disembunyikan | 1 |

F-ISL-09 dan F-ISL-13 (OSD brightness dan menyembunyikan OSD brightness Windows) dihapus karena Windows sudah menampilkan brightness sendiri; nomornya tidak dipakai ulang.

### 7.4 Bagian kanan

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-RIGHT-01 | CPU, RAM, jaringan | Diperbarui tiap 1 detik; selisih dengan Task Manager ≤ 5 poin persen | 1 |
| F-RIGHT-02 | Wi‑Fi | Nama jaringan tampil; tanpa izin Location tampil ikon saja + tooltip cara memberi izin | 3 |
| F-RIGHT-03 | Modul GPU dan disk | Default mati; saat nyala, selisih dengan Task Manager ≤ 5 poin | 3 |
| F-RIGHT-04 | Atur modul dan overflow | Modul bisa dinyalakan/dimatikan dan diurutkan; overflow mengikuti urutan di 6.4 | 3 |
| F-RIGHT-05 | Cuaca | Suhu dan ikon dari Open-Meteo untuk kota di Settings; diperbarui tiap 30 menit; offline menampilkan nilai terakhir berwarna abu-abu | 4 |
| F-RIGHT-07 | Power | Ikon power selalu paling kanan dan membuka pulau Power; Sleep, Shut down, dan Restart bekerja seperti di Start menu (aplikasi yang belum disimpan tetap ditanyakan oleh Windows); tombolnya bisa dipakai dengan keyboard | 3 |
| F-RIGHT-09 | Panel tray | Semua ikon notification area aplikasi latar belakang tampil di panel tray, termasuk yang tersembunyi di overflow Windows; klik kiri, klik kanan, dan klik dua kali bekerja seperti di taskbar Windows; ikon baru/berubah/hilang tampil ≤ 1 detik; tetap bekerja saat taskbar Windows disembunyikan | 3 |
| F-RIGHT-10 | Pulau kontrol | Modul Wi‑Fi dan ikon Bluetooth, Volume, Brightness, serta Power masing-masing membuka pulau berisi kontrol itu saja, tanpa menu tab, dengan ukuran mengikuti isinya; klik ikon yang sama menutup, klik ikon kontrol lain mengganti isi; ikon Volume dan Bluetooth mencerminkan keadaan sistem ≤ 1 detik; saklar Wi‑Fi, Bluetooth, dan Airplane mode mengubah radio sistem; daftar Wi‑Fi bisa menyambung ke jaringan yang tersimpan; daftar Bluetooth bisa menyambung/memutus perangkat ter-pair; slider volume dan brightness mengubah nilai sistem langsung; pilihan perangkat output; tombol Windows Quick Settings dan All settings | 3 |
| F-RIGHT-11 | Jam dan tanggal di kanan | Gaya dua baris (tanggal di atas, jam dengan detik di bawah) di kiri tombol Settings; detik berganti tepat di pergantian detik sistem (selisih ≤ 50 ms) tanpa menggeser elemen bar lain; Show seconds mati menampilkan jam tanpa detik; format 12/24 jam dari Settings; klik membuka/menutup halaman Calendar; bisa disembunyikan | 1 |
| F-RIGHT-08 | Update and shut down / Update and restart | Kedua item dan titik oranye hanya muncul saat Windows punya update yang menunggu, cocok dengan yang ditampilkan Start menu; memilihnya memasang update lalu mematikan/restart | 3 |

F-RIGHT-06 (suhu CPU lewat helper sensor admin) dihapus di revisi 6: helper itu butuh hak admin dan driver kernel, sehingga tidak bisa lolos Microsoft Store dan rawan dianggap malware. Nomornya tidak dipakai ulang.

### 7.5 Tools dan produktivitas

Jendela Tools: jendela gelap biasa (bisa dipindah dan diubah ukurannya) dengan tab di kiri: Timer, Stopwatch, Pomodoro, Alarms, Todo. Note dan riwayat clipboard tidak ada di Tools; keduanya ada di halaman Notes dan Clipboard di pulau (tab Clipboard di Tools dihapus di revisi 8).

Tata letak Tools mengikuti jendela Settings di mockup: bilah judul `Gili Tools`, tab di kiri, 720 × 520 (minimal 560 × 420). Jendela dibuka dari "Manage alarms" di halaman Alarms dan dari Open Tools di menu klik kanan bar. Tab Alarms berisi tombol `New alarm` dan baris alarm (jam, label, hari · suara, `Edit`, `Delete`, saklar). `Delete` meminta klik kedua: teksnya menjadi `Click again to delete` dan kembali normal setelah 3 detik. Editor muncul di atas daftar dan berisi Time (jam yang tidak valid memberi garis merah dan mematikan Save), Label (maks. 40 karakter), Repeat (`Once` / `Weekdays` / `Every day` atau tombol hari `Mo`–`Su`), Sound (lima suara dan tombol putar), lalu `Cancel` dan `Save`. Keempat tab lain memakai tata letak yang sama: judul tab 18 px semibold dengan tombol di kanan, editor di atas daftar (kolom nama 76 px, jarak baris 10 px), baris berlatar `#0AFFFFFF` radius 8 px, padding 12 × 6, jarak antarbaris 4 px. Angka besar (Stopwatch dan Pomodoro) 44 px bold dengan digit tabular, angka baris 17 px semibold.

**Tab Timer** (F-TOOL-01). Tombol `New timer` di judul; daftar kosong menampilkan `No timers yet`. Setiap baris berisi sisa waktu, label dengan detail di bawahnya (`Ready · 10:00`, `Running · 10:00`, atau `Paused · 10:00`, durasi penuhnya), lalu `Start` / `Pause` / `Resume`, `Reset` (mati saat sudah siap), `Edit`, dan `Delete` (dua klik, `Click again to delete`, 3 detik). Editor (`New timer` / `Edit timer`) berisi Label (maks. 40 karakter, placeholder `Timer`) dan Duration (placeholder `5:00`), lalu `Cancel` dan `Save`; durasi yang tidak valid memberi garis merah dan mematikan Save. Durasi ditulis `m`, `m:ss`, atau `h:mm:ss` (angka tunggal = menit), 1 detik sampai 23:59:59. Maksimal 10 timer berlabel; `New timer` mati di batas itu. Contoh di mockup: `Tea` 03:12 berjalan, `Laundry` 45:00 dijeda, `Pasta` 10:00 siap.

**Tab Stopwatch** (F-TOOL-02). Angka 44 px (`00:02.6`; tetap utuh lewat 99 menit, `100:00.0`), tombol `Start` / `Pause` / `Resume`, `Lap` (aktif hanya saat berjalan), dan `Reset`. Daftar lap dengan yang terbaru di atas: `Lap 3`, waktu split, lalu waktu total; kosong menampilkan `No laps yet`. Maksimal 99 lap, `Reset` menghapus semuanya.

**Tab Pomodoro** (F-TOOL-03). Sub `Focus · session 1 of 4` (sama dengan halaman Timer di pulau), angka 44 px, tombol `Start` / `Pause` / `Resume` dan `Reset`. Bagian `Lengths` berisi empat baris dengan stepper `−` nilai `+` (tombol bernama `Decrease Focus` / `Increase Focus`, dan seterusnya): `Focus`, `Short break`, `Long break` (`25 min`, `5 min`, `15 min`) dan `Long break after` (`4 sessions`). Rentang fokus 1–120 menit, istirahat 1–60 menit, sesi 2–8; tombol mati di batasnya.

**Tab Todo** (F-TOOL-06). Segmen `Today` / `All` / `Done` dan catatan `New tasks are added from Calendar in the island.`; tidak ada tombol atau kolom tambah todo di sini. Kosong menampilkan `No tasks for today` / `No tasks yet` / `No finished tasks`. Baris berisi centang, judul, tanggal di kanan (`Today`, `Tomorrow`, `Tue 28 Oct`, atau `No date`, ditambah ` · 15:00` bila ada jam), `Edit`, dan `Delete` (dua klik). Tugas belum selesai yang tanggalnya sudah lewat menampilkan `Overdue` merah `#FF453A` sebagai pengganti tanggal. Editor `Edit task` berisi Title (maks. 60 karakter), Date (placeholder `No date`), dan Time (placeholder `No time`). Baris bisa diseret untuk mengubah urutan (juga Alt + ↑/↓) di Today dan All; di Done tidak. Baris yang diseret terangkat (skala 1,02, bayangan) dan mengikuti pointer, baris lain bergeser 200 ms, lepas mendarat 200 ms; kalau "Animation effects" Windows mati, tanpa geser dan tanpa angkat.

**Keputusan untuk keempat tab**
1. **Pulau tetap satu timer.** Timer berlabel hanya ada di Tools; halaman Timer di pulau tidak berubah.
2. **Slot** menampilkan pomodoro bila berjalan. Kalau tidak, slot menampilkan timer yang paling cepat habis di antara timer pulau dan timer berlabel. Klik slot saat yang tampil timer berlabel membuka Tools di tab Timer dan pulau tetap compact; timer pulau tetap membuka halaman Timer (6.3 "Membuka pulau").
3. **Kartu per timer berlabel**: judul `<label> done`, prioritas 1 seperti kartu timer, dengan suara timer yang berulang (6.6). Beberapa timer yang habis di detik yang sama memunculkan kartu berurutan, yang paling dulu habis tampil lebih dulu. Mengedit durasi atau menghapus timer yang kartunya tampil menutup kartu itu dan menghentikan suaranya.
4. **Label kosong** disimpan sebagai `Timer`. Mengubah label tidak menghentikan timer; mengubah durasi mengembalikan timer ke keadaan siap.
5. **Stopwatch dan pomodoro di Tools sama dengan yang di pulau** (satu stopwatch, satu pomodoro). Lap hanya ada di Tools, karena halaman Timer di pulau tidak punya tombol Lap.
6. **Durasi pomodoro disimpan di `settings.json`.** Perubahan berlaku untuk fase berikutnya: fase yang berjalan atau dijeda tidak berubah, fase yang belum dimulai langsung memakai durasi baru. Menurunkan jumlah sesi di bawah sesi yang sedang berjalan membuat fokus itu berakhir dengan long break.
7. **Filter**: Today sama dengan halaman Todo di pulau (tanpa tanggal atau jatuh tempo hari ini), All berisi semua tugas, Done berisi tugas selesai, semuanya dalam urutan pengguna. Memindahkan tugas di daftar yang tersaring menaruhnya di posisi tugas tujuan dalam urutan penuh, seperti Notes, sehingga tugas yang tersembunyi tetap di tempatnya.
8. **Kolom Date** menerima `yyyy-MM-dd`, `d MMM`, `d MMM yyyy`, `ddd d MMM` (bentuk yang tampil), `today`, dan `tomorrow`, tanpa membedakan huruf besar/kecil; tanpa tahun berarti tahun ini, dan bentuk yang tampil (kolom Date dan baris) memakai tahun hanya bila bukan tahun ini (`Wed 3 Nov 2027`), supaya menyimpan ulang tanpa mengubah tidak memindahkan tanggal. Kosong berarti tanpa tanggal; isi yang tidak valid memberi garis merah dan mematikan Save. Kolom Time memakai aturan jam editor alarm; jam yang sudah lewat hari ini diberi garis kuning, seperti di 6.3.

Anggaran (8.1): tidak ada polling baru. Tab Timer dan Pomodoro mengikuti tick 1 detik yang sudah ada, hanya selama ada yang berjalan; tab Stopwatch punya satu timer 100 ms yang hanya jalan selama tab itu tampil dan stopwatch berjalan. Semua langganan dilepas saat Tools ditutup.

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-TOOL-01 | Timer | Beberapa timer berlabel bisa jalan bersamaan; jeda/lanjut/reset; tetap benar setelah app atau laptop restart | 2 |
| F-TOOL-02 | Stopwatch | Start/pause/reset/lap; tetap benar setelah app restart | 2 |
| F-TOOL-03 | Pomodoro | Default 25 menit fokus / 5 menit istirahat / 15 menit istirahat panjang tiap 4 sesi; bisa diubah | 2 |
| F-TOOL-04 | Mode fokus | Selama fase fokus, pulau prioritas 2 dan 4 ditahan sampai istirahat (alarm dan timer tetap muncul), dan app mencoba menyalakan Do Not Disturb Windows | 2 (pulau), 4 (DND Windows) |
| F-TOOL-05 | Alarm | Jam, label, hari berulang atau sekali, nyala/mati, snooze 5 menit; alarm saat sleep membangunkan laptop bila wake timer diizinkan; alarm terlewat berbunyi saat bangun dengan label "Missed" | 2 |
| F-TOOL-06 | Todo | Judul, tanggal jatuh tempo opsional, jam pengingat opsional, selesai, urutan manual (seret), filter Today / All / Done | 2 |
| F-TOOL-07 | Pengingat todo | Saat jam pengingat tiba, kartu + toast muncul; "Snooze 10m" menggeser pengingat | 2 |
| F-TOOL-08 | Note di pulau | Halaman Notes sesuai "Halaman Notes" di 6.3: lebar sama dengan halaman lain, tinggi mengikuti kartu (dan editor saat terbuka); grid kartu masonry ala Google Keep selebar penuh dalam urutan pengguna (note baru di atas; mengedit tidak mengubah urutan); kartu yang dibuka tumbuh di tempat menjadi editor ke arah ruang kosong sesuai posisinya (kiri → kanan, kanan → kiri, tengah → dua sisi; ke bawah, atau ke atas bila ruang bawah kurang) dan tidak pernah keluar dari area grid; note baru lahir sebagai kartu kosong di bawah New note lalu tumbuh ke bawah dan ke kiri; editor menyusut kembali ke slot kartunya lewat Done, Esc, atau klik di grid; note yang ditutup dalam keadaan kosong dibuang, baik note baru maupun note lama yang isinya dihapus habis; 8 pilihan warna per note yang tersimpan dan tampil di kartu serta area teks; 4 ukuran teks per note (Extra small, Small, Medium, Large; default Medium) yang dipilih saat menambah atau mengedit, tersimpan, dan berlaku di editor, kartu, dan note mengambang sesuai tabel 6.3, juga lewat `Ctrl+=` / `Ctrl+-` / `Ctrl+0`; seret kartu untuk mengatur urutan (dan Alt + panah); tersimpan 0,5 detik setelah berhenti mengetik dan saat pulau menutup; cari; hapus dengan konfirmasi; pulau tidak menutup otomatis selama editor fokus atau kartu sedang diseret | 2 |
| F-TOOL-09 | Ketahanan data | Mematikan paksa laptop saat mengetik note kehilangan paling banyak 1 detik ketikan terakhir; database memakai WAL dengan `synchronous=FULL` (bagian 10) | 2 |
| F-TOOL-10 | Hotkey global dan quick capture | `Ctrl+Alt+Space` buka/tutup pulau; `Ctrl+Alt+V` halaman Clipboard di pulau dengan kolom cari fokus; `Ctrl+Alt+N` halaman Notes dengan note baru dan editor fokus; `Ctrl+Alt+T` halaman Calendar di hari ini dengan kolom Add task terbuka dan fokus (Enter simpan, Esc batal) | 4 |
| F-TOOL-11 | Hotkey bisa diubah | Semua hotkey bisa diganti di Settings; bentrok ditampilkan "Used by another app" | 4 |
| F-TOOL-12 | Note mengambang | Sesuai "Note mengambang" di 6.3: kartu yang diseret melewati tepi pulau, editor yang diseret dari grip di tepi atasnya (termasuk note baru), atau tombol Float on top di editor, menjadi jendela note always on top (awal 260 × 220 px) yang langsung bisa diketik; ukurannya bisa diubah dari keempat tepi dan keempat sudut tanpa ikon pegangan, 200 × 140 sampai 520 × 640 px, di dalam area kerja; kartunya hilang dari grid saat robek dan pulau menutup setelah note dilepas; kembali ke dalam pulau sebelum dilepas membatalkannya; yang diseret adalah satu jendela di atas pulau dari terangkat sampai mendarat, tidak pernah terpotong tepi pulau, dengan titik pegang tetap di bawah pointer di setiap frame dan ukuran kartu (atau editor) yang tetap sampai dilepas, lalu tumbuh ke ukuran jendela dengan pegas setelah dilepas di luar pulau (8.3); beberapa note bisa mengambang bersamaan (maksimal 6); tersimpan seperti F-TOOL-08; jendela bisa dipindah hanya di dalam area kerja; Back to island menerbangkan note masuk ke pulau (ke slot kartunya bila Notes terbuka) tanpa menghapusnya, di posisi urutan semula (note kosong dibuang); posisi dan ukuran kembali setelah restart; tersembunyi saat fullscreen; pulau yang dibuka tampil di atasnya | 2 |

### 7.6 Suara

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-SND-01 | Suara bawaan | Lima suara (Radar, Chime, Bell, Soft, Ping) ada di dalam aplikasi tanpa file eksternal; semuanya bisa didengar contohnya di Settings | 2 |
| F-SND-02 | Suara alarm dan timer | Alarm memakai suara pilihan per alarm, timer memakai suara di Settings; berulang sampai ditanggapi; berhenti otomatis setelah 5 menit | 2 |
| F-SND-03 | Bunyi sekali | Fase pomodoro selesai berbunyi Bell sekali; pengingat todo berbunyi Ping sekali, kecuali selama fase fokus | 2 |
| F-SND-04 | Volume | Alert volume hanya mengubah volume sesi Gili; Gradually increase menaikkan alarm dari 30% ke 100% dalam 30 detik (±1 detik); tombol kartu menghentikan suara ≤ 150 ms | 2 |
| F-SND-05 | Peringatan mute | Saat Windows di-mute atau volume < 5%, kartu alarm/timer menampilkan peringatan dan tombol Unmute; volume sistem tidak berubah tanpa klik pengguna | 2 |
| F-SND-06 | Ganti perangkat | Mencabut headset saat alarm berbunyi memindahkan suara ke speaker dalam ≤ 1 detik tanpa menghentikan alarm | 2 |

### 7.7 Integrasi Claude

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-CLD-01 | Pasang dan lepas hook | Saklar `Show Claude Code sessions` di Settings (default mati) menambahkan hook Gili ke `~/.claude/settings.json` setelah membuat cadangan `settings.json.gili-backup`; hook lain (misalnya wmux) tetap utuh; memasang dua kali tidak menggandakan entri; mematikan saklar hanya menghapus entri Gili; file yang bukan JSON valid tidak diubah; baris status di Settings sesuai bagian 11 | 3 |
| F-CLD-02 | Status di slot | Working, Needs input, dan Done tampil sesuai "Sesi Claude" di 6.3 ≤ 1 detik setelah kejadiannya; beberapa sesi digabung jadi satu aktivitas; Esc di Claude mengembalikan sesi ke Idle tanpa Done; sesi yang prosesnya mati hilang ≤ 5 detik | 3 |
| F-CLD-03 | Kartu Needs input | Pertanyaan, persetujuan rencana, dan permintaan izin (bila hook-nya terpicu, lihat bagian 12) membuka kartu dengan Ping sekali; kartu mengecil ke slot kuning setelah 8 detik; slot kembali ke Working ≤ 1 detik setelah dijawab; kartu dan Ping ditahan selama fase fokus; tanpa toast | 3 |
| F-CLD-04 | Selesai dan belum dilihat | Done tampil 4 detik; gelembung hijau muncul hanya bila jendela sesi tidak aktif saat selesai, dan hilang saat jendela itu aktif, gelembung diklik, atau prompt baru dikirim di sesi itu | 3 |
| F-CLD-05 | Halaman Claude | Halaman 9 mendaftar semua sesi yang hidup dengan urutan dan isi sesuai 6.3; klik baris membawa jendela sesi ke depan | 3 |
| F-CLD-06 | Membawa jendela sesi ke depan | Sesi Claude Desktop membuka jendela Claude Desktop; sesi CLI di wmux dan Windows Terminal membuka jendela terminalnya; jendela yang diminimize dipulihkan | 3 |
| F-CLD-08 | Timer jendela 5 jam | Jendela dimulai dari prompt pertama dan dihitung mundur di slot sesuai "Batas pemakaian Claude" di 6.3; batas tercapai mengubah slot ke kuning; saat jendela berakhir kartu `Claude usage reset` muncul dengan suara timer dan toast; waktu reset tetap benar setelah Gili atau laptop restart; tooltip menandai `Estimated` bila waktu resmi tidak tersedia | 3 |
| F-CLD-07 | Hook tidak memperlambat Claude | `Gili.ClaudeHook` selesai ≤ 50 ms (p95) saat Gili jalan dan ≤ 100 ms saat Gili mati; tidak pernah menulis ke stdout; selalu keluar dengan kode 0 | 3 |

### 7.8 Rilis publik

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-PUB-01 | Paket MSIX | `scripts/package.ps1` membangun MSIX self-contained yang terpasang dan jalan di laptop pengembang sejak Fase 1; di Fase 5 semua fitur bekerja sama seperti build biasa (AppBar, tray, hotkey, Shelf, hook Claude lewat app execution alias, StartupTask, toast); lolos Windows App Certification Kit | 1 (paket dasar), 5 |
| F-PUB-02 | Listing Microsoft Store | Nama `Gili` dipesan di Partner Center (cadangan bila sudah dipakai ditentukan saat itu); deskripsi, screenshot 1920 × 1080, ikon, kategori Productivity, harga gratis, URL privacy policy, alasan kapabilitas `runFullTrust` di catatan sertifikasi; lolos sertifikasi Store | 5 |
| F-PUB-03 | Installer GitHub bertanda tangan | Installer per-pengguna tanpa UAC sesuai 9.4; installer dan semua exe ditandatangani sertifikat OV dengan timestamp; dirilis di repo publik `gili-releases` bersama release notes dan checksum SHA-256; uninstaller melepas hook Claude, entri Run, dan memulihkan taskbar | 5 |
| F-PUB-04 | Update | Versi Store: update lewat Store dan baris update di Settings → About. Versi GitHub: cek versi tiap 24 jam, Install update hanya menjalankan installer yang tanda tangannya dari penerbit yang sama, lalu Gili jalan lagi di versi baru dengan data utuh; offline tidak menampilkan error | 5 |
| F-PUB-05 | Bersih dari deteksi antivirus | Sebelum setiap rilis, installer dan MSIX diunggah ke VirusTotal; deteksi dari Microsoft Defender harus 0, dan deteksi dari engine lain dilaporkan sebagai false positive ke vendornya. Deteksi Defender dilaporkan lewat Microsoft Security Intelligence (submission perangkat lunak pengembang) sebelum rilis | 5 |
| F-PUB-06 | Uji di mesin bersih | Kedua jalur dipasang di VM Windows 11 baru tanpa .NET dan tanpa alat pengembang: terpasang tanpa UAC, bar muncul, autostart jalan setelah restart, update dan uninstall bekerja, tanpa sisa ruang AppBar atau taskbar tersembunyi setelah uninstall | 5 |
| F-PUB-07 | Pindah dari GitHub ke Store | Data dan pengaturan versi GitHub terbawa ke versi Store saat pertama jalan (bagian 10); dua versi yang terpasang bersamaan tidak jalan bersamaan | 5 |
| F-PUB-08 | Privacy policy dan dukungan | Halaman privacy policy (isi di 8.2) dan halaman dukungan (cara lapor bug, lokasi log) tersedia online dan ditautkan dari Store, `gili-releases`, dan Settings → About | 5 |

## 8. Kebutuhan non-fungsional

### 8.1 Performa
- RAM (private working set) < 120 MB setelah 8 jam.
- CPU rata-rata < 1% saat diam (tanpa animasi), < 5% saat animasi.
- Polling dijeda saat bar tersembunyi (fullscreen), layar mati, atau sleep.
- Bar muncul ≤ 2 detik setelah login.
- Helper hook Claude selesai ≤ 50 ms (p95), jadi Claude tidak terasa lebih lambat walau hook dipanggil di setiap tool.

**Aturan ringan** (pengguna meminta Gili tetap sangat ringan dan minta diingatkan bila fitur berat):
- Tidak ada animasi yang berjalan terus saat diam, kecuali kedipan Gili (sekitar 230 ms tiap 3–6 detik) dan detik di jam (satu teks per detik). Animasi yang tidak terlihat (pulau tertutup, bar tersembunyi, layar mati) dihentikan, bukan sekadar disembunyikan.
- Panel tray dan jendela Settings dibuat saat dibuka dan dilepas dari memori 30 detik setelah ditutup. Jendela Tools dan Settings tidak menyisakan referensi setelah ditutup, tetapi memorinya baru kembali ke Windows setelah GC berjalan: 30 detik setelah salah satunya ditutup, kalau keduanya tidak sedang terbuka, Gili menjalankan satu GC penuh yang memadatkan heap (`GC.Collect` mode Aggressive). Biayanya satu GC penuh per penutupan; membuka salah satu jendela sebelum 30 detik membatalkannya.
- Modul atau fitur yang dimatikan di Settings berhenti mengambil datanya.
- Tidak ada hook mouse atau keyboard global. Status Caps/Num Lock dibaca 8×/detik lewat `GetKeyState` (dua bit, bukan tombol yang ditekan), ikut dijeda bersama polling lain.

**Anggaran per fitur revisi 4 dan 5** (diukur di laptop pengguna saat fase fiturnya; kalau melewati anggaran, cadangannya dipakai):

| Fitur | Biaya yang diperkirakan | Anggaran | Cadangan bila lewat |
|---|---|---|---|
| Mata Gili mengikuti kursor | Baca posisi kursor 30×/detik; gambar ulang dua kotak kecil (area kecil di dalam pulau 200 px) hanya saat kursor bergerak | CPU ≤ 0,3% rata-rata saat mouse aktif dipakai, 0% saat kursor diam | Turun ke 15×/detik |
| Ambient glow bergerak acak | **Paling berat di revisi ini**: menggambar ulang gradasi selebar pulau terus-menerus selama pulau terbuka (sejak revisi 8 tidak lagi ditambah pembacaan kursor). WPF menggambar jendela transparan (layered) di CPU, jadi tiap frame mahal. Titik tujuan acak hanya dipilih sekali per 10–16 detik per gumpalan | CPU ≤ 3% selama pulau terbuka, 0% saat tertutup | Turun ke 15 fps; kalau tetap lewat, Ambient motion default mati (glow diam tetap ada) |
| Panel tray | Library notification area (ManagedShell) + bitmap ikon tray | RAM ≤ +10 MB; CPU 0% saat diam (berbasis event) | — |
| Pulau kontrol | Daftar Wi‑Fi, Bluetooth, dan perangkat audio hanya dibaca saat pulaunya terbuka; ikon bar memakai event sistem | 0 saat tidak tampil | — |
| Detik di jam | Satu teks kecil diperbarui sekali per detik; timer diselaraskan ke pergantian detik sistem, bukan polling cepat; tata letak bar dihitung ulang hanya saat menit atau tanggal berganti | Tidak terukur (< 0,1%) | Show seconds dimatikan |
| Timer jendela Claude 5 jam | Satu teks diperbarui tiap detik saat tampil di slot | Tidak terukur | Saat hanya di gelembung, tidak ada pembaruan teks |
| Tinggi pulau mengikuti isi, posisi bawah, sembunyikan taskbar, tombol Start, jam di kanan | Hanya saat ada perubahan | Tidak terukur | — |
| Ekspresi Gili | `GetLastInputInfo` sekali per detik untuk tidur; status tombol mouse dibaca di pembacaan kursor 30×/detik yang sudah ada (tanpa hook); satu kedipan 230 ms per klik; ekspresi dihitung ulang hanya saat ada perubahan; anggukan musik menggambar ulang area mata (lingkaran 24 px) hanya saat bergerak, sekitar 60% waktu selama musik diputar | CPU ≤ 0,2% selama musik diputar, 0% saat tidur dan saat ekspresi lain diam | Anggukan dimatikan (mata setengah terpejam diam) |
| Kartu note tumbuh jadi editor | Animasi ±420 ms di dalam jendela pulau (layered, digambar CPU) hanya saat membuka/menutup note; isi editor ditata sekali di ukuran akhir, bukan tiap frame | Masuk anggaran animasi (< 5%) selama animasi, 0% setelahnya | Ukuran berpindah dengan cross-fade 100 ms |
| Note mengambang | Satu jendela WPF biasa per note yang sedang mengambang, ditambah satu selama seretan (dibuat saat kartu terangkat; menjadi note mengambang kalau dilepas di luar, dibuang kalau tidak) (transparan, layered, supaya sudutnya 12 px seperti kartu; digambar CPU hanya saat isinya berubah: mengetik, resize, seret, dan animasi), di thread UI yang sama; tidak ada jendela untuk note yang tidak mengambang; maksimal 6 | RAM ≤ +5 MB per jendela (bitmap layered sampai 520 × 640 px plus margin bayangan, di DPI 150%); CPU 0% saat diam | Batas turun ke 3; kalau masih lewat, jendela kembali tidak transparan dengan sudut 8 px dari DWM |
| Artwork musik di pulau compact (revisi 8) | Satu gambar kecil di-decode sekali per lagu dari thumbnail sesi media; tidak ada pembaruan selama lagu yang sama | RAM ≤ +1 MB; CPU tidak terukur | Artwork hanya di halaman Media |
| Notifikasi Windows di pulau (revisi 8) | Event `NotificationChanged` dari `UserNotificationListener` (tanpa polling); satu baris teks di pulau per notifikasi; logo aplikasi di-cache per AppUserModelID. Cadangan bila event tidak terkirim ke aplikasi desktop: `GetNotificationsAsync` tiap 2 detik selama fitur nyala, membandingkan ID saja | RAM ≤ +4 MB; CPU 0% tanpa notifikasi (≤ 0,1% dengan cadangan polling); 0 saat mati | Polling cadangan turun ke tiap 5 detik |
| Lirik lagu (revisi 8) | Satu permintaan HTTPS kecil per lagu (hanya bila Show lyrics nyala); parsing LRC sekali; pengecekan baris 4×/detik hanya selama lirik tampil dan musik diputar, dan teks diperbarui hanya saat baris berganti (sekitar tiap 4 detik) | RAM ≤ +2 MB; CPU tidak terukur; 0 saat mati | Pengecekan baris turun ke 2×/detik |
| Ukuran teks note (revisi 8) | Satu angka per note; teks ditata ulang hanya saat ukurannya diganti | Tidak terukur | — |
| Caps/Num Lock (Fase 3a) | Dua `GetKeyState` per 125 ms di thread pool, dijeda bersama polling lain | Tidak terukur (< 0,05%) | Turun ke 4×/detik |
| Modul GPU (Fase 3c) | Satu query PDH wildcard `GPU Engine` per detik (±2 ms, ±470 instance di laptop pengguna) hanya saat modul GPU nyala atau halaman System terbuka | CPU ≤ 0,2% saat nyala; 0 saat mati | Dibaca tiap 2 detik |
| Modul disk (Fase 3c) | Satu `GetDiskFreeSpaceEx` per 10 detik saat modul nyala atau halaman System terbuka | Tidak terukur | — |
| Shelf (Fase 3d) | Event drag-and-drop OLE; `File.Exists`/`Directory.Exists` untuk ≤ 8 item saat halaman Shelf dibuka, setelah seret keluar, dan tiap 60 detik hanya selama Shelf berisi (dijeda bersama polling lain) | CPU 0% saat diam; RAM ≤ +1 MB | — |
| Halaman System (Fase 3c) | Empat garis 60 titik dan lima teks digambar ulang sekali per detik hanya saat halamannya terlihat (jendela layered, digambar CPU) | CPU ≤ 0,5% selama terlihat; 0 saat tertutup | Grafik diperbarui tiap 2 detik |
| Titik kamera/mic (Fase 3a) | Satu thread menunggu notifikasi registry `ConsentStore`; dua key dibaca hanya saat berubah; nama app dicari sekali per app. Versi MSIX tidak pernah menerima notifikasi itu (registry tervirtualisasi paket), jadi di sana dua key dibaca sekali per detik dan dijeda bersama polling lain | CPU 0% saat diam (versi GitHub), tidak terukur (versi MSIX); RAM ≤ +1 MB | — |
| Kejadian Bluetooth (Fase 3a) | Properti tambahan di watcher perangkat yang sudah ada | Tidak terukur | — |
| Peringatan baterai (Fase 3a) | Event `PowerManager` yang sudah ada | Tidak terukur | — |
| Halaman Clipboard dan tempel ke kursor (revisi 8) | Daftar 50 teks yang sudah ada di memori dan database (F-LEFT-03), digambar hanya saat halamannya dibuka; satu tulisan clipboard dan satu `SendInput` per tempel; jendela yang terakhir aktif sudah dicatat oleh pelacak aplikasi aktif (6.2), jadi tidak ada pembacaan tambahan | RAM ≤ +1 MB; CPU 0% saat halaman tertutup | — |
| Build self-contained (revisi 6) | Runtime .NET ikut di paket, jadi pengguna tidak perlu memasangnya. Tidak menambah RAM atau CPU; ukuran di disk naik | Paket unduhan ≤ 80 MB, terpasang ≤ 200 MB | Trimming dimatikan untuk WPF; kurangi aset yang ikut |
| Cek update (installer GitHub saja, F-PUB-04) | Satu permintaan HTTPS kecil saat start lalu tiap 24 jam; tidak ada proses latar terpisah | Tidak terukur | — |

### 8.2 Keandalan dan privasi
- Gagalnya satu provider tidak mematikan bar (bagian 11).
- Semua data disimpan lokal. Akses internet hanya API cuaca Open-Meteo (tanpa API key; hanya mengirim koordinat kota), LRCLIB untuk lirik bila Show lyrics dinyalakan (hanya judul, artis, album, dan durasi lagu), dan, khusus installer GitHub, cek versi terbaru ke GitHub Releases (tanpa data pengguna). Versi Store diperbarui oleh Store sendiri.
- Tanpa telemetri, analitik, atau pelaporan crash otomatis. Laporan crash versi Store hanya yang dikumpulkan Windows sendiri dan tampil di Partner Center.
- Listing Store dan halaman rilis GitHub menautkan **privacy policy** yang menyebut: data tersimpan lokal, koordinat kota dikirim ke Open-Meteo, judul/artis/album/durasi lagu dikirim ke LRCLIB hanya bila lirik dinyalakan, cek versi ke GitHub, clipboard, sesi Claude, dan isi notifikasi aplikasi lain hanya di memori, tanpa telemetri.
- Isi notifikasi aplikasi lain (revisi 8, opt-in) hanya dibaca saat Show Windows notifications nyala dan izin Windows diberikan, hanya disimpan di memori selama tampil dan selama di antrean, dan tidak pernah ditulis ke disk, log, atau dikirim ke mana pun.
- Isi clipboard sensitif tidak ditampilkan dan tidak disimpan (6.3). Thumbnail gambar clipboard hanya di memori, tidak ditulis ke disk.
- Data sesi Claude (nama sesi, folder, cuplikan pertanyaan) hanya ada di memori, tidak ditulis ke database atau log. Teks prompt pengguna tidak dikirim ke Gili. Gili hanya mengubah `~/.claude/settings.json` saat pengguna menyalakan atau mematikan integrasi.

### 8.3 Visual dan gerak

**Warna**

| Token | Nilai | Pemakaian |
|---|---|---|
| `bar-tint` | `#161618` opacity 100% (bisa diatur 40–100%) di atas blur | Latar bar |
| `gili-eye` | `#FFFFFF` | Mata maskot Gili |
| `island` | `#000000` | Pulau di semua keadaan |
| `surface` | `#1C1C1E` | Baris, tile, kolom input di dalam pulau |
| `control` | `#2C2C2E` / hover `#3A3A3C` | Tombol |
| `text` | `#F5F5F7` | Teks utama |
| `text-2` | `#8E8E93` | Teks sekunder (kontras 6,6:1 di atas hitam) |
| `accent` | `#30D158` | Centang, ikon tab aktif, bar level musik di tab Media, hari ini di Calendar |
| `done-bg` | `#0F2A17` | Baris todo selesai |
| `warn` | `#FFD60A` | Baterai 20%, Claude Needs input |
| `claude` | `#D97757` | Ikon Claude saat Working |
| `danger` | `#FF453A` | Baterai 10%, alarm terlewat |
| `cam` / `mic` | `#30D158` / `#FF9F0A` | Indikator privasi |

**Tipografi**: Segoe UI Variable (font sistem Windows 11). Teks bar 12 px dengan tracking +0,01em, kecuali modul kanan yang memakai gaya dua baris (label 8.5 px kapital, nilai 11.5 px; lihat 6.4); judul di pulau 19 px semibold −0,01em; angka besar 44–46 px tebal −0,02em; digit tabular untuk semua angka yang berubah.

**Bentuk**: kapsul compact radius setengah tinggi pulau (12 px pada default 24 px), lebar minimal 200 px; mata Gili 10 × 10 px radius 3 px (di lingkaran 6 × 6 px radius 2 px); lingkaran Gili setinggi pulau; panel expanded radius 32 px (termasuk Notes); elemen yang menempel di tepi panel memakai radius konsentris (radius dalam = 32 − jarak ke tepi, contoh menu halaman 18 px pada jarak 14 px); kartu 28 px; drop zone 30 px; baris dan tile 12 px; kartu note dan note mengambang 12 px, editor note 16 px; gelembung aktivitas 24 px bulat penuh; titik kamera/mic 8 px.

**Gerak** (pegas dijelaskan dengan damping ratio dan response, mengikuti panduan gerak Apple):

| Transisi | Pegas / waktu |
|---|---|
| Compact → expanded, kartu, drop zone | Pegas, damping 0,82, response 0,42 dtk (sedikit memantul seperti Dynamic Island) |
| Mengambang → compact | Pegas, damping 1,0, response 0,32 dtk (tanpa memantul) |
| Compact ↔ wide | Pegas, damping 0,85, response 0,30 dtk |
| Hover pulau compact (masuk/keluar) | Lebar +16 px, pegas damping 0,8, response 0,30 dtk |
| Pindah aplikasi aktif di bagian kiri | Nama lama menyusut dan nama baru melebar 300 ms ease-out (fade masuk mulai 90 ms); kapsul latar pindah bersamaan; aplikasi baru masuk dengan pegas skala 0,6 → 1 (320 ms) |
| Hover ikon aplikasi di bagian kiri | Nama melebar 300 ms ease-out dan fade masuk 160 ms, mulai saat kursor masuk; menyusut dengan gerak yang sama saat kursor keluar |
| Tinggi bar atau pulau diubah di Settings | Pegas compact ↔ wide |
| Latar bar transparan/kembali saat pulau mengambang/menutup | Warna, blur, dan garis bawah 220 ms, mulai bersamaan dengan pulau |
| Elemen bar yang tertimpa panel | Opacity 160 ms keluar dan masuk |
| Isi pulau muncul | 220 ms: opacity + blur 6 → 0 px + skala 0,98 → 1, mulai 120 ms setelah pulau mulai membesar |
| Pindah halaman | Geser horizontal 300 ms, ease-out eksponensial; nama tab lama menyusut, nama tab baru melebar, dan sorotan bergeser serta melebar ke tab baru, semuanya 300 ms dengan kurva yang sama (cubic-bezier .16, 1, .3, 1) |
| Nama tab muncul saat hover atau fokus keyboard | Nama melebar dari ikon 300 ms ease-out (cubic-bezier .16, 1, .3, 1) dan fade masuk 160 ms, mulai saat kursor masuk; tab lain bergeser dengan kurva yang sama; menyusut dengan gerak yang sama saat kursor keluar dari menu. Nama tab aktif fade masuk 220 ms mulai 90 ms |
| Tinggi pulau saat masuk/keluar Notes atau saat editor note membuka/menutup | Pegas yang sama dengan membuka pulau |
| Lebar pulau berubah karena tab dinyalakan/dimatikan | Pegas compact ↔ wide |
| Kartu note tumbuh jadi editor | Posisi dan ukuran dengan pegas membuka pulau (damping 0,82, response 0,42 dtk), radius 12 → 16 px; isi muncul 180 ms mulai 140 ms; kartu lain meredup 220 ms |
| Editor menyusut kembali ke kartu | Pegas damping 1,0, response 0,32 dtk (tanpa memantul); isi hilang 120 ms; note kosong atau dihapus memudar 160 ms mulai 160 ms; editor berganti kartu di frame yang sama |
| Kartu (atau editor) dilepas jadi note mengambang | Satu jendela dari terangkat sampai mendarat, di atas pulau; mengikuti pointer 1:1 dari titik pegang dengan ukuran kartu/editor yang tetap selama diseret, termasuk di luar pulau; melewati tepi: bayangan makin dalam 220 ms, kartu lain merapat menutup celah 320 ms ease-out; batal (kembali ke pulau): kartu lain bergeser memberi tempat dengan gerak yang sama; dilepas di luar: ukuran berpindah dari kartu/editor ke 260 × 220 px dengan pegas membuka pulau (damping 0,82, response 0,42 dtk) yang dihitung per frame, posisi dihitung dari ukuran supaya titik lepas tetap di tempat; radius tetap 12 px dari kartu, atau 16 → 12 px dari editor mengikuti progres ukuran; skala 1,03 → 1 dalam 220 ms ease-out; isi cross-fade 160 ms; dilepas di dalam pulau: terbang ke slot kartu atau bingkai editor 340 ms (cubic-bezier .34, 1.3, .64, 1); dilepas sebagian di luar area kerja: posisi masuk dengan pegas damping 0,82, response 0,34 dtk; dilepas di luar: pulau menutup dengan pegas mengambang → compact |
| Note mengambang muncul lewat Float on top | Skala 0,9 → 1 + opacity dengan pegas membuka pulau |
| Note kembali ke pulau (Back to island) | Isi jendela memudar 100 ms; posisi, ukuran, dan radius menuju kapsul setinggi pulau di tengah pulau dalam 440 ms dengan kurva yang makin cepat (ease-in, cubic-bezier .55, 0, .75, .2), warna note berubah ke hitam pulau dan memudar di 25% terakhir; pulau menggembung sekali (skala 1,07 × 1,18 lalu kembali) dengan pegas membuka pulau. Ke halaman Notes yang terbuka: menuju slot kartu dengan kurva yang sama, tanpa memudar |
| Kartu note yang diseret | Mengikuti pointer 1:1 dari titik pegang, terangkat dengan bayangan dan skala 3%; kartu lain bergeser 320 ms; saat dilepas mendarat dengan pegas 340 ms |
| Isi slot berganti (cuplikan clipboard, progres file, aktivitas lain), atau jam/tanggal hilang dan muncul | Lebar pulau dan rentang isi aktivitas mengikuti dengan pegas compact ↔ wide; isi slot fade 180 ms |
| Titik kamera/mic dan gelembung aktivitas muncul/hilang | Skala 0 → 1 dengan pegas 320 ms; posisinya ikut bergeser dengan pegas yang sama seperti pulau saat pulau berubah ukuran |
| Ikon Claude saat Working | Opacity 0,55 ↔ 1, 1,6 detik ease-in-out berulang; diam di opacity 1 bila Animation effects mati |
| Mata Gili mengikuti kursor | Pegas damping 0,8, response 0,25 dtk menuju target; kembali ke tengah 600 ms ease-in-out setelah kursor diam 8 detik |
| Kedipan Gili | Tinggi mata 100% → 10% dalam 90 ms ease-in, kembali 140 ms ease-out; kedip ganda berjeda 120 ms |
| Ekspresi Gili berganti | Bentuk mata dengan pegas membuka pulau; radius 200 ms ease; tidur: memejam 900 ms, `z` muncul 400 ms setelah jeda 600 ms; bangun: kedip dua kali |
| Anggukan Gili saat musik | Satu anggukan per 0,48 detik: turun 22% ukuran mata dalam 0,12 detik (ease-in), naik lagi dalam 0,17 detik (ease-out), lalu diam 0,19 detik |
| Lirikan Gili ke angka timer / ke note yang bergerak | Pegas mata mengikuti kursor (damping 0,8, response 0,25 dtk) menuju angka di slot atau pusat jendela note |
| Gili pindah ke lingkaran / kembali ke pulau | Pegas damping 0,8, response 0,45 dtk; lingkaran mulai dari skala 0,6 menempel di tepi pulau; mata mengecil/membesar bersamaan |
| Ambient glow bergerak | Tiap gumpalan ke titik acak (±25% lebar, ±30% tinggi) dalam 10–16 detik, ease-in-out (cubic-bezier .37, 0, .63, 1), lalu titik berikutnya; napas skala 9 detik; warna halaman bersilang 600 ms; tidak bereaksi pada kursor |
| Tinggi pulau terbuka berubah | Pegas membuka pulau saat pindah halaman; pegas compact ↔ wide saat isi berubah |
| Panel tray dan daftar `+N` | Turun 4 px + fade 160 ms ease-out (naik 4 px saat bar di bawah) |
| Pindah antar-pulau kontrol | Ukuran dengan pegas buka; isi lama memudar 90 ms, isi baru muncul 180 ms |
| Elemen bar disembunyikan/dimunculkan dari Settings | Skala 0,6 → 1 dan opacity, pegas 320 ms; deretan merapat 300 ms ease-out |
| Bar pindah posisi atas ↔ bawah | Keluar dari tepi lama dan masuk dari tepi baru, masing-masing pegas compact ↔ wide |

Semua transisi pulau bisa disela: keadaan baru mulai dari ukuran yang sedang tampil di layar, bukan dari target sebelumnya. Kalau "Animation effects" Windows mati, semua transisi menjadi cross-fade 100 ms.

### 8.4 Aksesibilitas
- Semua kontrol di pulau dan Tools bisa dioperasikan dengan keyboard; Esc selalu menutup pulau.
- Kontras teks minimal 4,5:1.
- Teks dan ikon ikut skala DPI 100–200%.
- Mengatur urutan note punya jalur keyboard (Alt + panah pada kartu yang fokus); Enter membuka kartu di editor. Ukuran teks note dipilih dengan panah di pemilihnya atau `Ctrl+=` / `Ctrl+-` / `Ctrl+0` sambil mengetik.
- Melepas note punya jalur keyboard: tombol Float on top di editor. Note mengambang bisa dicapai dengan Alt+Tab, tombol Back to island bisa difokuskan dengan Tab, dan Esc melepas fokus dari teksnya.

## 9. Arsitektur teknis

### 9.1 Proyek

| Proyek | Isi | Bergantung pada |
|---|---|---|
| `Gili.App` | WPF + CommunityToolkit.Mvvm: `BarWindow` (AppBar), `IslandWindow` (jendela transparan topmost untuk keadaan mengambang), `NoteWindow` (satu per note mengambang, topmost, transparan dengan sudut 12 px dan bayangan yang digambar sendiri), `ToolsWindow`, `SettingsWindow`, ViewModel | Core, Platform, Data |
| `Gili.Core` | `IslandCoordinator`, `Scheduler`, `ShelfService`, model, format angka/tanggal. Tanpa WPF dan Win32 | — |
| `Gili.Platform` | Provider Windows (9.2), `AppBarService`, `BarMaterial`, `HotkeyService`, `NotificationService`, helper drag-and-drop | Core |
| `Gili.Data` | Repository SQLite (Microsoft.Data.Sqlite), `SettingsStore` | Core |
| `Gili.ClaudeHook` | Console app kecil (NativeAOT, mulai ≤ 20 ms, tanpa hak admin) yang dipanggil hook Claude Code: membaca JSON hook dari stdin, mengambil field yang dibutuhkan, mengirimnya ke named pipe `Gili.Claude` dengan timeout 100 ms, lalu keluar. Tidak pernah menulis ke stdout | — |
| `Gili.Core.Tests` | Unit test xUnit | Core, Data |
| `packaging/` | Bukan proyek .NET: manifest MSIX (`AppxManifest.xml`, aset logo), skrip installer GitHub, dan skrip `scripts/package.ps1` yang membangun keduanya dari `dotnet publish` self-contained (9.4) | App, ClaudeHook |

`IslandWindow` terpisah dari `BarWindow` supaya pulau mengambang tidak mengubah ruang AppBar dan jendela lain tidak bergeser saat pulau terbuka.

Tidak ada bagian Gili yang jalan dengan hak admin, dan instalasinya juga tidak meminta UAC. Ini penting untuk Shelf (Windows memblokir drag-and-drop dari proses biasa ke proses admin), untuk lolos sertifikasi Microsoft Store, dan supaya Gili tidak tampak mencurigakan bagi antivirus.

### 9.2 Provider

Setiap provider punya interface di Core dan implementasi di Platform; mode demo memakai implementasi palsu.

| Provider | Sumber di Windows | Cara |
|---|---|---|
| Lirik | LRCLIB (`lrclib.net/api/get` lalu `/api/search`) lewat `HttpClient`, satu permintaan per lagu, cache 20 lagu di memori; posisi dari timeline sesi media + jam lokal | Saat lagu berganti (hanya bila Show lyrics nyala); baris dicek 4×/detik selama lirik tampil dan musik diputar |
| Media | `GlobalSystemMediaTransportControlsSessionManager` (WinRT); musik vs video dari `PlaybackType` sesi, cadangan daftar aplikasi video yang dikenal; progres dari `GetTimelineProperties`; artwork dari `MediaProperties.Thumbnail`, dibaca sekali per lagu dan diperkecil di memori ke ukuran slot (2× untuk DPI tinggi) dan ke 112 px untuk halaman Media, lalu warna dominannya dihitung sekali | Event |
| Cadangan VLC | Judul jendela `vlc.exe` untuk judul video bila VLC tidak mengirim sesi media | Tiap 2 detik selama VLC jalan |
| Clipboard | `AddClipboardFormatListener`; gambar diubah jadi thumbnail kecil di memori. Tempel dari halaman Clipboard: `SetClipboardData` (`CF_UNICODETEXT`), lalu `SetForegroundWindow` ke jendela yang terakhir aktif sebelum pulau dibuka (dicatat dari `EVENT_SYSTEM_FOREGROUND` yang sudah dipakai untuk aplikasi aktif, jendela Gili diabaikan), tunggu sampai jendela itu benar-benar di depan (maks. 150 ms), lalu `SendInput` Ctrl+V. Perubahan clipboard yang ditulis Gili sendiri dikenali dari `GetClipboardOwner` dan tidak memunculkan cuplikan | Event |
| Operasi file Explorer | UI Automation pada dialog progres Explorer (`OperationStatusWindow`). Jenis operasi (copy, move, delete, delete permanen), nama item, dan persen dibaca dari teks dan progress bar dialog; dialog konflik dikenali dari tombol Replace/Skip/Try again, dan pilihan dari kartu pulau ditekan lewat `InvokePattern` pada tombol itu | Event + baca tiap 500 ms saat aktif |
| Aplikasi berjalan dan aktif | Daftar aplikasi dari jendela yang tampil di taskbar: `EnumWindows` + `SetWinEventHook(EVENT_OBJECT_SHOW/HIDE/DESTROY)`, disaring seperti taskbar (terlihat, tanpa owner atau `WS_EX_APPWINDOW`, bukan `WS_EX_TOOLWINDOW`, tidak `DWMWA_CLOAKED`), dikelompokkan per AppUserModelID/proses. Aplikasi aktif: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` → `GetWindowThreadProcessId` → `QueryFullProcessImageName`. Ikon dari .exe lewat `SHGetFileInfo` / `IShellItemImageFactory` (ukuran sesuai DPI); aplikasi Store dikenali dari `AppUserModelID` dan memakai logo paketnya (`Package.Logo`). Nama dari `FileDescription` versi file atau nama tampilan paket. Ikon di-cache per path atau AppUserModelID | Event |
| Daya | `PowerManager` / `SystemInformation.PowerStatus`, `WM_POWERBROADCAST` (hanya untuk kejadian charger dan peringatan baterai) | Event |
| Caps/Num Lock | Low-level keyboard hook + `GetKeyState` | Event |
| CPU, GPU, disk | CPU: performance counter `Processor Information`. GPU: PDH wildcard `\GPU Engine(*)\Utilization Percentage`, dijumlah per engine lalu diambil maksimum (rumus kolom GPU Task Manager). Disk: `GetDiskFreeSpaceEx` drive Windows (Fase 3c, menggantikan `LogicalDisk`). Semuanya hanya saat modulnya nyala atau halaman System terbuka | CPU dan GPU tiap 1 detik, disk tiap 10 detik |
| RAM | `GlobalMemoryStatusEx` | Tiap 1 detik |
| Jaringan | `NetworkInterface.GetIPv4Statistics` (selisih byte) | Tiap 1 detik |
| Wi‑Fi | Native WLAN API (`WlanQueryInterface`) | Tiap 10 detik + event koneksi |
| Kamera/mic | Registry `...\CapabilityAccessManager\ConsentStore\{webcam,microphone}` (`LastUsedTimeStop = 0` berarti sedang dipakai) | Tiap 2 detik |
| Cuaca | Open-Meteo forecast + geocoding API (HTTP) | Tiap 30 menit, di-cache |
| Shelf | OLE drag-and-drop di `BarWindow` dan `IslandWindow` (`DataFormats.FileDrop` masuk, `DoDragDrop` dengan Copy dan Move keluar); cek keberadaan file | Event + tiap 60 detik |
| Hotkey | `RegisterHotKey` | Event |
| Bluetooth | `DeviceWatcher` (Windows.Devices.Enumeration) untuk perangkat Bluetooth Classic dan LE dengan properti `System.Devices.Aep.IsConnected`; jenis dari `ClassOfDevice` / kategori perangkat; baterai dari properti baterai Bluetooth perangkat atau GATT Battery Service bila tersedia (best-effort) | Event |
| Power | Sleep: `SetSuspendState(FALSE, FALSE, FALSE)` (PowrProf). Shut down/Restart: `InitiateShutdownW` dengan `SHUTDOWN_POWEROFF` / `SHUTDOWN_RESTART` (tanpa memaksa aplikasi lain), ditambah `SHUTDOWN_INSTALL_UPDATES` untuk varian update. Update yang menunggu dideteksi dari Windows Update Agent (`ISystemInformation.RebootRequired`) dan registry `...\WindowsUpdate\Auto Update\RebootRequired` | Saat menu dibuka + tiap 30 menit |
| Suara | NAudio: `WasapiOut` (shared mode) di perangkat default, `VolumeSampleProvider` untuk Alert volume dan kurva naik bertahap; `IAudioEndpointVolume` untuk cek mute/volume sistem; `IMMNotificationClient` untuk pergantian perangkat default | Saat dibutuhkan + event perangkat |
| Notifikasi aplikasi lain | `UserNotificationListener` (Windows.UI.Notifications.Management, kapabilitas `userNotificationListener`): `RequestAccessAsync` saat saklar dinyalakan, event `NotificationChanged` (`Added`) lalu `GetNotification(id)`; teks dari `Notification.Visual.GetBinding(KnownNotificationBindings.ToastGeneric).GetTextElements()`, aplikasi dari `AppInfo` (nama, logo, AppUserModelID). Butuh identitas paket: versi MSIX punya; installer GitHub mendaftarkan sparse package bertanda tangan (paket dengan lokasi eksternal) supaya `Gili.exe` punya identitas. Do Not Disturb dari `SHQueryUserNotificationState` | Event (cadangan polling 2 detik) |
| Notifikasi | Toast via Microsoft.Toolkit.Uwp.Notifications (MSIX: identitas paket; installer GitHub dan `dotnet run`: shortcut Start Menu dengan AUMID; lihat 9.4) | — |
| Material bar | `SetWindowCompositionAttribute` (accent policy): acrylic untuk "Strong", blur-behind untuk "Light", tanpa accent (tint polos) untuk "Off"; tint digambar WPF di atasnya. Bukan `DWMWA_SYSTEMBACKDROP_TYPE`, karena backdrop DWM berubah polos saat jendela tidak aktif, padahal bar tidak pernah aktif (diverifikasi 26 Okt 2026). Membaca `UISettings.AdvancedEffectsEnabled` | Saat berubah |
| Kursor (Gili) | `GetCursorPos` dari `DispatcherTimer` 30 Hz, aktif hanya selama Gili tampil dan bar terlihat | 30×/detik saat aktif |
| Start | Membuka Start menu seperti tombol Windows: `SendInput` VK_LWIN, atau pesan Start ke `Shell_TrayWnd` bila taskbar disembunyikan (diverifikasi Fase 3) | Saat diklik |
| Taskbar Windows | `SHAppBarMessage(ABM_GETSTATE/ABM_SETSTATE)` untuk auto-hide, `ShowWindow(SW_HIDE/SW_SHOW)` pada `Shell_TrayWnd` dan `Shell_SecondaryTrayWnd`; status asli disimpan di `settings.json` | Saat saklar berubah + `TaskbarCreated` |
| Notification area (tray) | ManagedShell (MIT, dipakai RetroBar) `NotificationArea`: menjadi host tray tambahan sehingga aplikasi mengirim ikonnya (`Shell_NotifyIcon`) juga ke Gili, sementara Explorer tetap menerima salinannya. Klik diteruskan ke aplikasi lewat pesan callback ikon (`uCallbackMessage`) | Event |
| Pulau kontrol | Radio Wi‑Fi/Bluetooth/airplane: `Windows.Devices.Radios`; daftar dan sambung Wi‑Fi: Native WLAN API (`WlanScan`, `WlanGetAvailableNetworkList`, `WlanConnect` dengan profil tersimpan); Bluetooth ter-pair: `DeviceInformation` + `BluetoothDevice`; volume dan perangkat output: Core Audio (`IAudioEndpointVolume`, `IPolicyConfig` untuk ganti perangkat default); sambung/putus perangkat audio Bluetooth: `KSPROPERTY_ONESHOT_RECONNECT`/`DISCONNECT` di filter KS endpoint audio-nya (dicocokkan lewat `ContainerId`); brightness: WMI `WmiMonitorBrightness` / `WmiMonitorBrightnessMethods` | Saat pulaunya terbuka; ikon bar dari event (`IAudioEndpointVolumeCallback`, `Radio.StateChanged`, `DeviceWatcher`) |
| Jendela Claude 5 jam | Waktu `UserPromptSubmit` dari hook (sudah ada) untuk perkiraan; waktu reset resmi dari data Claude Code bila tersedia (diverifikasi Fase 3). Waktu akhir jendela disimpan sebagai UTC lewat `Scheduler`, jadi tahan restart | Event + jadwal |
| Sesi Claude | Hook Claude Code → `Gili.ClaudeHook` → named pipe `\\.\pipe\Gili.Claude` (tabel di bawah). Pelengkap: file `~/.claude/sessions/<pid>.json` (judul sesi, status `busy`/`idle`, PID) dipantau dengan `FileSystemWatcher`. Jendela sesi dicari dari rantai proses induk (Toolhelp snapshot) lalu dipulihkan dan difokuskan (`ShowWindow` + `SetForegroundWindow`). Pembaruan UI digabung, maksimal 4 kali per detik | Event + cek proses hidup tiap 5 detik selama ada sesi |

**Hook Claude Code**

Semua hook memanggil `"<path helper>" <NamaEvent>` (path absolut di folder instalasi untuk installer GitHub, app execution alias untuk MSIX; lihat 9.4) dengan timeout 2 detik. Path ditulis dengan garis miring maju, seperti hook wmux yang sudah ada, supaya aman di shell yang dipakai Claude Code untuk menjalankan hook di Windows.

| Hook | Matcher | Dikirim ke Gili | Efek |
|---|---|---|---|
| `SessionStart` | — | `session_id`, `cwd` | Sesi masuk daftar sebagai Idle |
| `UserPromptSubmit` | — | `session_id`, waktu (teks prompt tidak dikirim) | Working; waktu mulai dicatat; gelembung "belum dilihat" sesi itu hilang |
| `PreToolUse` | `AskUserQuestion\|ExitPlanMode` | `session_id`, nama tool, teks pertanyaan pertama (maks. 200 karakter) | Needs input (pertanyaan atau rencana) |
| `Notification` | — | `session_id`, `notification_type`, `message` | `permission_prompt` → Needs input (izin); jenis lain diabaikan |
| `PostToolUse` | `*` | `session_id` | Needs input → Working |
| `Stop` | — | `session_id` | Done |
| `SessionEnd` | — | `session_id` | Sesi dihapus dari daftar |

- Izin yang ditolak tidak memicu `PostToolUse`; status kembali ke Working saat tool berikutnya selesai, atau langsung Done saat Claude berhenti.
- Esc di Claude tidak memicu `Stop`. Kalau file sesi berubah ke `idle` sementara Gili masih mencatat Working atau Needs input, sesi menjadi Idle tanpa Done.
- Sesi yang prosesnya sudah tidak ada dihapus walau `SessionEnd` tidak terkirim (misalnya Claude Desktop ditutup paksa).

### 9.3 Alur data

Provider mengirim snapshot ke state di Core. `IslandCoordinator` menentukan apa yang tampil di pulau. ViewModel berlangganan state dan memperbarui UI lewat dispatcher WPF. `Scheduler` menyimpan setiap jadwal sebagai waktu absolut (UTC) di database, lalu menghitung ulang semuanya saat app mulai, saat bangun dari sleep, dan saat jam atau zona waktu sistem berubah.

### 9.4 Instalasi dan autostart

Gili dirilis lewat dua jalur dari build yang sama. Keduanya `dotnet publish` **self-contained** win-x64 (runtime .NET ikut, karena pengguna umum tidak punya .NET Desktop Runtime), tanpa hak admin, dan bertanda tangan.

| | Microsoft Store | Installer GitHub |
|---|---|---|
| Bentuk | Paket MSIX (`runFullTrust`), diunggah ke Partner Center dengan akun developer perorangan (gratis) | Installer per-pengguna (`Gili-Setup-<versi>.exe`) yang memasang ke `%LOCALAPPDATA%\Programs\Gili\` tanpa UAC |
| Tanda tangan | Ditandatangani Microsoft saat sertifikasi, jadi tanpa peringatan SmartScreen | Installer dan semua exe di dalamnya ditandatangani sertifikat code signing **OV** milik pengembang (sekitar $200–400 per tahun, kunci di token hardware atau HSM cloud) plus timestamp. EV tidak dipakai karena sejak 2024 tidak lagi langsung melewati SmartScreen. Peringatan SmartScreen bisa tetap muncul di minggu-minggu awal sampai reputasi sertifikat terkumpul |
| Tempat unduh | Halaman Store | Repo **publik** khusus rilis (`gili-releases`, tanpa kode sumber), karena repo kode tetap privat |
| Autostart | Ekstensi `StartupTask` di manifest (bukan `HKCU\...\Run`, yang divirtualisasi di MSIX). Kalau pengguna mematikannya di Task Manager, Gili tidak bisa menyalakannya lagi; saklar Start with Windows menampilkan `Turned off in Task Manager › Startup apps` | Entri `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, ditulis saat run pertama |
| Toast | Memakai identitas paket, tanpa shortcut | Shortcut Start Menu dengan AUMID, dibuat installer |
| Update | Store. Gili juga memeriksa update Store sekali sehari (`StoreContext`); kalau ada, Settings → About menampilkan `Update available · <versi>` dan tombol **Install update** | Cek versi terbaru di `gili-releases` saat start lalu tiap 24 jam (F-PUB-04). Kalau ada, titik biru kecil di tombol Settings dan baris `Update available · <versi>` di Settings → About dengan **Install update** dan **Release notes**. Install update mengunduh installer ke folder temp, memastikan tanda tangannya dari penerbit yang sama, lalu menjalankannya tanpa dialog; installer menutup Gili lewat `--quit` dan menjalankannya lagi |
| Uninstall | Lewat Windows. MSIX tidak bisa menjalankan kode saat dilepas, jadi Settings punya tombol **Prepare to uninstall** yang melepas hook Claude dan memulihkan taskbar Windows | Uninstaller menjalankan `Gili.exe --quit`, melepas hook Claude dari `~/.claude/settings.json` (aturan F-CLD-01), menghapus entri Run, dan menyisakan data pengguna kecuali pengguna mencentang `Also delete my notes and settings`, yang juga menghapus salinan di `%USERPROFILE%\.gili` (bagian 10) |

- Pengembangan tetap memakai `dotnet run` (tanpa paket) dan `pwsh scripts/publish.ps1` untuk memasang salinan lokal. `scripts/package.ps1` membangun paket MSIX dan installer GitHub; paket MSIX ikut dibangun dan dipasang di laptop pengembang **sejak Fase 1**, supaya perbedaan perilaku MSIX (virtualisasi file dan registry, StartupTask, identitas paket) ketahuan sebelum Fase 5.
- Nomor versi `MAJOR.MINOR.PATCH` sama di kedua jalur; MSIX memakai `MAJOR.MINOR.PATCH.0`.
- Kalau kedua versi terpasang, hanya satu yang jalan (mutex F-BAR-05 dipakai bersama), dan Settings versi yang jalan menampilkan `Another copy of Gili is installed` dengan saran melepas salah satunya.
- Menyalakan `Show Claude Code sessions` di Settings: Gili menulis hook ke `~/.claude/settings.json` dengan path helper yang tidak berubah antarversi; tanpa UAC. Installer GitHub memakai path absolut `Gili.ClaudeHook.exe` di folder instalasi. MSIX tidak boleh memakai path di `C:\Program Files\WindowsApps\…` karena berubah tiap update, jadi helper didaftarkan sebagai **app execution alias** (`gili-claude-hook.exe` di `%LOCALAPPDATA%\Microsoft\WindowsApps\`) dan path itu yang ditulis ke hook. Hook berlaku untuk sesi Claude yang dimulai sesudahnya, dan Settings menuliskan `Applies to Claude sessions started from now on`.
- Baris status integrasi di Settings: `Waiting for the first Claude session`, `Connected · last event 2 min ago`, atau salah satu pesan error di bagian 11.

## 10. Data dan penyimpanan

Folder data berisi `gili.db` (SQLite, mode WAL dengan `synchronous=FULL`: setiap commit di-fsync, jadi listrik putus tidak menghilangkan tulisan yang sudah di-commit; F-TOOL-09), `settings.json`, `backups\`, `logs\`. Lokasinya tergantung cara Gili dipasang:

| Instalasi | Folder data |
|---|---|
| Installer GitHub, `dotnet run`, `scripts/publish.ps1` | `%LOCALAPPDATA%\Gili\` |
| Microsoft Store (MSIX) | Folder lokal paket (`ApplicationData.Current.LocalFolder`, yaitu `%LOCALAPPDATA%\Packages\<nama paket>\LocalState\`). MSIX mengalihkan tulisan ke AppData ke folder privat paket, jadi folder ini dipakai langsung supaya lokasinya jelas. Windows menghapus folder ini saat uninstall; salinan untuk reinstall (di bawah) membawa datanya kembali |

- **Salinan untuk reinstall** (permintaan pengguna 2026-10-31: note tidak boleh hilang saat Gili dipasang ulang dengan versi terbaru): salinan `gili-YYYY-MM-DD.db` (backup API SQLite, 7 hari) dan `settings.json` juga ditulis ke `%USERPROFILE%\.gili\backups\` (folder hidden, di luar folder data mana pun, tidak divirtualisasi MSIX dan tidak ikut OneDrive) saat start, tiap jam, dan saat Quit. Saat Gili mulai tanpa `gili.db` (instalasi baru, reinstall, atau versi Store yang pertama kali jalan setelah versi GitHub), salinan terbaru dipakai, begitu juga `settings.json` yang belum ada; file yang sudah ada tidak pernah ditimpa. Jadi uninstall lalu install versi Store kehilangan paling banyak satu jam, dan pindah GitHub ↔ Store membawa note ke dua arah. Log mencatat `Restored gili.db from …`; toast `Restored your notes and settings` menyusul bersama layanan toast. Desain: `docs/superpowers/specs/2026-10-31-data-tetap-saat-reinstall-design.md`.

| Tabel | Kolom utama |
|---|---|
| `notes` | `id`, `body`, `color` (0–7, default 0), `text_size` (0–3: Extra small, Small, Medium, Large; default 2), `sort_order`, `created_at`, `updated_at`, `float_x`, `float_y`, `float_w`, `float_h` (posisi dan ukuran note mengambang dalam piksel tanpa skala DPI; kosong bila tidak mengambang) |
| `todos` | `id`, `title`, `due_date` (opsional), `due_time` (opsional, jam lokal `HH:mm`), `remind_at` (opsional, pengingat yang masih menunggu), `done_at` (opsional), `sort_order`, `created_at` |
| `alarms` | `id`, `time_of_day`, `repeat_days` (bitmask, 0 = sekali), `label`, `sound` (default `radar`), `enabled`, `next_fire_at` |
| `timers` | `id`, `kind` (timer/pomodoro), `label` (kosong untuk timer pulau; timer berlabel dari Tools selalu berlabel tidak kosong), `duration_s`, `ends_at` (saat jalan), `remaining_s` (saat dijeda), `pomodoro_phase`, `pomodoro_round` |
| `stopwatch` | `id`, `started_at`, `accumulated_ms`, `laps` (JSON) |
| `clipboard_history` | `id`, `text`, `source_app`, `copied_at` (dibatasi 50 baris) |
| `claude_window` | `id`, `started_at`, `resets_at`, `source` (official/estimated), `limit_hit_at` (opsional), `notified` (1 baris aktif) |
| `shelf_items` | `id`, `paths` (JSON, satu path atau banyak untuk bundel), `display_name`, `size_bytes`, `sort_order`, `added_at` (dibatasi 8 baris) |

- Database dibackup sekali sehari ke `backups\`, disimpan 7 salinan. `gili.db` yang rusak diganti backup harian terbaru, lalu salinan untuk reinstall.
- Migrasi skema lewat tabel `schema_version`. File dari versi Gili yang lebih lama disalin dulu ke `backups\gili-before-v<N>.db` (tidak ikut dirotasi) sebelum dimigrasi, jadi langkah migrasi yang salah tidak menghilangkan data.
- `settings.json` berisi: posisi bar, sembunyikan taskbar Windows beserta status asli taskbar, format jam, tampilkan detik (default nyala), opacity bar (default 100), tingkat kaca, tinggi bar, tinggi pulau, ambient glow dan motion, Gili nyala/mati, saklar tampil elemen kiri, saklar halaman pulau, modul kanan (id, nyala, urutan), Wi‑Fi/Bluetooth/Volume/Brightness/tray/jam nyala, timer Claude 5 jam nyala, lirik nyala/mati, ambang baterai, kota cuaca (nama, lat, lon), hotkey, integrasi Claude nyala/mati, autostart, suara timer, Alert volume, Gradually increase, durasi pomodoro (fokus, istirahat pendek, istirahat panjang, jumlah sesi).

## 11. Penanganan error

| Kejadian | Penanganan |
|---|---|
| Provider melempar exception atau sumber data hilang | Modul menampilkan "—" dengan alasan di tooltip; dicoba lagi setelah 5 detik, 30 detik, lalu tiap 2 menit |
| Exception tak tertangani | Ditulis ke log harian (disimpan 7 hari), AppBar dilepas, taskbar Windows dikembalikan ke status aslinya, app restart sendiri sekali. Crash kedua dalam 1 menit menghentikan app dan menampilkan toast berisi lokasi log |
| Explorer restart | Daftar ulang AppBar saat menerima `TaskbarCreated` |
| Resolusi, DPI, atau monitor utama berubah | Hitung ulang posisi dan ukuran bar serta pulau |
| Bangun dari sleep | Daftar ulang AppBar, muat ulang provider, jalankan `Scheduler` untuk jadwal terlewat |
| `settings.json` rusak | Ganti nama ke `settings.json.bak`, pakai default, tampilkan toast |
| Database rusak | Pulihkan dari backup terbaru dan tampilkan toast; kalau gagal, buat database baru dan simpan file rusak |
| Hotkey sudah dipakai aplikasi lain | Hotkey itu tidak aktif; Settings menampilkan "Used by another app" |
| Izin notifikasi ditolak atau dicabut di Windows Settings | Saklar Show Windows notifications mati; Settings menampilkan `Gili can't read notifications` dan tombol Open Windows settings |
| `UserNotificationListener` tidak tersedia (identitas paket gagal didaftarkan di versi GitHub) | Saklarnya nonaktif dengan keterangan `Not available in this copy of Gili`; dicatat sekali di log |
| Lirik gagal diambil (LRCLIB tidak menjawab dalam 5 detik, offline, atau error) | Halaman Media menampilkan `Lyrics unavailable offline` (offline) atau `No lyrics for this song`; slot tetap judul; dicoba lagi saat lagu berikutnya, tanpa toast |
| Cuaca gagal diambil | Tampilkan nilai terakhir berwarna abu-abu; tooltip "Last updated 14:00" |
| Item Shelf hilang saat diseret keluar | Seret dibatalkan, tile menampilkan "File moved or deleted" selama 2 detik lalu dihapus |
| File dilepas dari jendela admin | Windows memblokirnya; drop zone menampilkan "Can't take files from an admin window" selama 2 detik |
| Tempel dari halaman Clipboard ke jendela admin (UIPI memblokir `SendInput`) atau tidak ada jendela tujuan (desktop, jendela sudah ditutup) | Teks tetap ditaruh di clipboard; wide `Copied · press Ctrl+V to paste` selama 2 detik |
| Clipboard sedang dikunci aplikasi lain saat menempel (`OpenClipboard` gagal) | Dicoba lagi 3 kali tiap 50 ms; kalau tetap gagal, wide `Couldn't paste · try again` selama 2 detik dan pulau tidak menutup |
| Tidak ada perangkat audio atau suara gagal diputar | Kartu tetap tampil; toast dikirim dengan suara notifikasi Windows sebagai cadangan; dicatat di log |
| `~/.claude/settings.json` bukan JSON valid | File tidak diubah sama sekali; Settings menampilkan `Can't read Claude settings` dan tombol untuk membuka file itu |
| Gili tidak berjalan saat hook dipanggil | Helper tidak menemukan pipe dan keluar dalam ≤ 100 ms tanpa efek; Claude bekerja seperti biasa |
| Helper tidak ada di path hook (Gili dipindah atau dihapus) | Hook gagal tanpa menghentikan Claude; Settings menampilkan `Hook helper not found · Reinstall` |
| Hook Gili dihapus dari luar Gili | Settings menampilkan `Hooks removed outside Gili · Reinstall` |
| Gili mati paksa saat taskbar Windows disembunyikan | Taskbar tetap tersembunyi sampai Gili jalan lagi; saat start, Gili membaca status asli dari `settings.json` dan memulihkannya bila saklar sudah mati. `Gili.exe --quit` dan `--restore-taskbar` memulihkannya dari luar |
| Ikon tray tidak bisa dibaca (host tray gagal) | Tombol Tray menampilkan panel dengan `Can't show tray icons` dan tombol `Show Windows taskbar`; dicoba lagi sesuai aturan provider |
| Radio, Wi‑Fi, atau brightness tidak bisa diubah | Kontrolnya dinonaktifkan dengan alasan di tooltip; panel lain tetap jalan |
| Format file sesi Claude tidak dikenali | Nama sesi diganti nama folder, status hanya dari hook, deteksi Esc tidak jalan; dicatat sekali di log |
| Cek update gagal (offline, GitHub atau Store tidak menjawab) | Diam saja; dicoba lagi di jadwal berikutnya; Settings → About menampilkan `Couldn't check for updates` bila gagal lebih dari 3 hari |
| Installer update yang diunduh tanda tangannya tidak cocok | Tidak dijalankan dan dihapus; Settings menampilkan `Update couldn't be verified` dan tombol Release notes untuk unduh manual |
| Gili ditutup paksa oleh update atau uninstall | Sebelum menimpa file, installer GitHub menjalankan `Gili.exe --quit`. Untuk MSIX, Gili menangani `WM_ENDSESSION` dan `WM_CLOSE` lewat jalur Quit biasa (lepas AppBar, pulihkan taskbar) dan mendaftar `RegisterApplicationRestart` supaya jalan lagi setelah update |
| Versi Store dilepas tanpa Prepare to uninstall | Hook Gili tertinggal di `~/.claude/settings.json` dan menunjuk alias yang sudah hilang; Claude tetap jalan karena hook yang gagal tidak menghentikannya. Taskbar Windows tetap auto-hide sampai pengguna mengubahnya. Halaman dukungan menjelaskan cara membersihkannya |

## 12. Risiko dan mitigasi

| Risiko | Dampak | Mitigasi |
|---|---|---|
| Windows 11 24H2+ mewajibkan izin Location untuk membaca nama Wi‑Fi | Nama tidak tampil | Tampilkan ikon sinyal saja; tooltip menjelaskan izinnya; tombol ke `ms-settings:privacy-location` |
| VLC 3.x tidak mengirim sesi media Windows | Video VLC tampil dengan judul saja, tanpa progres | Judul dari judul jendela VLC; progres hanya bila HTTP interface VLC diaktifkan (pengaturan opsional). VLC 4 sudah mengirim sesi |
| Tombol dialog konflik Explorer tidak bisa ditekan dari luar | Pilihan di kartu pulau tidak sampai ke Explorer | Kartu menampilkan Open dialog yang membawa dialog Explorer ke depan; pengguna memilih di sana |
| Aplikasi tujuan tidak menempel dengan Ctrl+V (misalnya konsol lama yang memakai klik kanan, atau aplikasi yang memakai shortcut lain) | Teks dari halaman Clipboard tidak muncul di aplikasi itu | Teks tetap ada di clipboard, jadi pengguna bisa menempelnya sendiri; diuji di Fase 4 dengan Notepad, Word, VS Code, Chrome/Edge, Windows Terminal, dan wmux |
| Lirik tidak ada di LRCLIB, salah lagu, atau waktunya meleset (terutama lagu daerah dan rilisan baru); LRCLIB berhenti beroperasi | Lirik tidak tampil atau tidak pas | Pencocokan durasi ≤ 2 detik; `No lyrics for this song` bila ragu; fitur opt-in dan best-effort, bar tidak terpengaruh. Lirik resmi Spotify tidak dipakai karena tidak tersedia untuk aplikasi lain |
| `UserNotificationListener` butuh identitas paket; versi GitHub adalah exe biasa | Notifikasi Windows tidak bisa dibaca di versi GitHub | Installer GitHub mendaftarkan sparse package bertanda tangan sertifikat OV yang sama (tanpa admin); diverifikasi di awal Fase 4. Kalau gagal, fitur hanya ada di versi Store dan Settings versi GitHub menuliskannya |
| Event `NotificationChanged` tidak dikirim ke aplikasi desktop (hanya ke background task UWP) | Notifikasi tidak muncul di pulau | Cadangan `GetNotificationsAsync` tiap 2 detik selama fitur nyala (anggaran 8.1); diverifikasi di awal Fase 4 |
| Do Not Disturb Windows 11 tidak tercermin di `SHQueryUserNotificationState` | Notifikasi tetap muncul di pulau saat DND | Diverifikasi di Fase 4; kalau tidak terbaca, Settings menyarankan mematikan Show Windows notifications selama DND, dan fase fokus pomodoro tetap menahan banner |
| Sertifikasi Store mempertanyakan pembacaan notifikasi aplikasi lain | Fitur ditolak di versi Store | Opt-in dengan izin Windows sendiri; alasannya ditulis di catatan sertifikasi; kalau ditolak, fitur dimatikan di versi Store saja (flag build) |
| Struktur atau teks dialog progres Explorer berubah | Progres copy/move/delete tidak terbaca atau jenis operasinya salah dikenali | Fitur best-effort; kalau jenis operasi tidak dikenali, tampil ikon dan warna netral dengan teks asli dari dialog; cuplikan clipboard tetap jalan |
| Tidak ada API resmi untuk Do Not Disturb | Notif aplikasi lain tetap muncul saat fokus | Kejadian pulau milik app tetap ditahan; kalau DND gagal, buka quick settings Windows |
| Wake timer dimatikan di Power Options | Alarm tidak berbunyi saat laptop sleep | Alarm terlewat berbunyi saat bangun; Settings memberi peringatan bila wake timer mati |
| Windows tidak menyediakan radius blur yang bisa diatur | "Glass" hanya tiga tingkat, bukan slider | Opacity tetap slider kontinu; kaca dipetakan ke Off / blur-behind / acrylic |
| Drag-and-drop dari aplikasi admin diblokir (UIPI) | File dari jendela admin tidak bisa masuk Shelf | App utama tetap non-admin supaya drag biasa jalan. Windows tidak memberi tahu aplikasi biasa bahwa seret dari jendela admin diblokir, jadi drop zone memang tidak muncul dan tidak ada pesan (temuan Fase 3d) |
| App crash meninggalkan ruang AppBar kosong | Ruang 32 px tidak terpakai | Lepas AppBar di semua jalur keluar dan di handler exception; restart otomatis mendaftar ulang |
| Browser hanya mengirim satu sesi media | Hanya satu sumber terlihat | Pakai sesi yang sedang diputar; kalau beberapa diputar, pilih yang terakhir berubah |
| Windows di-mute atau volume 0 saat alarm | Alarm tidak terdengar | Peringatan dan tombol Unmute di kartu; animasi kartu tetap jalan; Gili tidak mengubah volume sistem diam-diam |
| Deteksi update yang menunggu tidak selalu sama dengan Start menu | Item "Update and …" tidak muncul padahal ada update, atau sebaliknya | Item hanya ditampilkan bila deteksi yakin; perilaku `SHUTDOWN_INSTALL_UPDATES` dan sumber deteksi diverifikasi di laptop pengguna saat Fase 3. Kalau tidak cocok, menu menampilkan `Open Windows Update` sebagai ganti item update |
| Sleep lewat `SetSuspendState` berbeda di laptop Modern Standby | Laptop tidak tidur, atau masuk hibernate | Diuji di laptop pengguna saat Fase 3; kalau berbeda, Sleep memakai jalur lain yang ditentukan saat implementasi |
| Prompt izin di Claude Desktop tidak memicu hook `Notification` (Desktop menangani izin sendiri) | Menunggu izin tampil sebagai Working, bukan Needs input | Diverifikasi di awal Fase 3. Kalau tidak terpicu, Needs input hanya untuk pertanyaan dan rencana; kalau file sesi ternyata punya status menunggu izin, status itu dipakai sebagai gantinya |
| Format `~/.claude/sessions/*.json` tidak resmi dan bisa berubah setelah update Claude | Nama sesi dan deteksi Esc hilang | File itu hanya pelengkap; cadangan di bagian 11 |
| Claude Code tidak menyediakan waktu reset jendela 5 jam yang bisa dibaca Gili | Timer hanya perkiraan; bisa meleset bila jendela dimulai dari perangkat lain (claude.ai, ponsel) | Diverifikasi di awal Fase 3. Tanpa sumber resmi, timer memakai perkiraan dari prompt pertama dan tooltip menulis `Estimated`; batas tercapai dideteksi dari pesan batas di file sesi bila ada |
| Host tray tambahan (ManagedShell) tidak menerima semua ikon di Windows 11 versi pengguna | Sebagian ikon tray tidak tampil | Diuji di laptop pengguna di awal Fase 3 sebelum tray dibangun; kalau gagal, tombol Tray membuka overflow tray Windows sebagai cadangan |
| Menyembunyikan `Shell_TrayWnd` tidak didukung resmi dan bisa berubah setelah update Windows | Taskbar muncul lagi atau ruangnya tertinggal | Pakai auto-hide resmi sebagai dasar, sembunyikan jendela hanya sebagai tambahan; periksa ulang tiap `TaskbarCreated` dan saat bangun dari sleep |
| Animasi terus-menerus di jendela WPF transparan dirender di CPU | Ambient motion atau mata Gili melewati anggaran CPU | Anggaran dan cadangan di 8.1; diukur di Fase 1 sebelum fitur dianggap selesai |
| Sertifikasi Microsoft Store menolak perilaku yang mengubah shell Windows: menyembunyikan `Shell_TrayWnd`, menjadi host tray tambahan (ManagedShell), mendaftar AppBar, atau mengubah file konfigurasi app lain (`~/.claude/settings.json`) | Gili tidak bisa masuk Store, atau harus dirilis tanpa fitur tertentu | Semua fitur itu opt-in dan bisa dibatalkan pengguna; catatan sertifikasi menjelaskan alasannya. Paket MSIX diajukan sebagai **private/hidden listing** lebih dulu di awal Fase 5 untuk melihat hasil sertifikasi sebelum rilis. Kalau satu fitur ditolak, fitur itu dimatikan di versi Store saja (flag build) dan tetap ada di versi GitHub |
| Antivirus menandai Gili sebagai malware (false positive) karena perilakunya mirip spyware: hook keyboard global untuk Caps/Num Lock, listener clipboard, autostart, membaca jendela app lain (UI Automation), dan mengubah `~/.claude/settings.json` | Installer diblokir atau dikarantina di komputer pengguna | Semua exe ditandatangani; tidak ada unduhan kode, obfuscation, atau packer; cek VirusTotal sebelum setiap rilis dan lapor false positive ke Microsoft Security Intelligence serta vendor lain (F-PUB-05). Hook keyboard langsung mengabaikan semua tombol selain Caps Lock dan Num Lock, tanpa menyimpan atau mencatat apa pun; privacy policy menyebutkannya |
| Reputasi SmartScreen sertifikat OV baru masih nol | Pengguna awal versi GitHub tetap melihat `Windows protected your PC` | Halaman rilis mengarahkan ke Store sebagai jalur utama; reputasi naik seiring unduhan. Sertifikat diperpanjang sebelum habis dengan kunci yang sama bila penerbit memungkinkan, supaya reputasi tidak mulai dari nol |
| App execution alias menambah waktu mulai `Gili.ClaudeHook` di versi Store | Target ≤ 50 ms (p95) F-CLD-07 terlewati | Diukur di awal Fase 3 dengan paket MSIX. Kalau lewat, helper disalin ke folder di luar paket yang tidak divirtualisasi (kapabilitas `unvirtualizedResources`, perlu alasan di sertifikasi) dan path itu yang ditulis ke hook |
| Perangkat pengguna umum berbeda dari laptop pengembang (desktop tanpa baterai atau brightness, GPU dan driver lain, versi Windows 11 yang masih didukung Microsoft, DPI dan bahasa sistem lain) | Modul atau pulau kontrol rusak di perangkat yang tidak pernah diuji | Modul yang sumbernya tidak ada disembunyikan, bukan error; matriks uji di bagian 13; log menyebut versi Windows dan jenis instalasi supaya laporan bug bisa dilacak |
| Update Claude Code mengubah nama event atau isi JSON hook | Status sesi tidak terbaca | Helper mengabaikan field yang tidak dikenal; Settings menampilkan waktu kejadian terakhir (`Connected · last event 2 min ago`) supaya masalah cepat terlihat |

## 13. Pengujian

- **Unit test (xUnit) untuk Core**: `Scheduler` (alarm berulang, snooze, terlewat saat sleep, restart di tengah timer, perubahan zona waktu), `IslandCoordinator` (semua aturan prioritas di 6.3, termasuk fullscreen dan drop zone), `ShelfService` (batas 8 item, bundel, pembersihan file hilang termasuk sebagian isi bundel, hasil Move vs Copy), format angka/satuan/tanggal, definisi "todo hari ini", pengubahan urutan note, kurva volume naik bertahap, aturan kapan suara dibisukan (fase fokus, mute), pemilihan ekspresi Gili (urutan prioritas, tidur setelah 5 menit tanpa input dan bangun, lirikan per menit dengan jeda minimal 4 detik, ambang seret 6 px, klik singkat di antara dua pembacaan tetap terhitung).
- **Unit test revisi 4 (Core)**: aturan bentuk Gili (di pulau ↔ lingkaran, tersembunyi saat mengambang), pemetaan posisi kursor ke posisi mata (ujung kiri, tengah, ujung kanan pulau; naik/turun mengikuti tinggi kursor di layar; lirikan 2 × 2 px di lingkaran), jadwal kedip acak dengan seed tetap, perhitungan tinggi pulau terbuka (minimal, maksimal, posisi bawah), jendela Claude 5 jam (mulai dari prompt pertama, pembulatan jam, restart di tengah jendela, batas tercapai, kartu saat berakhir, ditahan saat fokus), halaman tersembunyi dan tab sementara (termasuk tab yang ikut mati lewat ikon kiri, tanpa pernah nol tab), aturan posisi bawah hanya saat taskbar Windows tersembunyi.
- **Unit test sesi Claude (Core)**: perpindahan status (prompt → Working → Needs input → Working → Done; Esc → Idle; proses mati → dihapus; izin ditolak), penggabungan beberapa sesi jadi satu aktivitas, aturan gelembung "belum dilihat", posisi di `IslandCoordinator` termasuk saat fase fokus dan fullscreen.
- **Unit test penggabung `settings.json` Claude**: memasang dua kali tidak menggandakan entri, hook wmux tetap ada, melepas hanya menghapus entri Gili, JSON rusak tidak disentuh, cadangan dibuat.
- **Unit test notifikasi Windows (Core)**: posisi notifikasi di `IslandCoordinator` (di bawah drop zone, di atas kejadian wide lain yang dilewati selama notifikasi tampil; menunggu saat pulau terbuka dan dibuang setelah 10 detik; dilewati saat fokus, DND, dan fullscreen), penggabungan pesan dengan aplikasi + judul yang sama, pemendekan notifikasi saat antrean berisi, panjang pulau maksimal dan elemen bar yang tertimpa, penyaringan Gili, Claude Desktop, dan aplikasi yang dimatikan.
- **Unit test lirik (Core)**: parsing LRC (beberapa stempel waktu per baris, offset `[offset:]`, baris kosong), pemilihan baris dari posisi lagu (sebelum baris pertama, jeda instrumental, seek mundur), pembersihan judul, pemilihan hasil cari berdasarkan durasi ≤ 2 detik.
- **Test repository** pada file SQLite sementara: simpan/muat, migrasi, batas baris (50 riwayat clipboard, 8 item Shelf), teks clipboard yang sama naik ke atas tanpa duplikat.
- **Mode demo** untuk memeriksa tampilan dan gerak semua keadaan tanpa kejadian nyata.
- **Unit test revisi 6**: perbandingan versi untuk cek update (termasuk versi pra-rilis yang diabaikan), keputusan migrasi data GitHub → Store (folder kosong, sudah ada, database terkunci), pemilihan folder data per jenis instalasi.
- **Checklist manual per fase** di laptop pengembang, dengan build biasa dan dengan paket MSIX (paket MSIX sejak Fase 1, F-PUB-01): ruang AppBar, fullscreen (YouTube, game), restart Explorer, sleep/resume, DPI 100/125/150%, lebar 1366 dan 1920 px, colok/cabut charger, Spotify dan YouTube di Chrome/Edge (termasuk artwork di slot dan gelembung, lagu tanpa artwork, dan lirik: nyala/mati, lagu Indonesia dan Inggris, lagu tanpa lirik, seek, jeda, offline), video di Media Player dan VLC, copy, cut/move, dan delete file besar (termasuk Shift+Delete, batal di tengah jalan, dan bentrok nama file), copy gambar, Caps Lock, kamera dan mikrofon dipakai bersamaan (compact dan saat pulau terbuka), alarm saat Windows di-mute dan saat volume 0, cabut headset saat alarm berbunyi, pulau Power (Sleep, Shut down, Restart, dan varian update saat ada update menunggu), alarm saat sleep, matikan paksa saat mengetik note, seret file masuk dan keluar Shelf, atur urutan note dengan mouse dan keyboard, buka note di kolom kiri, tengah, dan kanan serta di baris bawah (arah tumbuh editor), ubah ukuran teks note saat menambah dan mengedit (keempat ukuran di editor, kartu, dan note mengambang, lalu restart), New note dan `Ctrl+Alt+N` (tumbuh ke bawah dan ke kiri), seret note keluar pulau lalu kembali masuk sebelum dilepas (tanpa patah: tidak terpotong tepi pulau, titik pegang tetap di bawah pointer saat robek dan saat kembali, direkam layar 60 fps), seret editor yang terbuka dan note baru keluar lewat grip lalu lepas di luar dan di dalam pulau, lepas note di luar pulau (kartu hilang, pulau menutup) dan langsung mengetik, Float on top, ubah ukuran note mengambang dari keempat tepi dan keempat sudut sampai batas minimal dan maksimal, enam note mengambang lalu yang ketujuh, Back to island saat pulau tertutup dan saat halaman Notes terbuka, note mengambang setelah restart dan setelah DPI 125/150%, note mengambang saat YouTube fullscreen, tambah todo untuk hari ini dan tanggal lain dari halaman Calendar (termasuk lewat `Ctrl+Alt+T`), sambungkan dan putuskan earbuds Bluetooth, buka dan tutup beberapa aplikasi lalu pindah fokus di antaranya, ubah tinggi bar dan pulau di Settings, Transparency effects mati, sesi Claude Desktop yang bertanya / minta izin / menyerahkan rencana / dihentikan dengan Esc / ditutup, dua sesi Claude bersamaan, sesi `claude` di wmux, Claude dipakai saat Gili mati, overhead hook Claude (p95), Gili mengikuti kursor di seluruh layar dan berkedip (termasuk lingkaran di kiri menu saat pulau terbuka), ekspresi Gili (kedip setiap klik termasuk klik ganda cepat, mata besar saat menyeret file di Explorer dan jendela, musik, timer dengan lirikan menit, kartu alarm, note kembali ke pulau, diam 5 menit lalu gerakkan kursor, mengetik tanpa menggerakkan kursor tidak membuat Gili tidur), Gili pindah ke lingkaran saat musik mulai dan kembali saat berhenti, CPU saat menggerakkan mouse dan saat pulau terbuka dengan ambient motion (bandingkan dengan anggaran 8.1), tombol Start, sembunyikan dan munculkan taskbar Windows (termasuk mati paksa lalu start lagi), bar di bawah, panel tray (klik kiri/kanan ikon OneDrive, Discord, dll.), pulau kontrol Wi‑Fi, Bluetooth, Volume, Brightness, dan Power (masing-masing tanpa menu tab, ganti isi saat ikon lain diklik), 15 aplikasi terbuka sekaligus di 1366 px dan 1920 px (`+N` dan daftarnya), jam di kanan, tombol Settings, matikan setiap elemen di Settings (termasuk ikon kiri yang ikut mematikan tabnya lalu menyalakannya lagi), todo dengan jam dan pengingatnya, edit dan hapus todo, Todo dengan 12 tugas tanpa scroll, notifikasi WhatsApp Desktop (chat pribadi, grup, pesan beruntun), Outlook, Teams, dan Telegram di pulau (izin ditolak lalu diberikan, Show message text mati, aplikasi dimatikan, saat pulau terbuka, saat Do Not Disturb, saat fase fokus, saat fullscreen, versi Store dan versi GitHub), jendela Claude 5 jam berakhir, geser kursor perlahan dan cepat sepanjang menu tab lalu keluar (nama melebar dan menyusut, sorotan tetap pas, lebar pulau tidak berubah), pindah tab dengan Tab dan panah, semua halaman di pulau 560 px (9 tab) dan 576 px (10 tab) pada DPI 100/125/150%, tempel dari halaman Clipboard ke Notepad, Word, VS Code, kolom teks browser, Windows Terminal, wmux, dan jendela admin (hanya clipboard), `Ctrl+Alt+V` lalu ketik, panah, dan Enter tanpa mouse.
- **Pengukuran performa**: catat RAM dan CPU dari Task Manager setelah 8 jam pemakaian normal.
- **Matriks perangkat (Fase 5)**: selain laptop pengembang, uji di minimal satu PC desktop (tanpa baterai, tanpa brightness, monitor eksternal sebagai monitor utama), satu VM Windows 11 bersih untuk tiap jalur instalasi, versi Windows 11 lama dan terbaru yang masih didukung, DPI 100/125/150/175%, dan bahasa tampilan Windows selain Inggris (format tanggal dan angka mengikuti UI Inggris). Hal yang di PRD ini "diverifikasi di laptop pengguna" (hook izin Claude Desktop, `SHUTDOWN_INSTALL_UPDATES`, `SetSuspendState` di Modern Standby, host tray) diulang di perangkat ini.
- **Checklist rilis (Fase 5)**: pasang dari Store dan dari installer GitHub di VM bersih (tanpa .NET, tanpa UAC, tanpa peringatan SmartScreen untuk versi Store), autostart setelah restart, update dari versi sebelumnya dengan data utuh, Install update di versi GitHub saat offline dan online, pindah GitHub → Store, dua versi terpasang bersamaan, Prepare to uninstall lalu uninstall versi Store, uninstall versi GitHub (hook Claude terlepas, taskbar pulih, tidak ada ruang AppBar tersisa), Windows App Certification Kit, hasil VirusTotal.

## 14. Fase rilis

| Fase | Isi | Selesai bila |
|---|---|---|
| 1. Fondasi bar | F-BAR-01–07, F-BAR-08 (jam, material, autostart), F-BAR-09, F-BAR-13, F-PUB-01 (paket MSIX dasar), F-LEFT-01, F-ISL-17, F-ISL-01–06, F-ISL-07 kerangka + Media, F-ISL-25, F-ISL-19, F-ISL-24 (netral, tidur, musik), F-ISL-21 (Media), F-RIGHT-01, F-RIGHT-11; tampilan bar lengkap seperti mockup sejak Fase 1 (keputusan pengguna 2026-10-27): F-LEFT-06 Start, ikon launcher F-LEFT-02 (membuka tabnya; halaman Fase 2 berisi placeholder), F-LEFT-04/05 ikon Shelf dan Claude (tanpa fitur di baliknya), modul cuaca, Wi‑Fi, Bluetooth, volume, brightness, tray, dan Power dengan data asli dan fungsi dasar; pulau kontrol, panel tray lengkap, dan isi halaman tetap di fasenya | Bar dipakai seharian penuh tanpa crash, baik dari build biasa maupun paket MSIX; checklist Fase 1 lulus; target 8.1 terpenuhi |
| 2. Produktivitas | SQLite, jendela Tools, F-TOOL-01–09 (F-TOOL-04 bagian pulau), F-TOOL-12, F-ISL-07 Timer/Alarms/Calendar/Todo/Notes, F-ISL-24 (fokus, kaget, senang, mengikuti note), F-ISL-08, F-ISL-16, F-ISL-20, F-ISL-21 (halaman lain), F-ISL-22, F-SND-01–06 | Data tahan restart dan matikan paksa; alarm berbunyi tepat waktu termasuk setelah sleep |
| 3. Sistem lengkap | F-ISL-10–12, F-ISL-14, F-ISL-18, F-ISL-23, F-LEFT-02, F-LEFT-04–06, F-RIGHT-02–04, F-RIGHT-07–10, F-ISL-07 Shelf + System, F-BAR-08 modul dan baterai, F-BAR-10–12, F-CLD-01–08 (diawali verifikasi hook izin di Claude Desktop, sumber waktu reset 5 jam, dan host tray) | Modul kanan cocok dengan Task Manager; overflow bekerja di 1366 px; file bisa masuk dan keluar Shelf; sesi Claude Desktop dan terminal tampil benar saat bekerja, bertanya, dan selesai |
| 4. Ekstra | F-TOOL-10–11, F-LEFT-03, F-ISL-28, F-ISL-07 Clipboard, F-ISL-26, F-ISL-27 (diawali verifikasi `UserNotificationListener` di versi GitHub dan event untuk aplikasi desktop), F-ISL-15, F-RIGHT-05, F-TOOL-04 DND Windows, sisa F-BAR-08 | Semua fitur best-effort punya cadangan yang bekerja saat fiturnya gagal |
| 5. Rilis publik | F-PUB-01 (lengkap), F-PUB-02–08, Settings → About. Diawali pengajuan MSIX sebagai listing tersembunyi untuk melihat hasil sertifikasi Store, dan pembelian sertifikat OV (proses verifikasi identitas bisa makan beberapa hari sampai minggu) | Gili tersedia gratis di Microsoft Store dan `gili-releases`; checklist rilis dan matriks perangkat di bagian 13 lulus; Microsoft Defender tidak mendeteksi apa pun |

Rilis publik pertama dilakukan setelah Fase 4, jadi versi publik pertama sudah berisi semua fitur. Mockup HTML di `docs/mockup/top-taskbar-mockup.html` menampilkan keadaan dari Fase 1–4 sebagai acuan visual dan gerak.

## 15. Di luar cakupan

- Lebih dari satu monitor.
- Membuat ulang Start menu, Quick Settings, atau Notification Center Windows (Gili hanya membukanya, menampilkan ikon tray, dan punya pulau kontrol).
- Memasukkan password Wi‑Fi baru dari pulau Wi‑Fi (diserahkan ke daftar jaringan Windows).
- Ekspresi atau suasana hati maskot selain mengikuti kursor dan berkedip.
- Bahasa UI selain Inggris.
- Sinkronisasi cloud dan backup online.
- Note berformat (markdown, gambar), lampiran todo, todo berulang.
- Riwayat clipboard untuk gambar dan file.
- Lirik resmi dari Spotify (tidak tersedia untuk aplikasi lain), lirik tanpa waktu, terjemahan lirik, dan sorotan per kata (karaoke).
- Menyalin file ke dalam Shelf (Shelf hanya menyimpan referensi).
- File suara milik pengguna untuk alarm atau timer (hanya lima suara bawaan).
- Versi berbayar, langganan, iklan, kode sumber terbuka, dan telemetri.
- Windows 10, ARM64, dan distribusi lewat winget atau toko lain di rilis pertama.
- Suhu CPU dan sensor hardware lain yang butuh hak admin atau driver (F-RIGHT-06 dihapus).
- Tema terang dan kustomisasi warna aksen.
- Tab Chat Claude Desktop; hanya sesi Claude Code (tab Code dan CLI) yang dipantau.
- Menjawab pertanyaan atau izin Claude langsung dari pulau, dan membuka percakapan tertentu di dalam Claude Desktop.
- Membalas pesan, menjalankan tombol aksi notifikasi, menampilkan gambar notifikasi, dan riwayat notifikasi di pulau (Notification Center Windows tetap tempatnya).
