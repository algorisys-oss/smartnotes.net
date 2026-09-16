using SmartNotes.Core;
using SmartNotes.TestKit;

namespace SmartNotes.Core.Tests;

/// <summary>
/// The fake, against the same contract SQLite answers in Data.Tests.
/// </summary>
public sealed class InMemoryNoteRepositoryTests : NoteRepositoryContract
{
    protected override Task<INoteRepository> NewRepositoryAsync()
        => Task.FromResult<INoteRepository>(new InMemoryNoteRepository());
}
