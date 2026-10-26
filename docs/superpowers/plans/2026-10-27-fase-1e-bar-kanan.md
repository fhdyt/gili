# Fase 1e: Bar Kanan Persis Mockup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bagian kanan bar berisi semua modul mockup dengan data asli: cuaca, jaringan, CPU, RAM, Wi‑Fi, Bluetooth, volume, brightness, tray, jam, Settings, dan Power, plus menu "…" saat ruang habis dan pulau Power.

**Architecture:** Aturan teks/ikon (kode cuaca, kekuatan sinyal, ikon volume, baris baterai, overflow modul) di `Gili.Core` dan dites. `Gili.Platform` membaca Open-Meteo, WLAN API, radio dan perangkat Bluetooth (WinRT), Core Audio, dan WMI brightness, serta menjalankan Quick Settings, ikon tersembunyi, Sleep, Shut down, dan Restart. `Gili.App` menggambar modul dan pulau kontrol Power.

**Tech Stack:** .NET 10 WPF, `HttpClient` + `System.Text.Json`, `wlanapi.dll`, `Windows.Devices.Radios` + `DeviceWatcher`, Core Audio COM, `System.Management` (WMI), xUnit.

**Spec:** [PRD](../specs/2026-10-25-top-taskbar-prd.md) 6.4 (modul, pulau kontrol Power), 8.1, 9.2, 11; 14 (keputusan 2026-10-27: tampilan bar lengkap dengan data asli dan fungsi dasar); mockup CSS baris 103–148 dan 181–246, JS baris 2754–2886, HTML baris 1093–1130.

## Global Constraints

- Modul: tinggi area klik = tinggi bar − 6 (min. 24), padding 0 4, radius 6, celah 2, min. lebar 24; hover putih 8%, ditekan 14%. Ikon 16 px (chevron tray 14 px). Ikon cuaca `#AEAEB2`, ikon lain warna teks.
- Dua baris: label 8,5 px semibold kapital, jarak huruf 0,06 em, `#9A9AA0`; nilai 11,5 px semibold digit tabular. Ikon di kiri blok dua baris dengan celah 6.
- Urutan: Cuaca, Jaringan, CPU, RAM, "…", Wi‑Fi, Bluetooth, Volume, Brightness, Tray, Jam, Settings, Power.
- Teks UI persis: `Show hidden icons`, `Settings`, `Power`, `Volume 46%`, `Muted`, `Brightness 70%`, `Bluetooth · <perangkat>` / `Bluetooth · On` / `Bluetooth · Off`, `Strong signal · Home_5G`, `Wi‑Fi is off`, `Sleep`, `Shut down`, `Restart`, `Windows Quick Settings`, `All settings`, baris baterai `64% · 2 h 10 min` / `Charging · 64%`.
- Pulau kontrol: lebar 360, radius 28, isi 16 px dari tepi, tanpa menu tab dan tanpa Gili, tepi atas sama dengan compact, pegas buka/tutup sama, glow biru `#0A84FF`; tinggi = isi + 32.
- Tidak ada polling untuk volume, mute, dan Bluetooth (event sistem, PRD 6.4). Cuaca tiap 30 menit; Wi‑Fi tiap 10 detik plus event koneksi; brightness dibaca saat tooltip dibuka.

## Keputusan yang dibuat di rencana ini

1. **Kota cuaca default Jakarta** (−6,2088, 106,8456) sampai Settings punya pilihan kota (paket 4); nama kota tampil di tooltip.
2. **Fungsi dasar sampai Fase 3**: klik Wi‑Fi, Bluetooth, Volume, Brightness membuka Quick Settings Windows (`Win+A`); tray membuka ikon tersembunyi Windows (`Win+B` lalu `Enter`). Power membuka pulau Power (tanpa baris update; deteksi update menunggu diverifikasi di Fase 3).
3. **Wi‑Fi tanpa izin Location**: ikon saja dengan tooltip `Allow location access for Gili to show the Wi‑Fi name (Settings > Privacy & security > Location)`.
4. **Label kekuatan**: kualitas sinyal ≥ 75 `Strong`, ≥ 50 `Good`, ≥ 25 `Fair`, selain itu `Weak`.
5. **Label cuaca** (kode WMO → label bar / kondisi panjang): 0 `Clear`/`Clear sky`, 1 `Clear`/`Mainly clear`, 2 `Cloudy`/`Partly cloudy`, 3 `Overcast`/`Overcast`, 45 dan 48 `Fog`/`Fog`, 51–57 `Drizzle`/`Drizzle`, 61–67 `Rain`/`Rain`, 71–77 dan 85–86 `Snow`/`Snow`, 80–82 `Showers`/`Rain showers`, 95–99 `Storm`/`Thunderstorm`, kode lain `Weather`/`Weather`. Tooltip: `<kondisi panjang> · <maks>° / <min>° · <kota>`.

## Review Focus

