# Fase 4a: riwayat dan halaman Clipboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 50 teks terakhir yang dicopy tersimpan dan tampil di halaman Clipboard; memilih satu menempelkannya di posisi kursor jendela sebelumnya (F-LEFT-03, F-ISL-28, F-ISL-07 Clipboard).

**Spec:** [desain 4a](../specs/2026-10-31-fase-4a-clipboard-design.md).

- [x] Task 1: Core `ClipboardHistory`, `ClipboardEntry`, `IClipboardHistoryStore`, `ClipboardText` (baris, waktu); Data `ClipboardHistoryStore`; tes.
- [x] Task 2: Platform: nama aplikasi pemilik clipboard, tulis teks ke clipboard, cek jendela admin, kirim Ctrl+V.
- [x] Task 3: App: `ClipboardService` mengisi riwayat dan mengabaikan tulisannya sendiri; `ClipboardPage` + view model (cari, sorot, hapus, Clear all, kosong); tempel lewat `IslandWindow`; placeholder tab dihapus.
- [x] Task 4: build, format, tes, publish, cek di laptop, dokumen, commit.
