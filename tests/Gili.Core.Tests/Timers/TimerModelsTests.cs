using Gili.Core.Timers;

namespace Gili.Core.Tests.Timers;

public class TimerModelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Timer_counts_down_from_its_end()
    {
        var timer = CountdownTimer.Default.Start(Now);
        Assert.True(timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(7), timer.Remaining(Now.AddMinutes(3)));
        Assert.False(timer.IsDone(Now.AddMinutes(9)));
        Assert.True(timer.IsDone(Now.AddMinutes(10)));
    }

    [Fact]
    public void Paused_timer_keeps_its_remaining()
    {
        var paused = CountdownTimer.Default.Start(Now).Pause(Now.AddMinutes(4));
        Assert.True(paused.IsPaused);
        Assert.Equal(TimeSpan.FromMinutes(6), paused.Remaining(Now.AddHours(5)));
    }

    [Fact]
    public void Resume_sets_a_new_end()
    {
        var resumed = CountdownTimer.Default.Start(Now).Pause(Now.AddMinutes(4)).Start(Now.AddMinutes(20));
        Assert.Equal(Now.AddMinutes(26), resumed.EndsAt);
    }

    [Fact]
    public void Reset_goes_back_to_the_total()
    {
        var reset = CountdownTimer.Default.WithTotal(300).Start(Now).Reset();
        Assert.False(reset.IsRunning || reset.IsPaused);
        Assert.Equal(TimeSpan.FromSeconds(300), reset.Remaining(Now));
    }

    [Fact]
    public void Remaining_ignores_a_time_zone_change()
    {
        var timer = CountdownTimer.Default.Start(Now);
        var sameMomentElsewhere = Now.AddMinutes(3).ToOffset(TimeSpan.FromHours(9));
        Assert.Equal(TimeSpan.FromMinutes(7), timer.Remaining(sameMomentElsewhere));
    }

    [Fact]
    public void Stopwatch_adds_up_runs()
    {
        var sw = StopwatchState.Default.Start(Now).Pause(Now.AddSeconds(10)).Start(Now.AddSeconds(100));
        Assert.Equal(TimeSpan.FromSeconds(15), sw.Elapsed(Now.AddSeconds(105)));
        Assert.Equal(TimeSpan.Zero, sw.Reset().Elapsed(Now));
    }

    [Fact]
    public void Pomodoro_goes_focus_break_focus()
    {
        var focus = PomodoroState.Default.Start(Now);
        Assert.Equal((PomodoroPhase.Focus, 1, TimeSpan.FromMinutes(25)), (focus.Phase, focus.Round, focus.Clock.Remaining(Now)));
        var rest = focus.NextPhase(Now.AddMinutes(25), startNow: true);
        Assert.Equal((PomodoroPhase.Break, 1, TimeSpan.FromMinutes(5)), (rest.Phase, rest.Round, rest.Clock.Remaining(Now.AddMinutes(25))));
        var next = rest.NextPhase(Now.AddMinutes(30), startNow: false);
        Assert.Equal((PomodoroPhase.Focus, 2, false), (next.Phase, next.Round, next.Clock.IsRunning));
    }

    [Fact]
    public void Fourth_focus_leads_to_a_long_break()
    {
        var fourth = PomodoroState.Default with { Round = 4 };
        var rest = fourth.NextPhase(Now, startNow: true);
        Assert.Equal((PomodoroPhase.LongBreak, TimeSpan.FromMinutes(15)), (rest.Phase, rest.Clock.Remaining(Now)));
    }

    [Fact]
    public void Long_break_starts_over_at_session_1()
    {
        var longBreak = new PomodoroState(PomodoroPhase.LongBreak, 4, CountdownTimer.Default.WithTotal(900));
        Assert.Equal((PomodoroPhase.Focus, 1), (longBreak.NextPhase(Now, true).Phase, longBreak.NextPhase(Now, true).Round));
    }

    [Fact]
    public void Skip_starts_the_next_focus()
    {
        var skipped = PomodoroState.Default.Skip(Now);
        Assert.Equal((PomodoroPhase.Focus, 2, true), (skipped.Phase, skipped.Round, skipped.Clock.IsRunning));
        Assert.Equal(1, (PomodoroState.Default with { Round = 4 }).Skip(Now).Round);
    }
}
