# Fase 1b: Pulau Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pulau hidup di jendelanya sendiri dengan animasi pegas: compact (jam atau aktivitas + gelembung), wide (kejadian singkat), dan expanded (panel mengambang dengan menu tab, halaman Media, jam di bawah). Pulau diatur oleh `IslandCoordinator` sesuai prioritas PRD, menampilkan musik/video dari media session Windows, dan menampilkan status ngecas.

**Architecture:** Aturan prioritas, pegas, geometri, dan aturan media/daya ada di `Gili.Core` dan dites unit. `Gili.Platform` membaca media session (GSMTC), judul jendela VLC, dan `PowerManager`. Di `Gili.App`, `IslandController` menghubungkan provider ke coordinator, `IslandViewModel` menyiapkan data tampilan, dan `IslandWindow` (jendela transparan topmost milik `BarWindow`) menggambar pulau. Ukuran pulau digerakkan pegas per frame lewat `CompositionTarget.Rendering`, hanya selama ada pegas yang bergerak.

**Tech Stack:** .NET 10, C# WPF, CommunityToolkit.Mvvm, WinRT `Windows.Media.Control` dan `Windows.System.Power`, xUnit.

**Spec:** [docs/superpowers/specs/2026-10-25-top-taskbar-prd.md](../specs/2026-10-25-top-taskbar-prd.md) (6.3, 8.3, 9.2, F-ISL-02..07). Lanjutan dari [rencana 1a](2026-10-26-fase-1a-bar-dasar.md).

**Nama:** rencana ini ditulis dan dieksekusi dengan nama lama `TopBar`. Setelah eksekusi, semua rujukan di sini diganti ke **Gili** agar cocok dengan kode; commit 1b di riwayat git masih memakai path `src/TopBar.*`.

## Global Constraints

- Semua teks UI bahasa Inggris; angka dan waktu memakai format invariant/`en-US`. `Gili.Core` tanpa WPF/WinRT/P/Invoke.
- Pulau `#000000` di semua keadaan. Compact: tinggi = setting Island height, di tengah tinggi bar, radius = setengah tinggi, lebar = isi + 28 px dengan minimum 200 px, +16 px saat hover. Wide: lebar = isi + 32 px, minimum 200 px.
- Mengambang (8 px dari tepi atas, lebar maksimum = lebar layar − 24 px): expanded 680 × 256 radius 32, kartu 560 × 104 radius 28, drop zone 420 × 140 radius 30.
- Pegas (damping ratio / response): buka 0,82 / 0,42 dtk; tutup 1,0 / 0,32 dtk; compact ↔ wide, isi slot berganti, dan perubahan tinggi 0,85 / 0,30 dtk; hover 0,8 / 0,30 dtk; gelembung muncul/hilang 320 ms dengan sedikit memantul. Transisi bisa disela: keadaan baru mulai dari ukuran yang tampil di layar.
- Saat pulau mengambang: latar bar (tint, kaca, garis bawah) memudar 220 ms; isi bar tetap tampil dengan bayangan teks hitam 55% blur 3 px; elemen yang tertimpa panel (+8 px kiri-kanan) memudar 160 ms dan tidak bisa diklik.
- Isi pulau muncul 220 ms (opacity + blur 6 → 0 px + skala 0,98 → 1) mulai 120 ms setelah pulau mulai membesar. Isi slot berganti dengan fade 180 ms. Pindah halaman: geser 300 ms ease-out eksponensial; sorotan tab 320 ms ease-out.
- "Animation effects" Windows mati → semua transisi pulau menjadi cross-fade 100 ms tanpa pegas.
- Menu tab: kapsul 14 px dari tepi atas/kiri/kanan, tinggi 36, radius 18, latar `#1C1C1E`; sorotan `#3A3A3C` radius 15, inset 3; teks tab 12 px `#8E8E93`, tab aktif putih semibold dengan ikon hijau `#30D158`; ikon 15 px. Saat musik diputar, ikon tab Media menjadi bar level hijau.
- Jam saat terbuka: baris 12 px di tengah bawah (8 px dari bawah, tinggi 22, radius 11), jam putih semibold + tanggal abu-abu; hover latar `#1C1C1E`; klik membuka Calendar.
- Slot musik: bar level + `Judul – Artis` (maks. 36 karakter) + tombol play/pause. Slot video: ikon film oranye `#FF9F0A` + judul (maks. 32 karakter) + garis progres 36 px. Slot ngecas: petir hijau + persen.
- Wide ngecas: petir hijau + `Charging · 64%` selama 3 detik; dicabut: petir + `On battery · 63%` selama 2 detik.
- Klik ikon atau slot yang membuka halaman pulau bekerja sebagai buka/tutup. Pulau menutup dengan klik di luar, Esc, atau mouse di luar pulau selama 3 detik.

## Keputusan yang dibuat di rencana ini

PRD tidak mengatur hal-hal berikut secara eksplisit, jadi rencana ini memutuskannya. Konfirmasikan ke pengguna sebelum eksekusi kalau ada yang ragu.

1. **Media yang di-pause** tetap memegang slot selama 2 menit setelah sebelumnya diputar, supaya bisa dilanjutkan dari pulau. Setelah itu jam kembali. Session yang ditemukan dalam keadaan pause saat Gili mulai tidak memegang slot.
2. **Tab yang halamannya belum dibangun** tetap tampil (supaya kerangka menu bisa diuji) dan berisi nama halaman + `Coming in Phase 2` atau `Coming in Phase 3`. Tab Claude disembunyikan karena integrasi Claude baru ada di Fase 3 (default mati), jadi menu berisi 8 tab.
3. **Kartu dan drop zone**: `IslandCoordinator` dan `IslandLayout` sudah mendukung keduanya (aturan prioritas dan ukuran), tetapi isinya baru dibuat bersama fiturnya (mode demo di 1c, kartu di Fase 2, Shelf di Fase 3).
4. **Pulau selalu digambar di `IslandWindow`**, juga saat compact. `BarWindow` mengosongkan bagian tengah. `IslandWindow` dimiliki (`Owner`) oleh `BarWindow` supaya selalu di atas bar walaupun bar menegaskan ulang posisi topmost-nya.
5. **Kejadian wide dengan kunci yang sama saling mengganti** (cabut-colok charger berulang hanya menampilkan yang terakhir).
6. **Saat fullscreen**, kejadian wide dan kartu prioritas 2 dibuang (tidak ditampilkan belakangan), dan pulau yang terbuka ditutup.
7. **Kaca bar saat pulau mengambang**: accent policy tidak bisa di-fade, jadi tint memudar 220 ms lalu kaca dimatikan di akhir fade; saat menutup, kaca dinyalakan dulu lalu tint muncul lagi.

## Di luar rencana ini

- **1c**: aplikasi berjalan di kiri, cuplikan clipboard, aturan jarak 24 px dan overflow, mode demo (termasuk isi kartu dan drop zone), total jaringan hari ini, jeda polling saat layar mati.
- Halaman Timer, Alarms, Calendar, Todo, Notes (Fase 2); Shelf, Claude, System (Fase 3); hotkey `Ctrl+Alt+Space` (Fase 4).
- Peringatan baterai 20%/10%/80%/100% (F-ISL-11, Fase 3).

## Struktur file

```
src/Gili.Core/
  Motion/Spring.cs                     pegas + preset PRD 8.3
  Island/IslandModels.cs               mode, halaman, jenis aktivitas, wide, kartu, IslandView
  Island/IslandCoordinator.cs          aturan prioritas dan antrean (F-ISL-03)
  Island/IslandLayout.cs               ukuran/posisi tiap mode, aturan elemen tertimpa
  Media/MediaState.cs                  MediaState, MediaKind, PlaybackType
  Media/MediaRules.cs                  klasifikasi musik/video, nama aplikasi, judul VLC, teks slot/waktu, MediaPresence
  Power/PowerRules.cs                  PowerSnapshot, kejadian wide charger
src/Gili.Platform/
  Media/MediaSessionMonitor.cs         GSMTC: pilih session, baca properti/timeline/cover, perintah
  Media/VlcWindow.cs                   judul jendela vlc.exe
  Power/PowerMonitor.cs                Windows.System.Power.PowerManager
  Shell/WindowStyles.cs                (+ SetNoActivate)
  Shell/ForegroundWindow.cs            ambil/kembalikan jendela foreground
  Interop/Win32.cs, WindowMessages.cs  (+ SetForegroundWindow, WM_MOUSEHWHEEL)
src/Gili.App/
  ClockService.cs                      detak tiap pergantian menit (dipindah dari BarViewModel)
  Resources/Icons.xaml                 geometri ikon 24 × 24 dari mockup
  Controls/IconView.cs                 menggambar ikon garis/isi
  Controls/LevelMeter.cs               bar level musik
  Island/IslandController.cs           coordinator + timer tenggat + daya + media
  Island/MediaService.cs               GSMTC + cadangan VLC + MediaPresence + detak progres
  Island/IslandViewModel.cs            data tampilan pulau
  Island/IslandWindow.xaml(.cs)        jendela pulau, pegas, layer compact/wide/expanded, input
  Island/MediaPage.xaml(.cs)           halaman Media (musik dan video)
  Bar/BarWindow.xaml(.cs), BarViewModel.cs   tanpa pulau; latar memudar, elemen tertimpa
  App.xaml(.cs)                        komposisi
tests/Gili.Core.Tests/
  Motion/SpringTests.cs
  Island/IslandCoordinatorTests.cs, IslandLayoutTests.cs
  Media/MediaRulesTests.cs, MediaPresenceTests.cs
  Power/PowerRulesTests.cs
```

Pesan commit diakhiri dengan `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`. Sebelum `dotnet build` aplikasi, tutup Gili yang sedang jalan dengan `Gili.exe --quit` (exe terkunci selama jalan). Jangan menjalankan `scripts/publish.ps1`: pengguna ingin Gili baru dipasang setelah semua fase selesai, jadi pemeriksaan memakai build dev.

---

### Task 1: Pegas

**Files:**
- Create: `src/Gili.Core/Motion/Spring.cs`
- Test: `tests/Gili.Core.Tests/Motion/SpringTests.cs`

**Interfaces:**
- Produces: `readonly record struct SpringParameters(double DampingRatio, double Response)` dengan preset `Open`, `Close`, `Resize`, `Hover`, `Pop`; `sealed class Spring(double value)` dengan `double Value`, `double Velocity`, `double Target { get; set; }`, `SpringParameters Parameters { get; set; }`, `bool IsAtRest`, `void Step(double seconds)`, `void SnapTo(double value)`. Namespace `Gili.Core.Motion`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Motion/SpringTests.cs`:

```csharp
using Gili.Core.Motion;

namespace Gili.Core.Tests.Motion;

public class SpringTests
{
    private const double Frame = 1.0 / 60;

    private static double Run(Spring spring, double seconds, Action<double>? onFrame = null)
    {
        for (var t = 0.0; t < seconds; t += Frame)
        {
            spring.Step(Frame);
            onFrame?.Invoke(spring.Value);
        }
        return spring.Value;
    }

    [Fact]
    public void Settles_exactly_on_the_target()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        Run(spring, 3);
        Assert.Equal(100, spring.Value);
        Assert.True(spring.IsAtRest);
    }

    [Fact]
    public void The_opening_spring_overshoots_a_little()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        var max = 0.0;
        Run(spring, 2, v => max = Math.Max(max, v));
        Assert.InRange(max, 100.1, 103);
    }

    [Fact]
    public void The_closing_spring_never_overshoots()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Close, Target = 100 };
        var max = 0.0;
        Run(spring, 2, v => max = Math.Max(max, v));
        Assert.True(max <= 100.0001, $"max was {max}");
    }

    [Fact]
    public void The_closing_spring_is_within_one_percent_after_half_a_second()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Close, Target = 100 };
        Assert.InRange(Run(spring, 0.5), 99, 100);
    }

    [Fact]
    public void Retargeting_keeps_the_current_value_and_velocity()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        Run(spring, 0.1);
        var (value, velocity) = (spring.Value, spring.Velocity);

        spring.Target = 50;

        Assert.Equal(value, spring.Value);
        Assert.Equal(velocity, spring.Velocity);
        Assert.Equal(50, Run(spring, 3));
    }

    [Fact]
    public void SnapTo_jumps_without_motion()
    {
        var spring = new Spring(0) { Target = 100 };
        spring.SnapTo(40);
        Assert.Equal(40, spring.Value);
        Assert.Equal(40, spring.Target);
        Assert.True(spring.IsAtRest);
    }

    [Fact]
    public void A_long_stalled_frame_does_not_fling_the_value()
    {
        var spring = new Spring(0) { Parameters = SpringParameters.Open, Target = 100 };
        spring.Step(5);
        Assert.InRange(spring.Value, 0, 110);
    }
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~SpringTests"`
Expected: build gagal, namespace `Gili.Core.Motion` tidak ada.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Motion/Spring.cs`:

```csharp
namespace Gili.Core.Motion;

/// <summary>A spring as Apple's motion guidelines describe it (PRD 8.3): damping ratio (1 = no bounce) and response in seconds.</summary>
public readonly record struct SpringParameters(double DampingRatio, double Response)
{
    /// <summary>Compact to expanded, card or drop zone, and resizing while floating: a slight Dynamic Island bounce.</summary>
    public static readonly SpringParameters Open = new(0.82, 0.42);

    /// <summary>Floating back to compact, without bounce.</summary>
    public static readonly SpringParameters Close = new(1.0, 0.32);

    /// <summary>Compact ↔ wide, slot content changes and bar or island height changes.</summary>
    public static readonly SpringParameters Resize = new(0.85, 0.30);

    public static readonly SpringParameters Hover = new(0.8, 0.30);

    /// <summary>Activity bubbles and privacy dots appearing (scale 0 → 1).</summary>
    public static readonly SpringParameters Pop = new(0.7, 0.32);
}

/// <summary>
/// A damped spring stepped once per frame. Changing <see cref="Target"/> keeps the value and velocity, so every
/// transition starts from what is on screen and can be interrupted (PRD 8.3).
/// </summary>
public sealed class Spring(double value)
{
    private const double SubStep = 1.0 / 480;
    private const double MaxFrame = 0.1;
    private const double RestDistance = 0.01;
    private const double RestVelocity = 0.05;

    public double Value { get; private set; } = value;
    public double Velocity { get; private set; }
    public double Target { get; set; } = value;
    public SpringParameters Parameters { get; set; } = SpringParameters.Resize;
    public bool IsAtRest => Value == Target && Velocity == 0;

    public void SnapTo(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    public void Step(double seconds)
    {
        if (IsAtRest) return;
        var stiffness = Math.Pow(2 * Math.PI / Parameters.Response, 2);
        var damping = 4 * Math.PI * Parameters.DampingRatio / Parameters.Response;
        // A stalled frame (debugger, resume) only advances a tenth of a second instead of flinging the value.
        for (var left = Math.Min(seconds, MaxFrame); left > 0; left -= SubStep)
        {
            var dt = Math.Min(left, SubStep);
            Velocity += (-stiffness * (Value - Target) - damping * Velocity) * dt;
            Value += Velocity * dt;
        }
        if (Math.Abs(Value - Target) < RestDistance && Math.Abs(Velocity) < RestVelocity)
        {
            Value = Target;
            Velocity = 0;
        }
    }
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~SpringTests"`
Expected: PASS, 7 test.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Motion tests/Gili.Core.Tests/Motion
git commit -m "feat(core): interruptible spring with PRD motion presets" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 2: Model pulau dan `IslandCoordinator`

**Files:**
- Create: `src/Gili.Core/Island/IslandModels.cs`, `src/Gili.Core/Island/IslandCoordinator.cs`
- Test: `tests/Gili.Core.Tests/Island/IslandCoordinatorTests.cs`

**Interfaces:**
- Produces (namespace `Gili.Core.Island`):
  - `enum IslandMode { Hidden, Compact, Wide, Expanded, Card, DropZone }`
  - `enum IslandPage { Media, Timer, Alarms, Calendar, Todo, Notes, Shelf, Claude, System }`
  - `enum ActivityKind { ClipboardSnippet, ClaudeAttention, FileOperation, Timer, Stopwatch, ClaudeWorking, Media, Charging }` (urutan = peringkat)
  - `enum WideIcon { None, Charging, OnBattery }`, `sealed record WideEvent(string Key, WideIcon Icon, string Text, string? Value, TimeSpan Duration)`
  - `enum CardPriority { Urgent = 1, Reminder = 2 }`, `sealed record IslandCard(string Id, CardPriority Priority, TimeSpan Timeout)`
  - `sealed record IslandView(IslandMode Mode, ActivityKind? Slot, ActivityKind? Bubble, WideEvent? Wide, IslandCard? Card, IslandPage Page)` dengan `bool IsFloating`
  - `IslandCoordinator(Func<DateTimeOffset> now)`: `IslandView View`, `event Action<IslandView>? Changed`, `DateTimeOffset? NextDeadline`, `SetActivity(ActivityKind, bool)`, `ShowWide(WideEvent)`, `ShowCard(IslandCard)`, `DismissCard(string)`, `SetDropZone(bool)`, `SetFullscreen(bool)`, `Open(IslandPage)`, `Close()`, `Toggle(IslandPage)`, `OpenFor(ActivityKind?)`, `Tick()`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Island/IslandCoordinatorTests.cs`:

```csharp
using Gili.Core.Island;

namespace Gili.Core.Tests.Island;

public class IslandCoordinatorTests
{
    private readonly IslandCoordinator _island;
    private DateTimeOffset _now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    public IslandCoordinatorTests() => _island = new IslandCoordinator(() => _now);

    private void Advance(double seconds)
    {
        _now = _now.AddSeconds(seconds);
        _island.Tick();
    }

    private static WideEvent Wide(string key, double seconds = 3) =>
        new(key, WideIcon.None, key, null, TimeSpan.FromSeconds(seconds));

    private static IslandCard Urgent(string id) => new(id, CardPriority.Urgent, TimeSpan.Zero);

    private static IslandCard Reminder(string id, double seconds = 10) =>
        new(id, CardPriority.Reminder, TimeSpan.FromSeconds(seconds));

    [Fact]
    public void An_idle_island_is_compact_with_the_clock()
    {
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
        Assert.Null(_island.View.Slot);
        Assert.Null(_island.View.Bubble);
    }

    [Fact]
    public void The_best_activity_takes_the_slot_and_the_next_one_the_bubble()
    {
        _island.SetActivity(ActivityKind.Charging, true);
        _island.SetActivity(ActivityKind.Media, true);
        Assert.Equal(ActivityKind.Media, _island.View.Slot);
        Assert.Equal(ActivityKind.Charging, _island.View.Bubble);

        _island.SetActivity(ActivityKind.Timer, true);
        Assert.Equal(ActivityKind.Timer, _island.View.Slot);
        Assert.Equal(ActivityKind.Media, _island.View.Bubble);
    }

    [Fact]
    public void The_clock_returns_when_the_last_activity_ends()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Media, false);
        Assert.Null(_island.View.Slot);
    }

