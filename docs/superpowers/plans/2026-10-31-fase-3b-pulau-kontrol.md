# Fase 3b: Pulau Kontrol Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ikon Wi‑Fi, Bluetooth, Volume, Brightness, dan Power di bar masing-masing membuka pulau kontrolnya sendiri (F-RIGHT-10), dan semua kontrolnya mengubah sistem langsung tanpa admin.

**Architecture:** Core mendapat `ControlKind` di koordinator dan layout, plus aturan daftar yang dites (`WifiNetworks`, `BluetoothList`, `ControlSliders`, `AirplaneRules`). Platform memperluas monitor yang ada (`AudioMonitor`, `WifiMonitor`, `BluetoothMonitor`) dan menambah `RadioControl` serta `BrightnessControl`. App menaruh satu `UserControl` per kontrol di `ControlLayer` `IslandWindow`, dipilih oleh `IslandView.Control`.

**Tech Stack:** .NET 10 WPF, WinRT (`Windows.Devices.Radios`, `Windows.Devices.Bluetooth`, `Windows.Devices.Enumeration`), Win32 (Native WLAN API, Core Audio, `IPolicyConfig`, `IKsControl`), WMI, xUnit.

**Spec:** [desain Fase 3b](../specs/2026-10-31-fase-3b-pulau-kontrol-design.md), PRD 6.4 "Pulau kontrol", 8.1 "Pulau kontrol", 9.2. Mockup: `.pg-ctl`, `.cv*`, `.qn`, `.sw`, `openCtl`, `renderWifi/Bt/Vol/Bri`, `layout()` cabang `PAGE.controls`.

## Global Constraints

- Teks persis: `Volume`, `Muted`, `Mute`, `Unmute`, `Brightness`, `Wi‑Fi` (dengan U+2011), `Airplane mode`, `Connected`, `Saved`, `Secured`, `Open`, `Connecting…`, `More Wi‑Fi settings`, `Wi‑Fi is off`, `Airplane mode is on`, `Bluetooth`, `Paired`, `Connected · 80%`, `Add device`, `Bluetooth is off`, `Couldn't connect`, `Not available`, `Power`.
- Lebar: Wi‑Fi dan Bluetooth 400, lainnya 360 (dibatasi lebar layar − 24); padding 16; radius 28; tinggi = isi + 32, maks. tinggi layar dengan jarak atas yang sama di bawah.
- Ganti kontrol: isi lama memudar 90 ms, isi baru muncul 180 ms; ukuran lewat pegas yang ada (`SpringParameters.Resize` saat sudah terbuka).
- Daftar: maks. tinggi 232 px lalu scroll; baris min. 36 px.
- Scroll di atas slider: ±2 poin, clamp 0–100. Panah kiri/kanan: ±1 (perilaku slider WPF).
- Tidak ada pembacaan daftar saat pulau kontrol tertutup. Tidak ada polling baru.
- Warna: biru `#0A84FF`, saklar mati `#48484A`, teks `#F5F5F7`, text2 `#8E8E93`.

## Keputusan yang dibuat di rencana ini

1. **`IslandPage.Controls` tetap satu halaman**; `IslandView` mendapat `ControlKind Control` (default `Power`). `Toggle(IslandPage.Controls)` yang lama tetap ada sebagai `Toggle(ControlKind.Power)`.
2. **Klik ikon kontrol lain saat pulau kontrol terbuka** mengganti isi tanpa menutup; aturan "klik yang menutup lewat Dismiss" berlaku per jenis kontrol.
3. **Platform tidak diberi interface Core baru.** Kontrol pulau hanya dibuka pengguna dan selalu memakai data nyata; demo cukup membuka dan mengganti pulau kontrol (aksi `DemoControl`). Ini menyimpang dari pola "setiap fitur punya provider palsu" di CLAUDE.md karena tidak ada keadaan pulau baru yang muncul sendiri.
4. **Brightness** diatur dari satu tugas latar yang hanya mengambil nilai terakhir.
5. **Sambung Bluetooth audio** lewat `KSPROPERTY_ONESHOT_RECONNECT`/`DISCONNECT` di filter KS endpoint audio Bluetooth (dicocokkan lewat `ContainerId`); perangkat lain membuka `ms-settings:bluetooth`.

## Review Focus

