# Fase 3e: Update and shut down / Update and restart (desain singkat)

Tanggal: 2026-10-31. Fitur: F-RIGHT-08. Perubahan kecil, jadi desainnya singkat. Pengguna meminta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Maksud

Pulau Power menampilkan `Update and shut down` dan `Update and restart` (dengan titik oranye), dan ikon Power di bar mendapat titik oranye, hanya saat Windows punya update yang menunggu, seperti Start menu (PRD 6.4, 12).

## Verifikasi di laptop pengguna (sebelum implementasi)

Dibaca tanpa mengubah apa pun, saat laptop sedang punya update yang menunggu restart:

| Sumber | Nilai |
|---|---|
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired` | Ada (satu nilai GUID = 1) |
| Windows Update Agent `Microsoft.Update.SystemInfo.RebootRequired` | `True` |
| `HKLM\SOFTWARE\Microsoft\WindowsUpdate\Orchestrator` | `ShutdownFlyoutOptions = 15`, `EnhancedShutdownEnabled = 1` |
| `Component Based Servicing\RebootPending` | Ada |

## Pendekatan

| Cara | Untung | Rugi |
|---|---|---|
| **Kunci `RebootRequired` atau `ISystemInformation.RebootRequired`** (dua sumber di PRD 9.2) | API resmi dan kunci yang didokumentasikan; keduanya cocok di laptop pengguna | Tidak membaca flyout Start secara langsung |
| `Orchestrator\ShutdownFlyoutOptions` | Nilai yang dipakai flyout Start sendiri | Tidak didokumentasikan; arti bitnya tebakan |

Dipilih yang pertama **(keputusan Claude)**: registry dibaca dulu (murah), Windows Update Agent bila kuncinya tidak ada. Dibaca di thread latar saat start, saat pulau Power dibuka, dan tiap 30 menit (dijeda bersama polling lain).

Aksi: `InitiateShutdownW` dengan `SHUTDOWN_POWEROFF` atau `SHUTDOWN_RESTART` ditambah `SHUTDOWN_INSTALL_UPDATES`, tanpa flag paksa, setelah menyalakan `SeShutdownPrivilege` milik pengguna (seperti `shutdown.exe`). Kalau Windows menolak, pulau membuka `ms-settings:windowsupdate`, sesuai cadangan di PRD 12 **(keputusan Claude)**.

Tooltip ikon Power saat ada update: `Power · updates are ready to install` (teks mockup).

## Anggaran (PRD 8.1)

Satu pembacaan registry (dan bila perlu satu objek COM Windows Update Agent) tiap 30 menit dan saat pulau Power dibuka: tidak terukur.

## Hasil verifikasi

- Build bersih; titik oranye tampil di ikon Power saat update menunggu (dicek dengan screenshot).
- **Belum teruji, butuh pengguna**: membandingkan dengan flyout power Start menu (apakah Start juga menampilkan `Update and shut down` sekarang), membuka pulau Power untuk melihat kedua tombol, dan menekannya (memasang update lalu mematikan/restart laptop). Saya tidak menekan tombol apa pun yang mematikan laptop pengguna.
