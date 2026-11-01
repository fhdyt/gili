using System.Text.Json;

namespace Gili.Core.About;

/// <summary>The latest release on GitHub (F-PUB-04): its version, its page, and its installer when it has one.</summary>
public sealed record ReleaseInfo(Version Version, string PageUrl, string? InstallerUrl)
{
    /// <summary>GitHub's "latest release" JSON; anything else (an error, a tag that is not a version) is null.</summary>
    public static ReleaseInfo? Parse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("tag_name", out var tag) || tag.ValueKind != JsonValueKind.String
                || !root.TryGetProperty("html_url", out var page) || page.ValueKind != JsonValueKind.String
                || !Version.TryParse(tag.GetString()!.TrimStart('v', 'V'), out var parsed))
                return null;
            var version = UpdateRules.ThreeParts(parsed);
            var installer = $"Gili-Setup-{AboutInfo.Format(version)}.exe";
            string? url = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var name) && name.GetString() == installer
                        && asset.TryGetProperty("browser_download_url", out var link))
                        url = link.GetString();
                }
            }
            return new ReleaseInfo(version, page.GetString()!, url);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public enum UpdateStatus { Off, NotChecked, UpToDate, Available, Installing, NotVerified, CouldNotCheck }

/// <summary>
/// What Gili sees when it thinks about installing an update by itself (revision 14, issue #27): how long the computer has
/// been left alone, a fullscreen app, the island compact or open, and how soon the next alarm or timer ends.
/// </summary>
public readonly record struct UpdateMoment(TimeSpan Idle, bool Fullscreen, bool IslandCompact, TimeSpan? NextAlert);

/// <summary>Where the update check stands. <paramref name="OffReason"/> says why this copy never checks.</summary>
public sealed record UpdateState(UpdateStatus Status, string? OffReason = null, Version? Latest = null, string? PageUrl = null);

/// <summary>When Gili checks for a newer version and what Settings → About says about it (F-PUB-04, PRD 11, design 5d).</summary>
public static class UpdateRules
{
    public static readonly TimeSpan Every = TimeSpan.FromHours(24);

    /// <summary>Failing quietly turns into "Couldn't check for updates" after this long without an answer (PRD 11).</summary>
    public static readonly TimeSpan QuietFor = TimeSpan.FromDays(3);

    /// <summary>The Store copy asks the Store; the GitHub copy only once it is signed, since the friends' build never checks (PRD 9.4).</summary>
    public static UpdateState Initial(InstallKind kind, bool signed) => kind switch
    {
        InstallKind.Store => new(UpdateStatus.NotChecked),
        InstallKind.Installed when signed => new(UpdateStatus.NotChecked),
        InstallKind.Installed => new(UpdateStatus.Off, "This test build doesn't check. Install a newer Gili-Setup over it"),
        _ => new(UpdateStatus.Off, "Only the installed copy checks for updates"),
    };

    /// <summary>A day after the last answer; a clock set back counts as due.</summary>
    public static bool IsDue(DateTimeOffset? lastCheck, DateTimeOffset now) =>
        lastCheck is not { } last || now - last >= Every || last > now;

    public static UpdateState Checked(Version mine, Version latest, string? pageUrl) =>
        ThreeParts(latest) > ThreeParts(mine)
            ? new(UpdateStatus.Available, Latest: ThreeParts(latest), PageUrl: pageUrl)
            : new(UpdateStatus.UpToDate, PageUrl: pageUrl);

    /// <summary>Offline or no answer: nothing changes until three days without one; an update already found stays.</summary>
    public static UpdateState Failed(UpdateState current, DateTimeOffset? lastCheck, DateTimeOffset now) =>
        current.Status is UpdateStatus.UpToDate or UpdateStatus.CouldNotCheck && lastCheck is { } last && now - last > QuietFor
            ? current with { Status = UpdateStatus.CouldNotCheck }
            : current;

    /// <summary>The Updates row: text on the right, or a note under its label.</summary>
    public static (string? Text, string? Note) Row(UpdateState s) => s.Status switch
    {
        UpdateStatus.Off => (null, s.OffReason),
        UpdateStatus.NotChecked => ("Not checked yet", null),
        UpdateStatus.UpToDate => ("Up to date", null),
        UpdateStatus.Available or UpdateStatus.Installing => (null, "Update available · " + AboutInfo.Format(s.Latest ?? new Version(0, 0, 0))),
        UpdateStatus.NotVerified => (null, "Update couldn't be verified"),
        _ => ("Couldn't check for updates", null),
    };

    /// <summary>The row's button, only while an update waits or installs.</summary>
    public static string? Button(UpdateState s) => s.Status switch
    {
        UpdateStatus.Available => "Install update",
        UpdateStatus.Installing => "Installing…",
        _ => null,
    };

    /// <summary>Install updates automatically waits this long without a key or the mouse (revision 14, issue #27).</summary>
    public static readonly TimeSpan AwayFor = TimeSpan.FromMinutes(10);

    /// <summary>An alarm or a timer ending sooner than this keeps Gili from restarting under it.</summary>
    public static readonly TimeSpan AlertMargin = TimeSpan.FromMinutes(15);

    /// <summary>
    /// A verified update installs by itself only while nobody is using the computer: away 10 minutes, no fullscreen app, the
    /// island compact, and no alarm or timer within 15 minutes, so a restart never cuts into something.
    /// </summary>
    public static bool ShouldInstallNow(bool autoUpdate, UpdateStatus status, UpdateMoment now) =>
        autoUpdate && status == UpdateStatus.Available && now.Idle >= AwayFor && !now.Fullscreen && now.IslandCompact
        && (now.NextAlert is not { } alert || alert > AlertMargin);

    /// <summary>The version this run is, when the last run was an older one; null on the first run ever.</summary>
    public static Version? JustUpdated(string? lastRun, Version mine) =>
        Version.TryParse(lastRun, out var last) && ThreeParts(mine) > ThreeParts(last) ? ThreeParts(mine) : null;

    /// <summary>The first start of a new version says so for 4 s: "Updated to Gili 0.2.0".</summary>
    public static Island.WideEvent UpdatedWide(Version version) => new("updated", Island.WideIcon.Restart, Island.WideTone.Accent,
        [new Island.WideRun("Updated to Gili"), new Island.WideRun(AboutInfo.Format(version), Island.WideRunStyle.Number, SpaceBefore: true)],
        TimeSpan.FromSeconds(4));

    /// <summary>1.2.3, so 1.2.3.0 (the assembly's version) and 1.2.3 (a tag) compare equal.</summary>
    public static Version ThreeParts(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));
}
