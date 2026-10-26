# PRD: TopBar, top taskbar bergaya Dynamic Island untuk Windows

| | |
|---|---|
| Tanggal | 25 Oktober 2026 (revisi 3: integrasi Claude) |
| Status | Disetujui saat brainstorming, menunggu review dokumen |
| Platform | Windows 11 (laptop), monitor utama |
| Pengguna | Pribadi (satu pengguna) |
| Teknologi | C# WPF, .NET 10 LTS |
| Bahasa UI aplikasi | Inggris saja |
| Mockup | [`docs/mockup/top-taskbar-mockup.html`](../../mockup/top-taskbar-mockup.html) |

Catatan: dokumen ini berbahasa Indonesia, tetapi semua teks yang tampil di aplikasi (label, notifikasi, tombol) berbahasa Inggris dan ditulis apa adanya di dokumen ini.

## 1. Ringkasan

TopBar adalah bar kaca semi-transparan di tepi atas layar, mirip panel desktop Linux, dengan "pulau" hitam pekat di tengah yang meniru Dynamic Island iPhone. Bar dibagi tiga: kiri untuk konteks kerja (launcher dan aplikasi aktif), tengah untuk jam dan aktivitas yang sedang berjalan (media, timer, copy clipboard, progres file, status sesi Claude, ngecas, file di Shelf), kanan untuk status sistem (cuaca, jaringan, CPU, RAM, Wi‑Fi).

Saat dibuka, pulau lepas dari bar menjadi panel hitam mengambang sedikit di bawah tepi atas. Latar bar menjadi transparan, isi kiri dan kanan tetap tampil, dan hanya elemen yang tertimpa panel yang memudar. Pulau punya halaman untuk media, timer, alarm, kalender, todo, note, Shelf (tempat file sementara), sesi Claude, dan statistik sistem. Note ditulis langsung di pulau. Jendela Tools terpisah dipakai untuk pengelolaan: daftar alarm, edit todo, riwayat clipboard, dan Settings.

Aplikasi jalan otomatis saat login, ringan, dan semua note, todo, alarm, serta isi Shelf tetap tersimpan setelah restart.

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
5. Ringan: RAM di bawah 120 MB, CPU saat diam rata-rata di bawah 1%.
6. Status sesi Claude Code (bekerja, butuh jawaban, selesai) terlihat tanpa membuka jendelanya.

**Bukan tujuan**
- Menggantikan taskbar, Start menu, atau system tray Windows. Taskbar bawah tetap seperti biasa, termasuk ikon volume dan baterai.
- Lebih dari satu monitor.
- Sinkronisasi cloud, aplikasi mobile, atau akun pengguna.
- Distribusi publik: Microsoft Store, auto-update, code signing.
- Bahasa UI selain Inggris.
- Riwayat clipboard untuk gambar/file; note berformat kaya.

## 4. Skenario pengguna

1. **Pagi hari**: laptop menyala, bar langsung muncul. Ring progres di pulau menunjukkan 2 dari 6 todo hari ini selesai.
2. **Bekerja sambil mendengar musik**: Spotify jalan, pulau menampilkan judul lagu dan tombol pause. Klik membuka player lengkap; pulau menutup sendiri saat mouse pergi.
3. **Menonton kuliah**: video diputar di VLC, pulau menampilkan judul dan garis progres tipis.
4. **Menyalin**: Ctrl+C pada screenshot, pulau sedikit memanjang dan menampilkan thumbnail gambarnya selama 3 detik. Menyalin folder 4 GB di Explorer membuat pulau sedikit memanjang dan menampilkan progres 0–100%.
5. **Memindahkan file**: pengguna menyeret tiga file dari Downloads ke pulau, membuka folder tujuan, lalu menyeret file-file itu keluar dari halaman Shelf.
6. **Fokus**: pomodoro 25 menit dimulai, pulau menghitung mundur, lalu kartu "Focus session done" muncul saat habis.
7. **Ide mendadak**: Ctrl+Alt+N membuka halaman Notes dengan note baru kosong dan kursor siap mengetik. Tulisan tersimpan otomatis; Esc menutup pulau.
8. **Alarm**: jam 14:30 pulau terbuka dengan kartu alarm (Snooze 5m / Stop), suara berbunyi, toast masuk Notification Center.
9. **Bekerja dengan Claude**: pengguna memberi tugas di Claude Desktop lalu pindah ke browser. Pulau menampilkan nama sesi dan waktu berjalan. Saat Claude bertanya, kartu `Claude needs your input` muncul dengan cuplikan pertanyaan dan bunyi Ping; **Open Claude** membawa Claude Desktop ke depan. Saat selesai, pulau menampilkan `Done · 4m 12s`, lalu gelembung hijau menunggu sampai Claude Desktop dibuka.

## 5. Keputusan desain utama

| Topik | Keputusan |
|---|---|
| Bentuk bar | Bar selebar monitor utama, didaftarkan sebagai AppBar (memesan 32 px; jendela maximize mulai di bawahnya) |
| Material bar | Kaca semi-transparan, secara default lebih lembut dari pulau. Opacity dan kekuatan blur bisa diatur |
| Material pulau | Hitam pekat di semua keadaan |
| Pulau saat terbuka | Panel mengambang dengan sudut membulat penuh, 8 px di bawah tepi atas. Hanya latar bar (warna dan kaca) yang menjadi transparan; isi kiri dan kanan tetap tampil kecuali yang tertimpa panel |
| Taskbar Windows | Tidak diubah; volume dan baterai tetap di sana dan tidak diduplikasi |
| Monitor | Monitor utama saja |
| Aksi cepat vs pengelolaan | Aksi cepat, menulis note, dan Shelf ada di pulau; daftar alarm, edit todo, riwayat clipboard, dan Settings ada di jendela Tools |
| Notifikasi | Kartu di pulau + toast Windows (toast tanpa suara supaya bunyi tidak dobel) |
| Integrasi Claude | Status sesi Claude Code (tab Code di Claude Desktop dan CLI di terminal) dibaca lewat hook Claude Code dan helper kecil; opt-in di Settings. Tab Chat Claude Desktop tidak didukung |
| Bahasa UI | Inggris saja, tanpa pilihan bahasa |
| Teknologi | C# WPF di .NET 10 LTS (.NET 8 habis dukungan 10 Nov 2026) |
| Penyimpanan | SQLite + `settings.json` di `%LOCALAPPDATA%\TopBar\` |

## 6. Tata letak dan perilaku

### 6.1 Bar

- Tinggi default 32 px logis (ikut skala DPI), selebar monitor utama.
- **Tinggi bisa diatur** di Settings:
  - **Bar height**: 28–44 px, default 32 px.
  - **Island height** (tinggi pulau compact dan wide): 20 px sampai tinggi bar − 4 px, default 24 px. Pulau selalu berada di tengah tinggi bar; radius sudutnya setengah tinggi pulau; ukuran teks di pulau mengikuti (setengah tinggi pulau, 11–15 px). Gelembung aktivitas ikut setinggi pulau.
  - Perubahan langsung berlaku dengan animasi pegas; ruang AppBar didaftarkan ulang dengan tinggi baru.
- Terdaftar sebagai AppBar di tepi atas (`SHAppBarMessage`), jadi area kerja Windows berkurang setinggi bar.
- **Material**: warna gelap di atas latar yang di-blur. Default: tint `#161618` dengan opacity 78%, kaca "Strong". Pengaturan:
  - **Bar opacity**: 40–100%.
  - **Glass**: Off (tint polos), Light (blur-behind), Strong (acrylic). Windows tidak menyediakan pengaturan radius blur bebas, jadi tiga tingkat ini dipetakan ke efek blur-behind dan acrylic bawaan sistem.
  - Kalau "Transparency effects" Windows mati, bar digambar polos apa pun pengaturannya.
- Pulau selalu `#000000` pekat, jadi terlihat sebagai benda paling padat di bar.
- Disembunyikan saat aplikasi fullscreen aktif di monitor utama (`ABN_FULLSCREENAPP`), muncul lagi setelah keluar.
- Klik kanan di area kosong bar membuka menu: Open Tools, Settings, Demo mode, Quit.
- **Aturan jarak**: isi kiri dan kanan tidak boleh lebih dekat dari 24 px ke pulau, termasuk titik kamera/mic dan gelembung aktivitas di sampingnya. Jarak ini dihitung dari ukuran pulau saat di-hover (6.3), supaya isi kiri dan kanan tidak bergeser ketika kursor berada di atas pulau. Modul kanan yang tidak muat pindah ke menu "…" (6.4); di kiri, ikon aplikasi yang tidak aktif pindah ke `+N` lalu nama aplikasi aktif dipotong dengan "…" (6.2).

### 6.2 Bagian kiri

Urutan dari kiri: ikon launcher, ikon Shelf, ikon Claude, **pemisah** (garis vertikal tipis), lalu ikon aplikasi yang sedang berjalan. Indikator kamera/mic tidak di sini, tetapi di samping pulau (6.3).

| Elemen | Perilaku |
|---|---|
| Launcher | Tiga ikon: Timer, Todo, Notes. Masing-masing membuka halamannya di pulau (Timer, Todo, Notes). Kalau pulau sudah terbuka, klik pindah ke halaman itu; klik ikon halaman yang sedang terbuka menutup pulau. Pengelolaan lengkap tetap di jendela Tools (tombol Edit / Manage di halaman pulau). |
| Shelf | Ikon tray tepat setelah launcher, selalu tampil. Kalau Shelf berisi, diikuti jumlah file (`3`). Ikon memantul kecil (skala 1,25, pegas 380 ms) saat file baru masuk. Klik membuka halaman Shelf di pulau; hover menampilkan `Shelf · 3 files`; file bisa diseret ke ikon ini seperti ke bagian bar lain. |
| Claude | Glyph Claude (warna `#D97757`) tepat setelah Shelf, selalu tampil saat integrasi Claude nyala. Status mengikuti sesi Claude Code (F-CLD): titik kuning di pojok saat ada sesi yang butuh input, glyph berdenyut pelan saat ada sesi bekerja, titik hijau saat ada sesi selesai yang belum dilihat. Klik membuka halaman Claude Sessions di pulau; hover menampilkan ringkasan (`Claude · 1 needs input`). |
| Pemisah | Garis 1 × 14 px (putih 22%) yang memisahkan grup launcher + Shelf + Claude dari aplikasi. |
| Aplikasi berjalan | Semua aplikasi yang punya jendela di taskbar Windows tampil sebagai **ikon asli** 16 px, urut sesuai waktu pertama dibuka. **Hanya aplikasi aktif yang menampilkan nama**, dalam kapsul tipis di belakang ikonnya; aplikasi lain ikon saja (opacity 70%, penuh saat hover). Hover menampilkan nama; klik ikon membawa aplikasi itu ke depan. Kalau ikon tidak bisa diambil, tampil ikon jendela generik. |

**Gerak saat berpindah aplikasi**: nama aplikasi lama menyusut (lebar ke 0, fade 160 ms) sementara nama aplikasi baru melebar dari ikonnya (300 ms ease-out, fade masuk mulai 90 ms), dan kapsul latarnya berpindah bersamaan; ikon lain ikut bergeser mulus. Aplikasi yang baru dibuka muncul di ujung kanan deretan dengan pegas (skala 0,6 → 1, 320 ms); aplikasi yang ditutup mengecil lalu hilang dan ikon lain merapat.

**Ruang sempit**: ikon aplikasi tidak aktif dari ujung kanan pindah ke penanda `+N` (hover menampilkan daftarnya); aplikasi aktif selalu tampil, dan namanya baru dipotong "…" setelah tidak ada ikon lain yang bisa dipindahkan.

