# Fase 3a: Pulau sistem kecil (desain)

Tanggal: 2026-10-31. Fitur: F-ISL-10 (Caps/Num Lock), F-ISL-11 (peringatan baterai), F-ISL-14 (titik kamera/mic), F-ISL-18 (Bluetooth tersambung/terputus), F-ISL-23 (tab pulau bisa disembunyikan), dan bagian kiri F-BAR-12 (saklar Settings → Left side dan Island).

Pengguna menyetujui isi ringkasan di percakapan dan meminta semua pilihan teknis diambil sendiri ("jika ada pilihan pilih sendiri"). Bagian yang ditandai **(keputusan Claude)** adalah pilihan saya, bukan pilihan pengguna.

## Maksud

Lima kejadian sistem kecil yang biasanya dicari pengguna di taskbar atau lampu laptop sekarang tampil di pulau: status Caps/Num Lock (laptop pengguna tidak punya lampu Caps Lock, PRD 2), baterai, kamera/mic yang sedang dipakai, dan perangkat Bluetooth yang tersambung atau terputus. Pengguna juga bisa menyembunyikan tab pulau dan ikon kiri yang tidak dipakai. Semuanya tanpa polling yang berat, tanpa hook global, dan tanpa hak admin.

## Tujuan

- Setiap kejadian di tabel "Kejadian wide" PRD 6.3 tampil sesuai teks dan lamanya.
- Titik kamera/mic memenuhi F-ISL-14: ≤ 2 detik, tetap tampil saat pulau mengambang, terlihat di latar terang dan gelap, tidak menimpa isi kiri bar.
- Saklar Settings → Left side dan Island bekerja langsung, tersimpan, dan saling terkait sesuai PRD 6.2 dan 6.3.
- Semua keadaan baru ada di mode demo.

## Bukan tujuan

- Saklar Settings → Right side, overflow, dan urutan modul (sub-fase modul kanan).
- Syarat "Claude hanya saat integrasinya nyala" di saklar kiri (menunggu integrasi Claude).
- Pulau kontrol Bluetooth (F-RIGHT-10) dan modul baterai di bar.

## Anti-tujuan

- Hook keyboard atau mouse global, raw input keyboard, driver, atau hak admin. Gili harus lolos Store dan tidak dicurigai antivirus (CLAUDE.md "Hak akses", "Distribusi").
- Peringatan yang salah di laptop dengan batas pengisian, misalnya `80% reached · unplug` padahal laptop sudah berhenti mengisi sendiri.
- Kejadian palsu saat Gili baru mulai: perangkat Bluetooth yang sudah tersambung dan Caps Lock yang sudah nyala tidak memicu apa pun. Pengecualiannya baterai yang sudah di bawah ambang saat start, yang tetap memberi peringatan sekali (lihat Baterai).

## Keputusan bersama pengguna

1. **Batas pengisian dikenali otomatis.** Laptop pengguna (Lenovo, batas Vantage) melaporkan charger tercolok, tidak mengisi, tidak memakai baterai di 79% (`BatteryStatus`: `PowerOnline` true, `Charging` false, `Discharging` false). Keadaan itu disebut **ditahan**. Saat ditahan, pulau menampilkan `Charging on hold · 79%` sekali per colok, tidak ada `unplug`, dan tidak ada `Fully charged`.
2. **Laptop tanpa batas** tetap mendapat `80% reached · unplug` (ambang bisa diubah) dan `Fully charged` di 100%.
3. **Peringatan rendah** tetap `Battery 20% · plug in` (ambang bisa diubah) dan `Battery 10%`.
4. **Saklar Left side dan Island** dikerjakan di 3a; saklar Right side menunggu sub-fase modul kanan.

## Pendekatan

### Caps/Num Lock

| Cara | Untung | Rugi |
|---|---|---|
| Hook keyboard global (`WH_KEYBOARD_LL`) | Instan, tanpa polling | Tanda tangan keylogger: antivirus dan peninjau Store curiga; setiap tombol yang ditekan di sistem melewati Gili |
| Raw input keyboard (`RIDEV_INPUTSINK`) | Berbasis event | Sama-sama menerima semua tombol; butuh jendela tersembunyi |
| **Membaca `GetKeyState(VK_CAPITAL/VK_NUMLOCK)` 8×/detik** | Tidak melihat tombol apa pun, hanya dua bit status; teruji di laptop pengguna: proses latar yang tidak pernah menerima pesan tombol membaca status yang benar setelah toggle dari proses lain | Bangun 8×/detik; jeda ≤ 125 ms |

