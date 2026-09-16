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
- **[TODO.md](TODO.md)** — noticed since, not scheduled. Add to it rather than
  letting an idea live in a commit message; do not work from it without asking.

[docs/sticky-notes-architecture.pdf](docs/sticky-notes-architecture.pdf) is the
original whiteboard drawing and has no text layer, so it cannot be read by
grepping. `docs/sticky-notes-architecture.md` is the transcription; read that one.
Where it and `plan.md` disagree, `plan.md` is newer and wins.

The SDK is pinned to `10.0.302` in `global.json` and every project targets
`net10.0`.

**The project is at Milestone 4, done — the MMF is complete.** All eight MMF
items hold: notes are their own draggable, resizable, pinnable, recolourable
windows, autosaved and restored; the manager lists, searches and archives; there
is a settings window, keyboard shortcuts, CI, and packaging for six runtime
identifiers. 186 green tests.

Next is **Milestone 5**: the stream timer and hyperlinks, designed in "Review:
dynamic notes" in `docs/plan.md`. Read that before starting — the timer's shape
is already decided and the reasons matter.

`origin` is <https://github.com/algorisys-oss/smartnotes.net>, public.

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

### CI

`.github/workflows/ci.yml` builds, tests and format-checks on every push to
`main` and every pull request, then packages all six runtime identifiers. Ubuntu
only, because nothing in the suite needs a window. There are no skipped tests and
nothing that needs a database server — if CI is green and your machine is not,
the difference is yours.

### Packaging

`scripts/package.sh <rid>` builds a self-contained release for one of six runtime
identifiers and **prints the artifact path on stdout and nothing else** — build
logs go to stderr, because callers capture the path with `$(...)`. Keep it that
way, and add new packaging (a `.deb`, an `.app`) by calling it with
`--publish-only` rather than writing a second `dotnet publish`.

Releases are **not** single-file: Avalonia's native libraries want to be real
files on disk.

`scripts/version.sh` is the only reader of the version, and `VersionPrefix` in
`Directory.Build.props` the only place it is written.

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
- **`SmartNotes.Data`** — `SqliteNoteRepository`, `NoteDatabase` (the connection
  factory — not named `SqliteConnectionFactory`, because Microsoft.Data.Sqlite
  has an internal type by that name and the collision compiles into a baffling
  "inaccessible due to its protection level"), and `Migrator`. **The only project that contains SQL.** A query anywhere else is a
  bug, not a shortcut.
- **`SmartNotes.ViewModels`** — `ManagerViewModel`, `NoteViewModel`, and
  `IWindowManager`. Ticking belongs here too: `TimeProvider.CreateTimer` is BCL,
  so a view-model can drive a once-a-second repaint without reaching for
  `DispatcherTimer` — but its callback lands on a thread-pool thread, so getting
  back to the UI thread goes through an `IUiDispatcher` seam implemented in the
  app. **Do not add an Avalonia package reference to this project.**
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

**Applying a note's geometry to its window must be guarded.** Setting `Position`,
`Width` or `Height` raises the window's own change events, which write straight
back to the note and schedule a save — so restoring a note nobody touched would
rewrite every one of them on every start. `NoteWindow._applyingGeometry` is that
guard and it has a test; removing it makes the test fail, which was checked
rather than assumed.

**Closing a note window cancels the close, flushes, then closes again.** Closing
is synchronous and flushing is not. Letting the close through first loses
whatever the debounce was still holding, which is the last sentence somebody
typed.

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

**One note, one `NoteViewModel`, one window.** `WindowManager` builds them and
hands the same one back, because both the manager and the desktop open notes — if
each built its own there would be two `Note` objects for one note, both held by
the autosave, and whichever wrote last would quietly undo the other. The
view-model is forgotten when the window closes, or an archive/restore would serve
a stale copy. This is why `IWindowManager.ShowNoteAsync` takes an id.

**A manager row is a `NoteListItem`, not a `NoteViewModel`** — read-only, cheap,
and there may be a hundred. The manager lists newest-first while the repository
returns oldest-first; both are right for what they are, so do not "fix" either to
match the other.

**`NoteService` owns what the repository refuses to decide.** A repository stores
what it is given without an opinion and never filters archived notes out;
`NoteService` is where "which notes should a reader see" and "delete means
archive" live. Nothing above it should hold an `INoteRepository` of its own.

**`PurgeAsync` refuses a note that is not archived.** That is the second half of
the archive rule and it is deliberate: the only path to a real delete goes through
the archive, so no single action destroys a note a reader can still see on their
desktop. Do not add a convenience overload around it.

**Saving is debounced, and flushed on close.** `AutoSaveService` holds a pending
write per note. A note window closing, and app shutdown, must flush rather than
cancel. Both paths have tests; keep them.