Cuplikan clipboard (copy/cut teks, gambar, file) dan progres copy, move, dan delete dari Explorer tidak tampil di bagian kiri, tetapi di slot pulau (6.3).

### 6.3 Pulau tengah

#### Keadaan

| Keadaan | Posisi | Isi | Bagian bar lainnya |
|---|---|---|---|
| Compact | Di dalam bar, kapsul hitam setinggi pengaturan "Tinggi pulau" (default 24 px), di tengah tinggi bar | Jam + tanggal saat tidak ada aktivitas; saat ada aktivitas hanya aktivitas itu. Lebar mengikuti isinya, minimal 200 px | Tampil |
| Wide | Di dalam bar, kapsul lebih panjang | Kejadian singkat: Caps/Num Lock, charger, peringatan baterai | Tampil |
| Expanded | Panel mengambang 8 px di bawah tepi atas, sekitar 680 × 256 px (lebih besar di halaman Notes: 960 × 500 px), radius sudut 32 px di semua halaman | Menu tab (ikon + nama) selebar pulau, halaman, jam + tanggal di tengah bawah | Latar transparan; elemen tertimpa memudar |
| Kartu notifikasi | Panel mengambang sekitar 560 × 104 px, radius 28 px | Satu notifikasi dengan tombol aksi | Latar transparan; elemen tertimpa memudar |
| Drop zone | Panel mengambang sekitar 420 × 140 px, radius 30 px | Target "Drop to keep on the Shelf" saat file diseret ke atas bar | Latar transparan; elemen tertimpa memudar |

**Bar saat pulau mengambang**
- Latar bar (warna, blur kaca, garis bawah) memudar sampai transparan penuh, jadi wallpaper terlihat di area bar.
- Isi kiri dan kanan **tetap tampil dan tetap bisa diklik**. Teksnya diberi bayangan tipis (hitam 55%, blur 3 px) supaya tetap terbaca di atas wallpaper terang.
- Hanya elemen yang posisinya tertimpa panel pulau, ditambah jarak 8 px di kiri dan kanan panel, yang memudar (opacity 0, 160 ms) dan tidak bisa diklik. Elemen dinilai satu per satu (tiap ikon launcher, aplikasi aktif, tiap modul kanan), jadi di layar lebar biasanya tidak ada yang hilang.
- Saat pulau kembali compact, latar bar dan semua elemen muncul lagi.

Keadaan mengambang memakai bayangan lembut dan garis cahaya tipis di tepi atas, supaya panel hitam tetap terpisah dari jendela gelap di belakangnya.

#### Isi compact

- **Saat tidak ada aktivitas**: jam `14:32` (24 jam, tanpa detik) dan tanggal `Sat 25 Oct`.
- **Saat ada aktivitas**: jam dan tanggal disembunyikan; pulau hanya berisi satu aktivitas dengan prioritas tertinggi. **Lebar pulau mengikuti isi aktivitasnya, tetapi tidak pernah kurang dari 200 px** (selebar notifikasi `Charging · 64%`), dan berubah dengan pegas saat isinya berganti. Lebar minimal ini berlaku juga saat tidak ada aktivitas dan untuk timer, pomodoro, dan stopwatch; aktivitas yang lebih pendek direntang dari ujung ke ujung (ikon di kiri, nilai di kanan). Jam dan tanggal muncul lagi saat aktivitas terakhir selesai. Saat pulau terbuka, jam dan tanggal pindah ke baris kecil di tengah bawah pulau (lihat "Menu halaman").
- Urutan prioritas aktivitas:
  1. Cuplikan clipboard, 3 detik (lihat "Cuplikan clipboard" di bawah)
  2. Sesi Claude yang butuh jawaban, atau hasil `Done` selama 4 detik (lihat "Sesi Claude" di bawah)
  3. Operasi file Explorer (copy, move, delete): ikon berwarna + nama file + garis progres 44 px + persen (lihat "Operasi file Explorer" di bawah)
  4. Timer atau pomodoro berjalan: ikon + sisa waktu `12:04`
  5. Stopwatch berjalan: ikon + waktu berjalan
  6. Sesi Claude yang sedang bekerja: ikon Claude berdenyut + nama sesi + waktu berjalan `2:14`
  7. Sesi media:
     - Musik: bar level bergerak + judul dan artis (maks. 36 karakter) + tombol play/pause
     - Video: ikon film + judul (maks. 32 karakter) + garis progres 36 px
  8. Sedang dicas: ikon petir + persen
- Isi Shelf bukan aktivitas pulau; jumlahnya tampil di ikon Shelf di bagian kiri bar (6.2), jadi jam dan tanggal tidak tersembunyi hanya karena ada file di Shelf.
- Aktivitas berjalan kedua tampil sebagai gelembung bundar kecil di kanan pulau (ikon saja). Klik gelembung membuka halaman aktivitas itu. Gelembung hijau Claude "selesai, belum dilihat" (lihat "Sesi Claude") selalu paling kanan dan bukan aktivitas, jadi tidak menyembunyikan jam.
- **Hover**: saat kursor berada di atas pulau compact, pulau memanjang 16 px (8 px di tiap sisi) dengan pegas, lalu kembali ke ukuran semula saat kursor pergi. Ini tanda bahwa pulau bisa diklik. Gelembung aktivitas dan titik kamera/mic ikut bergeser. Hover tidak berlaku saat pulau sedang wide atau mengambang.

#### Cuplikan clipboard

Setiap kali isi clipboard berubah, slot pulau menampilkan cuplikannya selama 3 detik. Pulau tidak terbuka; ia tetap compact dengan lebar yang mengikuti isi cuplikan (minimal 200 px), dan aktivitas yang sedang berjalan pindah sementara ke gelembung di kanan pulau.

| Isi clipboard | Di slot |
|---|---|
| Teks | Ikon clipboard + `"rapat jam 3 di ruang Merapi, bawa…"` (baris pertama, maks. 40 karakter) |
| Gambar | **Thumbnail gambar yang dicopy** (tinggi 20 px, rasio dipertahankan, sudut membulat) + **nama file** (maks. 32 karakter) + `1920 × 1080` |
| File (copy) | Ikon file + `3 files`; kalau hanya satu file, namanya (maks. 32 karakter). Satu file gambar tampil seperti baris Gambar, dengan thumbnail |
| File (cut, Ctrl+X) | Ikon gunting + `3 files`; dibedakan dari copy lewat format clipboard `Preferred DropEffect` |
| Isi sensitif | Ikon gembok + `Copied (hidden)` |

- **Nama file gambar** diambil berurutan dari: (1) path file, kalau file gambar dicopy dari Explorer; (2) nama di URL gambar, kalau "Copy image" dari browser (dibaca dari format clipboard `HTML Format` atau `UniformResourceLocatorW`); (3) `Screenshot`, kalau sumbernya Snipping Tool atau Print Screen; (4) `Image` kalau tidak diketahui.
- Tooltip slot menampilkan detail lengkap: teks sampai 120 karakter, nama file lengkap, atau daftar nama file.
- Klik slot membuka riwayat clipboard di jendela Tools.
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

TopBar menampilkan status sesi Claude Code: tab **Code** di Claude Desktop dan `claude` di terminal (termasuk wmux). Tab Chat Claude Desktop tidak didukung karena tidak mengirim sinyal apa pun ke luar aplikasinya. Integrasi ini mati secara default dan dinyalakan di Settings (F-CLD-01); sumber datanya di 9.2.

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
- TopBar **tidak mengirim toast** untuk kejadian Claude, supaya tidak dobel dengan notifikasi Claude Desktop sendiri. Karena itu saat aplikasi fullscreen aktif, kartu Claude dilewati.

**Setelah selesai**
- Kalau jendela sesi itu sedang aktif saat Claude selesai, cukup tampilan `Done` 4 detik.
- Kalau tidak, setelah 4 detik muncul **gelembung hijau** berikon Claude di kanan pulau, paling kanan dari semua gelembung (kecuali aktivitas Claude sedang tampil; lihat titik hijau di atas). Gelembung ini bukan aktivitas, jadi jam dan tanggal tetap tampil.
- Gelembung hilang saat jendela sesi itu menjadi aktif, saat gelembung diklik (membawa jendela itu ke depan), atau saat prompt baru dikirim di sesi itu. Kalau lebih dari satu sesi selesai dan belum dilihat, gelembung diberi angka (`2`) dan klik membuka halaman Claude.
- Untuk sesi Claude Desktop, "jendela aktif" berarti jendela Claude Desktop di depan, karena TopBar tidak tahu percakapan mana yang sedang dilihat di dalamnya.
- Selesai tidak berbunyi.

**Jendela sesi**: TopBar menelusuri proses induk dari proses `claude` sebuah sesi sampai menemukan jendela pertama yang terlihat: Claude Desktop untuk sesi Desktop, atau jendela terminal (wmux, Windows Terminal) untuk sesi CLI. Membawa ke depan berarti memulihkan jendela kalau diminimize, lalu memfokuskannya. Membuka percakapan tertentu di dalam Claude Desktop di luar cakupan.

#### Indikator kamera dan mikrofon

- Saat kamera atau mikrofon dipakai aplikasi mana pun, **titik berwarna 8 px tanpa latar** muncul tepat di **kiri pulau**, berjarak 6 px, seperti titik privasi di iPhone. Kamera hijau (`#30D158`), mikrofon oranye (`#FF9F0A`).
- Saat compact, titik berjajar horizontal dan sejajar tengah dengan pulau; kalau keduanya aktif, titik kamera paling dekat ke pulau (jarak antar-titik 5 px).
- Setiap titik punya garis tepi gelap tipis (1,5 px, hitam 35%) supaya tetap terlihat di atas jendela berlatar terang. Area hover diperlebar 6 px di semua sisi.
- Titik muncul dengan pegas (skala 0 → 1) dan hilang dengan cara sebaliknya ≤ 2 detik setelah perangkat berhenti dipakai.
- Titik **tetap tampil di semua keadaan pulau**. Saat pulau mengambang, titik bergeser ke sisi kiri panel dan bersusun vertikal mulai sejajar menu halaman. Indikator privasi tidak boleh tersembunyi oleh pulau yang sedang terbuka.
- Hover menampilkan nama aplikasinya (`Camera in use: Zoom`, `Microphone in use: Discord`).
- Saat aplikasi fullscreen aktif, titik ikut tersembunyi bersama bar; indikator mikrofon bawaan Windows di system tray tetap ada.

#### Kejadian wide

| Kejadian | Isi | Lama |
|---|---|---|
| Caps Lock / Num Lock | `Caps Lock on` / `off` | 1,5 detik |
| Charger dicolok | Petir + `Charging · 64%` | 3 detik |
| Charger dicabut | `On battery · 63%` | 2 detik |
| Peringatan baterai | `Battery 20% · plug in`, `Battery 10%`, `80% reached · unplug`, `Fully charged` | 5 detik |
| Perangkat Bluetooth tersambung | Ikon jenis perangkat (biru) + nama (`Galaxy Buds2 Pro`) + `Connected` + baterai perangkat (`80%`) kalau perangkat melaporkannya | 4 detik |
| Perangkat Bluetooth terputus | Ikon abu-abu + nama + `Disconnected` | 2 detik |

Perubahan volume dan brightness tidak ditampilkan; flyout bawaan Windows sudah menanganinya.

