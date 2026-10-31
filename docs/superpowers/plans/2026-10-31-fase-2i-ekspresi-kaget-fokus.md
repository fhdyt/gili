# Fase 2i: Ekspresi Kaget dan Fokus Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sisa F-ISL-24 Fase 2: Gili kaget di samping kartu alarm, timer, pomodoro, dan pengingat todo (lingkarannya tetap tampil di kiri atas kartu), dan fokus saat slot berisi timer, stopwatch, atau fase fokus pomodoro yang berjalan, dengan lirikan ke angka slot setiap menit berganti.

**Architecture:** Aturan di `Gili.Core.Mascot.GiliRules`: urutan ekspresi, kartu mana yang mengagetkan, angka menit slot, dan kapan melirik. `GiliService` menerima status kaget dan fokus dari `IslandWindow` (hanya saat tampilan pulau atau timer berubah), membaca angka menit slot sekali per detik di detak yang sudah ada, dan melirik 1,2 detik ke slot. `IslandWindow` menaruh lingkaran Gili di kiri atas kartu yang mengagetkan dan menurunkan titik privasi ke bawahnya.

**Spec:** PRD 6.3 "Maskot Gili" (Saat pulau mengambang, Titik kamera/mic), "Ekspresi Gili" baris 1 dan 5 serta "Arah pandang"; F-ISL-24. Mockup: `STARTLE`, `gMood`, `gTarget`, detak per detik (`GX.min`, `glanceUntil`), `.x-surprise`, `.x-focus`, posisi `gCirc` dan titik privasi di `layout()`.

## Global Constraints

- Urutan (atas menang): Kaget, Senang, Mata besar, Tidur, Fokus, Menikmati musik, Netral.
- Kaget: kartu `alarm:*`, `timer`, `timer:*`, `pomo-focus`, `pomo-break`, `todo:*`. Mata 130% dan bulat penuh (rupa Mata besar), tidak berkedip. Lingkaran tetap tampil, `IslandHeight` px, 6 px di kiri kartu dengan tepi atasnya sejajar tepi atas kartu (tepi bawah bila bar di bawah). Titik privasi: di kiri kartu, mulai `IslandHeight + 8` px di bawah tepi atas kartu.
- Fokus: slot `Timer` dengan pomodoro fase fokus berjalan, atau timer berjalan bila tidak ada pomodoro; slot `Stopwatch` yang berjalan. Fase istirahat pomodoro netral. Mata 70% tinggi dari tengah, tetap berkedip.
- Angka menit: timer dan pomodoro = sisa menit dibulatkan ke atas; stopwatch = menit berjalan dibulatkan ke bawah. Lirikan 1,2 detik ke tengah angka slot saat angka berganti selama Fokus, paling sering sekali per 4 detik. Lirikan kalah dari note yang diawasi (Fase 2h) dan menang atas kursor.
- Tanpa timer baru: angka dibaca di detak per detik `GiliService` yang hanya jalan selama Gili tampil.

## Keputusan

1. **Ekspresi dihitung ulang hanya saat ada perubahan**: tampilan pulau (kartu, slot) dan `TimerService.Changed` (fase pomodoro, mulai/jeda). Angka menit tidak mengubah ekspresi, hanya memicu lirikan.
2. **Bar di bawah**: lingkaran kaget dicerminkan seperti kartu (`IslandLayout.FromEdge`), jadi menempel di sisi kartu yang dekat bar.
3. **Demo**: skenario sudah memuat kartu timer, alarm, dan pengingat (Kaget) serta timer berjalan (Fokus); tidak ada langkah baru.

### Task 1: Aturan (Core) dan tes
- [x] `GiliMood` + `Focus`, `Surprise`; `GiliInputs` + `Startled`, `Focused`; `Mood` sesuai urutan; `CanBlink` tanpa Kaget.
- [x] `Startles(string cardId)`, `TimerMinute(TimeSpan)`, `StopwatchMinute(TimeSpan)`, `Glances(int? before, int? now, DateTime lastGlance, DateTime at)`, `GlanceFor = 1,2 s`, `GlanceGap = 4 s`.
- [x] Tes untuk semua aturan di atas.

### Task 2: Kaget di app
- [x] Rupa mata Kaget dan Fokus di `GiliEyes`.
- [x] `IslandWindow.UpdateGili`: lingkaran tampil di samping kartu yang mengagetkan; `Apply` menaruhnya di kiri atas kartu; titik privasi turun.
- [x] `GiliService.SetStartled`.

### Task 3: Fokus di app
- [x] `IslandWindow` menghitung fokus dan angka menit dari slot; memanggil `GiliService.SetFocus` saat view atau timer berubah.
- [x] `GiliService`: lirikan di detak per detik, target tengah angka slot.

### Task 4: Verifikasi dan dokumen
- [x] Build, tes, format, potret kartu timer dan slot timer (mode demo lewat UIA bila bisa), publish, commit, PRD bila ada temuan.

## Verifikasi (31 Oktober 2026)

- 877 tes unit lulus (aturan baru: urutan ekspresi, kartu yang mengagetkan, angka menit timer dan stopwatch, jeda lirikan 4 detik), `dotnet format` bersih.
- Fokus diuji di laptop pengembang tanpa input sintetis: stopwatch berjalan ditulis ke `gili.db` (lalu dikembalikan ke keadaan semula), Gili dijalankan, dan potret slot menunjukkan mata lingkaran menyipit di samping stopwatch. Pada pergantian menit, log sementara dan pengukuran posisi mata di layar sama-sama menunjukkan lirikan: mata naik sekitar 2 px dan bergeser ke kanan ke arah angka selama 1,2 detik, lalu kembali ke kursor.
- Kaget di samping kartu dicek pengguna lewat Demo mode (31 Oktober 2026) dan dinyatakan aman. Yang dicek: klik kanan bar → Demo mode: kartu timer di detik 13,5, kartu alarm di 36,5, dan pengingat di 42 harus menampilkan lingkaran Gili bermata besar di kiri atas kartu, dengan titik kamera/mic (detik 22,5–33) di bawahnya bila tampil bersamaan.