1. **Daftar Wi‑Fi dengan SSID ganda dan tersembunyi** (2,4 GHz dan 5 GHz bernama sama, satu tersambung; SSID kosong): satu baris per nama, yang tersambung di atas, SSID kosong dibuang. Dites di Task 3 (`Same_name_merges_and_keeps_connected`, `Hidden_networks_are_dropped`).
2. **Klik ikon Volume saat pulau Power terbuka, lalu klik Volume lagi**: pertama mengganti isi, kedua menutup. Klik di luar yang tepat mengenai ikon kontrol yang sedang terbuka tidak membukanya lagi. Dites di Task 2 (`Other_control_swaps_without_closing`, `Same_control_closes`, `Dismissing_click_on_the_open_control_stays_closed`).
3. **Airplane mode sistem nyala saat Gili mulai** (radio mati karena Windows): saklar tampil nyala, daftar `Airplane mode is on`; mematikannya saat Windows menolak membuka Settings. Dites di Task 3 (`Airplane_follows_system_flag`, `Both_radios_off_reads_as_airplane`).
4. **Perangkat Bluetooth tanpa nama dan baterai di luar 0–100**: nama `Bluetooth device`, baterai tidak ditampilkan. Dites di Task 3 (`Unnamed_and_bad_battery`).
5. **Scroll di slider pada 99 dan 1**: berhenti di 100 dan 0. Dites di Task 3 (`Wheel_clamps`).

---

### Task 1: PRD, mockup, desain

**Files:** Modify `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md`, `docs/mockup/top-taskbar-mockup.html`; Create (sudah) `docs/superpowers/specs/2026-10-31-fase-3b-pulau-kontrol-design.md`.

- [ ] **Step 1:** PRD 6.4 tabel pulau kontrol: Wi‑Fi menyebut status `Open` dan perilaku Airplane (semua radio; `DeniedBySystem` membuka `ms-settings:network-airplanemode`); Bluetooth: perangkat audio disambung/diputus langsung, lainnya membuka `ms-settings:bluetooth`, gagal `Couldn't connect` 3 detik; `Not available` saat provider gagal. PRD 6.4 "Sampai Fase 3" ditandai selesai untuk pulau kontrol (klik membuka pulau, bukan `Win+A`). 9.2 baris "Pulau kontrol": KS oneshot untuk sambung audio Bluetooth.
- [ ] **Step 2:** Mockup: `WIFI` mendapat satu jaringan terbuka (`Warung Free WiFi`, status `Open`) dan klik jaringan tanpa profil tetap memberi hint daftar jaringan Windows (yang terbuka juga, karena Gili tidak menyimpan profil baru).
- [ ] **Step 3:** `node --check` skrip mockup lolos.
- [ ] **Step 4: Commit** `docs: phase 3b control islands design, PRD and mockup`.

### Task 2: Jenis kontrol di koordinator dan layout (Core)

**Files:** Modify `src/Gili.Core/Island/IslandModels.cs`, `IslandCoordinator.cs`, `IslandLayout.cs`, `src/Gili.Core/Demo/DemoScript.cs`; Test `tests/Gili.Core.Tests/Island/IslandCoordinatorTests.cs`, `IslandLayoutTests.cs`, `Demo/DemoTests.cs`.

**Interfaces (Produces):**
- `public enum ControlKind { Power, Volume, Brightness, Wifi, Bluetooth }`.
- `IslandView(..., IslandPage Page, ControlKind Control = ControlKind.Power)`.
- `IslandCoordinator.Toggle(ControlKind kind)`; `Toggle(IslandPage.Controls)` = `Toggle(ControlKind.Power)`.
- `IslandLayout.ControlWidthFor(ControlKind kind) → double` (400/360); `IslandLayout.For(..., IslandPage page = Media, ControlKind control = Power)`.
- `public sealed record DemoControl(ControlKind Kind) : DemoAction`; skenario: `Step(31, DemoControl(Power))`, `Step(32.5, DemoControl(Volume))` menggantikan `DemoOpen(Controls)`.

- [ ] **Step 1: Tes gagal**: `Other_control_swaps_without_closing`, `Same_control_closes`, `Dismissing_click_on_the_open_control_stays_closed`, `Dismissing_click_on_another_control_opens_it`, `Control_width_follows_kind` (Wi‑Fi 400, Volume 360, layar 380 → 356), `Demo_swaps_power_for_volume`.
- [ ] **Step 2:** `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandCoordinator|FullyQualifiedName~IslandLayout|FullyQualifiedName~Demo"` gagal kompilasi.
- [ ] **Step 3:** Implementasi; pemanggil App (`App.xaml.cs` PowerRequested, `DemoService`) diperbaiki secukupnya.
- [ ] **Step 4:** Tes yang sama lulus, lalu seluruh suite.
- [ ] **Step 5: Commit** `feat(island): a control island per bar icon, swapped without closing (F-RIGHT-10)`.

### Task 3: Aturan daftar dan slider (Core)

