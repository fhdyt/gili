# Microsoft Store listing (draft, F-PUB-02)

Isi untuk Partner Center. Teks listing bahasa Inggris (aturan teks app); catatan di luar blok kutipan bahasa Indonesia.

- **Nama**: Gili (cadangan bila dipakai: `Gili Bar`, ditentukan saat memesan nama).
- **Kategori**: Productivity. **Harga**: Free. **Pasar**: semua. **Usia**: 3+ (tanpa konten pengguna yang dibagikan, tanpa pembelian).
- **Privacy policy URL**: halaman dari `docs/release/privacy-policy.md` setelah online (F-PUB-08).
- **Support**: `https://github.com/fhdyt/gili/issues` dan halaman dari `docs/release/support.md`.

## Short description

> A glass bar for the top of your screen, with a little island that shows what matters right now.

## Description

> Gili puts a slim glass bar at the top of your Windows 11 screen. In the middle sits the island: a small black shape that grows to show what is happening right now, then shrinks back out of the way. When nothing is going on, Gili the mascot watches your pointer.
>
> **The island**
> • Music that is playing, with artwork and controls
> • Timers, alarms, pomodoro and the stopwatch, right where you look
> • Your calendar, todos and quick notes, and notes that float on top of other windows
> • Clipboard history: paste text, images and files you copied recently
> • The Shelf: drop files on the bar to keep them handy, then drag them out again
> • Wi‑Fi, Bluetooth, volume and brightness controls
> • Claude Code sessions: see when Claude is working, needs your answer, or is done
>
> **The bar**
> • Start, your favourite apps and the clock where you expect them
> • Weather, network, CPU, memory, GPU and disk at a glance
> • Tray icons and power options, and the option to hide the Windows taskbar
>
> Gili is free, has no ads and no accounts, and keeps your data on your PC.

## Feature bullets (maksimal 20, dipakai 8)

> Dynamic island for music, timers, alarms and more
> Calendar, todos and quick notes
> Notes that float on top
> Clipboard history
> Drop files on the Shelf
> Quick controls for Wi‑Fi, Bluetooth, volume and brightness
> Claude Code session status
> No ads, no accounts, no telemetry

## Screenshots (1920 × 1080)

Dibuat dari app asli di laptop pengembang, bukan dari mockup: bar dengan pulau compact berisi musik; pulau terbuka di Calendar; Notes dengan note mengambang; Clipboard; Controls; Claude Code bekerja dan bertanya; Settings. Mode demo (F-BAR-09) dipakai supaya semua keadaan tampil tanpa data pribadi.

## Catatan sertifikasi (Notes for certification)

> Gili is a desktop app (WPF, .NET) packaged as MSIX with runFullTrust. It needs full trust because it registers the bar as a Windows AppBar (SHAppBarMessage) so other windows do not cover it, reads system information (performance counters, network and battery state) for the bar's modules, listens to the clipboard for its clipboard history, and runs a small helper (exposed as the app execution alias gili-claude-hook.exe) that Claude Code calls through hooks the user turns on in Settings. It does not require admin rights, install drivers, or download code. To test: start Gili, and the bar appears at the top of the primary monitor. Right-click the bar and choose Demo mode to see every state of the island without real events. Quit from the same menu.
