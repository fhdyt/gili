# Release notes: Gili 0.3.1

Rilis GitHub ketiga. Tag `v0.3.1`, aset `Gili-Setup-0.3.1.exe` (nama ini yang dicari cek update Gili, jangan diganti). Teks di bawah garis bahasa Inggris dan dipakai di halaman rilis.

---

**Gili** is a glass bar for the top of your Windows 11 screen, with a black island in the middle that shows what matters right now: music, timers, copies, files, your Claude sessions, and Gili, a pair of eyes that follows your pointer.

## Install

1. Download **Gili-Setup-0.3.1.exe** below and run it. No administrator rights, no .NET, nothing else to install.
2. This build isn't signed yet, so Windows shows **"Windows protected your PC"** once. Click **More info**, then **Run anyway**.
3. Gili starts right away and with Windows from then on. Right-click the bar, the island, or Gili's icon in the notification area for **Settings** and **Quit Gili**.

Already on an older version? Run this Gili-Setup over it. Your notes, todos, alarms, Shelf and settings stay.

Requirements: Windows 11, 64-bit (x64), main monitor.

## What's new

- **Set the weekly reset once**: the Weekly limit row on the Claude page now moves on 7 days each time the reset comes, so the last day and time you set keep being used. Click the row to change it. A weekly reset you set in 0.3.0 comes back on its own.
- **Projects in a window**: Open in window shows the Projects page in a large window, with the same editing and the same data as the island.

## What changed

- **Claude reset cards ring for 1 minute.** The sound of the 5-hour and weekly reset cards stops by itself after a minute instead of five. The card stays until you close it. Alarms and timers still ring for up to 5 minutes.

## Known limits

- Unsigned for now: SmartScreen warns on first run, and updates are installed by running the new Gili-Setup yourself.
- Windows notifications in the island and turning on Windows' Do not disturb automatically aren't available yet.
- Explorer's progress is read in English Windows; other languages show the percent only.

## Privacy

Everything stays on your PC. Gili has no telemetry and no ads. It only connects to Open-Meteo for the weather, to LRCLIB for lyrics if you turn them on, and to GitHub to check for a new version. See the privacy policy in `docs/release/privacy-policy.md`.

## Uninstall

Windows Settings › Apps › Installed apps › Gili › Uninstall. Gili removes its Claude hooks and brings the Windows taskbar back; answer **Yes** to "Also delete my notes and settings?" to remove your data too.
