# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Apa ini

Gili: bar kaca di tepi atas layar Windows 11 dengan "pulau" hitam ala Dynamic Island di tengah. Namanya dari kata Indonesia untuk pulau kecil, dan maskot mata di pulau memakai nama yang sama. Sebelum 26 Oktober 2026 app ini bernama TopBar, jadi commit lama memakai path `src/TopBar.*`. Gili akan dirilis gratis untuk umum (kode tetap tertutup, repo privat) lewat Microsoft Store dan installer bertanda tangan di GitHub, setelah Fase 4 (PRD 9.4, Fase 5). Hanya Windows 11 dan monitor utama. Laptop pengembangan juga laptop uji utama, jadi menjalankan app langsung mengubah desktop pengembang: area kerja Windows berkurang setinggi bar.

Sumber kebenaran:
- PRD: [docs/superpowers/specs/2026-10-25-top-taskbar-prd.md](docs/superpowers/specs/2026-10-25-top-taskbar-prd.md). Setiap fitur punya ID (`F-BAR-01`, `F-ISL-07`, `F-CLD-03`, …) dan nomor fase (bagian 14). Rencana dan commit merujuk ID ini. `F-ISL-09`, `F-ISL-13`, dan `F-RIGHT-06` (suhu CPU) sudah dihapus; nomornya jangan dipakai lagi.
- Mockup HTML: [docs/mockup/top-taskbar-mockup.html](docs/mockup/top-taskbar-mockup.html), acuan visual dan gerak untuk semua fase. Tampilan dan gerak app harus persis mockup (keputusan pengguna 2026-10-27). Kalau mockup dan PRD berbeda, laporkan ke pengguna dan samakan keduanya; jangan memilih salah satu diam-diam.
- `NOTE.jpg` dan `Untitled Diagram.jpg` di root adalah sketsa awal pengguna. Sketsa kedua ("yang lain fade out") sudah diganti PRD 6.3: hanya elemen yang tertimpa panel (+8 px) yang memudar.

## Bahasa

- Chat, PRD, rencana, dan dokumen lain: Bahasa Indonesia.
- Semua teks yang tampil di app (label, tombol, tooltip, toast) hanya bahasa Inggris, tanpa pilihan bahasa. Ambil string persis dari PRD; jangan menerjemahkannya.

## Perintah

Butuh .NET 10 SDK (`net10.0-windows`). Build dari root repo:

```bash
dotnet build Gili.slnx
dotnet test tests/Gili.Core.Tests
dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandCoordinatorTests"
dotnet format Gili.slnx --verify-no-changes
dotnet run --project src/Gili.App
```

