using YappyNotes.Core;

namespace YappyNotes.TestKit;

/// <summary>
/// An INoteRepository in a dictionary, for tests of everything above the storage
/// layer.
/// </summary>
/// <remarks>
/// Real behaviour rather than a recording mock: it enforces the same rules SQLite
/// does - ids are unique, updating something absent is an error, and everything
/// crossing the boundary is copied. That last one is the reason it stores
/// <see cref="Note.Copy"/> rather than the caller's object. Without it a test
/// could mutate "stored" state without going through the repository at all, and
/// pass while the app failed.
/// </remarks>
public sealed class InMemoryNoteRepository : INoteRepository
{
    private readonly Dictionary<Guid, Note> _notes = [];

    public Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Ordered(_notes.Values));

    public Task<Note?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_notes.TryGetValue(id, out var note) ? note.Copy() : null);

    public Task<IReadOnlyList<Note>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(Ordered(_notes.Values));
        }

        var matches = _notes.Values.Where(note =>
            note.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
            || note.Content.Contains(text, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(Ordered(matches));
    }

    public Task InsertAsync(Note note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (!_notes.TryAdd(note.Id, note.Copy()))
        {
            throw new InvalidOperationException($"A note with id {note.Id} is already stored.");
        }

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (!_notes.ContainsKey(note.Id))
        {
            throw new KeyNotFoundException($"There is no note with id {note.Id}.");
        }

        _notes[note.Id] = note.Copy();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!_notes.Remove(id))
        {
            throw new KeyNotFoundException($"There is no note with id {id}.");
        }

        return Task.CompletedTask;
    }

    // Oldest first. The id is a UUIDv7, so ordering by it is ordering by age -
    // the same thing ORDER BY Id gives the SQLite implementation.
    private static IReadOnlyList<Note> Ordered(IEnumerable<Note> notes)
        => notes.OrderBy(note => note.Id).Select(note => note.Copy()).ToList();
}
