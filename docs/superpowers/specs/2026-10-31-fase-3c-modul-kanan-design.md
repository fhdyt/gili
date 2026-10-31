# Fase 3c: Modul kanan dan halaman System (desain)

Tanggal: 2026-10-31. Fitur: F-RIGHT-03 (modul GPU dan disk), F-RIGHT-04 (atur modul dan overflow), bagian kanan F-BAR-12 (saklar Settings → Right side), F-BAR-08 bagian modul, dan F-ISL-07 System (halaman 10).

Pengguna meminta saya melanjutkan tanpa berhenti dan memilih sendiri setiap pilihan ("jika ada pilihan pilih sendiri"), jadi tidak ada sesi tanya-jawab. Semua bagian **(keputusan Claude)** adalah pilihan saya. Isi dan tampilan diambil dari PRD 6.3 tabel halaman (baris 10), PRD 6.4, PRD 6.5, dan mockup (`.sys`, `.tile`, `renderSys`, `mods`, `HIDE_ORDER`).

## Maksud

Sejak Fase 1 bar kanan selalu menampilkan cuaca, jaringan, CPU, RAM, Wi‑Fi, Bluetooth, volume, brightness, tray, dan jam, dan klik CPU/RAM membuka tab System berisi `Coming in Phase 3`. Fase ini membuat bar kanan bisa diatur: setiap modul bisa disembunyikan, modul sistem bisa diurutkan, GPU dan sisa ruang disk tersedia sebagai modul tambahan (default mati), dan halaman System menampilkan grafik 60 detik. Modul yang dimatikan berhenti mengambil datanya, sesuai permintaan pengguna supaya Gili tetap ringan.

## Tujuan

- Settings → Right side: saklar Weather, Network, CPU, RAM, GPU, Disk, Wi‑Fi, Bluetooth, Volume, Brightness, Tray icons, Clock and date bekerja langsung dan tersimpan. GPU dan Disk default mati.
- Enam modul sistem (Weather, Network, CPU, RAM, GPU, Disk) bisa diurutkan di Settings, dan bar mengikuti urutan itu.
- Saklar menganimasikan bar seperti saklar Left side: modul mengecil dan deretan merapat (PRD 6.5).
- Overflow tetap memindahkan modul ke "…" dengan urutan Weather, Network, Disk, GPU, RAM, CPU, apa pun urutan tampilnya (F-RIGHT-04).
- Modul GPU `GPU` / `4%` dan Disk `C: FREE` / `120 GB`; selisih dengan Task Manager ≤ 5 poin (F-RIGHT-03).
- Halaman System: kotak CPU, RAM, GPU, Download dengan grafik mini 60 detik, dan kotak Disk C: selebar dua kolom dengan sisa ruang dan bilah pemakaian. Klik membuka Task Manager.
- Modul yang mati berhenti mengambil data: counter CPU, RAM, jaringan, GPU, dan disk hanya dibaca bila modulnya nyala atau halaman System terbuka; cuaca tidak mengakses internet saat mati; Wi‑Fi tidak dibaca tiap 10 detik saat mati.
- Mode demo membuka halaman System.

## Bukan tujuan

- Panel tray (F-RIGHT-09, sub-fase sendiri). Saklar Tray icons hanya menyembunyikan chevron.
- Pilihan kota cuaca (F-BAR-08, Fase 4). Baris Weather city tetap nonaktif.
- Item update di Power (F-RIGHT-08).
- Mengurutkan Wi‑Fi, Bluetooth, Volume, Brightness, Tray, dan Jam: PRD hanya mengizinkan urutan modul sistem; Settings dan Power selalu paling kanan.
- GPU per proses, suhu, atau beberapa GPU terpisah: modul menampilkan satu angka seperti kolom GPU Task Manager.

## Anti-tujuan

- Hak admin, driver, atau WMI berat. Suhu CPU sudah dihapus (CLAUDE.md).
- Counter yang terus dibaca padahal tidak ada yang menampilkannya.
- Bar yang bergeser saat angka berubah: lebar minimum modul tetap seperti modul yang ada (digit tabular).
- Grafik yang menyesatkan: bagian grafik yang tidak dibaca tidak diisi angka palsu.

## Verifikasi di laptop pengguna (sebelum desain)

Program uji kecil di scratchpad, tanpa mengubah sistem:

| API | Hasil |
|---|---|
| `PdhAddEnglishCounter(@"\GPU Engine(*)\Utilization Percentage")` + `PdhGetFormattedCounterArray` | 466 instance di Intel Iris Xe; satu pembacaan 1,2–3 ms (pertama 22 ms); proses uji memakai 0,12% CPU mesin selama 20 detik, termasuk startup .NET. Nilai 2,5–3,8% saat diam, cocok dengan `Get-Counter` |
| `DriveInfo("C").AvailableFreeSpace` | 113 702 154 240 byte (106 GB dalam satuan Explorer) dari 476 GB |

## Pendekatan

### Membaca GPU

| Cara | Untung | Rugi |
|---|---|---|
| **PDH `GPU Engine(*)` wildcard, jumlah per engine, ambil maksimum** | Rumus yang sama dengan kolom GPU Task Manager; PDH mengikuti proses yang datang dan pergi; teruji 2 ms per bacaan | Ratusan instance per bacaan |
| `D3DKMTQueryStatistics` per adapter/engine | Paling dekat dengan cara Task Manager membaca | API kernel-thunk tanpa dokumentasi lengkap, struktur besar, rawan salah di driver lain |
| `System.Diagnostics.PerformanceCounter` per instance | Sudah dipakai untuk CPU | Harus enumerasi instance sendiri tiap detik (mahal) dan membuat ratusan objek counter |

Dipilih PDH wildcard **(keputusan Claude)**. Nama instance `pid_<pid>_luid_<a>_<b>_phys_<n>_eng_<m>_engtype_<t>` dikelompokkan per bagian setelah `pid_<pid>_`, nilai tiap kelompok dijumlahkan (dibatasi 100), lalu diambil yang terbesar. Logika pengelompokan ada di Core (`GpuEngines.Utilization`) dan dites; Platform hanya membaca PDH.

### Membaca disk

PRD 9.2 menyebut PDH `LogicalDisk`. `DriveInfo` (`GetDiskFreeSpaceEx`) memberi angka yang sama tanpa query PDH, jadi dipakai itu **(keputusan Claude)**, dibaca tiap 10 detik karena sisa ruang jarang berubah cepat. Drive yang dibaca adalah drive Windows (`Environment.SystemDirectory`), dan labelnya mengikuti hurufnya (`C: FREE`). Satuan GB mengikuti Explorer (1024³), dibulatkan: `106 GB`; di bawah 10 GB satu desimal: `8.4 GB`. PRD 9.2 diperbarui.

### Siapa yang meminta data

`StatsService` menerima himpunan `StatsDemand` (Cpu, Memory, Network, Gpu, Disk) dan hanya menjalankan sampler yang diminta. Permintaan = modul yang nyala di Settings (termasuk yang sedang di "…", karena daftar "…" menampilkan angkanya) ditambah semua lima saat halaman System terlihat. Aturan ini ada di Core (`StatsDemand.For(settings, systemPageOpen)`) dan dites. Sampler yang berhenti lalu jalan lagi mulai dari nol: riwayatnya dikosongkan dan pembacaan pertama CPU (yang selalu 0) dibuang **(keputusan Claude)**.

Cuaca: saat modul dimatikan, timer cuaca berhenti; saat dinyalakan lagi, cuaca diambil langsung bila pembacaan terakhir lebih tua dari 30 menit. Wi‑Fi: pembacaan 10 detik berhenti saat modul mati; event koneksi tetap didengar (tanpa biaya) supaya pulau Wi‑Fi tetap benar. Bluetooth dan volume tetap dari event, karena pulau kejadian Bluetooth (F-ISL-18) dan pulau kontrol memakainya.

### Riwayat 60 detik

`History` di Core: ring buffer 60 nilai per metrik, diisi setiap pembacaan 1 detik. Grafik digambar dari kanan (nilai terbaru di tepi kanan), sehingga riwayat yang baru mulai tampil sebagai garis pendek di kanan, bukan garis datar palsu. Skala CPU, RAM, GPU 0–100%; Download menyesuaikan maksimum 60 detik terakhir dengan batas bawah 100 KB/s, supaya jaringan yang diam tidak tampil sebagai lonjakan **(keputusan Claude; mockup memakai skala tetap 9 MB/s untuk data palsu)**. Riwayat hanya ada selama metrik dibaca.

