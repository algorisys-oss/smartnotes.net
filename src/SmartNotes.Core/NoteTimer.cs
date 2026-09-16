using System.Globalization;

namespace SmartNotes.Core;

public enum TimerDirection
{
    /// <summary>"Back in 4:59" — a break.</summary>
    CountDown,

    /// <summary>"Live for 2:14:33" — a session.</summary>
    CountUp,
}

/// <summary>
/// A running counter on a note: a break countdown, or how long you have been on
/// air.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here changes while it runs.</b> What is stored is when the current
/// stretch began and how much time was banked before it; everything on screen is
/// computed from those and the moment you ask. That is not a stylistic choice,
/// and three things depend on it:
/// </para>
/// <list type="bullet">
/// <item>Ticking never makes a note dirty, so the autosave has nothing to write
/// once a second and needs no special case to avoid it.</item>
/// <item>Closing the app mid-countdown needs no catch-up on restart, because
/// nothing was ever counting — the answer is simply recomputed.</item>
/// <item>Every test is exact, because a fake clock can jump two hours without
/// anything sleeping.</item>
/// </list>
/// <para>
/// Storing "seconds remaining" instead would cost all three.
/// </para>
/// </remarks>
public sealed class NoteTimer
{
    public TimerDirection Direction { get; set; } = TimerDirection.CountDown;

    /// <summary>How long a countdown was set for. Ignored when counting up.</summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>What the counter is for — "Back in", "Live for".</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>When the current running stretch began. Null means not running.</summary>
    public DateTimeOffset? StartedAtUtc { get; set; }

    /// <summary>Time banked from stretches that have already been paused.</summary>
    public TimeSpan Accumulated { get; set; }

    public bool IsRunning => StartedAtUtc is not null;

    /// <summary>How long this timer has run in total, as of <paramref name="now"/>.</summary>
    public TimeSpan ElapsedAt(DateTimeOffset now)
        => StartedAtUtc is { } startedAt
            ? Accumulated + (now - startedAt)
            : Accumulated;

    /// <summary>
    /// How much of a countdown is left, which goes negative once it has overrun.
    /// </summary>
    /// <remarks>
    /// The raw value, because <see cref="HasFinishedAt"/> needs the sign. What is
    /// shown does not go below zero - see <see cref="DisplayAt"/>.
    /// </remarks>
    public TimeSpan RemainingAt(DateTimeOffset now) => Duration - ElapsedAt(now);

    public bool HasFinishedAt(DateTimeOffset now)
        => Direction == TimerDirection.CountDown && RemainingAt(now) <= TimeSpan.Zero;

    /// <summary>
    /// The counter as it should read on screen.
    /// </summary>
    /// <remarks>
    /// A finished countdown reads 0:00 and stays there rather than counting into
    /// negative numbers. It did show the overrun at first, on the theory that
    /// knowing how late you are back is useful; it reads as a fault instead, so
    /// it stops. <see cref="HasFinishedAt"/> is how anything that cares tells a
    /// finished timer from one that has not started.
    /// </remarks>
    public string DisplayAt(DateTimeOffset now)
    {
        var value = Direction == TimerDirection.CountDown
            ? Max(RemainingAt(now), TimeSpan.Zero)
            : ElapsedAt(now);

        var magnitude = value;

        return magnitude.TotalHours >= 1
            ? string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:00}:{2:00}",
                (int)magnitude.TotalHours, magnitude.Minutes, magnitude.Seconds)
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1:00}",
                (int)magnitude.TotalMinutes, magnitude.Seconds);
    }

    private static TimeSpan Max(TimeSpan left, TimeSpan right) => left > right ? left : right;

    /// <summary>
    /// Begins, or picks up again after a pause. Starting one that is already
    /// running does nothing rather than discarding the stretch in progress.
    /// </summary>
    public void Start(DateTimeOffset now)
    {
        if (IsRunning)
        {
            return;
        }

        StartedAtUtc = now;
    }

    /// <summary>Banks the stretch that has been running, and stops.</summary>
    public void Pause(DateTimeOffset now)
    {
        if (StartedAtUtc is not { } startedAt)
        {
            return;
        }

        Accumulated += now - startedAt;
        StartedAtUtc = null;
    }

    /// <summary>Back to the beginning, still running.</summary>
    public void Restart(DateTimeOffset now)
    {
        Accumulated = TimeSpan.Zero;
        StartedAtUtc = now;
    }

    /// <summary>Back to the beginning, stopped.</summary>
    public void Reset()
    {
        Accumulated = TimeSpan.Zero;
        StartedAtUtc = null;
    }

    /// <summary>
    /// An independent copy. <see cref="Note.Copy"/> needs this: without it two
    /// notes would share one timer, and pausing a countdown in a window would
    /// pause it in the database too.
    /// </summary>
    public NoteTimer Copy() => new()
    {
        Direction = Direction,
        Duration = Duration,
        Label = Label,
        StartedAtUtc = StartedAtUtc,
        Accumulated = Accumulated,
    };
}
