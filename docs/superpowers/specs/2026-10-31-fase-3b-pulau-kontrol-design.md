# Fase 3b: Pulau kontrol (desain)

Tanggal: 2026-10-31. Fitur: F-RIGHT-10 (pulau kontrol Volume, Brightness, Wi‑Fi, Bluetooth, dan Power), bagian bar dari F-RIGHT-02 (klik Wi‑Fi membuka pulaunya).

Pengguna meminta saya melanjutkan tanpa berhenti dan memilih sendiri setiap pilihan ("jika ada pilihan pilih yang terbaik"), jadi tidak ada sesi tanya-jawab. Semua bagian **(keputusan Claude)** adalah pilihan saya. Isi dan tampilan diambil dari PRD 6.4 "Pulau kontrol" dan mockup (`.pg-ctl`, `openCtl`, `renderWifi/Bt/Vol/Bri`).

## Maksud

Sejak Fase 1, klik Wi‑Fi, Bluetooth, Volume, dan Brightness di bar membuka Quick Settings Windows (`Win+A`), dan hanya Power yang membuka pulau kontrolnya sendiri. Fase ini mengganti keempatnya dengan pulau kontrol Gili: pulau membesar hanya berisi satu kontrol, ukurannya mengikuti isi, dan semua pengaturan berlaku langsung ke sistem. Dengan begitu pengguna yang menyembunyikan taskbar Windows tidak perlu Quick Settings untuk hal sehari-hari.

## Tujuan

- Klik ikon Wi‑Fi, Bluetooth, Volume, Brightness, atau Power membuka pulau kontrolnya; klik ikon yang sama menutup; klik ikon kontrol lain mengganti isi dengan pegas, isi lama memudar 90 ms dan isi baru muncul 180 ms.
- Volume: slider mengubah volume sistem langsung, ikon speaker mute/unmute, daftar perangkat output yang bisa dijadikan default.
- Brightness: slider mengubah brightness layar bawaan langsung.
- Wi‑Fi: saklar radio, saklar Airplane mode, daftar jaringan (maks. 6 terlihat, sisanya di-scroll), klik jaringan tersimpan menyambung, jaringan baru membuka daftar jaringan Windows, **More Wi‑Fi settings**.
- Bluetooth: saklar radio, daftar perangkat ter-pair dengan jenis dan status, klik menyambung/memutus, **Add device**.
- Semua kontrol bisa dipakai dengan keyboard; scroll di atas slider mengubah 2 poin.
- Pembacaan daftar hanya berjalan saat pulaunya terbuka (PRD 8.1 "Pulau kontrol": 0 saat tidak tampil).
- Mode demo membuka pulau Power lalu menggantinya dengan pulau Volume.

## Bukan tujuan

- Item `Update and shut down` / `Update and restart` dan titik oranye di Power (F-RIGHT-08, perlu verifikasi deteksi update dulu; sub-fase sendiri).
- Panel tray (F-RIGHT-09), modul GPU/disk, saklar dan urutan Right side, overflow (F-RIGHT-03/04).
- Memasukkan password Wi‑Fi di Gili: jaringan baru selalu lewat daftar jaringan Windows.
- Brightness monitor eksternal (DDC/CI): modul brightness memang disembunyikan bila layar tidak mendukung (PRD 6.4).

## Anti-tujuan

- Hak admin, driver, atau API yang memicu UAC.
- Polling saat pulau kontrol tertutup. Ikon bar tetap dari event yang sudah ada.
- Mematikan Wi‑Fi saat pengguna tidak memintanya (misalnya karena salah baca status airplane).

## Verifikasi di laptop pengguna (sebelum desain)

Program uji kecil, tanpa mengubah keadaan sistem kecuali Bluetooth sebentar:

| API | Hasil |
|---|---|
| `Radio.RequestAccessAsync` (tanpa paket) | `Allowed`; radio Wi‑Fi dan Bluetooth terbaca |
| `Radio.SetStateAsync` Bluetooth Off lalu On | `Allowed`, 123 ms dan 214 ms |
| `WmiMonitorBrightnessMethods.WmiSetBrightness` sebagai pengguna biasa | Berhasil, 27 ms |
| `WlanGetAvailableNetworkList` | 2 entri, nama jaringan terbaca |
| Perangkat Bluetooth ter-pair (`GetDeviceSelectorFromPairingState(true)`) | 5 perangkat (2 ponsel, 3 earbuds), semuanya tidak tersambung |
| Registry `HKLM\SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState` | Terbaca (`0`) |