**Files:** Create `src/Gili.Core/Modules/ControlRules.cs`; Test `tests/Gili.Core.Tests/Modules/ControlRulesTests.cs`.

**Interfaces (Produces):**
- `public sealed record WifiNetwork(string Ssid, int Quality, bool Secured, bool HasProfile, bool Connected, string? Profile)`.
- `public enum WifiStatus { Connected, Saved, Secured, Open }`; `public sealed record WifiRow(string Ssid, WifiStatus Status, string? Profile)` dengan `string StatusText`.
- `WifiNetworks.Order(IEnumerable<WifiNetwork>) → IReadOnlyList<WifiRow>`: buang SSID kosong/spasi; gabung per SSID (Connected menang, lalu HasProfile, lalu Quality tertinggi; profil diambil dari entri mana pun yang punya); tersambung dulu, lalu Quality turun, lalu nama.
- `public sealed record PairedDevice(string Id, string Name, BluetoothKind Kind, bool Connected, int? Battery, bool CanConnect)`.
- `BluetoothList.Order(IEnumerable<PairedDevice>) → IReadOnlyList<PairedDevice>`: tersambung dulu lalu nama (ordinal ignore case); nama kosong → `Bluetooth device`.
- `BluetoothList.Status(PairedDevice d) → string`: `Connected · 80%`, `Connected`, `Paired`; baterai di luar 0–100 diabaikan.
- `ControlSliders.Wheel(int value, int delta) → int`: `delta > 0` → +2, `< 0` → −2, clamp 0–100.
- `AirplaneRules.IsOn(bool systemFlag, bool? wifiOn, bool? bluetoothOn) → bool`: flag sistem, atau kedua radio ada dan mati.

- [ ] **Step 1: Tes gagal**: `Same_name_merges_and_keeps_connected`, `Hidden_networks_are_dropped`, `Statuses` (Connected, Saved, Secured, Open), `Order_is_connected_then_signal`, `Unnamed_and_bad_battery`, `Bluetooth_order_connected_first`, `Wheel_clamps`, `Airplane_follows_system_flag`, `Both_radios_off_reads_as_airplane`, `One_radio_missing_is_not_airplane`.
- [ ] **Step 2:** Filter `~ControlRules` gagal kompilasi.
- [ ] **Step 3:** Implementasi.
- [ ] **Step 4:** Lulus; seluruh suite lulus.
- [ ] **Step 5: Commit** `feat(core): Wi‑Fi, Bluetooth and slider rules for the control islands (F-RIGHT-10)`.

### Task 4: Platform

**Files:** Modify `src/Gili.Platform/Modules/AudioMonitor.cs`, `WifiMonitor.cs`, `BluetoothMonitor.cs`, `packaging/AppxManifest.xml`; Create `src/Gili.Platform/Modules/RadioControl.cs`, `BrightnessControl.cs`, `AudioOutputs.cs` (IPolicyConfig + daftar), `BluetoothAudio.cs` (KS).

**Interfaces (Produces):**
- `AudioMonitor.SetVolume(int percent)`, `SetMute(bool)`; `AudioOutputs.List() → IReadOnlyList<(string Id, string Name, bool IsDefault)>`; `AudioOutputs.SetDefault(string id)`.
- `BrightnessControl.Set(int percent)` (latest-wins di thread pool), `BrightnessControl.Read() → int?` (pindahan `BrightnessReader`).
- `RadioControl`: `StartAsync()`, `bool? WifiOn`, `bool? BluetoothOn`, `bool SystemAirplane`, `event Action Changed`, `Task<bool> SetWifiAsync(bool)`, `Task<bool> SetBluetoothAsync(bool)`, `Task<bool> SetAirplaneAsync(bool on)` (false = Windows menolak).
- `WifiMonitor.Networks() → IReadOnlyList<WifiNetwork>`, `Scan()`, `Connect(string profile)`, event `ListChanged` (scan selesai), `ConnectFinished(bool success)`.
- `BluetoothMonitor.PairedAsync() → Task<IReadOnlyList<PairedDevice>>`, `ConnectAsync(PairedDevice, bool connect) → Task<bool>`.
- Manifest: `<DeviceCapability Name="bluetooth" />`, `<DeviceCapability Name="radios" />`.

- [ ] **Step 1:** Implementasi, mengikuti struktur dari spike (`WLAN_AVAILABLE_NETWORK` 628 byte: SSID di 512, `bSecurityEnabled` di 608, `dwFlags` di 620 dengan 1 = tersambung, 2 = punya profil; kualitas di 604).
- [ ] **Step 2:** `dotnet build Gili.slnx` 0 warning; program uji kecil memanggil `Networks()`, `AudioOutputs.List()`, `SetDefault` ke perangkat default yang sama, `BrightnessControl.Set` ke nilai sekarang, `PairedAsync()`, dan semuanya berhasil.
- [ ] **Step 3: Commit** `feat(platform): set volume, output, brightness, radios, Wi‑Fi and Bluetooth from the control islands (F-RIGHT-10)`.

