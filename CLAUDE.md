# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Apa ini

TopBar: bar kaca di tepi atas layar Windows 11 dengan "pulau" hitam ala Dynamic Island di tengah. Dipakai pribadi oleh satu pengguna, di satu monitor (monitor utama). Laptop pengembangan adalah laptop target, jadi menjalankan app langsung mengubah desktop pengguna: area kerja Windows berkurang setinggi bar.

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
dotnet build TopBar.slnx
dotnet test tests/TopBar.Core.Tests
dotnet test tests/TopBar.Core.Tests --filter "FullyQualifiedName~IslandCoordinatorTests"
dotnet format TopBar.slnx --verify-no-changes
dotnet run --project src/TopBar.App
```

- Hanya satu instance yang bisa jalan (F-BAR-05). Tutup TopBar yang sedang jalan sebelum `dotnet run`, kalau tidak instance baru hanya memfokuskan yang lama lalu keluar.
- Kalau proses dihentikan paksa dari debugger tanpa melepas AppBar, ruang di atas layar bisa tertinggal kosong. Restart Explorer untuk memulihkannya.
- Mode demo (klik kanan bar → Demo mode, F-BAR-09) memutar skenario palsu untuk mengecek tampilan semua keadaan pulau tanpa kejadian nyata.
- Instalasi (PRD 9.4): `dotnet publish` framework-dependent, disalin ke `%LOCALAPPDATA%\Programs\TopBar\`. `TopBar.ClaudeHook` dipublish NativeAOT, jadi butuh workload C++ Build Tools.

## Arsitektur

Proyek dan arah dependensi (PRD 9.1):

| Proyek | Isi | Aturan |
|---|---|---|
| `TopBar.Core` | `IslandCoordinator`, `Scheduler`, `ShelfService`, model, interface provider, format angka/tanggal | Tanpa WPF dan tanpa Win32. Semua logika yang bisa dites ada di sini |
| `TopBar.Platform` | Implementasi provider Windows, `AppBarService`, `BarMaterial`, hotkey, notifikasi, drag-and-drop | Bergantung ke Core |
| `TopBar.Data` | Repository SQLite (Microsoft.Data.Sqlite), `SettingsStore` | Bergantung ke Core |
| `TopBar.App` | WPF + CommunityToolkit.Mvvm: `BarWindow`, `IslandWindow`, `ToolsWindow`, `SettingsWindow`, ViewModel | Komposisi semuanya |
| `TopBar.ClaudeHook` | Console NativeAOT yang dipanggil hook Claude Code | Berdiri sendiri |
| `TopBar.SensorHelper` | Proses admin untuk suhu CPU (LibreHardwareMonitorLib), kirim lewat named pipe | Berdiri sendiri |
| `TopBar.Core.Tests` | xUnit untuk Core dan Data | |

Hal yang butuh membaca beberapa bagian PRD untuk dipahami:

- **Dua jendela, bukan satu.** `BarWindow` mendaftar sebagai AppBar (`SHAppBarMessage`) dan memesan ruang setinggi bar. `IslandWindow` adalah jendela transparan topmost terpisah untuk keadaan mengambang (expanded, kartu, drop zone), supaya pulau yang membesar tidak mengubah ruang AppBar dan jendela lain tidak bergeser. AppBar harus dilepas di semua jalur keluar, termasuk handler exception, dan didaftar ulang saat `TaskbarCreated`, perubahan DPI/resolusi, dan bangun dari sleep.
- **Alur data.** Provider (interface di Core, implementasi di Platform) mengirim snapshot ke state di Core. `IslandCoordinator` memutuskan apa yang tampil di pulau. ViewModel berlangganan state dan memperbarui UI lewat dispatcher WPF. Mode demo hanyalah set implementasi provider palsu, jadi fitur baru selalu butuh interface provider supaya bisa didemokan.
- **`IslandCoordinator` adalah inti aturan.** Ada dua tabel prioritas yang terpisah di PRD 6.3: prioritas tampilan (1 kartu alarm/timer/konflik file … 6 jam saja) dan urutan aktivitas di slot compact (1 cuplikan clipboard … 8 ngecas). Expanded yang dibuka pengguna berada di atas prioritas 4–6 dan di bawah 1–3. Fullscreen dan fase fokus pomodoro mengubah aturan ini. Aktivitas kedua jadi gelembung di kanan pulau. Semua aturan ini wajib punya unit test (F-ISL-03, PRD 13).
- **Waktu.** `Scheduler` menyimpan setiap jadwal sebagai waktu absolut UTC di database, lalu menghitung ulang saat app mulai, bangun dari sleep, dan saat jam atau zona waktu berubah. Timer dan stopwatch harus tetap benar setelah restart.
- **Hak akses.** App utama selalu jalan tanpa hak admin, karena Windows (UIPI) memblokir drag-and-drop dari proses biasa ke proses admin dan Shelf akan rusak. Hanya `TopBar.SensorHelper` yang jalan sebagai admin, didaftarkan lewat Task Scheduler.
- **Kegagalan terisolasi.** Provider yang gagal tidak boleh mematikan bar: modulnya menampilkan "—" dengan alasan di tooltip, lalu dicoba lagi setelah 5 detik, 30 detik, kemudian tiap 2 menit (PRD 11).
- **Penyimpanan.** `%LOCALAPPDATA%\TopBar\`: `topbar.db` (SQLite WAL, migrasi lewat tabel `schema_version`, backup harian 7 salinan), `settings.json`, `logs\`. Thumbnail clipboard dan semua data sesi Claude hanya di memori, tidak pernah ditulis ke disk atau log.
- **Integrasi Claude (Fase 3).** Hook Claude Code → `TopBar.ClaudeHook.exe <Event>` → named pipe `\\.\pipe\TopBar.Claude`, dilengkapi `~/.claude/sessions/*.json` yang dipantau. Helper tidak boleh menulis ke stdout, selalu keluar dengan kode 0, dan harus selesai ≤ 50 ms p95. Penggabung `~/.claude/settings.json` wajib membuat cadangan, tidak menggandakan entri, dan tidak menyentuh hook lain: `settings.json` pengguna sudah berisi hook wmux. JSON yang tidak valid dibiarkan apa adanya.

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

Hal yang harus diverifikasi di laptop pengguna sebelum diimplementasikan (Fase 3): hook `Notification` untuk prompt izin di Claude Desktop, `SHUTDOWN_INSTALL_UPDATES` beserta deteksi update yang menunggu, dan `SetSuspendState` di laptop Modern Standby.
