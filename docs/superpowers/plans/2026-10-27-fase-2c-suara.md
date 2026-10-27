# Fase 2c: Mesin Suara Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Lima suara bawaan (Radar, Chime, Bell, Soft, Ping) disintesis di aplikasi tanpa file, berbunyi untuk kartu timer dan pomodoro dengan Alert volume, Gradually increase, berhenti otomatis setelah 5 menit, dan kartu memperingatkan saat Windows di-mute.

**Architecture:** `Gili.Core.Sound` berisi definisi suara (nada mockup `VOICES`), penyintesis sampel, mixer dering (pengulangan, volume naik bertahap, fade 100 ms, batas 5 menit), dan aturan suara per kartu dan peringatan mute; semuanya fungsi deterministik yang dites. `Gili.Platform.Sound.WaveOutStream` memutar sampel lewat `waveOut` (WAVE_MAPPER, mengikuti perangkat default Windows). `Gili.App.SoundService` menghubungkan kartu yang tampil ke dering dan setting suara.

**Tech Stack:** .NET 10, winmm `waveOut*` (P/Invoke), Core Audio (`IAudioEndpointVolume.SetMute`, sudah ada di `AudioMonitor`), xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.6 (suara, kapan berbunyi, volume, mute, perangkat), F-SND-01–06, 6.5 Sounds; mockup JS `Sound`, `tone`, `VOICES`, `ring`, `chimeOnce`, `stopSound` (±1637–1680), `showCard` (`muted`, `Unmute`).

## Global Constraints

- Nada (mockup `tone`): osilator `sine` atau `triangle`; gain naik linear 0 → g dalam 6 ms, lalu turun eksponensial ke 0,0001 di akhir durasi; berhenti 50 ms setelahnya.
- Suara (offset s, frekuensi Hz, durasi s, bentuk, gain): **Radar** setiap 1,2 s: (0; 1318,5), (0,14; 1318,5), (0,28; 1568), durasi 0,11, triangle, 0,22. **Chime** setiap 2 s: (0; 1046,5; 1,4; 0,26), (0,18; 1318,5; 1,6; 0,22), (0; 2093; 0,6; 0,05), sine. **Bell** setiap 2,5 s: rasio (1; 0,3), (2,76; 0,12), (5,4; 0,06), (8,93; 0,03) dari 523,25 Hz, durasi 2,2 / rasio^0,3, sine. **Soft** setiap 2 s: 659,25 / 783,99 / 987,77 di 0 / 0,22 / 0,44, durasi 0,9, sine, 0,16. **Ping** setiap 2 s: (0; 1760; 0,5; 0,2), (0,09; 2637; 0,35; 0,08), sine.
- Dering berulang: gain = Alert volume (default 80%); dengan Gradually increase mulai 30% dari Alert volume, naik linear ke 100% dalam 30 detik (±1 detik). Berhenti sendiri setelah 5 menit. Stop: fade linear ke 0 dalam 100 ms; jarak dari klik ke mulai fade ≤ 50 ms.
- Kartu: `timer` → suara Timer sound (default Chime) berulang tanpa naik bertahap; `pomo-focus`, `pomo-break` → Bell sekali. Kartu hilang → suara berhenti.
- Peringatan (kartu dengan suara berulang): bila perangkat default di-mute atau volumenya < 5%, baris kuning `#FFD60A` `Sound is off · Windows volume is muted` di bawah subjudul; bila di-mute ada tombol `Unmute` paling kiri. Gili tidak pernah mengubah volume atau mute tanpa klik.
- Settings → Sounds: `Timer sound` (Radar/Chime/Bell/Soft/Ping, default Chime), `Alert volume` 0–100% (default 80), `Gradually increase` (default nyala) menjadi aktif; memilih Timer sound memutar contohnya sekali.
- Suara keluar di perangkat default Windows; format 44 100 Hz mono 16-bit; buffer 20 ms, maksimal 3 antre. Tanpa perangkat audio: kartu tetap muncul, kegagalan dicatat sekali di log.
- Anggaran PRD 8.1: tidak ada thread atau buffer selama tidak ada suara.

## Keputusan yang dibuat di rencana ini

1. **`waveOut` dengan WAVE_MAPPER**, bukan WASAPI/NAudio: tanpa dependensi, dan sejak Windows 7 aliran API lama dipindahkan otomatis ke perangkat default yang baru (F-SND-06).
2. **Alert volume diterapkan ke sampel**, jadi hanya suara Gili yang berubah (F-SND-04), tanpa menyentuh volume sistem.
3. **Tombol Unmute hanya saat di-mute**; volume < 5% hanya menampilkan peringatan, karena Gili tidak boleh menaikkan volume.
4. **Kartu yang tidak ditanggapi 5 menit**: suara berhenti (di sini); kartu mengecil ke compact dengan tanda merah dan toast "Missed" masuk paket alarm (2d) bersama kartu alarm.
5. **Contoh suara di Settings**: mengganti Timer sound memutar suara itu sekali; editor alarm (2d) memakai mekanisme yang sama.

