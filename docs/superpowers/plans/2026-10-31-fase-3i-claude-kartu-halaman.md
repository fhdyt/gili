# Fase 3i: kartu Needs input, belum dilihat, halaman Claude — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sesi Claude yang menunggu membuka kartu sekali, sesi yang selesai di belakang meninggalkan gelembung hijau, halaman Claude mendaftar semua sesi, dan klik membawa jendela sesi ke depan (F-CLD-03–06, F-LEFT-05).

**Architecture:** Core: `ClaudeBoard` (belum dilihat, `WaitId`, host), `ClaudeCards`, `ClaudePage`, `ClaudeWindows`, `ClaudeSlot` (titik, `Involved`), `IslandCoordinator.SetClaudeUnseen` → `IslandView.DoneBubble`. Platform: `ProcessTree` (Toolhelp + waktu mulai) dan jendela per PID. App: `ClaudeService` (jendela, foreground, demo), `IslandController` (kartu `claude`, klik slot/gelembung), `ClaudePage`, gelembung hijau di `IslandWindow`, titik di ikon kiri, demo.

**Spec:** [desain 3h–3j, bagian Rancangan 3i](../specs/2026-10-31-fase-3h-claude-design.md), PRD 6.2, 6.3 "Sesi Claude", halaman 9, 9.2, F-CLD-03–06. Mockup: `CL_ASK`, `clIcon`, `clSlotHTML`, `renderCbub`, `clRight`, `renderClPage`, `clAsk`, `clCardFor`, `clHeld`, `clDone`, `clSeen`, `clClick`, `.crow`, `.bubble.cdone`, `.bn`, `.clp.dot`, `.lcl.seen`, `CARDS.claude`.

## Global Constraints

- Teks persis: `Claude needs your input`, `Claude needs permission`, `Plan ready for review`, `Later`, `Open Claude`, `Open terminal`, `Needs input`, `Working · 2:14`, `Done · 4m 12s`, `Idle since 14:05`, `No Claude sessions`, `Claude · 2 finished`, `2 Claude sessions finished`, `Claude finished: <name>`, `Turn on Claude Code sessions first`.
- Cuplikan pertanyaan di kartu maks. 90 karakter (89 + `…`).
- Data sesi hanya di memori; tidak ada log berisi nama sesi atau cuplikan.
- Tanpa timer baru saat diam (desain "Anggaran").

## Review Focus

1. Pertanyaan yang sama tidak membuka kartu dua kali (file `waiting` lalu hook `Notification`): `Same_wait_never_cards_twice`.
2. Kartu ditahan saat fokus lalu tampil saat istirahat hanya bila sesi masih menunggu: coordinator (sudah) + `Answered_wait_dismisses_card`.
3. Fullscreen melewati kartu: `Fullscreen_skips_new_wait`.
4. Done yang belum dilihat tidak memegang slot setelah 4 detik, tapi tetap Done di halaman: `Unseen_done_stays_done_after_four_seconds`.
5. PID dipakai ulang tidak membawa ke jendela yang salah: `Stops_when_parent_started_after_child`.

---

### Task 1: Core — papan, slot, kartu, halaman, jendela, coordinator

**Files:** Modify `src/Gili.Core/Claude/ClaudeBoard.cs`, `ClaudeModels.cs`, `ClaudeSlot.cs`, `src/Gili.Core/Island/IslandCoordinator.cs`, `IslandModels.cs`, `src/Gili.Core/Sound/SoundRules.cs`, `src/Gili.Core/Settings/AppSettings.cs`, `SettingsCatalog.cs`; Create `src/Gili.Core/Claude/ClaudeCards.cs`, `ClaudePage.cs`, `ClaudeWindows.cs`; Tests in `tests/Gili.Core.Tests/Claude/*`, `Island/IslandCoordinatorTests.cs`, settings tests.

