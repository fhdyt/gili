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

## Rancangan 3i

Fitur: F-CLD-03, F-CLD-04, F-CLD-05, F-CLD-06, klik slot, titik hijau di ikon kiri (F-LEFT-05). Pengguna minta saya memilih sendiri; semua bagian **(keputusan Claude)** adalah pilihan saya.

### Pendekatan

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. Aturan di Core, App hanya menyambung (dipilih)** | `ClaudeBoard` mencatat "belum dilihat" dan nomor tunggu; kelas Core baru `ClaudeCards` (kartu mana yang tampil), `ClaudePage` (baris halaman), `ClaudeWindows` (penelusuran proses induk); semua bisa dites tanpa WPF (F-ISL-03, PRD 13) | Lebih banyak tipe kecil |
| B. Logika kartu dan gelembung di `ClaudeService` (App) | Lebih sedikit file | Aturan "tidak muncul lagi untuk pertanyaan yang sama", fokus, dan fullscreen tidak tertes |
| C. Kartu Claude sebagai jenis kartu baru di `IslandCoordinator` | Satu tempat untuk semua kartu | Coordinator jadi tahu isi sesi Claude; kartu pengingat prioritas 2 yang ada sudah memberi 8 detik, tahan saat fokus, dan lewati saat fullscreen |

### Sesi (`ClaudeBoard`)

- `Stop` mencatat `FinishedAt` dan `Unseen = !isFront(sesi)`; `isFront` disuntikkan (App: jendela sesi = jendela di depan).
- Done tetap berstatus Done setelah 4 detik selama belum dilihat (untuk urutan halaman "Done (belum dilihat)"); slot hanya menampilkan Done yang 4 detiknya masih berjalan (`DoneUntil`). Setelah dilihat: Idle, `IdleSince = FinishedAt`.
- Dilihat (`MarkSeen`): jendela sesi menjadi aktif, gelembung atau baris halaman diklik, atau prompt baru (`UserPromptSubmit`, atau file `busy` saat Done yang sudah selesai tampil).
- Setiap kali sesi masuk ke Needs input dari keadaan lain, sesi mendapat `WaitId` baru. Hook yang datang setelah file `waiting` untuk tunggu yang sama hanya memperbarui isi (jenis dan pesan), tidak memberi nomor baru.
- `Host`: nama terminal dari deskripsi file exe pemilik jendela (`Windows Terminal`, `wmux`); Claude Desktop tetap `Claude Desktop`.

### Kartu Needs input (`ClaudeCards`, F-CLD-03)

- Satu kartu dengan id `claude`, prioritas 2 (Reminder), 8 detik. Isi sesuai tabel PRD: judul per jenis, nama sesi, baris ketiga (cuplikan pertanyaan maks. 90 karakter, atau pesan izin; rencana tanpa baris ketiga). Tombol **Later** dan **Open Claude** / **Open terminal**. Ikon Claude kuning tanpa pulse (mockup `CARDS.claude`, `cls:''`), Ping sekali.
- Tunggu baru (WaitId yang belum pernah ditangani) → kartu tampil untuk tunggu terbaru; kartu yang sedang tampil berganti isi dan menghitung ulang 8 detik (sudah ditangani `IslandCoordinator.ShowCard`).
- Tunggu yang sudah ditangani tidak pernah membuka kartu lagi (Later, 8 detik habis, Open).
- Sesi kartu dijawab, berhenti, atau hilang → kartu ditutup. Isi berubah untuk tunggu yang sama (pesan izin dari hook datang setelah file) → isi diperbarui tanpa menghitung ulang.
- Saat fullscreen tunggu baru langsung dianggap ditangani (dilewati, PRD 6.3). Saat fase fokus kartu ditahan oleh coordinator dan tampil saat istirahat mulai bila sesinya masih menunggu.
- **(keputusan Claude)** Kartu pengingat todo juga tanpa pulse, karena mockup `CARDS.todo` juga `cls:''`.

