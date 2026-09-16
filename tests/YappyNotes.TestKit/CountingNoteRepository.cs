using YappyNotes.Core;

namespace YappyNotes.TestKit;

/// <summary>
/// Wraps a repository and counts the writes, for the tests that are about how
/// many times something was saved rather than what was saved.
/// </summary>
public sealed class CountingNoteRepository(INoteRepository inner) : INoteRepository
{
    public int Updates { get; private set; }

    public Task<IReadOnlyList<Note>> GetAllAsync(CancellationToken cancellationToken = default)
        => inner.GetAllAsync(cancellationToken);

    public Task<Note?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Note>> SearchAsync(string text, CancellationToken cancellationToken = default)
        => inner.SearchAsync(text, cancellationToken);

    public Task InsertAsync(Note note, CancellationToken cancellationToken = default)
        => inner.InsertAsync(note, cancellationToken);

    public Task UpdateAsync(Note note, CancellationToken cancellationToken = default)
    {
        Updates++;
        return inner.UpdateAsync(note, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => inner.DeleteAsync(id, cancellationToken);
}