    [Fact]
    public void A_wide_notice_shows_for_its_duration()
    {
        _island.ShowWide(Wide("charger", 3));
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Assert.Equal("charger", _island.View.Wide?.Key);

        Advance(2.9);
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
        Assert.Null(_island.View.Wide);
    }

    [Fact]
    public void Wide_notices_queue_and_a_queued_one_with_the_same_key_is_replaced()
    {
        _island.ShowWide(Wide("caps", 1.5));
        _island.ShowWide(Wide("charger", 3));
        _island.ShowWide(Wide("charger", 2) with { Text = "On battery" });

        Advance(1.5);
        Assert.Equal("On battery", _island.View.Wide?.Text);
        Advance(2);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void A_notice_with_the_key_on_screen_replaces_it_and_restarts_its_time()
    {
        _island.ShowWide(Wide("caps", 1.5));
        Advance(1);
        _island.ShowWide(Wide("caps", 1.5) with { Text = "Caps Lock off" });
        Advance(1);
        Assert.Equal("Caps Lock off", _island.View.Wide?.Text);
    }

    [Fact]
    public void The_bubble_only_shows_while_compact()
    {
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Charging, true);
        _island.ShowWide(Wide("caps"));
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Assert.Null(_island.View.Bubble);
    }

    [Fact]
    public void An_open_page_holds_wide_notices_back_until_it_closes()
    {
        _island.Open(IslandPage.Media);
        _island.ShowWide(Wide("caps", 1.5));
        Advance(5);
        Assert.Equal(IslandMode.Expanded, _island.View.Mode);

        _island.Close();
        Assert.Equal(IslandMode.Wide, _island.View.Mode);
        Advance(1.5);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void An_urgent_card_replaces_the_open_page_which_returns_after_it_is_answered()
    {
        _island.Open(IslandPage.Media);
        _island.ShowCard(Urgent("alarm"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);
        Assert.Equal("alarm", _island.View.Card?.Id);

        Advance(600);
        Assert.Equal(IslandMode.Card, _island.View.Mode);

        _island.DismissCard("alarm");
        Assert.Equal(IslandMode.Expanded, _island.View.Mode);
        Assert.Equal(IslandPage.Media, _island.View.Page);
    }

    [Fact]
    public void A_reminder_card_closes_itself_after_its_timeout()
    {
        _island.ShowCard(Reminder("todo", 10));
        Advance(9.9);
        Assert.Equal(IslandMode.Card, _island.View.Mode);
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void A_reminder_waits_behind_an_urgent_card_and_then_gets_its_full_time()
    {
        _island.ShowCard(Urgent("alarm"));
        _island.ShowCard(Reminder("todo", 10));
        Assert.Equal("alarm", _island.View.Card?.Id);

        Advance(30);
        _island.DismissCard("alarm");
        Assert.Equal("todo", _island.View.Card?.Id);
        Advance(9.9);
        Assert.Equal("todo", _island.View.Card?.Id);
        Advance(0.1);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Showing_the_same_card_again_restarts_its_timeout()
    {
        _island.ShowCard(Reminder("claude", 8));
        Advance(6);
        _island.ShowCard(Reminder("claude", 8));
        Advance(6);
        Assert.Equal(IslandMode.Card, _island.View.Mode);
    }

    [Fact]
    public void The_drop_zone_sits_above_the_open_page_and_below_cards()
    {
        _island.Open(IslandPage.Media);
        _island.SetDropZone(true);
        Assert.Equal(IslandMode.DropZone, _island.View.Mode);

        _island.ShowCard(Urgent("alarm"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);

        _island.DismissCard("alarm");
        _island.SetDropZone(false);
        Assert.Equal(IslandMode.Expanded, _island.View.Mode);
    }

    [Fact]
    public void Fullscreen_hides_the_island_and_closes_the_open_page()
    {
        _island.Open(IslandPage.Media);
        _island.SetFullscreen(true);
        Assert.Equal(IslandMode.Hidden, _island.View.Mode);

        _island.SetFullscreen(false);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void In_fullscreen_only_urgent_cards_show_and_everything_else_is_dropped()
    {
        _island.ShowWide(Wide("caps"));
        _island.SetFullscreen(true);
        _island.ShowWide(Wide("charger"));
        _island.ShowCard(Reminder("todo"));
        Assert.Equal(IslandMode.Hidden, _island.View.Mode);

        _island.ShowCard(Urgent("alarm"));
        Assert.Equal(IslandMode.Card, _island.View.Mode);

        _island.DismissCard("alarm");
        _island.SetFullscreen(false);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Toggle_opens_switches_and_closes()
    {
        _island.Toggle(IslandPage.Timer);
        Assert.Equal((IslandMode.Expanded, IslandPage.Timer), (_island.View.Mode, _island.View.Page));
        _island.Toggle(IslandPage.Notes);
        Assert.Equal(IslandPage.Notes, _island.View.Page);
        _island.Toggle(IslandPage.Notes);
        Assert.Equal(IslandMode.Compact, _island.View.Mode);
    }

    [Fact]
    public void Clicking_the_island_opens_the_slot_activity_page_or_the_last_page()
    {
        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Media, _island.View.Page);
        _island.Open(IslandPage.Calendar);
        _island.Close();

        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Calendar, _island.View.Page);
        _island.Close();

        _island.SetActivity(ActivityKind.Media, true);
        _island.OpenFor(_island.View.Slot);
        Assert.Equal(IslandPage.Media, _island.View.Page);
    }

    [Fact]
    public void NextDeadline_is_the_soonest_timed_end()
    {
        Assert.Null(_island.NextDeadline);
        _island.ShowCard(Reminder("todo", 10));
        Assert.Equal(_now.AddSeconds(10), _island.NextDeadline);
    }

    [Fact]
    public void Changed_fires_only_when_the_view_changes()
    {
        var count = 0;
        _island.Changed += _ => count++;
        _island.SetActivity(ActivityKind.Media, true);
        _island.SetActivity(ActivityKind.Media, true);
        _island.Tick();
        Assert.Equal(1, count);
    }
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandCoordinatorTests"`
Expected: build gagal, namespace `Gili.Core.Island` tidak ada.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Island/IslandModels.cs`:

```csharp
namespace Gili.Core.Island;

public enum IslandMode { Hidden, Compact, Wide, Expanded, Card, DropZone }

/// <summary>Pages of the expanded island, in menu order (PRD 6.3).</summary>
public enum IslandPage { Media, Timer, Alarms, Calendar, Todo, Notes, Shelf, Claude, System }

/// <summary>Activities that can hold the compact slot, best first (PRD 6.3 activity order).</summary>
public enum ActivityKind { ClipboardSnippet, ClaudeAttention, FileOperation, Timer, Stopwatch, ClaudeWorking, Media, Charging }

public enum WideIcon { None, Charging, OnBattery }

/// <summary>A short notice in the wide island, shown as "Text · Value". A newer event with the same key replaces it.</summary>
public sealed record WideEvent(string Key, WideIcon Icon, string Text, string? Value, TimeSpan Duration);

public enum CardPriority { Urgent = 1, Reminder = 2 }

/// <summary>A notification card. Urgent cards stay until answered; reminders close after <paramref name="Timeout"/>.</summary>
public sealed record IslandCard(string Id, CardPriority Priority, TimeSpan Timeout);

/// <summary>What the island shows right now. <see cref="Bubble"/> is only set while compact.</summary>
public sealed record IslandView(
    IslandMode Mode,
    ActivityKind? Slot,
    ActivityKind? Bubble,
    WideEvent? Wide,
    IslandCard? Card,
    IslandPage Page)
{
    public bool IsFloating => Mode is IslandMode.Expanded or IslandMode.Card or IslandMode.DropZone;
}
```

`src/Gili.Core/Island/IslandCoordinator.cs`:

```csharp
namespace Gili.Core.Island;

/// <summary>
/// Decides what the island shows (PRD 6.3 "Prioritas tampilan"): cards, then the drop zone, then the page the user
/// opened, then wide notices, then running activities, then the clock. Lower priorities wait in a queue.
/// </summary>
public sealed class IslandCoordinator
{
    private readonly Func<DateTimeOffset> _now;
    private readonly HashSet<ActivityKind> _activities = [];
    private readonly List<WideEvent> _wideQueue = [];
    private readonly List<IslandCard> _cards = [];
    private WideEvent? _wide;
    private DateTimeOffset _wideUntil;
    private IslandCard? _shownCard;
    private DateTimeOffset _cardUntil;
    private bool _dropZone;
    private bool _fullscreen;
    private IslandPage? _openPage;
    private IslandPage _lastPage = IslandPage.Media;

    public IslandCoordinator(Func<DateTimeOffset> now)
    {
        _now = now;
        View = Compute();
    }

    public IslandView View { get; private set; }

    public event Action<IslandView>? Changed;

    /// <summary>When <see cref="Tick"/> must run for a wide notice or reminder card to end; null when nothing is timed.</summary>
    public DateTimeOffset? NextDeadline
    {
        get
        {
            DateTimeOffset? next = _wide is null ? null : _wideUntil;
            if (_shownCard is { Priority: CardPriority.Reminder } && (next is null || _cardUntil < next)) next = _cardUntil;
            return next;
        }
    }

    public void SetActivity(ActivityKind kind, bool active)
    {
        if (active ? _activities.Add(kind) : _activities.Remove(kind)) Update();
    }

    public void ShowWide(WideEvent wide)
    {
        if (_fullscreen) return;
        _wideQueue.RemoveAll(w => w.Key == wide.Key);
        if (_wide?.Key == wide.Key)
        {
            _wide = wide;
            _wideUntil = _now() + wide.Duration;
        }
        else
        {
            _wideQueue.Add(wide);
        }
        Update();
    }

    public void ShowCard(IslandCard card)
    {
        if (_fullscreen && card.Priority == CardPriority.Reminder) return;
        var index = _cards.FindIndex(c => c.Id == card.Id);
        if (index >= 0)
        {
            _cards[index] = card;
            // New content for the card on screen restarts its time (PRD 6.3: a new question restarts the 8 s).
            if (_shownCard?.Id == card.Id) _shownCard = null;
        }
        else
        {
            _cards.Add(card);
        }
        Update();
    }

    public void DismissCard(string id)
    {
        if (_cards.RemoveAll(c => c.Id == id) > 0) Update();
    }

    public void SetDropZone(bool active)
    {
        _dropZone = active;
        Update();
    }

    /// <summary>A fullscreen app hides the bar: only urgent cards still appear; wide notices and reminders are dropped.</summary>
    public void SetFullscreen(bool fullscreen)
    {
        _fullscreen = fullscreen;
        if (fullscreen)
        {
            _openPage = null;
            _wide = null;
            _wideQueue.Clear();
            _cards.RemoveAll(c => c.Priority == CardPriority.Reminder);
        }
        Update();
    }

    public void Open(IslandPage page)
    {
        if (_fullscreen) return;
        _openPage = page;
        _lastPage = page;
        Update();
    }

    public void Close()
    {
        _openPage = null;
        Update();
    }

    /// <summary>Bar icons and slots that open a page toggle it: the page that is already open closes the island.</summary>
    public void Toggle(IslandPage page)
    {
        if (_openPage == page) Close();
        else Open(page);
    }

    /// <summary>Opens the page of an activity (slot or bubble click), or the last page viewed when it has none.</summary>
    public void OpenFor(ActivityKind? kind) => Open(PageFor(kind) ?? _lastPage);

    public void Tick() => Update();

    private static IslandPage? PageFor(ActivityKind? kind) => kind switch
    {
        ActivityKind.Media => IslandPage.Media,
        ActivityKind.Timer or ActivityKind.Stopwatch => IslandPage.Timer,
        ActivityKind.ClaudeAttention or ActivityKind.ClaudeWorking => IslandPage.Claude,
        _ => null,
    };

    private void Update()
    {
        var view = Compute();
        if (view == View) return;
        View = view;
        Changed?.Invoke(view);
    }

    private IslandView Compute()
    {
        var now = _now();
        if (_wide is not null && now >= _wideUntil) _wide = null;
        if (_shownCard is { Priority: CardPriority.Reminder } && now >= _cardUntil)
        {
            _cards.Remove(_shownCard);
            _shownCard = null;
        }

        // Cards outrank everything; within a priority the oldest goes first (OrderBy is stable).
        var card = _cards.Where(c => !_fullscreen || c.Priority == CardPriority.Urgent).OrderBy(c => c.Priority).FirstOrDefault();
        if (card != _shownCard)
        {
            _shownCard = card;
            if (card is { Priority: CardPriority.Reminder }) _cardUntil = now + card.Timeout;
        }

        IslandMode mode;
        if (_shownCard is not null) mode = IslandMode.Card;
        else if (_fullscreen) mode = IslandMode.Hidden;
        else if (_dropZone) mode = IslandMode.DropZone;
        else if (_openPage is not null) mode = IslandMode.Expanded;
        else
        {
            // A wide notice waits while anything above it is on screen; its time starts when it shows.
            if (_wide is null && _wideQueue.Count > 0)
            {
                _wide = _wideQueue[0];
                _wideQueue.RemoveAt(0);
                _wideUntil = now + _wide.Duration;
            }
            mode = _wide is null ? IslandMode.Compact : IslandMode.Wide;
        }

        var ranked = _activities.Order().ToList();
        return new IslandView(
            mode,
            ranked.Count > 0 ? ranked[0] : null,
            mode == IslandMode.Compact && ranked.Count > 1 ? ranked[1] : null,
            mode == IslandMode.Wide ? _wide : null,
            mode == IslandMode.Card ? _shownCard : null,
            _openPage ?? _lastPage);
    }
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandCoordinatorTests"`
Expected: PASS, 19 test.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Island tests/Gili.Core.Tests/Island
git commit -m "feat(core): island coordinator with display priorities and queues (F-ISL-03)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Geometri pulau dan elemen tertimpa

**Files:**
- Create: `src/Gili.Core/Island/IslandLayout.cs`
- Test: `tests/Gili.Core.Tests/Island/IslandLayoutTests.cs`

**Interfaces:**
- Consumes: `IslandMode`, `IslandPage` (Task 2).
- Produces: `readonly record struct IslandGeometry(double Width, double Height, double Top, double CornerRadius)`; `static class IslandLayout` dengan konstanta `MinCompactWidth`, `HoverGrow`, `CompactPadding`, `WidePadding`, `FloatTop`, `ScreenMargin`, `BubbleGap`, `CoverMargin`, dan `static IslandGeometry For(IslandMode mode, IslandPage page, double contentWidth, bool hovered, int barHeight, int islandHeight, double screenWidth)`, `static bool Covers(double islandLeft, double islandRight, double elementLeft, double elementRight)`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Island/IslandLayoutTests.cs`:

```csharp
using Gili.Core.Island;

namespace Gili.Core.Tests.Island;

public class IslandLayoutTests
{
    private static IslandGeometry Layout(
        IslandMode mode, double content = 120, bool hovered = false, IslandPage page = IslandPage.Media, double screen = 1536) =>
        IslandLayout.For(mode, page, content, hovered, barHeight: 32, islandHeight: 24, screenWidth: screen);

    [Fact]
    public void Compact_is_never_narrower_than_200() => Assert.Equal(200, Layout(IslandMode.Compact, content: 100).Width);

    [Fact]
    public void Compact_fits_its_content_plus_28() => Assert.Equal(328, Layout(IslandMode.Compact, content: 300).Width);

    [Fact]
    public void Hovering_compact_adds_16() => Assert.Equal(216, Layout(IslandMode.Compact, content: 100, hovered: true).Width);

    [Fact]
    public void Compact_is_centred_in_the_bar_with_round_ends() =>
        Assert.Equal(new IslandGeometry(200, 24, 4, 12), Layout(IslandMode.Compact));

    [Fact]
    public void Wide_adds_32_and_ignores_hover()
    {
        Assert.Equal(232, Layout(IslandMode.Wide, content: 200, hovered: true).Width);
        Assert.Equal(200, Layout(IslandMode.Wide, content: 100).Width);
    }

    [Fact]
    public void Expanded_floats_8_below_the_top() =>
        Assert.Equal(new IslandGeometry(680, 256, 8, 32), Layout(IslandMode.Expanded));

    [Fact]
    public void The_notes_page_makes_the_island_larger() =>
        Assert.Equal(new IslandGeometry(960, 500, 8, 32), Layout(IslandMode.Expanded, page: IslandPage.Notes));

    [Fact]
    public void Cards_and_the_drop_zone_have_their_own_size()
    {
        Assert.Equal(new IslandGeometry(560, 104, 8, 28), Layout(IslandMode.Card));
        Assert.Equal(new IslandGeometry(420, 140, 8, 30), Layout(IslandMode.DropZone));
    }

    [Fact]
    public void A_floating_island_leaves_24_of_the_screen() =>
        Assert.Equal(576, Layout(IslandMode.Expanded, screen: 600).Width);

    [Theory]
    [InlineData(1110, 1150, true)]
    [InlineData(1117, 1150, false)]
    [InlineData(380, 421, true)]
    [InlineData(380, 419, false)]
    [InlineData(600, 700, true)]
    public void An_element_is_covered_within_8_of_the_island(double left, double right, bool covered) =>
        Assert.Equal(covered, IslandLayout.Covers(428, 1108, left, right));
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandLayoutTests"`
Expected: build gagal, `IslandLayout` tidak ditemukan.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Island/IslandLayout.cs`:

```csharp
namespace Gili.Core.Island;

public readonly record struct IslandGeometry(double Width, double Height, double Top, double CornerRadius);

/// <summary>Size and position of the island in each mode (PRD 6.3, 8.3), in DIPs relative to the top of the screen.</summary>
public static class IslandLayout
{
    public const double MinCompactWidth = 200;
    public const double HoverGrow = 16;
    public const double CompactPadding = 28;
    public const double WidePadding = 32;
    public const double FloatTop = 8;
    public const double ScreenMargin = 24;
    public const double BubbleGap = 6;
    public const double CoverMargin = 8;

    public static IslandGeometry For(
        IslandMode mode, IslandPage page, double contentWidth, bool hovered, int barHeight, int islandHeight, double screenWidth)
    {
        var top = (barHeight - islandHeight) / 2.0;
        var radius = islandHeight / 2.0;
        return mode switch
        {
            IslandMode.Wide => new(Math.Max(MinCompactWidth, contentWidth + WidePadding), islandHeight, top, radius),
            IslandMode.Expanded when page == IslandPage.Notes => Floating(960, 500, 32, screenWidth),
            IslandMode.Expanded => Floating(680, 256, 32, screenWidth),
            IslandMode.Card => Floating(560, 104, 28, screenWidth),
            IslandMode.DropZone => Floating(420, 140, 30, screenWidth),
            _ => new(Math.Max(MinCompactWidth, contentWidth + CompactPadding) + (hovered ? HoverGrow : 0), islandHeight, top, radius),
        };
    }

    /// <summary>A bar element fades while the floating island covers it, with 8 DIP to spare on each side (PRD 6.3).</summary>
    public static bool Covers(double islandLeft, double islandRight, double elementLeft, double elementRight) =>
        elementRight > islandLeft - CoverMargin && elementLeft < islandRight + CoverMargin;

    private static IslandGeometry Floating(double width, double height, double radius, double screenWidth) =>
        new(Math.Min(width, screenWidth - ScreenMargin), height, FloatTop, radius);
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~IslandLayoutTests"`
Expected: PASS, 14 test.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Island tests/Gili.Core.Tests/Island
git commit -m "feat(core): island geometry per mode and covered-element rule (F-ISL-02)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Model dan aturan media

**Files:**
- Create: `src/Gili.Core/Media/MediaState.cs`, `src/Gili.Core/Media/MediaRules.cs`, `src/Gili.Core/Media/MediaPresence.cs`
- Test: `tests/Gili.Core.Tests/Media/MediaRulesTests.cs`, `tests/Gili.Core.Tests/Media/MediaPresenceTests.cs`

**Interfaces:**
- Produces (namespace `Gili.Core.Media`):
  - `enum MediaKind { Music, Video }`, `enum PlaybackType { Unknown, Music, Video, Image }`
  - `sealed record MediaState(string Title, string Artist, string AppId, MediaKind Kind, bool IsPlaying, TimeSpan Position, TimeSpan Duration, DateTimeOffset PositionUpdatedAt, bool CanSeek, bool CanSkip, byte[]? Cover)` dengan `TimeSpan PositionAt(DateTimeOffset now)` dan `bool HasTimeline`
  - `static class MediaRules`: `MediaKind Classify(PlaybackType type, string appId)`, `string AppName(string appId)`, `string LevelColor(string appId)`, `string? VlcTitle(string windowTitle)`, `string SlotTitle(MediaState media)`, `string Truncate(string text, int max)`, `string Time(TimeSpan time)`, `string Remaining(TimeSpan position, TimeSpan duration)`, `double Progress(TimeSpan position, TimeSpan duration)`
  - `sealed class MediaPresence` dengan `static readonly TimeSpan PausedGrace` dan `bool IsActive(MediaState? media, DateTimeOffset now)`

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Media/MediaRulesTests.cs`:

```csharp
using Gili.Core.Media;

namespace Gili.Core.Tests.Media;

public class MediaRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private static MediaState Song(string title = "Blinding Lights", string artist = "The Weeknd", bool playing = true) =>
        new(title, artist, "Spotify.exe", MediaKind.Music, playing, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(200), T0,
            CanSeek: true, CanSkip: true, Cover: null);

    [Theory]
    [InlineData(PlaybackType.Video, "Chrome", MediaKind.Video)]
    [InlineData(PlaybackType.Music, "vlc.exe", MediaKind.Music)]
    [InlineData(PlaybackType.Unknown, "vlc.exe", MediaKind.Video)]
    [InlineData(PlaybackType.Unknown, "Microsoft.ZuneVideo_8wekyb3d8bbwe!Microsoft.ZuneVideo", MediaKind.Video)]
    [InlineData(PlaybackType.Unknown, "Spotify.exe", MediaKind.Music)]
    public void Classify_trusts_the_playback_type_then_known_video_apps(PlaybackType type, string appId, MediaKind expected) =>
        Assert.Equal(expected, MediaRules.Classify(type, appId));

    [Theory]
    [InlineData("Spotify.exe", "Spotify")]
    [InlineData("Chrome", "Google Chrome")]
    [InlineData("MSEdge", "Microsoft Edge")]
    [InlineData("Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic", "Media Player")]
    [InlineData("Contoso.Player_abc123!App", "App")]
    [InlineData("foobar2000.exe", "Foobar2000")]
    public void AppName_maps_known_apps_and_falls_back_to_the_id(string appId, string expected) =>
        Assert.Equal(expected, MediaRules.AppName(appId));

    [Theory]
    [InlineData("Spotify.exe", "#1DB954")]
    [InlineData("Chrome", "#30D158")]
    public void LevelColor_uses_the_brand_for_spotify_and_the_accent_otherwise(string appId, string expected) =>
        Assert.Equal(expected, MediaRules.LevelColor(appId));

    [Theory]
    [InlineData("lecture-03.mkv - VLC media player", "lecture-03.mkv")]
    [InlineData("VLC media player", null)]
    public void VlcTitle_strips_the_player_name(string windowTitle, string? expected) =>
        Assert.Equal(expected, MediaRules.VlcTitle(windowTitle));

    [Fact]
    public void Music_slot_shows_title_and_artist() =>
        Assert.Equal("Blinding Lights – The Weeknd", MediaRules.SlotTitle(Song()));

    [Fact]
    public void Music_slot_without_artist_shows_the_title() =>
        Assert.Equal("Blinding Lights", MediaRules.SlotTitle(Song(artist: "")));

    [Fact]
    public void Music_slot_is_cut_at_36_characters()
    {
        var text = MediaRules.SlotTitle(Song(title: "A Very Long Song Title That Keeps Going", artist: "Someone"));
        Assert.Equal(36, text.Length);
        Assert.EndsWith("…", text);
    }

    [Fact]
    public void Video_slot_is_cut_at_32_characters()
    {
        var video = Song(title: "Lecture 3: Linear Algebra and Its Applications") with { Kind = MediaKind.Video };
        Assert.Equal("Lecture 3: Linear Algebra and I…", MediaRules.SlotTitle(video));
    }

    [Fact]
    public void Truncate_drops_trailing_spaces_before_the_ellipsis() =>
        Assert.Equal("Hello…", MediaRules.Truncate("Hello  world", 8));

    [Theory]
    [InlineData(7, "0:07")]
    [InlineData(187, "3:07")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    public void Time_is_minutes_and_seconds_with_hours_when_needed(int seconds, string expected) =>
        Assert.Equal(expected, MediaRules.Time(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Remaining_counts_down_from_the_end() =>
        Assert.Equal("-2:10", MediaRules.Remaining(TimeSpan.FromSeconds(50), TimeSpan.FromSeconds(180)));

    [Theory]
    [InlineData(30, 120, 0.25)]
    [InlineData(30, 0, 0)]
    [InlineData(200, 120, 1)]
    public void Progress_is_a_fraction_of_the_duration(int position, int duration, double expected) =>
        Assert.Equal(expected, MediaRules.Progress(TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration)));

    [Fact]
    public void A_playing_position_moves_on_with_time() =>
        Assert.Equal(TimeSpan.FromSeconds(40), Song().PositionAt(T0.AddSeconds(10)));

    [Fact]
    public void A_playing_position_stops_at_the_end() =>
        Assert.Equal(TimeSpan.FromSeconds(200), Song().PositionAt(T0.AddMinutes(10)));

    [Fact]
    public void A_paused_position_stays_put() =>
        Assert.Equal(TimeSpan.FromSeconds(30), Song(playing: false).PositionAt(T0.AddSeconds(10)));
}
```

`tests/Gili.Core.Tests/Media/MediaPresenceTests.cs`:

```csharp
using Gili.Core.Media;

namespace Gili.Core.Tests.Media;

public class MediaPresenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
    private readonly MediaPresence _presence = new();

    private static MediaState Song(bool playing) =>
        new("Song", "Artist", "Spotify.exe", MediaKind.Music, playing, TimeSpan.Zero, TimeSpan.FromMinutes(3), T0, true, true, null);

    [Fact]
    public void Playing_media_holds_the_slot() => Assert.True(_presence.IsActive(Song(playing: true), T0));

    [Fact]
    public void Media_paused_after_playing_keeps_the_slot_for_two_minutes()
    {
        _presence.IsActive(Song(playing: true), T0);
        Assert.True(_presence.IsActive(Song(playing: false), T0.AddSeconds(1)));
        Assert.True(_presence.IsActive(Song(playing: false), T0.AddSeconds(121)));
        Assert.False(_presence.IsActive(Song(playing: false), T0.AddSeconds(122)));
    }

    [Fact]
    public void Media_found_paused_does_not_take_the_slot() =>
        Assert.False(_presence.IsActive(Song(playing: false), T0));

    [Fact]
    public void Playing_again_resets_the_grace_period()
    {
        _presence.IsActive(Song(playing: true), T0);
        _presence.IsActive(Song(playing: false), T0.AddSeconds(1));
        _presence.IsActive(Song(playing: true), T0.AddSeconds(100));
        Assert.True(_presence.IsActive(Song(playing: false), T0.AddSeconds(200)));
    }

    [Fact]
    public void Losing_the_session_forgets_that_it_played()
    {
        _presence.IsActive(Song(playing: true), T0);
        Assert.False(_presence.IsActive(null, T0.AddSeconds(1)));
        Assert.False(_presence.IsActive(Song(playing: false), T0.AddSeconds(2)));
    }
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Gili.Core.Tests.Media"`
Expected: build gagal, namespace `Gili.Core.Media` tidak ada.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Media/MediaState.cs`:

```csharp
namespace Gili.Core.Media;

public enum MediaKind { Music, Video }

/// <summary>The playback type a media session reports to Windows (PRD 9.2).</summary>
public enum PlaybackType { Unknown, Music, Video, Image }

/// <summary>The media session the island shows. Windows reports the position only when it changes, at <paramref name="PositionUpdatedAt"/>.</summary>
public sealed record MediaState(
    string Title,
    string Artist,
    string AppId,
    MediaKind Kind,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionUpdatedAt,
    bool CanSeek,
    bool CanSkip,
    byte[]? Cover)
{
    public bool HasTimeline => Duration > TimeSpan.Zero;

    public TimeSpan PositionAt(DateTimeOffset now)
    {
        if (!IsPlaying) return Position;
        var position = Position + (now - PositionUpdatedAt);
        return HasTimeline && position > Duration ? Duration : position;
    }
}
```

`src/Gili.Core/Media/MediaRules.cs`:

```csharp
using System.Globalization;

namespace Gili.Core.Media;

public static class MediaRules
{
    public const int MusicSlotLength = 36;
    public const int VideoSlotLength = 32;
    private const string VlcSuffix = " - VLC media player";

    private static readonly string[] VideoApps = ["vlc", "Microsoft.ZuneVideo", "mpc-hc", "PotPlayer", "mpv"];

    private static readonly (string Match, string Name)[] KnownApps =
    [
        ("Microsoft.ZuneMusic", "Media Player"),
        ("Microsoft.ZuneVideo", "Movies & TV"),
        ("spotify", "Spotify"),
        ("chrome", "Google Chrome"),
        ("msedge", "Microsoft Edge"),
        ("firefox", "Firefox"),
        ("vlc", "VLC"),
    ];

    /// <summary>Music or video from the session's playback type, else from apps known to play video (PRD 9.2).</summary>
    public static MediaKind Classify(PlaybackType type, string appId) => type switch
    {
        PlaybackType.Video => MediaKind.Video,
        PlaybackType.Music => MediaKind.Music,
        _ => VideoApps.Any(app => appId.Contains(app, StringComparison.OrdinalIgnoreCase)) ? MediaKind.Video : MediaKind.Music,
    };

    /// <summary>The source app's name from its AppUserModelID ("Spotify.exe", "Chrome", "Package_hash!App").</summary>
    public static string AppName(string appId)
    {
        foreach (var (match, name) in KnownApps)
        {
            if (appId.Contains(match, StringComparison.OrdinalIgnoreCase)) return name;
        }
        var bang = appId.LastIndexOf('!');
        var fallback = bang >= 0 ? appId[(bang + 1)..] : Path.GetFileNameWithoutExtension(appId);
        return fallback.Length == 0 ? appId : char.ToUpperInvariant(fallback[0]) + fallback[1..];
    }

    /// <summary>Colour of the music level bars: Spotify's green for Spotify, the accent green for everything else.</summary>
    public static string LevelColor(string appId) =>
        appId.Contains("spotify", StringComparison.OrdinalIgnoreCase) ? "#1DB954" : "#30D158";

    /// <summary>VLC 3 has no media session; "file.mkv - VLC media player" gives the video title (PRD 12).</summary>
    public static string? VlcTitle(string windowTitle) =>
        windowTitle.EndsWith(VlcSuffix, StringComparison.Ordinal) ? windowTitle[..^VlcSuffix.Length] : null;

    public static string SlotTitle(MediaState media) => media.Kind == MediaKind.Video
        ? Truncate(media.Title, VideoSlotLength)
        : Truncate(string.IsNullOrWhiteSpace(media.Artist) ? media.Title : $"{media.Title} – {media.Artist}", MusicSlotLength);

    public static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)].TrimEnd() + "…";

    public static string Time(TimeSpan time)
    {
        var t = time < TimeSpan.Zero ? TimeSpan.Zero : time;
        return t.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{t.Minutes}:{t.Seconds:00}");
    }

    public static string Remaining(TimeSpan position, TimeSpan duration) => "-" + Time(duration - position);

    public static double Progress(TimeSpan position, TimeSpan duration) =>
        duration > TimeSpan.Zero ? Math.Clamp(position / duration, 0, 1) : 0;
}
```

`src/Gili.Core/Media/MediaPresence.cs`:

```csharp
namespace Gili.Core.Media;

/// <summary>
/// Whether media holds the compact slot. Playing media always does. Media paused after playing keeps it for two
/// minutes so it can be resumed from the island, then the clock comes back. A session found paused never takes it.
/// </summary>
public sealed class MediaPresence
{
    public static readonly TimeSpan PausedGrace = TimeSpan.FromMinutes(2);

    private bool _played;
    private DateTimeOffset? _pausedAt;

    public bool IsActive(MediaState? media, DateTimeOffset now)
    {
        if (media is null)
        {
            _played = false;
            _pausedAt = null;
            return false;
        }
        if (media.IsPlaying)
        {
            _played = true;
            _pausedAt = null;
            return true;
        }
        if (!_played) return false;
        _pausedAt ??= now;
        return now - _pausedAt.Value <= PausedGrace;
    }
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Gili.Core.Tests.Media"`
Expected: PASS semua.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Media tests/Gili.Core.Tests/Media
git commit -m "feat(core): media state, slot text, app names and paused grace (F-ISL-04, F-ISL-05)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Aturan daya

**Files:**
- Create: `src/Gili.Core/Power/PowerRules.cs`
- Test: `tests/Gili.Core.Tests/Power/PowerRulesTests.cs`

**Interfaces:**
- Consumes: `WideEvent`, `WideIcon` (Task 2).
- Produces (namespace `Gili.Core.Power`): `sealed record PowerSnapshot(bool HasBattery, bool PluggedIn, bool Charging, int Percent)`; `static class PowerRules` dengan `WideEvent? WideFor(PowerSnapshot? previous, PowerSnapshot current)` dan `bool IsCharging(PowerSnapshot power)`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Power/PowerRulesTests.cs`:

```csharp
using Gili.Core.Island;
using Gili.Core.Power;

namespace Gili.Core.Tests.Power;

public class PowerRulesTests
{
    private static PowerSnapshot OnBattery(int percent = 63) => new(HasBattery: true, PluggedIn: false, Charging: false, percent);
    private static PowerSnapshot Charging(int percent = 64) => new(HasBattery: true, PluggedIn: true, Charging: true, percent);

    [Fact]
    public void The_first_reading_shows_nothing() => Assert.Null(PowerRules.WideFor(null, Charging()));

    [Fact]
    public void Plugging_in_shows_charging_for_3_seconds()
    {
        var wide = PowerRules.WideFor(OnBattery(), Charging(64));
        Assert.Equal(new WideEvent("charger", WideIcon.Charging, "Charging", "64%", TimeSpan.FromSeconds(3)), wide);
    }

    [Fact]
    public void Unplugging_shows_on_battery_for_2_seconds()
    {
        var wide = PowerRules.WideFor(Charging(), OnBattery(63));
        Assert.Equal(new WideEvent("charger", WideIcon.OnBattery, "On battery", "63%", TimeSpan.FromSeconds(2)), wide);
    }

    [Fact]
    public void A_percentage_change_alone_shows_nothing() => Assert.Null(PowerRules.WideFor(Charging(64), Charging(65)));

    [Fact]
    public void A_desktop_without_battery_shows_nothing()
    {
        var desktop = new PowerSnapshot(HasBattery: false, PluggedIn: true, Charging: false, 100);
        Assert.Null(PowerRules.WideFor(desktop with { PluggedIn = false }, desktop));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void Only_a_charging_battery_is_an_activity(bool hasBattery, bool charging, bool expected) =>
        Assert.Equal(expected, PowerRules.IsCharging(new PowerSnapshot(hasBattery, PluggedIn: true, charging, 80)));
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~PowerRulesTests"`
Expected: build gagal, namespace `Gili.Core.Power` tidak ada.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Power/PowerRules.cs`:

```csharp
using System.Globalization;
using Gili.Core.Island;

namespace Gili.Core.Power;

public sealed record PowerSnapshot(bool HasBattery, bool PluggedIn, bool Charging, int Percent);

public static class PowerRules
{
    /// <summary>The wide notice for plugging the charger in or out (PRD 6.3); nothing on the first reading or without a battery.</summary>
    public static WideEvent? WideFor(PowerSnapshot? previous, PowerSnapshot current)
    {
        if (previous is null || !current.HasBattery || previous.PluggedIn == current.PluggedIn) return null;
        var percent = current.Percent.ToString(CultureInfo.InvariantCulture) + "%";
        return current.PluggedIn
            ? new WideEvent("charger", WideIcon.Charging, "Charging", percent, TimeSpan.FromSeconds(3))
            : new WideEvent("charger", WideIcon.OnBattery, "On battery", percent, TimeSpan.FromSeconds(2));
    }

    /// <summary>The slot shows a bolt and the percentage while the battery is charging (PRD 6.3, activity 8).</summary>
    public static bool IsCharging(PowerSnapshot power) => power.HasBattery && power.Charging;
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests`
Expected: PASS semua.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core/Power tests/Gili.Core.Tests/Power
git commit -m "feat(core): charger notices and charging activity rule (F-ISL-06)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Provider Platform: media session, VLC, daya, fokus jendela

Kode Windows tanpa unit test; diperiksa di Task 9–11 saat pulau sudah menampilkannya.

**Files:**
- Create: `src/Gili.Platform/Media/MediaSessionMonitor.cs`, `src/Gili.Platform/Media/VlcWindow.cs`, `src/Gili.Platform/Power/PowerMonitor.cs`, `src/Gili.Platform/Shell/ForegroundWindow.cs`
- Modify: `src/Gili.Platform/Interop/Win32.cs`, `src/Gili.Platform/Interop/WindowMessages.cs`, `src/Gili.Platform/Shell/WindowStyles.cs`

**Interfaces:**
- Consumes: `MediaState`, `MediaRules`, `PlaybackType` (Task 4); `PowerSnapshot` (Task 5).
- Produces:
  - `Gili.Platform.Media.MediaSessionMonitor : IDisposable`: `Task StartAsync()`, `event Action<MediaState?>? Changed` dan `event Action<Exception>? Failed` (keduanya di thread latar), `Task TogglePlayPauseAsync()`, `Task NextAsync()`, `Task PreviousAsync()`, `Task SeekAsync(TimeSpan position)`.
  - `Gili.Platform.Media.VlcWindow.ReadTitle()` → `string?`.
  - `Gili.Platform.Power.PowerMonitor : IDisposable`: `static PowerSnapshot Read()`, `event Action<PowerSnapshot>? Changed` (thread latar).
  - `Gili.Platform.Shell.ForegroundWindow`: `static IntPtr Get()`, `static void Restore(IntPtr hwnd)`.
  - `WindowStyles.SetNoActivate(IntPtr hwnd, bool noActivate)`; `WindowMessages.WM_MOUSEHWHEEL`.

- [ ] **Step 1: Interop dan gaya jendela**

Di `src/Gili.Platform/Interop/Win32.cs`, tambahkan di bawah deklarasi `GetForegroundWindow`:

```csharp
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
```

Di `src/Gili.Platform/Interop/WindowMessages.cs`, tambahkan di bawah `WM_POWERBROADCAST`:

```csharp
    public const int WM_MOUSEHWHEEL = 0x020E;
```

Di `src/Gili.Platform/Shell/WindowStyles.cs`, tambahkan method:

```csharp
    /// <summary>The island takes focus only while it is open, so Esc and the arrow keys reach it.</summary>
    public static void SetNoActivate(IntPtr hwnd, bool noActivate)
    {
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        style = noActivate ? style | Win32.WS_EX_NOACTIVATE : style & ~Win32.WS_EX_NOACTIVATE;
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, style);
    }
```

`src/Gili.Platform/Shell/ForegroundWindow.cs`:

```csharp
using Gili.Platform.Interop;

namespace Gili.Platform.Shell;

/// <summary>Remembers which window had focus before the island opened, and gives it back when the island closes.</summary>
public static class ForegroundWindow
{
    public static IntPtr Get() => Win32.GetForegroundWindow();

    public static void Restore(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero) Win32.SetForegroundWindow(hwnd);
    }
}
```

- [ ] **Step 2: `PowerMonitor`**

`src/Gili.Platform/Power/PowerMonitor.cs`:

```csharp
using Gili.Core.Power;
using Windows.System.Power;

namespace Gili.Platform.Power;

/// <summary>Battery and charger state from PowerManager (PRD 9.2).</summary>
public sealed class PowerMonitor : IDisposable
{
    public PowerMonitor()
    {
        PowerManager.BatteryStatusChanged += OnChanged;
        PowerManager.PowerSupplyStatusChanged += OnChanged;
        PowerManager.RemainingChargePercentChanged += OnChanged;
    }

    /// <summary>Raised on a background thread.</summary>
    public event Action<PowerSnapshot>? Changed;

    public static PowerSnapshot Read() => new(
        HasBattery: PowerManager.BatteryStatus != BatteryStatus.NotPresent,
        PluggedIn: PowerManager.PowerSupplyStatus != PowerSupplyStatus.NotPresent,
        Charging: PowerManager.BatteryStatus == BatteryStatus.Charging,
        Percent: PowerManager.RemainingChargePercent);

    public void Dispose()
    {
        PowerManager.BatteryStatusChanged -= OnChanged;
        PowerManager.PowerSupplyStatusChanged -= OnChanged;
        PowerManager.RemainingChargePercentChanged -= OnChanged;
    }

    private void OnChanged(object? sender, object e) => Changed?.Invoke(Read());
}
```

- [ ] **Step 3: `MediaSessionMonitor` dan `VlcWindow`**

`src/Gili.Platform/Media/MediaSessionMonitor.cs`:

```csharp
using Gili.Core.Media;
using Windows.Media;
using Windows.Media.Control;
using Windows.Storage.Streams;
using Session = Windows.Media.Control.GlobalSystemMediaTransportControlsSession;

namespace Gili.Platform.Media;

/// <summary>
/// Follows Windows' media sessions (Spotify, browsers, Media Player…) and reports the one to show (PRD 9.2, 12):
/// a playing session before a paused one, and among those the one whose track or playback changed last.
/// </summary>
public sealed class MediaSessionMonitor : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Session> _hooked = [];
    private readonly Dictionary<string, DateTimeOffset> _lastChange = [];
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private Session? _current;
    private (string Key, byte[] Cover)? _cover;
    private volatile bool _disposed;

    /// <summary>The session to show, or null when there is none. Raised on a background thread.</summary>
    public event Action<MediaState?>? Changed;

    /// <summary>Raised on a background thread when Windows refuses a request; the bar keeps running.</summary>
    public event Action<Exception>? Failed;

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += (_, _) => _ = RefreshAsync(null);
        await RefreshAsync(null);
    }

    public Task TogglePlayPauseAsync() => RunAsync(s => s.TryTogglePlayPauseAsync());

    public Task NextAsync() => RunAsync(s => s.TrySkipNextAsync());

    public Task PreviousAsync() => RunAsync(s => s.TrySkipPreviousAsync());

    public Task SeekAsync(TimeSpan position) =>
        RunAsync(s => s.TryChangePlaybackPositionAsync((s.GetTimelineProperties().StartTime + position).Ticks));

    public void Dispose() => _disposed = true;

    private async Task RefreshAsync(Session? changed)
    {
        if (_disposed || _manager is null) return;
        await _gate.WaitAsync();
        try
        {
            if (changed is not null) _lastChange[changed.SourceAppUserModelId] = DateTimeOffset.UtcNow;
            var sessions = _manager.GetSessions();
            foreach (var session in sessions) Hook(session);
            _current = sessions
                .OrderByDescending(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                .ThenByDescending(s => _lastChange.GetValueOrDefault(s.SourceAppUserModelId))
                .FirstOrDefault();
            var state = _current is null ? null : await ReadAsync(_current);
            if (!_disposed) Changed?.Invoke(state);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Hook(Session session)
    {
        var id = session.SourceAppUserModelId;
        if (_hooked.TryGetValue(id, out var known))
        {
            if (ReferenceEquals(known, session)) return;
            Unhook(known);
        }
        _hooked[id] = session;
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
    }

    private void Unhook(Session session)
    {
        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
    }

    private void OnMediaPropertiesChanged(Session sender, MediaPropertiesChangedEventArgs args) => _ = RefreshAsync(sender);

    private void OnPlaybackInfoChanged(Session sender, PlaybackInfoChangedEventArgs args) => _ = RefreshAsync(sender);

    // Some apps report the position every second; that alone must not make a background session "most recent".
    private void OnTimelinePropertiesChanged(Session sender, TimelinePropertiesChangedEventArgs args) => _ = RefreshAsync(null);

    private async Task<MediaState?> ReadAsync(Session session)
    {
        var properties = await session.TryGetMediaPropertiesAsync();
        if (properties is null || string.IsNullOrWhiteSpace(properties.Title)) return null;
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var appId = session.SourceAppUserModelId;
        var type = (properties.PlaybackType ?? playback.PlaybackType) switch
        {
            MediaPlaybackType.Music => PlaybackType.Music,
            MediaPlaybackType.Video => PlaybackType.Video,
            MediaPlaybackType.Image => PlaybackType.Image,
            _ => PlaybackType.Unknown,
        };
        // Some apps never stamp the timeline; extrapolating from year 1 would jump straight to the end.
        var updatedAt = timeline.LastUpdatedTime.Year < 2000 ? DateTimeOffset.UtcNow : timeline.LastUpdatedTime;
        return new MediaState(
            properties.Title,
            properties.Artist ?? "",
            appId,
            MediaRules.Classify(type, appId),
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            timeline.Position - timeline.StartTime,
            timeline.EndTime - timeline.StartTime,
            updatedAt,
            playback.Controls.IsPlaybackPositionEnabled,
            playback.Controls.IsNextEnabled,
            await ReadCoverAsync(properties, appId));
    }

    /// <summary>Cover art bytes, read once per track. Apps often attach the thumbnail a moment after the title, so a miss is not cached.</summary>
    private async Task<byte[]?> ReadCoverAsync(GlobalSystemMediaTransportControlsSessionMediaProperties properties, string appId)
    {
        var key = $"{appId}\n{properties.Title}\n{properties.Artist}";
        if (_cover is { } cached && cached.Key == key) return cached.Cover;
        if (properties.Thumbnail is null) return null;
        using var stream = await properties.Thumbnail.OpenReadAsync();
        using var reader = new DataReader(stream);
        var size = (uint)stream.Size;
        await reader.LoadAsync(size);
        var bytes = new byte[size];
        reader.ReadBytes(bytes);
        _cover = (key, bytes);
        return bytes;
    }

    private async Task RunAsync(Func<Session, Windows.Foundation.IAsyncOperation<bool>> command)
    {
        if (_current is not { } session) return;
        try
        {
            await command(session);
        }
        catch (Exception ex)
        {
            Failed?.Invoke(ex);
        }
    }
}
```

`src/Gili.Platform/Media/VlcWindow.cs`:

```csharp
using System.Diagnostics;
using Gili.Core.Media;

namespace Gili.Platform.Media;

public static class VlcWindow
{
    /// <summary>The video title from a running VLC window, or null when VLC is closed or idle.</summary>
    public static string? ReadTitle()
    {
        var processes = Process.GetProcessesByName("vlc");
        try
        {
            return processes.Select(p => MediaRules.VlcTitle(p.MainWindowTitle)).FirstOrDefault(t => t is not null);
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Gili.slnx`
Expected: Build succeeded, 0 error.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Platform
git commit -m "feat(platform): media sessions, VLC title, power state and focus helpers (F-ISL-04..06)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Komponen dasar App: jam, ikon, bar level

**Files:**
- Create: `src/Gili.App/ClockService.cs`, `src/Gili.App/Resources/Icons.xaml`, `src/Gili.App/Controls/IconView.cs`, `src/Gili.App/Controls/LevelMeter.cs`
- Modify: `src/Gili.App/App.xaml` (gabungkan `Icons.xaml`)

**Interfaces:**
- Consumes: `ClockText` (1a).
- Produces:
  - `Gili.App.ClockService`: `DateTime Now`, `event Action? Ticked`, `void Refresh()`.
  - Resource kunci `Icon.Music`, `Icon.Hourglass`, `Icon.Alarm`, `Icon.Calendar`, `Icon.Todo`, `Icon.Note`, `Icon.Tray`, `Icon.Cpu`, `Icon.Film` (garis) dan `Icon.Play`, `Icon.Pause`, `Icon.Prev`, `Icon.Next`, `Icon.Bolt` (isi), semua `PathGeometry` 24 × 24.
  - `Gili.App.Controls.IconView`: `Geometry? Glyph`, `bool Filled`, `Brush Foreground` (mewarisi `TextElement.Foreground`).
  - `Gili.App.Controls.LevelMeter`: `bool IsPlaying`, `Brush BarBrush`.

- [ ] **Step 1: `ClockService`**

`src/Gili.App/ClockService.cs`:

```csharp
using System.Windows.Threading;
using Gili.Core.Formatting;

namespace Gili.App;

/// <summary>Ticks exactly when the minute changes (F-ISL-01). Call Refresh after a clock, time zone or resume change.</summary>
public sealed class ClockService
{
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Normal);

    public ClockService()
    {
        _timer.Tick += (_, _) => Refresh();
        Refresh();
    }

    public DateTime Now { get; private set; }

    public event Action? Ticked;

    public void Refresh()
    {
        Now = DateTime.Now;
        _timer.Stop();
        _timer.Interval = ClockText.UntilNextMinute(Now) + TimeSpan.FromMilliseconds(50);
        _timer.Start();
        Ticked?.Invoke();
    }
}
```

- [ ] **Step 2: Geometri ikon**

Diambil dari simbol SVG di mockup (`i-music`, `i-hourglass`, …); lingkaran dan persegi membulat ditulis ulang sebagai path.

`src/Gili.App/Resources/Icons.xaml`:

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <!-- Stroked, 24 × 24, drawn by IconView with a 2-unit round pen like the mockup's SVGs. -->
    <PathGeometry x:Key="Icon.Music" Figures="M9 18V5l12-2v13 M3 18a3 3 0 1 0 6 0a3 3 0 1 0 -6 0 M15 16a3 3 0 1 0 6 0a3 3 0 1 0 -6 0" />
    <PathGeometry x:Key="Icon.Hourglass" Figures="M6 2h12 M6 22h12 M7 2v3a5 5 0 0 0 5 5a5 5 0 0 0 5 -5V2 M7 22v-3a5 5 0 0 1 5 -5a5 5 0 0 1 5 5v3" />
    <PathGeometry x:Key="Icon.Alarm" Figures="M4 13a8 8 0 1 0 16 0a8 8 0 1 0 -16 0 M12 9v4l2 2 M5 3L2 6 M22 6l-3 -3" />
    <PathGeometry x:Key="Icon.Calendar" Figures="M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1 -2 2H5a2 2 0 0 1 -2 -2V6a2 2 0 0 1 2 -2z M16 2v4 M8 2v4 M3 10h18" />
    <PathGeometry x:Key="Icon.Todo" Figures="M9 11l3 3L22 4 M21 12v7a2 2 0 0 1 -2 2H5a2 2 0 0 1 -2 -2V5a2 2 0 0 1 2 -2h11" />
    <PathGeometry x:Key="Icon.Note" Figures="M14 2H6a2 2 0 0 0 -2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2 -2V8z M14 2v6h6 M16 13H8 M16 17H8 M10 9H8" />
    <PathGeometry x:Key="Icon.Tray" Figures="M22 12h-6l-2 3h-4l-2 -3H2 M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2 -2v-6l-3.45 -6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0 -1.79 1.11z" />
    <PathGeometry x:Key="Icon.Cpu" Figures="M8 6h8a2 2 0 0 1 2 2v8a2 2 0 0 1 -2 2H8a2 2 0 0 1 -2 -2V8a2 2 0 0 1 2 -2z M10 10h4v4h-4z M9 2v4 M15 2v4 M9 18v4 M15 18v4 M2 9h4 M2 15h4 M18 9h4 M18 15h4" />
    <PathGeometry x:Key="Icon.Film" Figures="M4.5 3h15a2.5 2.5 0 0 1 2.5 2.5v13a2.5 2.5 0 0 1 -2.5 2.5h-15A2.5 2.5 0 0 1 2 18.5v-13A2.5 2.5 0 0 1 4.5 3z M7 3v18 M17 3v18 M2 12h20 M2 7.5h5 M2 16.5h5 M17 7.5h5 M17 16.5h5" />

    <!-- Filled (IconView Filled="True"). -->
    <PathGeometry x:Key="Icon.Play" Figures="M7 4.5v15l12.5 -7.5z" />
    <PathGeometry x:Key="Icon.Pause" Figures="M7 4.5h2a1 1 0 0 1 1 1v13a1 1 0 0 1 -1 1H7a1 1 0 0 1 -1 -1v-13a1 1 0 0 1 1 -1z M15 4.5h2a1 1 0 0 1 1 1v13a1 1 0 0 1 -1 1h-2a1 1 0 0 1 -1 -1v-13a1 1 0 0 1 1 -1z" />
    <PathGeometry x:Key="Icon.Prev" Figures="M19 5v14L8.5 12z M6 5h0.5a1 1 0 0 1 1 1v12a1 1 0 0 1 -1 1H6a1 1 0 0 1 -1 -1V6a1 1 0 0 1 1 -1z" />
    <PathGeometry x:Key="Icon.Next" Figures="M5 5v14l10.5 -7z M17.5 5h0.5a1 1 0 0 1 1 1v12a1 1 0 0 1 -1 1h-0.5a1 1 0 0 1 -1 -1V6a1 1 0 0 1 1 -1z" />
    <PathGeometry x:Key="Icon.Bolt" Figures="M13 2L4 14h7l-1 8 9 -12h-7z" />
</ResourceDictionary>
```

Ganti `<Application.Resources />` di `src/Gili.App/App.xaml` dengan:

```xml
    <Application.Resources>
        <ResourceDictionary Source="Resources/Icons.xaml" />
    </Application.Resources>
```

- [ ] **Step 3: `IconView` dan `LevelMeter`**

`src/Gili.App/Controls/IconView.cs`:

```csharp
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Gili.App.Controls;

/// <summary>Draws a 24 × 24 icon geometry at any size: stroked with a 2-unit round pen like the mockup's SVGs, or filled.</summary>
public sealed class IconView : FrameworkElement
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(Geometry), typeof(IconView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FilledProperty = DependencyProperty.Register(
        nameof(Filled), typeof(bool), typeof(IconView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(IconView),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));

    public Geometry? Glyph
    {
        get => (Geometry?)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public bool Filled
    {
        get => (bool)GetValue(FilledProperty);
        set => SetValue(FilledProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (Glyph is null) return;
        var size = Math.Min(ActualWidth, ActualHeight);
        dc.PushTransform(new TranslateTransform((ActualWidth - size) / 2, (ActualHeight - size) / 2));
        dc.PushTransform(new ScaleTransform(size / 24, size / 24));
        if (Filled)
        {
            dc.DrawGeometry(Foreground, null, Glyph);
        }
        else
        {
            var pen = new Pen(Foreground, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            dc.DrawGeometry(null, pen, Glyph);
        }
        dc.Pop();
        dc.Pop();
    }
}
```

`src/Gili.App/Controls/LevelMeter.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Gili.App.Controls;

/// <summary>
/// Four bars that bounce while music plays and rest low while it is paused (PRD 6.3). Each bar has its own period so
/// they never move in step. Capped at 20 fps: it can run for hours, and the island is a layered window.
/// </summary>
public sealed class LevelMeter : StackPanel
{
    public static readonly DependencyProperty IsPlayingProperty = DependencyProperty.Register(
        nameof(IsPlaying), typeof(bool), typeof(LevelMeter), new PropertyMetadata(false, (d, _) => ((LevelMeter)d).Update()));

    public static readonly DependencyProperty BarBrushProperty = DependencyProperty.Register(
        nameof(BarBrush), typeof(Brush), typeof(LevelMeter),
        new PropertyMetadata(Brushes.White, (d, e) => ((LevelMeter)d).Recolor((Brush)e.NewValue)));

    private static readonly double[] Periods = [0.70, 0.62, 0.78, 0.55];
    private readonly ScaleTransform[] _scales;
    private bool _running;

    public LevelMeter()
    {
        Orientation = Orientation.Horizontal;
        VerticalAlignment = VerticalAlignment.Center;
        _scales = Periods.Select(_ => new ScaleTransform(1, 0.25)).ToArray();
        foreach (var scale in _scales)
        {
            Children.Add(new Rectangle
            {
                Width = 2.5,
                Height = 12,
                RadiusX = 1,
                RadiusY = 1,
                Margin = new Thickness(1, 0, 1, 0),
                Fill = BarBrush,
                RenderTransform = scale,
                RenderTransformOrigin = new Point(0.5, 0.5),
            });
        }
        IsVisibleChanged += (_, _) => Update();
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    public Brush BarBrush
    {
        get => (Brush)GetValue(BarBrushProperty);
        set => SetValue(BarBrushProperty, value);
    }

    private void Recolor(Brush brush)
    {
        foreach (var bar in Children.OfType<Rectangle>()) bar.Fill = brush;
    }

    private void Update()
    {
        var run = IsPlaying && IsVisible;
        if (run == _running) return;
        _running = run;
        for (var i = 0; i < _scales.Length; i++)
        {
            if (!run)
            {
                _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
                continue;
            }
            var bounce = new DoubleAnimation(0.25, 1, TimeSpan.FromSeconds(Periods[i]))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Timeline.SetDesiredFrameRate(bounce, 20);
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
        }
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Gili.slnx`
Expected: Build succeeded, 0 error. (Komponen ini dipakai mulai Task 9; geometri ikon diperiksa saat pertama tampil di layar karena WPF mem-parse path saat runtime.)

- [ ] **Step 5: Commit**

```bash
git add src/Gili.App
git commit -m "feat(app): minute clock service, icon geometries, icon view and level meter" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `MediaService`, `IslandController`, `IslandViewModel`

Lapisan perekat tanpa UI. Diperiksa lewat build; perilakunya terlihat mulai Task 9.

**Files:**
- Create: `src/Gili.App/Island/MediaService.cs`, `src/Gili.App/Island/IslandController.cs`, `src/Gili.App/Island/IslandViewModel.cs`

**Interfaces:**
- Consumes: Task 2–7; `SettingsHub`, `FileLog` (1a).
- Produces (namespace `Gili.App.Island`):
  - `MediaService(Dispatcher dispatcher, FileLog log) : IDisposable`: `MediaState? Current`, `bool IsActive`, `event Action? Updated` (thread UI; saat media berubah dan tiap detik selama ada media), `Task StartAsync()`, `TogglePlayPauseAsync()`, `NextAsync()`, `PreviousAsync()`, `SeekAsync(TimeSpan)`.
  - `IslandController(Dispatcher dispatcher, MediaService media) : IDisposable`: `IslandView View`, `MediaService Media`, `PowerSnapshot? Power`, `event Action<IslandView>? ViewChanged`, `event Action? PowerChanged`, `OpenFromIsland()`, `OpenBubble()`, `Open(IslandPage)`, `Toggle(IslandPage)`, `Close()`, `SetFullscreen(bool)`.
  - `sealed record IslandTab(IslandPage Page, string Name, Geometry Glyph, string? Placeholder)`.
  - `IslandViewModel(IslandController island, ClockService clock, SettingsHub settings)` dengan properti yang dipakai XAML di Task 9–11: `Time`, `Date`, `FontSize`, `Slot`, `ShowClock`, `ShowMediaSlot`, `ShowChargingSlot`, `HasMedia`, `IsMusic`, `IsVideo`, `IsPlaying`, `MusicPlaying`, `MediaSlotTitle`, `MediaTitle`, `MediaArtist`, `MediaApp`, `LevelBrush`, `Cover`, `HasCover`, `Progress`, `PositionText`, `DurationText`, `RemainingText`, `HasTimeline`, `CanSeek`, `CanSkip`, `ChargePercent`, `WideText`, `WideValue`, `HasWideValue`, `WideHasIcon`, `WideIconBrush`, `BubbleGlyph`, `BubbleBrush`, `BubbleFilled`, `Tabs`, `SelectedTab`.

- [ ] **Step 1: `MediaService`**

`src/Gili.App/Island/MediaService.cs`:

```csharp
using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Media;
using Gili.Platform.Media;

namespace Gili.App.Island;

/// <summary>
/// The media the island shows: Windows' media session, or VLC's window title when no session exists (PRD 9.2).
/// Ticks every second while there is media, so progress and the paused grace period stay current.
/// </summary>
public sealed class MediaService : IDisposable
{
    private readonly FileLog _log;
    private readonly MediaSessionMonitor _monitor = new();
    private readonly MediaPresence _presence = new();
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private MediaState? _session;
    private MediaState? _vlc;
    private int _ticks;

    public MediaService(Dispatcher dispatcher, FileLog log)
    {
        _log = log;
        _monitor.Changed += state => dispatcher.InvokeAsync(() =>
        {
            _session = state;
            Publish();
        });
        _monitor.Failed += ex => _log.Write("Media session request failed", ex);
        _tick.Tick += (_, _) => OnTick();
    }

    public MediaState? Current => _session ?? _vlc;

    /// <summary>Whether media holds the compact slot (see MediaPresence).</summary>
    public bool IsActive { get; private set; }

    /// <summary>Raised on the UI thread when the media changes, and once a second while there is media.</summary>
    public event Action? Updated;

    public async Task StartAsync()
    {
        _tick.Start();
        try
        {
            await _monitor.StartAsync();
        }
        catch (Exception ex)
        {
            _log.Write("Media sessions are unavailable", ex);
        }
    }

    public Task TogglePlayPauseAsync() => _monitor.TogglePlayPauseAsync();

    public Task NextAsync() => _monitor.NextAsync();

    public Task PreviousAsync() => _monitor.PreviousAsync();

    public Task SeekAsync(TimeSpan position) => _monitor.SeekAsync(position);

    public void Dispose()
    {
        _tick.Stop();
        _monitor.Dispose();
    }

    private void OnTick()
    {
        // VLC is checked every 2 s, and only while no media session covers it (PRD 9.2).
        if (_session is null && ++_ticks % 2 == 0) ReadVlc();
        if (Current is not null || IsActive) Publish();
    }

    private void ReadVlc()
    {
        var title = VlcWindow.ReadTitle();
        if (title == _vlc?.Title) return;
        _vlc = title is null
            ? null
            : new MediaState(title, "", "vlc.exe", MediaKind.Video, IsPlaying: true, TimeSpan.Zero, TimeSpan.Zero,
                DateTimeOffset.Now, CanSeek: false, CanSkip: false, Cover: null);
        Publish();
    }

    private void Publish()
    {
        IsActive = _presence.IsActive(Current, DateTimeOffset.Now);
        Updated?.Invoke();
    }
}
```

- [ ] **Step 2: `IslandController`**

`src/Gili.App/Island/IslandController.cs`:

```csharp
using System.Windows.Threading;
using Gili.Core.Island;
using Gili.Core.Power;
using Gili.Platform.Power;

namespace Gili.App.Island;

/// <summary>Feeds the providers into the IslandCoordinator and runs its timers for wide notices and reminder cards.</summary>
public sealed class IslandController : IDisposable
{
    private readonly IslandCoordinator _coordinator = new(() => DateTimeOffset.Now);
    private readonly DispatcherTimer _deadline = new(DispatcherPriority.Normal);
    private readonly PowerMonitor _power = new();

    public IslandController(Dispatcher dispatcher, MediaService media)
    {
        Media = media;
        _deadline.Tick += (_, _) =>
        {
            _deadline.Stop();
            _coordinator.Tick();
            ScheduleDeadline();
        };
        _coordinator.Changed += view =>
        {
            ScheduleDeadline();
            ViewChanged?.Invoke(view);
        };
        media.Updated += () => _coordinator.SetActivity(ActivityKind.Media, media.IsActive);
        _power.Changed += snapshot => dispatcher.InvokeAsync(() => ApplyPower(snapshot));
        ApplyPower(PowerMonitor.Read());
    }

    public IslandView View => _coordinator.View;

    public MediaService Media { get; }

    public PowerSnapshot? Power { get; private set; }

    public event Action<IslandView>? ViewChanged;

    public event Action? PowerChanged;

    public void OpenFromIsland() => _coordinator.OpenFor(View.Slot);

    public void OpenBubble() => _coordinator.OpenFor(View.Bubble);

    public void Open(IslandPage page) => _coordinator.Open(page);

    public void Toggle(IslandPage page) => _coordinator.Toggle(page);

    public void Close() => _coordinator.Close();

    public void SetFullscreen(bool fullscreen) => _coordinator.SetFullscreen(fullscreen);

    public void Dispose()
    {
        _deadline.Stop();
        _power.Dispose();
    }

    private void ApplyPower(PowerSnapshot snapshot)
    {
        var wide = PowerRules.WideFor(Power, snapshot);
        Power = snapshot;
        PowerChanged?.Invoke();
        _coordinator.SetActivity(ActivityKind.Charging, PowerRules.IsCharging(snapshot));
        if (wide is not null) _coordinator.ShowWide(wide);
    }

    private void ScheduleDeadline()
    {
        _deadline.Stop();
        if (_coordinator.NextDeadline is not { } at) return;
        var wait = at - DateTimeOffset.Now;
        _deadline.Interval = wait > TimeSpan.FromMilliseconds(10) ? wait : TimeSpan.FromMilliseconds(10);
        _deadline.Start();
    }
}
```

- [ ] **Step 3: `IslandViewModel`**

`src/Gili.App/Island/IslandViewModel.cs`:

```csharp
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Formatting;
using Gili.Core.Island;
using Gili.Core.Layout;
using Gili.Core.Media;

namespace Gili.App.Island;

public sealed record IslandTab(IslandPage Page, string Name, Geometry Glyph, string? Placeholder);

public sealed partial class IslandViewModel : ObservableObject
{
    private const string Phase2 = "Coming in Phase 2";
    private const string Phase3 = "Coming in Phase 3";
    private static readonly Brush Accent = Frozen("#30D158");
    private static readonly Brush VideoOrange = Frozen("#FF9F0A");
    private static readonly Brush Text = Frozen("#F5F5F7");

    private readonly IslandController _island;
    private readonly ClockService _clock;
    private readonly SettingsHub _settings;
    private byte[]? _coverBytes;
    private string? _levelColor;

    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private double _fontSize = 12;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClock), nameof(ShowMediaSlot), nameof(ShowChargingSlot))]
    private ActivityKind? _slot;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowClock), nameof(ShowMediaSlot))]
    private bool _hasMedia;

    [ObservableProperty] private bool _isMusic;
    [ObservableProperty] private bool _isVideo;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _musicPlaying;
    [ObservableProperty] private string _mediaSlotTitle = "";
    [ObservableProperty] private string _mediaTitle = "";
    [ObservableProperty] private string _mediaArtist = "";
    [ObservableProperty] private string _mediaApp = "";
    [ObservableProperty] private Brush _levelBrush = Accent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCover))]
    private ImageSource? _cover;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _positionText = "";
    [ObservableProperty] private string _durationText = "";
    [ObservableProperty] private string _remainingText = "";
    [ObservableProperty] private bool _hasTimeline;
    [ObservableProperty] private bool _canSeek;
    [ObservableProperty] private bool _canSkip;
    [ObservableProperty] private string _chargePercent = "";
    [ObservableProperty] private string _wideText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWideValue))]
    private string? _wideValue;

    [ObservableProperty] private bool _wideHasIcon;
    [ObservableProperty] private Brush _wideIconBrush = Text;
    [ObservableProperty] private Geometry? _bubbleGlyph;
    [ObservableProperty] private Brush _bubbleBrush = Text;
    [ObservableProperty] private bool _bubbleFilled;
    [ObservableProperty] private int _selectedTab;

    public IslandViewModel(IslandController island, ClockService clock, SettingsHub settings)
    {
        _island = island;
        _clock = clock;
        _settings = settings;
        // The Claude page joins in Phase 3, when its integration exists.
        Tabs =
        [
            new(IslandPage.Media, "Media", Glyph("Icon.Music"), null),
            new(IslandPage.Timer, "Timer", Glyph("Icon.Hourglass"), Phase2),
            new(IslandPage.Alarms, "Alarms", Glyph("Icon.Alarm"), Phase2),
            new(IslandPage.Calendar, "Calendar", Glyph("Icon.Calendar"), Phase2),
            new(IslandPage.Todo, "Todo", Glyph("Icon.Todo"), Phase2),
            new(IslandPage.Notes, "Notes", Glyph("Icon.Note"), Phase2),
            new(IslandPage.Shelf, "Shelf", Glyph("Icon.Tray"), Phase3),
            new(IslandPage.System, "System", Glyph("Icon.Cpu"), Phase3),
        ];
        clock.Ticked += UpdateClock;
        settings.Changed += _ => UpdateSettings();
        island.ViewChanged += UpdateView;
        island.PowerChanged += UpdatePower;
        island.Media.Updated += UpdateMedia;
        UpdateSettings();
        UpdatePower();
        UpdateMedia();
        UpdateView(island.View);
    }

    public IReadOnlyList<IslandTab> Tabs { get; }

    public bool ShowClock => Slot is not (ActivityKind.Media or ActivityKind.Charging) || (Slot == ActivityKind.Media && !HasMedia);

    public bool ShowMediaSlot => Slot == ActivityKind.Media && HasMedia;

    public bool ShowChargingSlot => Slot == ActivityKind.Charging;

    public bool HasCover => Cover is not null;

    public bool HasWideValue => WideValue is not null;

    private void UpdateClock()
    {
        Time = ClockText.Time(_clock.Now, _settings.Current.Use24HourClock);
        Date = ClockText.Date(_clock.Now);
    }

    private void UpdateSettings()
    {
        FontSize = BarMetrics.IslandFontSize(_settings.Current.IslandHeight);
        UpdateClock();
    }

    private void UpdatePower() =>
        ChargePercent = _island.Power is { } power ? power.Percent.ToString(CultureInfo.InvariantCulture) + "%" : "";

    private void UpdateView(IslandView view)
    {
        Slot = view.Slot;
        var index = Tabs.ToList().FindIndex(t => t.Page == view.Page);
        SelectedTab = index >= 0 ? index : 0;
        if (view.Wide is { } wide)
        {
            WideText = wide.Text;
            WideValue = wide.Value;
            WideHasIcon = wide.Icon != WideIcon.None;
            WideIconBrush = wide.Icon == WideIcon.Charging ? Accent : Text;
        }
        UpdateBubble();
    }

    private void UpdateBubble()
    {
        (BubbleGlyph, BubbleBrush, BubbleFilled) = _island.View.Bubble switch
        {
            ActivityKind.Media => (Glyph(IsVideo ? "Icon.Film" : "Icon.Music"), IsVideo ? VideoOrange : Accent, false),
            ActivityKind.Charging => (Glyph("Icon.Bolt"), Accent, true),
            _ => ((Geometry?)null, Text, false),
        };
    }

    private void UpdateMedia()
    {
        var media = _island.Media.Current;
        HasMedia = media is not null;
        if (media is null)
        {
            IsMusic = false;
            IsVideo = false;
            IsPlaying = false;
            MusicPlaying = false;
            UpdateBubble();
            return;
        }
        var position = media.PositionAt(DateTimeOffset.Now);
        IsVideo = media.Kind == MediaKind.Video;
        IsMusic = !IsVideo;
        IsPlaying = media.IsPlaying;
        MusicPlaying = media.IsPlaying && IsMusic;
        MediaSlotTitle = MediaRules.SlotTitle(media);
        MediaTitle = media.Title;
        MediaArtist = media.Artist;
        MediaApp = MediaRules.AppName(media.AppId);
        var color = MediaRules.LevelColor(media.AppId);
        if (color != _levelColor)
        {
            _levelColor = color;
            LevelBrush = Frozen(color);
        }
        HasTimeline = media.HasTimeline;
        Progress = MediaRules.Progress(position, media.Duration);
        PositionText = MediaRules.Time(position);
        DurationText = MediaRules.Time(media.Duration);
        RemainingText = MediaRules.Remaining(position, media.Duration);
        CanSeek = media.CanSeek && media.HasTimeline;
        CanSkip = media.CanSkip;
        if (!ReferenceEquals(media.Cover, _coverBytes))
        {
            _coverBytes = media.Cover;
            Cover = Decode(media.Cover);
        }
        UpdateBubble();
    }

    private static ImageSource? Decode(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 }) return null;
        try
        {
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 224;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or InvalidOperationException)
        {
            // An app sent cover bytes WPF cannot decode; the page shows the music icon instead.
            return null;
        }
    }

    private static Geometry Glyph(string key) => (Geometry)Application.Current.FindResource(key);

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
```

- [ ] **Step 4: Build**

Run: `dotnet build Gili.slnx`
Expected: Build succeeded, 0 error.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.App/Island
git commit -m "feat(app): island controller, media service with VLC fallback and island view model" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `IslandWindow` (compact, wide, gelembung, pegas) dan bar yang mengosongkan tengah

**Files:**
- Create: `src/Gili.App/Controls/VisibleWhenFalse.cs`, `src/Gili.App/Island/IslandWindow.xaml`, `src/Gili.App/Island/IslandWindow.xaml.cs`
- Modify (ganti seluruh isi): `src/Gili.App/Bar/BarViewModel.cs`, `src/Gili.App/Bar/BarWindow.xaml.cs`
- Modify: `src/Gili.App/Bar/BarWindow.xaml`, `src/Gili.App/App.xaml.cs`

**Interfaces:**
- Consumes: Task 1–8.
- Produces:
  - `IslandWindow(IslandController island, IslandViewModel vm, SettingsHub settings)` dengan `event Action<bool, double>? FloatingChanged` (mengambang?, lebar target panel). Task 10 mengisi `ExpandedLayer` dan menambah method di `IslandWindow.xaml.cs`; field `_island`, `_vm`, `_mode`, `_hwnd` dan method `OnPreviewKeyDown`, `OnSourceInitialized` dipakai di sana.
  - `BarWindow`: `event Action? TimeChanged`, `event Action<bool>? FullscreenChanged`, `void SetIslandFloating(bool floating, double islandWidth)`. `BarViewModel` tidak lagi punya jam dan ukuran pulau.

- [ ] **Step 1: Konverter visibilitas terbalik**

`src/Gili.App/Controls/VisibleWhenFalse.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Gili.App.Controls;