### Belum dilihat (F-CLD-04)

- Gelembung hijau `IslandView.DoneBubble`: hanya saat compact, ada sesi belum dilihat, dan aktivitas Claude tidak tampil di slot maupun di gelembung aktivitas. Paling kanan, 6 px setelah gelembung aktivitas. Lingkaran hitam, ikon Claude hijau, garis dalam hijau 50%; angka di lencana hijau bila lebih dari satu (mockup `.bubble.cdone`, `.bn`).
- Klik: satu sesi → jendelanya ke depan dan dilihat; lebih dari satu → halaman Claude.
- Selama aktivitas Claude tampil, sesi lain yang belum dilihat jadi titik hijau 5 px di ikon Claude (slot dan gelembung aktivitas).
- Ikon kiri: titik hijau saat ada yang belum dilihat dan tidak ada yang menunggu; tooltip `Claude · 2 finished`.

### Jendela sesi (`ClaudeWindows`, F-CLD-06)

- Dari PID sesi, naik ke proses induk sampai proses yang punya jendela top-level terlihat (bukan tool window, tanpa owner). Induk yang mulai setelah anaknya berarti PID sudah dipakai ulang → berhenti. Berhenti juga di `explorer.exe` dan setelah 12 langkah.
- Diverifikasi di laptop (2026-10-31): sesi Desktop `claude.exe` → `claude.exe` (jendela `Claude`); sesi CLI `claude.exe` → `pwsh.exe` → `WindowsTerminal.exe`.
- Membawa ke depan memakai `TaskbarWindows.BringToFront` (pulihkan bila diminimize, lalu fokus).
- "Jendela aktif" = proses pemilik jendela di depan sama dengan proses jendela sesi. Pemantauan foreground memakai event `ForegroundChanged` yang sudah dimiliki `AppsService`, dan hanya dihitung bila ada sesi belum dilihat.

### Halaman Claude (`ClaudePage`, F-CLD-05)

- Baris persis mockup `renderClPage`: urut Needs input → Working → Done → Idle; ikon (centang untuk Done), nama 14 px semibold, baris kedua 12 px (`"pertanyaan"` untuk pertanyaan, pesan/judul untuk izin dan rencana, selain itu `folder · aplikasi`), kanan `Needs input`, `Working · 2:14`, `Done · 4m 12s`, `Idle since 14:05` (format jam mengikuti 12-hour clock). Needs input kuning, Done hijau, Idle opacity 0,55. Baris 44 px, jarak 6 px, radius 12, `#1C1C1E` dan `#2C2C2E` saat hover. Tanpa sesi: `No Claude sessions`.
- Klik baris: jendela sesi ke depan dan Done ditandai dilihat; pulau tetap terbuka (mockup `clFocus`).
- Waktu Working diperbarui tiap detik hanya selama halaman tampil (PRD 8.1).
- Baris ringkasan 5 jam menyusul di 3j.

### Klik slot

`ClaudeSlot.Involved`: sesi yang menunggu; kalau tidak ada, Done yang sedang tampil; kalau tidak ada, yang bekerja. Satu sesi → jendelanya ke depan (Done ikut dilihat) dan pulau tidak terbuka; lebih dari satu → halaman Claude. Klik gelembung aktivitas Claude tetap membuka halaman (PRD 6.3).

### Integrasi mati

**(keputusan Claude)** Sesuai PRD 6.2 dan tabel halaman 9: ikon Claude di kiri dan tab Claude hanya ada saat `Show Claude Code sessions` nyala. Saklar Left side → Claude dan Island → Claude nonaktif dengan keterangan `Turn on Claude Code sessions first` selama integrasi mati.

### Demo (F-BAR-09)