**Bluetooth**: berlaku untuk perangkat audio (TWS, headphone, speaker), mouse, keyboard, controller, dan ponsel, dengan ikon sesuai jenisnya. Hanya kejadian sambung/putus yang ditampilkan, bukan perangkat yang sudah tersambung saat TopBar mulai. Kalau perangkat yang sama tersambung-putus berulang dalam 10 detik, hanya kejadian terakhir yang ditampilkan.

#### Prioritas tampilan

Kalau beberapa kejadian bersamaan, `IslandCoordinator` menampilkan yang paling tinggi dan menahan sisanya di antrean:

1. Alarm berbunyi / timer habis / fase pomodoro selesai / konflik file Explorer (kartu, tetap tampil sampai ditanggapi)
2. Pengingat todo (kartu selama 10 detik lalu menutup; toast-nya tetap di Notification Center) dan kartu Claude "Needs input" (8 detik lalu mengecil ke slot, tanpa toast)
3. Drop zone saat file diseret ke atas bar
4. Kejadian wide
5. Aktivitas berjalan (slot compact)
6. Jam dan tanggal saja

Pulau yang dibuka pengguna (expanded) berada di atas prioritas 4–6 dan di bawah 1–3. Alarm menggantikan halaman yang sedang terbuka, lalu halaman itu kembali setelah alarm ditanggapi. Saat aplikasi fullscreen aktif, hanya prioritas 1 yang muncul (sebagai overlay); prioritas 2 lewat toast saja (kartu Claude dilewati karena tidak punya toast), prioritas 3–6 dilewati.

#### Halaman expanded

**Menu halaman** (di semua halaman; dipilih user 2026-10-26, menggantikan header judul, tombol ‹ › dan titik halaman):
- Kapsul menu di bagian atas pulau, **selebar pulau**, berjarak **14 px dari tepi atas, kiri, dan kanan**, tinggi 36 px, radius penuh 18 px, latar `#1C1C1E`.
- **Sudut konsentris**: radius menu + jaraknya ke tepi = radius pulau (18 + 14 = 32 px), jadi lengkung menu sejajar dengan lengkung pulau dan celahnya sama rata di sisi maupun sudut. Sorotan tab mengikuti aturan yang sama di dalam menu (15 + 3 = 18 px). Karena itu pulau Notes juga memakai radius 32 px (bukan 36), supaya menu tidak bergeser saat pindah ke Notes.
- Berisi 9 tab dengan lebar sama, masing-masing **ikon + nama**: Media, Timer, Alarms, Calendar, Todo, Notes, Shelf, Claude, System. Teks 12 px abu-abu, ikon 15 px.
- Tab aktif: sorotan `#3A3A3C` yang bergeser ke tab yang dipilih, teks putih semibold, ikon hijau aksen (ikon Claude tetap oranye Claude).
- Saat musik diputar, ikon tab Media berganti menjadi bar level hijau yang bergerak, jadi musik tetap terlihat dari halaman mana pun.
- Di halaman Notes (960 px) menu ikut melebar; sorotan tetap pas dengan tabnya selama pulau berubah ukuran.
- Kalau layar terlalu sempit (pulau < 680 px), tab yang tidak aktif hanya menampilkan ikon.
- **Jam saat pulau terbuka**: baris kecil di tengah bawah pulau, jauh dari menu di atas: jam putih semibold + tanggal abu-abu (`19:38 Mon 26 Oct`), 12 px, digit tabular, 8 px dari tepi bawah. Klik membuka halaman Calendar (hover: latar `#1C1C1E`). Supaya baris ini tidak mengambil ruang halaman, pulau 20 px lebih tinggi (680 × 256 px; Notes 960 × 500 px).
- Nama bulan dan tahun tampil di atas grid halaman Calendar. Aksesibilitas: menu adalah `tablist`, tiap tab `tab` dengan `aria-selected`.

**Halaman** (urutan tetap):

| # | Halaman | Isi | Aksi |
|---|---|---|---|
| 1 | Media | **Musik**: cover, judul, artis, aplikasi sumber, progress bar dengan waktu. **Video**: hanya judul, aplikasi sumber, dan progress bar dengan waktu berjalan dan sisa waktu, tanpa gambar dan tanpa tombol | Musik: ⏮ ⏯ ⏭, klik progress bar untuk seek bila sumber mendukung |
| 2 | Timer | Pilihan mode Timer / Stopwatch / Pomodoro, angka besar, ring sisa waktu | Start, Pause, Reset, preset 10 dtk / 5 / 10 / 25 menit, fase pomodoro |
| 3 | Alarms | Alarm berikutnya dalam huruf besar ("Tomorrow 05:30"), tiga alarm dengan saklar | Nyala/mati alarm; "Manage alarms" membuka Tools |
| 4 | Calendar | Nama bulan dan tahun di atas grid, bulan berjalan, hari ini ditandai, titik pada tanggal yang punya todo/alarm; memilih tanggal menampilkan daftarnya di kanan (maksimal 2 baris, sisanya `+N more`) | Bulan sebelum/berikutnya; **Add task** untuk tanggal terpilih (satu-satunya tempat membuat todo) |
| 5 | Todo | Angka besar `4/5` + "Tasks done today", daftar hari ini (yang selesai dicoret dengan latar hijau gelap), scroll vertikal | Centang; **Add in Calendar** (membuka Calendar di hari ini dengan Add task terbuka); Edit membuka Tools |
| 6 | Notes | Pulau membesar ke sekitar 960 × 500 px. Atas: kolom cari dan tombol New note. Kiri: **grid kartu ala Google Keep** (lihat "Halaman Notes" di bawah). Kanan: editor teks yang **hanya muncul saat menambah atau mengedit** note | Buat, klik kartu untuk edit, cari, **seret kartu untuk mengatur urutan** (Alt + panah dari keyboard), hapus (klik dua kali untuk konfirmasi), Done |
| 7 | Shelf | Grid tetap 4 × 2 (maksimal 8 item, **tanpa scroll**). Tile ringkas: ikon jenis file dan nama satu baris; bundel menampilkan ikon tumpukan dengan jumlah file. Ringkasan jumlah file dan total ukuran di kiri | Seret tile keluar ke folder/aplikasi, hapus satu, Clear all |
| 8 | Claude | Judul "Claude" + "Sessions". Satu baris per sesi yang hidup, urut Needs input → Working → Done (belum dilihat) → Idle: ikon status, nama sesi, folder, dan waktu (berjalan, lama selesai, atau sejak kapan idle). Baris Needs input menampilkan cuplikan pertanyaan di baris kedua; sesi Idle redup; daftar bisa di-scroll vertikal. Tanpa sesi: `No Claude sessions`. Halaman hanya ada bila integrasi Claude menyala | Klik baris membawa jendela sesi ke depan dan menandai Done sebagai sudah dilihat |
| 9 | System | Grafik mini 60 detik untuk CPU, RAM, GPU, unduhan; suhu CPU; sisa ruang C: | Klik membuka Task Manager |

Titik penanda halaman di bawah tengah; halaman aktif berupa garis lebih panjang.

**Todo hari ini**: todo yang jatuh tempo hari ini, ditambah todo tanpa tanggal yang belum selesai, ditambah todo tanpa tanggal yang diselesaikan hari ini. Rasio dan ring = jumlah selesai / total daftar itu. Kalau daftar kosong tampil "Nothing due today" dan tombol Add in Calendar.

**Halaman Notes**
- **Ukuran**: pulau membesar ke sekitar 960 × 500 px (dibatasi lebar layar dikurangi 24 px), jauh lebih besar dari halaman lain, dengan pegas yang sama seperti membuka pulau. Muat 5 kolom kartu saat editor tertutup dan 3 kolom saat editor terbuka.
- **Grid kartu ala Google Keep**: setiap note jadi kartu (judul tebal = baris pertama, isi maksimal 8 baris, waktu terakhir diedit). Lebar kolom minimal 140 px dan jarak antar-kartu 8 px. Tinggi kartu mengikuti isinya, dan kartu ditempatkan berurutan ke kolom yang paling pendek (masonry), jadi susunannya rapat seperti Keep. Kalau note lebih banyak dari ruangnya, grid bisa di-scroll vertikal.
- **Editor hanya saat dibutuhkan**: saat tidak sedang menulis, grid memakai seluruh lebar. Klik kartu atau **New note** membuat editor (lebar 360 px) meluncur masuk dari kanan, grid menyempit, dan kartu-kartu bergeser ke posisi barunya dengan animasi. Kartu yang sedang diedit diberi garis hijau.
- **Menutup editor**: tombol **Done**, atau Esc di editor (Esc berikutnya menutup pulau). Grid kembali selebar penuh. Note baru yang ditutup dalam keadaan kosong dibuang otomatis, seperti di Keep.
- **Mengatur urutan**: seret kartu. Kartu terangkat (bayangan dan skala 3%) dan mengikuti pointer 1:1 dari titik pegang; saat melewati kartu lain, urutannya bertukar dan kartu lain bergeser dengan animasi; saat dilepas, kartu mendarat ke slotnya dengan pegas. Dari keyboard: fokus kartu lalu Alt + panah (kiri/atas = lebih awal, kanan/bawah = lebih akhir). Mengurutkan tidak bisa selama pencarian aktif.
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

**Menambah todo (hanya lewat Calendar)**
- Todo **hanya dibuat dari halaman Calendar**: pilih tanggal, lalu **Add task** di bawah daftar tanggal itu. Kolom input muncul di tempat; Enter menyimpan, Esc membatalkan.
- Halaman Todo tidak punya kolom input. Tombol **Add in Calendar** di sana menggeser pulau ke halaman Calendar dengan tanggal hari ini terpilih dan kolom Add task langsung terbuka dan fokus.
- **Umpan balik**: tugas untuk hari ini langsung muncul di daftar Todo dengan animasi. Tugas untuk tanggal lain tidak masuk daftar hari ini; pulau menampilkan konfirmasi singkat `Added to Tue 28 Oct`, dan tanggal itu di Calendar mendapat titik.
- Jam pengingat tetap diatur di Tools, supaya kolom di pulau tetap ringkas.

**Membuka pulau**
- Klik pulau compact. Halaman yang dibuka sesuai aktivitas di slot (Media, Timer); kalau tidak ada, halaman terakhir yang dilihat. Slot Claude punya aturan sendiri (lihat "Sesi Claude").
- Klik jam atau tanggal: halaman Calendar (sebelum Fase 2: halaman terakhir).
- Klik CPU/RAM/GPU/suhu/disk di kanan: halaman System (sebelum Fase 3: Task Manager).
- Ikon Timer / Todo di launcher: halaman Timer / Todo.
- Semua ikon bar yang membuka halaman pulau (launcher, Shelf, Claude, modul sistem di kanan) bekerja sebagai tombol buka/tutup: saat pulau terbuka, klik berpindah ke halamannya; klik saat halamannya sedang terbuka menutup pulau.
- Ikon Notes di launcher: halaman Notes dengan grid kartu (editor tertutup). `Ctrl+Alt+N`: halaman Notes dengan note baru kosong dan editor langsung terbuka dan fokus (Fase 4).
- `Ctrl+Alt+Space` (Fase 4).

**Pindah halaman**: scroll horizontal touchpad, roda mouse di area halaman (kecuali di atas daftar yang bisa di-scroll dan di seluruh halaman Notes, tempat roda mouse vertikal hanya men-scroll kartu), klik tab di menu halaman, atau tombol ← →.

**Menutup pulau**: klik di luar, Esc, `Ctrl+Alt+Space`, atau mouse berada di luar pulau selama 3 detik. Penutupan otomatis ditunda selama kolom teks atau editor note fokus. Note yang sedang ditulis disimpan sebelum pulau menutup.

