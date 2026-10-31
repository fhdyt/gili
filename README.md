# Gili

Bar kaca di tepi atas layar Windows 11, dengan "pulau" hitam ala Dynamic Island di tengahnya. Nama Gili diambil dari kata Indonesia untuk pulau kecil, dan maskot sepasang mata di pulau memakai nama yang sama.

Saat tidak ada yang berjalan, pulau berisi mata Gili yang mengikuti kursor dan sesekali berkedip. Saat ada aktivitas (musik, timer, progres copy, sesi Claude Code), Gili bergeser ke kiri sebagai lingkaran kecil dan pulau menampilkan aktivitas itu. Klik pulau, dan pulau lepas dari bar menjadi panel mengambang berisi halaman Media, Timer, Alarms, Calendar, Todo, Notes, Shelf, Claude, dan System.

> **Status:** pra-rilis, versi `0.1.1` (lihat `Directory.Build.props`). Gili sedang di Fase 4 dari 6. Rilis publik gratis lewat Microsoft Store dan installer bertanda tangan di GitHub menyusul setelah Fase 4 (Fase 5); Fase 6 berisi fitur yang dirilis sebagai update sesudahnya. Repo ini akan dibuat publik; installer dan release notes ada di GitHub Releases repo ini. Sampai saat itu, Gili hanya bisa dipasang dari kode sumber (lihat [Instalasi](#instalasi)).

## Daftar isi

- [Fitur](#fitur)
- [Kebutuhan sistem](#kebutuhan-sistem)
- [Instalasi](#instalasi)
- [Memakai Gili](#memakai-gili)
- [Tutorial: integrasi Claude Code](#tutorial-integrasi-claude-code)
- [Tutorial lain](#tutorial-lain)
- [Data, backup, dan privasi](#data-backup-dan-privasi)
- [Menutup dan melepas Gili](#menutup-dan-melepas-gili)
- [Masalah yang sering muncul](#masalah-yang-sering-muncul)
- [Pengembangan](#pengembangan)

## Fitur

Bar dibagi tiga bagian.

**Kiri:** tombol Start, ikon launcher (Timer, Alarms, Todo, Notes, Clipboard), ikon Shelf, ikon Claude, lalu ikon aplikasi yang sedang terbuka. Aplikasi aktif selalu menampilkan namanya; aplikasi lain cukup ikon.

**Tengah, pulau:**
- Maskot Gili saat tidak ada aktivitas.
- Aktivitas di slot compact: media yang sedang diputar, timer dan pomodoro, progres copy/move/delete Explorer, status sesi Claude Code, ngecas. Aktivitas kedua muncul sebagai gelembung di kanan pulau.
- Kartu notifikasi untuk alarm, timer habis, dan pengingat todo, dengan suara dan toast Windows.
- Halaman yang dibuka dari menu tab ikon: Media, Timer, Alarms, Calendar, Todo, Notes, Shelf, System (grafik CPU/RAM/GPU 60 detik dan sisa disk).
- Pulau kontrol untuk Wi‑Fi, Bluetooth, volume, brightness, dan power, masing-masing dibuka dari ikonnya di kanan bar.
- Titik kamera dan mikrofon di samping pulau saat ada aplikasi yang memakainya.

**Kanan:** cuaca, kecepatan jaringan, CPU, RAM, GPU, disk, Wi‑Fi, Bluetooth, volume, brightness, tombol Tray (ikon tersembunyi Windows), jam dan tanggal, Settings, dan Power. Setiap modul bisa dimatikan, dan modul sistem bisa diurutkan ulang.

**Lainnya:**
- Jendela Tools untuk mengelola timer, pomodoro, stopwatch, alarm, dan todo.
- Note ditulis langsung di pulau. Satu note bisa diseret keluar menjadi jendela kecil yang selalu di atas.
- Shelf: seret file ke bar atau pulau untuk menyimpannya sementara, lalu seret keluar ke folder tujuan.
- Hide Windows taskbar: Gili bisa menggantikan taskbar Windows sehari-hari. Taskbar dipulihkan setiap kali Gili keluar, termasuk setelah crash.
- Bar position Bottom, setelah taskbar Windows disembunyikan.
- Demo mode: skenario palsu ±30 detik untuk melihat semua keadaan pulau.

**Belum ada, menyusul di Fase 4 (sebelum rilis pertama):** halaman Clipboard dan riwayat clipboard, hotkey global (`Ctrl+Alt+N`, `Ctrl+Alt+V`, `Ctrl+Alt+Space`), dan pilihan kota cuaca (sementara Jakarta). **Menyusul sebagai update setelah rilis (Fase 6):** lirik lagu, notifikasi Windows di pulau, progres operasi file Explorer, dan menyalakan Do Not Disturb Windows saat fase fokus. Baris Settings untuk fitur ini tampil abu-abu dengan keterangan `Coming in a later version`.

Semua teks di app berbahasa Inggris, tanpa pilihan bahasa.

## Kebutuhan sistem

- Windows 11 (Windows 10 tidak didukung).
- Monitor utama saja. Monitor lain tidak mendapat bar.
- Tanpa hak admin. Gili tidak pernah meminta UAC.

Gili memesan ruang setinggi bar (default 32 px) di tepi atas layar, jadi jendela yang di-maximize mulai di bawah bar.

## Instalasi

### Untuk pengguna umum

Belum tersedia. Setelah rilis publik, ada dua jalur yang dibangun dari kode yang sama:

| | Microsoft Store | Installer GitHub |
|---|---|---|
| Bentuk | Paket MSIX | `Gili-Setup-<versi>.exe`, terpasang di `%LOCALAPPDATA%\Programs\Gili\` |
| Tanda tangan | Microsoft | Sertifikat code signing milik pengembang |
| Update | Lewat Store | Gili memeriksa update tiap 24 jam |
| Butuh .NET terpasang | Tidak | Tidak |

Installer GitHub adalah satu file `.exe` (dibangun dengan Inno Setup): unduh, klik dua kali, selesai, tanpa UAC. Installer ini baru dibuat setelah semua fitur Fase 1–4 selesai (awal Fase 5). Sebelum rilis publik, versi tanpa tanda tangan dibagikan dulu ke beberapa teman penguji; Windows akan menampilkan "Windows protected your PC" sekali, lalu pilih **More info → Run anyway**. Rinciannya di PRD 9.4.

### Dari kode sumber

Yang perlu dipasang dulu:

| Alat | Untuk apa |
|---|---|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Build semua proyek |
| PowerShell 7 (`pwsh`) | Menjalankan skrip di `scripts/` |
| Visual Studio Build Tools dengan workload **Desktop development with C++** | `Gili.ClaudeHook` dipublish NativeAOT dan butuh linker MSVC |
| Windows SDK (hanya untuk paket MSIX) | `makeappx.exe` |

#### Pilihan A: salinan terpasang (disarankan untuk dipakai sehari-hari)

```powershell
pwsh scripts/publish.ps1
```

Skrip ini:
1. Meminta Gili yang sedang jalan keluar lewat `--quit`, supaya ruang AppBar-nya dilepas.
2. Mempublish build Release ke `%LOCALAPPDATA%\Programs\Gili\`, bersama `Gili.ClaudeHook.exe`.
3. Menjalankan `Gili.exe` dari folder itu.

Salinan ini berperilaku seperti versi installer GitHub: saat pertama jalan ia menulis entri autostart di `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (bisa dimatikan lewat Settings → General → **Start with Windows**). Build ini framework-dependent, jadi butuh .NET 10 Desktop Runtime, yang sudah ikut terpasang bersama SDK.

Jalankan skrip yang sama lagi untuk memperbarui salinan setelah menarik perubahan baru.

#### Pilihan B: paket MSIX (versi Store)

Nyalakan **Developer Mode** dulu (Settings → System → For developers), lalu:

```powershell
pwsh scripts/package.ps1            # hanya membangun artifacts/msix/Gili-<versi>.msix
pwsh scripts/package.ps1 -Install   # membangun lalu mendaftarkan paketnya di laptop ini
```

`-Install` mendaftarkan layout paket lewat `Add-AppxPackage -Register`, tanpa admin dan tanpa sertifikat. Beda versi MSIX dengan salinan terpasang:
- Autostart lewat `StartupTask` di manifest, bukan `HKCU\...\Run`. Kalau dimatikan di Task Manager → Startup apps, hanya Task Manager yang bisa menyalakannya lagi.
- Data di `%LOCALAPPDATA%\Packages\Gili_*\LocalState\`, bukan `%LOCALAPPDATA%\Gili\`.
- Hook Claude memanggil helper lewat app execution alias `gili-claude-hook.exe`.

Hanya satu Gili yang bisa jalan sekaligus, jadi jangan menjalankan salinan terpasang dan paket MSIX bersamaan.

#### Pilihan C: langsung dari kode (pengembangan)

```powershell
dotnet run --project src/Gili.App
```

Tutup dulu Gili yang sedang jalan: menjalankan Gili kedua hanya membuka Settings di instance lama lalu keluar. `dotnet run` tidak pernah mendaftarkan autostart.

## Memakai Gili

### Menu bar

Klik kanan di area kosong bar:

| Item | Fungsi |
|---|---|
| Open Tools | Jendela Tools: timer, pomodoro, stopwatch, alarm, todo |
| Settings | Jendela Settings |
| Demo mode | Memutar skenario palsu ±30 detik |
| Quit | Menutup Gili dan melepas ruang di atas layar |

Settings juga terbuka dari ikon roda gigi di kanan bar, atau dengan menjalankan Gili lagi saat sudah berjalan.

### Pulau

- **Klik pulau** atau ikon launcher di kiri untuk membuka halamannya. Klik ikon halaman yang sedang terbuka untuk menutup pulau.
- **Menutup pulau:** klik di luar, tekan Esc, atau jauhkan mouse dari pulau selama 3 detik. Penutupan otomatis ditunda selama kolom teks atau editor note sedang fokus.
- **Klik CPU, RAM, GPU, atau disk** di kanan bar untuk membuka halaman System.
- **Klik jam** untuk membuka Calendar.

### Settings

Semua perubahan berlaku langsung, tanpa tombol Save.

| Bagian | Isi |
|---|---|
| General | Bar position, Hide Windows taskbar, 12-hour clock, Show seconds, Start with Windows |
| Appearance | Bar opacity, Glass (Off / Light / Strong), Bar height, Island height, Ambient glow, Ambient motion |
| Left side | Saklar Start, Timer, Alarms, Todo, Notes, Clipboard, Shelf, Claude |
| Island | Show Gili, dan saklar tiap halaman pulau |
| Right side | Saklar dan urutan modul, Battery alert, Unplug reminder |
| Claude | Show Claude Code sessions |
| Sounds | Timer sound, Alert volume, Gradually increase |

Mematikan ikon di **Left side** ikut menyembunyikan halaman dengan nama yang sama di pulau. Modul sistem di **Right side** bisa diurutkan dengan menyeret pegangan titik-titik di kiri barisnya, atau Alt + panah atas/bawah.

## Tutorial: integrasi Claude Code

Gili bisa menampilkan status sesi Claude Code di pulau, supaya kamu tahu kapan Claude sedang bekerja, butuh jawaban, atau sudah selesai tanpa membuka jendelanya. Ini berlaku untuk tab **Code** di Claude Desktop dan `claude` di terminal (Windows Terminal, wmux, dan lainnya). Tab Chat di Claude Desktop tidak didukung karena tidak mengirim sinyal apa pun keluar aplikasinya.

Integrasi ini **mati secara default** karena menyalakannya mengubah file konfigurasi Claude Code milikmu.

### Cara kerjanya

```
Claude Code ──hook──▶ Gili.ClaudeHook.exe <Event> ──named pipe \\.\pipe\Gili.Claude──▶ Gili
                                    ~/.claude/sessions/*.json ──dipantau──▶ Gili
```

- Claude Code memanggil `Gili.ClaudeHook.exe` di setiap hook. Helper ini program NativeAOT kecil yang meneruskan satu baris kejadian ke Gili lewat named pipe, lalu keluar. Ia tidak menulis apa pun ke stdout, selalu keluar dengan kode 0, dan selesai dalam ±16 ms (p95) saat Gili jalan. Kalau Gili tidak jalan, helper keluar tanpa efek dan Claude bekerja seperti biasa.
- Gili juga membaca file sesi di `~/.claude/sessions/` untuk nama sesi dan untuk sesi yang sudah berjalan sebelum Gili mulai.
- Isi prompt tidak pernah dibaca. Semua data sesi hanya di memori, tidak ditulis ke database atau log.

### Menyalakan

1. Pastikan Gili dipasang lewat `scripts/publish.ps1` atau `scripts/package.ps1 -Install`. Helper `Gili.ClaudeHook.exe` ikut terpasang di sana. Build `dotnet run` memakai helper dari salinan terpasang kalau ada.
2. Buka **Settings → Claude**, lalu nyalakan **Show Claude Code sessions**.
3. Baris di bawah saklar menampilkan `Applies to Claude sessions started from now on`. Hook hanya berlaku untuk sesi Claude yang dimulai **setelah** saklar dinyalakan, jadi mulai sesi baru (atau restart sesi lama).
4. Kirim satu prompt. Baris status berubah menjadi `Connected · last event …`, dan pulau menampilkan nama sesi dengan waktu berjalannya.

### Yang diubah di `~/.claude/settings.json`

Sebelum menulis, Gili menyalin file aslimu ke `~/.claude/settings.json.gili-backup`. Lalu Gili menambahkan tujuh entri hook. Contoh untuk salinan terpasang (path mengikuti folder pengguna):

```json
{
  "hooks": {
    "SessionStart": [
      { "hooks": [ { "type": "command", "command": "\"C:/Users/<kamu>/AppData/Local/Programs/Gili/Gili.ClaudeHook.exe\" SessionStart", "timeout": 2 } ] }
    ],
    "UserPromptSubmit": [ … ],
    "PreToolUse": [
      { "matcher": "AskUserQuestion|ExitPlanMode", "hooks": [ … ] }
    ],
    "Notification": [ … ],
    "PostToolUse": [
      { "matcher": "*", "hooks": [ … ] }
    ],
    "Stop": [ … ],
    "SessionEnd": [ … ]
  }
}
```

Aturan penggabungnya:
- Hook lain yang sudah ada (misalnya hook wmux) dan field lain seperti `statusLine` atau `mcpServers` tidak disentuh.
- Menyalakan dua kali tidak menggandakan entri. Entri Gili lama selalu diganti yang baru, jadi kalau Gili dipindah, path helper ikut diperbarui saat Gili mulai.
- Kalau `settings.json` bukan JSON yang valid, Gili tidak mengubahnya sama sekali dan Settings menampilkan `Can't read Claude settings`.
- Versi MSIX menulis path alias `%LOCALAPPDATA%\Microsoft\WindowsApps\gili-claude-hook.exe`, karena folder paket di `WindowsApps` berubah setiap update.

### Yang tampil di pulau

| Keadaan | Di slot pulau |
|---|---|
| Working | Ikon Claude oranye berdenyut, nama sesi, waktu berjalan (`2:14`). Beberapa sesi: `2 sessions working` |
| Needs input | Ikon Claude kuning, nama sesi, `Needs input`. Muncul saat Claude bertanya, meminta persetujuan rencana, atau meminta izin menjalankan tool |
| Done | Centang hijau, nama sesi, `Done · 4m 12s`, selama 4 detik |

Nama sesi diambil dari judul sesi di Claude Desktop. Sesi terminal tanpa judul memakai nama foldernya. Ikon Claude di kiri bar ikut menandai status: titik kuning saat ada sesi yang butuh jawaban, dan berdenyut saat ada sesi yang bekerja. Menekan Esc di Claude mengembalikan sesi ke diam tanpa tanda Done.

Kartu `Claude needs your input` dengan bunyi Ping, gelembung hijau untuk sesi selesai yang belum dilihat, halaman Claude, dan timer jendela 5 jam menyusul di sub-fase berikutnya.

### Mematikan

Matikan **Show Claude Code sessions** di Settings → Claude. Gili hanya menghapus entrinya sendiri dari `~/.claude/settings.json`; array event dan objek `hooks` yang jadi kosong ikut dibuang, dan hook lain tetap utuh. Matikan saklar ini **sebelum** menghapus Gili, supaya hook tidak menunjuk ke helper yang sudah hilang.

Kalau Gili sudah terhapus duluan, Claude tetap bekerja (hook yang gagal tidak menghentikannya). Bersihkan dengan menghapus entri hook yang command-nya memuat `Gili.ClaudeHook` atau `gili-claude-hook` dari `~/.claude/settings.json`, atau kembalikan dari `settings.json.gili-backup` kalau sejak itu kamu tidak mengubah file tersebut.

### Status dan error di Settings

| Pesan | Artinya |
|---|---|
| `Waiting for the first Claude session` | Hook terpasang, belum ada kejadian sejak Gili mulai |
| `Applies to Claude sessions started from now on` | Saklar baru dinyalakan; mulai sesi Claude baru |
| `Connected · last event 2 min ago` | Gili menerima kejadian dari hook |
| `Can't read Claude settings` | `~/.claude/settings.json` bukan JSON yang valid; file dibiarkan apa adanya |
| `Can't save Claude settings` | File terkunci atau tidak bisa ditulis |
| `Hook helper not found · Reinstall` | `Gili.ClaudeHook.exe` tidak ada di samping `Gili.exe`; jalankan `pwsh scripts/publish.ps1` lagi |

## Tutorial lain

### Menyimpan file sementara di Shelf

1. Seret file atau folder dari Explorer ke bar atau pulau. Pulau berubah menjadi drop zone hijau.
2. Lepas. Satu file menjadi satu tile; beberapa file yang dilepas bersamaan menjadi satu bundel (`12 files`).
3. Buka folder tujuan, lalu seret tile dari halaman Shelf ke sana. Explorer menawarkan Copy atau Move; tahan Shift untuk memaksa Move.

Shelf menyimpan referensi ke file asli, bukan salinannya, maksimal 8 item, dan isinya tetap ada setelah restart. File yang dipindahkan keluar otomatis hilang dari Shelf. File dari jendela yang jalan sebagai admin tidak bisa diterima, karena Windows memblokir drag-and-drop dari proses admin.

### Note yang mengambang

Buka halaman Notes, klik **New note**, lalu tulis. Note tersimpan otomatis. Untuk menempelkannya di atas jendela lain, seret kartunya (atau grip di tepi atas editor) keluar dari pulau. Seret kembali ke pulau untuk menyimpannya lagi di grid.

### Menggantikan taskbar Windows

1. Settings → General → nyalakan **Hide Windows taskbar**. Taskbar Windows disembunyikan dan ruangnya kembali ke jendela lain. Tombol Windows di keyboard tetap membuka Start menu.
2. Pakai tombol Start di kiri bar, tombol Tray untuk ikon tersembunyi, dan pulau kontrol di kanan untuk Wi‑Fi, Bluetooth, volume, brightness, dan power.
3. Opsional: **Bar position** → `Bottom` untuk memindah bar ke tepi bawah. Pilihan ini hanya aktif selama taskbar Windows disembunyikan.

Taskbar Windows dikembalikan ke keadaan aslinya saat saklar dimatikan, saat Quit, saat crash, dan saat Gili mulai lagi setelah dimatikan paksa.

### Mode demo

Klik kanan bar → **Demo mode**. Gili memutar skenario palsu sekitar 30 detik (musik, timer, progres file, sesi Claude, kartu alarm, dan lainnya) tanpa menyentuh data aslimu. Berguna untuk melihat tampilan semua keadaan pulau.

## Data, backup, dan privasi

| Instalasi | Folder data |
|---|---|
| `scripts/publish.ps1`, `dotnet run`, installer GitHub | `%LOCALAPPDATA%\Gili\` |
| Paket MSIX / Store | `%LOCALAPPDATA%\Packages\Gili_*\LocalState\` |

Isi folder data:
- `gili.db`: note, todo, alarm, timer, stopwatch, dan Shelf (SQLite, mode WAL, setiap commit di-fsync).
- `settings.json`: semua pengaturan.
- `backups\`: backup harian `gili.db`, 7 salinan.
- `logs\`: log harian, 7 hari. Buka foldernya kalau perlu melaporkan crash.

Salinan `gili.db` dan `settings.json` juga ditulis ke `%USERPROFILE%\.gili\backups\` saat start, tiap jam, dan saat Quit. Folder ini di luar folder data mana pun, jadi note dan pengaturan kembali sendiri setelah Gili di-uninstall lalu dipasang ulang, juga saat pindah antara versi GitHub dan Store.

Privasi:
- Tanpa telemetri, tanpa akun, tanpa sinkronisasi cloud.
- Data sesi Claude dan thumbnail clipboard hanya di memori, tidak pernah ditulis ke disk atau log.
- Satu-satunya akses internet saat ini adalah cuaca. Mematikan modul Weather menghentikannya.

## Menutup dan melepas Gili

**Menutup:** klik kanan bar → **Quit**. Dari terminal:

```powershell
& "$env:LOCALAPPDATA\Programs\Gili\Gili.exe" --quit
```

`--quit` (dari exe Gili mana pun) meminta instance yang sedang jalan keluar lewat jalur Quit biasa, jadi ruang AppBar dilepas dan taskbar Windows dipulihkan. **Jangan pakai `taskkill`**: perintah itu ikut menutup jendela tersembunyi WPF dan bisa membuat proses tetap hidup tanpa bar.

**Melepas salinan terpasang** (belum ada uninstaller sampai Fase 5):
1. Matikan **Show Claude Code sessions** di Settings → Claude, dan **Hide Windows taskbar** di Settings → General.
2. Matikan **Start with Windows** supaya entri `HKCU\...\Run` dihapus.
3. Quit Gili, lalu hapus folder `%LOCALAPPDATA%\Programs\Gili\`.
4. Opsional: hapus `%LOCALAPPDATA%\Gili\` dan `%USERPROFILE%\.gili\` untuk membuang note dan pengaturan.

**Melepas paket MSIX:** lakukan langkah 1 di atas, lalu uninstall Gili dari Settings → Apps → Installed apps. Windows menghapus folder data paket; salinan di `%USERPROFILE%\.gili\` tetap ada.

## Masalah yang sering muncul

| Gejala | Solusi |
|---|---|
| Ada ruang kosong di atas layar setelah Gili dihentikan paksa | Restart Explorer (Task Manager → Windows Explorer → Restart). Ruang AppBar tertinggal karena Gili tidak sempat melepasnya |
| Taskbar Windows tidak muncul lagi setelah Gili mati paksa | Jalankan Gili lagi; saat start ia memulihkan taskbar dari `taskbar-state.json` di folder data. Atau matikan auto-hide di Settings Windows → Personalization → Taskbar |
| Menjalankan Gili hanya membuka Settings | Sudah ada Gili yang jalan. Tutup dulu dengan Quit atau `--quit` |
| Nama Wi‑Fi tidak tampil, hanya ikon | Izinkan akses lokasi untuk Gili di Settings → Privacy & security → Location |
| Modul menampilkan `—` | Sumber datanya gagal dibaca. Alasannya ada di tooltip, dan Gili mencoba lagi setelah 5 detik, 30 detik, lalu tiap 2 menit |
| Sesi Claude tidak muncul di pulau | Cek baris status di Settings → Claude. Ingat hook hanya berlaku untuk sesi yang dimulai setelah saklar dinyalakan |
| `Publishing Gili.ClaudeHook failed` saat `publish.ps1` | Pasang workload **Desktop development with C++** di Visual Studio Build Tools |

Kalau Gili crash, ia menulis log, melepas AppBar, memulihkan taskbar, lalu restart sendiri sekali. Crash kedua dalam satu menit menghentikan Gili.

## Pengembangan

```powershell
dotnet build Gili.slnx
dotnet test tests/Gili.Core.Tests
dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandCoordinatorTests"
dotnet format Gili.slnx --verify-no-changes
dotnet run --project src/Gili.App
```

Build Debug punya item **Simulate crash** di menu klik kanan bar untuk menguji restart otomatis.

| Proyek | Isi |
|---|---|
| `src/Gili.Core` | Logika yang bisa dites: `IslandCoordinator`, `Scheduler`, `ShelfService`, pegas, model, interface provider. Tanpa WPF dan Win32 |
| `src/Gili.Platform` | Implementasi provider Windows, `AppBarService`, material bar, drag-and-drop, pipe Claude |
| `src/Gili.Data` | SQLite dan `SettingsStore` |
| `src/Gili.App` | WPF: `BarWindow`, `IslandWindow`, `ToolsWindow`, `SettingsWindow`, ViewModel |
| `src/Gili.ClaudeHook` | Helper NativeAOT yang dipanggil hook Claude Code |
| `tests/Gili.Core.Tests` | xUnit untuk Core dan Data |

Dokumen acuan:
- PRD: [`docs/superpowers/specs/2026-10-25-top-taskbar-prd.md`](docs/superpowers/specs/2026-10-25-top-taskbar-prd.md). Setiap fitur punya ID (`F-BAR-01`, `F-ISL-07`, `F-CLD-03`, …) dan fase.
- Mockup: [`docs/mockup/top-taskbar-mockup.html`](docs/mockup/top-taskbar-mockup.html), acuan tampilan dan gerak.
- Desain dan rencana per sub-fase: `docs/superpowers/specs/` dan `docs/superpowers/plans/`.
- Aturan kerja di repo ini (arsitektur, bahasa, keputusan final): [`CLAUDE.md`](CLAUDE.md).

Laptop pengembangan juga laptop uji utama: menjalankan Gili langsung mengubah desktop, karena area kerja Windows berkurang setinggi bar.
