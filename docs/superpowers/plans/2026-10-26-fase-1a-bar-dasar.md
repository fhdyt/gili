# Fase 1a: Bar Dasar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Bar kaca 32 px di tepi atas monitor utama yang memesan ruang AppBar, menampilkan jam + tanggal di pulau compact dan CPU/RAM/jaringan bergaya dua baris di kanan, dengan Settings untuk tampilan dan autostart, dan pulih sendiri dari crash.

**Architecture:** Logika yang bisa dites (format jam dan angka, aturan settings, backoff provider, kebijakan crash) ada di `Gili.Core` tanpa WPF/Win32 dan dites dengan xUnit. `Gili.Data` menyimpan `settings.json`. `Gili.Platform` membungkus Win32/WinRT (AppBar, kaca, transparansi, autostart, sampler CPU/RAM/jaringan). `Gili.App` (WPF) menyusun semuanya: `BarWindow` + ViewModel, `SettingsWindow`, satu instance, dan penanganan crash.

**Tech Stack:** .NET 10 (`net10.0`, `net10.0-windows10.0.19041.0`), C# WPF, CommunityToolkit.Mvvm, System.Diagnostics.PerformanceCounter, xUnit.

**Spec:** [docs/superpowers/specs/2026-10-25-top-taskbar-prd.md](../specs/2026-10-25-top-taskbar-prd.md)

**Nama:** rencana ini ditulis dan dieksekusi dengan nama lama `TopBar`. Setelah eksekusi, semua rujukan di sini diganti ke **Gili** agar cocok dengan kode; commit 1a di riwayat git masih memakai path `src/TopBar.*`.

## Global Constraints

- Semua teks UI bahasa Inggris, persis seperti di PRD. Format angka dan tanggal selalu `en-US`/invariant, tidak pernah mengikuti locale Windows (misalnya `2.1 MB/s`, bukan `2,1 MB/s`).
- `Gili.Core` tidak boleh mereferensikan WPF, WinRT, atau P/Invoke.
- App jalan tanpa hak admin. Data di `%LOCALAPPDATA%\Gili\` (`settings.json`, `logs\`).
- Bar: tint `#161618`, opacity default 78% (40–100), kaca Off/Light/Strong (default Strong), garis bawah putih 6%. Tinggi bar 28–44 px (default 32). Tinggi pulau 20 px sampai tinggi bar − 4 (default 24).
- Pulau: `#000000`, lebar minimal 200 px, radius = setengah tinggi pulau, ukuran teks = setengah tinggi pulau dibatasi 11–15 px. Jam semibold `#F5F5F7`, tanggal regular `#8E8E93`, jarak 6 px.
- Modul kanan dua baris: label 8.5 px semibold kapital `#9A9AA0` di atas nilai 11.5 px semibold. Jaringan dua baris 9.5 px `#D1D1D6`, lebar minimal 58 px. Tinggi area klik = tinggi bar − 6 (minimal 24). Padding 0 6 px, radius 6, hover putih 8%, jarak antarmodul 12 px.
- Semua angka yang berubah memakai digit tabular (`Typography.NumeralAlignment="Tabular"`).
- Target performa: RAM < 120 MB, CPU diam < 1%. Polling dijeda saat bar tersembunyi (fullscreen) dan saat sleep.

## Catatan eksekusi (26 Okt 2026)

- Task 8: `taskkill` tanpa `/F` ternyata juga mengirim `WM_CLOSE` ke jendela tersembunyi WPF ("Hidden Window", "SystemResourceNotifyWindow", …), sehingga proses bisa tetap hidup tanpa bar. Sebagai gantinya ditambahkan `Gili.exe --quit`: `SingleInstance.Acquire(wait, onActivateRequest, onQuitRequest)` mendengarkan named event `Local\Gili.Quit`, dan `SingleInstance.RequestQuit()` memicunya. `App.OnStartup` menangani `--quit` sebelum hal lain. Skrip di Task 11 dan CLAUDE.md memakai `--quit`, bukan `taskkill`.
- Task 8: kaca lewat accent policy terbukti blur di jendela yang tidak aktif (Strong dan Light), jadi baris "Material bar" di PRD 9.2 sudah diperbarui.

## Di luar rencana ini (Fase 1 lanjutan)

- **Rencana 1b**: `IslandWindow`, keadaan wide/expanded/kartu/drop zone, animator pegas, hover +16 px, `IslandCoordinator` dan prioritas (F-ISL-02/03), media (F-ISL-04/05), ngecas (F-ISL-06), kerangka expanded + halaman Media (F-ISL-07).
- **Rencana 1c**: aplikasi berjalan di kiri (F-LEFT-01), cuplikan clipboard (F-ISL-17), jarak 24 px dan overflow (F-BAR-07), mode demo (F-BAR-09), total jaringan hari ini di tooltip, jeda polling saat layar mati.
- Toast untuk crash kedua (PRD 11) menunggu `NotificationService` di Fase 2; sampai saat itu hanya ditulis ke log.

## Struktur file

```
Gili.slnx
Directory.Build.props
scripts/publish.ps1
src/Gili.Core/
  AppPaths.cs
  Formatting/ClockText.cs, StatText.cs
  Settings/AppSettings.cs, BarAppearance.cs
  Layout/BarMetrics.cs
  Modules/ModuleSampler.cs, Readings.cs
  Diagnostics/FileLog.cs, CrashPolicy.cs
src/Gili.Data/
  SettingsStore.cs
src/Gili.Platform/
  Interop/Win32.cs, WindowMessages.cs
  Shell/AppBarService.cs, BarMaterial.cs, TransparencyWatcher.cs, Autostart.cs, WindowStyles.cs
  Stats/CpuSampler.cs, MemorySampler.cs, NetworkSampler.cs
src/Gili.App/
  App.xaml(.cs), app.manifest, SingleInstance.cs, CrashHandler.cs, SettingsHub.cs
  Bar/BarWindow.xaml(.cs), BarViewModel.cs, StatsService.cs
  Settings/SettingsWindow.xaml(.cs), SettingsViewModel.cs
tests/Gili.Core.Tests/
  Formatting/, Settings/, Layout/, Modules/, Diagnostics/, Data/, AppPathsTests.cs
```

Pesan commit diakhiri dengan trailer `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` (ditulis sebagai `-m` terakhir di setiap langkah commit).

---

### Task 1: Solusi, proyek Core + test, dan teks jam

**Files:**
- Create: `Gili.slnx`, `Directory.Build.props`, `.gitignore`
- Create: `src/Gili.Core/Gili.Core.csproj`, `src/Gili.Core/Formatting/ClockText.cs`
- Create: `tests/Gili.Core.Tests/Gili.Core.Tests.csproj`, `tests/Gili.Core.Tests/Formatting/ClockTextTests.cs`

**Interfaces:**
- Produces: `Gili.Core.Formatting.ClockText` dengan `static string Time(DateTime local, bool use24Hour)`, `static string Date(DateTime local)`, `static TimeSpan UntilNextMinute(DateTime now)`.

- [ ] **Step 1: Buat solusi dan proyek**

```bash
dotnet new sln -n Gili --format slnx
dotnet new gitignore
dotnet new classlib -n Gili.Core -o src/Gili.Core -f net10.0
dotnet new xunit -n Gili.Core.Tests -o tests/Gili.Core.Tests -f net10.0
rm src/Gili.Core/Class1.cs tests/Gili.Core.Tests/UnitTest1.cs
dotnet sln Gili.slnx add src/Gili.Core/Gili.Core.csproj tests/Gili.Core.Tests/Gili.Core.Tests.csproj
dotnet add tests/Gili.Core.Tests reference src/Gili.Core
echo ".claude/settings.local.json" >> .gitignore
```

Pastikan `tests/Gili.Core.Tests/Gili.Core.Tests.csproj` berisi `<Using Include="Xunit" />`; kalau tidak ada, tambahkan dalam `<ItemGroup>`.

- [ ] **Step 2: Tulis `Directory.Build.props` dan ganti isi `Gili.Core.csproj`**

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

`src/Gili.Core/Gili.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
```

- [ ] **Step 3: Tulis test yang gagal**

`tests/Gili.Core.Tests/Formatting/ClockTextTests.cs`:

```csharp
using Gili.Core.Formatting;

namespace Gili.Core.Tests.Formatting;

public class ClockTextTests
{
    private static readonly DateTime Evening = new(2026, 10, 26, 19, 38, 0);

    [Fact]
    public void Time_uses_24_hour_digits_without_seconds() =>
        Assert.Equal("19:38", ClockText.Time(Evening, use24Hour: true));

    [Fact]
    public void Morning_in_24_hour_mode_keeps_the_leading_zero() =>
        Assert.Equal("05:30", ClockText.Time(new DateTime(2026, 10, 27, 5, 30, 0), use24Hour: true));

    [Fact]
    public void Time_in_12_hour_mode_has_am_pm() =>
        Assert.Equal("7:38 PM", ClockText.Time(Evening, use24Hour: false));

    [Fact]
    public void Midnight_in_12_hour_mode_is_12_am() =>
        Assert.Equal("12:05 AM", ClockText.Time(new DateTime(2026, 10, 27, 0, 5, 0), use24Hour: false));

    [Fact]
    public void Date_is_short_english_day_day_number_and_month() =>
        Assert.Equal("Mon 26 Oct", ClockText.Date(Evening));

    [Theory]
    [InlineData(20, 250, 39_750)]
    [InlineData(0, 0, 60_000)]
    [InlineData(59, 999, 1)]
    public void UntilNextMinute_lands_on_the_next_minute_boundary(int second, int millisecond, int expectedMs)
    {
        var now = new DateTime(2026, 10, 26, 19, 38, second, millisecond);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), ClockText.UntilNextMinute(now));
    }
}
```

- [ ] **Step 4: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~ClockTextTests"`
Expected: build gagal, `The name 'ClockText' does not exist`.

- [ ] **Step 5: Implementasi**

`src/Gili.Core/Formatting/ClockText.cs`:

```csharp
using System.Globalization;

namespace Gili.Core.Formatting;

/// <summary>Clock and date as the compact island shows them: "19:38" / "7:38 PM" and "Mon 26 Oct" (PRD 6.3).</summary>
public static class ClockText
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static string Time(DateTime local, bool use24Hour) =>
        local.ToString(use24Hour ? "HH:mm" : "h:mm tt", English);

    public static string Date(DateTime local) => local.ToString("ddd d MMM", English);

    /// <summary>Time left until the next minute starts, so the clock flips exactly on the minute (F-ISL-01).</summary>
    public static TimeSpan UntilNextMinute(DateTime now)
    {
        var minuteStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Kind);
        return minuteStart.AddMinutes(1) - now;
    }
}
```

- [ ] **Step 6: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~ClockTextTests"`
Expected: PASS, 8 test.

- [ ] **Step 7: Commit**

```bash
git add Gili.slnx Directory.Build.props .gitignore src/Gili.Core tests/Gili.Core.Tests
git commit -m "feat(core): solution scaffold and clock text (F-ISL-01)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Settings, tampilan bar, ukuran, dan path

**Files:**
- Create: `src/Gili.Core/Settings/AppSettings.cs`, `src/Gili.Core/Settings/BarAppearance.cs`, `src/Gili.Core/Layout/BarMetrics.cs`, `src/Gili.Core/AppPaths.cs`
- Test: `tests/Gili.Core.Tests/Settings/AppSettingsTests.cs`, `tests/Gili.Core.Tests/Settings/BarAppearanceTests.cs`, `tests/Gili.Core.Tests/Layout/BarMetricsTests.cs`, `tests/Gili.Core.Tests/AppPathsTests.cs`

**Interfaces:**
- Produces:
  - `enum GlassLevel { Off, Light, Strong }` dan `sealed record AppSettings` (init properties `bool Use24HourClock = true`, `int BarOpacity = 78`, `GlassLevel Glass = Strong`, `int BarHeight = 32`, `int IslandHeight = 24`, `bool Autostart = true`), `AppSettings Normalize()`, `static int MaxIslandHeight(int barHeight)`. Namespace `Gili.Core.Settings`.
  - `readonly record struct BarAppearance(GlassLevel Glass, double TintOpacity)` + `static BarAppearance Resolve(AppSettings settings, bool transparencyEffects)`.
  - `Gili.Core.Layout.BarMetrics`: `const double IslandMinWidth = 200`, `static double IslandFontSize(int islandHeight)`, `static double IslandCornerRadius(int islandHeight)`, `static double ModuleHitHeight(int barHeight)`.
  - `Gili.Core.AppPaths`: `Root`, `Logs`, `CrashMarker`, `InstallDirectory` (string), `static bool IsUnder(string path, string directory)`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Settings/AppSettingsTests.cs`:

```csharp
using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_match_the_prd()
    {
        var s = new AppSettings();
        Assert.True(s.Use24HourClock);
        Assert.Equal(78, s.BarOpacity);
        Assert.Equal(GlassLevel.Strong, s.Glass);
        Assert.Equal(32, s.BarHeight);
        Assert.Equal(24, s.IslandHeight);
        Assert.True(s.Autostart);
    }

    [Theory]
    [InlineData(10, 28)]
    [InlineData(28, 28)]
    [InlineData(36, 36)]
    [InlineData(60, 44)]
    public void Bar_height_is_clamped_to_28_through_44(int input, int expected) =>
        Assert.Equal(expected, new AppSettings { BarHeight = input }.Normalize().BarHeight);

    [Fact]
    public void Island_height_never_exceeds_bar_height_minus_4() =>
        Assert.Equal(24, new AppSettings { BarHeight = 28, IslandHeight = 30 }.Normalize().IslandHeight);

    [Fact]
    public void Island_height_never_goes_below_20() =>
        Assert.Equal(20, new AppSettings { IslandHeight = 12 }.Normalize().IslandHeight);

    [Theory]
    [InlineData(10, 40)]
    [InlineData(78, 78)]
    [InlineData(150, 100)]
    public void Bar_opacity_is_clamped_to_40_through_100(int input, int expected) =>
        Assert.Equal(expected, new AppSettings { BarOpacity = input }.Normalize().BarOpacity);

    [Fact]
    public void Unknown_glass_value_falls_back_to_strong() =>
        Assert.Equal(GlassLevel.Strong, new AppSettings { Glass = (GlassLevel)9 }.Normalize().Glass);
}
```

`tests/Gili.Core.Tests/Settings/BarAppearanceTests.cs`:

```csharp
using Gili.Core.Settings;

namespace Gili.Core.Tests.Settings;

public class BarAppearanceTests
{
    [Fact]
    public void With_transparency_effects_the_chosen_glass_and_opacity_apply()
    {
        var look = BarAppearance.Resolve(new AppSettings { Glass = GlassLevel.Light, BarOpacity = 60 }, transparencyEffects: true);
        Assert.Equal(GlassLevel.Light, look.Glass);
        Assert.Equal(0.6, look.TintOpacity, 3);
    }

    [Fact]
    public void Without_transparency_effects_the_bar_is_solid()
    {
        var look = BarAppearance.Resolve(new AppSettings { Glass = GlassLevel.Strong, BarOpacity = 40 }, transparencyEffects: false);
        Assert.Equal(GlassLevel.Off, look.Glass);
        Assert.Equal(1.0, look.TintOpacity);
    }
}
```

`tests/Gili.Core.Tests/Layout/BarMetricsTests.cs`:

```csharp
using Gili.Core.Layout;

namespace Gili.Core.Tests.Layout;

public class BarMetricsTests
{
    [Theory]
    [InlineData(24, 12)]
    [InlineData(26, 13)]
    [InlineData(20, 11)]
    [InlineData(30, 15)]
    [InlineData(40, 15)]
    public void Island_font_is_half_the_island_height_within_11_to_15(int islandHeight, double expected) =>
        Assert.Equal(expected, BarMetrics.IslandFontSize(islandHeight));

    [Fact]
    public void Island_corner_radius_is_half_its_height() =>
        Assert.Equal(12, BarMetrics.IslandCornerRadius(24));

    [Theory]
    [InlineData(28, 24)]
    [InlineData(32, 26)]
    [InlineData(44, 38)]
    public void Module_hit_height_is_bar_minus_6_but_at_least_24(int barHeight, double expected) =>
        Assert.Equal(expected, BarMetrics.ModuleHitHeight(barHeight));
}
```

`tests/Gili.Core.Tests/AppPathsTests.cs`:

```csharp
namespace Gili.Core.Tests;

public class AppPathsTests
{
    private const string Install = @"C:\Users\a\AppData\Local\Programs\Gili";

    [Theory]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Gili\Gili.exe", Install, true)]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Gili\Gili.exe", Install + @"\", true)]
    [InlineData(@"c:\users\a\appdata\local\programs\gili\Gili.exe", Install, true)]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Gili2\Gili.exe", Install, false)]
    [InlineData(@"C:\src\top-taskbar\src\Gili.App\bin\Debug\Gili.exe", Install, false)]
    public void IsUnder_matches_whole_directory_names(string path, string directory, bool expected) =>
        Assert.Equal(expected, AppPaths.IsUnder(path, directory));
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests`
Expected: build gagal, `AppSettings`, `BarAppearance`, `BarMetrics`, `AppPaths` tidak ditemukan.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Settings/AppSettings.cs`:

```csharp
namespace Gili.Core.Settings;

public enum GlassLevel { Off, Light, Strong }

/// <summary>Everything stored in settings.json. Later phases add fields with their own defaults.</summary>
public sealed record AppSettings
{
    public const int MinBarHeight = 28;
    public const int MaxBarHeight = 44;
    public const int MinIslandHeight = 20;
    public const int MinBarOpacity = 40;
    public const int MaxBarOpacity = 100;

    public bool Use24HourClock { get; init; } = true;
    public int BarOpacity { get; init; } = 78;
    public GlassLevel Glass { get; init; } = GlassLevel.Strong;
    public int BarHeight { get; init; } = 32;
    public int IslandHeight { get; init; } = 24;
    public bool Autostart { get; init; } = true;

    public static int MaxIslandHeight(int barHeight) => barHeight - 4;

    /// <summary>Clamps every value to the ranges in PRD 6.1 and F-BAR-08.</summary>
    public AppSettings Normalize()
    {
        var barHeight = Math.Clamp(BarHeight, MinBarHeight, MaxBarHeight);
        return this with
        {
            BarHeight = barHeight,
            IslandHeight = Math.Clamp(IslandHeight, MinIslandHeight, MaxIslandHeight(barHeight)),
            BarOpacity = Math.Clamp(BarOpacity, MinBarOpacity, MaxBarOpacity),
            Glass = Enum.IsDefined(Glass) ? Glass : GlassLevel.Strong,
        };
    }
}
```

`src/Gili.Core/Settings/BarAppearance.cs`:

```csharp
namespace Gili.Core.Settings;

/// <summary>The material the bar actually draws: Windows' "Transparency effects" switch overrides the user's choice (F-BAR-06).</summary>
public readonly record struct BarAppearance(GlassLevel Glass, double TintOpacity)
{
    public static BarAppearance Resolve(AppSettings settings, bool transparencyEffects) =>
        transparencyEffects
            ? new BarAppearance(settings.Glass, settings.BarOpacity / 100.0)
            : new BarAppearance(GlassLevel.Off, 1.0);
}
```

`src/Gili.Core/Layout/BarMetrics.cs`:

```csharp
namespace Gili.Core.Layout;

/// <summary>Sizes derived from the bar and island heights (PRD 6.1, 6.4, 8.3).</summary>
public static class BarMetrics
{
    public const double IslandMinWidth = 200;

    public static double IslandFontSize(int islandHeight) => Math.Clamp(islandHeight / 2.0, 11, 15);

    public static double IslandCornerRadius(int islandHeight) => islandHeight / 2.0;

    public static double ModuleHitHeight(int barHeight) => Math.Max(24, barHeight - 6);
}
```

`src/Gili.Core/AppPaths.cs`:

```csharp
namespace Gili.Core;

public static class AppPaths
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public static string Root { get; } = Path.Combine(LocalAppData, "Gili");
    public static string Logs { get; } = Path.Combine(Root, "logs");
    public static string CrashMarker { get; } = Path.Combine(Root, "last-crash.txt");
    public static string InstallDirectory { get; } = Path.Combine(LocalAppData, "Programs", "Gili");

    public static bool IsUnder(string path, string directory)
    {
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests`
Expected: PASS semua.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core tests/Gili.Core.Tests
git commit -m "feat(core): settings model, bar appearance and metrics (F-BAR-06, F-BAR-08)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `SettingsStore` di proyek Data

**Files:**
- Create: `src/Gili.Data/Gili.Data.csproj`, `src/Gili.Data/SettingsStore.cs`
- Test: `tests/Gili.Core.Tests/Data/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `AppSettings`, `GlassLevel` (Task 2).
- Produces: `Gili.Data.SettingsStore(string directory)` dengan `string FilePath`, `SettingsLoadResult Load()`, `void Save(AppSettings settings)`; `sealed record SettingsLoadResult(AppSettings Settings, bool WasCorrupt)`.

- [ ] **Step 1: Buat proyek**

```bash
dotnet new classlib -n Gili.Data -o src/Gili.Data -f net10.0
rm src/Gili.Data/Class1.cs
dotnet sln Gili.slnx add src/Gili.Data/Gili.Data.csproj
dotnet add src/Gili.Data reference src/Gili.Core
dotnet add tests/Gili.Core.Tests reference src/Gili.Data
```

Hapus baris `<ImplicitUsings>` dan `<Nullable>` dari `src/Gili.Data/Gili.Data.csproj` (sudah ada di `Directory.Build.props`), sisakan `<TargetFramework>net10.0</TargetFramework>` dan referensi Core.

- [ ] **Step 2: Tulis test yang gagal**

`tests/Gili.Core.Tests/Data/SettingsStoreTests.cs`:

```csharp
using Gili.Core.Settings;
using Gili.Data;

namespace Gili.Core.Tests.Data;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gili-tests", Guid.NewGuid().ToString("N"));

    private SettingsStore Store => new(_dir);
    private string SettingsFile => Path.Combine(_dir, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var result = Store.Load();
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.False(result.WasCorrupt);
    }

    [Fact]
    public void Saved_settings_load_back()
    {
        var saved = new AppSettings { BarHeight = 36, Glass = GlassLevel.Light, Use24HourClock = false };
        Store.Save(saved);
        Assert.Equal(saved, Store.Load().Settings);
    }

    [Fact]
    public void Unreadable_file_is_renamed_to_bak_and_defaults_are_used()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsFile, "{ not json");

        var result = Store.Load();

        Assert.True(result.WasCorrupt);
        Assert.Equal(new AppSettings(), result.Settings);
        Assert.True(File.Exists(SettingsFile + ".bak"));
        Assert.False(File.Exists(SettingsFile));
    }

    [Fact]
    public void Missing_fields_keep_their_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsFile, """{ "barHeight": 36 }""");

        var settings = Store.Load().Settings;

        Assert.Equal(36, settings.BarHeight);
        Assert.Equal(24, settings.IslandHeight);
        Assert.Equal(78, settings.BarOpacity);
    }

    [Fact]
    public void Out_of_range_values_are_normalized_on_load()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(SettingsFile, """{ "barHeight": 80, "islandHeight": 60 }""");

        var settings = Store.Load().Settings;

        Assert.Equal(44, settings.BarHeight);
        Assert.Equal(40, settings.IslandHeight);
    }

    [Fact]
    public void Glass_is_stored_by_name()
    {
        Store.Save(new AppSettings { Glass = GlassLevel.Light });
        Assert.Contains("\"glass\": \"light\"", File.ReadAllText(SettingsFile));
    }
}
```

- [ ] **Step 3: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~SettingsStoreTests"`
Expected: build gagal, `SettingsStore` tidak ditemukan.

- [ ] **Step 4: Implementasi**

`src/Gili.Data/SettingsStore.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Gili.Core.Settings;

namespace Gili.Data;

public sealed record SettingsLoadResult(AppSettings Settings, bool WasCorrupt);

/// <summary>settings.json in the app data folder (PRD 10).</summary>
public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string FilePath { get; } = Path.Combine(directory, "settings.json");

    /// <summary>A missing file gives defaults. A file that isn't valid JSON is renamed to settings.json.bak and defaults are used (PRD 11).</summary>
    public SettingsLoadResult Load()
    {
        if (!File.Exists(FilePath)) return new SettingsLoadResult(new AppSettings(), WasCorrupt: false);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options)
                ?? throw new JsonException("settings.json holds null");
            return new SettingsLoadResult(settings.Normalize(), WasCorrupt: false);
        }
        catch (JsonException)
        {
            File.Move(FilePath, FilePath + ".bak", overwrite: true);
            return new SettingsLoadResult(new AppSettings(), WasCorrupt: true);
        }
    }

    /// <summary>Writes to a temp file first so a crash mid-write never leaves a half-written settings.json.</summary>
    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
        File.Move(temp, FilePath, overwrite: true);
    }
}
```

- [ ] **Step 5: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests`
Expected: PASS semua.

- [ ] **Step 6: Commit**

```bash
git add Gili.slnx src/Gili.Data tests/Gili.Core.Tests
git commit -m "feat(data): settings.json store with corrupt-file fallback" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---
### Task 4: Sampler modul dengan backoff, dan format angka

**Files:**
- Create: `src/Gili.Core/Modules/ModuleSampler.cs`, `src/Gili.Core/Modules/Readings.cs`, `src/Gili.Core/Formatting/StatText.cs`
- Test: `tests/Gili.Core.Tests/Modules/ModuleSamplerTests.cs`, `tests/Gili.Core.Tests/Formatting/StatTextTests.cs`

**Interfaces:**
- Produces:
  - `readonly record struct ModuleState<T>(T? Value, string? Error) where T : class`
  - `sealed class ModuleSampler<T>(Func<T> sample) where T : class` dengan `ModuleState<T> Tick(DateTimeOffset now)`
  - `static class RetryDelay` dengan `static TimeSpan After(int failures)`
  - `sealed record CpuReading(double Percent)`, `sealed record MemoryReading(double Percent, ulong UsedBytes, ulong TotalBytes)`, `sealed record NetworkReading(double UpBytesPerSecond, double DownBytesPerSecond, string Adapters)`. Namespace `Gili.Core.Modules`.
  - `Gili.Core.Formatting.StatText`: `static string Percent(double value)`, `static string Rate(double bytesPerSecond)`, `static string MemoryUsage(ulong usedBytes, ulong totalBytes)`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Modules/ModuleSamplerTests.cs`:

```csharp
using Gili.Core.Modules;

namespace Gili.Core.Tests.Modules;

public class ModuleSamplerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    private sealed record Reading(int N);

    [Fact]
    public void A_successful_sample_is_the_value()
    {
        var state = new ModuleSampler<Reading>(() => new Reading(7)).Tick(T0);
        Assert.Equal(new Reading(7), state.Value);
        Assert.Null(state.Error);
    }

    [Fact]
    public void Failures_show_the_reason_and_retry_after_5s_30s_then_every_2min()
    {
        var calls = 0;
        var sampler = new ModuleSampler<Reading>(() => { calls++; throw new InvalidOperationException("Counter missing"); });

        var state = sampler.Tick(T0);
        Assert.Null(state.Value);
        Assert.Equal("Counter missing", state.Error);

        sampler.Tick(T0.AddSeconds(4.9));
        Assert.Equal(1, calls);
        sampler.Tick(T0.AddSeconds(5));
        Assert.Equal(2, calls);
        sampler.Tick(T0.AddSeconds(34.9));
        Assert.Equal(2, calls);
        sampler.Tick(T0.AddSeconds(35));
        Assert.Equal(3, calls);
        sampler.Tick(T0.AddSeconds(154.9));
        Assert.Equal(3, calls);
        sampler.Tick(T0.AddSeconds(155));
        Assert.Equal(4, calls);
        sampler.Tick(T0.AddSeconds(275));
        Assert.Equal(5, calls);
    }

    [Fact]
    public void Skipped_ticks_keep_reporting_the_error()
    {
        var sampler = new ModuleSampler<Reading>(() => throw new InvalidOperationException("down"));
        sampler.Tick(T0);
        Assert.Equal("down", sampler.Tick(T0.AddSeconds(1)).Error);
    }

    [Fact]
    public void A_success_resets_the_backoff()
    {
        var fail = true;
        var calls = 0;
        var sampler = new ModuleSampler<Reading>(() =>
        {
            calls++;
            return fail ? throw new InvalidOperationException("x") : new Reading(1);
        });

        sampler.Tick(T0);                  // failure 1, next try at +5 s
        sampler.Tick(T0.AddSeconds(5));    // failure 2, next try at +35 s
        fail = false;
        Assert.Equal(new Reading(1), sampler.Tick(T0.AddSeconds(35)).Value);
        fail = true;
        sampler.Tick(T0.AddSeconds(36));   // failure 1 again, next try at +41 s
        sampler.Tick(T0.AddSeconds(41));

        Assert.Equal(5, calls);
    }
}
```

`tests/Gili.Core.Tests/Formatting/StatTextTests.cs`:

```csharp
using Gili.Core.Formatting;

