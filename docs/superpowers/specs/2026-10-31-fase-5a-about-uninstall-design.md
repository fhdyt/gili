# Fase 5a: Settings → About dan `--prepare-uninstall` (desain)

Tanggal: 2026-10-31. Fitur: Settings → About (PRD 6.5), `Gili.exe --prepare-uninstall` dan tombol **Prepare to uninstall** (PRD 9.4, F-PUB-03). Pembelian sertifikat OV ditunda pengguna, jadi Fase 5 mulai dari bagian yang tidak butuh tanda tangan. Pengguna minta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Kondisi awal

- Kriteria selesai Fase 4 ("semua fitur best-effort punya cadangan") sudah terpenuhi oleh fase-fase sebelumnya: cuaca abu-abu saat offline (`WeatherStale`), hotkey yang bentrok menampilkan `Used by another app`, pencarian kota menampilkan `Can't search right now…`, provider gagal menampilkan "—" dengan jadwal coba ulang (PRD 11). Fase 5 bisa dimulai.
- About di Settings dan mockup masih placeholder: dua saklar `Updates` dan `Open logs folder` yang nonaktif. PRD 6.5 meminta versi dan jenis instalasi, baris update, tautan Privacy policy dan Release notes, Open logs folder, dan Prepare to uninstall (versi Store).

## Pendekatan

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. Dua jenis baris baru di katalog: `Info` (teks di kanan) dan `Action` (tombol) (dipilih)** | About tetap dibangun dari `SettingsCatalog` seperti bagian lain; baris yang belum bisa tetap nonaktif dengan `Coming in a later version` | Dua jenis baris lagi di template |
| B. Halaman About buatan tangan di XAML | Bebas tata letak | Menyimpang dari pola satu katalog untuk semua bagian; mockup juga memakai baris |

## Isi About

| Baris | Jenis | Isi | Kapan aktif |
|---|---|---|---|
| `Version` | Info | `0.1.1 · GitHub`, `0.1.1 · Microsoft Store`, atau `0.1.1 · Development build` **(keputusan Claude: nama untuk `dotnet run`)** | Selalu |
| `Updates` | Info | `Coming in a later version` | Setelah sertifikat (cek GitHub butuh penerbit yang sama) dan listing Store (`StoreContext`) |
| `Privacy policy` | Action `Open` | — | Setelah halaman online (F-PUB-08) |
| `Release notes` | Action `Open` | — | Setelah `gili-releases` ada |
| `Open logs folder` | Action `Open` | Keterangan `Logs from the last 7 days, for bug reports` **(keputusan Claude)**; membuka folder log di Explorer (versi Store: folder paket) | Selalu |
| `Prepare to uninstall` | Action `Prepare` | Keterangan `Removes Gili's Claude hooks and brings the Windows taskbar back, then quits. Uninstall Gili in Windows Settings next` | Hanya tampil di versi Store (PRD 9.4); salinan GitHub memakai uninstaller |

Tombol memakai tutup tombol yang sama dengan hotkey dan kota (`.kcap`), jadi tidak ada gaya baru.

## Prepare to uninstall

Satu set langkah dipakai dua jalur. Setiap langkah dijalankan walaupun langkah sebelumnya gagal; kegagalan dicatat di log (PRD 9.4: "selalu keluar dengan kode 0").

**`Gili.exe --prepare-uninstall`** (dipanggil uninstaller Inno Setup dari folder instalasi, Fase 5b):
1. Minta instance yang jalan keluar lewat jalur Quit (`--quit`), lalu tunggu mutex F-BAR-05 lepas, maks. 5 detik. Jalur Quit sudah melepas AppBar dan memulihkan taskbar.
2. Lepas hook Gili dari `~/.claude/settings.json` (aturan F-CLD-01: cadangan `.gili-backup`, hook lain tidak disentuh, JSON tidak valid dibiarkan). File yang tidak ada dibiarkan tidak ada.
3. Pulihkan taskbar dari `taskbar-state.json` kalau masih ada (run yang dimatikan paksa atau instance yang tidak keluar dalam 5 detik).
4. Hapus entri `HKCU\…\Run\Gili`.
5. Keluar dengan kode 0 tanpa jendela apa pun.

**Tombol Prepare to uninstall** (versi Store): langkah 2 di dalam proses yang jalan, lalu Quit (yang melepas AppBar dan memulihkan taskbar), lalu membuka Settings Windows → Installed apps (`ms-settings:appsfeatures`) **(keputusan Claude: langkah berikutnya langsung di depan pengguna)**. Tanpa konfirmasi **(keputusan Claude)**: semuanya bisa dibalik dengan membuka Gili lagi, yang menulis hook dan menyembunyikan taskbar lagi sesuai setelannya. StartupTask tidak disentuh; Windows menghapusnya bersama paket.

## Kode

