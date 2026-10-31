# Fase 5d: cek update (desain)

Tanggal: 2026-11-01. Fitur: F-PUB-04, PRD 9.4 baris "Update", PRD 11 (`Couldn't check for updates`, `Update couldn't be verified`, `RegisterApplicationRestart`). Sertifikat OV ditunda, jadi kodenya dibuat sekarang tetapi cek GitHub baru menyala di salinan yang `Gili.exe`-nya bertanda tangan. Pengguna minta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Siapa yang memeriksa

| Salinan | Sumber | Keterangan baris Updates sampai ada hasil |
|---|---|---|
| GitHub, `Gili.exe` bertanda tangan | `https://api.github.com/repos/fhdyt/gili-releases/releases/latest` **(keputusan Claude: pemilik yang sama dengan repo kode)** | `Not checked yet` |
| GitHub, tanpa tanda tangan (build untuk teman, `publish.ps1`) | Tidak ada (PRD 9.4: build untuk teman tanpa cek update) | `This test build doesn't check. Install a newer Gili-Setup over it` |
| Microsoft Store | `StoreContext.GetAppAndOptionalStorePackageUpdatesAsync` | `Not checked yet` |
| `dotnet run` | Tidak ada | `Only the installed copy checks for updates` |

## Jadwal

- Cek pertama 60 detik setelah start **(keputusan Claude: start tetap ringan)**, lalu setiap jam diperiksa apakah sudah 24 jam sejak cek terakhir yang berhasil (menangani sleep tanpa timer 24 jam yang meleset). Waktu cek terakhir yang berhasil disimpan di `settings.json` (`LastUpdateCheck`), jadi restart tidak memicu cek ulang dan aturan 3 hari tetap benar.
- Gagal (offline, GitHub atau Store tidak menjawab): diam, dicoba di jam berikutnya. Baris Updates baru menampilkan `Couldn't check for updates` bila cek terakhir yang berhasil lebih dari 3 hari lalu (PRD 11). Belum pernah berhasil sama sekali: tetap `Not checked yet`.

## Baris di Settings → About

| Keadaan | Baris Updates | Release notes |
|---|---|---|
| Belum ada hasil | teks `Not checked yet` | `Coming in a later version` (belum ada halaman yang diketahui) |
| Terbaru | teks `Up to date` | membuka halaman rilis terbaru |
| Ada versi baru | keterangan `Update available · 0.2.0`, tombol **Install update**; titik biru 6 px di tombol Settings bar | membuka halaman rilis versi itu |
| Memasang | tombol `Installing…` nonaktif | sama |
| Tanda tangan tidak cocok | keterangan `Update couldn't be verified`, tanpa tombol; installer dihapus | membuka halaman rilis untuk unduh manual |
| Gagal > 3 hari | teks `Couldn't check for updates` | halaman terakhir yang diketahui |

## Install update

- **GitHub**: aset `Gili-Setup-<versi>.exe` diunduh ke `%TEMP%\Gili-Setup-<versi>.exe`, lalu diperiksa: tanda tangan Authenticode sah (`WinVerifyTrust`) dan subjek sertifikat penandatangan sama dengan subjek sertifikat `Gili.exe` yang jalan **(keputusan Claude: subjek, bukan thumbprint, karena sertifikat OV diperbarui tiap tahun)**. Cocok: dijalankan dengan `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`; installer menutup Gili lewat `--quit` dan menjalankannya lagi (Fase 5b). Tidak cocok: file dihapus, `Update couldn't be verified`.
- **Store**: `RequestDownloadAndInstallStorePackageUpdatesAsync` dengan jendela Settings sebagai pemilik dialog Store. Windows menutup Gili saat memasang; `RegisterApplicationRestart` (hanya salinan Store, tanpa restart setelah crash, hang, atau reboot karena Gili punya restart crash sendiri, PRD 11) menjalankannya lagi.

## Kode

- Core `Gili.Core.About.Updates`: `ReleaseInfo.Parse` (JSON GitHub: `tag_name` dengan atau tanpa `v`, `html_url`, aset installer yang namanya cocok dengan versinya), `UpdateRules` (keadaan setelah cek berhasil atau gagal, jatuh tempo 24 jam, teks baris). Unit test.
- Platform: `GithubReleases` (HTTP, 15 detik), `Signatures.SameSigner`, `StoreUpdates`, `RestartRegistration`.
- App: `UpdateService` (jadwal, keadaan, Install update), titik biru di bar, baris Updates dan Release notes.

## Anggaran (PRD 8.1)

Satu permintaan HTTPS kecil sehari; timer satu jam yang hanya membandingkan waktu. Tidak ada proses latar.

## Mockup dan PRD

- Mockup: titik biru di tombol Settings (`.udot` dengan warna aksen biru) dan baris Updates `Up to date`.
- PRD 9.4 dan 6.5: teks keadaan di atas; 8.2: URL `gili-releases` milik `fhdyt`.

## Hasil verifikasi 5d

- Build dan `dotnet format` bersih, 1009 unit test lulus (baru: `ReleaseInfoTests` 8, `UpdateRulesTests` 9).
- `Signatures` pada file nyata: `dotnet.exe` dan `hostfxr.dll` (subjek `CN=.NET, O=Microsoft Corporation, …`) cocok; `git.exe` (Johannes Schindelin) dengan `dotnet.exe` tidak cocok; salinan `dotnet.exe` yang diubah 1 byte tetap punya subjek tetapi tidak dipercaya `WinVerifyTrust`, jadi ditolak; `Gili.exe` tanpa tanda tangan tidak dianggap bertanda tangan.
- `GithubReleases.LatestAsync` ke `fhdyt/gili-releases` yang belum ada: 404 menjadi `null` tanpa exception (dianggap gagal yang diam).
- Salinan harian (GitHub, tanpa tanda tangan) lewat UIA: Updates nonaktif dengan `This test build doesn't check. Install a newer Gili-Setup over it`, Release notes nonaktif dengan `Coming in a later version`; tidak ada permintaan jaringan.
- Paket MSIX dev: setelah 60 detik `StoreContext` menolak paket sideload (`0x803FB112`), log mencatat `Could not check for updates`, baris tetap `Not checked yet` tanpa error di layar; Release notes aktif ke halaman Store paket. Paket dilepas lagi.
- **Belum teruji** (butuh sertifikat OV, repo `gili-releases`, dan listing Store): cek GitHub yang menemukan versi baru, titik biru di tombol Settings, unduh dan verifikasi installer asli, Install update yang memasang lalu menjalankan Gili lagi, update Store dan `RegisterApplicationRestart`.

## Perubahan sesudahnya (PRD revisi 11)

Pengguna akan membuat repo kode `fhdyt/gili` publik, jadi `gili-releases` tidak dipakai. Cek update membaca `https://api.github.com/repos/fhdyt/gili/releases/latest`; selama repo masih privat, GitHub menjawab 404 tanpa login, yang diperlakukan sebagai gagal yang diam. Issue dan dukungan di `github.com/fhdyt/gili/issues`.
