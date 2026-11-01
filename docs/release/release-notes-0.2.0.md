# Release notes: Gili 0.2.0 (draft)

Rilis GitHub pertama (revisi 15: rilis hanya lewat GitHub Releases `fhdyt/gili`, diunggah pengguna sendiri). Tag `v0.2.0`, aset `Gili-Setup-0.2.0.exe` (nama ini yang dicari cek update Gili, jangan diganti). Teks di bawah garis bahasa Inggris dan siap ditempel ke halaman rilis.

---

**Gili** is a glass bar for the top of your Windows 11 screen, with a black island in the middle that shows what matters right now: music, timers, copies, files, your Claude Code sessions, and Gili, a pair of eyes that follows your pointer.

## Install

1. Download **Gili-Setup-0.2.0.exe** below and run it. No administrator rights, no .NET, nothing else to install.
2. This build isn't signed yet, so Windows shows **"Windows protected your PC"** once. Click **More info**, then **Run anyway**.
3. Gili starts right away and with Windows from then on. Right-click the bar, the island, or Gili's icon in the notification area for **Settings** and **Quit Gili**.

Installing a newer Gili-Setup over this one keeps your notes, todos, alarms, Shelf and settings. Gili checks GitHub once a day and shows a blue dot on its Settings button when a new version is out.

Requirements: Windows 11, 64-bit (x64), main monitor.

## What's inside

- **The bar**: Start, a launcher for Timer, Alarms, Todo, Notes and Clipboard, the Shelf, your favorite apps, the open apps; weather, network, CPU, RAM, Wi‑Fi, Bluetooth, volume, brightness, tray icons, clock and power on the right. Every part can be hidden, and the bar's color, opacity, glass and height are yours to set. It can also sit at the bottom, and hide the Windows taskbar.
- **The island**: what is playing (with artwork, video thumbnails and optional synced lyrics from LRCLIB), timers and the pomodoro, the stopwatch, what you just copied, Explorer's copy, move and delete progress, files on the Shelf, charging, and Claude Code sessions with the 5-hour usage window. Click it to open pages for Media, Timer, Alarms, Calendar, Todo, Notes, Clipboard, Shelf, Claude and System.
- **Notes** you can pull out of the island to float on top of other windows.
- **Clipboard history** for text, images and files; pick one and it is pasted where your cursor was.
- **Shelf**: drop files on the island to hold them, then drag them out where they belong.
- **Alarms, timers and todo reminders** as cards in the island, with sound, that survive restarts and sleep.
- **Claude Code**: see when a session works, needs your answer, or is done (opt-in; adds hooks to `~/.claude/settings.json` and backs it up first).

## Privacy

Everything stays on your PC. Gili has no telemetry and no ads. It only connects to Open-Meteo for the weather, to LRCLIB for lyrics if you turn them on, and to GitHub to check for a new version. See the privacy policy in `docs/release/privacy-policy.md`.

## Known limits

- Unsigned for now: SmartScreen warns on first run, and updates are installed by running the new Gili-Setup yourself.
- Windows notifications in the island and turning on Windows' Do not disturb automatically aren't available yet.
- Explorer's progress is read in English Windows; other languages show the percent only.

## Uninstall

Windows Settings › Apps › Installed apps › Gili › Uninstall. Gili removes its Claude hooks and brings the Windows taskbar back; answer **Yes** to "Also delete my notes and settings?" to remove your data too.