/// <summary>The inverse of BooleanToVisibilityConverter: visible when the value is false.</summary>
public sealed class VisibleWhenFalse : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
```

- [ ] **Step 2: `IslandWindow.xaml`**

`src/Gili.App/Island/IslandWindow.xaml`:

```xml
<Window x:Class="Gili.App.Island.IslandWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:c="clr-namespace:Gili.App.Controls"
        Title="Gili Island" WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" ShowActivated="False" Topmost="True" UseLayoutRounding="True"
        FontFamily="Segoe UI Variable Text, Segoe UI" FontSize="12" Foreground="#F5F5F7">
    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="VisibleWhen" />
        <c:VisibleWhenFalse x:Key="VisibleWhenFalse" />
        <Style x:Key="SlotButton" TargetType="Button">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#F5F5F7" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bg" Width="20" Height="20" CornerRadius="10" Background="Transparent">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#2C2C2E" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Canvas>
        <!-- The floating shadow sits on its own plate so the island's text is never rendered through an effect. -->
        <Border x:Name="ShadowPlate" Background="#000000" Opacity="0" IsHitTestVisible="False">
            <Border.Effect>
                <DropShadowEffect BlurRadius="44" ShadowDepth="12" Direction="270" Opacity="0.7" Color="Black" />
            </Border.Effect>
        </Border>

        <Border x:Name="Body" Background="#000000"
                MouseLeftButtonUp="OnBodyClick" MouseEnter="OnBodyEnter" MouseLeave="OnBodyLeave">
            <Grid>
                <!-- Thin light line along the top edge while floating, so the black panel stands apart from dark windows. -->
                <Border x:Name="TopHighlight" BorderBrush="#17FFFFFF" BorderThickness="0,1,0,0" Opacity="0" IsHitTestVisible="False" />

                <!-- Layers are wider than the island and centred, so measuring their content never depends on the animated width. -->
                <Grid x:Name="CompactLayer" Width="760" HorizontalAlignment="Center" TextElement.FontSize="{Binding FontSize}">
                    <Grid x:Name="CompactContent" HorizontalAlignment="Center" VerticalAlignment="Center">
                        <StackPanel Orientation="Horizontal" Visibility="{Binding ShowClock, Converter={StaticResource VisibleWhen}}">
                            <TextBlock Text="{Binding Time}" FontWeight="SemiBold" Typography.NumeralAlignment="Tabular" />
                            <TextBlock Text="{Binding Date}" Foreground="#8E8E93" Margin="6,0,0,0" />
                        </StackPanel>

                        <!-- Activities span at least the 200 px island edge to edge: icon left, value right (PRD 6.3). -->
                        <Grid Visibility="{Binding ShowMediaSlot, Converter={StaticResource VisibleWhen}}">
                            <DockPanel MinWidth="172" Visibility="{Binding IsMusic, Converter={StaticResource VisibleWhen}}">
                                <c:LevelMeter DockPanel.Dock="Left" IsPlaying="{Binding IsPlaying}" BarBrush="{Binding LevelBrush}" />
                                <Button DockPanel.Dock="Right" Style="{StaticResource SlotButton}" Margin="6,0,0,0" Click="OnPlayPause">
                                    <Grid>
                                        <c:IconView Glyph="{StaticResource Icon.Pause}" Filled="True" Width="11" Height="11"
                                                    Visibility="{Binding IsPlaying, Converter={StaticResource VisibleWhen}}" />
                                        <c:IconView Glyph="{StaticResource Icon.Play}" Filled="True" Width="11" Height="11"
                                                    Visibility="{Binding IsPlaying, Converter={StaticResource VisibleWhenFalse}}" />
                                    </Grid>
                                </Button>
                                <TextBlock Text="{Binding MediaSlotTitle}" Margin="6,0,0,0" HorizontalAlignment="Center" VerticalAlignment="Center" />
                            </DockPanel>
                            <DockPanel MinWidth="172" Visibility="{Binding IsVideo, Converter={StaticResource VisibleWhen}}">
                                <c:IconView DockPanel.Dock="Left" Glyph="{StaticResource Icon.Film}" Width="14" Height="14" Foreground="#FF9F0A" />
                                <Grid DockPanel.Dock="Right" Width="36" Height="3" Margin="6,0,0,0" VerticalAlignment="Center"
                                      Visibility="{Binding HasTimeline, Converter={StaticResource VisibleWhen}}">
                                    <Border Background="#3A3A3C" CornerRadius="1.5" />
                                    <Border Background="#F5F5F7" CornerRadius="1.5" RenderTransformOrigin="0,0.5">
                                        <Border.RenderTransform>
                                            <ScaleTransform ScaleX="{Binding Progress}" />
                                        </Border.RenderTransform>
                                    </Border>
                                </Grid>
                                <TextBlock Text="{Binding MediaSlotTitle}" Margin="6,0,0,0" HorizontalAlignment="Center" VerticalAlignment="Center" />
                            </DockPanel>
                        </Grid>

                        <DockPanel MinWidth="172" Visibility="{Binding ShowChargingSlot, Converter={StaticResource VisibleWhen}}">
                            <c:IconView DockPanel.Dock="Left" Glyph="{StaticResource Icon.Bolt}" Filled="True" Width="14" Height="14" Foreground="#30D158" />
                            <TextBlock DockPanel.Dock="Right" Text="{Binding ChargePercent}" FontWeight="SemiBold"
                                       Typography.NumeralAlignment="Tabular" VerticalAlignment="Center" />
                            <Border />
                        </DockPanel>
                    </Grid>
                </Grid>

                <Grid x:Name="WideLayer" Width="760" HorizontalAlignment="Center" Opacity="0" IsHitTestVisible="False"
                      TextElement.FontSize="{Binding FontSize}">
                    <StackPanel x:Name="WideContent" Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                        <c:IconView Glyph="{StaticResource Icon.Bolt}" Filled="True" Width="14" Height="14" Margin="0,0,8,0"
                                    Foreground="{Binding WideIconBrush}" Visibility="{Binding WideHasIcon, Converter={StaticResource VisibleWhen}}" />
                        <TextBlock Text="{Binding WideText}" />
                        <TextBlock Visibility="{Binding HasWideValue, Converter={StaticResource VisibleWhen}}">
                            <Run Text=" · " /><Run Text="{Binding WideValue, Mode=OneWay}" FontWeight="SemiBold" Typography.NumeralAlignment="Tabular" />
                        </TextBlock>
                    </StackPanel>
                </Grid>

                <!-- Filled in Task 10: page menu, pages and the clock line. -->
                <Grid x:Name="ExpandedLayer" Width="680" Height="256" HorizontalAlignment="Center" VerticalAlignment="Top"
                      Opacity="0" IsHitTestVisible="False" />
            </Grid>
        </Border>

        <Border x:Name="Bubble" Background="#000000" RenderTransformOrigin="0.5,0.5" MouseLeftButtonUp="OnBubbleClick">
            <c:IconView Glyph="{Binding BubbleGlyph}" Filled="{Binding BubbleFilled}" Foreground="{Binding BubbleBrush}" Width="13" Height="13" />
        </Border>
    </Canvas>