Radio Wi‑Fi tidak dimatikan saat uji karena sesi pengembangan memakai jaringan itu; Wi‑Fi memakai API yang sama dengan Bluetooth.

## Pendekatan

### Airplane mode

| Cara | Untung | Rugi |
|---|---|---|
| COM `IRadioManager` (tidak terdokumentasi) | Mengubah flag airplane sistem persis seperti Quick Settings | API privat; tidak bisa diuji tanpa memutus jaringan sesi; risiko Store |
| **Mematikan semua radio lewat `Windows.Devices.Radios`** | API resmi, sudah teruji untuk Bluetooth | Flag airplane Windows tidak ikut berubah |
| Membuka `ms-settings:network-airplanemode` | Paling aman | Bukan saklar |

**Dipilih: semua radio lewat API resmi (keputusan Claude),** sesuai bunyi PRD ("nyala mematikan Wi‑Fi dan Bluetooth"). Saklar tampil nyala bila flag airplane sistem nyala, atau bila radio Wi‑Fi dan Bluetooth sama-sama mati karena saklar Gili. Mematikannya menyalakan kedua radio; kalau Windows menolak karena flag airplane sistem masih nyala (`DeniedBySystem`), Gili membuka `ms-settings:network-airplanemode`. Saat airplane nyala, daftar diganti `Airplane mode is on` (mockup).

### Sambung/putus Bluetooth

Windows tidak punya API publik untuk menyambung perangkat Bluetooth klasik yang sudah ter-pair. Pilihan yang tersisa:

| Cara | Untung | Rugi |
|---|---|---|
| **Properti KS `KSPROPERTY_ONESHOT_RECONNECT` / `DISCONNECT` di filter audio Bluetooth** | Terdokumentasi di WDK; cara yang dipakai app tray Bluetooth populer; tanpa admin | Hanya untuk perangkat audio |
| `BluetoothSetServiceState` | Semua jenis | Mengubah driver layanan; bisa butuh admin |
| Membuka `ms-settings:bluetooth` | Selalu bekerja | Bukan sambung langsung |

**Dipilih: KS untuk perangkat audio, Settings untuk yang lain (keputusan Claude).** Perangkat audio (earbuds, headset, speaker) disambung/diputus langsung. Mouse, keyboard, dan ponsel membuka `ms-settings:bluetooth`, karena perangkat input menyambung sendiri dan ponsel butuh layanan lain. Gagal menyambung (perangkat mati atau di luar jangkauan) menampilkan `Couldn't connect` di baris itu selama 3 detik **(keputusan Claude; teks baru, ditambahkan ke PRD)**. Bagian ini belum bisa diuji dengan perangkat nyata di laptop pengguna (tidak ada earbuds yang menyala), jadi dilaporkan sebagai belum teruji.

### Perangkat output

`IPolicyConfig::SetDefaultEndpoint` (tidak terdokumentasi, tetapi dipakai app volume di Microsoft Store seperti EarTrumpet) untuk ketiga role (Console, Multimedia, Communications), sesuai PRD 9.2. Daftar dari `IMMDeviceEnumerator.EnumAudioEndpoints(eRender, ACTIVE)` dengan nama dari `PKEY_Device_FriendlyName`. **Dipilih seperti PRD.**

### Brightness

`WmiSetBrightness` makan ±27 ms, jadi menggeser slider tidak boleh memanggilnya di thread UI per piksel. Satu tugas latar mengambil nilai terakhir saja (yang lama dibuang), jadi layar mengikuti slider tanpa antrean **(keputusan Claude)**. Event `WmiMonitorBrightnessEvent` tidak dipakai; nilai dibaca saat pulau dibuka dan saat tooltip dibuka (seperti sekarang).

### Daftar Wi‑Fi