#### Kartu notifikasi

| Kartu | Isi | Tombol |
|---|---|---|
| Alarm | Ikon alarm, jam, label | Snooze 5m, Stop |
| Timer habis | `Timer 10:00 done`, label | Repeat, Close |
| Pengingat todo | Judul tugas, waktu jatuh tempo | Snooze 10m, Done |
| Fokus selesai | `Focus session done`, `5-minute break · session 1 of 4` | Skip, Start break |
| Istirahat selesai | `Break over`, `Session 2 of 4 ready` | Stop, Start focus |
| Konflik file Explorer | `Replace or skip files`, `Project-Final.zip already exists in D:\Backup` | Skip, Replace |
| File sedang dipakai | `File in use`, `budget.xlsx is open in Excel` | Skip, Try again |
| Butuh izin admin | `Needs administrator permission`, nama folder tujuan | Open dialog |
| Claude butuh jawaban | `Claude needs your input`, `Claude needs permission`, atau `Plan ready for review`; nama sesi; cuplikan pertanyaan | Later, Open Claude (sesi terminal: Open terminal) |

Suara tiap kartu diatur di 6.5. Kalau alarm atau timer tidak ditanggapi dalam 5 menit, suara berhenti, kartu mengecil ke compact dengan tanda merah, dan toast "Missed alarm 14:30" tetap di Notification Center.

#### Shelf (tempat file sementara)

- Menyeret file (dari Explorer, desktop, atau aplikasi apa pun yang bisa drag file) ke atas bar atau pulau mengubah pulau menjadi drop zone. Melepas file menambahkannya ke Shelf lalu membuka halaman Shelf; menyeret menjauh tanpa melepas mengembalikan keadaan sebelumnya.
- **Satu kali drop = satu item.** Satu file menjadi tile file biasa; beberapa file yang dilepas bersamaan menjadi satu **bundel** (`12 files`) yang diseret keluar sekaligus. Hover tile menampilkan nama dan ukuran file di dalamnya.
- Maksimal **8 item**, cukup untuk grid 4 × 2 tanpa scroll. Kalau Shelf penuh, drop zone menampilkan `Shelf is full · drag something out first` dan file tidak ditambahkan.
- Shelf menyimpan **referensi** ke file asli, bukan salinan; tetap ada setelah restart.
- Menyeret tile keluar menawarkan Copy dan Move ke tujuan (Explorer yang memilih; Shift memaksa Move). Setelah Move berhasil, item keluar dari Shelf; setelah Copy, item tetap.
- File yang sudah tidak ada dibuang dari Shelf saat halaman Shelf dibuka dan setiap 1 menit. Bundel yang semua filenya hilang ikut terhapus.
- Folder yang dilepas masuk sebagai satu item.

### 6.4 Bagian kanan

**Gaya dua baris** (dipilih user 2026-10-26, mirip iStat Menus): tiap modul menampilkan label kecil di atas dan nilainya di bawah.

- Label: 8.5 px, semibold, huruf kapital, jarak huruf 0.06 em, warna `#9A9AA0`.
- Nilai: 11.5 px, semibold, digit tabular, warna teks utama.
- Jaringan memakai dua baris nilai tanpa label: upload di atas, download di bawah (9.5 px), lebar minimum 58 px supaya bar tidak bergeser saat angkanya berubah.
- Ikon (cuaca, Wi‑Fi) berada di kiri blok dua baris; ikon power berdiri sendiri.
- Tinggi area klik modul = tinggi bar − 6 px (minimal 24 px). Di tinggi bar terkecil (28 px) dua baris tetap muat.

Urutan default dari kiri ke kanan:

| Modul | Default | Tampilan (label / nilai) | Interaksi |
|---|---|---|---|
| Cuaca | Nyala | Ikon cuaca + `CLOUDY` / `29°` (label = kondisi singkat) | Hover: kondisi, tertinggi/terendah hari ini, kota |
| Jaringan | Nyala | `↑ 0.3 MB/s` / `↓ 2.1 MB/s` (satuan B/KB/MB otomatis) | Hover: adapter, total hari ini |
| CPU | Nyala | `CPU` / `12%` | Klik: halaman System |
| RAM | Nyala | `RAM` / `54%` | Hover: `8.6 / 16 GB`. Klik: halaman System |
| GPU | Mati | `GPU` / `4%` | Klik: halaman System |
| Suhu CPU | Mati | `TEMP` / `62°C`, nilai kuning ≥ 80°C, merah ≥ 90°C | Klik: halaman System |
| Disk | Mati | `C: FREE` / `120 GB` | Klik: halaman System |
| Wi‑Fi | Nyala | Ikon sinyal + kekuatan sinyal / nama jaringan (`STRONG` / `Home_5G`, nama maks. 16 karakter) | Klik: daftar jaringan Windows (`ms-availablenetworks:`) |
| Power | Selalu, paling kanan | Ikon power; titik oranye kecil di pojoknya kalau ada update Windows yang menunggu dipasang | Klik: menu power (di bawah) |

- Volume dan baterai tidak ditampilkan; taskbar Windows sudah memilikinya.
- Tiap modul bisa dinyalakan/dimatikan dan diurutkan di Settings. GPU, suhu, dan disk tetap tersedia di halaman System walaupun modulnya mati.
- Kalau modul tidak muat dalam aturan jarak 24 px, modul pindah ke menu "…" dengan urutan: Cuaca, Jaringan, Disk, Suhu, GPU, RAM, CPU. Wi‑Fi dan Power selalu tampil.
- Semua angka yang berubah memakai digit tabular supaya bar tidak bergoyang.

**Menu power**

Klik ikon power membuka menu kecil yang turun dari ikon (latar `#1C1C1E`, radius 12 px, bayangan lembut). Urutannya mengikuti Start menu Windows 11:

| Item | Kapan tampil | Aksi |
|---|---|---|
| Sleep | Selalu | Laptop tidur |
| Update and shut down | Hanya saat ada update yang menunggu | Pasang update, lalu matikan |
| Shut down | Selalu | Matikan |
| Update and restart | Hanya saat ada update yang menunggu | Pasang update, lalu restart |
| Restart | Selalu | Restart |

- Item update ditandai titik oranye, sama seperti titik di ikon power.
- Tidak ada konfirmasi tambahan; seperti Start menu, butuh dua klik (buka menu, pilih item). Kalau ada aplikasi dengan pekerjaan belum disimpan, Windows sendiri yang menampilkan layar "These apps are preventing shutdown".
- Menu menutup dengan klik di luar, Esc, atau saat pulau terbuka. Bisa dipakai dengan keyboard (panah atas/bawah, Enter).
- Saat pulau mengambang, ikon power ikut aturan elemen tertimpa (6.3).

### 6.5 Suara

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
| Fase pomodoro selesai | Bell | Sekali |
| Pengingat todo | Ping | Sekali; dibisukan selama fase fokus pomodoro (ikut F-TOOL-04) |
| Claude butuh jawaban | Ping | Sekali per pertanyaan; dibisukan selama fase fokus pomodoro |
| Kejadian lain (wide, Shelf, clipboard, charger, Claude bekerja atau selesai) | — | Tanpa suara |

**Volume**
- Suara TopBar mengikuti volume Windows. Settings punya **Alert volume** (0–100%, default 80%) yang hanya mengatur volume sesi audio TopBar, bukan volume sistem.
- **Gradually increase** (default nyala): alarm mulai di 30% dari Alert volume, lalu naik linear ke 100% dalam 30 detik. Tidak berlaku untuk timer.
- Tombol apa pun di kartu (Snooze, Stop, Repeat, Close, Done, Skip) menghentikan suara seketika dengan fade 100 ms.
- Settings dan editor alarm di Tools punya tombol putar untuk mendengar contoh tiap suara.

**Windows di-mute atau volumenya terlalu kecil**
- Sebelum berbunyi, TopBar memeriksa perangkat output default. Kalau di-mute atau volumenya di bawah 5%, kartu menampilkan baris peringatan kuning `Sound is off · Windows volume is muted` dan tombol tambahan **Unmute**.
- TopBar **tidak pernah** mengubah volume atau mute Windows tanpa tindakan pengguna. Unmute hanya terjadi saat pengguna menekan tombol itu, lalu suara alarm langsung mulai.
- Animasi kartu (pulse/shake) tetap jalan, jadi alarm tetap terlihat walaupun tanpa suara.

**Perangkat dan keadaan lain**
- Suara keluar di perangkat output default Windows. Kalau perangkat berganti saat berbunyi (misalnya headset dicabut), suara pindah ke perangkat default yang baru tanpa berhenti.
- Do Not Disturb Windows tidak membisukan alarm dan timer (seperti alarm di ponsel), karena suaranya diputar TopBar sendiri, bukan lewat toast.
- Saat aplikasi fullscreen aktif, alarm dan timer tetap berbunyi (prioritas 1).
- Kalau tidak ada perangkat audio sama sekali, kartu tetap muncul dan toast dikirim **dengan** suara notifikasi Windows sebagai cadangan.

## 7. Kebutuhan fungsional

ID dipakai untuk melacak di rencana implementasi. Kolom Fase merujuk ke bagian 14.

### 7.1 Bar dan sistem

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-BAR-01 | Bar terdaftar sebagai AppBar di monitor utama | Jendela maximize mulai tepat di bawah bar; taskbar Windows tidak berubah | 1 |
| F-BAR-02 | Bar sembunyi saat fullscreen | YouTube fullscreen dan game fullscreen tidak tertutup; bar kembali ≤ 1 detik setelah keluar | 1 |
| F-BAR-03 | Bar pulih dari kejadian sistem | Setelah Explorer restart, resolusi/DPI berubah, atau bangun dari sleep, bar kembali di posisi benar dan area kerja tetap berkurang 32 px | 1 |
| F-BAR-04 | Autostart saat login | Setelah restart, bar muncul ≤ 2 detik setelah desktop tampil; bisa dimatikan di Settings | 1 |
| F-BAR-05 | Hanya satu instance | Menjalankan app kedua kalinya hanya memfokuskan instance yang sudah jalan | 1 |
| F-BAR-06 | Material bar | Opacity dan tingkat kaca mengubah bar langsung; Transparency effects Windows mati memaksa bar polos; pulau tetap hitam pekat | 1 |
| F-BAR-07 | Jarak dan overflow | Di lebar 1366 px dan 1920 px, isi kiri dan kanan berjarak minimal 24 px dari pulau di semua keadaan pulau yang masih menampilkan bar | 1 |
| F-BAR-08 | Settings | Jendela Settings: jam 12/24, opacity bar, tingkat kaca, tinggi bar (28–44 px), tinggi pulau (20 px – tinggi bar − 4), modul kanan (nyala/urutan), ambang baterai, kota cuaca, hotkey, suhu CPU, autostart | 1 (jam, material, tinggi bar dan pulau, autostart), 3 (modul, baterai), 4 (cuaca, hotkey, suhu) |
| F-BAR-09 | Mode demo | "Demo mode" memutar skenario palsu (media, ngecas, notifikasi, copy, drop ke Shelf, dan mulai Fase 3 sesi Claude) untuk mengecek UI | 1 |

