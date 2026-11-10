# Release notes: Gili 0.3.2

Rilis GitHub keempat. Tag `v0.3.2`, aset `Gili-Setup-0.3.2.exe` (nama ini yang dicari cek update Gili, jangan diganti). Teks di bawah garis bahasa Inggris dan dipakai di halaman rilis.

---

**Gili** is a glass bar for the top of your Windows 11 screen, with a black island in the middle that shows what matters right now: music, timers, copies, files, your Claude sessions, and Gili, a pair of eyes that follows your pointer.

## Install

1. Download **Gili-Setup-0.3.2.exe** below and run it. No administrator rights, no .NET, nothing else to install.
2. This build isn't signed yet, so Windows shows **"Windows protected your PC"** once. Click **More info**, then **Run anyway**.
3. Gili starts right away and with Windows from then on. Right-click the bar, the island, or Gili's icon in the notification area for **Settings** and **Quit Gili**.

Already on an older version? Run this Gili-Setup over it. Your notes, todos, alarms, Shelf and settings stay.

Requirements: Windows 11, 64-bit (x64), main monitor.

## What changed

- **The weekly reset takes a day and a time.** On the Claude page, pick the day, such as Tuesday, and type the time. Gili works out the date: the next time that day and time come. The Weekly limit row still shows the date so you can check it.

## Known limits

- Unsigned for now: SmartScreen warns on first run, and updates are installed by running the new Gili-Setup yourself.
- Windows notifications in the island and turning on Windows' Do not disturb automatically aren't available yet.
- Explorer's progress is read in English Windows; other languages show the percent only.

## Privacy

Everything stays on your PC. Gili has no telemetry and no ads. It only connects to Open-Meteo for the weather, to LRCLIB for lyrics if you turn them on, and to GitHub to check for a new version. See the privacy policy in `docs/release/privacy-policy.md`.

## Uninstall

Windows Settings › Apps › Installed apps › Gili › Uninstall. Gili removes its Claude hooks and brings the Windows taskbar back; answer **Yes** to "Also delete my notes and settings?" to remove your data too.
