# Fase 3h–3j: integrasi Claude Code (desain)

Tanggal: 2026-10-31. Fitur: F-CLD-01–08, F-LEFT-05. Pengguna meminta saya memilih sendiri; bagian **(keputusan Claude)** adalah pilihan saya. Sumber perilaku: PRD 6.3 "Sesi Claude" dan "Batas pemakaian Claude", 9.2 (hook), 11, 12; mockup `clSlotHTML`, `renderClPage`, `CL_ASK`.

## Verifikasi di laptop pengguna (PRD 14, awal fase)

Dibaca tanpa mengubah apa pun:

| Hal | Temuan | Akibat |
|---|---|---|
| `~/.claude/sessions/<pid>.json` | Ada untuk sesi CLI (`entrypoint: "cli"`, wmux) dan sesi Claude Desktop (`entrypoint: "claude-desktop"`, `hostSessionId`). Field: `pid`, `sessionId`, `cwd`, `name` (judul sesi Desktop, misalnya `Desain PCB LED SMD 4x4cm`; untuk CLI nama turunan seperti `gili-6b` dengan `nameSource: "derived"`), `status` | Nama sesi dan PID dari file ini |
| Nilai `status` | Dari binary Claude Code 2.1.314: `busy`, `idle`, dan `waiting` dengan `waitingFor` (`input needed`, `dialog open`, `approve plan`, judul dialog izin, `sandbox request`, …) | Izin yang tidak memicu hook `Notification` (Claude Desktop) tetap terdeteksi sebagai Needs input lewat `waiting` |
| Waktu reset 5 jam resmi | Hanya dikirim Claude Code ke perintah **statusline** (`rate_limits.five_hour.resets_at`, `used_percentage`). Hook tidak menerimanya dan tidak ada salinannya di disk. Statusline pengguna sudah dipakai skrip lain | Fase 3j memakai perkiraan dari hook (sumber 2 PRD), tooltip `Estimated`. Membungkus statusline pengguna terlalu invasif **(keputusan Claude)** |
| Hook pengguna | `~/.claude/settings.json` berisi hook wmux untuk `PostToolUse` (per tool), `Notification`, `Stop` | Penggabung tidak boleh menyentuh entri itu |
| C++ Build Tools | VS 2019 Build Tools dengan VC tools ada | NativeAOT untuk helper bisa dibangun |

Belum bisa diverifikasi tanpa pengguna: apakah prompt izin di Claude Desktop memicu hook `Notification`, dan waktu mulai helper lewat app execution alias (paket MSIX). Keduanya dicek di akhir Fase 3h.

## Sub-fase

| Sub-fase | Isi |
|---|---|
| **3h** | F-CLD-01 (saklar, penggabung hook, cadangan, status di Settings), F-CLD-07 (`Gili.ClaudeHook`), pipe, pemantau file sesi, F-CLD-02 (status di slot dan gelembung, Done 4 detik), ikon Claude di kiri (F-LEFT-05: warna dan tooltip) |
| 3i | F-CLD-03 (kartu Needs input + Ping, 8 detik, ditahan saat fokus), F-CLD-04 (gelembung hijau belum dilihat, titik hijau), F-CLD-05 (halaman Claude), F-CLD-06 (membawa jendela sesi ke depan), klik slot |
| 3j | F-CLD-08 (jendela 5 jam perkiraan, kartu `Claude usage reset`, halaman ringkasan) |

## Rancangan 3h

### Helper `Gili.ClaudeHook`

- Proyek console baru `src/Gili.ClaudeHook`, `PublishAot`, `InvariantGlobalization`, tanpa refleksi (JSON dibaca dengan `Utf8JsonReader`).
- `Gili.ClaudeHook.exe <Event>`: membaca stdin (input hook), mengambil `session_id`, `cwd`, `tool_name`, `notification_type`, `message`, dan teks pertanyaan pertama `AskUserQuestion` (maks. 200 karakter), lalu menulis satu baris JSON ke `\\.\pipe\Gili.Claude` dengan batas sambung 30 ms. Teks prompt tidak pernah dibaca.
- Tidak menulis ke stdout atau stderr; semua exception ditelan; selalu keluar dengan kode 0 (F-CLD-07).