### 7.2 Bagian kiri

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-LEFT-01 | Aplikasi berjalan dan aplikasi aktif | Ikon asli semua aplikasi yang punya jendela di taskbar tampil setelah pemisah; hanya aplikasi aktif yang menampilkan nama; perpindahan nama dimulai ≤ 200 ms setelah fokus pindah dengan animasi 6.2; aplikasi yang dibuka/ditutup masuk/keluar dari deretan; klik ikon membawa aplikasi ke depan; overflow ke `+N`; berlaku untuk aplikasi desktop (.exe) dan Store (UWP/MSIX); ikon tajam di DPI 100–200% | 1 |
| F-LEFT-02 | Launcher | Timer, Todo, dan Notes membuka halamannya di pulau; saat pulau terbuka klik berpindah halaman tanpa menutup-membuka ulang; klik ikon halaman yang sedang terbuka menutup pulau | 3 |
| F-LEFT-03 | Riwayat clipboard | 50 teks terakhir tersimpan (tahan restart), bisa dicari, klik untuk menyalin ulang, bisa dihapus semua | 4 |
| F-LEFT-04 | Ikon Shelf | Selalu tampil tepat setelah launcher; jumlah file sesuai isi Shelf; memantul saat file baru masuk; klik membuka halaman Shelf | 3 |
| F-LEFT-05 | Ikon Claude | Tampil setelah Shelf saat integrasi Claude nyala; status (titik kuning, denyut, titik hijau) sesuai sesi Claude Code dalam ≤ 1 detik; klik membuka halaman Claude Sessions | 3 |

### 7.3 Pulau tengah

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-ISL-01 | Jam dan tanggal | Berganti tepat di pergantian menit; jam dan tanggal hanya tampil saat tidak ada aktivitas, hilang saat aktivitas pertama muncul, dan kembali saat aktivitas terakhir selesai; lebar pulau mengikuti isinya dengan minimal 200 px di semua keadaan compact dan wide | 1 |
| F-ISL-02 | Keadaan pulau dan gerak | Transisi compact ↔ wide ↔ expanded ↔ kartu ↔ drop zone sesuai 8.3; selama pulau mengambang latar bar transparan, isi bar tetap tampil, dan hanya elemen yang tertimpa panel (+8 px) yang memudar | 1 |
| F-ISL-03 | Prioritas dan antrean | Aturan di 6.3 terpenuhi; dites unit | 1 |
| F-ISL-04 | Musik | Spotify dan YouTube (Chrome/Edge) tampil di slot dan halaman Media; tombol kontrol berfungsi; judul berganti ≤ 1 detik setelah lagu ganti | 1 |
| F-ISL-05 | Video | Media Player, Movies & TV, dan video di browser tampil sebagai video (judul + progres saja); VLC sesuai tabel risiko | 1 |
| F-ISL-06 | Status ngecas | Colok/cabut tampil sebagai wide; slot menampilkan petir + persen selama dicas | 1 |
| F-ISL-07 | Halaman expanded | Menu tab ikon + nama selebar pulau, perpindahan halaman, dan aturan buka/tutup di 6.3; setiap halaman bisa dibuka dengan satu klik | 1 (kerangka + Media), 2 (Timer, Alarms, Calendar, Todo, Notes), 3 (Shelf, Claude, System) |
| F-ISL-08 | Kartu notifikasi | Kartu alarm, timer, todo, dan pomodoro muncul bersama toast Windows tanpa suara | 2 |
| F-ISL-10 | Caps/Num Lock | Menekan tombol menampilkan status baru | 3 |
| F-ISL-11 | Peringatan baterai | Muncul sekali per siklus di 20%, 10%, saat mencapai batas pengisian (default 80%), dan 100%; ambang bisa diubah | 3 |
| F-ISL-12 | Shelf | Perilaku di 6.3 "Shelf": drop zone, satu drop = satu item (bundel untuk banyak file), maksimal 8 item di grid 4 × 2 tanpa scroll, pesan saat penuh, referensi, seret keluar dengan Copy/Move, pembersihan file yang hilang, tersimpan setelah restart | 3 |
| F-ISL-14 | Titik kamera/mic | Titik muncul di kiri pulau ≤ 2 detik setelah kamera/mic mulai dipakai dan hilang ≤ 2 detik setelah berhenti; tetap tampil saat pulau mengambang; terlihat di atas latar gelap maupun terang; tidak pernah tumpang-tindih dengan isi bagian kiri bar | 3 |
| F-ISL-15 | Progres operasi file Explorer | Copy dan move file > 1 GB, serta delete > 1.000 item (ke Recycle Bin maupun permanen), tampil di slot pulau sesuai tabel di 6.3; pulau tetap compact dan tidak pernah terbuka atau mengambang karena operasi file; hasil akhir tampil 2 detik; batal menampilkan "Cancelled"; konflik file membuka kartu di pulau (Replace/Skip, Try again/Skip, atau Open dialog) dan pilihannya diteruskan ke dialog Explorer; klik slot membawa dialog Explorer ke depan | 4 |
| F-ISL-16 | Todo dibuat lewat Calendar | Todo hanya bisa dibuat dari Add task di halaman Calendar, dengan tanggal yang dipilih; Add in Calendar di halaman Todo membuka Calendar di hari ini dengan kolom terbuka; tugas hari ini muncul di daftar Todo, tugas tanggal lain memunculkan `Added to …` dan titik di Calendar; semua bisa dilakukan dengan keyboard | 2 |
| F-ISL-18 | Bluetooth tersambung/terputus | Menyambungkan earbuds atau perangkat Bluetooth lain menampilkan wide dengan ikon jenis, nama, `Connected`, dan baterai bila tersedia ≤ 2 detik setelah Windows menyatakan tersambung; memutus menampilkan `Disconnected`; perangkat yang sudah tersambung saat start tidak memicu apa pun | 3 |
| F-ISL-17 | Cuplikan clipboard di pulau | Teks, gambar, file (copy dan cut), dan isi sensitif tampil 3 detik di slot pulau sesuai tabel 6.3; gambar tampil sebagai thumbnail asli dengan nama file dan dimensinya; pulau tidak terbuka; isi sensitif disembunyikan | 1 |

F-ISL-09 dan F-ISL-13 (OSD brightness dan menyembunyikan OSD brightness Windows) dihapus karena Windows sudah menampilkan brightness sendiri; nomornya tidak dipakai ulang.

### 7.4 Bagian kanan

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-RIGHT-01 | CPU, RAM, jaringan | Diperbarui tiap 1 detik; selisih dengan Task Manager ≤ 5 poin persen | 1 |
| F-RIGHT-02 | Wi‑Fi | Nama jaringan tampil; tanpa izin Location tampil ikon saja + tooltip cara memberi izin | 3 |
| F-RIGHT-03 | Modul GPU, suhu, disk | Default mati; saat nyala, selisih dengan Task Manager ≤ 5 poin | 3 |
| F-RIGHT-04 | Atur modul dan overflow | Modul bisa dinyalakan/dimatikan dan diurutkan; overflow mengikuti urutan di 6.4 | 3 |
| F-RIGHT-05 | Cuaca | Suhu dan ikon dari Open-Meteo untuk kota di Settings; diperbarui tiap 30 menit; offline menampilkan nilai terakhir berwarna abu-abu | 4 |
| F-RIGHT-06 | Sumber suhu CPU | Tampil bila helper sensor jalan; sembunyi bila tidak bisa membaca nilai | 4 |
| F-RIGHT-07 | Menu power | Ikon power selalu paling kanan; Sleep, Shut down, dan Restart bekerja seperti di Start menu (aplikasi yang belum disimpan tetap ditanyakan oleh Windows); menu bisa dipakai dengan keyboard | 3 |
| F-RIGHT-08 | Update and shut down / Update and restart | Kedua item dan titik oranye hanya muncul saat Windows punya update yang menunggu, cocok dengan yang ditampilkan Start menu; memilihnya memasang update lalu mematikan/restart | 3 |

### 7.5 Tools dan produktivitas

Jendela Tools: jendela gelap biasa (bisa dipindah dan diubah ukurannya) dengan tab di kiri: Timer, Stopwatch, Pomodoro, Alarms, Todo, Clipboard. Note tidak ada di Tools; menulis dan mengatur note dilakukan di halaman Notes pulau.

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-TOOL-01 | Timer | Beberapa timer berlabel bisa jalan bersamaan; jeda/lanjut/reset; tetap benar setelah app atau laptop restart | 2 |
| F-TOOL-02 | Stopwatch | Start/pause/reset/lap; tetap benar setelah app restart | 2 |
| F-TOOL-03 | Pomodoro | Default 25 menit fokus / 5 menit istirahat / 15 menit istirahat panjang tiap 4 sesi; bisa diubah | 2 |
| F-TOOL-04 | Mode fokus | Selama fase fokus, pulau prioritas 2 dan 4 ditahan sampai istirahat (alarm dan timer tetap muncul), dan app mencoba menyalakan Do Not Disturb Windows | 2 (pulau), 4 (DND Windows) |
| F-TOOL-05 | Alarm | Jam, label, hari berulang atau sekali, nyala/mati, snooze 5 menit; alarm saat sleep membangunkan laptop bila wake timer diizinkan; alarm terlewat berbunyi saat bangun dengan label "Missed" | 2 |
| F-TOOL-06 | Todo | Judul, tanggal jatuh tempo opsional, jam pengingat opsional, selesai, urutan manual (seret), filter Today / All / Done | 2 |
| F-TOOL-07 | Pengingat todo | Saat jam pengingat tiba, kartu + toast muncul; "Snooze 10m" menggeser pengingat | 2 |
| F-TOOL-08 | Note di pulau | Halaman Notes sesuai "Halaman Notes" di 6.3: pulau 960 × 500 px; grid kartu masonry ala Google Keep dalam urutan pengguna (note baru di atas; mengedit tidak mengubah urutan); editor di kanan hanya muncul saat menambah/mengedit dan tertutup lewat Done atau Esc; note baru yang kosong dibuang saat ditutup; 8 pilihan warna per note yang tersimpan dan tampil di kartu serta area teks; seret kartu untuk mengatur urutan (dan Alt + panah); tersimpan 0,5 detik setelah berhenti mengetik dan saat pulau menutup; cari; hapus dengan konfirmasi; pulau tidak menutup otomatis selama editor fokus atau kartu sedang diseret | 2 |
| F-TOOL-09 | Ketahanan data | Mematikan paksa laptop saat mengetik note kehilangan paling banyak 1 detik ketikan terakhir | 2 |
| F-TOOL-10 | Hotkey global dan quick capture | `Ctrl+Alt+Space` buka/tutup pulau; `Ctrl+Alt+V` riwayat clipboard; `Ctrl+Alt+N` halaman Notes dengan note baru dan editor fokus; `Ctrl+Alt+T` halaman Calendar di hari ini dengan kolom Add task terbuka dan fokus (Enter simpan, Esc batal) | 4 |
| F-TOOL-11 | Hotkey bisa diubah | Semua hotkey bisa diganti di Settings; bentrok ditampilkan "Used by another app" | 4 |

### 7.6 Suara

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-SND-01 | Suara bawaan | Lima suara (Radar, Chime, Bell, Soft, Ping) ada di dalam aplikasi tanpa file eksternal; semuanya bisa didengar contohnya di Settings | 2 |
| F-SND-02 | Suara alarm dan timer | Alarm memakai suara pilihan per alarm, timer memakai suara di Settings; berulang sampai ditanggapi; berhenti otomatis setelah 5 menit | 2 |
| F-SND-03 | Bunyi sekali | Fase pomodoro selesai berbunyi Bell sekali; pengingat todo berbunyi Ping sekali, kecuali selama fase fokus | 2 |
| F-SND-04 | Volume | Alert volume hanya mengubah volume sesi TopBar; Gradually increase menaikkan alarm dari 30% ke 100% dalam 30 detik (±1 detik); tombol kartu menghentikan suara ≤ 150 ms | 2 |
| F-SND-05 | Peringatan mute | Saat Windows di-mute atau volume < 5%, kartu alarm/timer menampilkan peringatan dan tombol Unmute; volume sistem tidak berubah tanpa klik pengguna | 2 |
| F-SND-06 | Ganti perangkat | Mencabut headset saat alarm berbunyi memindahkan suara ke speaker dalam ≤ 1 detik tanpa menghentikan alarm | 2 |

