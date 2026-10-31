# Fase 3j: jendela pemakaian Claude 5 jam — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Jendela 5 jam dihitung mundur di slot dan halaman Claude, `Limit reached` dari hook `StopFailure`, dan kartu `Claude usage reset` saat berakhir (F-CLD-08).

**Architecture:** Core `ClaudeUsage` (mulai dari prompt, batas, berakhir, muat ulang) dengan `IClaudeWindowStore`; Data `ClaudeWindowStore` di tabel `claude_window`; aktivitas `ClaudeLimit`/`ClaudeWindow`; `IslandCard.HoldInFocus`; helper meneruskan `error` dan teks error `StopFailure`; App: `ClaudeService` (timer reset), `IslandController` (kartu, tick 1 detik hanya saat tampil), slot, baris ringkasan halaman, demo.

**Spec:** [desain 3h–3j, bagian Rancangan 3j](../specs/2026-10-31-fase-3h-claude-design.md), PRD 6.3 "Batas pemakaian Claude", 9.2, 10, F-CLD-08. Mockup: `limTip`, `limRow`, `limCheck`, `CARDS.lim`, slotHTML `lim`.

- [x] Task 1: Core `ClaudeUsage`, `ClaudeUsageText`, pengurai waktu reset, aktivitas dan kartu tahan-fokus, saklar `limOn`, `StopFailure` di papan dan daftar hook; tes.
- [x] Task 2: Data `ClaudeWindowStore`; tes SQLite.
- [x] Task 3: helper meneruskan `error` dan `last_assistant_message` hanya untuk `StopFailure`.
- [x] Task 4: App: timer reset, kartu, slot, gelembung, baris halaman, Open Claude, demo.
- [x] Task 5: verifikasi di salinan terpasang dan paket MSIX; dokumen.