</Window>
```

- [ ] **Step 3: `IslandWindow.xaml.cs`**

`src/Gili.App/Island/IslandWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Gili.Core.Island;
using Gili.Core.Motion;
using Gili.Platform.Shell;

namespace Gili.App.Island;

/// <summary>
/// The island's own transparent topmost window (PRD 9.1), owned by the bar so it always stays above it. The window is
/// an envelope around the island: bar-high while compact, big enough for the floating panel while floating. Width,
/// height, top and corner radius follow springs stepped once per rendered frame, and only while something moves.
/// </summary>
public partial class IslandWindow : Window
{
    private const double CompactEnvelopeWidth = 760;
    private const double FloatingEnvelopeWidth = 1040;
    private const double FloatingEnvelopeHeight = 600;

    private readonly IslandController _island;
    private readonly IslandViewModel _vm;
    private readonly SettingsHub _settings;
    private readonly Spring _width = new(IslandLayout.MinCompactWidth);
    private readonly Spring _height = new(24);
    private readonly Spring _top = new(4);
    private readonly Spring _radius = new(12);
    private readonly Spring _float = new(0);
    private readonly Spring _bubble = new(0);
    private readonly Spring[] _springs;
    private readonly ScaleTransform _bubbleScale = new(0, 0);
    private readonly RectangleGeometry _clip = new();
    private readonly DispatcherTimer _awayTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private IslandMode _mode = IslandMode.Compact;
    private TimeSpan _lastFrame;
    private bool _animating;
    private bool _hovered;
    private bool _floatingEnvelope;
    private IntPtr _hwnd;
    private IntPtr _previousForeground;