namespace Gili.Core.Tests.Formatting;

public class StatTextTests
{
    private const double KB = 1024;
    private const double MB = 1024 * 1024;
    private const double GB = 1024 * 1024 * 1024;

    [Theory]
    [InlineData(12.4, "12%")]
    [InlineData(12.6, "13%")]
    [InlineData(-3, "0%")]
    [InlineData(140, "100%")]
    public void Percent_is_a_whole_number_between_0_and_100(double value, string expected) =>
        Assert.Equal(expected, StatText.Percent(value));

    [Theory]
    [InlineData(0, "0 B/s")]
    [InlineData(-5, "0 B/s")]
    [InlineData(512, "512 B/s")]
    [InlineData(1023.9, "1023 B/s")]
    [InlineData(2 * KB, "2 KB/s")]
    [InlineData(999 * KB, "999 KB/s")]
    [InlineData(999.6 * KB, "1.0 MB/s")]
    [InlineData(2.1 * MB, "2.1 MB/s")]
    [InlineData(150 * MB, "150.0 MB/s")]
    public void Rate_picks_B_KB_or_MB(double bytesPerSecond, string expected) =>
        Assert.Equal(expected, StatText.Rate(bytesPerSecond));

    [Theory]
    [InlineData(8.6 * GB, 16 * GB, "8.6 / 16 GB")]
    [InlineData(8.6 * GB, 15.7 * GB, "8.6 / 15.7 GB")]
    public void MemoryUsage_shows_used_over_total_in_GB(double used, double total, string expected) =>
        Assert.Equal(expected, StatText.MemoryUsage((ulong)used, (ulong)total));
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~ModuleSamplerTests|FullyQualifiedName~StatTextTests"`
Expected: build gagal, `ModuleSampler` dan `StatText` tidak ditemukan.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Modules/Readings.cs`:

```csharp
namespace Gili.Core.Modules;

public sealed record CpuReading(double Percent);

public sealed record MemoryReading(double Percent, ulong UsedBytes, ulong TotalBytes);

/// <summary><paramref name="Adapters"/> lists the adapter names for the tooltip.</summary>
public sealed record NetworkReading(double UpBytesPerSecond, double DownBytesPerSecond, string Adapters);
```

`src/Gili.Core/Modules/ModuleSampler.cs`:

```csharp
namespace Gili.Core.Modules;

/// <summary>Latest reading of a bar module, or why there is none (the bar shows "—" with the reason as tooltip, PRD 11).</summary>
public readonly record struct ModuleState<T>(T? Value, string? Error) where T : class;

/// <summary>
/// Wraps one provider so its failure only blanks its own module. After a failure the provider is left alone for
/// 5 s, then 30 s, then every 2 min until it works again.
/// </summary>
public sealed class ModuleSampler<T>(Func<T> sample) where T : class
{
    private int _failures;
    private DateTimeOffset _retryAt = DateTimeOffset.MinValue;
    private ModuleState<T> _state;

    public ModuleState<T> Tick(DateTimeOffset now)
    {
        if (now < _retryAt) return _state;
        try
        {
            _state = new ModuleState<T>(sample(), null);
            _failures = 0;
            _retryAt = DateTimeOffset.MinValue;
        }
        catch (Exception ex)
        {
            _failures++;
            _state = new ModuleState<T>(null, ex.Message);
            _retryAt = now + RetryDelay.After(_failures);
        }
        return _state;
    }
}

public static class RetryDelay
{
    public static TimeSpan After(int failures) => failures switch
    {
        <= 1 => TimeSpan.FromSeconds(5),
        2 => TimeSpan.FromSeconds(30),
        _ => TimeSpan.FromMinutes(2),
    };
}
```

`src/Gili.Core/Formatting/StatText.cs`:

```csharp
using System.Globalization;

namespace Gili.Core.Formatting;

/// <summary>Numbers in the right-hand modules (PRD 6.4). Always invariant, never the Windows locale.</summary>
public static class StatText
{
    private const double BytesPerGB = 1024 * 1024 * 1024;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Percent(double value) =>
        Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero).ToString("0", Invariant) + "%";

    public static string Rate(double bytesPerSecond)
    {
        if (bytesPerSecond < 1024) return Math.Floor(Math.Max(0, bytesPerSecond)).ToString("0", Invariant) + " B/s";
        var kb = Math.Round(bytesPerSecond / 1024, MidpointRounding.AwayFromZero);
        if (kb < 1000) return kb.ToString("0", Invariant) + " KB/s";
        return (bytesPerSecond / (1024 * 1024)).ToString("0.0", Invariant) + " MB/s";
    }

    public static string MemoryUsage(ulong usedBytes, ulong totalBytes) =>
        $"{(usedBytes / BytesPerGB).ToString("0.0", Invariant)} / {(totalBytes / BytesPerGB).ToString("0.#", Invariant)} GB";
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests`
Expected: PASS semua.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core tests/Gili.Core.Tests
git commit -m "feat(core): module sampler with retry backoff and stat text (F-RIGHT-01)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Log harian dan kebijakan restart setelah crash

**Files:**
- Create: `src/Gili.Core/Diagnostics/FileLog.cs`, `src/Gili.Core/Diagnostics/CrashPolicy.cs`
- Test: `tests/Gili.Core.Tests/Diagnostics/FileLogTests.cs`, `tests/Gili.Core.Tests/Diagnostics/CrashPolicyTests.cs`

**Interfaces:**
- Produces: `Gili.Core.Diagnostics.FileLog(string directory)` dengan `void Write(string message, Exception? exception = null)` dan `void DeleteOlderThan(TimeSpan age, DateTime now)`; `static class CrashPolicy` dengan `static bool ShouldRestart(DateTimeOffset? previousCrash, DateTimeOffset now)`.

- [ ] **Step 1: Tulis test yang gagal**

`tests/Gili.Core.Tests/Diagnostics/FileLogTests.cs`:

```csharp
using Gili.Core.Diagnostics;

namespace Gili.Core.Tests.Diagnostics;

public sealed class FileLogTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "gili-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Write_appends_messages_and_exceptions_to_todays_file()
    {
        var log = new FileLog(_dir);
        log.Write("first");
        log.Write("second", new InvalidOperationException("boom"));

        var file = Assert.Single(Directory.GetFiles(_dir, "gili-*.log"));
        Assert.Equal($"gili-{DateTime.Now:yyyyMMdd}.log", Path.GetFileName(file));
        var text = File.ReadAllText(file);
        Assert.Contains("first", text);
        Assert.Contains("second", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void DeleteOlderThan_removes_only_old_log_files()
    {
        Directory.CreateDirectory(_dir);
        var now = new DateTime(2026, 10, 26, 12, 0, 0);
        var old = Path.Combine(_dir, "gili-20261001.log");
        var recent = Path.Combine(_dir, "gili-20261025.log");
        var other = Path.Combine(_dir, "notes.txt");
        foreach (var f in new[] { old, recent, other }) File.WriteAllText(f, "x");
        File.SetLastWriteTime(old, now.AddDays(-8));
        File.SetLastWriteTime(recent, now.AddDays(-1));
        File.SetLastWriteTime(other, now.AddDays(-30));

        new FileLog(_dir).DeleteOlderThan(TimeSpan.FromDays(7), now);

        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(other));
    }

    [Fact]
    public void DeleteOlderThan_without_a_log_folder_does_nothing() =>
        new FileLog(_dir).DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
}
```

`tests/Gili.Core.Tests/Diagnostics/CrashPolicyTests.cs`:

```csharp
using Gili.Core.Diagnostics;

namespace Gili.Core.Tests.Diagnostics;

public class CrashPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_first_crash_restarts() => Assert.True(CrashPolicy.ShouldRestart(null, Now));

    [Fact]
    public void A_second_crash_within_a_minute_stays_down() =>
        Assert.False(CrashPolicy.ShouldRestart(Now.AddSeconds(-59), Now));

    [Fact]
    public void A_crash_more_than_a_minute_after_the_last_restarts_again() =>
        Assert.True(CrashPolicy.ShouldRestart(Now.AddSeconds(-61), Now));
}
```

- [ ] **Step 2: Jalankan test, pastikan gagal**

Run: `dotnet test tests/Gili.Core.Tests --filter "FullyQualifiedName~Diagnostics"`
Expected: build gagal, `FileLog` dan `CrashPolicy` tidak ditemukan.

- [ ] **Step 3: Implementasi**

`src/Gili.Core/Diagnostics/FileLog.cs`:

```csharp
using System.Globalization;

namespace Gili.Core.Diagnostics;

/// <summary>Daily log files gili-yyyyMMdd.log, kept for 7 days (PRD 11).</summary>
public sealed class FileLog(string directory)
{
    private readonly Lock _gate = new();

    public void Write(string message, Exception? exception = null)
    {
        var now = DateTime.Now;
        var entry = $"{now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)} {message}{Environment.NewLine}";
        if (exception is not null) entry += exception + Environment.NewLine;
        var file = Path.Combine(directory, $"gili-{now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log");
        lock (_gate)
        {
            Directory.CreateDirectory(directory);
            File.AppendAllText(file, entry);
        }
    }

    public void DeleteOlderThan(TimeSpan age, DateTime now)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "gili-*.log"))
        {
            if (File.GetLastWriteTime(file) < now - age) File.Delete(file);
        }
    }
}
```

`src/Gili.Core/Diagnostics/CrashPolicy.cs`:

```csharp
namespace Gili.Core.Diagnostics;

