# Release notes: Gili 0.3.0 (draft)

Rilis GitHub kedua, diunggah pengguna sendiri. Tag `v0.3.0`, aset `Gili-Setup-0.3.0.exe` (nama ini yang dicari cek update Gili, jangan diganti). Teks di bawah garis bahasa Inggris dan siap ditempel ke halaman rilis.

---

**Gili** is a glass bar for the top of your Windows 11 screen, with a black island in the middle that shows what matters right now: music, timers, copies, files, your Claude sessions, and Gili, a pair of eyes that follows your pointer.

## Install

1. Download **Gili-Setup-0.3.0.exe** below and run it. No administrator rights, no .NET, nothing else to install.
2. This build isn't signed yet, so Windows shows **"Windows protected your PC"** once. Click **More info**, then **Run anyway**.
3. Gili starts right away and with Windows from then on. Right-click the bar, the island, or Gili's icon in the notification area for **Settings** and **Quit Gili**.

Already on 0.2.0? Run this Gili-Setup over it. Your notes, todos, alarms, Shelf and settings stay. Saved clipboard history doesn't: see below.

Requirements: Windows 11, 64-bit (x64), main monitor.

## What's new

- **Projects**: a new island page with a day timeline, one project at a time. Tasks are bars in a note color. Drag a bar to move it, drag an end to change a date, tick it done; overdue tasks turn red. Open it from the new Projects icon in the launcher.
- **Weekly limit**: the Claude page has a Weekly limit row under the 5-hour one, with the time left and a card when it resets. Type the reset's day and time from Claude Desktop.
- **Set the 5-hour reset yourself**: click the 5-hour window row on the Claude page and enter the time Claude Desktop shows, so the timer matches Claude's own instead of an estimate.

## What changed

- **The Clipboard tab is gone.** Its page, the history of text, images and files, paste to the cursor, the launcher icon and `Ctrl+Alt+V` are removed. **Updating permanently deletes the clipboard history saved by 0.2.0.** A copy or cut still shows in the island for 3 seconds.
- **Claude sessions follow Claude Desktop.** Sessions and the 5-hour timer show only while Claude Desktop has a window on the taskbar. Sessions from the `claude` command line are no longer shown.

## Fixes

- The 5-hour window now starts on the first use after its reset, and tool calls and the end of a turn count as use, not only prompts.
- A limit message whose reset is days away no longer moves the 5-hour window.

## Known limits

- Unsigned for now: SmartScreen warns on first run, and updates are installed by running the new Gili-Setup yourself.
- Windows notifications in the island and turning on Windows' Do not disturb automatically aren't available yet.
- Explorer's progress is read in English Windows; other languages show the percent only.

## Privacy

Everything stays on your PC. Gili has no telemetry and no ads. It only connects to Open-Meteo for the weather, to LRCLIB for lyrics if you turn them on, and to GitHub to check for a new version. See the privacy policy in `docs/release/privacy-policy.md`.

## Uninstall

Windows Settings › Apps › Installed apps › Gili › Uninstall. Gili removes its Claude hooks and brings the Windows taskbar back; answer **Yes** to "Also delete my notes and settings?" to remove your data too.