## Review Focus

1. **Tombol kartu ditekan cepat berulang** (Repeat lalu Close): satu dering berjalan pada satu waktu; dering lama berhenti sebelum yang baru. Dites di Task 1 (`A_new_ring_replaces_the_old`).
2. **Perangkat audio tidak ada atau gagal dibuka**: tidak ada exception ke UI; kartu tetap; log satu baris. Diperiksa lewat review (Task 2).
3. **Mute berubah saat kartu tampil**: peringatan dan tombol Unmute muncul/hilang mengikuti event volume. Diperiksa manual.
4. **Alert volume 0%**: tetap "berbunyi" tanpa suara, tidak error. Dites di Task 1 (`Zero_volume_is_silence`).
5. **App ditutup saat suara berbunyi**: thread pemutar berhenti dan `waveOut` ditutup; proses keluar normal. Diperiksa manual.

---

### Task 1: Suara dan mixer (Core)

**Files:**
- Create: `src/Gili.Core/Sound/Voices.cs`, `src/Gili.Core/Sound/AlertMixer.cs`, `src/Gili.Core/Sound/SoundRules.cs`
- Test: `tests/Gili.Core.Tests/Sound/VoicesTests.cs`, `AlertMixerTests.cs`, `SoundRulesTests.cs`

**Interfaces:**
- Produces:
  - `public enum Waveform { Sine, Triangle }`; `public sealed record Tone(double Offset, double Frequency, double Duration, Waveform Shape, double Gain)`; `public sealed record Voice(string Name, double Every, IReadOnlyList<Tone> Tones)`.
  - `Voices.All` (urutan Radar, Chime, Bell, Soft, Ping), `Voices.Find(string name)` (tanpa peduli huruf besar; default Chime), `Voices.Render(Voice voice, int sampleRate) → float[]` (satu pola, panjang = max(offset + durasi + 0,05) detik).
  - `public sealed class AlertMixer(int sampleRate)`: `Ring(Voice, double volume, bool ramp)`, `Once(Voice, double volume)`, `Stop()` (fade 100 ms), `bool IsPlaying`, `int Read(Span<float> buffer)` (mengisi sampel berikutnya, mengembalikan jumlah yang ditulis; 0 = selesai), `static MaxRing = 5 min`, `static RampLength = 30 s`.
  - `SoundRules.WarnMuted(int volumePercent, bool muted) → bool` (< 5 atau muted), `SoundRules.Warning = "Sound is off · Windows volume is muted"`, `SoundRules.ForCard(string cardId, string timerSound) → (string Voice, bool Loop, bool Ramp)?`.
- [ ] **Step 1: Tes yang gagal**: `Five_voices_in_mockup_order`, `Unknown_voice_falls_back_to_chime`, `Tone_rises_in_6_ms_then_decays` (sampel ke-265 di 44,1 kHz ≈ gain puncak; sampel terakhir < 0,001 dari puncak), `Radar_pattern_is_three_beeps` (energi di tiga jendela mulai 0, 0,14, 0,28 s; sunyi di antaranya), `Bell_partials_ring_longer_at_low_pitch` (durasi nada pertama 2,2 s, keempat 2,2/8,93^0,3), `Ring_repeats_every_period` (Chime: pola mulai lagi di 2 s), `Ramp_starts_at_30_percent_and_reaches_full_at_30_s` (gain efektif dari amplop mixer di t=0 = 0,3·vol, t=15 s = 0,65·vol, t=30 s = vol), `Ring_stops_by_itself_after_5_minutes` (`Read` mengembalikan 0 setelah 300 s + fade), `Stop_fades_out_in_100_ms` (4410 sampel turun linear ke 0, lalu `Read` = 0), `A_new_ring_replaces_the_old`, `Once_plays_one_pattern_then_ends`, `Zero_volume_is_silence`, `Muted_or_under_5_percent_warns`, `Card_sounds_follow_the_prd` (`timer` → (Chime, loop, tanpa ramp) dengan timerSound "Chime"; `pomo-focus`/`pomo-break` → (Bell, sekali); lainnya null).
- [ ] **Step 2: Jalankan** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Sound"` → gagal kompilasi. **Step 3:** implementasi. **Step 4:** seluruh suite lulus.
- [ ] **Step 5: Commit** `feat(core): five built-in sounds synthesised from the mockup, with ring, ramp, fade and limit (F-SND-01..04)`.

