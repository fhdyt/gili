# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Apa ini

Gili: bar kaca di tepi atas layar Windows 11 dengan "pulau" hitam ala Dynamic Island di tengah. Namanya dari kata Indonesia untuk pulau kecil, dan maskot mata di pulau memakai nama yang sama. Sebelum 26 Oktober 2026 app ini bernama TopBar, jadi commit lama memakai path `src/TopBar.*`. Dipakai pribadi oleh satu pengguna, di satu monitor (monitor utama). Laptop pengembangan adalah laptop target, jadi menjalankan app langsung mengubah desktop pengguna: area kerja Windows berkurang setinggi bar.

Sumber kebenaran:
- PRD: [docs/superpowers/specs/2026-10-25-top-taskbar-prd.md](docs/superpowers/specs/2026-10-25-top-taskbar-prd.md). Setiap fitur punya ID (`F-BAR-01`, `F-ISL-07`, `F-CLD-03`, …) dan nomor fase (bagian 14). Rencana dan commit merujuk ID ini. `F-ISL-09` dan `F-ISL-13` sudah dihapus; nomornya jangan dipakai lagi.
- Mockup HTML: [docs/mockup/top-taskbar-mockup.html](docs/mockup/top-taskbar-mockup.html), acuan visual dan gerak untuk semua fase. Kalau mockup dan PRD berbeda, PRD yang dipakai.
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
- Pasang build Release ke `%LOCALAPPDATA%\Programs\Gili\` lalu jalankan: `pwsh scripts/publish.ps1`. Hanya salinan terpasang yang menulis entri autostart (`HKCU\...\Run`); `dotnet run` tidak pernah mendaftarkan dirinya.
- Mode demo (klik kanan bar → Demo mode, F-BAR-09) memutar skenario palsu untuk mengecek tampilan semua keadaan pulau tanpa kejadian nyata. Belum ada; masuk rencana 1c.
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
| `Gili.SensorHelper` | Proses admin untuk suhu CPU (LibreHardwareMonitorLib), kirim lewat named pipe | Berdiri sendiri |
| `Gili.Core.Tests` | xUnit untuk Core dan Data | |

Hal yang butuh membaca beberapa bagian PRD untuk dipahami:

- **Dua jendela, bukan satu.** `BarWindow` mendaftar sebagai AppBar (`SHAppBarMessage`) dan memesan ruang setinggi bar. `IslandWindow` adalah jendela transparan topmost terpisah untuk keadaan mengambang (expanded, kartu, drop zone), supaya pulau yang membesar tidak mengubah ruang AppBar dan jendela lain tidak bergeser. AppBar harus dilepas di semua jalur keluar, termasuk handler exception, dan didaftar ulang saat `TaskbarCreated`, perubahan DPI/resolusi, dan bangun dari sleep.
- **Alur data.** Provider (interface di Core, implementasi di Platform) mengirim snapshot ke state di Core. `IslandCoordinator` memutuskan apa yang tampil di pulau. ViewModel berlangganan state dan memperbarui UI lewat dispatcher WPF. Mode demo hanyalah set implementasi provider palsu, jadi fitur baru selalu butuh interface provider supaya bisa didemokan.
- **`IslandCoordinator` adalah inti aturan.** Ada dua tabel prioritas yang terpisah di PRD 6.3: prioritas tampilan (1 kartu alarm/timer/konflik file … 6 jam saja) dan urutan aktivitas di slot compact (1 cuplikan clipboard … 8 ngecas). Expanded yang dibuka pengguna berada di atas prioritas 4–6 dan di bawah 1–3. Fullscreen dan fase fokus pomodoro mengubah aturan ini. Aktivitas kedua jadi gelembung di kanan pulau. Semua aturan ini wajib punya unit test (F-ISL-03, PRD 13).
- **Waktu.** `Scheduler` menyimpan setiap jadwal sebagai waktu absolut UTC di database, lalu menghitung ulang saat app mulai, bangun dari sleep, dan saat jam atau zona waktu berubah. Timer dan stopwatch harus tetap benar setelah restart.
- **Hak akses.** App utama selalu jalan tanpa hak admin, karena Windows (UIPI) memblokir drag-and-drop dari proses biasa ke proses admin dan Shelf akan rusak. Hanya `Gili.SensorHelper` yang jalan sebagai admin, didaftarkan lewat Task Scheduler.
- **Kegagalan terisolasi.** Provider yang gagal tidak boleh mematikan bar: modulnya menampilkan "—" dengan alasan di tooltip, lalu dicoba lagi setelah 5 detik, 30 detik, kemudian tiap 2 menit (PRD 11).
- **Penyimpanan.** `%LOCALAPPDATA%\Gili\`: `gili.db` (SQLite WAL, migrasi lewat tabel `schema_version`, backup harian 7 salinan), `settings.json`, `logs\`. Thumbnail clipboard dan semua data sesi Claude hanya di memori, tidak pernah ditulis ke disk atau log.
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
- Navigasi pulau selain menu tab selebar pulau (ikon + nama, 9 tab).
- Titik kamera/mic dengan lingkaran hitam: titik 8 px tanpa latar.
- Kolom input todo di halaman Todo: todo hanya dibuat dari Calendar (Add task).
- Panel dropdown untuk Wi‑Fi/Bluetooth/volume/brightness dan menu power: semuanya ada di halaman Controls di pulau, tanpa tab di menu (dibuka dari tombol Controls, modul Wi‑Fi, dan Power).
- Jam dan tanggal di dalam pulau: pindah ke kanan bar. Pulau tanpa aktivitas berisi maskot Gili, yang matanya berjalan sepanjang pulau mengikuti kursor.

Pengguna minta Gili tetap sangat ringan: setiap fitur baru harus punya anggaran CPU/RAM di PRD 8.1, dan ingatkan pengguna kalau fitur yang diminta berat.

Hal yang harus diverifikasi di laptop pengguna sebelum diimplementasikan (Fase 3): hook `Notification` untuk prompt izin di Claude Desktop, `SHUTDOWN_INSTALL_UPDATES` beserta deteksi update yang menunggu, dan `SetSuspendState` di laptop Modern Standby.