**Dipilih: membaca status 8×/detik (keputusan Claude).** Biayanya dua panggilan `GetKeyState` per 125 ms di timer thread pool, tidak terukur di CPU. Pembacaan dijeda bersama polling lain (fullscreen, layar mati, sleep). Pembacaan pertama tidak memicu apa pun. PRD 8.1 yang menyebut "hook Caps/Num Lock yang sudah ada" diperbaiki menjadi "pembacaan status Caps/Num Lock 8×/detik, tanpa hook".

### Kamera dan mikrofon

| Cara | Untung | Rugi |
|---|---|---|
| **Registry `CapabilityAccessManager\ConsentStore\{webcam,microphone}` (HKCU) dengan `RegNotifyChangeKeyValue`** | Sumber yang sama dengan indikator mic Windows; berbasis event; per aplikasi, jadi tooltip bisa menyebut nama app | Format tidak terdokumentasi resmi |
| Membaca registry yang sama tiap detik | Sederhana | Polling baru, melanggar aturan ringan |
| WinRT `AppCapabilityAccess` | Resmi | Hanya izin, bukan "sedang dipakai" |

**Dipilih: registry + notifikasi perubahan (keputusan Claude).** Aplikasi sedang memakai perangkat bila subkey-nya punya `LastUsedTimeStart` > 0 dan `LastUsedTimeStop` = 0. Subkey langsung adalah aplikasi paket (nama keluarga paket); subkey di `NonPackaged` adalah path exe dengan `#` sebagai pemisah. Nama untuk tooltip: aplikasi paket memakai `DisplayName` dari `PackageManager.FindPackagesForUser` (cadangan: bagian nama keluarga sebelum `_`, setelah titik terakhir); exe memakai `FileDescription` dari versi file (cadangan: nama file tanpa ekstensi). Gili sendiri tidak pernah dihitung. Satu thread menunggu dua notifikasi registry; tidak ada polling.

### Bluetooth

**Dipilih: memperluas `BluetoothMonitor` yang sudah ada (keputusan Claude)**, satu-satunya pendekatan yang masuk akal karena watcher berbasis event sudah ada dan dipakai ikon bar. Watcher yang ada (perangkat klasik dan LE yang tersambung) mendapat properti tambahan: kelas perangkat untuk jenis ikon dan `DEVPKEY_Bluetooth_Battery` (`{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2`) untuk baterai. `Added` dan `Removed` sebelum `EnumerationCompleted` hanya mengisi daftar awal, jadi perangkat yang sudah tersambung saat start tidak memicu apa pun. Setelah itu, `Added` berarti tersambung dan `Removed` berarti terputus. Satu monitor dipakai bersama oleh ikon bar dan pulau.

### Baterai

**Dipilih: memperluas `PowerMonitor` dan `PowerRules` yang sudah ada (keputusan Claude).** `PowerSnapshot` mendapat tanda ditahan: ada baterai, charger tercolok, tidak mengisi, dan < 100%. Aturannya di Core dan dites.

### Tab dan ikon kiri

**Dipilih: aturan murni di Core (keputusan Claude):** daftar tab yang tampil dihitung dari settings, halaman yang sedang terbuka, dan halaman yang dibuka langsung (tab sementara). App hanya menampilkan hasilnya. Ini mengikuti pola `IslandCoordinator` dan memenuhi aturan "semua logika yang bisa dites ada di Core".

## Perilaku

### Caps/Num Lock (F-ISL-10)

- Wide `Caps Lock on` / `Caps Lock off` dan `Num Lock on` / `Num Lock off` dengan ikon `i-caps` (Num Lock memakai ikon angka baru, **keputusan Claude**), 1,5 detik, prioritas wide yang sudah ada (dilewati saat notifikasi Windows tampil, kalah dari kartu).
- Menekan lagi selama wide tampil mengganti isinya dan memulai ulang 1,5 detik (kunci wide `caps` dan `num`, perilaku `ShowWide` yang ada).

### Baterai (F-ISL-11)

