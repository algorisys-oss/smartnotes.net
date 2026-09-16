namespace YappyNotes.Core;

/// <summary>
/// Turns typing into writes: nothing in YappyNotes is ever saved by pressing a
/// button.
/// </summary>
/// <remarks>
/// <para>
/// <b>Change-triggered, never scheduled.</b> A write happens because
/// <see cref="Schedule"/> was called, which happens because something on a note
/// actually changed. There is no sweep that periodically looks for dirty notes -
/// Milestone 5 puts a running countdown on a note, and a sweeper would write to
/// disk every second forever. See "Review: dynamic notes" in docs/plan.md.
/// </para>
/// <para>
/// A pending write holds the caller's note rather than a copy, so what reaches
/// disk is the latest text rather than a snapshot of the keystroke that started
/// the debounce.
/// </para>
/// <para>
/// Closing a note and closing the app both <b>flush</b>. Dropping a pending write
/// would lose the last sentence someone typed, which is the one thing autosave
/// exists to prevent.
/// </para>
/// </remarks>
public sealed class AutoSaveService : IAsyncDisposable
{
    /// <summary>Long enough to swallow a burst of typing, short enough to feel instant.</summary>
    public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(750);

    private readonly NoteService _notes;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _debounce;

    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Pending> _pending = [];
    private readonly List<Task> _inFlight = [];

    private bool _disposed;

    public AutoSaveService(NoteService notes, TimeProvider timeProvider, TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _notes = notes;
        _timeProvider = timeProvider;
        _debounce = debounce ?? DefaultDebounce;
    }

    /// <summary>
    /// Something on this note changed. Writes it once the typing stops.
    /// </summary>
    public void Schedule(Note note)
    {
        ArgumentNullException.ThrowIfNull(note);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_pending.TryGetValue(note.Id, out var already))
            {
                // Still typing: the note may be a different object for the same
                // id if the window was reopened, so take the newer one.
                already.Note = note;
                already.Timer.Change(_debounce, Timeout.InfiniteTimeSpan);
                return;
            }

            var entry = new Pending { Note = note };
            entry.Timer = _timeProvider.CreateTimer(
                _ => Fire(note.Id), state: null, _debounce, Timeout.InfiniteTimeSpan);

            _pending[note.Id] = entry;
        }
    }

    /// <summary>Writes this note now, if it has anything pending.</summary>
    public async Task FlushAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        Note? note;
        lock (_gate)
        {
            note = Take(noteId);
        }

        if (note is not null)
        {
            await SaveAsync(note, cancellationToken);
        }

        await WhenIdleAsync();
    }

    /// <summary>Writes everything pending. What shutdown calls.</summary>
    public async Task FlushAllAsync(CancellationToken cancellationToken = default)
    {
        List<Note> notes;
        lock (_gate)
        {
            notes = [.. _pending.Keys.ToList().Select(Take).OfType<Note>()];
        }

        foreach (var note in notes)
        {
            await SaveAsync(note, cancellationToken);
        }

        await WhenIdleAsync();
    }

    /// <summary>
    /// Waits for writes already started to finish, without starting any.
    /// </summary>
    /// <remarks>
    /// The debounce fires on a thread-pool thread, so without this there is no
    /// way to tell "has not saved yet" from "is saving right now" - which is the
    /// difference several of the tests are about.
    /// </remarks>
    public async Task WhenIdleAsync()
    {
        while (true)
        {
            Task[] running;
            lock (_gate)
            {
                _inFlight.RemoveAll(task => task.IsCompleted);
                running = [.. _inFlight];
            }

            if (running.Length == 0)
            {
                return;
            }

            await Task.WhenAll(running);
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        await FlushAllAsync();

        lock (_gate)
        {
            foreach (var entry in _pending.Values)
            {
                entry.Timer.Dispose();
            }

            _pending.Clear();
        }
    }

    // The debounce came up for this note.
    private void Fire(Guid noteId)
    {
        Note? note;
        lock (_gate)
        {
            note = Take(noteId);
            if (note is null)
            {
                return;
            }

            _inFlight.Add(SaveAsync(note, CancellationToken.None));
        }
    }

    // Removes a note's pending entry and returns what was waiting to be written.
    // Callers hold _gate.
    private Note? Take(Guid noteId)
    {
        if (!_pending.Remove(noteId, out var entry))
        {
            return null;
        }

        entry.Timer.Dispose();
        return entry.Note;
    }

    private async Task SaveAsync(Note note, CancellationToken cancellationToken)
    {
        try
        {
            await _notes.SaveAsync(note, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            // The note was purged while its window was still closing. There is
            // nothing to write and nothing wrong - swallowing anything broader
            // than this would hide real write failures.
        }
    }

    private sealed class Pending
    {
        public required Note Note { get; set; }
        public ITimer Timer { get; set; } = null!;
    }
}
