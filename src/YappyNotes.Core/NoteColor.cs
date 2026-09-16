namespace YappyNotes.Core;

/// <summary>
/// The palette a note can be painted in.
/// </summary>
/// <remarks>
/// Stored by name rather than by ordinal, so inserting a colour in the middle of
/// this list does not silently repaint every note already written. Whatever maps
/// these to actual brushes lives in the app - Core has no opinion about what
/// "Yellow" looks like.
/// </remarks>
public enum NoteColor
{
    Yellow,
    Green,
    Blue,
    Pink,
    Purple,
    Grey,
}
