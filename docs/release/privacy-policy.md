# Gili privacy policy

Last updated: 1 November 2026

Gili is a bar for the top of your Windows 11 screen. It is free, it has no accounts, and it does not collect anything about you. This page lists everything Gili stores and everything it sends over the internet.

## What stays on your PC

Everything Gili keeps is stored on your PC only:

- **Notes, todos, alarms, timers, the stopwatch, Shelf items and your settings.** The Microsoft Store version keeps them in its own app folder (`%LOCALAPPDATA%\Packages\Gili_…\LocalState`); the version from GitHub keeps them in `%LOCALAPPDATA%\Gili`. A copy of your notes and settings is also kept in `%USERPROFILE%\.gili` so they come back when you reinstall Gili. Shelf items are links to your files, except files you drop from inside a zip or a temporary folder, which Gili copies into its own folder until you drag them out or remove them.
- **Clipboard history.** The last 50 things you copied, so you can paste them again from the Clipboard page: text, the paths of copied files, and copied images (at most 20, saved as PNG files in Gili's folder). Gili never reads or keeps what an app marks as private (password managers do this). You can remove items or clear the history on the Clipboard page, which also deletes their images.
- **Logs** of the last 7 days, to help with bug reports. They never contain your notes, todos, clipboard text or Claude Code sessions.

Some things Gili only holds in memory while it runs and never writes to disk: thumbnails of copied images, and what it shows about your Claude Code sessions.

## What Gili sends over the internet

Gili does not send your data anywhere. It only makes these requests:

| Request | What is sent | When |
|---|---|---|
| Weather, to [Open-Meteo](https://open-meteo.com/) | The coordinates of the city you chose | About every 30 minutes while the weather is shown |
| City search, to Open-Meteo | The city name you type | Only while you search for a city in Settings |
| Update check, to GitHub | Nothing about you; Gili asks for the latest version number | Once a day, only in the version from GitHub |

The Microsoft Store version is updated by the Microsoft Store. Like any website, these services can see your IP address when Gili connects to them.

Gili has no telemetry, no analytics, no ads and no automatic crash reports. If Gili crashes, Windows may send its own crash report to Microsoft, which the developer can see in Microsoft's Partner Center.

## Claude Code

When you turn on **Show Claude Code sessions** in Settings, Gili adds its hooks to `~/.claude/settings.json` (it keeps a backup and leaves your other hooks alone) and listens to your Claude Code sessions on your PC. Session names, folders and questions are shown in the island and kept in memory only. Your prompts are never sent to Gili. Turning the switch off, or uninstalling Gili, removes the hooks.

## Other apps

Gili reads what Windows already shows: what is playing, Wi‑Fi, Bluetooth, battery, CPU, memory, disk and network use. None of it leaves your PC.

## Removing your data

- **Version from GitHub:** uninstall Gili in Windows Settings › Apps and answer **Yes** to "Also delete my notes and settings?".
- **Microsoft Store version:** uninstalling removes its app folder. To also remove the reinstall copy, delete `%USERPROFILE%\.gili`.

## Contact

Questions about privacy: open an issue at https://github.com/fhdyt/gili/issues.
