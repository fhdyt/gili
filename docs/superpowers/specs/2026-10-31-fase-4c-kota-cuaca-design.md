# Fase 4c: kota cuaca (desain)

Tanggal: 2026-10-31. Fitur: F-RIGHT-05, sisa F-BAR-08 (kota cuaca). Nilai terakhir abu-abu saat offline sudah ada sejak Fase 1 (`WeatherStale`).

## Pendekatan

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. Cari lewat geocoding Open-Meteo (dipilih)** | Kota mana pun di dunia (Gili dirilis untuk umum); penyedia yang sama dengan cuaca, tanpa API key | Nama yang diketik dikirim ke Open-Meteo saat mencari (dicatat di PRD 8.2) |
| B. Daftar kota tetap di Settings | Tanpa jaringan tambahan | Selalu kurang untuk pengguna di luar daftar |
| C. Lokasi Windows (`Geolocator`) | Otomatis | Butuh izin lokasi lagi dan lebih presisi dari yang perlu dikirim |

## Aturan (keputusan Claude)

- Baris `Weather city` menampilkan kota sebagai tombol; klik → kolom `Search city`; 400 ms setelah ketikan terakhir dan minimal dua huruf, maksimal lima tempat tampil di bawah baris (`Bandung, West Java, Indonesia`). Klik atau Enter (yang pertama) memilih; Esc kembali tanpa menutup Settings. Tanpa hasil: `No places found`; gagal jaringan: `Can't search right now. Check the connection`.
- Yang disimpan: nama kota (untuk tooltip) dan koordinatnya. Cuaca kota baru diambil saat itu juga.
- Mockup disamakan: baris kota memakai tutup tombol seperti hotkey.

## Hasil verifikasi 4c

- Build dan `dotnet format` bersih, 969 unit test lulus (baru: `CitySearchTests` 5).
- Di laptop lewat UIA (tanpa keyboard): buka Settings → Right side, klik Weather city, isi `Bandung` → lima tempat dari Open-Meteo (`Bandung, West Java, Indonesia`, …). Tidak ada yang dipilih; kota pengguna tetap Jakarta.
- Belum teruji: memilih kota lalu modul cuaca berganti (mengubah setelan pengguna).