Papan demo terpisah menggantikan papan asli selama demo (sesi asli tetap dicatat di belakang): sesi Desktop `Integrate taskbar with Claude` bekerja, bertanya (kartu + Ping), dijawab; sesi terminal `top-taskbar` ikut bekerja (`2 sessions working`); keduanya selesai saat jendelanya tidak di depan, jadi muncul titik hijau lalu gelembung `2`; halaman Claude dibuka. Demo menjadi ±90 detik.

### Anggaran (PRD 8.1)

Tidak ada timer baru saat diam. Penelusuran proses (satu snapshot Toolhelp + satu `EnumWindows`) hanya saat `Stop`, saat klik, saat sesi pertama kali punya PID (nama host), dan saat foreground berganti selama ada sesi belum dilihat (hasilnya di-cache per PID). Timer halaman 1 detik hanya selama halaman Claude tampil dengan sesi Working.

## Hasil verifikasi 3i

- Build dan `dotnet format` bersih, 914 unit test lulus (baru: `ClaudeSeenTests` 11, `ClaudeCardsTests` 11, `ClaudePageTests` 10, coordinator 3, settings 1, demo 1).
- Penelusuran jendela di laptop: sesi Claude Desktop → proses `Claude` (PID 17912), sesi CLI → Windows Terminal dengan nama `Terminal`; 8–37 ms per pencarian.
- Sesi uji di jendela yang diminimize (`claude "Reply with the single word OK"`): slot `2 sessions working` → `✓ gili · Done · 5s` selama 4 detik → `gili 0:17` dengan titik hijau di ikon Claude (sesi uji belum dilihat, sesi ini masih bekerja).
- **Bug lama ditemukan dan diperbaiki**: Done kadang tertahan di slot. `DispatcherTimer` bisa berbunyi sedikit sebelum tenggatnya; `Tick()` tidak mengubah apa pun sehingga tidak ada `Changed` dan tenggat tidak pernah dipasang lagi. Sekarang tenggat selalu dipasang ulang setelah setiap tick (sama seperti `IslandController`). Exception di handler pipe dan file sesi sekarang dicatat di log, tidak hilang di dalam `InvokeAsync`.
- Halaman Claude dibuka lewat UIA Invoke ikon kiri: baris `gili · Terminal · Working · 0:04` dan dua sesi Desktop yang redup `Idle since 23:07`, sesuai mockup.
- `claude -p` mengirim `SessionEnd` tepat setelah `Stop`, jadi sesinya hilang setelah Done 4 detik dan tidak meninggalkan gelembung hijau (benar: halaman hanya mendaftar sesi yang hidup).
- Catatan: sesi CLI yang dijalankan lewat `Start-Process pwsh` tidak menulis `~/.claude/sessions/<pid>.json`, jadi Gili hanya mengenalnya dari hook (tanpa PID, jadi tanpa jendela). Klik baris sesi seperti itu hanya menandainya dilihat.
- **Belum teruji langsung, butuh pengguna** (tanpa input sintetis): kartu `Claude needs your input` + Ping dari pertanyaan sungguhan, gelembung hijau (butuh sesi ini tidak bekerja), klik baris/slot yang membawa jendela ke depan, dan klik gelembung. Semuanya bisa dilihat di mode demo (klik kanan bar → Demo mode, detik 60–92).

## Rancangan 3j

Fitur: F-CLD-08 (jendela pemakaian 5 jam). Semua bagian **(keputusan Claude)** adalah pilihan saya.

### Sumber data (diverifikasi 2026-10-31, Claude Code 2.1.316)

