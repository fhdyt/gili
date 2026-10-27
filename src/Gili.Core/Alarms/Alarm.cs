using Gili.Core.Time;

namespace Gili.Core.Alarms;

/// <summary>
/// An alarm (F-TOOL-05): a local time, the days it repeats (none = once), a label, its sound, on or off, and when it
/// next rings in UTC (null while off).
/// </summary>
public sealed record Alarm(long Id, TimeOnly Time, Days Repeat, string Label, string Sound, bool Enabled, DateTimeOffset? NextFireAt);
