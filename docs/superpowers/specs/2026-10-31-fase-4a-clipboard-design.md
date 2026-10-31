# Fase 4a: riwayat dan halaman Clipboard (desain)

Tanggal: 2026-10-31. Fitur: F-LEFT-03, F-ISL-28, F-ISL-07 Clipboard. Sumber: PRD 6.3 "Halaman Clipboard" dan "Cuplikan clipboard", 8.1, 10; mockup `.pg-clip`, `.cbr`, `cbAdd`, `renderClipPage`, `cbPaste`, `cbRemove`, `#cbClear`. Pengguna minta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya. `Ctrl+Alt+V` menyusul di 4b (hotkey).

## Pendekatan

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. `ClipboardHistory` di Core + store di tabel `clipboard_history` yang sudah ada (dipilih)** | Aturan (duplikat naik, batas 50, cari, waktu) dites tanpa WPF; tahan restart | Satu store baru |
| B. Riwayat hanya di memori | Paling sederhana | PRD F-LEFT-03 meminta tahan restart |
| C. Memakai riwayat clipboard Windows (`Windows.ApplicationModel.DataTransfer.Clipboard.GetHistoryItemsAsync`) | Tanpa penyimpanan sendiri | Bergantung pada saklar riwayat Windows pengguna, tanpa aplikasi sumber, dan tidak bisa dihapus per baris dari Gili |

## Aturan

- **Masuk riwayat**: hanya copy yang cuplikannya berjenis teks (bukan file, gambar, atau isi sensitif). Teks kosong/spasi saja diabaikan. Teks disimpan utuh sampai 100.000 karakter **(keputusan Claude: batas atas supaya satu copy raksasa tidak membengkakkan database; pembacaan untuk cuplikan tetap 4.096 karakter)**. Teks yang sama naik ke atas dengan waktu dan aplikasi barunya. Lebih dari 50 → yang paling lama dibuang.
- **Aplikasi sumber**: nama seperti di bar kiri (deskripsi exe, atau nama paket untuk aplikasi Store), dari jendela pemilik clipboard, di-cache per program. Tanpa pemilik: kosong, baris kedua hanya waktu.
- **Waktu**: `now` (< 1 menit), `38 min`, `2 h`, `Yesterday` (< 48 jam), `3 days` (mockup `ago`).
- **Baris**: teks satu baris (spasi/baris baru diringkas), 13,5 px medium; baris kedua 11,5 px abu-abu `Chrome · 38 min`; tooltip 400 karakter pertama; hover/sorot `#2C2C2E`; tombol × 28 px muncul saat hover atau sorot, merah saat di-hover.
- **Menempel**: teks ditulis ke clipboard (Gili mengabaikan perubahan clipboard yang ia buat sendiri: tanpa cuplikan, tanpa baris baru), baris naik ke atas, pulau menutup dan fokus kembali ke jendela sebelumnya (sudah dilakukan `IslandWindow`), lalu `Ctrl+V` dikirim dengan `SendInput` setelah jendela itu benar-benar di depan (dicek tiap 15 ms, maks. 300 ms). Kalau tidak ada jendela tujuan (desktop, jendela sudah tutup), jendela itu berjalan sebagai admin, atau fokus tidak kembali: wide `Copied · press Ctrl+V to paste` 2 detik.
- **Keyboard**: ↑/↓ memindah sorotan (juga dari kolom cari), Enter menempel baris tersorot, Delete menghapus baris tersorot saat fokus tidak di kolom cari. Mengetik di kolom cari menyorot baris pertama hasil.
- **Clear all**: dua klik; klik pertama `Clear all?` merah 3 detik (sama dengan Delete todo). Tombol hanya tampil saat riwayat berisi.
- **Kosong**: `Nothing copied yet` + `Text you copy shows up here`; cari tanpa hasil: `No matches`.
- **Tinggi**: pulau mengikuti daftar; setelah setinggi layar hanya daftar yang di-scroll.
- **Privasi**: riwayat ada di `gili.db` (PRD 10 sudah menyebut tabel ini); isi sensitif tidak pernah dibaca. Riwayat tidak masuk log.

## Anggaran (PRD 8.1)

Satu INSERT/UPDATE per copy teks, daftar dibangun ulang hanya saat halaman terbuka, satu tulisan clipboard dan satu `SendInput` per tempel.

## Pengujian

Unit test: tambah/duplikat/batas 50/abaikan kosong/cari/hapus/clear/pakai (naik ke atas), teks satu baris, waktu relatif, store SQLite. Di laptop: copy dari beberapa aplikasi lalu buka halaman lewat UIA; tempel ke Notepad dan jendela admin dicek pengguna (tanpa input sintetis dari saya).

## Hasil verifikasi 4a

- Build dan `dotnet format` bersih, 949 unit test lulus (baru: `ClipboardHistoryTests` 12, termasuk store SQLite).
- Di laptop: dua teks uji lewat `Set-Clipboard` masuk riwayat di atas teks pengguna; baris kedua yang berisi baris baru dan spasi ganda tampil `Gili test two second line with spaces`. Halaman dibuka lewat UIA Invoke ikon kiri: kolom `Search clipboard`, `Clear all`, baris 44 px. Dua baris uji dihapus lagi lewat UIA Invoke tombol × (`Remove from history`), pulau memendek.
- Nama aplikasi sumber kosong untuk teks dari `Set-Clipboard`: proses PowerShell itu langsung keluar sehingga Windows mengosongkan pemilik clipboard. Aplikasi biasa (Chrome, Word) tetap memegang jendela clipboard-nya; jalur penamaannya sama dengan nama aplikasi di bar.
- **Belum teruji, butuh pengguna** (menempel berarti mengirim Ctrl+V ke desktop): klik baris lalu teks muncul di Notepad/VS Code/Chrome/Word/Terminal/wmux, jendela admin hanya mendapat clipboard dan wide `Copied · press Ctrl+V to paste`, serta ↑/↓/Enter/Delete/Esc.