### Hook di `~/.claude/settings.json` (Core `ClaudeHookConfig`)

- Fungsi murni teks → teks dengan `System.Text.Json.Nodes`, jadi semua aturan F-CLD-01 punya unit test.
- Entri Gili dikenali dari perintah yang memuat `Gili.ClaudeHook` atau `gili-claude-hook`. **Pasang** = buang entri Gili lama, lalu tambah entri baru per hook di tabel PRD 9.2 (`SessionStart`, `UserPromptSubmit`, `PreToolUse` dengan matcher `AskUserQuestion|ExitPlanMode`, `Notification`, `PostToolUse` dengan matcher `*`, `Stop`, `SessionEnd`), `timeout: 2`. Jadi memasang dua kali tidak menggandakan entri, dan path baru menggantikan yang lama. **Lepas** = buang entri Gili; array event yang kosong dan objek `hooks` yang kosong ikut dibuang.
- Hook lain (wmux) dan field lain tidak diubah. Teks yang bukan JSON valid atau root yang bukan objek ditolak tanpa menulis apa pun.
- Penulisan (App): salin dulu ke `settings.json.gili-backup`, tulis ke file sementara, lalu ganti. File atau folder `.claude` yang belum ada dibuat.
- Path helper: salinan GitHub dan `dotnet run` memakai `Gili.ClaudeHook.exe` di samping `Gili.exe`; versi Store memakai alias `gili-claude-hook.exe` di `%LOCALAPPDATA%\Microsoft\WindowsApps` (manifest `AppExecutionAlias`). Path ditulis dengan garis miring maju dan diberi tanda kutip.

### Sesi (Core `ClaudeBoard`)

Status per sesi: Idle, Working, NeedsInput (Question, Permission, Plan), Done. Masukan:

| Masukan | Efek |
|---|---|
| `SessionStart` | Sesi masuk sebagai Idle |
| `UserPromptSubmit` | Working; waktu mulai dicatat |
| `PreToolUse` `AskUserQuestion` / `ExitPlanMode` | NeedsInput Question (dengan cuplikan) / Plan |
| `Notification` `permission_prompt` | NeedsInput Permission dengan pesannya |
| `PostToolUse` | NeedsInput → Working |
| `Stop` | Done (lama = sejak prompt), tampil 4 detik lalu Idle |
| `SessionEnd`, proses mati | Sesi dihapus |
| File `busy` | Sesi yang Idle menjadi Working (sesi yang sudah jalan sebelum Gili mulai) |
| File `waiting` | Working → NeedsInput Permission bila hook belum memberi jenis lain |
| File `idle` saat Working/NeedsInput | Tunggu 1,5 detik untuk `Stop`; kalau tidak datang, Idle tanpa Done (Esc) **(keputusan Claude: file sering ditulis sebelum hook `Stop` sampai)** |

- Nama sesi: `name` dari file sesi kecuali `nameSource: "derived"`; kalau tidak ada, nama folder `cwd` (PRD 6.3). Aplikasi: `Claude Desktop` untuk `claude-desktop`, selain itu `Terminal`.
- Ringkasan slot (`ClaudeSlot`) persis `clSlotHTML` di mockup: NeedsInput (kuning, nama maks. 28 karakter + `Needs input`, atau `2 sessions need input`) → Done yang sedang tampil (centang hijau, nama + `Done · 4m 12s`) → Working (oranye berdenyut, nama + waktu berjalan, atau `2 sessions working`). Aktivitas `ClaudeAttention` (urutan 2) untuk NeedsInput dan Done; `ClaudeWorking` (urutan 6) untuk Working.
- Data sesi hanya di memori; tidak ditulis ke database atau log (PRD 8.2).

### Platform dan App

