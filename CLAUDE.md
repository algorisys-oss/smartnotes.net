# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with
code in this repository.

## What this is

SmartNotes is a cross-platform desktop sticky notes app — C# / .NET 10, Avalonia
UI, SQLite — where each note is its own always-there window rather than a row in
a list. MVVM, with a repository behind the services.

Three files carry the project and they do not overlap:

- **[docs/plan.md](docs/plan.md)** — what we are building, why the architecture is
  shaped this way, and the milestones.
- **[LOOP.md](LOOP.md)** — how we build it. **This project is test-driven; read
  LOOP.md before writing code, every session.**
- **[README.md](README.md)** — how to run it.

[docs/sticky-notes-architecture.pdf](docs/sticky-notes-architecture.pdf) is the
original whiteboard drawing and has no text layer, so it cannot be read by
grepping. `docs/sticky-notes-architecture.md` is the transcription; read that one.
Where it and `plan.md` disagree, `plan.md` is newer and wins.

The SDK is pinned to `10.0.302` in `global.json` and every project targets
`net10.0`.

**The project is at Milestone 0.** The scaffold exists and the suite is green:
eight projects, a `ManagerWindow` that opens and says nothing, and the
architecture guards below. There is no domain code yet — `Note` and
`INoteRepository` are Milestone 1, and "The first hour" in `LOOP.md` says where to
start. There is also no git repository yet — `git init` before the first commit.

## Commands

```bash
dotnet build smartnotes.sln
dotnet test smartnotes.sln
dotnet format smartnotes.sln

# One project's tests, one class, one test
dotnet test tests/SmartNotes.Core.Tests
dotnet test smartnotes.sln --filter "FullyQualifiedName~AutoSaveServiceTests"
dotnet test smartnotes.sln --filter "FullyQualifiedName~AutoSaveService_ClosingANoteMidDebounce_StillWrites"

# The fast tests, watched - keep this running while working
dotnet watch test --project tests/SmartNotes.Core.Tests

# Run the app
dotnet run --project src/SmartNotes.App/SmartNotes.App.csproj
scripts/dev-start.sh              # the same in Debug, so F12 developer tools exist
                                  # --watch to restart on a change
                                  # --sandbox for a throwaway database under artifacts/
```

Use `--sandbox` before touching the schema. Testing a migration against your own
week-old notes is how notes get lost.

## Architecture

Dependencies run one way and nothing points back:

    App ──► ViewModels ──► Core ◄── Data
     │                              ▲
     └──────────────────────────────┘
       (composition root only)

- **`SmartNotes.Core`** — the domain and the policy. `Note`, `NoteColor`,
  `AppSettings`, `INoteRepository`, `NoteService`, `AutoSaveService`,
  `SettingsService`, `UserPaths`. References nothing but the BCL. Most tests live
  here, and new logic belongs here unless it cannot.
- **`SmartNotes.Data`** — `SqliteNoteRepository`, the connection factory, and
  `Migrator`. **The only project that contains SQL.** A query anywhere else is a
  bug, not a shortcut.
- **`SmartNotes.ViewModels`** — `ManagerViewModel`, `NoteViewModel`, and
  `IWindowManager`. **Do not add an Avalonia package reference to this project.**
  It is absent on purpose: it is what keeps the view-models testable with `new`
  and no UI thread. If you need a type from Avalonia here, you need an abstraction
  instead.
- **`SmartNotes.App`** — Avalonia views, the `IWindowManager` implementation, and
  the bootstrap. The one place that sees every layer, because it wires them.

`UserPaths.Data` resolves the per-OS location of `notes.db`. Never build that path
from `$HOME` — it is wrong on two of the three platforms this ships to.

### Things about this design worth knowing before changing it

**Delete means archive.** `NoteViewModel.DeleteCommand` sets `IsArchived`; the
manager restores from there. `INoteRepository.DeleteAsync` is the real delete and
is only reached by purging an already-archived note. Losing a note to a mis-click
is the one bug this app cannot afford.

**A note's window geometry is on the note row** — `X`, `Y`, `Width`, `Height`,
`IsAlwaysOnTop`. The bootstrap restores windows from it. Do not split it into a
second store; a note *is* its window and two records would drift.

**Ids are generated in the app**, not SQLite rowids, so a `Note` is complete
before it has ever been written. That is what lets every layer above `Data` be
tested against the in-memory fake without identity rules of its own.

**Ids are UUIDv7 and must stay sortable.** Use `Guid.CreateVersion7()` — never
`Guid.NewGuid()`, which is v4 and random. A v7 holds a millisecond timestamp in
its high bits, so `ORDER BY Id` is creation order and inserts append to the index
instead of fragmenting it. Three ways to break that, all silent:

- **`Guid.NewGuid()` slipping into a new code path.** The app still works; the
  ordering just quietly stops being an ordering. There is a test asserting a
  later-created note has a greater id — keep it passing.