Saat pulau Wi‑Fi dibuka: baca daftar yang ada (`WlanGetAvailableNetworkList`), lalu minta `WlanScan`; notifikasi `scan_complete` / `scan_list_refresh` membaca ulang daftar. Tidak ada scan berkala. Satu baris per SSID (sinyal terkuat), SSID kosong (jaringan tersembunyi) dibuang, jaringan tersambung paling atas, lalu sinyal terkuat. Status: `Connected`, `Saved` (punya profil), `Open` untuk jaringan tanpa keamanan tanpa profil **(keputusan Claude: mockup hanya punya Saved/Secured; jaringan terbuka diberi `Open` supaya tidak terlihat butuh password)**, selain itu `Secured`. Klik jaringan tersimpan: `WlanConnect` dengan profilnya, baris menampilkan `Connecting…` sampai notifikasi `connection_complete`; gagal kembali ke statusnya. Klik jaringan lain membuka `ms-availablenetworks:`.

### Struktur kode

| Lapisan | Isi |
|---|---|
| Core `Island` | `ControlKind { Power, Volume, Brightness, Wifi, Bluetooth }`; `IslandCoordinator.Toggle(ControlKind)` dan `IslandView.Control`; `IslandLayout.ControlWidthFor(kind)` (400 untuk Wi‑Fi/Bluetooth, 360 lainnya) |
| Core `Devices`/`Modules` | Aturan yang bisa dites: `WifiNetworks.Order` (gabung, urutkan, status), `BluetoothList.Order` dan teks status, `ControlSliders.Step` (scroll 2 poin, clamp 0–100), `AirplaneRules.IsOn` |
| Platform | `AudioMonitor` + `SetVolume`, `SetMute`, `Outputs`, `SetDefault`; `BrightnessControl`; `WifiMonitor` + `Networks`, `Scan`, `Connect`; `RadioControl` (Wi‑Fi, Bluetooth, airplane); `BluetoothMonitor` + `PairedAsync`, `ConnectAsync`/`DisconnectAsync` |
| App | Satu `UserControl` per kontrol (`VolumeControl`, `BrightnessControl`, `WifiControl`, `BluetoothControl`, `PowerPage` yang sudah ada) di `ControlLayer`; satu gaya slider dan saklar bersama di `Resources` |

Pulau kontrol bukan halaman baru: `IslandPage.Controls` tetap, dan `IslandView.Control` memilih isinya. Toggle ikon yang sama menutup, ikon lain mengganti isi tanpa menutup.

## Detail tampilan (dari mockup)

- Padding 16, `--lead` 40 px (ikon 28 + jarak 12): slider dan baris perangkat output menjorok sejajar judul.
- Baris judul: kotak ikon 28 × 28 radius 9, latar putih 8%; biru `#0A84FF` dan ikon putih saat radio nyala. Judul 14 px semibold; nilai di kanan 13 px abu-abu, digit tabular.
- Slider: track 4 px, bagian terisi putih, sisanya putih 16%; thumb putih 16 px dengan bayangan, membesar 1,15× saat ditekan; fokus keyboard memberi cincin biru 3 px.
- Saklar: 38 × 22, latar `#48484A`, nyala `#0A84FF`, kenop 18 px bergeser 16 px.
- Daftar: latar putih 5%, radius 12, padding 4, baris min. 36 px radius 8 (hover putih 8%), maks. tinggi 232 px lalu scroll. Nama 13 px medium (semibold untuk yang aktif), status 12 px abu-abu; ceklis perangkat output berwarna aksen.
- Baris perangkat output: tinggi 28, teks 12 px abu-abu, chevron berputar 90° saat daftar terbuka.
- `Wi‑Fi is off`, `Bluetooth is off`, `Airplane mode is on`: padding 12/10, 13 px abu-abu.

## Perilaku lain