- `ClaudePipeServer`: `NamedPipeServerStream` async, satu baris per koneksi, hanya untuk pengguna yang sama (ACL bawaan pipe).
- `ClaudeSessionFiles`: `FileSystemWatcher` pada `~/.claude/sessions`, dibaca ulang paling cepat tiap 250 ms; cek proses hidup tiap 5 detik selama ada sesi.
- `ClaudeService` (App): menyambungkan semuanya hanya selama saklar nyala; pembaruan UI maksimal 4 kali per detik.
- Settings → Claude → `Show Claude Code sessions` (default mati) dengan baris status sebagai keterangan: `Waiting for the first Claude session`, `Connected · last event 2 min ago`, `Can't read Claude settings`, `Hook helper not found · Reinstall`, plus `Applies to Claude sessions started from now on` setelah dinyalakan.

## Anggaran (PRD 8.1)

Saat mati: nol (tidak ada pipe, watcher, atau timer). Saat nyala: satu pipe menunggu, satu watcher, satu cek proses tiap 5 detik hanya selama ada sesi, dan timer 1 detik hanya selama slot menampilkan waktu berjalan. Helper: proses NativeAOT ≤ 50 ms.

## Pengujian

- Unit test Core: penggabung hook (pasang, pasang dua kali, lepas, hook wmux tetap utuh, JSON tidak valid, path berganti), `ClaudeBoard` (semua baris tabel di atas, Esc, proses mati, beberapa sesi), `ClaudeSlot`, parser file sesi dan pesan pipe.
- Di laptop: ukur waktu helper (p95 dengan Gili jalan dan mati), pasang hook lewat saklar lalu cek `settings.json` (wmux tetap ada, cadangan dibuat), dan lihat status sesi ini sendiri (sesi Claude Code di wmux) di slot.

## Hasil verifikasi 3h

- Build bersih, 822 unit test lulus (Core: penggabung hook 12, `ClaudeBoard` 20, slot 12, parser 9).
- `Gili.ClaudeHook` NativeAOT 1,7 MB. Waktu dari mulai proses sampai keluar, 50 kali, input `PostToolUse` 20 KB, **Gili tidak berjalan**: median 46 ms, p95 60 ms (target ≤ 100 ms). Tidak ada output, kode keluar selalu 0.
- Salinan terpasang memuat helper di samping `Gili.exe`; saklar `Show Claude Code sessions` tetap mati.
- **Belum teruji, butuh pengguna**: menyalakan saklar di Settings → Claude. Uji langsung yang mengubah `~/.claude/settings.json` ditolak pengaman auto mode Claude Code, karena file itu konfigurasi Claude sendiri. Yang perlu dicek setelah saklar dinyalakan: hook Gili masuk dan hook wmux tetap ada, `settings.json.gili-backup` dibuat, sesi baru tampil di slot (Working → Done), waktu helper p95 ≤ 50 ms saat Gili berjalan, dan apakah prompt izin di Claude Desktop memicu Needs input.
- **Diuji setelah pengguna mengizinkan (2026-10-31 19:27):** saklar dinyalakan lewat `settings.json`. Tujuh hook Gili masuk ke `~/.claude/settings.json` dengan matcher dan `timeout: 2` sesuai PRD; semua hook wmux, `statusLine`, `mcpServers`, dan field lain utuh; `settings.json.gili-backup` identik dengan file asli. Helper saat Gili mendengarkan: median 13 ms, p95 16 ms, maks 23 ms (target ≤ 50 ms).
- **Sesi sungguhan** (`claude -p` di folder uji): slot `2 sessions working` saat sesi uji dan sesi CLI ini (dari file sesinya) bekerja bersamaan, lalu `✓ gili-hook-test · Done · 5s`, lalu kembali ke `gili 1:07`. Uji pertama menemukan bug: `claude -p` mengirim `SessionEnd` tepat setelah `Stop`, sehingga Done hilang sebelum tampil. Sekarang sesi yang berakhir tetap ada sampai Done 4 detiknya selesai (2 unit test baru).
- Masih butuh pengguna: apakah prompt izin di Claude Desktop membuat slot kuning `Needs input`.
