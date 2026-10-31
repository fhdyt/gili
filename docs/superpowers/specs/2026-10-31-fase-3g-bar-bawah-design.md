# Fase 3g: bar di tepi bawah (desain singkat)

Tanggal: 2026-10-31. Fitur: F-BAR-10. Pengguna meminta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya.

## Maksud

Settings → General → `Bar position` (`Top` / `Bottom`) memindahkan Gili ke tepi bawah setelah taskbar Windows disembunyikan (F-BAR-11, Fase 3f). Semua yang mengambang dicerminkan: pulau yang terbuka, kartu, drop zone, dan pulau kontrol menjaga tepi **bawah** pulau compact dan membesar ke atas. Isi pulau tidak dibalik (PRD 6.1, mockup `body.bottom` dan `layout()`).

## Perbedaan PRD dan mockup

PRD 6.1 menulis "bar keluar dari tepi lama dan masuk dari tepi baru (pegas compact ↔ wide)". Mockup memindahkan bar langsung, dan hanya pulau yang bergerak ke tempat barunya dengan pegas. Keputusan pengguna 2026-10-27: gerak app harus persis mockup, jadi app mengikuti mockup dan PRD disamakan **(keputusan Claude, dilaporkan ke pengguna)**.

## Aturan (Core)

- `AppSettings.BarPosition` (`Top` default, `Bottom`) dan `MovedToTop`.
- `Normalize`: `Bottom` tanpa `HideTaskbar` menjadi `Top` dengan `MovedToTop = true`. Ini satu-satunya jalan taskbar kembali saat Gili jalan (saklar dimatikan), jadi bar otomatis pindah ke atas.
- Settings: baris `Bar position` nonaktif selama taskbar tampil, dengan keterangan `Hide the Windows taskbar first`, atau `Moved to the top because the Windows taskbar is back` setelah pindah otomatis (`MovedToTop`). Memilih posisi atau menyalakan `Hide Windows taskbar` menghapus `MovedToTop`.
- `IslandLayout.FromEdge(y, height, envelope, bottom)`: posisi tegak yang dihitung dari tepi atas jendela dicerminkan ke tepi bawah saat bar di bawah. Pulau, gelembung, lingkaran Gili, dan titik kamera/mic memakai fungsi ini.

## App dan Platform

- `AppBarService.SetEdge(bottom)`: `ABE_BOTTOM`, strip di `monitor.Bottom − tinggi`, lalu daftar ulang posisi.
- `BarWindow`: garis tipis pindah dari tepi bawah ke tepi atas bar; chevron Tray diputar 180° (mockup `body.bottom #mTray .ic`). Menu klik kanan dan daftar `+N` adalah popup WPF yang membuka ke atas sendiri bila tidak ada ruang di bawah.
- `IslandWindow`: envelope menempel ke tepi bawah layar (`Top = tinggi layar − Height`), dan saat envelope membesar, posisinya ikut naik.

## Anggaran (PRD 8.1)

Tanpa kerja tambahan per frame selain satu perbandingan; tidak terukur.

## Pengujian

Unit test Core untuk `Normalize`, baris Settings, dan `FromEdge`. Di laptop, tanpa input sintetis: ubah `settings.json` saat Gili tertutup (`hideTaskbar` dan `barPosition: bottom`), jalankan, lalu cek area kerja (`0,0,1920,1040` pada bar 40 px) dan posisi jendela bar dan pulau. **Butuh pengguna**: membuka pulau, kartu, dan pulau kontrol di posisi bawah untuk melihat geraknya.
