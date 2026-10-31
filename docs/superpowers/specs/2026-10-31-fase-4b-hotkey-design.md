# Fase 4b: hotkey global yang bisa diubah (desain)

Tanggal: 2026-10-31. Fitur: F-TOOL-10, F-TOOL-11, sisa F-BAR-08 (hotkey). Sumber: PRD 6.3 (`Ctrl+Alt+V`, `Ctrl+Alt+N`, `Ctrl+Alt+T`), 6.5 Shortcuts, 9.2 "Hotkey" (`RegisterHotKey`), 11 ("Used by another app"). Pengguna minta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Mockup dan PRD berbeda

Mockup hanya punya saklar `Keyboard shortcuts`; PRD F-TOOL-11 meminta setiap hotkey bisa diganti dengan keterangan `Used by another app`. **(keputusan Claude)** PRD diikuti dan mockup disamakan: bagian Shortcuts sekarang berisi saklar itu lalu empat baris dengan tutup tombol (`.kcap`). Dilaporkan ke pengguna di ringkasan.

## Pendekatan

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. `RegisterHotKey` di jendela message-only Platform (dipilih)** | Sesuai PRD 9.2; tanpa hook keyboard global (tidak mirip keylogger, PRD 12); bentrok terdeteksi langsung | Kombinasi yang sudah dipegang aplikasi lain tidak bisa dipakai |
| B. Hook keyboard tingkat rendah (`WH_KEYBOARD_LL`) | Bisa "merebut" kombinasi apa pun | Membaca semua ketukan: dicurigai antivirus, dan PRD sudah memilih `RegisterHotKey` |
| C. `HwndSource` WPF di App | Lebih sedikit P/Invoke | Platform tidak memakai WPF; arsitektur menaruh `HotkeyService` di Platform |

## Aturan

- Default: `Ctrl+Alt+Space` buka/tutup pulau (halaman aktivitas di slot, kalau tidak ada halaman terakhir), `Ctrl+Alt+V` halaman Clipboard dengan kolom cari fokus dan baris pertama tersorot, `Ctrl+Alt+N` note baru dengan editor fokus, `Ctrl+Alt+T` Calendar hari ini dengan kolom Add task terbuka.
- Settings → Shortcuts: `Keyboard shortcuts` (default nyala; mati = semua hotkey dilepas, baris di bawahnya nonaktif) lalu satu baris per hotkey: `Open or close the island`, `Clipboard`, `New note`, `Add task for today` **(keputusan Claude: label bahasa Inggris pendek, belum ada di PRD)**.
- Mengganti: klik tutup tombol → `Press keys` (garis hijau) dan semua hotkey dijeda supaya kombinasi lama pun sampai ke baris itu; tombol berikutnya dengan Ctrl, Alt, atau Win menjadi hotkey baru; Esc tanpa modifier membatalkan; kehilangan fokus membatalkan. Tanpa Ctrl/Alt/Win: `Use Ctrl, Alt or Win with another key`; dipakai aksi Gili lain: `Already used for <nama>`. Disimpan sebagai teks `Ctrl+Alt+Space` di `settings.json`.
- Kombinasi yang dipegang aplikasi lain tidak aktif dan barisnya menampilkan `Used by another app` (dicatat sekali di log).
- `MOD_NOREPEAT`, jadi menahan tombol tidak membuka-tutup pulau berulang.

## Anggaran (PRD 8.1)

Tanpa polling dan tanpa hook: satu jendela message-only dan empat `RegisterHotKey`.

## Hasil verifikasi 4b

- Build dan `dotnet format` bersih, 964 unit test lulus (baru: `HotkeysTests` 16).
- Di laptop: tiga hotkey terdaftar; `Ctrl+Alt+Space` sudah dipegang aplikasi lain di laptop ini, jadi log mencatat `The Island hotkey is used by another app` dan Settings → Shortcuts (dibuka lewat UIA) menampilkan `Used by another app` di baris itu.
- **Belum teruji, butuh pengguna** (menekan hotkey berarti input keyboard): keempat hotkey membuka halamannya dengan fokus yang benar, merekam kombinasi baru, Esc membatalkan, dan pesan `Already used for …`.