    public IslandWindow(IslandController island, IslandViewModel vm, SettingsHub settings)
    {
        _island = island;
        _vm = vm;
        _settings = settings;
        _springs = [_width, _height, _top, _radius, _float, _bubble];
        InitializeComponent();
        DataContext = vm;
        Body.Clip = _clip;
        Bubble.RenderTransform = _bubbleScale;
        island.ViewChanged += OnViewChanged;
        settings.Changed += _ =>
        {
            if (!_floatingEnvelope) SetEnvelope(false);
            Relayout();
        };
        CompactContent.SizeChanged += (_, _) => Relayout();
        WideContent.SizeChanged += (_, _) => Relayout();
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.PrimaryScreenWidth)) SetEnvelope(_floatingEnvelope);
        };
        _awayTimer.Tick += (_, _) =>
        {
            _awayTimer.Stop();
            if (_mode == IslandMode.Expanded && !Body.IsMouseOver) _island.Close();
        };
        Deactivated += (_, _) =>
        {
            if (_mode == IslandMode.Expanded) _island.Close();
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>Whether the island floats, and the panel's width; the bar fades its backdrop and covered elements (PRD 6.3).</summary>
    public event Action<bool, double>? FloatingChanged;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeNonActivatingToolWindow(_hwnd);
        SetEnvelope(false);
        OnViewChanged(_island.View);
        Relayout(snap: true);
    }

    private static bool Floats(IslandMode mode) => mode is IslandMode.Expanded or IslandMode.Card or IslandMode.DropZone;

    private void OnViewChanged(IslandView view)
    {
        if (_hwnd == IntPtr.Zero) return;
        var previous = _mode;
        _mode = view.Mode;
        UpdateFocus(view.Mode, previous);
        if (view.Mode == IslandMode.Hidden)
        {
            Hide();
            return;
        }
        if (!IsVisible) Show();
        if (Floats(view.Mode) && !_floatingEnvelope) SetEnvelope(true);
        _hovered = view.Mode == IslandMode.Compact && Body.IsMouseOver;
        ShowLayers(view.Mode);
        Relayout(Floats(view.Mode) ? SpringParameters.Open : Floats(previous) ? SpringParameters.Close : SpringParameters.Resize);
        var s = _settings.Current;
        var width = Floats(view.Mode)
            ? IslandLayout.For(view.Mode, view.Page, 0, false, s.BarHeight, s.IslandHeight, SystemParameters.PrimaryScreenWidth).Width
            : 0;
        FloatingChanged?.Invoke(Floats(view.Mode), width);
    }

    /// <summary>Sets the springs' targets for the current mode. Without explicit parameters, a floating island keeps its own.</summary>
    private void Relayout(SpringParameters? parameters = null, bool snap = false)
    {
        if (_hwnd == IntPtr.Zero) return;
        var s = _settings.Current;
        var content = _mode == IslandMode.Wide ? WideContent.ActualWidth : CompactContent.ActualWidth;
        var target = IslandLayout.For(_mode, _island.View.Page, content, _hovered, s.BarHeight, s.IslandHeight, SystemParameters.PrimaryScreenWidth);
        var spring = parameters ?? (Floats(_mode) ? null : SpringParameters.Resize);
        var instant = snap || !SystemParameters.ClientAreaAnimation;
        Retarget(_width, target.Width, spring, instant);
        Retarget(_height, target.Height, spring, instant);
        Retarget(_top, target.Top, spring, instant);
        Retarget(_radius, target.CornerRadius, spring, instant);
        Retarget(_float, Floats(_mode) ? 1 : 0, spring, instant);
        Retarget(_bubble, _island.View.Bubble is null ? 0 : 1, SpringParameters.Pop, instant);
        if (!instant)
        {
            StartAnimating();
            return;
        }
        Apply();
        if (!Floats(_mode) && _floatingEnvelope) SetEnvelope(false);
    }

    private static void Retarget(Spring spring, double target, SpringParameters? parameters, bool instant)
    {
        if (parameters is { } p) spring.Parameters = p;
        if (instant) spring.SnapTo(target);
        else spring.Target = target;
    }

    private void StartAnimating()
    {
        if (_animating) return;
        _animating = true;
        _lastFrame = TimeSpan.Zero;
        CompositionTarget.Rendering += OnFrame;
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        var now = ((RenderingEventArgs)e).RenderingTime;
        if (now == _lastFrame) return; // Rendering can be raised more than once per frame.
        var seconds = _lastFrame == TimeSpan.Zero ? 1.0 / 60 : (now - _lastFrame).TotalSeconds;
        _lastFrame = now;
        foreach (var spring in _springs) spring.Step(seconds);
        Apply();
        if (!_springs.All(s => s.IsAtRest)) return;
        CompositionTarget.Rendering -= OnFrame;
        _animating = false;
        if (!Floats(_mode) && _floatingEnvelope) SetEnvelope(false);
    }

    /// <summary>The window spans the island's reach: bar-high while compact, panel-sized while floating; always centred at the top.</summary>
    private void SetEnvelope(bool floating)
    {
        _floatingEnvelope = floating;
        var screen = SystemParameters.PrimaryScreenWidth;
        Width = Math.Min(screen, floating ? FloatingEnvelopeWidth : CompactEnvelopeWidth);
        Height = floating ? FloatingEnvelopeHeight : _settings.Current.BarHeight;
        Left = (screen - Width) / 2;
        Top = 0;
        Apply();
    }

    private void Apply()
    {
        var w = _width.Value;
        var h = _height.Value;
        var r = Math.Max(0, _radius.Value);
        var left = (Width - w) / 2;
        foreach (var plate in new[] { Body, ShadowPlate })
        {
            System.Windows.Controls.Canvas.SetLeft(plate, left);
            System.Windows.Controls.Canvas.SetTop(plate, _top.Value);
            plate.Width = w;
            plate.Height = h;
            plate.CornerRadius = new CornerRadius(r);
        }
        _clip.Rect = new Rect(0, 0, w, h);
        _clip.RadiusX = r;
        _clip.RadiusY = r;
        var floating = Math.Clamp(_float.Value, 0, 1);
        ShadowPlate.Opacity = floating;
        TopHighlight.CornerRadius = new CornerRadius(r);
        TopHighlight.Opacity = floating;

        var s = _settings.Current;
        Bubble.Width = s.IslandHeight;
        Bubble.Height = s.IslandHeight;
        Bubble.CornerRadius = new CornerRadius(s.IslandHeight / 2.0);
        System.Windows.Controls.Canvas.SetLeft(Bubble, Width / 2 + w / 2 + IslandLayout.BubbleGap);
        System.Windows.Controls.Canvas.SetTop(Bubble, (s.BarHeight - s.IslandHeight) / 2.0);
        var scale = Math.Max(0, _bubble.Value);
        _bubbleScale.ScaleX = scale;
        _bubbleScale.ScaleY = scale;
        Bubble.IsHitTestVisible = _bubble.Target > 0;
    }

    private void ShowLayers(IslandMode mode)
    {
        Fade(CompactLayer, mode == IslandMode.Compact);
        Fade(WideLayer, mode == IslandMode.Wide);
        Reveal(ExpandedLayer, mode == IslandMode.Expanded);
    }

    /// <summary>Slot content cross-fades: out in 90 ms, in over 180 ms once the old content is gone (PRD 8.3).</summary>
    private static void Fade(UIElement layer, bool show)
    {
        if (layer.IsHitTestVisible == show) return;
        layer.IsHitTestVisible = show;
        var animated = SystemParameters.ClientAreaAnimation;
        var fade = new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(!animated ? 100 : show ? 180 : 90))
        {
            BeginTime = show && animated ? TimeSpan.FromMilliseconds(90) : TimeSpan.Zero,
        };
        layer.BeginAnimation(OpacityProperty, fade);
    }

    /// <summary>The open island's content arrives 120 ms after the panel starts growing: opacity, blur 6 → 0 and scale 0.98 → 1 over 220 ms.</summary>
    private static void Reveal(FrameworkElement layer, bool show)
    {
        if (layer.IsHitTestVisible == show) return;
        layer.IsHitTestVisible = show;
        var animated = SystemParameters.ClientAreaAnimation;
        if (!show || !animated)
        {
            layer.Effect = null;
            layer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(animated ? 90 : 100)));
            return;
        }
        var delay = TimeSpan.FromMilliseconds(120);
        var duration = TimeSpan.FromMilliseconds(220);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var blur = new BlurEffect { Radius = 6 };
        var scale = new ScaleTransform(0.98, 0.98);
        layer.Effect = blur;
        layer.RenderTransformOrigin = new Point(0.5, 0);
        layer.RenderTransform = scale;
        layer.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { BeginTime = delay, EasingFunction = ease });
        var sharpen = new DoubleAnimation(6, 0, duration) { BeginTime = delay, EasingFunction = ease };
        // An effect costs work on every frame; drop it once the content is sharp.
        sharpen.Completed += (_, _) =>
        {
            if (ReferenceEquals(layer.Effect, blur)) layer.Effect = null;
        };
        blur.BeginAnimation(BlurEffect.RadiusProperty, sharpen);
        var grow = new DoubleAnimation(0.98, 1, duration) { BeginTime = delay, EasingFunction = ease };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>The open island takes focus so Esc and the arrow keys reach it; the previous window gets it back on close.</summary>
    private void UpdateFocus(IslandMode mode, IslandMode previous)
    {
        if (mode == IslandMode.Expanded && previous != IslandMode.Expanded)
        {
            _previousForeground = ForegroundWindow.Get();
            WindowStyles.SetNoActivate(_hwnd, false);
            Activate();
        }
        else if (mode != IslandMode.Expanded && previous == IslandMode.Expanded)
        {
            _awayTimer.Stop();
            WindowStyles.SetNoActivate(_hwnd, true);
            if (IsActive) ForegroundWindow.Restore(_previousForeground);
            _previousForeground = IntPtr.Zero;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_mode != IslandMode.Expanded || e.Key != Key.Escape) return;
        _island.Close();
        e.Handled = true;
    }

    private void OnBodyClick(object sender, MouseButtonEventArgs e)
    {
        if (_mode is IslandMode.Compact or IslandMode.Wide) _island.OpenFromIsland();
    }

    private void OnBodyEnter(object sender, MouseEventArgs e)
    {
        _awayTimer.Stop();
        if (_mode != IslandMode.Compact || _hovered) return;
        _hovered = true;
        Relayout(SpringParameters.Hover);
    }

    private void OnBodyLeave(object sender, MouseEventArgs e)
    {
        if (_mode == IslandMode.Expanded) _awayTimer.Start();
        if (!_hovered) return;
        _hovered = false;
        Relayout(SpringParameters.Hover);
    }

    private void OnBubbleClick(object sender, MouseButtonEventArgs e)
    {
        _island.OpenBubble();
        e.Handled = true;
    }

    private void OnPlayPause(object sender, RoutedEventArgs e) => _ = _island.Media.TogglePlayPauseAsync();
}
```

- [ ] **Step 4: `BarViewModel` tanpa jam dan pulau**

Ganti seluruh isi `src/Gili.App/Bar/BarViewModel.cs`:

```csharp
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Formatting;
using Gili.Core.Layout;
using Gili.Core.Settings;