### Urutan di Settings

PRD 6.4 meminta modul sistem "bisa diurutkan dengan seret", tetapi mockup Settings tidak punya pegangan seret. Mockup disamakan dengan PRD: enam baris modul sistem di Right side mendapat pegangan titik-titik (`i-grip`, 14 px, `#8E8E93`) di kiri label, dan baris bisa diseret naik/turun **(keputusan Claude)**. Dari keyboard: Alt + panah atas/bawah saat fokus di saklar baris itu memindahkannya satu langkah, sama dengan pola Alt + panah di Notes. Baris lain (Wi‑Fi dan seterusnya) tidak punya pegangan. Perubahan urutan langsung berlaku di bar dengan animasi geser 300 ms.

### Animasi saklar

Saklar memakai animasi yang sama dengan Left side (lebar slot 300 ms dengan ease-out, ikon mengecil dan memudar), jadi kodenya dipindah ke satu helper bersama **(keputusan Claude)**. Overflow tetap tanpa animasi seperti sekarang. Saat "Animation effects" Windows mati, modul langsung hilang/muncul.

### Halaman System

Sesuai mockup `.sys`:

- Grid 3 kolom dengan jarak 8 px selebar isi halaman. Kotak: latar `#1C1C1E`, radius 12 px, padding 8 × 12 px, tinggi 70 px. Baris atas: nama (12 px, `#8E8E93`) di kiri dan nilai (14 px semibold, digit tabular, `#F5F5F7`) di kanan. Di bawahnya grafik setinggi 26 px, garis 1,6 px: CPU `#0A84FF`, RAM `#BF5AF2`, GPU `#30D158`, Download `#64D2FF`.
- Kotak kelima `Disk C:` selebar dua kolom: nilai `106 GB free`, bilah 6 px (radius 3, latar `#3A3A3C`, isi `#8E8E93` selebar persentase terpakai).
- Nilai: `12%`, `54%`, `4%`, dan laju unduhan dengan format bar (`2.1 MB/s`). Provider gagal: `—` dan tooltip alasannya (PRD 11).
- Seluruh halaman adalah satu tombol dengan nama aksesibilitas `Open Task Manager`; klik atau Enter menutup pulau lalu menjalankan `taskmgr.exe`. Kursor tangan saat hover.
- Grafik diperbarui sekali per detik hanya saat halaman System terlihat; menutup pulau atau pindah halaman menghentikan penggambaran.

### Bar

- Semua dua belas modul punya `Tag` id: `weather`, `net`, `cpu`, `ram`, `gpu`, `disk`, `wifi`, `bt`, `vol`, `bri`, `tray`, `clock`. Enam modul sistem disusun ulang di `RightModules` mengikuti `ModuleOrder`; sisanya tetap di urutan PRD.
- Modul tampil bila nyala di Settings, punya data (cuaca, Wi‑Fi, Bluetooth, volume, brightness seperti sekarang), dan tidak dipindah ke "…".
- GPU: `GPU` / `4%`, klik membuka System. Disk: `C: FREE` / `106 GB`, tooltip `Free space on C:` (string mockup), klik membuka System.
- Daftar "…" menambah `GPU · 4%` dan `Disk · 106 GB free`.

## Data

`AppSettings` mendapat dua field **(keputusan Claude)**:

- `HiddenModules` (`RightModules` flags: Weather, Network, Cpu, Ram, Gpu, Disk, Wifi, Bluetooth, Volume, Brightness, Tray, Clock), default `Gpu | Disk`. Settings lama tanpa field ini mendapat default, jadi GPU dan Disk tetap mati untuk pengguna yang sudah ada.
- `ModuleOrder` (daftar id enam modul sistem), default `weather, net, cpu, ram, gpu, disk`. `Normalize` membuang id yang tidak dikenal dan duplikat, lalu menambahkan id yang hilang di posisi default-nya relatif ke urutan yang ada (paling sederhana: di akhir).

## Anggaran (PRD 8.1)

Baris baru:

