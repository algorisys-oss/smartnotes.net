namespace YappyNotes.ViewModels;

/// <summary>How soon a to-do is due, which decides how its date is drawn.</summary>
public enum DueState
{
    /// <summary>No due date.</summary>
    None,

    /// <summary>Past its date and still not ticked.</summary>
    Overdue,

    Today,

    /// <summary>Tomorrow or after - or ticked, which is never late.</summary>
    Later,
}

public static class DueStates
{
    /// <summary>
    /// How soon a date is, for an item that is or is not done. "Today" is the
    /// reader's local day: a due date is a date on their calendar, not an instant.
    /// </summary>
    public static DueState Of(DateOnly? due, DateOnly today, bool done) => due switch
    {
        null => DueState.None,
        _ when done => DueState.Later,
        { } date when date < today => DueState.Overdue,
        { } date when date == today => DueState.Today,
        _ => DueState.Later,
    };

    /// <summary>The reader's today, from whatever clock the caller was given.</summary>
    public static DateOnly TodayBy(TimeProvider clock) => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}