- Tidak ada input hook yang memuat `rate_limits`, `five_hour`, atau `resets_at` (dokumentasi hook resmi). Waktu reset resmi tetap hanya untuk statusline (temuan 3h).
- Hook **`StopFailure`** (matcher jenis error: `rate_limit`, `overloaded`, …) dipanggil *sebagai ganti* `Stop` saat giliran berakhir karena error API. Inputnya `error`, `error_details`, dan `last_assistant_message` (teks error yang tampil, misalnya `API Error: Rate limit reached`).
- Jadi: **mulai jendela** = `UserPromptSubmit` pertama saat tidak ada jendela berjalan, dibulatkan ke bawah ke jam penuh, berakhir 5 jam kemudian (PRD sumber 2, tooltip `Estimated`). **Batas tercapai** = `StopFailure` dengan error `rate_limit`. Bila teks errornya memuat waktu reset (`|1730000000` atau `resets 7pm` / `resets 7:30pm` / `resets at 19:00`), waktu itu menggantikan perkiraan dan tooltip tidak lagi `Estimated` **(keputusan Claude)**. Persen terpakai tidak tersedia, jadi ring halaman selalu menunjukkan waktu yang sudah lewat.

### Pendekatan

| Pendekatan | Untung | Rugi |
|---|---|---|
| **A. `ClaudeUsage` di Core + `ClaudeWindowStore` di Data (dipilih)** | Aturan mulai/batas/berakhir/restart dites tanpa WPF; baris `claude_window` yang sudah ada di skema dipakai | Satu kelas Core dan satu store baru |
| B. Membungkus statusline pengguna untuk membaca waktu resmi | Waktu dan persen resmi | Mengubah konfigurasi statusline pengguna yang dipakai skrip lain (ditolak di 3h) |
| C. Hanya perkiraan, tanpa `StopFailure` | Paling sederhana | `Limit reached` tidak pernah tampil, padahal PRD memintanya |

### Aturan

- `StopFailure` juga mengakhiri giliran sesinya: Working/Needs input → Idle tanpa Done (bukan selesai normal) **(keputusan Claude)**.
- Batas tercapai tanpa jendela berjalan (dipakai dari perangkat lain): jendela dibuat dari waktu reset di teks error; kalau tidak ada, perkiraan dari jam penuh sekarang.
- Jendela berakhir → kartu `Claude usage reset` / `Your 5-hour limit is available again` (prioritas 1, ikon jam pasir oranye Claude, pulse), tombol **Close** dan **Open Claude**, suara timer berulang maksimal 5 menit. Kartu ditahan selama fase fokus pomodoro (bendera baru `IslandCard.HoldInFocus`), tetap tampil saat fullscreen seperti kartu timer.
- **Open Claude**: jendela sesi Claude Desktop yang hidup, kalau tidak ada sesi terminal terakhir; tanpa sesi → halaman Claude.
- Jendela yang berakhir saat Gili mati atau laptop tidur lebih dari 1 menit: tandai `notified` tanpa kartu **(keputusan Claude: kartu basi tidak berguna; jendela berikutnya mulai dari prompt berikutnya)**. Bangun dari tidur sebelum itu tetap memunculkan kartu.
- Slot: aktivitas `ClaudeWindow` (urutan 9) `Resets in 3:12:40` oranye; `ClaudeLimit` (tepat setelah urutan 2) `Limit reached 1:04:12` kuning. Teks diperbarui tiap detik hanya saat tampil di slot (PRD 8.1). Klik slot/gelembung → halaman Claude.
- Halaman Claude: baris ringkasan di atas sesi (mockup `limRow`): ring 22 px, `5-hour window`, `Resets 19:00` (atau `Limit reached · Resets 19:00`), sisa waktu di kanan.
- Saklar `Show 5-hour usage timer` (default nyala) aktif hanya saat integrasi nyala, dengan keterangan yang sama seperti saklar Claude lain.
- **(keputusan Claude)** Toast tanpa suara untuk kartu ini menunggu layanan toast, yang juga belum ada untuk kartu timer; dicatat sebagai sisa.
- Demo: setelah halaman Claude, slot `Limit reached`, lalu jendela demo yang berakhir 8 detik kemudian dengan kartunya.

### Anggaran (PRD 8.1)

Satu timer tenggat di waktu reset; teks slot 1 detik hanya saat tampil di slot; satu baris SQLite saat jendela mulai, batas tercapai, dan berakhir.
