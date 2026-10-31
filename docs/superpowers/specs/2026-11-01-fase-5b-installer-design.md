# Fase 5b: installer GitHub dengan Inno Setup (desain)

Tanggal: 2026-11-01. Fitur: F-PUB-03 tanpa tanda tangan (build untuk teman, PRD 9.4 "Installer GitHub" dan "Build untuk teman"). Tanda tangan, `gili-releases`, dan cek update (F-PUB-04) menunggu sertifikat OV yang pembeliannya ditunda pengguna. Pengguna minta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Pendekatan

PRD 9.4 sudah memilih Inno Setup (alasan menolak Velopack, WiX, dan MSIX ada di sana). Yang tersisa adalah cara membangunnya:

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. `package.ps1 -Installer` mem-publish self-contained ke `artifacts/installer/app` lalu memanggil `ISCC` dengan versi dan folder sumber sebagai `/D` (dipilih)** | Satu skrip untuk kedua jalur, versi dari `Directory.Build.props`; `gili.iss` tidak menyimpan versi | `-Installer` tidak ikut membangun MSIX (dua perintah untuk dua bentuk) |
| B. Installer dibungkus dari layout MSIX | Satu kali publish | Layout berisi `AppxManifest.xml` dan `Assets` yang harus dikecualikan, dan `-Install` MSIX bergantung pada layout itu |

## Aturan installer

- `PrivilegesRequired=lowest`, folder tetap `{localappdata}\Programs\Gili` tanpa halaman folder, Windows 11 saja (`MinVersion=10.0.22000`), x64 saja.
- Wizard: sambutan, progres, selesai dengan `Launch Gili` (nyala). Tanpa lisensi, komponen, grup program, atau halaman Ready. Teks bawaan Inno Setup bahasa Inggris.
- **Memasang di atas versi lama**: sebelum menyalin, installer menjalankan `{app}\Gili.exe --quit` (kalau ada), lalu menunggu mutex `Local\Gili.SingleInstance` lepas maks. 5 detik. Kalau Gili masih jalan (atau salinan lain memegang mutex): `Gili is still running. Quit it from the bar menu, then click Retry.` dengan Retry/Cancel. Isi `{app}` dihapus dulu (`[InstallDelete]`) **(keputusan Claude)**: build self-contained bisa mengganti daftar DLL antarversi, dan folder itu hanya berisi file program; data ada di `%LOCALAPPDATA%\Gili` dan `%USERPROFILE%\.gili`.
- `Gili.exe --quit` sekarang menunggu instance yang jalan benar-benar selesai (mutex dan prosesnya, maks. 5 detik) **(keputusan Claude)**, sama seperti `--prepare-uninstall`; installer tetap menunggu sendiri karena `Gili.exe` versi lama keluar seketika.
- Launch Gili tidak memakai `skipifsilent`, supaya pemasangan senyap untuk update (F-PUB-04, nanti) menjalankan Gili lagi.
- **Shortcut** Start Menu `Gili` dengan AUMID `Gili` **(keputusan Claude: nama pendek yang sama dengan id paket MSIX)**; entri uninstall di Apps dengan ikon `Gili.exe`, versi, dan penerbit `Gili` **(keputusan Claude: nama app sebagai penerbit sampai sertifikat OV menentukan nama resminya)**.
- **Uninstaller**: `Gili.exe --prepare-uninstall` (Fase 5a) dijalankan tersembunyi dan ditunggu sebelum file dihapus. Lalu `Also delete my notes and settings?` (Yes/No, default No); Yes menghapus `%LOCALAPPDATA%\Gili` dan `%USERPROFILE%\.gili` setelah file program terhapus. Uninstall senyap tidak bertanya dan menyimpan data.
- **Ikon**: `packaging/Gili.ico` (16–256 px, digambar `scripts/package-assets.ps1` dari maskot yang sama dengan logo MSIX) menjadi `ApplicationIcon` `Gili.exe` dan ikon installer.

## Build untuk teman

Installer yang sama tanpa tanda tangan. Pesan saat membagikannya:

> Gili belum ditandatangani, jadi Windows menampilkan "Windows protected your PC" sekali. Klik **More info**, lalu **Run anyway**. Gili terpasang tanpa izin admin di folder pengguna Anda. Versi baru cukup dipasang di atas yang lama; note dan pengaturan tetap ada. Log ada di Settings → About → Open logs folder.

## Anggaran (PRD 8.1)

Tidak ada kode yang jalan terus: `--quit` hanya menunggu saat dipanggil.

## Hasil verifikasi 5b

- Build dan `dotnet format` bersih, 981 unit test lulus. `pwsh scripts/package.ps1 -Installer` menghasilkan `Gili-Setup-0.1.1.exe` 49,7 MB dalam ±36 detik kompilasi Inno Setup 6.7.3.
- Pasang senyap (`/VERYSILENT`) di atas salinan harian `publish.ps1` yang sedang jalan: Gili lama keluar lewat `--quit`, 414 file self-contained terpasang (termasuk `coreclr.dll` dan `Gili.ClaudeHook.exe`), shortcut Start Menu dengan AUMID `Gili`, entri Apps `Gili 0.1.1` penerbit `Gili` dengan ikon `Gili.exe`, Gili jalan lagi dan menulis entri Run sendiri.
- Pasang ulang di atas salinan installer (jalur update): exit 0, proses lama diganti yang baru.
- Uninstall senyap: `--prepare-uninstall` jalan lebih dulu (Gili keluar, 8 hook Gili hilang, entri Run hilang), semua file program, shortcut, dan entri Apps terhapus ("Removed all? Yes"), `%LOCALAPPDATA%\Gili` dan `%USERPROFILE%\.gili` tetap ada.
- Salinan harian dipulihkan dengan `publish.ps1` (entri Run dan 8 hook kembali).
- Temuan: `Start-Process -Wait` di PowerShell ikut menunggu Gili yang dijalankan installer, jadi perintah uji menggantung sampai Gili ditutup; dicatat di CLAUDE.md.
- **Belum teruji, butuh pengguna** (klik di wizard): halaman sambutan dan selesai dengan `Launch Gili`, dialog `Gili is still running…` (Retry/Cancel), dan `Also delete my notes and settings?` saat uninstall dari Settings Windows → Apps. SmartScreen hanya muncul untuk file yang diunduh dari internet, jadi diuji teman lewat build untuk teman. Mesin bersih tanpa .NET (F-PUB-06) menunggu VM.