namespace Gili.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    [ObservableProperty] private Brush _tint = Brushes.Transparent;
    [ObservableProperty] private double _moduleHeight = 26;
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string? _cpuTooltip;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string? _ramTooltip;
    [ObservableProperty] private string _upText = "↑ —";
    [ObservableProperty] private string _downText = "↓ —";
    [ObservableProperty] private string? _netTooltip;

    public void ApplySettings(AppSettings settings, BarAppearance look)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(look.TintOpacity * 255), 0x16, 0x16, 0x18));
        brush.Freeze();
        Tint = brush;
        ModuleHeight = BarMetrics.ModuleHitHeight(settings.BarHeight);
    }

    public void ApplyStats(StatsSnapshot stats)
    {
        var cpu = stats.Cpu.Value;
        CpuText = cpu is null ? "—" : StatText.Percent(cpu.Percent);
        CpuTooltip = stats.Cpu.Error;

        var memory = stats.Memory.Value;
        RamText = memory is null ? "—" : StatText.Percent(memory.Percent);
        RamTooltip = memory is null ? stats.Memory.Error : StatText.MemoryUsage(memory.UsedBytes, memory.TotalBytes);

        var network = stats.Network.Value;
        UpText = "↑ " + (network is null ? "—" : StatText.Rate(network.UpBytesPerSecond));
        DownText = "↓ " + (network is null ? "—" : StatText.Rate(network.DownBytesPerSecond));
        NetTooltip = network is null ? stats.Network.Error : network.Adapters;
    }
}
```

- [ ] **Step 5: `BarWindow.xaml`: latar terpisah, tengah kosong**

Di `src/Gili.App/Bar/BarWindow.xaml`, ganti bagian mulai `<Border Background="{Binding Tint}" BorderBrush="#0FFFFFFF" BorderThickness="0,0,0,1">` sampai dan termasuk baris `<StackPanel Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">` (menu klik kanan, definisi kolom, dan `Border` pulau beserta jamnya) dengan:

```xml
    <Grid>
        <Grid.ContextMenu>
            <ContextMenu x:Name="BarMenu" Style="{StaticResource BarMenu}">
                <MenuItem Header="Settings" Style="{StaticResource BarMenuItem}" Click="OnSettings" />
                <Separator Style="{StaticResource BarMenuSeparator}" />
                <MenuItem Header="Quit" Style="{StaticResource BarMenuItem}" Click="OnQuit" />
            </ContextMenu>
        </Grid.ContextMenu>
        <!-- The backdrop fades on its own while the island floats; the content above it stays (PRD 6.3). -->
        <Border x:Name="Backdrop" Background="{Binding Tint}" BorderBrush="#0FFFFFFF" BorderThickness="0,0,0,1" />
        <Grid x:Name="BarContent" Margin="10,0">
            <Grid.ColumnDefinitions>
                <!-- Equal star columns keep the middle centred on the screen, under the island in IslandWindow. -->
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <StackPanel x:Name="RightModules" Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
```

dan ganti tiga baris terakhir file:

```xml
        </Grid>
    </Border>
</Window>
```

dengan:

```xml
        </Grid>
    </Grid>
</Window>
```

- [ ] **Step 6: `BarWindow.xaml.cs`**

Ganti seluruh isi `src/Gili.App/Bar/BarWindow.xaml.cs`:

```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using Gili.Core.Island;
using Gili.Core.Settings;
using Gili.Platform.Interop;
using Gili.Platform.Shell;

namespace Gili.App.Bar;

public partial class BarWindow : Window
{
    private static readonly DropShadowEffect TextShadow = CreateTextShadow();

    private readonly SettingsHub _settings;
    private readonly TransparencyWatcher _transparency;
    private readonly StatsService _stats;
    private readonly BarViewModel _vm = new();
    private AppBarService? _appBar;
    private IntPtr _hwnd;
    private bool _quitting;
    private bool _hidden;
    private bool _suspended;
    private bool _floating;
    private bool _glassOff;

    public BarWindow(SettingsHub settings, TransparencyWatcher transparency, StatsService stats)
    {
        _settings = settings;
        _transparency = transparency;
        _stats = stats;
        InitializeComponent();
        DataContext = _vm;
        _settings.Changed += OnSettingsChanged;
        _transparency.Changed += _ => Dispatcher.InvokeAsync(ApplyAppearance);
        _stats.Updated += _vm.ApplyStats;
#if DEBUG
        var crash = new MenuItem { Header = "Simulate crash", Style = (Style)FindResource("BarMenuItem") };
        crash.Click += (_, _) => throw new InvalidOperationException("Simulated crash from the bar menu");
        BarMenu.Items.Add(crash);
#endif
    }

    public event Action? QuitRequested;

    public event Action? SettingsRequested;

    /// <summary>The system clock or time zone changed, or the laptop woke up.</summary>
    public event Action? TimeChanged;

    public event Action<bool>? FullscreenChanged;

    public void ReleaseAppBar() => _appBar?.Unregister();

    public void Reassert() => _appBar?.Reposition();

    /// <summary>
    /// While the island floats only the bar's backdrop goes away (PRD 6.3): the tint fades in 220 ms, then the glass is
    /// switched off because the accent policy cannot fade. Content stays with a text shadow, except what the panel covers.
    /// </summary>
    public void SetIslandFloating(bool floating, double islandWidth)
    {
        UpdateCovered(floating ? islandWidth : 0);
        if (floating == _floating) return;
        _floating = floating;
        BarContent.Effect = floating ? TextShadow : null;
        var fade = new DoubleAnimation(floating ? 0 : 1, TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? 220 : 100));
        if (floating)
        {
            fade.Completed += (_, _) =>
            {
                if (!_floating) return;
                _glassOff = true;
                ApplyAppearance();
            };
        }
        else
        {
            _glassOff = false;
            ApplyAppearance();
        }
        Backdrop.BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        WindowStyles.MakeNonActivatingToolWindow(_hwnd);
        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);
        _appBar = new AppBarService(_hwnd);
        _appBar.FullscreenChanged += OnFullscreenChanged;
        _appBar.Register(_settings.Current.BarHeight);
        ApplyAppearance();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Whatever closes the bar from outside also quits the app, so no process is left running without a bar.
        if (!_quitting)
        {
            _quitting = true;
            QuitRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    private static DropShadowEffect CreateTextShadow()
    {
        var shadow = new DropShadowEffect { Color = Colors.Black, Opacity = 0.55, BlurRadius = 3, ShadowDepth = 1, Direction = 270 };
        shadow.Freeze();
        return shadow;
    }

    /// <summary>Each element under the floating panel (+8 px each side) fades in 160 ms and stops taking clicks.</summary>
    private void UpdateCovered(double islandWidth)
    {
        var left = ActualWidth / 2 - islandWidth / 2;
        var right = ActualWidth / 2 + islandWidth / 2;
        foreach (var element in RightModules.Children.OfType<FrameworkElement>())
        {
            var x = element.TranslatePoint(new Point(0, 0), this).X;
            var covered = islandWidth > 0 && IslandLayout.Covers(left, right, x, x + element.ActualWidth);
            if (element.IsHitTestVisible == !covered) continue;
            element.IsHitTestVisible = !covered;
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(covered ? 0 : 1, TimeSpan.FromMilliseconds(160)));
        }
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _appBar?.SetHeight(settings.BarHeight);
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        var look = BarAppearance.Resolve(_settings.Current, _transparency.Enabled);
        BarMaterial.Apply(_hwnd, _glassOff ? GlassLevel.Off : look.Glass);
        _vm.ApplySettings(_settings.Current, look);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _hidden = fullscreen;
        UpdatePolling();
        FullscreenChanged?.Invoke(fullscreen);
        if (fullscreen)
        {
            Hide();
            return;
        }
        Show();
        _appBar?.Reposition();
    }

    private void UpdatePolling() => _stats.Paused = _hidden || _suspended;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_appBar?.HandleMessage(msg, wParam, lParam) == true)
        {
            handled = true;
            return IntPtr.Zero;
        }
        switch (msg)
        {
            case WindowMessages.WM_TIMECHANGE:
                // Also sent when the time zone changes; DateTime.Now caches the zone.
                TimeZoneInfo.ClearCachedData();
                TimeChanged?.Invoke();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMSUSPEND:
                _suspended = true;
                UpdatePolling();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _suspended = false;
                UpdatePolling();
                TimeChanged?.Invoke();
                break;
        }
        return IntPtr.Zero;
    }

    /// <summary>Until the System page exists (Phase 3), CPU and RAM open Task Manager (PRD 6.3).</summary>
    private void OnOpenTaskManager(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            // The user declined the UAC prompt.
        }
    }

    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
```

- [ ] **Step 7: Rakit di `App.xaml.cs`**

Di `src/Gili.App/App.xaml.cs`:
- tambahkan `using Gili.App.Island;` di bawah `using Gili.App.Bar;`;
- tambahkan field di bawah `private StatsService? _stats;`:

```csharp
    private ClockService? _clock;
    private MediaService? _media;
    private IslandController? _islandController;
    private IslandWindow? _islandWindow;
```

- ganti blok

```csharp
        _bar = new BarWindow(_settings, _transparency, _stats);
        _bar.SettingsRequested += ShowSettings;
        _bar.QuitRequested += Quit;
        _bar.Show();
        _stats.Start();
```

dengan

```csharp
        _clock = new ClockService();
        _media = new MediaService(Dispatcher, _log);
        _islandController = new IslandController(Dispatcher, _media);

        _bar = new BarWindow(_settings, _transparency, _stats);
        _bar.SettingsRequested += ShowSettings;
        _bar.QuitRequested += Quit;
        _bar.TimeChanged += _clock.Refresh;
        _bar.FullscreenChanged += _islandController.SetFullscreen;
        _bar.Show();

        // Owned by the bar, so the island stays above it whenever the bar re-asserts its topmost place.
        _islandWindow = new IslandWindow(_islandController, new IslandViewModel(_islandController, _clock, _settings), _settings)
        {
            Owner = _bar,
        };
        _islandWindow.FloatingChanged += _bar.SetIslandFloating;
        _islandWindow.Show();
        _stats.Start();
        _ = _media.StartAsync();
```

- di `OnExit`, ganti baris `_stats?.Dispose();` dengan:

```csharp
        _stats?.Dispose();
        _islandController?.Dispose();
        _media?.Dispose();
```

- [ ] **Step 8: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua test PASS.

- [ ] **Step 9: Periksa di laptop**

`dotnet run --project src/Gili.App`, lalu:
1. Pulau compact tampil seperti di 1a (jam + tanggal, minimal 200 px) dan bar tetap memesan ruang 32 px. Tepi atas bar tidak lagi punya pulau sendiri di bawah pulau baru.
2. Hover pulau → melebar 16 px dengan pegas; mouse pergi → kembali.
3. Putar musik di Spotify atau YouTube Music → slot: bar level + `Judul – Artis` + tombol pause; tombol menghentikan dan melanjutkan musik tanpa membuka pulau. Pause → slot bertahan 2 menit lalu jam kembali.
4. Putar video di browser atau Media Player → ikon film oranye + judul + garis progres. VLC 3 → ikon film + nama file, tanpa garis progres.
5. Laptop sedang dicas → slot petir + persen (atau gelembung petir di kanan bila ada musik). Cabut charger → wide `On battery · 63%` 2 detik; colok → `Charging · 64%` 3 detik.
6. Klik pulau → panel hitam 680 × 256 mengambang 8 px dari atas dengan sedikit pantulan (masih kosong sampai Task 10); latar bar memudar, isi bar tetap tampil dengan bayangan teks, dan modul kanan yang tertimpa panel memudar. Esc, klik di luar, atau mouse di luar panel 3 detik → kembali ke compact tanpa pantulan, dan jendela yang tadi aktif mendapat fokus lagi.
7. Video fullscreen → bar dan pulau hilang; keluar fullscreen → keduanya kembali.
8. Settings › Bar height / Island height → pulau ikut berubah dengan pegas.
9. Windows Settings › Accessibility › Visual effects › Animation effects mati → transisi menjadi cross-fade tanpa pegas.

- [ ] **Step 10: Commit**

```bash
git add src/Gili.App
git commit -m "feat(app): island window with springs, compact and wide content, bubble, and bar backdrop fade (F-ISL-01, F-ISL-02, F-ISL-04..06)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Pulau terbuka: menu tab, halaman, baris jam, navigasi

**Files:**
- Modify: `src/Gili.App/Island/IslandWindow.xaml`, `src/Gili.App/Island/IslandWindow.xaml.cs`

**Interfaces:**
- Consumes: `IslandViewModel.Tabs`, `SelectedTab`, `MusicPlaying`, `Time`, `Date` (Task 8); `IslandController.Open` (Task 8); `IconView`, `LevelMeter` (Task 7); `WindowMessages.WM_MOUSEHWHEEL` (Task 6).
- Produces: `IslandWindow.CreatePage(IslandTab tab)` → `FrameworkElement`, tempat halaman nyata menggantikan placeholder (Task 11 untuk Media).

- [ ] **Step 1: Gaya tombol tab dan baris jam**

Di `src/Gili.App/Island/IslandWindow.xaml`, tambahkan tepat sebelum `</Window.Resources>`:

```xml
        <Style x:Key="TabButton" TargetType="Button">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#8E8E93" />
            <Setter Property="Margin" Value="1,0" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="Transparent" CornerRadius="15">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Foreground" Value="#F5F5F7" />
                </Trigger>
            </Style.Triggers>
        </Style>
        <Style x:Key="FooterButton" TargetType="Button">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#8E8E93" />
            <Setter Property="FontSize" Value="12" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bg" Height="22" Padding="10,0" CornerRadius="11" Background="Transparent">
                            <ContentPresenter VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#1C1C1E" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
```

- [ ] **Step 2: Isi `ExpandedLayer`**

Di file yang sama, ganti

```xml
                <!-- Filled in Task 10: page menu, pages and the clock line. -->
                <Grid x:Name="ExpandedLayer" Width="680" Height="256" HorizontalAlignment="Center" VerticalAlignment="Top"
                      Opacity="0" IsHitTestVisible="False" />
```

dengan

```xml
                <!-- The open island (PRD 6.3): page menu, the page itself, and a quiet clock line at the bottom. -->
                <Grid x:Name="ExpandedLayer" Width="680" Height="256" HorizontalAlignment="Center" VerticalAlignment="Top"
                      Opacity="0" IsHitTestVisible="False">
                    <!-- 14 px inset + 18 px radius = the island's 32 px, so the menu's curve follows the island's. -->
                    <Border Margin="14,14,14,0" Height="36" VerticalAlignment="Top" CornerRadius="18" Background="#1C1C1E" Padding="3">
                        <Grid>
                            <!-- 3 px inset + 15 px radius = the menu's 18 px. -->
                            <Border x:Name="TabHighlight" HorizontalAlignment="Left" CornerRadius="15" Background="#3A3A3C">
                                <Border.RenderTransform>
                                    <TranslateTransform x:Name="TabHighlightShift" />
                                </Border.RenderTransform>
                            </Border>
                            <UniformGrid x:Name="TabStrip" Rows="1" />
                        </Grid>
                    </Border>

                    <Canvas Margin="0,58,0,0" Height="164" VerticalAlignment="Top" ClipToBounds="True" Background="Transparent"
                            MouseWheel="OnPagesWheel">
                        <StackPanel x:Name="PageTrack" Orientation="Horizontal" Height="164">
                            <StackPanel.RenderTransform>
                                <TranslateTransform x:Name="PageShift" />
                            </StackPanel.RenderTransform>
                        </StackPanel>
                    </Canvas>

                    <Button Style="{StaticResource FooterButton}" HorizontalAlignment="Center" VerticalAlignment="Bottom"
                            Margin="0,0,0,8" Click="OnFooterClick" AutomationProperties.Name="Open calendar">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="{Binding Time}" Foreground="#F5F5F7" FontWeight="SemiBold" Typography.NumeralAlignment="Tabular" />
                            <TextBlock Text="{Binding Date}" Margin="6,0,0,0" />
                        </StackPanel>
                    </Button>
                </Grid>
```

- [ ] **Step 3: Kode menu, halaman, dan navigasi**

Di `src/Gili.App/Island/IslandWindow.xaml.cs`:

1. Tambahkan using:

```csharp
using System.ComponentModel;
using System.Windows.Automation;
using System.Windows.Controls;
using Gili.App.Controls;
using Gili.Platform.Interop;
```

   Karena `System.Windows.Controls` kini di-import, ganti setiap `System.Windows.Controls.Canvas.` di method `Apply` menjadi `Canvas.`.

2. Tambahkan field di bawah `private IntPtr _previousForeground;`:

```csharp
    private static readonly Brush TextBrush = Frozen(0xF5, 0xF5, 0xF7);
    private static readonly Brush SecondaryBrush = Frozen(0x8E, 0x8E, 0x93);
    private static readonly Brush AccentBrush = Frozen(0x30, 0xD1, 0x58);
    private readonly List<(Button Button, IconView Icon, TextBlock Label, LevelMeter? Meter)> _tabs = [];
    private int _wheel;
    private DateTime _wheelPausedUntil;
```

3. Di konstruktor, tambahkan tepat setelah `Bubble.RenderTransform = _bubbleScale;`:

```csharp
        ExpandedLayer.Width = Math.Min(680, SystemParameters.PrimaryScreenWidth - IslandLayout.ScreenMargin);
        BuildTabs();
        BuildPages();
        ShowPage(animate: false);
        vm.PropertyChanged += OnViewModelChanged;
        TabStrip.SizeChanged += (_, _) => UpdateTabs(animate: false);
```

4. Di `OnSourceInitialized`, tambahkan setelah `WindowStyles.MakeNonActivatingToolWindow(_hwnd);`:

```csharp
        HwndSource.FromHwnd(_hwnd)!.AddHook(WndProc);
```

5. Ganti method `OnPreviewKeyDown` dengan:

```csharp
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_mode != IslandMode.Expanded) return;
        switch (e.Key)
        {
            case Key.Escape:
                _island.Close();
                break;
            case Key.Left:
                StepPage(-1);
                break;
            case Key.Right:
                StepPage(1);
                break;
            default:
                return;
        }
        e.Handled = true;
    }