Aturan Core `BatteryAlerts` dengan state per siklus. Teks dan lama:

| Kejadian | Syarat | Teks | Lama |
|---|---|---|---|
| Rendah | Tidak tercolok, persen ≤ ambang `Battery alert` (default 20, rentang 5–50), belum muncul di siklus pakai ini | `Battery 18% · plug in` (persen saat itu) | 5 detik |
| Kritis | Tidak tercolok, persen ≤ 10, belum muncul di siklus ini | `Battery 10%` (persen saat itu) | 5 detik |
| Ditahan | Charger tercolok dan masuk keadaan ditahan, belum muncul sejak dicolok | `Charging on hold · 79%` | 5 detik |
| Pengingat cabut | Tercolok dan masih mengisi, persen **melewati** ambang `Unplug reminder` (default 80, rentang 50–100; 100 berarti mati), belum muncul sejak dicolok | `80% reached · unplug` (angka ambang) | 5 detik |
| Penuh | Tercolok, persen 100, belum muncul sejak dicolok | `Fully charged` | 5 detik |

- **Siklus pakai** mulai saat charger dicabut; **siklus isi** mulai saat dicolok. Mencolok mengatur ulang peringatan rendah dan kritis; mencabut mengatur ulang ditahan, cabut, dan penuh.
- Pengingat cabut menunggu persen **melewati** ambang (81% untuk ambang 80) sambil masih mengisi **(keputusan Claude)**. Laptop yang menahan di 80% tidak pernah melewatinya, jadi tidak ada pengingat yang salah, tanpa perlu mengingat apa pun di disk.
- Saat dicolok dan langsung ditahan (laptop pengguna di 79%), wide charger berbunyi `Charging on hold · 79%` alih-alih `Charging · 79%`, dan kejadian ditahan dianggap sudah muncul **(keputusan Claude)**: satu wide, bukan dua.
- Kalau rendah dan kritis terpicu bersamaan (Gili mulai di 8% tanpa charger), hanya kritis yang tampil.
- Kalau Gili mulai di bawah ambang tanpa charger, peringatan muncul sekali; mulai di atas ambang tidak memicu apa pun.
- Ikon mengikuti mockup: semua kejadian baterai memakai petir; rendah kuning (`#FFD60A`), kritis merah (`#FF453A`), ditahan, pengingat cabut, dan penuh hijau (`#30D158`).
- Settings → Right side: `Battery alert` (rentang 5–50, help `Also at 10%`) dan baris baru `Unplug reminder` (rentang 50–100, help `Only on laptops that keep charging past it. 100% turns it off`) jadi tersedia **(keputusan Claude: teks baru, ditambahkan ke PRD dan mockup)**.

### Titik kamera/mic (F-ISL-14)

Persis PRD 6.3 "Indikator kamera dan mikrofon": titik 8 px tanpa latar, kamera `#30D158`, mikrofon `#FF9F0A`, garis tepi 1,5 px hitam 35%, 6 px di kiri pulau atau lingkaran Gili, jarak antar-titik 5 px dengan kamera paling dekat ke pulau, sejajar tengah pulau compact. Saat pulau mengambang, titik bersusun vertikal di sisi kiri panel mulai sejajar menu halaman. Muncul dan hilang dengan pegas skala 0 → 1 (Animation effects mati: cross-fade 100 ms). Area hover 6 px lebih lebar; tooltip `Camera in use: Zoom` / `Microphone in use: Discord`; beberapa app digabung dengan koma **(keputusan Claude)**. Saat fullscreen, titik tersembunyi bersama bar. Hover pulau compact (memanjang 8 px tiap sisi) menggeser titik bersama lingkaran Gili. Posisi titik dihitung di Core (`IslandLayout`) dan digambar di `IslandWindow`, karena titik harus tetap di atas pulau yang mengambang.

### Bluetooth (F-ISL-18)