- Core: `AboutInfo.VersionLine(version, kind)` dan `UninstallSteps.Run(steps, log)` (semua langkah jalan, kegagalan dicatat), dengan unit test. `SettingKind.Info` dan `SettingKind.Action`, baris About di `SettingsCatalog`, dan `SettingsCatalog.RowsOf` menyembunyikan `Prepare to uninstall` di luar versi Store.
- Platform: `ClaudeSettingsFile.Edit` diambil dari `ClaudeService.Edit` supaya `--prepare-uninstall` (tanpa `ClaudeService`) memakai aturan cadangan yang sama; `Autostart.Remove()`.
- App: `SingleInstance.WaitForExit`, cabang `--prepare-uninstall` di `OnStartup`, aksi About di `SettingsViewModel` (`AboutActions` dari `App`).

## Anggaran (PRD 8.1)

Tanpa kerja di latar belakang: semua dijalankan hanya saat tombol diklik atau perintah dipanggil.

## Mockup dan PRD

- Mockup: About memakai jenis baris `info` dan `btn` dengan isi tabel di atas.
- PRD 6.5: baris About dirinci seperti tabel di atas (teks `Development build`, keterangan Open logs folder dan Prepare to uninstall, baris yang menunggu sertifikat dan halaman online).

## Hasil verifikasi 5a

- Build dan `dotnet format` bersih, 981 unit test lulus (baru: `AboutInfoTests` 4, `UninstallStepsTests` 2, `SettingsCatalogAboutTests` 6).
- Salinan terpasang, `Gili.exe --prepare-uninstall`: keluar dengan kode 0 dalam ±0,6 detik, Gili yang jalan keluar lewat Quit, 8 hook Gili hilang dari `~/.claude/settings.json` sementara 13 hook wmux utuh, entri `Run\Gili` terhapus. Membuka Gili lagi menulis ulang hook dan entri Run.
- Temuan: proses lama masih hidup sesaat setelah mutex lepas, padahal uninstaller langsung menghapus `Gili.exe`. Perbaikan: perintah juga menunggu proses Gili lain selesai dalam batas 5 detik yang sama; setelahnya tidak ada proses tersisa.
- Settings → About lewat UIA: `Version` = `0.1.1 · GitHub`; Updates, Privacy policy, Release notes nonaktif dengan `Coming in a later version`; `Open logs folder` (Invoke UIA) membuka folder log di Explorer; `Prepare to uninstall` tidak tampil di salinan GitHub.
- Belum teruji: tombol Prepare to uninstall di versi Store (butuh `package.ps1 -Install`; diuji bersama installer di 5b).

## Fase 5c: dua salinan terpasang (F-PUB-07)

Perpindahan data GitHub ↔ Store sudah ditangani salinan di `%USERPROFILE%\.gili` (desain data tetap 2026-10-31), dan mutex F-BAR-05 dipakai bersama. Yang tersisa dari F-PUB-07 dan PRD 9.4 adalah catatan di Settings.

- Catatan `Another copy of Gili is installed. Uninstall one of them in Windows Settings › Apps` di bawah baris Version **(keputusan Claude: teks PRD ditambah saran yang PRD minta)**.
- Versi GitHub: paket pengguna bernama `Gili` (paket dev) atau berakhiran `.Gili` (nama yang dipesan Partner Center, F-PUB-02) lewat `PackageManager.FindPackagesForUser`. Versi Store: `Gili.exe` di `%LOCALAPPDATA%\Programs\Gili` (bacaan file MSIX diteruskan ke folder asli). `dotnet run` tidak memeriksa. Pencarian jalan di `Task.Run` saat Settings dibuka; tanpa kerja latar lain (PRD 8.1).
- Aturan di `AboutInfo.AnotherCopyNote` dan `AboutInfo.IsGiliPackage`, 10 unit test.

### Hasil verifikasi 5c dan tombol Store

- 992 unit test lulus, format bersih.
- `package.ps1 -Install` lalu Settings Store lewat UIA: `0.1.1 · Microsoft Store`, catatan Another copy (salinan GitHub ada), baris Prepare to uninstall tampil. Invoke **Prepare**: Gili keluar, 8 hook Gili lepas dan 13 hook wmux utuh, Settings Windows terbuka; log paket `Prepared to uninstall`.
- Salinan GitHub dengan paket masih terdaftar: catatan Another copy tampil. Setelah `Remove-AppxPackage`: catatan hilang.
- Temuan dan perbaikan: pencarian yang sudah selesai saat Settings dibuat melanjutkan `ShowAnotherCopyAsync` sebelum konstruktor mengisi bagian terpilih (`NullReferenceException`, tercatat sebagai unobserved task exception). Sekarang pencarian disambung di akhir konstruktor.
- Paket dev dilepas lagi; salinan harian `publish.ps1` jalan dengan entri Run dan hook.