/// <summary>PRD 11: restart once after a crash; a second crash within a minute stops the app.</summary>
public static class CrashPolicy
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static bool ShouldRestart(DateTimeOffset? previousCrash, DateTimeOffset now) =>
        previousCrash is null || now - previousCrash.Value > Window;
}
```

- [ ] **Step 4: Jalankan test, pastikan lulus**

Run: `dotnet test tests/Gili.Core.Tests`
Expected: PASS semua.

- [ ] **Step 5: Commit**

```bash
git add src/Gili.Core tests/Gili.Core.Tests
git commit -m "feat(core): daily file log and crash restart policy" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Platform shell: AppBar, kaca, transparansi, autostart

Kode di task ini memanggil Win32/WinRT dan tidak punya unit test. Pemeriksaannya ada di Task 8 dan Task 10, saat bar benar-benar jalan.

**Files:**
- Create: `src/Gili.Platform/Gili.Platform.csproj`
- Create: `src/Gili.Platform/Interop/Win32.cs`, `src/Gili.Platform/Interop/WindowMessages.cs`
- Create: `src/Gili.Platform/Shell/AppBarService.cs`, `BarMaterial.cs`, `TransparencyWatcher.cs`, `Autostart.cs`, `WindowStyles.cs`

**Interfaces:**
- Consumes: `GlassLevel` (Task 2).
- Produces (namespace `Gili.Platform.Shell` kecuali disebut lain):
  - `AppBarService(IntPtr hwnd)`: `event Action<bool>? FullscreenChanged`, `void Register(double heightDip)`, `void SetHeight(double heightDip)`, `void Reposition()`, `void Unregister()`, `bool HandleMessage(int msg, IntPtr wParam, IntPtr lParam)`.
  - `static BarMaterial.Apply(IntPtr hwnd, GlassLevel glass)`.
  - `TransparencyWatcher : IDisposable`: `bool Enabled`, `event Action<bool>? Changed` (dipanggil di thread latar).
  - `static Autostart.Apply(bool enabled, string exePath)`.
  - `static WindowStyles.MakeNonActivatingToolWindow(IntPtr hwnd)`.
  - `Gili.Platform.Interop.WindowMessages`: konstanta `WM_ACTIVATE`, `WM_TIMECHANGE`, `WM_WINDOWPOSCHANGED`, `WM_DISPLAYCHANGE`, `WM_POWERBROADCAST`, `WM_DPICHANGED` (`int`), `PBT_APMSUSPEND`, `PBT_APMRESUMEAUTOMATIC` (`nint`).

Catatan desain: PRD 9.2 menyebut `DWMWA_SYSTEMBACKDROP_TYPE` untuk acrylic. Backdrop sistem itu berubah menjadi warna polos saat jendela tidak aktif, padahal bar tidak pernah aktif (`WS_EX_NOACTIVATE`). Karena itu kaca memakai `SetWindowCompositionAttribute` (accent policy), yang blurnya tetap ada di jendela tidak aktif. Kalau Task 8 membuktikannya, baris "Material bar" di PRD 9.2 diperbarui.

- [ ] **Step 1: Buat proyek**

```bash
dotnet new classlib -n Gili.Platform -o src/Gili.Platform -f net10.0
rm src/Gili.Platform/Class1.cs
dotnet sln Gili.slnx add src/Gili.Platform/Gili.Platform.csproj
```

Ganti isi `src/Gili.Platform/Gili.Platform.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Gili.Core\Gili.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Interop**

`src/Gili.Platform/Interop/Win32.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Text;

namespace Gili.Platform.Interop;

internal static class Win32
{
    public const int GWL_EXSTYLE = -20;
    public const nint WS_EX_TOOLWINDOW = 0x00000080;
    public const nint WS_EX_NOACTIVATE = 0x08000000;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint MONITOR_DEFAULTTOPRIMARY = 0x00000001;
    public static readonly IntPtr HWND_TOPMOST = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    [DllImport("shell32.dll")]
    public static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    public static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static extern nint GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static extern nint SetWindowLongPtr(IntPtr hwnd, int index, nint value);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int maxCount);
}
```

`src/Gili.Platform/Interop/WindowMessages.cs`:

```csharp
namespace Gili.Platform.Interop;

public static class WindowMessages
{
    public const int WM_ACTIVATE = 0x0006;
    public const int WM_TIMECHANGE = 0x001E;
    public const int WM_WINDOWPOSCHANGED = 0x0047;
    public const int WM_DISPLAYCHANGE = 0x007E;
    public const int WM_POWERBROADCAST = 0x0218;
    public const int WM_DPICHANGED = 0x02E0;
    public const nint PBT_APMSUSPEND = 0x0004;
    public const nint PBT_APMRESUMEAUTOMATIC = 0x0012;
}
```

- [ ] **Step 3: `AppBarService`**

`src/Gili.Platform/Shell/AppBarService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Text;
using Gili.Platform.Interop;

namespace Gili.Platform.Shell;

/// <summary>
/// Reserves a strip at the top of the primary monitor so maximized windows start below the bar (F-BAR-01), and
/// keeps it reserved across Explorer restarts, display and DPI changes and resume (F-BAR-03).
/// Unregister must run on every exit path: a strip left registered stays empty until Explorer restarts.
/// </summary>
public sealed class AppBarService(IntPtr hwnd)
{
    private const uint ABM_NEW = 0x0, ABM_REMOVE = 0x1, ABM_QUERYPOS = 0x2, ABM_SETPOS = 0x3;
    private const uint ABM_ACTIVATE = 0x6, ABM_WINDOWPOSCHANGED = 0x9;
    private const uint ABN_POSCHANGED = 0x1, ABN_FULLSCREENAPP = 0x2;
    private const uint ABE_TOP = 1;

    private readonly uint _callbackMessage = Win32.RegisterWindowMessage("Gili.AppBarNotify");
    private readonly uint _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private double _heightDip;
    private bool _registered;
    private bool _positioning;
    private bool _fullscreen;

    /// <summary>True when an app goes fullscreen on the primary monitor, false when it leaves (F-BAR-02).</summary>
    public event Action<bool>? FullscreenChanged;

    public void Register(double heightDip)
    {
        _heightDip = heightDip;
        if (!_registered)
        {
            var data = NewData();
            data.uCallbackMessage = _callbackMessage;
            _registered = Win32.SHAppBarMessage(ABM_NEW, ref data) != UIntPtr.Zero;
        }
        Reposition();
    }

    public void SetHeight(double heightDip)
    {
        _heightDip = heightDip;
        Reposition();
    }

    public void Unregister()
    {
        if (!_registered) return;
        var data = NewData();
        Win32.SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
    }

    /// <summary>Claims the strip again and puts the bar back on top of it, e.g. after a fullscreen app closes.</summary>
    public void Reposition()
    {
        if (!_registered || _positioning) return;
        _positioning = true;
        try
        {
            var monitor = PrimaryMonitor();
            var height = (int)Math.Round(_heightDip * Win32.GetDpiForWindow(hwnd) / 96.0);
            var data = NewData();
            data.uEdge = ABE_TOP;
            data.rc = new Win32.RECT { Left = monitor.Left, Top = monitor.Top, Right = monitor.Right, Bottom = monitor.Top + height };
            Win32.SHAppBarMessage(ABM_QUERYPOS, ref data);
            data.rc.Bottom = data.rc.Top + height;
            Win32.SHAppBarMessage(ABM_SETPOS, ref data);
            Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, data.rc.Left, data.rc.Top,
                data.rc.Right - data.rc.Left, height, Win32.SWP_NOACTIVATE);
        }
        finally
        {
            _positioning = false;
        }
    }

    /// <summary>Call from the window's message hook. Returns true for AppBar notifications, which need no further handling.</summary>
    public bool HandleMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == _callbackMessage)
        {
            if ((uint)wParam == ABN_POSCHANGED) Reposition();
            else if ((uint)wParam == ABN_FULLSCREENAPP) OnFullscreenApp(lParam != IntPtr.Zero);
            return true;
        }
        if (msg == _taskbarCreated)
        {
            // Explorer restarted and forgot every AppBar.
            _registered = false;
            Register(_heightDip);
            return true;
        }
        switch (msg)
        {
            case WindowMessages.WM_ACTIVATE:
                Notify(ABM_ACTIVATE);
                break;
            case WindowMessages.WM_WINDOWPOSCHANGED:
                Notify(ABM_WINDOWPOSCHANGED);
                break;
            case WindowMessages.WM_DISPLAYCHANGE:
            case WindowMessages.WM_DPICHANGED:
                // Let WPF finish its own DPI resize, then claim the strip at the new size.
                _ui?.Post(_ => Reposition(), null);
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _ui?.Post(_ => { Unregister(); Register(_heightDip); }, null);
                break;
        }
        return false;
    }

    private void OnFullscreenApp(bool entering)
    {
        // Clicking the desktop can make the shell report its own desktop window as fullscreen.
        if (entering && IsShellDesktop(Win32.GetForegroundWindow())) return;
        if (entering == _fullscreen) return;
        _fullscreen = entering;
        FullscreenChanged?.Invoke(entering);
    }

    private void Notify(uint message)
    {
        if (!_registered) return;
        var data = NewData();
        Win32.SHAppBarMessage(message, ref data);
    }

    private Win32.APPBARDATA NewData() =>
        new() { cbSize = (uint)Marshal.SizeOf<Win32.APPBARDATA>(), hWnd = hwnd };

    private static Win32.RECT PrimaryMonitor()
    {
        var info = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        Win32.GetMonitorInfo(Win32.MonitorFromPoint(default, Win32.MONITOR_DEFAULTTOPRIMARY), ref info);
        return info.rcMonitor;
    }

    private static bool IsShellDesktop(IntPtr window)
    {
        var name = new StringBuilder(32);
        Win32.GetClassName(window, name, name.Capacity);
        return name.ToString() is "Progman" or "WorkerW";
    }
}
```

- [ ] **Step 4: Kaca, transparansi, autostart, gaya jendela**

`src/Gili.Platform/Shell/BarMaterial.cs`:

```csharp
using System.Runtime.InteropServices;
using Gili.Core.Settings;

namespace Gili.Platform.Shell;

