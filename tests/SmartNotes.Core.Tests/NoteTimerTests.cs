using SmartNotes.Core;

namespace SmartNotes.Core.Tests;

/// <summary>
/// The whole timer is a pure function of six stored fields and "now", which is
/// why every one of these tests is exact and none of them sleeps.
/// </summary>
public class NoteTimerTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static NoteTimer FiveMinuteBreak() => new()
    {
        Direction = TimerDirection.CountDown,
        Duration = TimeSpan.FromMinutes(5),
        Label = "Back in",
    };

    [Fact]
    public void NoteTimer_BeforeItIsStarted_IsNotRunningAndHasItsWholeDurationLeft()
    {
        var timer = FiveMinuteBreak();

        Assert.False(timer.IsRunning);
        Assert.Equal(TimeSpan.Zero, timer.ElapsedAt(Noon));
        Assert.Equal(TimeSpan.FromMinutes(5), timer.RemainingAt(Noon));
    }

    [Fact]
    public void Start_ThenTimePassing_CountsDownWithoutAnythingStoredChanging()
    {
        // The property the whole design rests on: two minutes later the row on
        // disk is identical, and the number on screen is not.
        var timer = FiveMinuteBreak();
        timer.Start(Noon);
        var storedStart = timer.StartedAtUtc;
        var storedAccumulated = timer.Accumulated;

        var remaining = timer.RemainingAt(Noon.AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(3), remaining);
        Assert.Equal(storedStart, timer.StartedAtUtc);
        Assert.Equal(storedAccumulated, timer.Accumulated);
    }

    [Fact]
    public void Pause_BanksWhatHasRunAndStops()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        timer.Pause(Noon.AddMinutes(2));

        Assert.False(timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(2), timer.Accumulated);
    }

    [Fact]
    public void Pause_ThenTimePassing_ChangesNothing()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);
        timer.Pause(Noon.AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(3), timer.RemainingAt(Noon.AddHours(1)));
    }

    [Fact]
    public void Start_AfterAPause_PicksUpWhereItLeftOff()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);
        timer.Pause(Noon.AddMinutes(2));

        timer.Start(Noon.AddHours(1));

        // Two minutes were used before the pause; one more after resuming.
        Assert.Equal(TimeSpan.FromMinutes(2), timer.RemainingAt(Noon.AddHours(1).AddMinutes(1)));
    }

    [Fact]
    public void Start_OnATimerAlreadyRunning_DoesNotLoseTheStretchInProgress()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        timer.Start(Noon.AddMinutes(2));

        Assert.Equal(TimeSpan.FromMinutes(3), timer.RemainingAt(Noon.AddMinutes(2)));
    }

    [Fact]
    public void Restart_PutsItBackToTheTopAndKeepsRunning()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        timer.Restart(Noon.AddMinutes(4));

        Assert.True(timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.RemainingAt(Noon.AddMinutes(4)));
    }

    [Fact]
    public void Reset_PutsItBackToTheTopAndStops()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        timer.Reset();

        Assert.False(timer.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(5), timer.RemainingAt(Noon.AddHours(3)));
    }

    /// <summary>
    /// The reason a restart needs no catch-up pass: close the laptop mid-break,
    /// reopen it, and the answer is simply recomputed.
    /// </summary>
    [Fact]
    public void RemainingAt_AfterTheAppWasClosedForAWhile_IsWhereTheWallClockSays()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        // Nothing ran in between; only the stored fields survived.
        var reloaded = new NoteTimer
        {
            Direction = timer.Direction,
            Duration = timer.Duration,
            Label = timer.Label,
            StartedAtUtc = timer.StartedAtUtc,
            Accumulated = timer.Accumulated,
        };

        Assert.Equal(TimeSpan.FromMinutes(1), reloaded.RemainingAt(Noon.AddMinutes(4)));
    }

    [Fact]
    public void HasFinishedAt_OnceTheCountdownRunsOut_IsTrue()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        Assert.False(timer.HasFinishedAt(Noon.AddMinutes(4).AddSeconds(59)));
        Assert.True(timer.HasFinishedAt(Noon.AddMinutes(5)));
    }

    [Fact]
    public void HasFinishedAt_OnACountUp_IsNeverTrue()
    {
        var timer = new NoteTimer { Direction = TimerDirection.CountUp, Label = "Live for" };
        timer.Start(Noon);

        Assert.False(timer.HasFinishedAt(Noon.AddHours(9)));
    }

    [Fact]
    public void ElapsedAt_OnACountUp_IsJustHowLongItHasBeenRunning()
    {
        var timer = new NoteTimer { Direction = TimerDirection.CountUp };
        timer.Start(Noon);

        Assert.Equal(TimeSpan.FromMinutes(134), timer.ElapsedAt(Noon.AddMinutes(134)));
    }

    [Theory]
    [InlineData(0, "5:00")]
    [InlineData(61, "3:59")]
    [InlineData(299, "0:01")]
    [InlineData(300, "0:00")]
    public void DisplayAt_OnACountdown_ShowsMinutesAndSeconds(int secondsIn, string expected)
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        Assert.Equal(expected, timer.DisplayAt(Noon.AddSeconds(secondsIn)));
    }

    /// <summary>
    /// Overrunning a break is worth seeing rather than hiding at 0:00 - knowing
    /// you are two minutes over is the point of having it on screen.
    /// </summary>
    [Fact]
    public void DisplayAt_PastTheEndOfACountdown_ShowsHowFarOver()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        Assert.Equal("-2:05", timer.DisplayAt(Noon.AddMinutes(7).AddSeconds(5)));
    }

    [Fact]
    public void DisplayAt_OverAnHour_ShowsHoursToo()
    {
        var timer = new NoteTimer { Direction = TimerDirection.CountUp };
        timer.Start(Noon);

        Assert.Equal("2:14:33", timer.DisplayAt(Noon.AddHours(2).AddMinutes(14).AddSeconds(33)));
    }

    [Fact]
    public void Copy_ChangesToTheCopy_LeaveTheOriginalAlone()
    {
        var timer = FiveMinuteBreak();
        timer.Start(Noon);

        var copy = timer.Copy();
        copy.Pause(Noon.AddMinutes(1));
        copy.Label = "Something else";

        Assert.True(timer.IsRunning);
        Assert.Equal("Back in", timer.Label);
        Assert.Equal(TimeSpan.Zero, timer.Accumulated);
    }
}