### Task 5: Pulau Volume dan Brightness, penggantian isi (App)

**Files:** Create `src/Gili.App/Island/Controls/ControlStyles.xaml` (slider, saklar, kotak ikon judul, baris daftar), `VolumeControl.xaml(.cs)`, `BrightnessControl.xaml(.cs)`; Modify `IslandWindow.xaml(.cs)`, `IslandController.cs`, `Bar/BarWindow.xaml(.cs)`, `App.xaml.cs`, `Bar/RightService.cs`.

- [ ] **Step 1:** `ControlLayer` berisi kontrol sesuai `View.Control`; ganti kontrol memudarkan yang lama 90 ms lalu memunculkan yang baru 180 ms (`ExpandedLayer` pola yang ada); `ControlHeight()` mengukur kontrol yang tampil; `IslandLayout.For` diberi `Control`.
- [ ] **Step 2:** Bar: Wi‑Fi, Bluetooth, Volume, Brightness memanggil `Toggle(ControlKind.*)` (menggantikan `OnQuickSettings`), Power tetap.
- [ ] **Step 3:** Volume: judul (tombol speaker mute/unmute dengan glyph `Icon.Volume.*` dari `VolumeText.Level`), persen/`Muted`, slider, baris perangkat output dan daftarnya (ceklis di default). Brightness: judul, persen, slider. Wheel memakai `ControlSliders.Wheel`. Menahan slider menunda `_awayTimer`.
- [ ] **Step 4:** Build, jalankan (`dotnet run`), cek manual: klik Volume membuka 360 px, slider mengubah volume sistem (kembalikan nilai awal), tombol volume keyboard menggerakkan slider, klik Brightness mengganti isi dengan pudar, klik lagi menutup.
- [ ] **Step 5: Commit** `feat(island): volume and brightness control islands (F-RIGHT-10)`.

### Task 6: Pulau Wi‑Fi dan Bluetooth (App)

**Files:** Create `src/Gili.App/Island/Controls/WifiControl.xaml(.cs)`, `BluetoothControl.xaml(.cs)`; Modify `IslandWindow.xaml.cs`, `App.xaml.cs`.

- [ ] **Step 1:** Wi‑Fi: judul + saklar, baris Airplane + saklar, daftar `WifiNetworks.Order` (ikon Wi‑Fi, nama, status; `Connecting…` saat menyambung), `More Wi‑Fi settings`; mati → `Wi‑Fi is off` / `Airplane mode is on`. Buka → baca daftar lalu `Scan()`; `ListChanged` membaca ulang hanya selama terbuka.
- [ ] **Step 2:** Bluetooth: judul + saklar, daftar `BluetoothList.Order` (ikon jenis, nama, status), `Add device`; klik audio → `ConnectAsync`, status `Connecting…`, gagal `Couldn't connect` 3 detik; lainnya membuka Settings. Mati → `Bluetooth is off`.
- [ ] **Step 3:** Build, jalankan, cek manual: daftar Wi‑Fi tampil dengan jaringan tersambung di atas; saklar Bluetooth mati/nyala mengubah radio (dicek lewat ikon bar); daftar Bluetooth berisi 5 perangkat ter-pair. Wi‑Fi tidak dimatikan saat uji.
- [ ] **Step 4: Commit** `feat(island): Wi‑Fi and Bluetooth control islands (F-RIGHT-10)`.

### Task 7: Verifikasi

- [ ] **Step 1:** `dotnet build Gili.slnx -c Release` 0 warning, `dotnet test tests/Gili.Core.Tests` lulus, `dotnet format Gili.slnx --verify-no-changes` bersih.
- [ ] **Step 2:** Versi GitHub (`scripts/publish.ps1`) dan MSIX (`scripts/package.ps1 -Install`): kelima pulau terbuka; saklar Bluetooth bekerja di MSIX (capability `radios`); daftar Wi‑Fi tampil di MSIX.
- [ ] **Step 3:** Anggaran: CPU diam sebelum/sesudah (rata-rata 60 detik), RAM setelah membuka kelima pulau 5 kali dan menunggu 30 detik.
- [ ] **Step 4:** Mode demo: Power lalu Volume di 31–34 detik; total demo tetap < 60 detik.
- [ ] **Step 5: Commit** perbaikan yang muncul.