/// <summary>
/// Glass behind the bar (F-BAR-06): Off draws only the WPF tint, Light is blur-behind, Strong is acrylic.
/// Uses the undocumented accent policy because DWM system backdrops turn solid while the window is inactive,
/// and the bar never activates. WPF paints the tint on top, so the accent's own gradient stays nearly clear.
/// </summary>
public static class BarMaterial
{
    private const int WCA_ACCENT_POLICY = 19;
    private const int ACCENT_DISABLED = 0;
    private const int ACCENT_ENABLE_BLURBEHIND = 3;
    private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    public static void Apply(IntPtr hwnd, GlassLevel glass)
    {
        var policy = new AccentPolicy
        {
            AccentState = glass switch
            {
                GlassLevel.Light => ACCENT_ENABLE_BLURBEHIND,
                GlassLevel.Strong => ACCENT_ENABLE_ACRYLICBLURBEHIND,
                _ => ACCENT_DISABLED,
            },
            // AABBGGRR: the bar tint #161618 at alpha 1, so acrylic has a gradient to work with.
            GradientColor = glass == GlassLevel.Strong ? 0x01181616u : 0u,
        };
        var size = Marshal.SizeOf<AccentPolicy>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(policy, buffer, false);
            var data = new WindowCompositionAttributeData { Attribute = WCA_ACCENT_POLICY, Data = buffer, SizeOfData = size };
            SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
```

`src/Gili.Platform/Shell/TransparencyWatcher.cs`:

```csharp
using Windows.UI.ViewManagement;

namespace Gili.Platform.Shell;

/// <summary>Follows Windows' "Transparency effects" switch; when it is off the bar is drawn solid (F-BAR-06).</summary>
public sealed class TransparencyWatcher : IDisposable
{
    private readonly UISettings _settings = new();

    public TransparencyWatcher() => _settings.AdvancedEffectsEnabledChanged += OnChanged;

    public bool Enabled => _settings.AdvancedEffectsEnabled;

    /// <summary>Raised on a background thread.</summary>
    public event Action<bool>? Changed;

    public void Dispose() => _settings.AdvancedEffectsEnabledChanged -= OnChanged;

    private void OnChanged(UISettings sender, object args) => Changed?.Invoke(sender.AdvancedEffectsEnabled);
}
```

`src/Gili.Platform/Shell/Autostart.cs`:

```csharp
using Microsoft.Win32;

namespace Gili.Platform.Shell;

/// <summary>The start-with-Windows entry under HKCU\Software\Microsoft\Windows\CurrentVersion\Run (F-BAR-04).</summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Gili";

    public static void Apply(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
```

`src/Gili.Platform/Shell/WindowStyles.cs`:

```csharp
using Gili.Platform.Interop;

namespace Gili.Platform.Shell;

public static class WindowStyles
{
    /// <summary>Keeps the bar out of Alt+Tab, and stops clicks on it from taking focus from the active app.</summary>
    public static void MakeNonActivatingToolWindow(IntPtr hwnd)
    {
        var style = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE);
        Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, style | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE);
    }
}
```

- [ ] **Step 5: Build**

Run: `dotnet build Gili.slnx`
Expected: Build succeeded, 0 error.

- [ ] **Step 6: Commit**

```bash
git add Gili.slnx src/Gili.Platform
git commit -m "feat(platform): AppBar, glass material, transparency watcher and autostart (F-BAR-01..04, F-BAR-06)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Sampler CPU, RAM, dan jaringan

**Files:**
- Create: `src/Gili.Platform/Stats/CpuSampler.cs`, `MemorySampler.cs`, `NetworkSampler.cs`
- Modify: `src/Gili.Platform/Gili.Platform.csproj` (paket PerformanceCounter)

**Interfaces:**
- Consumes: `CpuReading`, `MemoryReading`, `NetworkReading` (Task 4).
- Produces (namespace `Gili.Platform.Stats`): `CpuSampler : IDisposable` dengan `CpuReading Sample()`; `MemorySampler` dengan `MemoryReading Sample()`; `NetworkSampler` dengan `NetworkReading Sample()`. Setiap `Sample()` boleh melempar exception; `ModuleSampler` yang menangkapnya.

- [ ] **Step 1: Tambah paket**

```bash
dotnet add src/Gili.Platform package System.Diagnostics.PerformanceCounter
```

- [ ] **Step 2: Implementasi**

`src/Gili.Platform/Stats/CpuSampler.cs`:

```csharp
using System.Diagnostics;
using Gili.Core.Modules;

namespace Gili.Platform.Stats;

/// <summary>
/// "% Processor Utility" is the counter Task Manager shows (F-RIGHT-01 allows 5 points of difference);
/// systems without it fall back to "% Processor Time". The first sample after creation reads 0.
/// </summary>
public sealed class CpuSampler : IDisposable
{
    private PerformanceCounter? _counter;

    public CpuReading Sample()
    {
        try
        {
            _counter ??= Create();
            return new CpuReading(Math.Clamp(_counter.NextValue(), 0, 100));
        }
        catch
        {
            // Recreate the counter on the next try instead of reusing a broken one.
            _counter?.Dispose();
            _counter = null;
            throw;
        }
    }

    public void Dispose() => _counter?.Dispose();

    private static PerformanceCounter Create() =>
        PerformanceCounterCategory.CounterExists("% Processor Utility", "Processor Information")
            ? new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", readOnly: true)
            : new PerformanceCounter("Processor", "% Processor Time", "_Total", readOnly: true);
}
```

`src/Gili.Platform/Stats/MemorySampler.cs`:

```csharp
using System.ComponentModel;
using System.Runtime.InteropServices;
using Gili.Core.Modules;

namespace Gili.Platform.Stats;

public sealed class MemorySampler
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

    public MemoryReading Sample()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status)) throw new Win32Exception(Marshal.GetLastPInvokeError());
        var used = status.ullTotalPhys - status.ullAvailPhys;
        return new MemoryReading(used * 100.0 / status.ullTotalPhys, used, status.ullTotalPhys);
    }
}
```

`src/Gili.Platform/Stats/NetworkSampler.cs`:

```csharp
using System.Diagnostics;
using System.Net.NetworkInformation;
using Gili.Core.Modules;

namespace Gili.Platform.Stats;

/// <summary>Upload and download speed from the byte counters of every adapter that is up (PRD 9.2).</summary>
public sealed class NetworkSampler
{
    private long _lastSent = -1;
    private long _lastReceived = -1;
    private long _lastTimestamp;

    public NetworkReading Sample()
    {
        long sent = 0, received = 0;
        var names = new List<string>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
            try
            {
                var stats = nic.GetIPv4Statistics();
                sent += stats.BytesSent;
                received += stats.BytesReceived;
                names.Add(nic.Name);
            }
            catch (NetworkInformationException)
            {
                // An adapter going away mid-enumeration; the next sample skips it.
            }
        }

        var timestamp = Stopwatch.GetTimestamp();
        double up = 0, down = 0;
        if (_lastSent >= 0)
        {
            var seconds = Stopwatch.GetElapsedTime(_lastTimestamp, timestamp).TotalSeconds;
            if (seconds > 0)
            {
                // Counters drop when an adapter disconnects; never show a negative speed.
                up = Math.Max(0, sent - _lastSent) / seconds;
                down = Math.Max(0, received - _lastReceived) / seconds;
            }
        }
        (_lastSent, _lastReceived, _lastTimestamp) = (sent, received, timestamp);
        return new NetworkReading(up, down, string.Join(", ", names));
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build Gili.slnx`
Expected: Build succeeded, 0 error.

- [ ] **Step 4: Commit**

```bash
git add src/Gili.Platform
git commit -m "feat(platform): CPU, memory and network samplers (F-RIGHT-01)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Kerangka app: satu instance, crash, settings, bar kosong dengan AppBar dan kaca

**Files:**
- Create: `src/Gili.App/Gili.App.csproj`, `app.manifest`, `App.xaml`, `App.xaml.cs`, `SingleInstance.cs`, `CrashHandler.cs`, `SettingsHub.cs`
- Create: `src/Gili.App/Bar/BarWindow.xaml`, `Bar/BarWindow.xaml.cs`, `Bar/BarViewModel.cs`
- Delete: `src/Gili.App/MainWindow.xaml`, `MainWindow.xaml.cs` (dari template)

**Interfaces:**
- Consumes: semua dari Task 2–6.
- Produces:
  - `Gili.App.SettingsHub(SettingsStore store, AppSettings initial, FileLog log)`: `AppSettings Current`, `event Action<AppSettings>? Changed`, `void Update(Func<AppSettings, AppSettings> change)`, `void Flush()`.
  - `Gili.App.Bar.BarWindow`: `event Action? QuitRequested`, `void ReleaseAppBar()`, `void Reassert()`.
  - `Gili.App.Bar.BarViewModel`: `Brush Tint`, `void ApplySettings(AppSettings settings, BarAppearance look)`.

- [ ] **Step 1: Buat proyek**

```bash
dotnet new wpf -n Gili.App -o src/Gili.App -f net10.0
rm src/Gili.App/MainWindow.xaml src/Gili.App/MainWindow.xaml.cs
dotnet sln Gili.slnx add src/Gili.App/Gili.App.csproj
```

Ganti isi `src/Gili.App/Gili.App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.19041.0</TargetFramework>
    <SupportedOSPlatformVersion>10.0.19041.0</SupportedOSPlatformVersion>
    <UseWPF>true</UseWPF>
    <AssemblyName>Gili</AssemblyName>
    <RootNamespace>Gili.App</RootNamespace>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <!-- WPF, not WinRT AOT: field-backed [ObservableProperty] is fine here. -->
    <NoWarn>$(NoWarn);MVVMTK0045</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Gili.Core\Gili.Core.csproj" />
    <ProjectReference Include="..\Gili.Data\Gili.Data.csproj" />
    <ProjectReference Include="..\Gili.Platform\Gili.Platform.csproj" />
  </ItemGroup>
</Project>
```

```bash
dotnet add src/Gili.App package CommunityToolkit.Mvvm
```

`src/Gili.App/app.manifest` (DPI per monitor, supaya bar tajam di 100–200% dan tinggi AppBar dihitung dalam piksel fisik):

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="Gili.app" />
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 2: `SingleInstance`, `CrashHandler`, `SettingsHub`**

`src/Gili.App/SingleInstance.cs`:

```csharp
namespace Gili.App;

/// <summary>F-BAR-05: a second launch tells the running instance to come forward, then exits.</summary>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\Gili.SingleInstance";
    private const string SignalName = @"Local\Gili.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private readonly RegisteredWaitHandle _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle signal, Action onActivateRequest)
    {
        _mutex = mutex;
        _signal = signal;
        _registration = ThreadPool.RegisterWaitForSingleObject(
            signal, (_, _) => onActivateRequest(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    /// <summary>
    /// Returns null when another instance is running. <paramref name="wait"/> lets a restart after a crash wait
    /// for the dying instance to let go of the mutex instead of giving up at once.
    /// </summary>
    public static SingleInstance? Acquire(TimeSpan wait, Action onActivateRequest)
    {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool owned;
        try
        {
            owned = mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            owned = true;
        }

        var signal = new EventWaitHandle(false, EventResetMode.AutoReset, SignalName);
        if (owned) return new SingleInstance(mutex, signal, onActivateRequest);

        signal.Set();
        signal.Dispose();
        mutex.Dispose();
        return null;
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _signal.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
```

`src/Gili.App/CrashHandler.cs`:

```csharp
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Gili.Core.Diagnostics;

namespace Gili.App;

/// <summary>
/// PRD 11: log the exception, release the AppBar strip, restart once. A second crash within a minute stays down.
/// The toast that should say so needs the notification service from Phase 2; until then only the log records it.
/// </summary>
internal sealed class CrashHandler(FileLog log, string markerPath, Action releaseAppBar)
{
    private int _handling;

    public void Attach(Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            Fail(e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Fail(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Write("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    private void Fail(Exception? exception)
    {
        if (Interlocked.Exchange(ref _handling, 1) == 1) return;
        TryRun(() => log.Write("Unhandled exception", exception));
        TryRun(releaseAppBar);

        var now = DateTimeOffset.Now;
        var previous = ReadMarker();
        TryRun(() => File.WriteAllText(markerPath, now.ToString("O", CultureInfo.InvariantCulture)));
        if (CrashPolicy.ShouldRestart(previous, now) && Environment.ProcessPath is { } exe)
            TryRun(() => Process.Start(new ProcessStartInfo(exe, "--restarted") { UseShellExecute = false }));
        Environment.Exit(1);
    }

    private DateTimeOffset? ReadMarker()
    {
        try
        {
            return DateTimeOffset.Parse(File.ReadAllText(markerPath), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryRun(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // Already crashing: nothing better to do than keep going towards exit.
        }
    }
}
```

`src/Gili.App/SettingsHub.cs`:

```csharp
using System.IO;
using System.Windows.Threading;
using Gili.Core.Diagnostics;
using Gili.Core.Settings;
using Gili.Data;

namespace Gili.App;

/// <summary>The live settings. Changes apply at once; writes to settings.json are coalesced 300 ms after the last change.</summary>
public sealed class SettingsHub
{
    private readonly SettingsStore _store;
    private readonly FileLog _log;
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public SettingsHub(SettingsStore store, AppSettings initial, FileLog log)
    {
        _store = store;
        _log = log;
        Current = initial;
        _saveTimer.Tick += (_, _) => Flush();
    }

    public AppSettings Current { get; private set; }

    public event Action<AppSettings>? Changed;

    public void Update(Func<AppSettings, AppSettings> change)
    {
        var next = change(Current).Normalize();
        if (next == Current) return;
        Current = next;
        _saveTimer.Stop();
        _saveTimer.Start();
        Changed?.Invoke(next);
    }

    public void Flush()
    {
        if (!_saveTimer.IsEnabled) return;
        _saveTimer.Stop();
        try
        {
            _store.Save(Current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Write("Could not save settings.json", ex);
        }
    }
}
```

- [ ] **Step 3: `BarViewModel` dan `BarWindow`**

`src/Gili.App/Bar/BarViewModel.cs`:

```csharp
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Settings;

namespace Gili.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    [ObservableProperty] private Brush _tint = Brushes.Transparent;

    public void ApplySettings(AppSettings settings, BarAppearance look)
    {
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(look.TintOpacity * 255), 0x16, 0x16, 0x18));
        brush.Freeze();
        Tint = brush;
    }
}
```

`src/Gili.App/Bar/BarWindow.xaml`:

```xml
<Window x:Class="Gili.App.Bar.BarWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Gili" WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" ShowActivated="False" Topmost="True"
        Left="0" Top="0" Width="800" Height="32" UseLayoutRounding="True"
        FontFamily="Segoe UI Variable Text, Segoe UI" FontSize="12" Foreground="#F5F5F7">
    <Window.Resources>
        <Style x:Key="BarMenu" TargetType="ContextMenu">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="FontFamily" Value="Segoe UI Variable Text, Segoe UI" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ContextMenu">
                        <Border Background="#1C1C1E" CornerRadius="12" Padding="4" BorderBrush="#12FFFFFF" BorderThickness="1">
                            <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Cycle" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="BarMenuItem" TargetType="MenuItem">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#F5F5F7" />
            <Setter Property="FontSize" Value="13" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="MenuItem">
                        <Border x:Name="Bg" MinWidth="180" Padding="10,7" CornerRadius="8" Background="Transparent">
                            <ContentPresenter ContentSource="Header" RecognizesAccessKey="True" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsHighlighted" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#3A3A3C" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="BarMenuSeparator" TargetType="Separator">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Separator">
                        <Border Height="1" Margin="6,4" Background="#14FFFFFF" />
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Border Background="{Binding Tint}" BorderBrush="#0FFFFFFF" BorderThickness="0,0,0,1">
        <Border.ContextMenu>
            <ContextMenu x:Name="BarMenu" Style="{StaticResource BarMenu}">
                <MenuItem Header="Quit" Style="{StaticResource BarMenuItem}" Click="OnQuit" />
            </ContextMenu>
        </Border.ContextMenu>
        <Grid Margin="10,0" />
    </Border>
</Window>
```

`src/Gili.App/Bar/BarWindow.xaml.cs`:

```csharp
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Gili.Core.Settings;
using Gili.Platform.Shell;

namespace Gili.App.Bar;

public partial class BarWindow : Window
{
    private readonly SettingsHub _settings;
    private readonly TransparencyWatcher _transparency;
    private readonly BarViewModel _vm = new();
    private AppBarService? _appBar;
    private IntPtr _hwnd;
    private bool _quitting;

    public BarWindow(SettingsHub settings, TransparencyWatcher transparency)
    {
        _settings = settings;
        _transparency = transparency;
        InitializeComponent();
        DataContext = _vm;
        _settings.Changed += OnSettingsChanged;
        _transparency.Changed += _ => Dispatcher.InvokeAsync(ApplyAppearance);
#if DEBUG
        var crash = new MenuItem { Header = "Simulate crash", Style = (Style)FindResource("BarMenuItem") };
        crash.Click += (_, _) => throw new InvalidOperationException("Simulated crash from the bar menu");
        BarMenu.Items.Add(crash);
#endif
    }

    public event Action? QuitRequested;

    public void ReleaseAppBar() => _appBar?.Unregister();

    public void Reassert() => _appBar?.Reposition();

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
        // `taskkill` without /F closes the window: treat it as Quit so the AppBar strip is released.
        if (!_quitting)
        {
            _quitting = true;
            QuitRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _appBar?.SetHeight(settings.BarHeight);
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        var look = BarAppearance.Resolve(_settings.Current, _transparency.Enabled);
        BarMaterial.Apply(_hwnd, look.Glass);
        _vm.ApplySettings(_settings.Current, look);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        if (fullscreen)
        {
            Hide();
            return;
        }
        Show();
        _appBar?.Reposition();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_appBar?.HandleMessage(msg, wParam, lParam) == true) handled = true;
        return IntPtr.Zero;
    }

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
```

- [ ] **Step 4: `App`**

`src/Gili.App/App.xaml`:

```xml
<Application x:Class="Gili.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources />
</Application>
```

`src/Gili.App/App.xaml.cs`:

```csharp
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Markup;
using Gili.App.Bar;
using Gili.Core;
using Gili.Core.Diagnostics;
using Gili.Data;
using Gili.Platform.Shell;

namespace Gili.App;

public partial class App : Application
{
    private FileLog? _log;
    private SingleInstance? _instance;
    private SettingsHub? _settings;
    private TransparencyWatcher? _transparency;
    private BarWindow? _bar;
    private bool _quitting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UseEnglishFormatting();
        Directory.CreateDirectory(AppPaths.Root);
        _log = new FileLog(AppPaths.Logs);
        _log.DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
        new CrashHandler(_log, AppPaths.CrashMarker, () => _bar?.ReleaseAppBar()).Attach(this);

        var restarted = e.Args.Contains("--restarted");
        _instance = SingleInstance.Acquire(restarted ? TimeSpan.FromSeconds(5) : TimeSpan.Zero,
            () => Dispatcher.InvokeAsync(() => _bar?.Reassert()));
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        var store = new SettingsStore(AppPaths.Root);
        var loaded = store.Load();
        if (loaded.WasCorrupt) _log.Write("settings.json could not be read; renamed to settings.json.bak and using defaults");
        _settings = new SettingsHub(store, loaded.Settings, _log);
        _transparency = new TransparencyWatcher();

        _bar = new BarWindow(_settings, _transparency);
        _bar.QuitRequested += Quit;
        _bar.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _bar?.ReleaseAppBar();
        _settings?.Flush();
        _transparency?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _bar?.ReleaseAppBar();
        _settings?.Flush();
        base.OnSessionEnding(e);
    }

    private void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        Shutdown();
    }

    /// <summary>The UI is English only, so numbers and dates never follow the Windows locale.</summary>
    private static void UseEnglishFormatting()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(english.IetfLanguageTag)));
    }
}
```

- [ ] **Step 5: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua test PASS.

- [ ] **Step 6: Periksa di laptop**

Jalankan `dotnet run --project src/Gili.App`, lalu periksa:
1. Bar 32 px muncul di tepi atas dengan kaca acrylic, garis bawah tipis.
2. Jendela yang di-maximize mulai tepat di bawah bar. Work area bisa dicek dengan PowerShell: `Add-Type -AssemblyName System.Windows.Forms; [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea` → `Y` = 32 × skala DPI.
3. Menjalankan `dotnet run` kedua kali langsung keluar, bar tetap satu.
4. `Stop-Process -Name explorer` (Explorer hidup lagi sendiri) → bar tetap di atas dan ruangnya tetap dipesan.
5. Video YouTube fullscreen → bar hilang; keluar fullscreen → bar kembali ≤ 1 detik. Klik desktop tidak menyembunyikan bar.
6. Tulis `%LOCALAPPDATA%\Gili\settings.json` berisi `{ "glass": "light" }`, lalu `{ "glass": "off", "barOpacity": 100 }`, restart app di antaranya → blur lebih ringan, lalu tint polos.
7. Windows Settings › Personalization › Colors › Transparency effects mati → bar langsung pekat; nyalakan lagi → kaca kembali.
8. Klik kanan bar → Simulate crash → app hidup lagi dan log berisi exception di `%LOCALAPPDATA%\Gili\logs\`. Simulate crash lagi dalam 1 menit → app berhenti dan ruang AppBar dilepas.
9. Klik kanan → Quit → ruang di atas dilepas.

Kalau blur tidak muncul di langkah 1 (bar hanya tint), catat hasilnya. Cadangannya: `AllowsTransparency="False"` + `WindowChrome` dengan `GlassFrameThickness="-1"`. Kalau blur muncul, ubah baris "Material bar" di PRD 9.2 menjadi `SetWindowCompositionAttribute` (accent policy: blur-behind untuk Light, acrylic untuk Strong), karena backdrop DWM menjadi polos di jendela yang tidak aktif.

- [ ] **Step 7: Commit**

```bash
git add Gili.slnx src/Gili.App docs/superpowers/specs
git commit -m "feat(app): bar window with AppBar, glass, single instance and crash restart (F-BAR-01..06)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Jam di pulau compact dan modul CPU/RAM/jaringan

**Files:**
- Create: `src/Gili.App/Bar/StatsService.cs`
- Modify (ganti seluruh isi): `src/Gili.App/Bar/BarViewModel.cs`, `Bar/BarWindow.xaml`, `Bar/BarWindow.xaml.cs`, `App.xaml.cs`

**Interfaces:**
- Consumes: `ClockText`, `StatText`, `BarMetrics`, `ModuleSampler`, readings (Task 1–4); samplers (Task 7).
- Produces:
  - `sealed record StatsSnapshot(ModuleState<CpuReading> Cpu, ModuleState<MemoryReading> Memory, ModuleState<NetworkReading> Network)`.
  - `StatsService(Dispatcher dispatcher, Func<CpuReading> cpu, Func<MemoryReading> memory, Func<NetworkReading> network) : IDisposable`: `bool Paused`, `event Action<StatsSnapshot>? Updated` (di thread UI), `void Start()`.
  - `BarWindow(SettingsHub settings, TransparencyWatcher transparency, StatsService stats)`.
  - `BarViewModel.RefreshClock()`, `BarViewModel.ApplyStats(StatsSnapshot stats)`.

- [ ] **Step 1: `StatsService`**

`src/Gili.App/Bar/StatsService.cs`:

```csharp
using System.Windows.Threading;
using Gili.Core.Modules;

namespace Gili.App.Bar;

public sealed record StatsSnapshot(
    ModuleState<CpuReading> Cpu,
    ModuleState<MemoryReading> Memory,
    ModuleState<NetworkReading> Network);

/// <summary>Samples CPU, RAM and network once a second off the UI thread (F-RIGHT-01).</summary>
public sealed class StatsService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly ModuleSampler<CpuReading> _cpu;
    private readonly ModuleSampler<MemoryReading> _memory;
    private readonly ModuleSampler<NetworkReading> _network;
    private readonly CancellationTokenSource _stop = new();
    private volatile bool _paused;

    public StatsService(Dispatcher dispatcher, Func<CpuReading> cpu, Func<MemoryReading> memory, Func<NetworkReading> network)
    {
        _dispatcher = dispatcher;
        _cpu = new ModuleSampler<CpuReading>(cpu);
        _memory = new ModuleSampler<MemoryReading>(memory);
        _network = new ModuleSampler<NetworkReading>(network);
    }

    /// <summary>Set while the bar is hidden by a fullscreen app or the laptop sleeps (PRD 8.1).</summary>
    public bool Paused
    {
        get => _paused;
        set => _paused = value;
    }

    /// <summary>Raised on the UI thread.</summary>
    public event Action<StatsSnapshot>? Updated;

    public void Start() => _ = Task.Run(RunAsync);

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            do
            {
                if (_paused) continue;
                var now = DateTimeOffset.UtcNow;
                var snapshot = new StatsSnapshot(_cpu.Tick(now), _memory.Tick(now), _network.Tick(now));
                _ = _dispatcher.InvokeAsync(() => Updated?.Invoke(snapshot));
            }
            while (await timer.WaitForNextTickAsync(_stop.Token));
        }
        catch (OperationCanceledException)
        {
            // App is closing.
        }
    }
}
```

- [ ] **Step 2: `BarViewModel` lengkap**

`src/Gili.App/Bar/BarViewModel.cs`:

```csharp
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Formatting;
using Gili.Core.Layout;
using Gili.Core.Settings;

namespace Gili.App.Bar;

public sealed partial class BarViewModel : ObservableObject
{
    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Normal);
    private bool _use24Hour = true;

    [ObservableProperty] private Brush _tint = Brushes.Transparent;
    [ObservableProperty] private double _islandHeight = 24;
    [ObservableProperty] private CornerRadius _islandCornerRadius = new(12);
    [ObservableProperty] private double _islandFontSize = 12;
    [ObservableProperty] private double _moduleHeight = 26;
    [ObservableProperty] private string _time = "";
    [ObservableProperty] private string _date = "";
    [ObservableProperty] private string _cpuText = "—";
    [ObservableProperty] private string? _cpuTooltip;
    [ObservableProperty] private string _ramText = "—";
    [ObservableProperty] private string? _ramTooltip;
    [ObservableProperty] private string _upText = "↑ —";
    [ObservableProperty] private string _downText = "↓ —";
    [ObservableProperty] private string? _netTooltip;

    public BarViewModel() => _clockTimer.Tick += (_, _) => RefreshClock();

    public void ApplySettings(AppSettings settings, BarAppearance look)
    {
        _use24Hour = settings.Use24HourClock;
        var brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(look.TintOpacity * 255), 0x16, 0x16, 0x18));
        brush.Freeze();
        Tint = brush;
        IslandHeight = settings.IslandHeight;
        IslandCornerRadius = new CornerRadius(BarMetrics.IslandCornerRadius(settings.IslandHeight));
        IslandFontSize = BarMetrics.IslandFontSize(settings.IslandHeight);
        ModuleHeight = BarMetrics.ModuleHitHeight(settings.BarHeight);
        RefreshClock();
    }

    /// <summary>Shows the current minute and re-arms the timer just past the next minute boundary (F-ISL-01).</summary>
    public void RefreshClock()
    {
        var now = DateTime.Now;
        Time = ClockText.Time(now, _use24Hour);
        Date = ClockText.Date(now);
        _clockTimer.Stop();
        _clockTimer.Interval = ClockText.UntilNextMinute(now) + TimeSpan.FromMilliseconds(50);
        _clockTimer.Start();
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

- [ ] **Step 3: `BarWindow.xaml` lengkap**

Ganti seluruh isi `src/Gili.App/Bar/BarWindow.xaml`. Bagian `Window.Resources` yang lama (`BarMenu`, `BarMenuItem`, `BarMenuSeparator`) tetap dipakai persis, ditambah gaya modul:

```xml
<Window x:Class="Gili.App.Bar.BarWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Gili" WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        ResizeMode="NoResize" ShowInTaskbar="False" ShowActivated="False" Topmost="True"
        Left="0" Top="0" Width="800" Height="32" UseLayoutRounding="True"
        FontFamily="Segoe UI Variable Text, Segoe UI" FontSize="12" Foreground="#F5F5F7">
    <Window.Resources>
        <Style x:Key="BarMenu" TargetType="ContextMenu">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="FontFamily" Value="Segoe UI Variable Text, Segoe UI" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ContextMenu">
                        <Border Background="#1C1C1E" CornerRadius="12" Padding="4" BorderBrush="#12FFFFFF" BorderThickness="1">
                            <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Cycle" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="BarMenuItem" TargetType="MenuItem">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Foreground" Value="#F5F5F7" />
            <Setter Property="FontSize" Value="13" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="MenuItem">
                        <Border x:Name="Bg" MinWidth="180" Padding="10,7" CornerRadius="8" Background="Transparent">
                            <ContentPresenter ContentSource="Header" RecognizesAccessKey="True" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsHighlighted" Value="True">
                                <Setter TargetName="Bg" Property="Background" Value="#3A3A3C" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
        <Style x:Key="BarMenuSeparator" TargetType="Separator">
            <Setter Property="OverridesDefaultStyle" Value="True" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Separator">
                        <Border Height="1" Margin="6,4" Background="#14FFFFFF" />
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Right-hand modules, two-line style (PRD 6.4). -->
        <Style x:Key="Module" TargetType="Border">
            <Setter Property="Height" Value="{Binding ModuleHeight}" />
            <Setter Property="Padding" Value="6,0" />
            <Setter Property="Margin" Value="12,0,0,0" />
            <Setter Property="CornerRadius" Value="6" />
            <Setter Property="Background" Value="Transparent" />
        </Style>
        <Style x:Key="ClickableModule" TargetType="Border" BasedOn="{StaticResource Module}">
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#14FFFFFF" />
                </Trigger>
            </Style.Triggers>
        </Style>
        <Style x:Key="TwoLine" TargetType="StackPanel">
            <Setter Property="VerticalAlignment" Value="Center" />
            <Setter Property="MinWidth" Value="28" />
        </Style>
        <Style x:Key="ModuleLabel" TargetType="TextBlock">
            <Setter Property="FontSize" Value="8.5" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Foreground" Value="#9A9AA0" />
            <Setter Property="LineStackingStrategy" Value="BlockLineHeight" />
            <Setter Property="LineHeight" Value="9.2" />
            <Setter Property="Margin" Value="0,0,0,1" />
        </Style>
        <Style x:Key="ModuleValue" TargetType="TextBlock">
            <Setter Property="FontSize" Value="11.5" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="LineStackingStrategy" Value="BlockLineHeight" />
            <Setter Property="LineHeight" Value="12.4" />
            <Setter Property="Typography.NumeralAlignment" Value="Tabular" />
        </Style>
        <Style x:Key="NetLine" TargetType="TextBlock">
            <Setter Property="FontSize" Value="9.5" />
            <Setter Property="Foreground" Value="#D1D1D6" />
            <Setter Property="LineStackingStrategy" Value="BlockLineHeight" />
            <Setter Property="LineHeight" Value="11.4" />
            <Setter Property="Typography.NumeralAlignment" Value="Tabular" />
        </Style>
    </Window.Resources>

    <Border Background="{Binding Tint}" BorderBrush="#0FFFFFFF" BorderThickness="0,0,0,1">
        <Border.ContextMenu>
            <ContextMenu x:Name="BarMenu" Style="{StaticResource BarMenu}">
                <MenuItem Header="Quit" Style="{StaticResource BarMenuItem}" Click="OnQuit" />
            </ContextMenu>
        </Border.ContextMenu>
        <Grid Margin="10,0">
            <Grid.ColumnDefinitions>
                <!-- Equal star columns keep the island centred on the screen whatever the sides hold. -->
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <Border Grid.Column="1" Background="#000000" MinWidth="200" Padding="12,0" VerticalAlignment="Center"
                    Height="{Binding IslandHeight}" CornerRadius="{Binding IslandCornerRadius}">
                <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <TextBlock Text="{Binding Time}" FontSize="{Binding IslandFontSize}" FontWeight="SemiBold"
                               Typography.NumeralAlignment="Tabular" />
                    <TextBlock Text="{Binding Date}" FontSize="{Binding IslandFontSize}" Foreground="#8E8E93" Margin="6,0,0,0" />
                </StackPanel>
            </Border>

            <StackPanel Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right" VerticalAlignment="Center">
                <Border Style="{StaticResource Module}" ToolTip="{Binding NetTooltip}">
                    <StackPanel VerticalAlignment="Center" MinWidth="58">
                        <TextBlock Text="{Binding UpText}" Style="{StaticResource NetLine}" />
                        <TextBlock Text="{Binding DownText}" Style="{StaticResource NetLine}" />
                    </StackPanel>
                </Border>
                <Border Style="{StaticResource ClickableModule}" ToolTip="{Binding CpuTooltip}" MouseLeftButtonUp="OnOpenTaskManager">
                    <StackPanel Style="{StaticResource TwoLine}">
                        <TextBlock Text="CPU" Style="{StaticResource ModuleLabel}" />
                        <TextBlock Text="{Binding CpuText}" Style="{StaticResource ModuleValue}" />
                    </StackPanel>
                </Border>
                <Border Style="{StaticResource ClickableModule}" ToolTip="{Binding RamTooltip}" MouseLeftButtonUp="OnOpenTaskManager">
                    <StackPanel Style="{StaticResource TwoLine}">
                        <TextBlock Text="RAM" Style="{StaticResource ModuleLabel}" />
                        <TextBlock Text="{Binding RamText}" Style="{StaticResource ModuleValue}" />
                    </StackPanel>
                </Border>
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 4: `BarWindow.xaml.cs` lengkap**

```csharp
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Gili.Core.Settings;
using Gili.Platform.Interop;
using Gili.Platform.Shell;

namespace Gili.App.Bar;

public partial class BarWindow : Window
{
    private readonly SettingsHub _settings;
    private readonly TransparencyWatcher _transparency;
    private readonly StatsService _stats;
    private readonly BarViewModel _vm = new();
    private AppBarService? _appBar;
    private IntPtr _hwnd;
    private bool _quitting;
    private bool _hidden;
    private bool _suspended;

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

    public void ReleaseAppBar() => _appBar?.Unregister();

    public void Reassert() => _appBar?.Reposition();

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
        // `taskkill` without /F closes the window: treat it as Quit so the AppBar strip is released.
        if (!_quitting)
        {
            _quitting = true;
            QuitRequested?.Invoke();
        }
        base.OnClosing(e);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        _appBar?.SetHeight(settings.BarHeight);
        ApplyAppearance();
    }

    private void ApplyAppearance()
    {
        var look = BarAppearance.Resolve(_settings.Current, _transparency.Enabled);
        BarMaterial.Apply(_hwnd, look.Glass);
        _vm.ApplySettings(_settings.Current, look);
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _hidden = fullscreen;
        UpdatePolling();
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
                _vm.RefreshClock();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMSUSPEND:
                _suspended = true;
                UpdatePolling();
                break;
            case WindowMessages.WM_POWERBROADCAST when wParam == WindowMessages.PBT_APMRESUMEAUTOMATIC:
                _suspended = false;
                UpdatePolling();
                _vm.RefreshClock();
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

    private void OnQuit(object sender, RoutedEventArgs e)
    {
        _quitting = true;
        QuitRequested?.Invoke();
    }
}
```

- [ ] **Step 5: `App.xaml.cs` dengan stats**

Di `src/Gili.App/App.xaml.cs`:
- tambahkan `using Gili.Platform.Stats;` dan field `private StatsService? _stats;`
- ganti blok pembuatan bar di `OnStartup` (mulai `_bar = new BarWindow(...)`) dengan:

```csharp
        var cpu = new CpuSampler();
        var memory = new MemorySampler();
        var network = new NetworkSampler();
        _stats = new StatsService(Dispatcher, cpu.Sample, memory.Sample, network.Sample);

        _bar = new BarWindow(_settings, _transparency, _stats);
        _bar.QuitRequested += Quit;
        _bar.Show();
        _stats.Start();
```

- di `OnExit`, tambahkan `_stats?.Dispose();` sebagai baris pertama.

- [ ] **Step 6: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua test PASS.

- [ ] **Step 7: Periksa di laptop**

`dotnet run --project src/Gili.App`, lalu:
1. Pulau hitam di tengah, lebar minimal 200 px: `19:38` semibold + `Mon 26 Oct` abu-abu, berganti tepat di pergantian menit.
2. Kanan: `↑ … / ↓ …`, `CPU / 12%`, `RAM / 54%` dalam gaya dua baris. CPU dan RAM berbeda ≤ 5 poin dari Task Manager (F-RIGHT-01). Unduh file besar → angka `↓` naik dalam MB/s.
3. Hover RAM → `8.6 / 15.7 GB`; hover jaringan → nama adapter. Klik CPU → Task Manager terbuka.
4. Digit tidak membuat bar bergoyang saat angka berubah.
5. Set `{ "barHeight": 28, "islandHeight": 24 }` dan `{ "barHeight": 44, "islandHeight": 40 }` di settings.json (restart app) → dua baris tetap muat, pulau dan teks ikut membesar.
6. Fullscreen YouTube 1 menit → setelah keluar, angka langsung jalan lagi.
7. RAM proses Gili di Task Manager < 120 MB, CPU saat diam < 1%.

- [ ] **Step 8: Commit**

```bash
git add src/Gili.App
git commit -m "feat(app): clock island and two-line CPU, RAM and network modules (F-ISL-01, F-RIGHT-01)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Jendela Settings dan autostart

**Files:**
- Create: `src/Gili.App/Settings/SettingsWindow.xaml`, `Settings/SettingsWindow.xaml.cs`, `Settings/SettingsViewModel.cs`
- Modify: `src/Gili.App/Gili.App.csproj` (NoWarn WPF0001), `Bar/BarWindow.xaml` (item Settings), `Bar/BarWindow.xaml.cs` (event Settings), `App.xaml.cs` (ganti seluruh isi)

**Interfaces:**
- Consumes: `SettingsHub` (Task 8), `Autostart`, `TransparencyWatcher` (Task 6), `AppPaths` (Task 2).
- Produces: `SettingsViewModel(SettingsHub hub, bool transparencyEnabled, bool installedBuild)`, `SettingsWindow(SettingsViewModel vm)`, `BarWindow.SettingsRequested` (`event Action?`).

- [ ] **Step 1: `SettingsViewModel`**

`src/Gili.App/Settings/SettingsViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Gili.Core.Settings;

namespace Gili.App.Settings;

/// <summary>Phase 1 part of F-BAR-08: clock format, material, bar and island height, autostart. Every change applies at once.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsHub _hub;
    private bool _loading;

    [ObservableProperty] private double _barOpacity;
    [ObservableProperty] private GlassLevel _glass;
    [ObservableProperty] private double _barHeight;
    [ObservableProperty] private double _islandHeight;
    [ObservableProperty] private double _maxIslandHeight;
    [ObservableProperty] private bool _use24HourClock;
    [ObservableProperty] private bool _autostart;

    public SettingsViewModel(SettingsHub hub, bool transparencyEnabled, bool installedBuild)
    {
        _hub = hub;
        TransparencyOff = !transparencyEnabled;
        AutostartNeedsInstall = !installedBuild;
        Load(hub.Current);
    }

    public GlassLevel[] GlassLevels { get; } = Enum.GetValues<GlassLevel>();
    public bool TransparencyOff { get; }
    public bool AutostartNeedsInstall { get; }

    partial void OnBarOpacityChanged(double value) => Push();
    partial void OnGlassChanged(GlassLevel value) => Push();
    partial void OnBarHeightChanged(double value) => Push();
    partial void OnIslandHeightChanged(double value) => Push();
    partial void OnUse24HourClockChanged(bool value) => Push();
    partial void OnAutostartChanged(bool value) => Push();

    private void Load(AppSettings settings)
    {
        _loading = true;
        BarOpacity = settings.BarOpacity;
        Glass = settings.Glass;
        BarHeight = settings.BarHeight;
        MaxIslandHeight = AppSettings.MaxIslandHeight(settings.BarHeight);
        IslandHeight = settings.IslandHeight;
        Use24HourClock = settings.Use24HourClock;
        Autostart = settings.Autostart;
        _loading = false;
    }

    private void Push()
    {
        if (_loading) return;
        _hub.Update(s => s with
        {
            BarOpacity = (int)Math.Round(BarOpacity),
            Glass = Glass,
            BarHeight = (int)Math.Round(BarHeight),
            IslandHeight = (int)Math.Round(IslandHeight),
            Use24HourClock = Use24HourClock,
            Autostart = Autostart,
        });
        // Shows the clamped values, e.g. the island shrinking with a lower bar.
        Load(_hub.Current);
    }
}
```

- [ ] **Step 2: `SettingsWindow`**

Tambahkan `WPF0001` ke `<NoWarn>` di `src/Gili.App/Gili.App.csproj` (`Window.ThemeMode` masih ditandai eksperimental):

```xml
    <NoWarn>$(NoWarn);MVVMTK0045;WPF0001</NoWarn>
```

`src/Gili.App/Settings/SettingsWindow.xaml`:

```xml
<Window x:Class="Gili.App.Settings.SettingsWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Gili Settings" Width="440" SizeToContent="Height" ResizeMode="NoResize"
        WindowStartupLocation="CenterScreen" ThemeMode="Dark">
    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="VisibleWhen" />
        <Style x:Key="Section" TargetType="TextBlock">
            <Setter Property="FontSize" Value="14" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Margin" Value="0,16,0,6" />
        </Style>
        <Style x:Key="Hint" TargetType="TextBlock">
            <Setter Property="FontSize" Value="12" />
            <Setter Property="Opacity" Value="0.7" />
            <Setter Property="TextWrapping" Value="Wrap" />
            <Setter Property="Margin" Value="0,4,0,0" />
        </Style>
        <Style x:Key="Value" TargetType="TextBlock">
            <Setter Property="HorizontalAlignment" Value="Right" />
            <Setter Property="VerticalAlignment" Value="Center" />
            <Setter Property="Typography.NumeralAlignment" Value="Tabular" />
        </Style>
    </Window.Resources>

    <StackPanel Margin="24,8,24,24">
        <TextBlock Text="Appearance" Style="{StaticResource Section}" />
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="110" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="52" />
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="40" />
                <RowDefinition Height="40" />
                <RowDefinition Height="40" />
                <RowDefinition Height="40" />
            </Grid.RowDefinitions>

            <TextBlock Text="Bar opacity" VerticalAlignment="Center" />
            <Slider Grid.Column="1" Minimum="40" Maximum="100" TickFrequency="1" IsSnapToTickEnabled="True"
                    VerticalAlignment="Center" Value="{Binding BarOpacity}" />
            <TextBlock Grid.Column="2" Style="{StaticResource Value}" Text="{Binding BarOpacity, StringFormat={}{0:0}%}" />

            <TextBlock Grid.Row="1" Text="Glass" VerticalAlignment="Center" />
            <ComboBox Grid.Row="1" Grid.Column="1" Grid.ColumnSpan="2" VerticalAlignment="Center"
                      ItemsSource="{Binding GlassLevels}" SelectedItem="{Binding Glass}" />

            <TextBlock Grid.Row="2" Text="Bar height" VerticalAlignment="Center" />
            <Slider Grid.Row="2" Grid.Column="1" Minimum="28" Maximum="44" TickFrequency="1" IsSnapToTickEnabled="True"
                    VerticalAlignment="Center" Value="{Binding BarHeight}" />
            <TextBlock Grid.Row="2" Grid.Column="2" Style="{StaticResource Value}" Text="{Binding BarHeight, StringFormat={}{0:0} px}" />

            <TextBlock Grid.Row="3" Text="Island height" VerticalAlignment="Center" />
            <Slider Grid.Row="3" Grid.Column="1" Minimum="20" Maximum="{Binding MaxIslandHeight}" TickFrequency="1"
                    IsSnapToTickEnabled="True" VerticalAlignment="Center" Value="{Binding IslandHeight}" />
            <TextBlock Grid.Row="3" Grid.Column="2" Style="{StaticResource Value}" Text="{Binding IslandHeight, StringFormat={}{0:0} px}" />
        </Grid>
        <TextBlock Style="{StaticResource Hint}" Text="Transparency effects are off in Windows, so the bar is drawn solid."
                   Visibility="{Binding TransparencyOff, Converter={StaticResource VisibleWhen}}" />

        <TextBlock Text="Clock" Style="{StaticResource Section}" />
        <CheckBox Content="24-hour clock" IsChecked="{Binding Use24HourClock}" />

        <TextBlock Text="General" Style="{StaticResource Section}" />
        <CheckBox Content="Start with Windows" IsChecked="{Binding Autostart}" />
        <TextBlock Style="{StaticResource Hint}" Text="Takes effect in the installed copy (%LOCALAPPDATA%\Programs\Gili)."
                   Visibility="{Binding AutostartNeedsInstall, Converter={StaticResource VisibleWhen}}" />
    </StackPanel>
</Window>
```

`src/Gili.App/Settings/SettingsWindow.xaml.cs`:

```csharp
using System.Windows;

namespace Gili.App.Settings;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
```

- [ ] **Step 3: Item Settings di menu bar**

Di `src/Gili.App/Bar/BarWindow.xaml`, ganti isi `ContextMenu` menjadi:

```xml
            <ContextMenu x:Name="BarMenu" Style="{StaticResource BarMenu}">
                <MenuItem Header="Settings" Style="{StaticResource BarMenuItem}" Click="OnSettings" />
                <Separator Style="{StaticResource BarMenuSeparator}" />
                <MenuItem Header="Quit" Style="{StaticResource BarMenuItem}" Click="OnQuit" />
            </ContextMenu>
```

Di `src/Gili.App/Bar/BarWindow.xaml.cs`, tambahkan di bawah `public event Action? QuitRequested;`:

```csharp
    public event Action? SettingsRequested;
```

dan di atas `private void OnQuit(...)`:

```csharp
    private void OnSettings(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();
```

- [ ] **Step 4: `App.xaml.cs` lengkap**

Ganti seluruh isi `src/Gili.App/App.xaml.cs`:

```csharp
using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Markup;
using Gili.App.Bar;
using Gili.App.Settings;
using Gili.Core;
using Gili.Core.Diagnostics;
using Gili.Core.Settings;
using Gili.Data;
using Gili.Platform.Shell;
using Gili.Platform.Stats;

namespace Gili.App;

public partial class App : Application
{
    private static readonly bool IsInstalledBuild =
        Environment.ProcessPath is { } exe && AppPaths.IsUnder(exe, AppPaths.InstallDirectory);

    private FileLog? _log;
    private SingleInstance? _instance;
    private SettingsHub? _settings;
    private TransparencyWatcher? _transparency;
    private StatsService? _stats;
    private BarWindow? _bar;
    private SettingsWindow? _settingsWindow;
    private bool? _autostartApplied;
    private bool _quitting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        UseEnglishFormatting();
        Directory.CreateDirectory(AppPaths.Root);
        _log = new FileLog(AppPaths.Logs);
        _log.DeleteOlderThan(TimeSpan.FromDays(7), DateTime.Now);
        new CrashHandler(_log, AppPaths.CrashMarker, () => _bar?.ReleaseAppBar()).Attach(this);

        var restarted = e.Args.Contains("--restarted");
        _instance = SingleInstance.Acquire(restarted ? TimeSpan.FromSeconds(5) : TimeSpan.Zero,
            () => Dispatcher.InvokeAsync(ShowSettings));
        if (_instance is null)
        {
            Shutdown();
            return;
        }

        var store = new SettingsStore(AppPaths.Root);
        var loaded = store.Load();
        if (loaded.WasCorrupt) _log.Write("settings.json could not be read; renamed to settings.json.bak and using defaults");
        _settings = new SettingsHub(store, loaded.Settings, _log);
        _settings.Changed += SyncAutostart;
        SyncAutostart(_settings.Current);
        _transparency = new TransparencyWatcher();

        var cpu = new CpuSampler();
        var memory = new MemorySampler();
        var network = new NetworkSampler();
        _stats = new StatsService(Dispatcher, cpu.Sample, memory.Sample, network.Sample);

        _bar = new BarWindow(_settings, _transparency, _stats);
        _bar.SettingsRequested += ShowSettings;
        _bar.QuitRequested += Quit;
        _bar.Show();
        _stats.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _stats?.Dispose();
        _bar?.ReleaseAppBar();
        _settings?.Flush();
        _transparency?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _bar?.ReleaseAppBar();
        _settings?.Flush();
        base.OnSessionEnding(e);
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        _settingsWindow = new SettingsWindow(new SettingsViewModel(_settings!, _transparency!.Enabled, IsInstalledBuild));
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <summary>
    /// Only the installed copy owns the Run entry, so a dev build started with `dotnet run` never registers itself
    /// (F-BAR-04; the first run of the installed copy creates the entry, PRD 9.4).
    /// </summary>
    private void SyncAutostart(AppSettings settings)
    {
        if (!IsInstalledBuild || _autostartApplied == settings.Autostart) return;
        try
        {
            Autostart.Apply(settings.Autostart, Environment.ProcessPath!);
            _autostartApplied = settings.Autostart;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SecurityException)
        {
            _log?.Write("Could not update the autostart entry", ex);
        }
    }

    private void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        Shutdown();
    }

    /// <summary>The UI is English only, so numbers and dates never follow the Windows locale.</summary>
    private static void UseEnglishFormatting()
    {
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = english;
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentCulture = english;
        CultureInfo.CurrentUICulture = english;
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(english.IetfLanguageTag)));
    }
}
```

- [ ] **Step 5: Build dan test**

Run: `dotnet build Gili.slnx` lalu `dotnet test tests/Gili.Core.Tests`
Expected: Build succeeded, semua test PASS.

- [ ] **Step 6: Periksa di laptop**

`dotnet run --project src/Gili.App`, lalu:
1. Klik kanan bar → Settings → jendela gelap "Gili Settings" terbuka.
2. Geser Bar opacity, ganti Glass, geser Bar height dan Island height → bar berubah langsung; jendela yang di-maximize ikut turun/naik saat tinggi bar berubah.
3. Turunkan Bar height ke 28 saat Island height 30 → Island height ikut turun ke 24.
4. Matikan 24-hour clock → jam jadi `7:38 PM`.
5. Tutup Settings, buka `%LOCALAPPDATA%\Gili\settings.json` → nilai baru tersimpan; restart app → nilai tetap.
6. Jalankan `dotnet run` kedua kali → Settings dari instance pertama terbuka.
7. Start with Windows di build dev menampilkan catatan "Takes effect in the installed copy …" dan tidak menulis registry (`reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v Gili` → tidak ada).

- [ ] **Step 7: Commit**

```bash
git add src/Gili.App
git commit -m "feat(app): settings window and autostart for the installed copy (F-BAR-04, F-BAR-08)" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Skrip instal, CLAUDE.md, dan verifikasi Fase 1a

**Files:**
- Create: `scripts/publish.ps1`
- Modify: `CLAUDE.md` (bagian Perintah)

**Interfaces:**
- Consumes: app dari Task 10.

- [ ] **Step 1: Skrip instal**

`scripts/publish.ps1`:

```powershell
# Publishes Gili framework-dependent to %LOCALAPPDATA%\Programs\Gili (PRD 9.4) and starts it.
# A running copy is closed first; taskkill without /F lets it release its AppBar strip.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $env:LOCALAPPDATA 'Programs\Gili'

$running = @(Get-Process -Name Gili -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    taskkill /IM Gili.exe | Out-Null
    foreach ($process in $running) {
        if (-not $process.WaitForExit(5000)) { throw 'Gili did not quit. Quit it from the bar menu and run this again.' }
    }
}

dotnet publish (Join-Path $root 'src/Gili.App/Gili.App.csproj') -c Release -o $target --no-self-contained
Start-Process (Join-Path $target 'Gili.exe')
```

- [ ] **Step 2: Perbarui CLAUDE.md**

Di bagian "Perintah" `CLAUDE.md`, tambahkan setelah blok perintah:

```markdown
- Pasang build Release ke `%LOCALAPPDATA%\Programs\Gili\` dan jalankan: `pwsh scripts/publish.ps1`. Hanya salinan terpasang yang menulis entri autostart; `dotnet run` tidak pernah mendaftarkan dirinya.
- Menutup Gili dari luar: `taskkill /IM Gili.exe` tanpa `/F`, supaya ruang AppBar dilepas. Build Debug punya item "Simulate crash" di menu bar untuk menguji restart otomatis.
```

dan hapus kalimat "Instalasi (PRD 9.4): …" yang lama kalau isinya sudah tercakup (sisakan catatan NativeAOT untuk `Gili.ClaudeHook`).

- [ ] **Step 3: Verifikasi lengkap**

Run:

```bash
dotnet build Gili.slnx -c Release
dotnet test tests/Gili.Core.Tests
dotnet format Gili.slnx --verify-no-changes
```

Expected: build sukses tanpa error, semua test PASS, format tanpa perubahan (kalau ada, jalankan `dotnet format Gili.slnx` lalu commit).

Lalu `pwsh scripts/publish.ps1` dan jalankan checklist Fase 1a di laptop (PRD 13): ruang AppBar dan maximize, fullscreen YouTube dan game, restart Explorer, sleep/resume, DPI 100/125/150%, Transparency effects mati, ubah tinggi bar dan pulau, satu instance, crash dan restart, autostart setelah logout/login (bar muncul ≤ 2 detik), RAM < 120 MB dan CPU diam < 1%.

- [ ] **Step 4: Commit**

```bash
git add scripts CLAUDE.md
git commit -m "chore: install script and Phase 1a commands" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