### Task 2: Pemutar, SoundService, setting, dan peringatan mute

**Files:**
- Create: `src/Gili.Platform/Sound/WaveOutStream.cs`, `src/Gili.App/SoundService.cs`
- Modify: `src/Gili.Core/Settings/AppSettings.cs` (+ `TimerSound` "Chime", `AlertVolume` 80, `GraduallyIncrease` true; Normalize: volume 0–100, suara dikenal), `src/Gili.Core/Settings/SettingsCatalog.cs` (Sounds aktif: kunci `snd`, `alertVol`, `ramp`), tes `SettingsCatalogTests`/`AppSettingsTests`, `src/Gili.Platform/Modules/AudioMonitor.cs` (+ `Unmute()`), `src/Gili.App/Bar/RightService.cs` (expose `Audio`), `src/Gili.App/Island/IslandController.cs`, `CardContent.cs`, `CardView.xaml`, `IslandViewModel.cs`, `src/Gili.App/App.xaml.cs`, `src/Gili.App/Resources/Icons.xaml` (+ `Icon.Mute` bila belum ada)

**Interfaces:**
- Consumes: Task 1 (`AlertMixer`, `Voices`, `SoundRules`).
- Produces:
  - `WaveOutStream(int sampleRate, Func<Span<float>, int> read, Action finished)`: membuka `waveOutOpen(WAVE_MAPPER, 44100 Hz mono 16-bit, CALLBACK_EVENT)`, thread latar mengisi buffer 20 ms (maks. 3 antre) dari `read`, memanggil `finished` saat `read` mengembalikan 0 lalu menutup; `Dispose()` menghentikan dan menutup. Gagal buka → `InvalidOperationException`.
  - `SoundService(SettingsHub, FileLog)`: `PlayForCard(string? cardId)` (null = berhenti), `Preview(string voice)`, `Stop()`, `Dispose()`; satu `AlertMixer` dan satu `WaveOutStream` aktif paling banyak.
  - `CardContent` + `string? Warning`; tombol `Unmute` di depan bila di-mute; `AudioMonitor.Unmute()` memanggil `SetMute(false)`.
  - Settings: kunci `snd` (Segment, opsi lima nama), `alertVol` (Range 0–100 %), `ramp` (Switch) tersedia; `SettingsCatalog.Read/Write/Range` mendukungnya.
- [ ] **Step 1: Tes yang gagal (Core)**: perbarui `Phase_1_rows_are_available` → tambah `snd`, `alertVol`, `ramp`; `Sound_settings_read_and_write` (`Write(s,"snd","Bell").TimerSound == "Bell"`, `Write(s,"alertVol",55).AlertVolume == 55`, `Write(s,"ramp",false).GraduallyIncrease == false`); `Sound_defaults_match_the_prd` (Chime, 80, true); `Alert_volume_is_clamped_to_0_100`. Jalankan → gagal; implementasi Core → lulus.
- [ ] **Step 2:** `WaveOutStream` dan `SoundService` sesuai Interfaces; kegagalan membuka perangkat ditangkap dan dicatat sekali (Review Focus 2).
- [ ] **Step 3:** `IslandController`: saat `View.Card` berganti ke kartu baru → `SoundService.PlayForCard(id)`; saat kartu hilang → `PlayForCard(null)`. `CardFor` menambah `Warning` dari `SoundRules.WarnMuted(volume, muted)` untuk kartu berulang dan tombol Unmute bila muted; perubahan volume/mute memperbarui kartu yang tampil.
- [ ] **Step 4:** `App`: buat `SoundService`; `settings.Changed` dengan `TimerSound` berubah → `Preview`; `OnExit` → `Dispose`.
- [ ] **Step 5:** build 0 warning, tes lulus. Manual: kartu timer 10 s (dering terdengar oleh pengguna; saya memeriksa `waveOut` terbuka tanpa error dan proses menutupnya setelah Close), Repeat lalu Close cepat, mute Windows lalu munculkan kartu (peringatan + Unmute; klik Unmute membuka mute), Settings → Sounds aktif.
- [ ] **Step 6: Commit** `feat(app): sounds for timer and pomodoro cards, alert volume, ramp and a muted warning (F-SND-01..06)`.

### Task 3: Verifikasi

- [ ] **Step 1:** Release build 0 warning, tes, format; publish; CPU dan RAM diam tidak berubah dibanding sebelum paket (tidak ada thread suara saat diam).
- [ ] **Step 2:** catat di ledger.
