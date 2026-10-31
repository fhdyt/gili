# Data tetap ada saat update dan reinstall (desain singkat)

Tanggal: 2026-10-31. Permintaan pengguna: "jika saya sudah install gili, dan reinstall dengan versi terbaru, data-data seperti note tidak hilang". Pengguna meminta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Keadaan sekarang

| Jalur | Note dan settings | Alasan |
|---|---|---|
| Versi GitHub / `publish.ps1`, pasang versi baru di atas yang lama | Tetap | Data di `%LOCALAPPDATA%\Gili`, terpisah dari folder program `%LOCALAPPDATA%\Programs\Gili` |
| Versi GitHub, uninstall lalu install | Tetap (kecuali `Also delete my notes and settings` dicentang, PRD 9.4) | Sama |
| Versi MSIX, update ke versi lebih baru | Tetap | Windows mempertahankan folder data paket saat update |
| **Versi MSIX, uninstall lalu install** | **Hilang**, termasuk 7 backup harian | Windows menghapus `%LOCALAPPDATA%\Packages\Gili_*` saat uninstall, dan backup ada di folder yang sama |
| **Versi baru dengan langkah migrasi yang salah** | Bisa rusak tanpa salinan sebelum migrasi | Backup harian dibuat setelah database dibuka, yaitu setelah migrasi |

## Maksud

Note, todo, alarm, timer, Shelf, dan settings selamat di semua jalur di atas, termasuk reinstall versi Store, tanpa langkah apa pun dari pengguna.

## Pendekatan

| Cara | Untung | Rugi |
|---|---|---|
| **Salinan cadangan di luar folder paket, dipulihkan otomatis saat `gili.db` belum ada** | Bekerja untuk Store dan GitHub; pindah dari versi GitHub ke Store juga membawa note; tanpa kapabilitas terbatas | Ada folder kedua yang harus ikut dihapus saat pengguna memilih menghapus datanya |
| Matikan virtualisasi file MSIX (`unvirtualizedResources`) supaya Store juga memakai `%LOCALAPPDATA%\Gili` | Satu folder data untuk kedua versi | Kapabilitas terbatas yang butuh alasan di sertifikasi Store; kalau ditolak, masalahnya kembali |
| Tombol Export/Import di Settings | Pengguna memegang filenya | Pengguna harus ingat melakukannya sebelum uninstall; tidak menjawab permintaan "tidak hilang" |

Dipilih yang pertama **(keputusan Claude)**.

## Rancangan

- **Folder salinan**: `%USERPROFILE%\.gili\backups` **(keputusan Claude)**. Folder ini tidak divirtualisasi MSIX (hanya AppData dan HKCU yang divirtualisasi), tidak ikut OneDrive seperti Documents, dan diberi atribut hidden supaya tidak mengotori folder profil. Kedua versi (Store dan GitHub) memakai folder yang sama.
- **Isi**: `gili-YYYY-MM-DD.db` (salinan lewat SQLite backup API, 7 hari terakhir, format sama dengan backup harian di PRD 10) dan `settings.json`.
- **Kapan ditulis**: saat start, tiap jam (timer backup yang sudah ada), dan saat Quit. Salinan hari ini ditimpa setiap kali, jadi reinstall kehilangan paling banyak satu jam. Database kecil (puluhan KB), jadi biayanya tidak terukur (PRD 8.1).
- **Kapan dipulihkan**: saat start, sebelum apa pun ditulis:
  - `gili.db` belum ada (instalasi baru atau reinstall): salinan terbaru dari folder salinan disalin ke folder data. Log mencatat `Restored gili.db from <file>`.
  - `gili.db` rusak: backup harian lokal dicoba dulu (PRD 11), lalu folder salinan.
  - `settings.json` belum ada: salinannya dipakai.
- **Sebelum migrasi**: kalau versi skema di file lebih lama dari versi app, file disalin dulu ke `backups\gili-before-v<N>.db` (N = versi tujuan), lalu dimigrasi. File ini tidak ikut dirotasi.
- **Menghapus data**: uninstaller GitHub dengan `Also delete my notes and settings` juga menghapus `%USERPROFILE%\.gili` (Fase 5, PRD 9.4).

## Di luar cakupan

- Sinkronisasi antarperangkat atau cloud: tetap tidak ada (tanpa telemetri, data lokal).
- Menggabungkan data dua salinan yang sama-sama sudah punya `gili.db`: yang sudah punya data tidak pernah ditimpa.

## Pengujian

Unit test di `Gili.Core.Tests/Data`: pemulihan saat file belum ada, pemulihan saat rusak dari folder salinan, salinan hari ini ditimpa, rotasi 7, salinan sebelum migrasi, settings dipulihkan. Lalu di laptop: pasang versi baru dengan `publish.ps1` dan pastikan jumlah note sama.
