# Revisi 16: halaman Projects di pulau (F-ISL-34)

Tanggal: 1 November 2026. Permintaan pengguna: tab baru di pulau berisi timeline project. Langkah pertama mengisi nama project, lalu task beserta tanggalnya. Task yang selesai bisa ditandai, dan tanggalnya bisa digeser dengan drag seperti di Notion. Permintaan lanjutan: ikon di launcher kiri dengan saklarnya, warna per task dari palet note, dan grid tegak serta mendatar.

Acuan visual dan gerak: mockup `docs/mockup/top-taskbar-mockup.html` (tab Projects; tombol mockup *Projects: first run*). Perilaku lengkap: PRD 6.3 "Halaman Projects" dan F-ISL-34.

Sebelum desain ini dipilih, dua desain lain dicoba di mockup, yaitu daftar + timeline dan kalender per minggu. Pengguna memilih kembali ke timeline ala Notion.

## Keputusan

1. **Posisi tab setelah Todo**: `IslandPage.Projects` disisipkan setelah `Todo`, dan launcher kiri mengikuti urutan yang sama. `PageSet` dan `LeftIcons` mendapat flag `Projects` dengan bit baru di akhir. Keduanya disimpan sebagai nama di `settings.json`, jadi pengaturan lama tetap terbaca. `PageSet.All` naik menjadi 2047. `IslandTabs.Of` tidak lagi menggeser bit menurut urutan enum, tetapi memetakan lewat nama, karena urutan `IslandPage` tidak lagi sama dengan urutan bit `PageSet`.
2. **Satu project tampil sekaligus**. Project yang terakhir dilihat disimpan di `settings.json` sebagai `ProjectId`. Kalau project itu sudah dihapus, yang tampil project pertama.
3. **Tanggal adalah tanggal lokal (`DateOnly`)**, bukan waktu UTC. Task berskala hari, jadi pindah zona waktu tidak boleh menggesernya. Ini berbeda dari jadwal `Scheduler`, yang memang butuh waktu absolut.
4. **Tabel baru di migrasi 5**: `projects (id, name, sort_order, created_at)` dan `project_tasks (id, project_id, title, start_date, end_date, color, done_at, sort_order, created_at)`. Menghapus project ikut menghapus tasknya lewat `ON DELETE CASCADE`, karena `Database` sudah menyalakan `PRAGMA foreign_keys=ON` di setiap koneksi.
5. **Logika di Core, tanpa WPF** (`Gili.Core.Projects`):
   - `ProjectTask` dan `Project` (record).
   - `ProjectText`: format ringkasan, label drag, dan teks aksesibel.
   - `TimelineRange`: rentang hari yang bisa digulir, posisi awal, dan scroll yang dibatasi.
   - `TaskDrag`: tanggal baru dari geseran sekian piksel untuk tengah, ujung mulai, dan ujung selesai.
   - Semuanya diuji unit.
6. **`ProjectService` di App** mengikuti pola `TodoService`: daftar di memori, store opsional (null tanpa database), dan event `Changed` di thread UI. Tidak ada timer.
7. **Timeline adalah kontrol WPF sendiri** (`ProjectTimeline`, turunan `FrameworkElement`):
   - Kepala tanggal, grid, kolom akhir pekan, dan garis hari ini digambar di `OnRender` untuk rentang hari yang terlihat saja.
   - Bar adalah elemen anak yang ditata di `ArrangeOverride`.
   - Scroll mengubah offset, lalu hanya `InvalidateVisual` dan `InvalidateArrange` yang dipanggil. Tidak ada ratusan `TextBlock` untuk tanggal.
   - Pilihan ini lebih ringan daripada mockup HTML, yang membangun semua kolom sebagai elemen.
8. **Gerak bar mendarat**: pegas compact ↔ wide (`SpringParameters.Resize`) untuk posisi kiri dan lebar, di-step lewat `CompositionTarget.Rendering` hanya selama ada bar yang bergerak. Pola ini sama dengan geometri pulau. Kalau Animation effects mati, bar langsung lompat.
9. **Roda mouse**: `IslandWindow.ListUnder` mengenali `ProjectTimeline` seperti `WheelColumnView`. Akibatnya roda di atas timeline tidak memindah halaman, dan timeline sendiri yang menggulir hari.
10. **Form task** memakai `TodoText.TryDate` untuk mengurai tanggal, sama dengan Tools, dan `TodoText.Day` untuk menampilkannya (`Sun 1 Nov`). Bulatan warna memakai `NoteStyle.Colors`.
11. **Tanpa pengingat, aktivitas slot, atau kaitan ke Calendar/Todo.** Pengguna tidak memintanya, dan menambahkannya berarti menambah timer dan prioritas baru di `IslandCoordinator`. Mode demo hanya membuka halaman ini, dengan data pengguna sendiri, seperti halaman Todo.
12. **Batas**: nama project 60 karakter dan nama task 80 karakter, mengikuti mockup. Jumlah project dan task tidak dibatasi. Setelah pulau setinggi layar, baris task digulir tegak di dalam timeline.

