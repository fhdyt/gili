# Fase 3f: menyembunyikan taskbar Windows dan tombol Tray (desain singkat)

Tanggal: 2026-10-31. Fitur: F-BAR-11, F-RIGHT-09. Pengguna meminta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya. F-BAR-10 (bar di bawah) menyusul di Fase 3g karena bergantung pada fitur ini.

## Verifikasi host tray (PRD 12, "diuji di awal Fase 3 sebelum tray dibangun")

Spike di laptop pengguna (Windows 11 build 26300), di luar repo:

| Percobaan | Hasil |
|---|---|
| ManagedShell 0.0.374 `NotificationArea` (host tray tambahan) | 17 ikon masuk dalam 450 ms, termasuk yang tersembunyi di overflow; judul, path, dan ikon lengkap; RAM proses spike 55 MB |
| Pesan AppBar selama host ManagedShell berjalan | **Rusak.** `ABM_QUERYPOS` untuk AppBar uji di tepi atas mengembalikan `top = 0` (seharusnya 40, di bawah bar Gili), `ABM_GETTASKBARPOS` mengembalikan persegi kosong. `SHAppBarMessage` mengirim ke `FindWindow("Shell_TrayWnd")`, yaitu jendela host yang topmost, dan Explorer mengabaikan pesan yang diteruskan |
| Host buatan sendiri yang meneruskan semua `WM_COPYDATA` ke Explorer apa adanya | Rusak dengan cara yang sama. Setelah host ditutup, semuanya normal lagi |

Artinya selama host tray tambahan hidup, AppBar Gili sendiri (daftar ulang saat `TaskbarCreated`, DPI, resume) dan AppBar aplikasi lain tidak bekerja. Selain itu, ManagedShell tidak meneruskan pesan ikon ke Explorer, jadi ikon di taskbar Windows berhenti diperbarui. **Host tray tambahan ditolak (keputusan Claude).**

## F-RIGHT-09: tombol Tray

| Cara | Untung | Rugi |
|---|---|---|
| **Cadangan PRD 12: tombol Tray membuka ikon tersembunyi Windows (`Win+B`, `Enter`)** | Klik, klik kanan, dan menu semua ikon persis seperti Windows; tanpa risiko ke AppBar | Bukan panel Gili seperti mockup |
| Host tray sendiri yang menjawab pesan AppBar dengan mengirim ulang lewat memori bersama milik Gili | Panel seperti mockup | Belum terbukti; menyentuh pesan AppBar semua aplikasi; perlu meneruskan setiap pesan ikon ke Explorer; kesalahan kecil merusak desktop pengguna |
| Membaca ikon lewat UI Automation dari taskbar Windows | Tanpa host | Klik kanan tidak bisa diteruskan; tidak bekerja saat taskbar disembunyikan |

Dipilih yang pertama **(keputusan Claude)**. PRD 6.4, F-RIGHT-09, dan mockup disamakan: tombol Tray membuka flyout ikon tersembunyi Windows. Cara kedua dicatat di PRD 12 sebagai kemungkinan setelah Fase 4.

Saat taskbar Windows disembunyikan Gili, flyout itu tidak bisa terbuka. Jadi klik Tray memunculkan taskbar sementara (tanpa mengubah auto-hide), mengirim `Win+B` lalu `Enter`, dan menyembunyikannya lagi setelah fokus keluar dari taskbar dan flyout-nya (dicek tiap 250 ms hanya selama taskbar tampil, maksimal 60 detik) **(keputusan Claude)**.

## F-BAR-11: Hide Windows taskbar

- **Saklar**: Settings → General → `Hide Windows taskbar` (default mati), keterangan dari mockup `Gili gives you Start, tray icons and Controls instead`.
- **Menyembunyikan** (`TaskbarHider` di Platform):
  1. Baca status asli lewat `ABM_GETSTATE` (`ABS_AUTOHIDE`). Kalau belum ada file `taskbar-state.json` di folder data, tulis status itu ke sana **sebelum** mengubah apa pun; file yang sudah ada tidak ditimpa, karena isinya status asli dari run yang mati paksa.
  2. `ABM_SETSTATE` dengan `ABS_AUTOHIDE`, supaya ruang taskbar kembali ke jendela lain.
  3. `ShowWindow(Shell_TrayWnd, SW_HIDE)`, supaya taskbar tidak muncul saat kursor menyentuh tepi bawah.
  4. WinEvent `EVENT_OBJECT_SHOW` (out-of-context, hanya proses Explorer): kalau Explorer memunculkan `Shell_TrayWnd` lagi, Gili menyembunyikannya lagi, kecuali saat tombol Tray sedang memunculkannya.
- **Hanya taskbar monitor utama** yang disembunyikan, sama seperti cakupan Gili. Auto-hide adalah pengaturan Windows yang berlaku untuk semua monitor, jadi taskbar monitor lain ikut auto-hide **(keputusan Claude)**.
- **Memulihkan**: `ShowWindow(SW_SHOWNA)`, `ABM_SETSTATE` dengan status dari file, lalu hapus file. Dijalankan saat saklar dimatikan, Quit, logoff/shutdown (`SessionEnding`), dan di handler crash (bersama pelepasan AppBar). Saat start, kalau file masih ada dan saklar mati, status asli dipulihkan; kalau saklar nyala, file dipakai lagi sebagai status asli.
- **Explorer restart** (`TaskbarCreated`): cari ulang `Shell_TrayWnd`, lalu sembunyikan lagi.
- **Tombol Windows** tetap membuka Start: Start menu Windows 11 adalah jendela terpisah dari taskbar. Tombol Start Gili memakai tombol Windows yang sama.
- Tanpa admin: semua API di atas jalan sebagai pengguna biasa.

## Anggaran (PRD 8.1)

Satu hook WinEvent out-of-context untuk proses Explorer; event `EVENT_OBJECT_SHOW` hanya dicocokkan dengan satu handle. Polling 250 ms hanya selama taskbar dimunculkan oleh tombol Tray. RAM dan CPU tidak terukur.

## Pengujian

- Unit test di Core untuk aturan status asli (kapan file ditulis, dibaca, dipulihkan) lewat interface yang bisa dipalsukan.
- Di laptop, tanpa input sintetis: nyalakan saklar dari Settings, pastikan taskbar hilang dan area kerja penuh (`SPI_GETWORKAREA`), restart Explorer lalu cek taskbar tetap tersembunyi, matikan saklar dan Quit lalu cek status asli kembali.
- **Butuh pengguna**: menekan tombol Windows (Start harus terbuka), mengklik tombol Tray Gili saat taskbar disembunyikan, dan menguji jalur crash (Debug → Simulate crash).