### 7.7 Integrasi Claude

| ID | Kebutuhan | Kriteria terima | Fase |
|---|---|---|---|
| F-CLD-01 | Pasang dan lepas hook | Saklar `Show Claude Code sessions` di Settings (default mati) menambahkan hook TopBar ke `~/.claude/settings.json` setelah membuat cadangan `settings.json.topbar-backup`; hook lain (misalnya wmux) tetap utuh; memasang dua kali tidak menggandakan entri; mematikan saklar hanya menghapus entri TopBar; file yang bukan JSON valid tidak diubah; baris status di Settings sesuai bagian 11 | 3 |
| F-CLD-02 | Status di slot | Working, Needs input, dan Done tampil sesuai "Sesi Claude" di 6.3 ≤ 1 detik setelah kejadiannya; beberapa sesi digabung jadi satu aktivitas; Esc di Claude mengembalikan sesi ke Idle tanpa Done; sesi yang prosesnya mati hilang ≤ 5 detik | 3 |
| F-CLD-03 | Kartu Needs input | Pertanyaan, persetujuan rencana, dan permintaan izin (bila hook-nya terpicu, lihat bagian 12) membuka kartu dengan Ping sekali; kartu mengecil ke slot kuning setelah 8 detik; slot kembali ke Working ≤ 1 detik setelah dijawab; kartu dan Ping ditahan selama fase fokus; tanpa toast | 3 |
| F-CLD-04 | Selesai dan belum dilihat | Done tampil 4 detik; gelembung hijau muncul hanya bila jendela sesi tidak aktif saat selesai, dan hilang saat jendela itu aktif, gelembung diklik, atau prompt baru dikirim di sesi itu | 3 |
| F-CLD-05 | Halaman Claude | Halaman 8 mendaftar semua sesi yang hidup dengan urutan dan isi sesuai 6.3; klik baris membawa jendela sesi ke depan | 3 |
| F-CLD-06 | Membawa jendela sesi ke depan | Sesi Claude Desktop membuka jendela Claude Desktop; sesi CLI di wmux dan Windows Terminal membuka jendela terminalnya; jendela yang diminimize dipulihkan | 3 |
| F-CLD-07 | Hook tidak memperlambat Claude | `TopBar.ClaudeHook` selesai ≤ 50 ms (p95) saat TopBar jalan dan ≤ 100 ms saat TopBar mati; tidak pernah menulis ke stdout; selalu keluar dengan kode 0 | 3 |

## 8. Kebutuhan non-fungsional

### 8.1 Performa
- RAM (private working set) < 120 MB setelah 8 jam.
- CPU rata-rata < 1% saat diam (tanpa animasi), < 5% saat animasi.
- Polling dijeda saat bar tersembunyi (fullscreen), layar mati, atau sleep.
- Bar muncul ≤ 2 detik setelah login.
- Helper hook Claude selesai ≤ 50 ms (p95), jadi Claude tidak terasa lebih lambat walau hook dipanggil di setiap tool.

### 8.2 Keandalan dan privasi
- Gagalnya satu provider tidak mematikan bar (bagian 11).
- Semua data disimpan lokal. Satu-satunya akses internet adalah API cuaca Open-Meteo (tanpa API key; hanya mengirim koordinat kota).
- Isi clipboard sensitif tidak ditampilkan dan tidak disimpan (6.3). Thumbnail gambar clipboard hanya di memori, tidak ditulis ke disk.
- Data sesi Claude (nama sesi, folder, cuplikan pertanyaan) hanya ada di memori, tidak ditulis ke database atau log. Teks prompt pengguna tidak dikirim ke TopBar. TopBar hanya mengubah `~/.claude/settings.json` saat pengguna menyalakan atau mematikan integrasi.

### 8.3 Visual dan gerak

**Warna**

| Token | Nilai | Pemakaian |
|---|---|---|
| `bar-tint` | `#161618` opacity 78% (bisa diatur) di atas blur | Latar bar |
| `island` | `#000000` | Pulau di semua keadaan |
| `surface` | `#1C1C1E` | Baris, tile, kolom input di dalam pulau |
| `control` | `#2C2C2E` / hover `#3A3A3C` | Tombol |
| `text` | `#F5F5F7` | Teks utama |
| `text-2` | `#8E8E93` | Teks sekunder (kontras 6,6:1 di atas hitam) |
| `accent` | `#30D158` | Centang, ikon tab aktif, bar level musik di tab Media, hari ini di Calendar |
| `done-bg` | `#0F2A17` | Baris todo selesai |
| `warn` | `#FFD60A` | Suhu ≥ 80°C, baterai 20%, Claude Needs input |
| `claude` | `#D97757` | Ikon Claude saat Working |
| `danger` | `#FF453A` | Suhu ≥ 90°C, baterai 10%, alarm terlewat |
| `cam` / `mic` | `#30D158` / `#FF9F0A` | Indikator privasi |

**Tipografi**: Segoe UI Variable (font sistem Windows 11). Teks bar 12 px dengan tracking +0,01em, kecuali modul kanan yang memakai gaya dua baris (label 8.5 px kapital, nilai 11.5 px; lihat 6.4); judul di pulau 19 px semibold −0,01em; angka besar 44–46 px tebal −0,02em; digit tabular untuk semua angka yang berubah.

**Bentuk**: kapsul compact radius setengah tinggi pulau (12 px pada default 24 px), lebar minimal 200 px; panel expanded radius 32 px (termasuk Notes); elemen yang menempel di tepi panel memakai radius konsentris (radius dalam = 32 − jarak ke tepi, contoh menu halaman 18 px pada jarak 14 px); kartu 28 px; drop zone 30 px; baris dan tile 12 px; gelembung aktivitas 24 px bulat penuh; titik kamera/mic 8 px.

**Gerak** (pegas dijelaskan dengan damping ratio dan response, mengikuti panduan gerak Apple):

| Transisi | Pegas / waktu |
|---|---|
| Compact → expanded, kartu, drop zone | Pegas, damping 0,82, response 0,42 dtk (sedikit memantul seperti Dynamic Island) |
| Mengambang → compact | Pegas, damping 1,0, response 0,32 dtk (tanpa memantul) |
| Compact ↔ wide | Pegas, damping 0,85, response 0,30 dtk |
| Hover pulau compact (masuk/keluar) | Lebar +16 px, pegas damping 0,8, response 0,30 dtk |
| Pindah aplikasi aktif di bagian kiri | Nama lama menyusut dan nama baru melebar 300 ms ease-out (fade masuk mulai 90 ms); kapsul latar pindah bersamaan; aplikasi baru masuk dengan pegas skala 0,6 → 1 (320 ms) |
| Tinggi bar atau pulau diubah di Settings | Pegas compact ↔ wide |
| Latar bar transparan/kembali saat pulau mengambang/menutup | Warna, blur, dan garis bawah 220 ms, mulai bersamaan dengan pulau |
| Elemen bar yang tertimpa panel | Opacity 160 ms keluar dan masuk |
| Isi pulau muncul | 220 ms: opacity + blur 6 → 0 px + skala 0,98 → 1, mulai 120 ms setelah pulau mulai membesar |
| Pindah halaman | Geser horizontal 300 ms, ease-out eksponensial; sorotan tab menu bergeser ke tab baru 320 ms ease-out bersamaan |
| Ukuran pulau saat masuk/keluar Notes | Pegas yang sama dengan membuka pulau |
| Editor Notes muncul/hilang | Geser 24 px dari kanan + opacity, 320 ms ease-out; grid menyempit/melebar dan kartu pindah posisi 320 ms ease-out |
| Kartu note yang diseret | Mengikuti pointer 1:1 dari titik pegang, terangkat dengan bayangan dan skala 3%; kartu lain bergeser 320 ms; saat dilepas mendarat dengan pegas 340 ms |
| Isi slot berganti (cuplikan clipboard, progres file, aktivitas lain), atau jam/tanggal hilang dan muncul | Lebar pulau dan rentang isi aktivitas mengikuti dengan pegas compact ↔ wide; isi slot fade 180 ms |
| Titik kamera/mic dan gelembung aktivitas muncul/hilang | Skala 0 → 1 dengan pegas 320 ms; posisinya ikut bergeser dengan pegas yang sama seperti pulau saat pulau berubah ukuran |
| Ikon Claude saat Working | Opacity 0,55 ↔ 1, 1,6 detik ease-in-out berulang; diam di opacity 1 bila Animation effects mati |

Semua transisi pulau bisa disela: keadaan baru mulai dari ukuran yang sedang tampil di layar, bukan dari target sebelumnya. Kalau "Animation effects" Windows mati, semua transisi menjadi cross-fade 100 ms.

### 8.4 Aksesibilitas
- Semua kontrol di pulau dan Tools bisa dioperasikan dengan keyboard; Esc selalu menutup pulau.
- Kontras teks minimal 4,5:1.
- Teks dan ikon ikut skala DPI 100–200%.
- Mengatur urutan note punya jalur keyboard (Alt + panah pada kartu yang fokus); Enter membuka kartu di editor.

## 9. Arsitektur teknis

### 9.1 Proyek

| Proyek | Isi | Bergantung pada |
|---|---|---|
| `TopBar.App` | WPF + CommunityToolkit.Mvvm: `BarWindow` (AppBar), `IslandWindow` (jendela transparan topmost untuk keadaan mengambang), `ToolsWindow`, `SettingsWindow`, ViewModel | Core, Platform, Data |
| `TopBar.Core` | `IslandCoordinator`, `Scheduler`, `ShelfService`, model, format angka/tanggal. Tanpa WPF dan Win32 | — |
| `TopBar.Platform` | Provider Windows (9.2), `AppBarService`, `BarMaterial`, `HotkeyService`, `NotificationService`, helper drag-and-drop | Core |
| `TopBar.Data` | Repository SQLite (Microsoft.Data.Sqlite), `SettingsStore` | Core |
| `TopBar.SensorHelper` | Proses kecil dengan hak admin; membaca suhu CPU via LibreHardwareMonitorLib, kirim lewat named pipe tiap 2 detik | — |
| `TopBar.ClaudeHook` | Console app kecil (NativeAOT, mulai ≤ 20 ms, tanpa hak admin) yang dipanggil hook Claude Code: membaca JSON hook dari stdin, mengambil field yang dibutuhkan, mengirimnya ke named pipe `TopBar.Claude` dengan timeout 100 ms, lalu keluar. Tidak pernah menulis ke stdout | — |
| `TopBar.Core.Tests` | Unit test xUnit | Core, Data |

`IslandWindow` terpisah dari `BarWindow` supaya pulau mengambang tidak mengubah ruang AppBar dan jendela lain tidak bergeser saat pulau terbuka.

App utama jalan **tanpa** hak admin. Ini penting untuk Shelf: Windows memblokir drag-and-drop dari proses biasa ke proses admin, jadi hanya helper sensor yang jalan sebagai admin.

### 9.2 Provider

Setiap provider punya interface di Core dan implementasi di Platform; mode demo memakai implementasi palsu.

