namespace SmartNotes.Core;

/// <summary>
/// Where notes are kept.
/// </summary>
/// <remarks>
/// <para>
/// Everything is async even though SQLite is local and fast: a note window must
/// never block the UI thread on a disk write, and threading async through a
/// repository after the fact is miserable.
/// </para>
/// <para>
/// Nothing here filters archived notes out. Deciding what a reader should see is
/// the service's job, and keeping that rule in one place is why it is not
/// half-applied down here.
/// </para>
/// <para>
/// Implementations round-trip by value: a caller that mutates a note it passed
/// in, or one it was handed back, changes nothing that is stored until it calls
/// <see cref="UpdateAsync"/>. NoteRepositoryContract enforces that for all of
/// them.
/// </para>
/// </remarks>
public interface INoteRepository
{
    /// <summary>Every note, archived ones included, oldest first.</summary>
    Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>The note with this id, or null if there is none.</summary>
    Task<Note?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notes whose title or content contains <paramref name="text"/>, ignoring
    /// case, oldest first. Blank text matches everything, which is what a cleared
    /// search box means. The text is matched literally - a reader typing % means
    /// the character.
    /// </summary>
    Task<IReadOnlyList<Note>> SearchAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>Stores a note that is not stored yet.</summary>
    /// <exception cref="InvalidOperationException">The id is already stored.</exception>
    Task InsertAsync(Note note, CancellationToken cancellationToken = default);

    /// <summary>Replaces a stored note's mutable fields.</summary>
    /// <exception cref="KeyNotFoundException">The id is not stored.</exception>
    Task UpdateAsync(Note note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a note for good. This is not what the delete button does - that
    /// archives. Only purging an already-archived note reaches here.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The id is not stored.</exception>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
