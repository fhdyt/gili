# Release notes: Gili 0.3.3

Rilis GitHub kelima. Tag `v0.3.3`, aset `Gili-Setup-0.3.3.exe` (nama ini yang dicari cek update Gili, jangan diganti). Teks di bawah garis bahasa Inggris dan dipakai di halaman rilis.

---

**Gili** is a glass bar for the top of your Windows 11 screen, with a black island in the middle that shows what matters right now: music, timers, copies, files, your Claude sessions, and Gili, a pair of eyes that follows your pointer.

## Install

1. Download **Gili-Setup-0.3.3.exe** below and run it. No administrator rights, no .NET, nothing else to install.
2. This build isn't signed yet, so Windows shows **"Windows protected your PC"** once. Click **More info**, then **Run anyway**.
3. Gili starts right away and with Windows from then on. Right-click the bar, the island, or Gili's icon in the notification area for **Settings** and **Quit Gili**.

Already on an older version? Run this Gili-Setup over it. Your notes, todos, alarms, Shelf and settings stay.

Requirements: Windows 11, 64-bit (x64), main monitor.

## What changed

- **Claude tells you when it's done.** When Claude finishes while you're in another window, the island opens like a finished timer: **Claude is done**, the session's name and how long it took, with one Ping. It stays until you click **Close** or **Open Claude**, or open Claude Desktop yourself. It waits during a pomodoro focus session and stays out of fullscreen apps.
- **Air quality on the bar.** A new **AQI** module after the weather shows the US AQI, the index IQAir uses, for your weather city, with a dot in the level's colour, from green (Good) to dark red (Hazardous). Hover it for the level, PM2.5 and the city. Turn it off or move it in Settings › Right side.

## Known limits

- Unsigned for now: SmartScreen warns on first run, and updates are installed by running the new Gili-Setup yourself.
- Windows notifications in the island and turning on Windows' Do not disturb automatically aren't available yet.
- Explorer's progress is read in English Windows; other languages show the percent only.

## Privacy

Everything stays on your PC. Gili has no telemetry and no ads. It only connects to Open-Meteo for the weather and air quality, to LRCLIB for lyrics if you turn them on, and to GitHub to check for a new version. See the privacy policy in `docs/release/privacy-policy.md`.

## Uninstall

Windows Settings › Apps › Installed apps › Gili › Uninstall. Gili removes its Claude hooks and brings the Windows taskbar back; answer **Yes** to "Also delete my notes and settings?" to remove your data too.
