# Gili support

## Report a bug or ask a question

Open an issue at https://github.com/fhdyt/gili/issues. Please include:

- Your Gili version and where you got it: **Settings › About › Version** (for example `0.1.1 · GitHub`).
- Your Windows version (**Settings › System › About**), screen resolution and scale.
- What you did, what you expected, and what happened instead. A screenshot helps.
- The log from the day it happened: **Settings › About › Open logs folder**, then attach the `gili-<date>.log` file. Logs never contain your notes, todos, clipboard text or Claude Code sessions.

## Common questions

**Windows says "Windows protected your PC" when I run the installer.**
Test builds of Gili are not signed yet. Click **More info**, then **Run anyway**. Signed releases on GitHub and the Microsoft Store version do not show this.

**Space is left empty at the top of my screen after Gili closed.**
That happens only when Gili is ended by force (for example from Task Manager). Start Gili and quit it from the bar's menu, or restart Windows Explorer.

**My Windows taskbar is hidden.**
Turn off **Settings › General › Hide Windows taskbar**, or quit Gili: the taskbar comes back whenever Gili quits.

**I uninstalled the Microsoft Store version and Claude Code still calls Gili.**
Before uninstalling it, use **Settings › About › Prepare to uninstall**. If it is already gone, delete the entries that mention `gili-claude-hook` from `~/.claude/settings.json`.

**Will I lose my notes when I update or reinstall?**
No. Updates keep everything, and a copy of your notes and settings in `%USERPROFILE%\.gili` brings them back after a reinstall, also when you move between the GitHub and Microsoft Store versions.

## Privacy

See the [privacy policy](privacy-policy.md).