- Tersambung: wide dengan ikon jenis perangkat biru (`#0A84FF`), nama, `Connected`, dan baterai (`80%`) bila dilaporkan, 4 detik. Terputus: ikon abu-abu, nama, `Disconnected`, 2 detik.
- Jenis dari kelas perangkat (klasik) atau appearance (LE): audio (headphone, earbuds, speaker), mouse, keyboard, controller, ponsel; selain itu ikon Bluetooth.
- Satu perangkat yang tersambung-putus berulang: kunci wide per perangkat (`bt:<id>`), jadi kejadian baru mengganti yang sedang tampil atau antre. Kejadian yang datang dalam 10 detik setelah kejadian sebelumnya untuk perangkat yang sama selalu mengganti, tidak pernah menambah antrean **(keputusan Claude tentang cara memenuhi "hanya kejadian terakhir")**.
- Bluetooth dimatikan dari radio: perangkat yang terputus karena itu tidak memicu `Disconnected` satu per satu **(keputusan Claude)**.

### Tab pulau dan ikon kiri (F-ISL-23, F-BAR-12 kiri)

- `AppSettings` menyimpan ikon kiri yang mati dan halaman pulau yang mati (default semua nyala).
- Settings → Left side: saklar Start, Timer, Alarms, Todo, Notes, Clipboard, Shelf, Claude tersedia. Mematikan ikon selain Start juga mematikan halaman bernama sama; saklar halaman itu di Island nonaktif dengan help `Turned off in Left side`. Menyalakan ikon lagi menyalakan halamannya.
- Settings → Island: saklar tiap halaman tersedia. Saklar halaman terakhir yang masih nyala nonaktif, sehingga tidak pernah nol tab. Kalau mematikan ikon kiri akan menyisakan nol tab, saklar ikon itu juga nonaktif **(keputusan Claude)**.
- Bar: ikon yang mati hilang tanpa celah dengan gerak yang sama seperti aplikasi yang ditutup; pemisah hanya tampil kalau ada ikon di kirinya.
- Pulau: tab yang mati keluar dari menu dan urutan geser/panah; tab yang tersisa tersebar ulang; pulau tetap 560 px.
- Halaman tersembunyi yang dibuka langsung (slot, ikon bar, hotkey, kartu) tampil dengan tab sementara di posisi aslinya; tab itu hilang setelah pindah halaman atau pulau ditutup.
- Menyembunyikan halaman yang sedang terbuka lewat Settings: pulau pindah ke tab terlihat terdekat di kiri, atau di kanan kalau tidak ada **(keputusan Claude)**.

### Mode demo

Skenario `DemoScript` mendapat langkah: Caps Lock on, Battery 20%, Charging on hold, earbuds tersambung (dengan baterai) lalu terputus, dan titik kamera + mikrofon selama beberapa detik dengan nama app contoh. Interface provider baru (`ILockKeys`, `IPrivacyMonitor`, `IBluetoothEvents`, ditambah data baterai di `PowerSnapshot`) supaya demo hanyalah implementasi palsu.

## Anggaran (PRD 8.1, baris baru)

| Fitur | Biaya | Anggaran | Cadangan |
|---|---|---|---|
| Caps/Num Lock | Dua `GetKeyState` per 125 ms di thread pool, dijeda bersama polling lain | Tidak terukur (< 0,05%) | Turun ke 4×/detik |
| Titik kamera/mic | Satu thread menunggu notifikasi registry; membaca dua key hanya saat berubah; nama app dicari sekali per app | CPU 0% saat diam; RAM ≤ +1 MB | — |
| Kejadian Bluetooth | Properti tambahan di watcher yang sudah ada | Tidak terukur | — |
| Peringatan baterai | Event `PowerMonitor` yang sudah ada | Tidak terukur | — |

## Batasan

- Core tanpa WPF dan Win32; provider di Platform; teks UI bahasa Inggris persis seperti di dokumen ini dan PRD.
- Mockup dan PRD diperbarui lebih dulu (tugas pertama rencana), termasuk teks baru `Charging on hold`, `Unplug reminder`, `Num Lock on/off`, dan help `Also at 10%`.
- Uji di build biasa dan paket MSIX: settings baru, registry HKCU (MSIX tidak memvirtualisasi pembacaan HKCU di luar paketnya), dan Bluetooth.

## Untuk pembuat rencana

- Bentuk interface provider dan nama tipe Core.
- Ikon baru (angka untuk Num Lock, baterai, mouse, keyboard, controller, ponsel) mengikuti gaya ikon garis yang sudah ada di `Icons.xaml` dan mockup.
- Bentuk tes untuk `BatteryAlerts`, `IslandTabs`, urutan titik privasi, dan nama app dari path registry.