**Interfaces (Produces):**
- `ClaudeBoard(Func<DateTimeOffset> now, Func<ClaudeSession, bool>? isFront = null)`; `void MarkSeen(string id)`; `void MarkSeen(Func<ClaudeSession, bool> front)`; `void SetHost(string id, string? host)`.
- `ClaudeSession`: `bool Unseen`, `long WaitId`, `DateTimeOffset? FinishedAt`, `string? Host`, `bool ShowsDone => Status == Done && DoneUntil is not null`, `string? Folder`.
- `ClaudeSlotView` + `bool Dot`; `ClaudeSlot.Unseen(sessions)`, `ClaudeSlot.Involved(sessions)`, `ClaudeSlot.AppName(ClaudeSession)`; `LauncherTooltip` dengan `finished`.
- `record ClaudeCardView(string SessionId, string Title, string Name, string? Quote, bool Terminal)`; `enum ClaudeCardChange { None, Show, Refresh, Dismiss }`; `class ClaudeCards { ClaudeCardView? Current; ClaudeCardChange Update(IReadOnlyList<ClaudeSession>, bool fullscreen); void Close(); }`.
- `enum ClaudeRowKind { Ask, Work, Done, Idle }`; `record ClaudeRow(string Id, ClaudeRowKind Kind, string Name, string Sub, string Right, string Tooltip)`; `static ClaudePage.Rows(sessions, now, use24Hour)`.
- `readonly record struct ProcessFacts(int Parent, long StartTicks, string Name)`; `static ClaudeWindows.Find(int pid, Func<int, ProcessFacts?> facts, Func<int, nint> windowOf) → (nint Window, int Pid)?`.
- `IslandCoordinator.SetClaudeUnseen(bool)`; `IslandView.DoneBubble`.
- `SoundRules.ForCard("claude") → ("Ping", false, false)`.
- `AppSettings.ShownHiddenLeft` (Claude ikut tersembunyi saat integrasi mati) dan `HiddenTabs` memakainya; `SettingsCatalog.State` untuk `L.Claude`/`T.Claude`.

- [ ] Tulis tes gagal untuk tiap perilaku di atas, jalankan, implementasi, jalankan `dotnet test tests/Gili.Core.Tests`.
- [ ] Commit `feat(core): ...`.

### Task 2: Platform — jendela sesi

**Files:** Create `src/Gili.Platform/Claude/SessionWindows.cs` (Toolhelp snapshot, `GetProcessTimes`, `EnumWindows` per PID, deskripsi exe, `IsFront`).

- [ ] Build; cek manual dengan sesi di laptop (Desktop → `Claude`, CLI → Windows Terminal).

### Task 3: App — kartu, gelembung, halaman, ikon kiri, klik, demo

**Files:** Modify `ClaudeService.cs`, `IslandController.cs`, `IslandViewModel.cs`, `IslandWindow.xaml(.cs)`, `CardContent.cs`, `CardView.xaml(.cs)`, `LeftSide.xaml(.cs)`, `BarWindow.xaml.cs`, `AppsService.cs`, `App.xaml.cs`, `DemoService.cs`, `src/Gili.Core/Demo/DemoScript.cs`; Create `ClaudePage.xaml(.cs)`, `ClaudePageViewModel.cs`.

- [ ] Kartu `claude` (CardContent `Quote`, `Filled`, `Pulse`), Later/Open, Ping.
- [ ] Gelembung hijau + lencana, titik di slot/gelembung aktivitas, ikon kiri hijau.
- [ ] Halaman Claude menggantikan placeholder; klik baris.
- [ ] Klik slot (satu sesi → jendela), klik gelembung hijau.
- [ ] Foreground → MarkSeen; Stop → isFront.
- [ ] Demo Claude.
- [ ] `dotnet build`, `dotnet format --verify-no-changes`, `dotnet test`, publish, cek di demo dan dengan sesi sungguhan; commit.

### Task 4: Dokumen

- [ ] Hasil verifikasi 3i di desain, PRD (bila ada yang berubah), commit, `scripts/publish.ps1`.
