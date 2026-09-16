namespace SmartNotes.Core;

/// <summary>
/// What the application does with notes, as opposed to how they are stored.
/// </summary>
/// <remarks>
/// Two rules live here rather than in the repository, which stores what it is
/// given without an opinion: what counts as a note worth showing, and the fact
/// that <b>deleting a note archives it</b>. Everything above this - windows,
/// view-models, commands - goes through here rather than reaching for an
/// INoteRepository of its own.
/// </remarks>
public sealed class NoteService
{
    private readonly INoteRepository _repository;
    private readonly TimeProvider _timeProvider;

    public NoteService(INoteRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _timeProvider = timeProvider;
    }

    /// <summary>A new, empty note, already stored.</summary>
    public async Task<Note> CreateAsync(CancellationToken cancellationToken = default)
    {
        var note = Note.Create(_timeProvider);
        await _repository.InsertAsync(note, cancellationToken);
        return note;
    }

    /// <summary>
    /// Writes a note's current state, stamping the moment it happened on both the
    /// stored copy and the caller's - the window keeps the object it is editing,
    /// and the two disagreeing about when it last changed is a bug waiting for
    /// whoever adds sync or conflict handling.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The note was never stored.</exception>
    public async Task SaveAsync(Note note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);

        note.ModifiedUtc = _timeProvider.GetUtcNow();
        await _repository.UpdateAsync(note, cancellationToken);
    }

    /// <summary>The notes on the desktop, oldest first.</summary>
    public async Task<IReadOnlyList<Note>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(note => !note.IsArchived).ToList();
    }

    /// <summary>The notes that were archived, oldest first.</summary>
    public async Task<IReadOnlyList<Note>> GetArchivedAsync(CancellationToken cancellationToken = default)
    {
        var all = await _repository.GetAllAsync(cancellationToken);
        return all.Where(note => note.IsArchived).ToList();
    }

    /// <summary>
    /// Active notes matching the text. The archive has its own view, so searching
    /// the board does not turn up what was filed away.
    /// </summary>
    public async Task<IReadOnlyList<Note>> SearchAsync(string text, CancellationToken cancellationToken = default)
    {
        var matches = await _repository.SearchAsync(text, cancellationToken);
        return matches.Where(note => !note.IsArchived).ToList();
    }

    /// <summary>
    /// Archived notes matching the text, oldest first.
    /// </summary>
    /// <remarks>
    /// The archive needs its own search because <see cref="SearchAsync"/>
    /// deliberately excludes archived notes, so the two cannot simply be
    /// combined by a caller.
    /// </remarks>
    public async Task<IReadOnlyList<Note>> SearchArchivedAsync(string text, CancellationToken cancellationToken = default)
    {
        var matches = await _repository.SearchAsync(text, cancellationToken);
        return matches.Where(note => note.IsArchived).ToList();
    }

    /// <summary>
    /// What the delete button does. The note keeps its text and can be restored.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The note was never stored.</exception>
    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
        => SetArchivedAsync(id, isArchived: true, cancellationToken);

    /// <exception cref="KeyNotFoundException">The note was never stored.</exception>
    public Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
        => SetArchivedAsync(id, isArchived: false, cancellationToken);

    /// <summary>
    /// Destroys a note for good.
    /// </summary>
    /// <remarks>
    /// Only reachable for a note that is already archived. That is the whole
    /// safety property: no single action a reader can take destroys a note they
    /// can still see on their desktop, and losing a note to a mis-click is the one
    /// bug this app cannot afford.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">The note was never stored.</exception>
    /// <exception cref="InvalidOperationException">The note is not archived.</exception>
    public async Task PurgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await RequireAsync(id, cancellationToken);

        if (!note.IsArchived)
        {
            throw new InvalidOperationException(
                $"Note {id} is not archived, so it cannot be purged. Archive it first - "
                + "a note a reader can still see must never be one action from gone.");
        }

        await _repository.DeleteAsync(id, cancellationToken);
    }

    private async Task SetArchivedAsync(Guid id, bool isArchived, CancellationToken cancellationToken)
    {
        var note = await RequireAsync(id, cancellationToken);

        note.IsArchived = isArchived;
        await SaveAsync(note, cancellationToken);
    }

    private async Task<Note> RequireAsync(Guid id, CancellationToken cancellationToken)
        => await _repository.GetByIdAsync(id, cancellationToken)
           ?? throw new KeyNotFoundException($"There is no note with id {id}.");
}