```

6. Tambahkan method berikut di akhir class:

```csharp
    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private void BuildTabs()
    {
        foreach (var tab in _vm.Tabs)
        {
            var icon = new IconView { Glyph = tab.Glyph, Width = 15, Height = 15 };
            var meter = tab.Page == IslandPage.Media ? new LevelMeter { BarBrush = AccentBrush, Visibility = Visibility.Collapsed } : null;
            var glyph = new Grid { MinWidth = 15, Height = 15, VerticalAlignment = VerticalAlignment.Center };
            glyph.Children.Add(icon);
            if (meter is not null) glyph.Children.Add(meter);
            var label = new TextBlock { Text = tab.Name, FontSize = 12, Margin = new Thickness(5, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var content = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(glyph);
            content.Children.Add(label);
            var button = new Button { Style = (Style)FindResource("TabButton"), Content = content };
            AutomationProperties.SetName(button, tab.Name);
            button.Click += (_, _) => _island.Open(tab.Page);
            TabStrip.Children.Add(button);
            _tabs.Add((button, icon, label, meter));
        }
    }

    private void BuildPages()
    {
        foreach (var tab in _vm.Tabs)
        {
            PageTrack.Children.Add(new Border { Width = ExpandedLayer.Width, Height = 164, Child = CreatePage(tab) });
        }
    }

    /// <summary>Real pages replace their placeholders here as they are built (Media in Task 11, the rest in Phases 2–3).</summary>
    private FrameworkElement CreatePage(IslandTab tab) => Placeholder(tab);

    private static FrameworkElement Placeholder(IslandTab tab)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock { Text = tab.Name, FontSize = 19, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        panel.Children.Add(new TextBlock
        {
            Text = tab.Placeholder ?? "",
            FontSize = 13,
            Foreground = SecondaryBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        });
        return panel;
    }

    /// <summary>
    /// One highlight slides to the open page's tab (320 ms); that tab's text turns white and semibold and its icon
    /// green. While music plays, the Media icon becomes the level meter. On a narrow screen only the open tab keeps its name.
    /// </summary>
    private void UpdateTabs(bool animate)
    {
        if (_tabs.Count == 0 || TabStrip.ActualWidth <= 0) return;
        var selected = _vm.SelectedTab;
        var narrow = ExpandedLayer.Width < 680;
        for (var i = 0; i < _tabs.Count; i++)
        {
            var (button, icon, label, meter) = _tabs[i];
            var active = i == selected;
            if (active) button.Foreground = TextBrush;
            else button.ClearValue(ForegroundProperty);
            if (active) icon.Foreground = AccentBrush;
            else icon.ClearValue(IconView.ForegroundProperty);
            label.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            label.Visibility = active || !narrow ? Visibility.Visible : Visibility.Collapsed;
            var playing = meter is not null && _vm.MusicPlaying;
            icon.Visibility = playing ? Visibility.Collapsed : Visibility.Visible;
            if (meter is null) continue;
            meter.Visibility = playing ? Visibility.Visible : Visibility.Collapsed;
            meter.IsPlaying = playing;
        }
        var cell = TabStrip.ActualWidth / _tabs.Count;
        TabHighlight.Width = cell - 2;
        Slide(TabHighlightShift, selected * cell + 1, 320, animate, new CubicEase { EasingMode = EasingMode.EaseOut });
    }

    private void ShowPage(bool animate) =>
        Slide(PageShift, -_vm.SelectedTab * ExpandedLayer.Width, 300, animate,
            new ExponentialEase { EasingMode = EasingMode.EaseOut, Exponent = 7 });

    private static void Slide(TranslateTransform shift, double x, int milliseconds, bool animate, IEasingFunction ease)
    {
        if (!animate || !SystemParameters.ClientAreaAnimation)
        {
            shift.BeginAnimation(TranslateTransform.XProperty, null);
            shift.X = x;
            return;
        }
        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(x, TimeSpan.FromMilliseconds(milliseconds)) { EasingFunction = ease });
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IslandViewModel.SelectedTab):
                // Opening the island lands on its page; only switching while open slides.
                var animate = _mode == IslandMode.Expanded;
                UpdateTabs(animate);
                ShowPage(animate);
                break;
            case nameof(IslandViewModel.MusicPlaying):
                UpdateTabs(animate: false);
                break;
        }
    }

    private void StepPage(int direction)
    {
        var next = Math.Clamp(_vm.SelectedTab + direction, 0, _vm.Tabs.Count - 1);
        if (next != _vm.SelectedTab) _island.Open(_vm.Tabs[next].Page);
    }

    private void OnPagesWheel(object sender, MouseWheelEventArgs e)
    {
        Scroll(-e.Delta);
        e.Handled = true;
    }

    /// <summary>Wheel and touchpad deltas add up to one page per notch (120), then pause while the page slides.</summary>
    private void Scroll(int delta)
    {
        if (DateTime.UtcNow < _wheelPausedUntil) return;
        _wheel += delta;
        if (Math.Abs(_wheel) < 120) return;
        StepPage(Math.Sign(_wheel));
        _wheel = 0;
        _wheelPausedUntil = DateTime.UtcNow.AddMilliseconds(300);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Horizontal touchpad swipes and wheel tilts arrive only as WM_MOUSEHWHEEL; WPF has no event for them.
        if (msg == WindowMessages.WM_MOUSEHWHEEL && _mode == IslandMode.Expanded)
        {
            Scroll((short)((wParam.ToInt64() >> 16) & 0xFFFF));
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OnFooterClick(object sender, RoutedEventArgs e) => _island.Open(IslandPage.Calendar);
```

- [ ] **Step 4: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua test PASS.

- [ ] **Step 5: Periksa di laptop**

`dotnet run --project src/Gili.App`, klik pulau, lalu:
1. Menu kapsul selebar pulau berisi 8 tab (Media, Timer, Alarms, Calendar, Todo, Notes, Shelf, System) dengan ikon + nama; tab Media aktif: sorotan abu-abu, teks putih semibold, ikon hijau. Di bawah tengah ada `19:38 Mon 26 Oct`.
2. Klik tab Notes → sorotan bergeser 320 ms dan halaman bergeser 300 ms → `Notes` / `Coming in Phase 2`. Hover tab lain → teksnya memutih.
3. ← dan → berpindah halaman; berhenti di ujung. Roda mouse di area halaman dan swipe horizontal touchpad juga berpindah halaman, satu halaman per gerakan.
4. Klik baris jam → halaman Calendar.
5. Tutup lalu buka lagi → terbuka di halaman terakhir (Calendar); dengan musik di slot → terbuka di Media.
6. Saat musik diputar, ikon tab Media berganti bar level hijau yang bergerak.
7. Isi pulau muncul dengan fade + blur singkat setelah panel mulai membesar, tidak muncul sebelum panel cukup besar.

- [ ] **Step 6: Commit**

```bash
git add src/Gili.App/Island
git commit -m "feat(app): expanded island menu, sliding pages, clock line and page navigation (F-ISL-07)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Halaman Media

**Files:**
- Create: `src/Gili.App/Island/MediaPage.xaml`, `src/Gili.App/Island/MediaPage.xaml.cs`
- Modify: `src/Gili.App/Island/IslandWindow.xaml.cs` (`CreatePage`)

**Interfaces:**
- Consumes: properti media `IslandViewModel` (Task 8, mewarisi `DataContext` dari `IslandWindow`); `MediaService` (Task 8).
- Produces: `MediaPage(MediaService media) : UserControl`.

- [ ] **Step 1: `MediaPage.xaml`**

`src/Gili.App/Island/MediaPage.xaml`:

```xml
<UserControl x:Class="Gili.App.Island.MediaPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:c="clr-namespace:Gili.App.Controls">
    <UserControl.Resources>
        <BooleanToVisibilityConverter x:Key="VisibleWhen" />
        <c:VisibleWhenFalse x:Key="VisibleWhenFalse" />
        <Style x:Key="ControlButton" TargetType="Button">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#F5F5F7" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bg" Width="40" Height="40" CornerRadius="20" Background="Transparent" RenderTransformOrigin="0.5,0.5">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#2C2C2E" />
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="Bg" Property="RenderTransform">
                                    <Setter.Value>
                                        <ScaleTransform ScaleX="0.9" ScaleY="0.9" />
                                    </Setter.Value>
                                </Setter>
                            </Trigger>
                            <Trigger Property="IsEnabled" Value="False">
                                <Setter TargetName="Bg" Property="Opacity" Value="0.35" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="MainButton" TargetType="Button">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#000000" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="Bg" Width="52" Height="52" CornerRadius="26" Background="#F5F5F7" RenderTransformOrigin="0.5,0.5">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#D1D1D6" />
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="Bg" Property="RenderTransform">
                                    <Setter.Value>
                                        <ScaleTransform ScaleX="0.9" ScaleY="0.9" />
                                    </Setter.Value>
                                </Setter>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </UserControl.Resources>

    <Grid>
        <TextBlock Text="Nothing playing" FontSize="13" Foreground="#8E8E93" HorizontalAlignment="Center" VerticalAlignment="Center"
                   Visibility="{Binding HasMedia, Converter={StaticResource VisibleWhenFalse}}" />

        <!-- Music: cover, source app, title, artist, progress, previous / play-pause / next (PRD 6.3). -->
        <DockPanel Margin="32,0" VerticalAlignment="Center" Visibility="{Binding IsMusic, Converter={StaticResource VisibleWhen}}">
            <Border DockPanel.Dock="Left" Width="112" Height="112" CornerRadius="16" Background="#2C2C2E">
                <Grid>
                    <c:IconView Glyph="{StaticResource Icon.Music}" Width="38" Height="38" Foreground="#D9FFFFFF" />
                    <Border CornerRadius="16" Visibility="{Binding HasCover, Converter={StaticResource VisibleWhen}}">
                        <Border.Background>
                            <ImageBrush ImageSource="{Binding Cover}" Stretch="UniformToFill" />
                        </Border.Background>
                    </Border>
                </Grid>
            </Border>
            <StackPanel DockPanel.Dock="Right" Orientation="Horizontal" VerticalAlignment="Center" Margin="24,0,0,0">
                <Button Style="{StaticResource ControlButton}" Click="OnPrevious" IsEnabled="{Binding CanSkip}"
                        AutomationProperties.Name="Previous track">
                    <c:IconView Glyph="{StaticResource Icon.Prev}" Filled="True" Width="20" Height="20" />
                </Button>
                <Button Style="{StaticResource MainButton}" Margin="8,0" Click="OnPlayPause" AutomationProperties.Name="Play or pause">
                    <Grid>
                        <c:IconView Glyph="{StaticResource Icon.Pause}" Filled="True" Width="20" Height="20"
                                    Visibility="{Binding IsPlaying, Converter={StaticResource VisibleWhen}}" />
                        <c:IconView Glyph="{StaticResource Icon.Play}" Filled="True" Width="20" Height="20"
                                    Visibility="{Binding IsPlaying, Converter={StaticResource VisibleWhenFalse}}" />
                    </Grid>
                </Button>
                <Button Style="{StaticResource ControlButton}" Click="OnNext" IsEnabled="{Binding CanSkip}"
                        AutomationProperties.Name="Next track">
                    <c:IconView Glyph="{StaticResource Icon.Next}" Filled="True" Width="20" Height="20" />
                </Button>
            </StackPanel>
            <StackPanel Margin="24,0,0,0" VerticalAlignment="Center">
                <StackPanel Orientation="Horizontal">
                    <Ellipse Width="8" Height="8" Fill="{Binding LevelBrush}" VerticalAlignment="Center" />
                    <TextBlock Text="{Binding MediaApp}" Margin="6,0,0,0" FontSize="12" Foreground="#8E8E93" />
                </StackPanel>
                <TextBlock Text="{Binding MediaTitle}" FontSize="22" FontWeight="Bold" Margin="0,4,0,0" TextTrimming="CharacterEllipsis" />
                <TextBlock Text="{Binding MediaArtist}" FontSize="14" Foreground="#8E8E93" TextTrimming="CharacterEllipsis" />
                <DockPanel Margin="0,14,0,0" TextElement.FontSize="11" TextElement.Foreground="#8E8E93"
                           Visibility="{Binding HasTimeline, Converter={StaticResource VisibleWhen}}">
                    <TextBlock DockPanel.Dock="Left" Text="{Binding PositionText}" Typography.NumeralAlignment="Tabular" VerticalAlignment="Center" />
                    <TextBlock DockPanel.Dock="Right" Text="{Binding DurationText}" Typography.NumeralAlignment="Tabular" VerticalAlignment="Center" />
                    <!-- 12 px tall hit area around the 4 px bar, so seeking does not need a pixel-perfect click. -->
                    <Grid Height="12" Margin="10,0" Background="Transparent" MouseLeftButtonUp="OnSeek">
                        <Border Height="4" CornerRadius="2" Background="#2C2C2E" />
                        <Border Height="4" CornerRadius="2" Background="#F5F5F7" RenderTransformOrigin="0,0.5">
                            <Border.RenderTransform>
                                <ScaleTransform ScaleX="{Binding Progress}" />
                            </Border.RenderTransform>
                        </Border>
                    </Grid>
                </DockPanel>
            </StackPanel>
        </DockPanel>

        <!-- Video: only the title, source app and progress with elapsed and remaining time, no cover and no buttons (PRD 6.3). -->
        <StackPanel MaxWidth="520" Margin="32,0" VerticalAlignment="Center" Visibility="{Binding IsVideo, Converter={StaticResource VisibleWhen}}">
            <StackPanel Orientation="Horizontal">
                <Ellipse Width="8" Height="8" Fill="#FF9F0A" VerticalAlignment="Center" />
                <TextBlock Text="{Binding MediaApp}" Margin="6,0,0,0" FontSize="12" Foreground="#8E8E93" />
            </StackPanel>
            <TextBlock Text="{Binding MediaTitle}" FontSize="24" FontWeight="Bold" Margin="0,6,0,0" TextWrapping="Wrap"
                       TextTrimming="CharacterEllipsis" LineStackingStrategy="BlockLineHeight" LineHeight="28.8" MaxHeight="57.6" />
            <DockPanel Margin="0,18,0,0" TextElement.FontSize="11" TextElement.Foreground="#8E8E93"
                       Visibility="{Binding HasTimeline, Converter={StaticResource VisibleWhen}}">
                <TextBlock DockPanel.Dock="Left" Text="{Binding PositionText}" Typography.NumeralAlignment="Tabular" VerticalAlignment="Center" />
                <TextBlock DockPanel.Dock="Right" Text="{Binding RemainingText}" Typography.NumeralAlignment="Tabular" VerticalAlignment="Center" />
                <Grid Height="4" Margin="10,0">
                    <Border CornerRadius="2" Background="#2C2C2E" />
                    <Border CornerRadius="2" Background="#F5F5F7" RenderTransformOrigin="0,0.5">
                        <Border.RenderTransform>
                            <ScaleTransform ScaleX="{Binding Progress}" />
                        </Border.RenderTransform>
                    </Border>
                </Grid>
            </DockPanel>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 2: `MediaPage.xaml.cs`**

`src/Gili.App/Island/MediaPage.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Gili.App.Island;

public partial class MediaPage : UserControl
{
    private readonly MediaService _media;

    public MediaPage(MediaService media)
    {
        _media = media;
        InitializeComponent();
    }

    private void OnPlayPause(object sender, RoutedEventArgs e) => _ = _media.TogglePlayPauseAsync();

    private void OnPrevious(object sender, RoutedEventArgs e) => _ = _media.PreviousAsync();

    private void OnNext(object sender, RoutedEventArgs e) => _ = _media.NextAsync();

    /// <summary>Clicking the progress bar seeks there when the source allows it (PRD 6.3).</summary>
    private void OnSeek(object sender, MouseButtonEventArgs e)
    {
        if (_media.Current is not { CanSeek: true, HasTimeline: true } media) return;
        var bar = (FrameworkElement)sender;
        var fraction = Math.Clamp(e.GetPosition(bar).X / bar.ActualWidth, 0, 1);
        _ = _media.SeekAsync(media.Duration * fraction);
        e.Handled = true;
    }
}
```

- [ ] **Step 3: Pakai halaman Media**

Di `src/Gili.App/Island/IslandWindow.xaml.cs`, ganti

```csharp
    private FrameworkElement CreatePage(IslandTab tab) => Placeholder(tab);
```

dengan

```csharp
    private FrameworkElement CreatePage(IslandTab tab) =>
        tab.Page == IslandPage.Media ? new MediaPage(_island.Media) : Placeholder(tab);
```

- [ ] **Step 4: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua test PASS.

- [ ] **Step 5: Periksa di laptop**

`dotnet run --project src/Gili.App`, lalu:
1. Spotify memutar lagu → buka pulau → halaman Media: cover, `● Spotify`, judul 22 px tebal, artis, progres dengan waktu berjalan dan durasi, tombol ⏮ ⏯ ⏭ (tombol tengah putih).
2. ⏯ menghentikan/melanjutkan; ⏭ ganti lagu → judul dan cover di slot dan halaman berganti ≤ 1 detik (F-ISL-04).
3. Klik di tengah garis progres → lagu loncat ke sekitar pertengahan.
4. YouTube di Chrome atau Edge → judul dan nama channel tampil, sumber `Google Chrome` atau `Microsoft Edge`.
5. Video di Media Player atau di browser yang melapor sebagai video → judul 24 px maksimal dua baris, garis progres dengan waktu berjalan dan sisa waktu (`-2:10`), tanpa cover dan tanpa tombol.
6. Tidak ada media → `Nothing playing`.

- [ ] **Step 6: Commit**

```bash
git add src/Gili.App/Island
git commit -m "feat(app): media page with cover, controls and seek; video layout (F-ISL-04, F-ISL-05, F-ISL-07)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Verifikasi Fase 1b dan dokumentasi

**Files:**
- Modify: `CLAUDE.md`, `docs/superpowers/specs/2026-10-25-top-taskbar-prd.md`

- [ ] **Step 1: CLAUDE.md**

Di bagian "Arsitektur" `CLAUDE.md`, tambahkan setelah butir **Dua jendela, bukan satu.**:

```markdown
- **Pulau selalu di `IslandWindow`.** Jendela transparan topmost dengan `Owner` = `BarWindow`, supaya tetap di atas bar setiap kali bar menegaskan ulang posisi topmost-nya. `BarWindow` mengosongkan bagian tengah, dan saat pulau mengambang hanya memudarkan latarnya serta elemen yang tertimpa. Ukuran `IslandWindow` adalah envelope: setinggi bar saat compact, cukup besar untuk panel saat mengambang.
- **Geometri pulau digerakkan `Spring` (Core), bukan Storyboard.** Lebar, tinggi, posisi atas, dan radius di-step per frame lewat `CompositionTarget.Rendering`, hanya selama ada pegas yang bergerak. Mengganti target pegas menjaga posisi dan kecepatan, jadi transisi selalu bisa disela (PRD 8.3). `IslandController` menghubungkan provider ke `IslandCoordinator` dan menjalankan timer tenggatnya; `IslandViewModel` hanya menyiapkan data tampilan.
```

- [ ] **Step 2: Catat keputusan di PRD**

Di PRD 6.3, di bawah daftar "Urutan prioritas aktivitas", tambahkan butir:

```markdown
- Media yang di-pause tetap memegang slot selama 2 menit setelah sebelumnya diputar, lalu jam kembali; session yang ditemukan dalam keadaan pause saat Gili mulai tidak memegang slot.
```

Di PRD 6.3 bagian "Halaman expanded", di bawah tabel halaman, tambahkan:

```markdown
Tab untuk halaman yang belum dibangun tetap tampil dengan isi `Coming in Phase 2` atau `Coming in Phase 3`; tab Claude baru muncul di Fase 3 bersama integrasinya.
```

- [ ] **Step 3: Verifikasi lengkap**

Run:

```bash
dotnet build Gili.slnx -c Release
dotnet test tests/Gili.Core.Tests
dotnet format Gili.slnx --verify-no-changes
```

Expected: build sukses, semua test PASS, format tanpa perubahan (kalau ada, jalankan `dotnet format Gili.slnx` lalu sertakan di commit).

Lalu dengan `dotnet run --project src/Gili.App` (tanpa memasang ke Programs), jalankan checklist Fase 1b dari PRD 13:
- Spotify dan YouTube di Chrome/Edge; video di Media Player dan VLC; tombol kontrol dan seek.
- Colok/cabut charger; musik + ngecas bersamaan (gelembung).
- Pulau terbuka/tertutup lewat klik, Esc, klik di luar, mouse pergi 3 detik; transisi bisa disela (klik pulau lalu Esc sebelum animasi selesai → menyusut dari ukuran saat itu).
- Video fullscreen (YouTube, game); DPI 100/125/150%; lebar 1366 px (panel 680 px muat, modul kanan yang tertimpa memudar).
- Animation effects mati; Transparency effects mati saat pulau terbuka.
- Task Manager setelah 10 menit musik diputar: Gili CPU < 5% saat animasi bar level, < 1% saat diam tanpa musik; RAM < 120 MB.

- [ ] **Step 4: Commit**

```bash
git add CLAUDE.md docs/superpowers/specs
git commit -m "docs: island architecture in CLAUDE.md and Phase 1b decisions in the PRD" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