13. **Penyesuaian setelah dicoba (permintaan pengguna 2026-11-01)**: nama task di bar 11 px (sebelumnya 12,5 px); timeline menempel ke tepi kiri, kanan, dan bawah pulau tanpa sudut sendiri; form task pindah ke atas timeline; `IslandLayout.PageBottomOf(IslandPage.Projects)` = 0, jadi pulau tidak menyisakan 20 px di bawah halaman ini.

## Arsitektur

```
Gili.Core/Projects/
  Project.cs          Project, ProjectTask (record)
  ProjectText.cs      Meta ("3/8 done · 23 Oct – 11 Nov"), DragLabel, Accessible
  TimelineRange.cs    Range(tasks, today), Home(...), Clamp(...)
  TaskDrag.cs         Drag(edge, start, end, days)
Gili.Data/
  Migrations.cs       langkah 5
  ProjectStore.cs     All, Add, Update, Rename, Delete (project + task)
Gili.App/Island/
  ProjectService.cs   data + event Changed
  ProjectsViewModel.cs  project terpilih, form, menu, teks
  ProjectsPage.xaml(.cs)  baris atas, menu project, form, keadaan kosong
Gili.App/Controls/
  ProjectTimeline.cs  timeline: gambar, scroll, drag, keyboard
```

Alur data mengikuti halaman lain. `ProjectService` menyimpan ke `ProjectStore` lalu memicu `Changed`, `ProjectsViewModel` membangun ulang teks dan data bar, dan `ProjectTimeline` menggambar ulang. Selama drag, timeline hanya memindah barnya sendiri, dan tanggal baru baru disimpan saat dilepas, jadi tidak ada tulisan ke database per frame.

Lokasi yang ikut berubah:
- `IslandModels.IslandPage`, `HiddenRules` (`LeftIcons`, `PageSet`, `PageOf`), `IslandTabs.Of`, dan `SettingsCatalog` (`LeftRows`, `Pages`).
- `IslandViewModel` (tab, ikon `Icon.Gantt`), `IslandWindow` (`CreatePage`, `PageGlow` indigo, `ListUnder`), dan `LeftSide.xaml` (tombol `Tag="Projects"`).
- `Icons.xaml`, `App.xaml.cs` (layanan), `IslandController` (properti `Projects`), dan `DemoScript` (`DemoOpen(IslandPage.Projects)`).
- `AppSettings.ProjectId`.

## Penanganan error

- Database tidak tersedia: `ProjectService` berjalan tanpa store, sama seperti Todo. Datanya hilang saat restart, tetapi bar tidak mati.
- Warna di luar 0–7 jatuh ke Default (`NoteStyle.ClampColor`).
- Tanggal di database yang tidak bisa diurai membuat baris itu dilewati dan dicatat di log. Satu baris rusak tidak mematikan halaman.
- `end_date` sebelum `start_date` (database diubah tangan) dibaca dengan kedua tanggal ditukar.

## Pengujian

- Unit (Core):
  - `TaskDrag`: tengah, ujung mulai, ujung selesai, batas satu hari, dan pembulatan setengah kolom.
  - `TimelineRange`: project kosong, project yang muat, project yang lebih lebar dari layar, dan batas scroll.
  - `ProjectText`: ringkasan, label satu hari dan banyak hari, teks aksesibel terlambat dan selesai.
  - `IslandTabs.Of` dan `HiddenRules.PageOf(LeftIcons.Projects)`.
  - `SettingsCatalog` untuk saklar Projects.
- Unit (Data): migrasi 5 di database baru dan database versi 4; `ProjectStore` untuk tambah, ubah, urutan, hapus project beserta tasknya, dan hapus task.
- Manual di laptop pengembang:
  - Buat project pertama, tambah beberapa task berturut-turut, lalu seret, tarik ujung, centang, ubah warna, hapus.
  - Roda mouse di atas timeline, ‹ › Today, dan Esc bertingkat.
  - Ikon kiri dan kedua saklarnya, lebar pulau 614 → 576 px.
  - Restart (data dan project terakhir tetap), mode demo, dan DPI 125%.

## Di luar cakupan

Pengingat task, task di Calendar dan Todo, aktivitas di slot, dependensi antar-task, mengatur urutan task dengan drag tegak, dan ekspor.