1. **Offline atau Open-Meteo gagal**: nilai terakhir tetap tampil abu-abu, tanpa exception; dicoba lagi 5 detik, 30 detik, lalu tiap 2 menit (PRD 11). Dites di Task 1 (`RetryDelay`).
2. **Tanpa adaptor Wi‑Fi / tanpa Bluetooth / layar eksternal tanpa brightness**: modulnya disembunyikan, tidak "—". Diperiksa manual.
3. **Volume 0% tapi tidak mute**: ikon mute (mockup `!S.vol`). Dites di Task 1.
4. **Layar sempit**: modul pindah ke "…" dengan urutan Cuaca, Jaringan, Disk, GPU, RAM, CPU; Wi‑Fi sampai Power tidak pernah pindah. Dites di Task 1.
5. **Perangkat audio default berganti** (headset dicolok): ikon volume mengikuti perangkat baru. Ditangani Task 2 (`IMMNotificationClient`), diperiksa manual.

---

### Task 1: Aturan modul kanan (Core)

**Files:** Create `src/Gili.Core/Modules/RightRules.cs`, test `tests/Gili.Core.Tests/Modules/RightRulesTests.cs`; modify `src/Gili.Core/Settings/AppSettings.cs` (+ `WeatherCity` = `Jakarta`, `WeatherLatitude` = −6,2088, `WeatherLongitude` = 106,8456).

**Interfaces (Produces):**
- `public enum WeatherIcon { Sun, Moon, CloudSun, Cloud, Fog, Drizzle, Rain, Snow, Storm }`; `WeatherText.For(int code, bool day) -> (string Label, string Long, WeatherIcon Icon)`; `WeatherText.Tooltip(string long, double max, double min, string city)`.
- `WifiText.Strength(int quality) -> string`; `WifiText.Name(string ssid) -> string` (maks. 16, dipotong `…`).
- `public enum VolumeLevel { Mute, Low, Mid, High }`; `VolumeText.Level(int percent, bool muted)`; `VolumeText.Tooltip(int percent, bool muted)`.
- `BatteryText.Line(int? percent, bool charging, TimeSpan? remaining) -> string` (`64% · 2 h 10 min`, `64% · 45 min`, `Charging · 64%`, `64%`, `""` tanpa baterai).
- `RightOverflow.Hide(IReadOnlyList<(string Id, double Width)> modules, double available) -> IReadOnlyList<string>` dengan urutan `weather, net, disk, gpu, ram, cpu`; lebar "…" 32 ikut dihitung begitu satu modul pindah.
- `ProviderRetry.Delay(int failures) -> TimeSpan` (5 detik, 30 detik, lalu 2 menit).

- [ ] **Step 1: Tes yang gagal** — minimal: `Partly_cloudy_by_day_is_cloud_sun`, `Clear_at_night_is_a_moon`, `Thunderstorm_codes_are_storm`, `Tooltip_reads_like_the_mockup` (`Partly cloudy · 31° / 24° · Jakarta`), `Signal_strength_labels`, `Long_network_names_are_cut_at_16`, `Zero_volume_shows_mute`, `Volume_levels_split_at_34_and_67`, `Battery_line_variants`, `Overflow_hides_weather_first_then_network`, `Controls_never_overflow`, `Retry_backs_off_5s_30s_then_2min`.
- [ ] **Step 2:** jalankan → gagal (tipe belum ada). **Step 3:** implementasi. **Step 4:** lulus. **Step 5: Commit** `feat(core): right module texts, icons, overflow and retry rules (F-RIGHT-02..05, F-RIGHT-10)`.

### Task 2: Provider Platform

**Files:** Create `src/Gili.Platform/Modules/WeatherClient.cs`, `WifiMonitor.cs`, `BluetoothMonitor.cs`, `AudioMonitor.cs`, `BrightnessReader.cs`, `src/Gili.Platform/Shell/SystemActions.cs`; modify `src/Gili.Platform/Gili.Platform.csproj` (+ `System.Management`).

**Interfaces (Produces):**
- `WeatherClient.GetAsync(double lat, double lon, CancellationToken) -> Task<WeatherReading>` (`record WeatherReading(double Temperature, int Code, bool Day, double Max, double Min)`), URL `https://api.open-meteo.com/v1/forecast?latitude=…&longitude=…&current=temperature_2m,weather_code,is_day&daily=temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=1`, timeout 10 detik.
- `WifiMonitor : IDisposable` — `WifiState? Read()` (`record WifiState(bool RadioOn, bool Connected, string? Ssid, int Quality, bool NameDenied)`; null = tanpa adaptor), event `Changed` dari `WlanRegisterNotification` (thread pool).
- `BluetoothMonitor : IDisposable` — `StartAsync()`, `BluetoothState? Current` (`record BluetoothState(bool On, string? Connected)`; null = tanpa radio), event `Changed` (radio `StateChanged`, `DeviceWatcher` perangkat tersambung).
- `AudioMonitor : IDisposable` — `(int Percent, bool Muted)? Current`, event `Changed` (`IAudioEndpointVolumeCallback`, ganti perangkat default lewat `IMMNotificationClient`).
- `BrightnessReader.Read() -> int?` (WMI `root\wmi` `WmiMonitorBrightness.CurrentBrightness`; null = tidak didukung).
- `SystemActions.QuickSettings()`, `HiddenIcons()`, `Sleep()` (`SetSuspendState(false, false, false)`), `ShutDown()` (`shutdown.exe /s /t 0`), `Restart()` (`shutdown.exe /r /t 0`), `OpenSettings(string uri)`.

