# Revisi 15: rilis lewat GitHub dan Fase 6 tanpa sertifikat

Tanggal: 1 November 2026. Pengguna memutuskan persiapan publikasi (Microsoft Store, sertifikat OV, WACK, VirusTotal, VM bersih, hosting privacy policy) ditunda dan untuk sementara Gili dirilis lewat GitHub Releases saja, lalu meminta semua pekerjaan yang tersisa dikerjakan tanpa berhenti dengan pilihan terbaik. Semua pilihan di bawah adalah **keputusan saya**.

| Bagian | Keputusan | ID PRD |
|---|---|---|
| Cek update build tanpa tanda tangan | Memeriksa GitHub seperti build bertanda tangan, tetapi hanya memberi tahu: titik biru, `Update available · <versi> · run the new Gili-Setup over this one`, tombol **Download** yang membuka halaman rilis. Tidak pernah mengunduh atau menjalankan installer yang tanda tangannya tidak bisa diperiksa; Install updates automatically nonaktif dengan keterangannya | F-PUB-04 |
| Aturan fase | CLAUDE.md dan PRD 14: Fase 6 boleh dikerjakan sebelum Fase 5 selesai, kecuali yang butuh sertifikat (notifikasi Windows di versi GitHub) | 14 |
| Lirik lagu | Diimplementasikan sesuai PRD 6.3: LRCLIB `/api/get` lalu `/api/search` (selisih durasi ≤ 2 detik), cache memori 20 lagu, baris saat ini di slot, blok tiga baris di halaman Media, tombol Lyrics = saklar Show lyrics. Mode demo memakai baris buatan sendiri (bukan lirik asli). Privacy policy menyebut LRCLIB. Saat diuji, LRCLIB menjawab 502, jadi jalur `Lyrics unavailable offline` yang teruji, belum lirik asli | F-ISL-26 |
| DND Windows | `FocusSessionManager` terbukti Limited Access Feature (ditolak dengan `Feature com.microsoft.windows.focussessionmanager.1 is not available`). Gili tidak memakai registry atau API tak resmi; selama fase fokus, halaman Timer menampilkan chip **Do not disturb** yang membuka `ms-actioncenter:` | F-TOOL-04 |
| Progres operasi file Explorer | Dialog dikenali dari `EVENT_OBJECT_SHOW` pada hook yang sudah ada (kelas `OperationStatusWindow`), dibaca dengan UI Automation 2×/detik di thread latar belakang hanya selama dialog terbuka. Struktur dialog diverifikasi langsung; kartu konflik tanpa suara meneruskan Replace/Skip/Try again lewat `InvokePattern`, dan diuji end-to-end (salinan 4 GB: `29%` lalu `Copied`; Skip dari kartu Gili menutup dialog Explorer) | F-ISL-15 |
| Notifikasi Windows | Tetap menunggu sertifikat OV untuk identitas paket versi GitHub; issue #28 tetap open | F-ISL-27 |
| Glitch drag | Log diagnostik hanya menunjukkan enter/leave biasa tanpa fullscreen palsu; gejalanya perlu dijelaskan pengguna. Kode diagnostik tetap di `git stash` | — |

## Anggaran (PRD 8.1)

- Lirik: satu permintaan HTTPS per lagu hanya bila Show lyrics nyala; pengecekan baris 4×/detik hanya selama lirik bersinkron diputar.
- Operasi file: UI Automation hanya selama dialog Explorer terbuka.
- Diukur setelah semua fitur: memori privat 107 MB (batas 120 MB).
