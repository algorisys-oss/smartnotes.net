# LOOP.md — how SmartNotes gets built

Test-driven, one behaviour at a time. [docs/plan.md](docs/plan.md) says what to
build; this says how, and it is the file to reread when you are unsure what to do
next.

The name is the point: this is a loop, and you stay in it.

## The loop

    ┌─► 1. RED      Write one failing test that names a behaviour.
    │                Run it. Watch it fail, and read the failure.
    │
    │   2. GREEN    Write the least code that makes it pass.
    │                Run the whole suite, not just the new test.
    │
    │   3. REFACTOR Clean what you just wrote, and what it touched.
    │                Run the whole suite again. Still green, or undo.
    │
    └── 4. COMMIT   One commit per green, refactored behaviour.

Nothing here is new. What is easy to skip is step 1's second sentence, so:

**Watch the test fail first.** A test that has never failed has not been shown to
test anything. Roughly one time in ten it passes immediately, and every one of
those is a test asserting something already true, a typo in the name of a fake,
or an assertion that cannot fail. You only find out by looking.

**Read the failure message, not just the red.** If it says
`Assert.Equal() Failure: Expected: 1, Actual: 0`, the test will be hard to debug
in six months. Fix the message now, while you know what it meant.

## What "one behaviour" means

One test, one sentence, one reason to fail.

    NoteService_SavingANote_StampsModifiedUtc
    Migrator_OnAFreshDatabase_CreatesTheNotesTable
    AutoSaveService_ClosingANoteMidDebounce_StillWrites
    ManagerViewModel_SearchingForText_KeepsOnlyMatchingNotes

`Subject_Situation_ExpectedOutcome`. Three parts, always. If the middle part
needs an "and", it is two tests.

Arrange, act, assert, with a blank line between them and nothing clever in the
arrange. A test that needs a helper to be understood is a test that will be
deleted by whoever inherits it.

## Where the tests live, and how fast they run

    tests/SmartNotes.TestKit/            the fake and the repository contract
    tests/SmartNotes.Core.Tests/         domain, services, UserPaths
    tests/SmartNotes.Data.Tests/         migrations, SqliteNoteRepository
    tests/SmartNotes.ViewModels.Tests/   ManagerViewModel, NoteViewModel
    tests/SmartNotes.App.Tests/          views, headless Avalonia

They get slower down that list, and there should be fewer of them down that list
too. Core and ViewModels tests touch no disk and no UI; they are the ones you run
on every save, and if they stop being fast something has leaked into a layer that
should not have it.

`InMemoryNoteRepository` lives in `SmartNotes.TestKit` — a library, not a test
project, because Core.Tests, Data.Tests and ViewModels.Tests all need it. It is
the fake every layer above Data is tested against. It is real code with real behaviour — it
enforces the same "id must be unique" rule the SQL does — not a mock framework
recording calls. When Data and the fake disagree, one of them is wrong and the
suite should say which — which is what `NoteRepositoryContract` in the same
project is for. It holds every rule an `INoteRepository` must follow and is
derived once per implementation, so a new method goes in the contract first and
both implementations have to answer it.

Data tests get a real SQLite file in a temp directory, created and deleted per
test class. Not `:memory:` — the app ships against a file, and the differences
that matter (locking, `PRAGMA journal_mode`, a migration surviving a reconnect)
only appear with one.

App tests use `Avalonia.Headless.XUnit` and `[AvaloniaFact]`. They are for things
that genuinely need a visual tree, and there should be few: if a test can be
written against a view-model instead, write it there.

The suite is **xunit v3** — all four projects, deliberately. Under a v2 runner
`[AvaloniaFact]` is not discovered, the project reports no tests, and the run
still passes. Copy an existing `.csproj` when adding a test project;
`dotnet new xunit` still scaffolds v2.

## Running it

    dotnet test smartnotes.sln                      # all of it, before every commit
    dotnet test tests/SmartNotes.Core.Tests         # the fast ones, constantly
    dotnet test smartnotes.sln --filter "FullyQualifiedName~AutoSaveService"
    dotnet watch test --project tests/SmartNotes.Core.Tests

Keep `dotnet watch test` on the fast project running in a second terminal while
you work. The loop above is only pleasant when step 2 takes a second.

## Rules that are not negotiable

**No production code without a failing test that needs it.** Including the
"obvious" line. Especially the obvious line.

**A bug is a missing test.** When something is wrong, the first move is a test
that reproduces it and fails. Fix it after. A bug fixed without that test is a
bug that comes back, and you will not recognise it when it does.

**Never edit a released migration.** Add a new one. An edited migration leaves
every existing database in a state no code path knows how to reach.

**Refactor only on green.** If the suite is red, the only legal edit is the one
making it green. Reverting is a legitimate move and usually the cheap one.

**Commit on green.** Never commit a red suite, not even behind a branch. The
commit before yours has to be a place someone can stand.

## When the loop does not fit

It fits less well in two places, and pretending otherwise wastes a day.

**UI layout.** You cannot write a failing test for "the note's title bar looks
right". Build it by eye, then write tests for the behaviour underneath — the
command fired, the property bound, the window opened at the saved position.

**Spikes.** When you do not know whether an approach works at all — whether
Avalonia can make a borderless always-on-top window behave on X11, say — write a
throwaway to find out. Then delete it and build the real thing test-first with
what you learned. A spike is not a draft; it is an experiment, and keeping it is
how untested code gets into the app.

Both of these are exceptions with edges. Neither is a reason to stop writing
tests for services, repositories or view-models.

## The first hour

In order, and do not skip ahead:

1. `dotnet new sln`, the four source projects, the four test projects, the
   references from [docs/plan.md](docs/plan.md)'s dependency diagram.
2. `global.json` pinning the SDK. `Directory.Build.props` with `Nullable` on.
3. One test per test project asserting something trivially true, and
   `dotnet test smartnotes.sln` green. Now red means you.
4. `Note_WhenCreated_HasAnId` — red, then green.
5. `Note_CreatedAfterAnother_HasAGreaterId` — red, then green. Ids are UUIDv7 and
   the ordering is load-bearing (`ORDER BY Id` is creation order), so it gets a
   test before anything depends on it rather than after something breaks. Spread
   the two timestamps by more than a millisecond, or the test is a coin flip.
6. Keep going.