- [ ] **Step 1:** implementasi. **Step 2:** build 0 warning. **Step 3: Commit** `feat(platform): weather, Wi-Fi, Bluetooth, audio and brightness readers; Quick Settings and power actions (F-RIGHT-02, F-RIGHT-05, F-RIGHT-07, F-RIGHT-10)`.

### Task 3: Modul kanan di bar

**Files:** Create `src/Gili.App/Bar/RightService.cs`; modify `src/Gili.App/Bar/BarWindow.xaml(.cs)`, `BarViewModel.cs`, `src/Gili.App/Resources/Icons.xaml` (+ `Icon.Wifi`, `Icon.Bluetooth`, `Icon.Volume.Mute/Low/Mid/High`, `Icon.Sun`, `Icon.ChevronDown`, `Icon.Power`, `Icon.Moon`, `Icon.Restart`, `Icon.Dots`, `Icon.Weather.*`), `src/Gili.App/App.xaml.cs`.

- [ ] **Step 1:** `RightService` mengumpulkan provider Task 2, menjalankan jadwal cuaca (30 menit, retry `ProviderRetry`), dan menaikkan `Changed` di dispatcher.
- [ ] **Step 2:** XAML: cuaca (ikon + dua baris; abu-abu bila basi), Wi‑Fi (ikon + `STRONG` / `Home_5G`, `WI‑FI` / `Off`), Bluetooth (ikon + titik biru 6 px di kanan atas, garis 1,5 px warna bar), Volume, Brightness (tersembunyi bila tidak didukung), Tray (chevron 14 px), Power; tooltip sesuai Global Constraints; klik sesuai Keputusan 2.
- [ ] **Step 3:** Overflow: lebar tersedia = `BarSpacing.RightLimit`; modul tersembunyi pindah ke tombol "…" (ikon titik tiga) yang membuka daftar seperti `+N`.
- [ ] **Step 4:** build, test, jalankan dan bandingkan bar kanan dengan mockup.
- [ ] **Step 5: Commit** `feat(bar): weather, Wi-Fi, Bluetooth, volume, brightness, tray and power on the right (F-RIGHT-02..05, F-RIGHT-07, F-RIGHT-09)`.

### Task 4: Pulau Power

**Files:** Modify `src/Gili.Core/Island/IslandModels.cs` (+ `IslandPage.Controls` di akhir, tanpa tab), `src/Gili.Core/Island/IslandLayout.cs` (+ ukuran kontrol), `src/Gili.App/Island/IslandWindow.xaml(.cs)`; create `src/Gili.App/Island/PowerPage.xaml(.cs)`; test `IslandLayoutTests`.

- [ ] **Step 1: Tes** `Control_island_is_360_wide_radius_28_and_as_tall_as_its_content` (`IslandLayout.For(Expanded…)` untuk halaman Controls: lebar min(360, layar − 24), tinggi isi + 32, radius 28).
- [ ] **Step 2:** gagal → implementasi → lulus.
- [ ] **Step 3:** Pulau kontrol di `IslandWindow`: menu dan Gili disembunyikan, halaman di Y 0, glow biru; `PowerPage` sesuai mockup (`.cvh` judul dengan chip ikon 28 px radius 9 dan baris baterai kanan 13 px abu; tiga tombol 40 px radius 12 putih 6% hover 10% dengan ikon 16 px abu; garis atas putih 8% lalu dua tombol 32 px radius 10). Tombol menjalankan `SystemActions` (tidak diklik saat pengujian).
- [ ] **Step 4:** klik ikon Power membuka/menutup; Esc, klik di luar, dan 3 detik menutup.
- [ ] **Step 5: Commit** `feat(island): Power control island (F-RIGHT-07, F-RIGHT-10)`.

### Task 5: Dokumen, perbandingan, verifikasi

- [ ] **Step 1:** PRD 6.4: catat Keputusan 1–3 sebagai perilaku sampai Fase 3.
- [ ] **Step 2:** potret bar mockup dan app 1920 × 40, bandingkan bagian kanan; potret pulau Power.
- [ ] **Step 3:** Release build, test, format; CPU diam < 1%.
- [ ] **Step 4: Commit** `docs: right side basics until Phase 3`.
