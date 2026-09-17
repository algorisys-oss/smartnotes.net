using YappyNotes.Core;

namespace YappyNotes.ViewModels;

/// <summary>A note's open to-dos, as the manager's to-do view lists them.</summary>
/// <remarks>
/// Read-only, like <see cref="NoteListItem"/>: the manager never holds a note's
/// text to edit. Ticking one goes through the window manager with the
/// <see cref="TodoItem"/>, which is checked against the note as it is by then.
/// </remarks>
public sealed record TodoGroup(Guid NoteId, string Title, NoteColor Color, IReadOnlyList<TodoEntry> Items);

/// <summary>One open to-do in the manager's list, and which note it belongs to.</summary>
public sealed record TodoEntry(Guid NoteId, TodoItem Item)
{
    public string Text => Item.Text;

    /// <summary>The item's indent, drawn as a margin so nesting still reads.</summary>
    public int Level => Item.Level;
}
