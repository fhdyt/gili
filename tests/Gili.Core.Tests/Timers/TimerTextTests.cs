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
        Assert.Equal("Focus · session 1 of 4", TimerText.PomodoroSub(PomodoroState.Default));
        Assert.Equal("Break · session 4 of 4", TimerText.PomodoroSub(new PomodoroState(PomodoroPhase.LongBreak, 4, CountdownTimer.Default)));
        Assert.Equal(("Start", "Pause", "Resume"), (TimerText.Button(false, false), TimerText.Button(true, true), TimerText.Button(false, true)));
    }

    [Fact]
    public void Card_texts_match_the_mockup()
    {
        Assert.Equal("Timer 10:00 done", TimerText.TimerDone(600));
        Assert.Equal("5-minute break · session 1 of 4", TimerText.FocusDone(PomodoroState.Default));
        Assert.Equal("15-minute break · session 4 of 4", TimerText.FocusDone(PomodoroState.Default with { Round = 4 }));
        Assert.Equal("Session 2 of 4 ready", TimerText.BreakOver(new PomodoroState(PomodoroPhase.Break, 1, CountdownTimer.Default)));
        Assert.Equal("Session 1 of 4 ready", TimerText.BreakOver(new PomodoroState(PomodoroPhase.LongBreak, 4, CountdownTimer.Default)));
    }
}