| Provider | Sumber di Windows | Cara |
|---|---|---|
| Media | `GlobalSystemMediaTransportControlsSessionManager` (WinRT); musik vs video dari `PlaybackType` sesi, cadangan daftar aplikasi video yang dikenal; progres dari `GetTimelineProperties` | Event |
| Cadangan VLC | Judul jendela `vlc.exe` untuk judul video bila VLC tidak mengirim sesi media | Tiap 2 detik selama VLC jalan |
| Clipboard | `AddClipboardFormatListener`; gambar diubah jadi thumbnail kecil di memori | Event |
| Operasi file Explorer | UI Automation pada dialog progres Explorer (`OperationStatusWindow`). Jenis operasi (copy, move, delete, delete permanen), nama item, dan persen dibaca dari teks dan progress bar dialog; dialog konflik dikenali dari tombol Replace/Skip/Try again, dan pilihan dari kartu pulau ditekan lewat `InvokePattern` pada tombol itu | Event + baca tiap 500 ms saat aktif |
| Aplikasi berjalan dan aktif | Daftar aplikasi dari jendela yang tampil di taskbar: `EnumWindows` + `SetWinEventHook(EVENT_OBJECT_SHOW/HIDE/DESTROY)`, disaring seperti taskbar (terlihat, tanpa owner atau `WS_EX_APPWINDOW`, bukan `WS_EX_TOOLWINDOW`, tidak `DWMWA_CLOAKED`), dikelompokkan per AppUserModelID/proses. Aplikasi aktif: `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` → `GetWindowThreadProcessId` → `QueryFullProcessImageName`. Ikon dari .exe lewat `SHGetFileInfo` / `IShellItemImageFactory` (ukuran sesuai DPI); aplikasi Store dikenali dari `AppUserModelID` dan memakai logo paketnya (`Package.Logo`). Nama dari `FileDescription` versi file atau nama tampilan paket. Ikon di-cache per path atau AppUserModelID | Event |
| Daya | `PowerManager` / `SystemInformation.PowerStatus`, `WM_POWERBROADCAST` (hanya untuk kejadian charger dan peringatan baterai) | Event |
| Caps/Num Lock | Low-level keyboard hook + `GetKeyState` | Event |
| CPU, GPU, disk | Performance counter (PDH): `Processor Information`, `GPU Engine`, `LogicalDisk` | Tiap 1 detik |
| RAM | `GlobalMemoryStatusEx` | Tiap 1 detik |
| Jaringan | `NetworkInterface.GetIPv4Statistics` (selisih byte) | Tiap 1 detik |
| Wi‑Fi | Native WLAN API (`WlanQueryInterface`) | Tiap 10 detik + event koneksi |
| Kamera/mic | Registry `...\CapabilityAccessManager\ConsentStore\{webcam,microphone}` (`LastUsedTimeStop = 0` berarti sedang dipakai) | Tiap 2 detik |
| Suhu CPU | Named pipe dari `TopBar.SensorHelper` | Tiap 2 detik |
| Cuaca | Open-Meteo forecast + geocoding API (HTTP) | Tiap 30 menit, di-cache |
| Shelf | OLE drag-and-drop di `BarWindow` dan `IslandWindow` (`DataFormats.FileDrop` masuk, `DoDragDrop` dengan Copy dan Move keluar); cek keberadaan file | Event + tiap 60 detik |
| Hotkey | `RegisterHotKey` | Event |
| Bluetooth | `DeviceWatcher` (Windows.Devices.Enumeration) untuk perangkat Bluetooth Classic dan LE dengan properti `System.Devices.Aep.IsConnected`; jenis dari `ClassOfDevice` / kategori perangkat; baterai dari properti baterai Bluetooth perangkat atau GATT Battery Service bila tersedia (best-effort) | Event |
| Power | Sleep: `SetSuspendState(FALSE, FALSE, FALSE)` (PowrProf). Shut down/Restart: `InitiateShutdownW` dengan `SHUTDOWN_POWEROFF` / `SHUTDOWN_RESTART` (tanpa memaksa aplikasi lain), ditambah `SHUTDOWN_INSTALL_UPDATES` untuk varian update. Update yang menunggu dideteksi dari Windows Update Agent (`ISystemInformation.RebootRequired`) dan registry `...\WindowsUpdate\Auto Update\RebootRequired` | Saat menu dibuka + tiap 30 menit |
| Suara | NAudio: `WasapiOut` (shared mode) di perangkat default, `VolumeSampleProvider` untuk Alert volume dan kurva naik bertahap; `IAudioEndpointVolume` untuk cek mute/volume sistem; `IMMNotificationClient` untuk pergantian perangkat default | Saat dibutuhkan + event perangkat |
| Notifikasi | Toast via Microsoft.Toolkit.Uwp.Notifications (butuh shortcut Start Menu dengan AUMID, dibuat saat run pertama) | — |
| Material bar | `DwmSetWindowAttribute(DWMWA_SYSTEMBACKDROP_TYPE)` untuk acrylic, blur-behind untuk "Light", tint polos untuk "Off"; membaca `UISettings.AdvancedEffectsEnabled` | Saat berubah |
| Sesi Claude | Hook Claude Code → `TopBar.ClaudeHook` → named pipe `\\.\pipe\TopBar.Claude` (tabel di bawah). Pelengkap: file `~/.claude/sessions/<pid>.json` (judul sesi, status `busy`/`idle`, PID) dipantau dengan `FileSystemWatcher`. Jendela sesi dicari dari rantai proses induk (Toolhelp snapshot) lalu dipulihkan dan difokuskan (`ShowWindow` + `SetForegroundWindow`). Pembaruan UI digabung, maksimal 4 kali per detik | Event + cek proses hidup tiap 5 detik selama ada sesi |

**Hook Claude Code**

Semua hook memanggil `"<folder instalasi>/TopBar.ClaudeHook.exe" <NamaEvent>` dengan timeout 2 detik. Path ditulis dengan garis miring maju, seperti hook wmux yang sudah ada, supaya aman di shell yang dipakai Claude Code untuk menjalankan hook di Windows.

| Hook | Matcher | Dikirim ke TopBar | Efek |
|---|---|---|---|
| `SessionStart` | — | `session_id`, `cwd` | Sesi masuk daftar sebagai Idle |
| `UserPromptSubmit` | — | `session_id`, waktu (teks prompt tidak dikirim) | Working; waktu mulai dicatat; gelembung "belum dilihat" sesi itu hilang |
| `PreToolUse` | `AskUserQuestion\|ExitPlanMode` | `session_id`, nama tool, teks pertanyaan pertama (maks. 200 karakter) | Needs input (pertanyaan atau rencana) |
| `Notification` | — | `session_id`, `notification_type`, `message` | `permission_prompt` → Needs input (izin); jenis lain diabaikan |
| `PostToolUse` | `*` | `session_id` | Needs input → Working |
| `Stop` | — | `session_id` | Done |
| `SessionEnd` | — | `session_id` | Sesi dihapus dari daftar |

- Izin yang ditolak tidak memicu `PostToolUse`; status kembali ke Working saat tool berikutnya selesai, atau langsung Done saat Claude berhenti.
- Esc di Claude tidak memicu `Stop`. Kalau file sesi berubah ke `idle` sementara TopBar masih mencatat Working atau Needs input, sesi menjadi Idle tanpa Done.
- Sesi yang prosesnya sudah tidak ada dihapus walau `SessionEnd` tidak terkirim (misalnya Claude Desktop ditutup paksa).

### 9.3 Alur data

Provider mengirim snapshot ke state di Core. `IslandCoordinator` menentukan apa yang tampil di pulau. ViewModel berlangganan state dan memperbarui UI lewat dispatcher WPF. `Scheduler` menyimpan setiap jadwal sebagai waktu absolut (UTC) di database, lalu menghitung ulang semuanya saat app mulai, saat bangun dari sleep, dan saat jam atau zona waktu sistem berubah.

### 9.4 Instalasi dan autostart

- Build `dotnet publish` framework-dependent (.NET 10 Desktop Runtime terpasang), disalin ke `%LOCALAPPDATA%\Programs\TopBar\`.
- Run pertama: membuat shortcut Start Menu (untuk toast) dan entri autostart di `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Menyalakan suhu CPU di Settings: satu prompt UAC untuk mendaftarkan `TopBar.SensorHelper` di Task Scheduler (jalan saat login, "highest privileges").
- Menyalakan `Show Claude Code sessions` di Settings: TopBar menulis hook ke `~/.claude/settings.json` dengan path absolut `TopBar.ClaudeHook.exe` di folder instalasi; tanpa UAC. Hook berlaku untuk sesi Claude yang dimulai sesudahnya, dan Settings menuliskan `Applies to Claude sessions started from now on`.
- Baris status integrasi di Settings: `Waiting for the first Claude session`, `Connected · last event 2 min ago`, atau salah satu pesan error di bagian 11.

## 10. Data dan penyimpanan

Lokasi: `%LOCALAPPDATA%\TopBar\` berisi `topbar.db` (SQLite, mode WAL), `settings.json`, `backups\`, `logs\`.

| Tabel | Kolom utama |
|---|---|
| `notes` | `id`, `body`, `color` (0–7, default 0), `sort_order`, `created_at`, `updated_at` |
| `todos` | `id`, `title`, `due_date` (opsional), `remind_at` (opsional), `done_at` (opsional), `sort_order`, `created_at` |
| `alarms` | `id`, `time_of_day`, `repeat_days` (bitmask, 0 = sekali), `label`, `sound` (default `radar`), `enabled`, `next_fire_at` |
| `timers` | `id`, `kind` (timer/pomodoro), `label`, `duration_s`, `ends_at` (saat jalan), `remaining_s` (saat dijeda), `pomodoro_phase`, `pomodoro_round` |
| `stopwatch` | `id`, `started_at`, `accumulated_ms`, `laps` (JSON) |
| `clipboard_history` | `id`, `text`, `source_app`, `copied_at` (dibatasi 50 baris) |
| `shelf_items` | `id`, `paths` (JSON, satu path atau banyak untuk bundel), `display_name`, `size_bytes`, `sort_order`, `added_at` (dibatasi 8 baris) |

- Database dibackup sekali sehari ke `backups\`, disimpan 7 salinan.
- Migrasi skema lewat tabel `schema_version`.
- `settings.json` berisi: format jam, opacity bar, tingkat kaca, tinggi bar, tinggi pulau, modul kanan (id, nyala, urutan), ambang baterai, kota cuaca (nama, lat, lon), hotkey, suhu CPU nyala/mati, integrasi Claude nyala/mati, autostart, suara timer, Alert volume, Gradually increase.

## 11. Penanganan error

| Kejadian | Penanganan |
|---|---|
| Provider melempar exception atau sumber data hilang | Modul menampilkan "—" dengan alasan di tooltip; dicoba lagi setelah 5 detik, 30 detik, lalu tiap 2 menit |
| Exception tak tertangani | Ditulis ke log harian (disimpan 7 hari), AppBar dilepas, app restart sendiri sekali. Crash kedua dalam 1 menit menghentikan app dan menampilkan toast berisi lokasi log |
| Explorer restart | Daftar ulang AppBar saat menerima `TaskbarCreated` |
| Resolusi, DPI, atau monitor utama berubah | Hitung ulang posisi dan ukuran bar serta pulau |
| Bangun dari sleep | Daftar ulang AppBar, muat ulang provider, jalankan `Scheduler` untuk jadwal terlewat |
| `settings.json` rusak | Ganti nama ke `settings.json.bak`, pakai default, tampilkan toast |
| Database rusak | Pulihkan dari backup terbaru dan tampilkan toast; kalau gagal, buat database baru dan simpan file rusak |
| Hotkey sudah dipakai aplikasi lain | Hotkey itu tidak aktif; Settings menampilkan "Used by another app" |
| Cuaca gagal diambil | Tampilkan nilai terakhir berwarna abu-abu; tooltip "Last updated 14:00" |
| Item Shelf hilang saat diseret keluar | Seret dibatalkan, tile menampilkan "File moved or deleted" selama 2 detik lalu dihapus |
| File dilepas dari jendela admin | Windows memblokirnya; drop zone menampilkan "Can't take files from an admin window" selama 2 detik |
| Tidak ada perangkat audio atau suara gagal diputar | Kartu tetap tampil; toast dikirim dengan suara notifikasi Windows sebagai cadangan; dicatat di log |
| `~/.claude/settings.json` bukan JSON valid | File tidak diubah sama sekali; Settings menampilkan `Can't read Claude settings` dan tombol untuk membuka file itu |
| TopBar tidak berjalan saat hook dipanggil | Helper tidak menemukan pipe dan keluar dalam ≤ 100 ms tanpa efek; Claude bekerja seperti biasa |
| Helper tidak ada di path hook (TopBar dipindah atau dihapus) | Hook gagal tanpa menghentikan Claude; Settings menampilkan `Hook helper not found · Reinstall` |
| Hook TopBar dihapus dari luar TopBar | Settings menampilkan `Hooks removed outside TopBar · Reinstall` |
| Format file sesi Claude tidak dikenali | Nama sesi diganti nama folder, status hanya dari hook, deteksi Esc tidak jalan; dicatat sekali di log |

