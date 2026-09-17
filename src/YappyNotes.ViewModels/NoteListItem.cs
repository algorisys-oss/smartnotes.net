using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>
/// One row in the manager: enough to recognise a note by, and nothing else.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="NoteViewModel"/>. A row is read-only and there
/// may be a hundred of them; the editable view-model belongs to the window, and
/// having two of those over one note would mean two <c>Note</c> objects racing
/// each other's autosave.
/// </remarks>
public sealed record NoteListItem(
    Guid Id,
    string DisplayTitle,
    string Preview,
    NoteColor Color,
    DateTimeOffset ModifiedUtc,
    bool IsArchived,
    string TodoProgress = "")
{
    private const int PreviewLength = 90;

    public static NoteListItem From(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);

        return new NoteListItem(
            note.Id,
            TitleFor(note),
            PreviewFor(note.Content),
            note.Color,
            note.ModifiedUtc,
            note.IsArchived,
            ProgressFor(note.Content));
    }

    /// <summary>
    /// Most notes never get a title typed into them - people just start writing -
    /// so a row falls back to the first line that has something on it.
    /// </summary>
    private static string TitleFor(Note note)
    {
        if (!string.IsNullOrWhiteSpace(note.Title))
        {
            return note.Title.Trim();
        }

        var firstRealLine = note.Content
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

        return string.IsNullOrEmpty(firstRealLine) ? "Untitled" : Shorten(firstRealLine);
    }

    /// <summary>"2/5" for a note with to-dos, so a checklist's state shows without opening it.</summary>
    private static string ProgressFor(string content)
    {
        var progress = TodoList.Progress(content);
        return progress.Total == 0 ? string.Empty : $"{progress.Done}/{progress.Total}";
    }

    // One line, however many the note has: a row is a fixed height.
    private static string PreviewFor(string content)
        => Shorten(string.Join(' ', content
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)));

    private static string Shorten(string text)
        => text.Length <= PreviewLength ? text : text[..PreviewLength].TrimEnd() + "…";
}