| Fitur | Biaya | Anggaran | Cadangan |
|---|---|---|---|
| Modul GPU (Fase 3c) | Satu query PDH wildcard `GPU Engine` per detik (±2 ms, ±470 instance) hanya saat modul GPU nyala atau halaman System terbuka | CPU ≤ 0,2% saat nyala; 0 saat mati | Dibaca tiap 2 detik |
| Modul disk (Fase 3c) | Satu `GetDiskFreeSpaceEx` per 10 detik saat modul nyala atau halaman System terbuka | Tidak terukur | — |
| Halaman System (Fase 3c) | Empat polyline 60 titik dan lima teks digambar ulang sekali per detik hanya saat halamannya terlihat (jendela layered, digambar CPU) | CPU ≤ 0,5% selama terlihat; 0 saat tertutup | Grafik diperbarui tiap 2 detik |

Diukur: CPU diam dengan GPU/Disk mati vs nyala, dan selama halaman System terbuka 30 detik.

## Mode demo

Langkah baru setelah Todo: `DemoOpen(IslandPage.System)` di 28,5 detik, sebelum pulau ditutup di 29,5 detik. Halaman memakai angka asli laptop, karena mode demo hanya memalsukan provider pulau.

## Yang diserahkan ke pembangun

Nama kelas dan pembagian file, cara menyusun ulang anak `RightModules` (memindahkan elemen atau membuat ulang daftar), cara menggambar grafik (`Polyline` atau `StreamGeometry`), dan detail seret di Settings selama hasilnya sesuai bagian di atas.

## Temuan saat implementasi

- **Urutan modul disimpan sebagai string berkoma** (`"moduleOrder": "weather,net,cpu,ram,gpu,disk"`), bukan daftar JSON, karena `AppSettings` adalah record yang dibandingkan nilainya; daftar membuat dua settings yang sama dianggap berbeda. `AppSettings.Order` membacanya dan `Normalize` memperbaikinya (id ganda, tak dikenal, atau hilang).
- **`RightModules` bentrok dengan nama `StackPanel` di `BarWindow.xaml`**; di code-behind tipe flag dipakai lewat alias `ModuleSet`.
- **`MinWidth` 24 px modul menahan slot yang menutup**, jadi `SlotAnimation` ikut menganimasikan `MinWidth` bersama `Width`. Animasi saklar Left side dan Right side sekarang satu helper (`SlotAnimation`), termasuk geser 300 ms saat urutan berubah.
- **Sampler dibuat ulang lewat factory** setiap kali diminta lagi, dan objek di balik method group (`CpuSampler`, `GpuSampler`) dilepas saat berhenti, jadi query PDH tidak tertinggal terbuka.
- **Belum teruji dengan input asli**: seret pegangan dan Alt + panah di Settings, serta klik modul GPU/Disk di bar (modul bar tidak punya peer UI Automation). Urutan dari `settings.json` tampil benar di bar dan di Settings; saklar diuji lewat pola Toggle UI Automation; `Open Task Manager` lewat Invoke.
- **Overflow di 1366 px** tidak diuji di layar nyata (laptop pengguna 1920 px); aturannya dites di Core (`RightOverflow`) dan tidak berubah.

## Hasil verifikasi

- Release 0 warning, 702 tes lulus, format bersih.
- GPU dari `GpuSampler` 1,8–2,5% saat `Get-Counter` membaca 2,5% (selisih < 1 poin); disk `106 GB free` sama dengan `DriveInfo`.
- Bar: saklar GPU, Disk, CPU, Weather, Clock and date, dan Tray icons menampilkan/menyembunyikan modulnya langsung; urutan `disk,gpu,weather,net,cpu,ram` diikuti bar dan Settings. Bekerja di build pengembangan, salinan GitHub (`publish.ps1`), dan MSIX (`package.ps1 -Install`).
- Halaman System sesuai mockup; grafik mulai dari tepi kanan dan skala Download mengikuti lonjakan; klik membuka Task Manager dan menutup pulau.
- CPU (Debug, 60 detik diam): GPU/Disk mati 0,44%, nyala 0,56% (+0,12%, anggaran ≤ 0,2%). Pulau terbuka 30 detik: halaman Calendar 2,26%, halaman System 2,41% (+0,15%, anggaran ≤ 0,5%; sisanya ambient glow).
- Memori privat (Debug) dibanding build sebelum 3c dengan settings yang sama: 90 detik 104,6 vs 101,8 MB, 150 detik 101,9 vs 112,5 MB; perbedaannya dalam derau GC.