- **A different string format.** Store `ToString()` only: lowercase, hyphenated,
  big-endian hex, which SQLite's default `BINARY` collation sorts correctly. `N`,
  `B` and `P` formats, or upper case, mix two spellings into one column and every
  row already written stays wrong.
- **Storing the id as a BLOB via `ToByteArray()`.** That overload is
  little-endian — it byte-reverses the first three fields, which is exactly where
  the timestamp lives — so the bytes do *not* sort. Verified: across ids minutes
  apart it appears to work and across ids weeks apart it does not, so a small test
  will pass and production will be wrong. If a BLOB column is ever genuinely
  wanted, it is `ToByteArray(bigEndian: true)`.

**Timestamps are ISO-8601 UTC text.** SQLite has no date type; text sorts
correctly and stays readable in a SQL browser. Convert at the edge, never store
local time.

**Saving is debounced, and flushed on close.** `AutoSaveService` holds a pending
write per note. A note window closing, and app shutdown, must flush rather than
cancel. Both paths have tests; keep them.

**Migrations are append-only.** `PRAGMA user_version` is the schema number, and
`Migrator` runs the steps above it in order, each in a transaction. **Never edit a
migration that has shipped** — add a new one. An edited migration leaves databases
in a state no code path can reach.

## The test suite

**It is xunit v3, and every test project stays on it.** `Avalonia.Headless.XUnit`
12.x is built against `xunit.v3.extensibility.core`; under a v2 runner its
`[AvaloniaFact]` attribute is not discovered at all, so the project reports no
tests **and the run still passes**. Nothing goes red when that happens, which is
why the version is uniform rather than per-project. Add a test project by copying
an existing `.csproj` — `dotnet new xunit` still scaffolds v2. Test projects are
`OutputType=Exe`, which xunit v3 requires.

**Two guards enforce the architecture, and they have teeth** — both were verified
by breaking the rule on purpose and watching them fail:

- `SmartNotes.Core.Tests/ArchitectureTests` — Core references nothing but the BCL.
- `SmartNotes.ViewModels.Tests/ArchitectureTests` — ViewModels reference no
  Avalonia assembly, and nothing beyond CommunityToolkit.Mvvm.

They read `Assembly.GetReferencedAssemblies()`, which lists what the compiled code
*uses* — the compiler drops a `PackageReference` nothing touches. So an unused
package will not fail them, and a used one will, which is the distinction worth
having. If one fails, its message names the offending assembly; do not make it
pass by widening the allow-list without saying why in the commit.

`scripts/dev-start.sh --sandbox` sets `SMARTNOTES_DATA_DIR`. **`UserPaths` has to
honour it** when Milestone 1 writes it, or `--sandbox` silently opens the real
`notes.db` and the flag becomes a lie at the worst moment.

Regenerating the solution needs `dotnet new sln --format sln`: the .NET 10 SDK
defaults to the newer `.slnx`, and the docs and scripts all say `smartnotes.sln`.

## Conventions

**This project is test-driven.** No production code without a failing test that
needed it, and a bug starts as a test that reproduces it. The full rules,
including the two places the loop genuinely does not fit, are in
[LOOP.md](LOOP.md). Do not silently opt out of it because a change looks small.

**Test names are `Subject_Situation_ExpectedOutcome`.** Three parts, always. An
"and" in the middle part means it is two tests.

**Comments explain why, not what.** Prefer an XML doc comment giving the reason a
type exists and the trap it avoids over a comment narrating the code beneath it.

**Commit messages are prose that explains the decision** — what was built and why
that approach, including what was rejected — not a list of changed files. Read
`git log` before writing one.

**Work happens on a feature branch**, merged back with a `Merge <branch-name>`
commit. Never commit a red suite.

There is no `.editorconfig`; `dotnet format` is the formatter of record.

**MVVM is CommunityToolkit.Mvvm**, not ReactiveUI. Use `[ObservableProperty]` and
`[RelayCommand]` and let the source generator write the boilerplate. Do not add a
second MVVM framework.

**Data access is `Microsoft.Data.Sqlite` directly.** No EF Core, no ORM. The
schema is two tables and startup time is a feature.

## Avalonia notes

This is **Avalonia 12**, whose API differs from most samples and answers online,
which target 11. Probe the assembly rather than trusting a snippet.

`x:Name` on a `ColumnDefinition` or `RowDefinition` generates no field. Name the
`Grid` and index into `ColumnDefinitions`.

Bindings fail silently — a binding to a property that does not exist throws
nothing and shows nothing. Run in Debug and press **F12** for the developer tools
before concluding the data is wrong.

Views bind to view-models and nothing else. Code-behind is for what genuinely
cannot be expressed as a binding — window chrome, drag-to-move, platform window
flags. Logic in a `.axaml.cs` file is logic that no test will ever reach.

Sticky notes are borderless, always-on-top windows, which is the part of this app
most likely to behave differently per platform. Verify window flags on X11,
Windows and macOS separately rather than assuming; when a behaviour has to
diverge, put the branch behind an interface in `App` rather than scattering
`OperatingSystem.IsLinux()` through the views.
