using Gili.Core.Timers;

namespace Gili.Core.Tests.Timers;

public class TimerTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(600, "10:00")]
    [InlineData(59.2, "01:00")]
    [InlineData(0, "00:00")]
    [InlineData(-3, "00:00")]
    public void Clock_rounds_up(double seconds, string text) => Assert.Equal(text, TimerText.Clock(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Stopwatch_shows_tenths_on_the_page()
    {
        Assert.Equal("01:05.4", TimerText.Stopwatch(TimeSpan.FromMilliseconds(65_480), tenths: true));
        Assert.Equal("01:05", TimerText.Stopwatch(TimeSpan.FromMilliseconds(65_480), tenths: false));
    }

    [Fact]
    public void Page_texts_match_the_mockup()
    {
        Assert.Equal("Pick a duration", TimerText.TimerSub(CountdownTimer.Default));
        Assert.Equal("Running", TimerText.TimerSub(CountdownTimer.Default.Start(Now)));
        Assert.Equal("Paused", TimerText.TimerSub(CountdownTimer.Default.Start(Now).Pause(Now.AddSeconds(5))));
        Assert.Equal("Stopwatch", TimerText.StopwatchSub(StopwatchState.Default));
        Assert.Equal("Focus · session 1 of 4", TimerText.PomodoroSub(PomodoroState.Default, PomodoroPlan.Default));
        Assert.Equal("Break · session 4 of 4", TimerText.PomodoroSub(new PomodoroState(PomodoroPhase.LongBreak, 4, CountdownTimer.Default), PomodoroPlan.Default));
        Assert.Equal(("Start", "Pause", "Resume"), (TimerText.Button(false, false), TimerText.Button(true, true), TimerText.Button(false, true)));
    }

    [Fact]
    public void Card_texts_match_the_mockup()
    {
        Assert.Equal("Timer 10:00 done", TimerText.TimerDone(600));
        Assert.Equal("5-minute break · session 1 of 4", TimerText.FocusDone(PomodoroState.Default, PomodoroPlan.Default));
        Assert.Equal("15-minute break · session 4 of 4", TimerText.FocusDone(PomodoroState.Default with { Round = 4 }, PomodoroPlan.Default));
        Assert.Equal("Session 2 of 4 ready", TimerText.BreakOver(new PomodoroState(PomodoroPhase.Break, 1, CountdownTimer.Default), PomodoroPlan.Default));
        Assert.Equal("Session 1 of 4 ready", TimerText.BreakOver(new PomodoroState(PomodoroPhase.LongBreak, 4, CountdownTimer.Default), PomodoroPlan.Default));
    }

    [Theory]
    [InlineData("5", 300)]
    [InlineData("90", 5400)]
    [InlineData("1:30", 90)]
    [InlineData("1:00:00", 3600)]
    [InlineData(" 2:05 ", 125)]
    [InlineData("23:59:59", 86399)]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("0:00", null)]
    [InlineData("1:60", null)]
    [InlineData("24:00:00", null)]
    [InlineData("abc", null)]
    [InlineData("-5", null)]
    [InlineData("1:2:3:4", null)]
    public void Duration_input_parses_loosely_and_rejects_nonsense(string text, int? seconds) =>
        Assert.Equal(seconds, TimerText.ParseDuration(text));

    [Theory]
    [InlineData(300, "5:00")]
    [InlineData(90, "1:30")]
    [InlineData(3600, "1:00:00")]
    [InlineData(86399, "23:59:59")]
    [InlineData(1, "0:01")]
    public void Duration_input_round_trips(int seconds, string text)
    {
        Assert.Equal(text, TimerText.DurationInput(seconds));
        Assert.Equal(seconds, TimerText.ParseDuration(text));
    }

    [Fact]
    public void Long_shows_hours_from_one_hour()
    {
        Assert.Equal("59:59", TimerText.Long(TimeSpan.FromSeconds(3599)));
        Assert.Equal("1:00:00", TimerText.Long(TimeSpan.FromSeconds(3600)));
        Assert.Equal("1:02:03", TimerText.Long(TimeSpan.FromSeconds(3723)));
    }

    [Fact]
    public void Row_detail_says_ready_running_or_paused()
    {
        var ready = CountdownTimer.Default;
        Assert.Equal("Ready · 10:00", TimerText.RowDetail(ready));
        Assert.Equal("Running · 10:00", TimerText.RowDetail(ready.Start(Now)));
        Assert.Equal("Paused · 10:00", TimerText.RowDetail(ready.Start(Now).Pause(Now.AddSeconds(5))));
        Assert.Equal("Ready · 1:00:00", TimerText.RowDetail(ready.WithTotal(3600)));
    }

    [Fact]
    public void Labeled_card_texts()
    {
        Assert.Equal("Tea done", TimerText.LabeledDone("Tea"));
        Assert.Equal("Timer 10:00", TimerText.LabeledSub(600));
        Assert.Equal("Timer 1:00:00", TimerText.LabeledSub(3600));
        Assert.Equal(("Lap 3", "25 min", "4 sessions"), (TimerText.LapName(3), TimerText.Minutes(25), TimerText.Sessions(4)));
    }

    [Fact]
    public void Stopwatch_past_99_minutes_keeps_every_digit() =>
        Assert.Equal("100:00.0", TimerText.Stopwatch(TimeSpan.FromMinutes(100), tenths: true));

    [Fact]
    public void Pomodoro_texts_follow_the_plan()
    {
        var plan = new PomodoroPlan(20, 4, 12, 3);
        Assert.Equal("Focus · session 1 of 3", TimerText.PomodoroSub(PomodoroState.Initial(plan), plan));
        Assert.Equal("4-minute break · session 2 of 3", TimerText.FocusDone(PomodoroState.Initial(plan) with { Round = 2 }, plan));
        Assert.Equal("12-minute break · session 3 of 3", TimerText.FocusDone(PomodoroState.Initial(plan) with { Round = 3 }, plan));
        Assert.Equal("Session 3 of 3 ready", TimerText.BreakOver(new PomodoroState(PomodoroPhase.Break, 2, CountdownTimer.Default), plan));
        Assert.Equal("Session 1 of 3 ready", TimerText.BreakOver(new PomodoroState(PomodoroPhase.LongBreak, 3, CountdownTimer.Default), plan));
    }
}