## 12. Risiko dan mitigasi

| Risiko | Dampak | Mitigasi |
|---|---|---|
| Windows 11 24H2+ mewajibkan izin Location untuk membaca nama Wi‑Fi | Nama tidak tampil | Tampilkan ikon sinyal saja; tooltip menjelaskan izinnya; tombol ke `ms-settings:privacy-location` |
| Driver sensor diblokir Defender atau tidak didukung laptop | Suhu CPU tidak tampil | Pakai LibreHardwareMonitorLib versi yang memakai driver PawnIO (bukan WinRing0 yang ditandai Defender); verifikasi di Fase 4. Modul sembunyi bila tidak terbaca |
| VLC 3.x tidak mengirim sesi media Windows | Video VLC tampil dengan judul saja, tanpa progres | Judul dari judul jendela VLC; progres hanya bila HTTP interface VLC diaktifkan (pengaturan opsional). VLC 4 sudah mengirim sesi |
| Tombol dialog konflik Explorer tidak bisa ditekan dari luar | Pilihan di kartu pulau tidak sampai ke Explorer | Kartu menampilkan Open dialog yang membawa dialog Explorer ke depan; pengguna memilih di sana |
| Struktur atau teks dialog progres Explorer berubah | Progres copy/move/delete tidak terbaca atau jenis operasinya salah dikenali | Fitur best-effort; kalau jenis operasi tidak dikenali, tampil ikon dan warna netral dengan teks asli dari dialog; cuplikan clipboard tetap jalan |
| Tidak ada API resmi untuk Do Not Disturb | Notif aplikasi lain tetap muncul saat fokus | Kejadian pulau milik app tetap ditahan; kalau DND gagal, buka quick settings Windows |
| Wake timer dimatikan di Power Options | Alarm tidak berbunyi saat laptop sleep | Alarm terlewat berbunyi saat bangun; Settings memberi peringatan bila wake timer mati |
| Windows tidak menyediakan radius blur yang bisa diatur | "Glass" hanya tiga tingkat, bukan slider | Opacity tetap slider kontinu; kaca dipetakan ke Off / blur-behind / acrylic |
| Drag-and-drop dari aplikasi admin diblokir (UIPI) | File dari jendela admin tidak bisa masuk Shelf | Pesan jelas di drop zone; app utama tetap non-admin supaya drag biasa jalan |
| App crash meninggalkan ruang AppBar kosong | Ruang 32 px tidak terpakai | Lepas AppBar di semua jalur keluar dan di handler exception; restart otomatis mendaftar ulang |
| Browser hanya mengirim satu sesi media | Hanya satu sumber terlihat | Pakai sesi yang sedang diputar; kalau beberapa diputar, pilih yang terakhir berubah |
| Windows di-mute atau volume 0 saat alarm | Alarm tidak terdengar | Peringatan dan tombol Unmute di kartu; animasi kartu tetap jalan; TopBar tidak mengubah volume sistem diam-diam |
| Deteksi update yang menunggu tidak selalu sama dengan Start menu | Item "Update and …" tidak muncul padahal ada update, atau sebaliknya | Item hanya ditampilkan bila deteksi yakin; perilaku `SHUTDOWN_INSTALL_UPDATES` dan sumber deteksi diverifikasi di laptop pengguna saat Fase 3. Kalau tidak cocok, menu menampilkan `Open Windows Update` sebagai ganti item update |
| Sleep lewat `SetSuspendState` berbeda di laptop Modern Standby | Laptop tidak tidur, atau masuk hibernate | Diuji di laptop pengguna saat Fase 3; kalau berbeda, Sleep memakai jalur lain yang ditentukan saat implementasi |
| Prompt izin di Claude Desktop tidak memicu hook `Notification` (Desktop menangani izin sendiri) | Menunggu izin tampil sebagai Working, bukan Needs input | Diverifikasi di awal Fase 3. Kalau tidak terpicu, Needs input hanya untuk pertanyaan dan rencana; kalau file sesi ternyata punya status menunggu izin, status itu dipakai sebagai gantinya |
| Format `~/.claude/sessions/*.json` tidak resmi dan bisa berubah setelah update Claude | Nama sesi dan deteksi Esc hilang | File itu hanya pelengkap; cadangan di bagian 11 |
| Update Claude Code mengubah nama event atau isi JSON hook | Status sesi tidak terbaca | Helper mengabaikan field yang tidak dikenal; Settings menampilkan waktu kejadian terakhir (`Connected · last event 2 min ago`) supaya masalah cepat terlihat |

## 13. Pengujian

- **Unit test (xUnit) untuk Core**: `Scheduler` (alarm berulang, snooze, terlewat saat sleep, restart di tengah timer, perubahan zona waktu), `IslandCoordinator` (semua aturan prioritas di 6.3, termasuk fullscreen dan drop zone), `ShelfService` (batas 8 item, bundel, pembersihan file hilang termasuk sebagian isi bundel, hasil Move vs Copy), format angka/satuan/tanggal, definisi "todo hari ini", pengubahan urutan note, kurva volume naik bertahap, aturan kapan suara dibisukan (fase fokus, mute).
- **Unit test sesi Claude (Core)**: perpindahan status (prompt → Working → Needs input → Working → Done; Esc → Idle; proses mati → dihapus; izin ditolak), penggabungan beberapa sesi jadi satu aktivitas, aturan gelembung "belum dilihat", posisi di `IslandCoordinator` termasuk saat fase fokus dan fullscreen.
- **Unit test penggabung `settings.json` Claude**: memasang dua kali tidak menggandakan entri, hook wmux tetap ada, melepas hanya menghapus entri TopBar, JSON rusak tidak disentuh, cadangan dibuat.
- **Test repository** pada file SQLite sementara: simpan/muat, migrasi, batas baris (50 riwayat clipboard, 8 item Shelf).
- **Mode demo** untuk memeriksa tampilan dan gerak semua keadaan tanpa kejadian nyata.
- **Checklist manual per fase** di laptop pengguna: ruang AppBar, fullscreen (YouTube, game), restart Explorer, sleep/resume, DPI 100/125/150%, lebar 1366 dan 1920 px, colok/cabut charger, Spotify dan YouTube di Chrome/Edge, video di Media Player dan VLC, copy, cut/move, dan delete file besar (termasuk Shift+Delete, batal di tengah jalan, dan bentrok nama file), copy gambar, Caps Lock, kamera dan mikrofon dipakai bersamaan (compact dan saat pulau terbuka), alarm saat Windows di-mute dan saat volume 0, cabut headset saat alarm berbunyi, menu power (Sleep, Shut down, Restart, dan varian update saat ada update menunggu), alarm saat sleep, matikan paksa saat mengetik note, seret file masuk dan keluar Shelf, atur urutan note dengan mouse dan keyboard, tambah todo untuk hari ini dan tanggal lain dari halaman Calendar (termasuk lewat Add in Calendar), sambungkan dan putuskan earbuds Bluetooth, buka dan tutup beberapa aplikasi lalu pindah fokus di antaranya, ubah tinggi bar dan pulau di Settings, Transparency effects mati, sesi Claude Desktop yang bertanya / minta izin / menyerahkan rencana / dihentikan dengan Esc / ditutup, dua sesi Claude bersamaan, sesi `claude` di wmux, Claude dipakai saat TopBar mati, overhead hook Claude (p95).
- **Pengukuran performa**: catat RAM dan CPU dari Task Manager setelah 8 jam pemakaian normal.

## 14. Fase rilis

| Fase | Isi | Selesai bila |
|---|---|---|
| 1. Fondasi bar | F-BAR-01–07, F-BAR-08 (jam, material, autostart), F-BAR-09, F-LEFT-01, F-ISL-17, F-ISL-01–06, F-ISL-07 kerangka + Media, F-RIGHT-01 | Bar dipakai seharian penuh tanpa crash; checklist Fase 1 lulus; target 8.1 terpenuhi |
| 2. Produktivitas | SQLite, jendela Tools, F-TOOL-01–09 (F-TOOL-04 bagian pulau), F-ISL-07 Timer/Alarms/Calendar/Todo/Notes, F-ISL-08, F-ISL-16, F-SND-01–06 | Data tahan restart dan matikan paksa; alarm berbunyi tepat waktu termasuk setelah sleep |
| 3. Sistem lengkap | F-ISL-10–12, F-ISL-14, F-ISL-18, F-LEFT-02, F-LEFT-04–05, F-RIGHT-02–04, F-RIGHT-07–08, F-ISL-07 Shelf + System, F-BAR-08 modul dan baterai, F-CLD-01–07 (diawali verifikasi hook izin di Claude Desktop) | Modul kanan cocok dengan Task Manager; overflow bekerja di 1366 px; file bisa masuk dan keluar Shelf; sesi Claude Desktop dan terminal tampil benar saat bekerja, bertanya, dan selesai |
| 4. Ekstra | F-TOOL-10–11, F-LEFT-03, F-ISL-15, F-RIGHT-05–06, F-TOOL-04 DND Windows, sisa F-BAR-08 | Semua fitur best-effort punya cadangan yang bekerja saat fiturnya gagal |

Mockup HTML di `docs/mockup/top-taskbar-mockup.html` menampilkan keadaan dari keempat fase sebagai acuan visual dan gerak.

## 15. Di luar cakupan

- Lebih dari satu monitor.
- Mengganti taskbar, Start menu, atau system tray Windows, termasuk volume dan baterai.
- Bahasa UI selain Inggris.
- Sinkronisasi cloud dan backup online.
- Note berformat (markdown, gambar), lampiran todo, todo berulang.
- Riwayat clipboard untuk gambar dan file.
- Menyalin file ke dalam Shelf (Shelf hanya menyimpan referensi).
- File suara milik pengguna untuk alarm atau timer (hanya lima suara bawaan).
- Installer publik, auto-update, code signing, Microsoft Store.
- Tema terang dan kustomisasi warna aksen.
- Tab Chat Claude Desktop; hanya sesi Claude Code (tab Code dan CLI) yang dipantau.
- Menjawab pertanyaan atau izin Claude langsung dari pulau, dan membuka percakapan tertentu di dalam Claude Desktop.