**Autosave is triggered by a change, never by a schedule.** A debounce that starts
when a property actually changes — not a sweep that periodically writes whatever
looks dirty. This is not style: Milestone 5 puts a running timer on a note, and a
sweeping saver would write to disk every second forever. See "Review: dynamic
notes" in `docs/plan.md`, which was agreed before `AutoSaveService` was written
precisely so this decision would not have to be undone.

**Anything that ticks is derived, never stored.** A countdown persists the instant
it started and the time banked before that, and computes what to display from
`now`. It must never persist "seconds remaining", because then every tick is a
change, the note is dirty forever, and it drifts across a restart instead of
simply being recomputed. The same rule holds for anything added later that moves
on its own.

**`Note.Copy()` must deep-copy anything that is not a scalar.** It is what makes a
repository round-trip by value, and it is trivially correct today only because
every field on `Note` is a value. The first reference-typed member — `Note.Timer`
is the one coming — has to be copied, or two notes share it and the two contract
tests that guard this keep passing while it is broken. Add a contract case in the
same commit as the field.

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

**`NoteRepositoryContract` in `tests/SmartNotes.TestKit` is the definition of an
`INoteRepository`**, derived once for the in-memory fake and once for SQLite so
the two cannot drift. A new repository method goes in the contract first, and both
implementations answer it. Two of its tests exist only to keep the fake honest:
the store round-trips by value, so mutating a note you inserted — or one you were
handed back — must change nothing. `TestKit` is a library, not a test project;
`dotnet test` does not look at it.

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

**Every finished feature is committed and pushed.** Not at the end of a session
and not in a batch: a feature that is implemented, green and formatted gets its
commit and reaches `origin` before the next one starts. "Green" means the full
`dotnet test smartnotes.sln`, not the project you were working in. A feature that
is half-done at the end of a session stays uncommitted rather than being pushed
behind a flag.

`origin` is <https://github.com/algorisys-oss/smartnotes.net> — public, so
anything committed is published. Nothing secret goes in the repository; there is
no `.env` here and a new environment variable belongs in an `.env.example` with a
placeholder value.

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

**Keep the note's title strip clear of controls.** A borderless window has no
title bar, so the strip is what `BeginMoveDrag` is wired to — and any control put
in it fills its cell and marks presses handled, leaving nowhere to pick the note
up by. The title `TextBox` is `IsHitTestVisible="False"` until you double-click
to rename. `NoteWindowDragTests` samples twenty points across the strip and fails
if fewer than half are free.

**Resizing is ours too.** No decorations means no OS resize handles, so
`ResizeGrip` in the bottom corner calls `BeginResizeDrag(WindowEdge.SouthEast)`.
Without it a note is stuck at the size it was created.

**A headless test that measures layout must force a layout pass first.**
`Dispatcher.UIThread.RunJobs()` after `Show()`. Every `Bounds` is empty until
then, so a test sampling points inside a control reports success while measuring
nothing — which is exactly how the drag tests first passed against the bug they
exist to catch. Assert the bounds are non-empty before relying on them.

**Do not drive a view-model from a control's change event when a binding writes
the same property.** The event and the binding have no guaranteed order, so the
handler runs against the previous value — the manager's search was a keystroke
behind until the trigger moved onto `ManagerViewModel`'s own property changes.
Trigger from the view-model, not from `TextChanged`/`IsCheckedChanged`.

**A command a control fires is not finished when the control returns.** Tests
wait on `IAsyncRelayCommand.ExecutionTask` rather than sleeping.

**Fluent styles a `TextBox` as a filled, bordered form field**, and
`Background="Transparent"` on the control does not undo it: the template's own
`Border#PART_BorderElement` carries the fill and swaps it again on `:pointerover`
and `:focus`. A note rendered as a white form field inside a coloured frame until
each state was reached into individually — see `NoteWindow.axaml`'s styles. The
same shape of problem applies to `Button` and `ContentPresenter#PART_ContentPresenter`.

**Anything drawn on note-coloured paper must be scoped to the Light variant.**
A note row in the manager wraps its content in
`<ThemeVariantScope RequestedThemeVariant="Light">`, and a note window pins the
same on itself. Without it, controls inside take the application's variant and
render pale text on a pastel background — which shipped, and made the Open and
Archive buttons all but invisible in dark mode. `ManagerListContrastTests` fails
if the scope goes.

**The theme variant follows the reader's setting in `App.axaml`.** A note is always light
paper; on a dark desktop Fluent otherwise resolves dark-theme foregrounds onto it.

**`Window` has no styled property for its position.** `PositionChanged` is the
only way to hear about a drag landing somewhere new — `PositionProperty` does not
exist.

**`SystemDecorations` and `TextBox.Watermark` are obsolete in Avalonia 12** —
`WindowDecorations` and `PlaceholderText` replace them. `TextPresenter.Foreground`
is not an `AvaloniaProperty` and cannot be set from a style selector.

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