- Menggeser slider menunda penutupan otomatis 3 detik (sudah ada untuk pulau terbuka; diperluas ke slider).
- Volume dan Bluetooth di pulau mengikuti event sistem selama terbuka (≤ 1 detik), jadi tombol volume keyboard menggerakkan slider.
- Tombol `Add device`, `More Wi‑Fi settings`, dan pilihan yang membuka Settings Windows menutup pulau lalu membuka halamannya.
- Kegagalan provider (tanpa adapter, tanpa speaker) menampilkan baris abu-abu `Not available` di tempat daftar **(keputusan Claude)**; ikon bar sendiri sudah tersembunyi di keadaan itu.

## Anggaran (PRD 8.1)

Sudah ada baris "Pulau kontrol": 0 saat tidak tampil. Fase ini menambah: satu `WlanScan` per pembukaan pulau Wi‑Fi, satu enumerasi perangkat ter-pair per pembukaan pulau Bluetooth, satu enumerasi perangkat audio per pembukaan daftar output. Diukur: CPU diam sebelum dan sesudah, RAM setelah membuka semua pulau lima kali.

## Paket MSIX

`<DeviceCapability Name="radios" />` dan `<DeviceCapability Name="bluetooth" />` ditambahkan ke `AppxManifest.xml` supaya `Windows.Devices.Radios` dan `BluetoothDevice` bekerja di paket. Keduanya capability umum (bukan terbatas) yang diterima Store.

## Yang diserahkan ke pembangun

Nama kelas dan pembagian file di dalam lapisan, cara memudarkan isi lama saat ganti kontrol, dan susunan XAML selama tampilannya sama dengan mockup.

## Temuan saat implementasi

- **Query association endpoint Bluetooth bisa menggantung selamanya.** Di laptop pengguna, `DeviceInformation.FindAllAsync` dengan `DeviceInformationKind.AssociationEndpoint` awalnya cepat, lalu tidak pernah selesai (dicek sampai 15 detik), sementara selector `BluetoothDevice/BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)` tetap menjawab dalam ±90 ms dengan properti yang sama (`IsConnected`, `ContainerId`, kelas perangkat, appearance, baterai). Daftar perangkat memakai selector itu, dan `RightService` memberi batas 5 detik plus log bila tetap macet.
- **Dua kelas `[ComImport]` dengan CLSID yang sama di satu assembly** (enumerator perangkat audio di `AudioMonitor` dan `AudioDevices`) membuat runtime mengembalikan wrapper kelas yang lain, dan pulau Volume crash saat dibuka. `AudioDevices` membuat objek COM lewat CLSID (`Type.GetTypeFromCLSID`).
- **Tinggi pulau setelah daftar terisi**: mengukur kontrol langsung memakai hasil ukur lama, karena daftar yang baru diisi hanya ditandai untuk layout. `IslandWindow` menata ulang kontrol dulu (`UpdateLayout`) sebelum mengukurnya.
- **Saklar dan pembaca layar**: `Toggle` dari UI Automation tidak memicu `Click`, jadi saklar Wi‑Fi, Airplane, dan Bluetooth memakai `Checked`/`Unchecked` dengan penjaga saat tampilan diperbarui dari kode.
- **Paket MSIX dengan capability baru** tidak mau didaftarkan ulang di versi yang sama dalam Developer Mode; versi dinaikkan ke 0.1.1 (menghapus paket lama akan ikut menghapus data di LocalState).
- **Belum teruji dengan perangkat nyata**: menyambung/memutus earbuds (perintah KS diterima Windows, tetapi tidak ada earbuds yang menyala), menyambung ke jaringan Wi‑Fi tersimpan lain, dan Airplane mode (tidak diuji karena memutus jaringan sesi pengembangan). Modul kanan di bar (Wi‑Fi, Bluetooth, Volume, Brightness, Power) tidak punya peer UI Automation, jadi klik sungguhan di ikon bar perlu dicek pengguna.

## Hasil verifikasi

- Release 0 warning, 662 tes lulus, format bersih.
- Kelima pulau tampil sesuai mockup di build pengembangan; daftar Wi‑Fi dan saklar Bluetooth bekerja di salinan GitHub dan MSIX.
- Build Debug: CPU diam 0,43% sebelum dan 0,49% sesudah; RAM 79,9 MB di awal, ±100 MB saat pulau baru saja dibuka, 78,1 MB 30 detik setelah kelima pulau dibuka 5 kali.