- Hanya satu instance yang bisa jalan (F-BAR-05). Menjalankan Gili saat sudah ada yang jalan hanya membuka Settings di instance lama lalu keluar, jadi tutup dulu instance lama sebelum `dotnet run`.
- Menutup Gili dari luar: `Gili.exe --quit` (exe mana pun) meminta instance yang jalan keluar lewat jalur Quit biasa dan melepas ruang AppBar. Jangan pakai `taskkill`: perintah itu ikut menutup jendela tersembunyi WPF dan bisa membuat proses tetap hidup tanpa bar.
- Kalau proses dihentikan paksa (debugger, `Stop-Process`) tanpa melepas AppBar, ruang di atas layar bisa tertinggal kosong. Restart Explorer untuk memulihkannya.
- Build Debug punya item "Simulate crash" di menu klik kanan bar untuk menguji restart otomatis (PRD 11).
- Pasang build Release ke `%LOCALAPPDATA%\Programs\Gili\` lalu jalankan: `pwsh scripts/publish.ps1`. Salinan ini berperilaku seperti installer GitHub: hanya salinan terpasang yang menulis entri autostart (`HKCU\...\Run`); `dotnet run` tidak pernah mendaftarkan dirinya.
- Paket MSIX (versi Store): `pwsh scripts/package.ps1` membangun `artifacts/msix/Gili-<versi>.msix` (self-contained, butuh Windows SDK untuk `makeappx`); `-Install` mendaftarkan layout-nya di laptop ini lewat `Add-AppxPackage -Register` (butuh Developer Mode, tanpa admin dan tanpa sertifikat). Installer GitHub (satu file `Gili-Setup-<versi>.exe` dari Inno Setup, tanpa UAC) baru dibuat setelah Fase 1–4 selesai, di awal Fase 5, didahului build untuk teman tanpa tanda tangan (PRD 9.4 "Installer GitHub"); jangan dikerjakan lebih awal. Versi MSIX autostart lewat `StartupTask`, bukan `HKCU\...\Run`, dan datanya di folder paket (`%LOCALAPPDATA%\Packages\Gili_*\LocalState\`), bukan `%LOCALAPPDATA%\Gili\`. Uji perubahan yang menyentuh file, registry, autostart, toast, atau hook Claude di kedua bentuk. Versi ada di `Directory.Build.props`.
- Mode demo (klik kanan bar → Demo mode, F-BAR-09) memutar skenario palsu ±90 detik (`Gili.Core.Demo.DemoScript`) untuk mengecek tampilan semua keadaan pulau tanpa kejadian nyata. Keadaan baru di pulau ditambahkan ke skenario itu.
- `Gili.ClaudeHook` (Fase 3) dipublish NativeAOT, jadi butuh workload C++ Build Tools.

## Arsitektur

Proyek dan arah dependensi (PRD 9.1):

| Proyek | Isi | Aturan |
|---|---|---|
| `Gili.Core` | `IslandCoordinator`, `Scheduler`, `ShelfService`, model, interface provider, format angka/tanggal | Tanpa WPF dan tanpa Win32. Semua logika yang bisa dites ada di sini |
| `Gili.Platform` | Implementasi provider Windows, `AppBarService`, `BarMaterial`, hotkey, notifikasi, drag-and-drop | Bergantung ke Core |
| `Gili.Data` | Repository SQLite (Microsoft.Data.Sqlite), `SettingsStore` | Bergantung ke Core |
| `Gili.App` | WPF + CommunityToolkit.Mvvm: `BarWindow`, `IslandWindow`, `ToolsWindow`, `SettingsWindow`, ViewModel | Komposisi semuanya |
| `Gili.ClaudeHook` | Console NativeAOT yang dipanggil hook Claude Code | Berdiri sendiri |
| `Gili.Core.Tests` | xUnit untuk Core dan Data | |

Hal yang butuh membaca beberapa bagian PRD untuk dipahami:

- **Dua jendela, bukan satu.** `BarWindow` mendaftar sebagai AppBar (`SHAppBarMessage`) dan memesan ruang setinggi bar. `IslandWindow` adalah jendela transparan topmost terpisah untuk keadaan mengambang (expanded, kartu, drop zone), supaya pulau yang membesar tidak mengubah ruang AppBar dan jendela lain tidak bergeser. AppBar harus dilepas di semua jalur keluar, termasuk handler exception, dan didaftar ulang saat `TaskbarCreated`, perubahan DPI/resolusi, dan bangun dari sleep.
- **Pulau selalu di `IslandWindow`.** Jendela transparan topmost dengan `Owner` = `BarWindow`, supaya tetap di atas bar setiap kali bar menegaskan ulang posisi topmost-nya. `BarWindow` mengosongkan bagian tengah, dan saat pulau mengambang hanya memudarkan latarnya serta elemen yang tertimpa. Ukuran `IslandWindow` adalah envelope: setinggi bar saat compact, cukup besar untuk panel saat mengambang.
- **Geometri pulau digerakkan `Spring` (Core), bukan Storyboard.** Lebar, tinggi, posisi atas, dan radius di-step per frame lewat `CompositionTarget.Rendering`, hanya selama ada pegas yang bergerak. Mengganti target pegas menjaga posisi dan kecepatan, jadi transisi selalu bisa disela (PRD 8.3). `IslandController` menghubungkan provider ke `IslandCoordinator` dan menjalankan timer tenggatnya; `IslandViewModel` hanya menyiapkan data tampilan.
- **Alur data.** Provider (interface di Core, implementasi di Platform) mengirim snapshot ke state di Core. `IslandCoordinator` memutuskan apa yang tampil di pulau. ViewModel berlangganan state dan memperbarui UI lewat dispatcher WPF. Mode demo hanyalah set implementasi provider palsu, jadi fitur baru selalu butuh interface provider supaya bisa didemokan.
- **`IslandCoordinator` adalah inti aturan.** Ada dua tabel prioritas yang terpisah di PRD 6.3: prioritas tampilan (1 kartu alarm/timer/konflik file … 4 notifikasi Windows … 7 Gili saja) dan urutan aktivitas di slot compact (1 cuplikan clipboard … 8 ngecas). Expanded yang dibuka pengguna berada di atas prioritas 4–7 dan di bawah 1–3. Fullscreen dan fase fokus pomodoro mengubah aturan ini. Aktivitas kedua jadi gelembung di kanan pulau. Semua aturan ini wajib punya unit test (F-ISL-03, PRD 13).
- **Waktu.** `Scheduler` menyimpan setiap jadwal sebagai waktu absolut UTC di database, lalu menghitung ulang saat app mulai, bangun dari sleep, dan saat jam atau zona waktu berubah. Timer dan stopwatch harus tetap benar setelah restart.
- **Hak akses.** Tidak ada bagian Gili yang jalan sebagai admin, dan instalasinya tidak meminta UAC: Windows (UIPI) memblokir drag-and-drop dari proses biasa ke proses admin sehingga Shelf rusak, Microsoft Store menolaknya, dan antivirus jadi curiga. Jangan menambah fitur yang butuh admin atau driver (karena itu suhu CPU dihapus).
- **Distribusi.** Build self-contained (pengguna tidak punya .NET). Hindari perilaku yang mirip malware: tidak mengunduh atau menjalankan kode, tanpa obfuscation atau packer, semua exe ditandatangani, dan setiap hook atau akses ke data app lain harus opt-in. Path helper Claude di versi MSIX memakai app execution alias, bukan folder `WindowsApps` (PRD 9.4, 12).
- **Kegagalan terisolasi.** Provider yang gagal tidak boleh mematikan bar: modulnya menampilkan "—" dengan alasan di tooltip, lalu dicoba lagi setelah 5 detik, 30 detik, kemudian tiap 2 menit (PRD 11).
- **Penyimpanan.** `%LOCALAPPDATA%\Gili\` (versi MSIX: folder lokal paket, PRD 10): `gili.db` (SQLite WAL, migrasi lewat tabel `schema_version`, backup harian 7 salinan), `settings.json`, `logs\`. Thumbnail clipboard dan semua data sesi Claude hanya di memori, tidak pernah ditulis ke disk atau log.
- **Integrasi Claude (Fase 3).** Hook Claude Code → `Gili.ClaudeHook.exe <Event>` → named pipe `\\.\pipe\Gili.Claude`, dilengkapi `~/.claude/sessions/*.json` yang dipantau. Helper tidak boleh menulis ke stdout, selalu keluar dengan kode 0, dan harus selesai ≤ 50 ms p95. Penggabung `~/.claude/settings.json` wajib membuat cadangan, tidak menggandakan entri, dan tidak menyentuh hook lain: `settings.json` pengguna sudah berisi hook wmux. JSON yang tidak valid dibiarkan apa adanya.

## UI dan gerak

- Token warna, tipografi (Segoe UI Variable, digit tabular untuk angka yang berubah), radius, dan tabel pegas ada di PRD 8.3. Pegas ditulis sebagai damping ratio + response (gaya Apple), bukan durasi.
- Semua transisi pulau harus bisa disela: keadaan baru mulai dari ukuran yang sedang tampil di layar, bukan dari target sebelumnya. Kalau "Animation effects" Windows mati, semua transisi jadi cross-fade 100 ms.
- Radius konsentris: radius elemen dalam = radius panel − jaraknya ke tepi (menu tab 18 px pada jarak 14 px di panel 32 px).
- Untuk memoles UI, pakai skill `apple-design` (ada di `.claude/skills/apple-design`) dan `impeccable`.

## Keputusan yang sudah final

Pengguna sudah menolak atau menghapus hal-hal ini saat brainstorming. Jangan diusulkan lagi:
- OSD volume dan brightness, ikon volume dan baterai di bar, indikator virtual desktop.
- Gaya modul kanan selain "dua baris" (label kapital kecil di atas nilai): ikon+angka, ring mini, sparkline, dan kapsul berkelompok sudah ditolak.
- Navigasi pulau selain menu tab (10 tab ikon; nama hanya di tab aktif dan saat hover) yang mengisi lebar pulau di kanan lingkaran Gili.
- Titik kamera/mic dengan lingkaran hitam: titik 8 px tanpa latar.
- Kolom input todo di halaman Todo: todo hanya dibuat dari Calendar (Add task).
- Panel dropdown untuk Wi‑Fi/Bluetooth/volume/brightness dan menu power: semuanya ada di halaman Controls di pulau, tanpa tab di menu (dibuka dari tombol Controls, modul Wi‑Fi, dan Power).
- Jam dan tanggal di dalam pulau: pindah ke kanan bar. Pulau tanpa aktivitas berisi maskot Gili, yang matanya berjalan sepanjang pulau mengikuti kursor.
- Suhu CPU dan helper admin (`Gili.SensorHelper`): dihapus supaya Gili lolos Store dan tidak dianggap malware.
- Versi berbayar, kode terbuka, dan telemetri: Gili gratis, kode tertutup, tanpa telemetri.

Pengguna minta Gili tetap sangat ringan: setiap fitur baru harus punya anggaran CPU/RAM di PRD 8.1, dan ingatkan pengguna kalau fitur yang diminta berat.

Hal yang harus diverifikasi di laptop pengembang sebelum diimplementasikan (Fase 3): hook `Notification` untuk prompt izin di Claude Desktop, `SHUTDOWN_INSTALL_UPDATES` beserta deteksi update yang menunggu, `SetSuspendState` di laptop Modern Standby, dan waktu mulai `Gili.ClaudeHook` lewat app execution alias di paket MSIX. Sebelum Fase 4: `UserNotificationListener` di versi GitHub (sparse package untuk identitas) dan apakah event `NotificationChanged` sampai ke aplikasi desktop (notifikasi Windows di pulau, F-ISL-27). Di Fase 5 semuanya diulang di matriks perangkat PRD 13 (desktop, VM bersih).
