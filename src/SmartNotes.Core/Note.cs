namespace SmartNotes.Core;

/// <summary>
/// One sticky note: its text, its colour, and the window it lives in.
/// </summary>
/// <remarks>
/// The window geometry is on the note rather than in a store of its own because a
/// note *is* its window - two records would drift the first time one was written
/// without the other. See docs/plan.md.
/// </remarks>
public sealed class Note
{
    /// <summary>A note wide and tall enough to type a few lines into.</summary>
    public const int DefaultWidth = 280;
    public const int DefaultHeight = 300;

    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }

    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NoteColor Color { get; set; } = NoteColor.Yellow;

    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; } = DefaultWidth;
    public int Height { get; set; } = DefaultHeight;

    public bool IsAlwaysOnTop { get; set; }
    public bool IsArchived { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>
    /// A new, empty note stamped with the current time.
    /// </summary>
    /// <remarks>
    /// The id is a UUIDv7 built from that same instant, so it carries the
    /// creation time in its high bits: ordering by id is ordering by age, and
    /// inserts land at the right-hand edge of the index instead of scattering
    /// through it. Never swap this for Guid.NewGuid - that is v4, it is random,
    /// and both properties are lost silently.
    /// </remarks>
    public static Note Create(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();

        return new Note
        {
            Id = Guid.CreateVersion7(now),
            CreatedUtc = now,
            ModifiedUtc = now,
        };
    }
}
