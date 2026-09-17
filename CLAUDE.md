# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with
code in this repository.

## What this is

YappyNotes is a cross-platform desktop sticky notes app — C# / .NET 10, Avalonia
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

**Every milestone in the plan is done, Milestone 6 included — the tray icon and
rich text.**
All eight MMF items hold — notes are their own draggable, resizable, pinnable,
recolourable windows, autosaved and restored; the manager lists, searches and
archives; there is a settings window, keyboard shortcuts, CI and packaging for
six runtime identifiers. On top of that, a note can carry a stream timer that
counts down or up, and its text is Markdown drawn formatted — headings, bullets,
checklists you tick in place, bold, italic, code and clickable links. The app
lives in the tray and outlives its windows, only one copy runs per notes folder,
and an installed copy starts at login and updates itself from GitHub releases
through Velopack. 556 green tests.

Nothing is scheduled beyond that. `docs/plan.md` parks seven ideas with their
reasons, sync among them — rejected rather than deferred — and `TODO.md` holds
what has been noticed since.

**The app was called SmartNotes until it was renamed to YappyNotes.** Nothing in
the code carries the old name. `UserPaths.PreviousAppFolderNames` is the one
deliberate exception: it is how notes kept under the old name are adopted on the
first start afterwards. Append to that list if it is ever renamed again — an
entry removed is somebody's notes left behind.

`origin` is <https://github.com/algorisys-oss/yappynotes>, public.

## Commands

```bash
dotnet build yappynotes.sln
dotnet test yappynotes.sln
dotnet format yappynotes.sln

# One project's tests, one class, one test
dotnet test tests/YappyNotes.Core.Tests
dotnet test yappynotes.sln --filter "FullyQualifiedName~AutoSaveServiceTests"
dotnet test yappynotes.sln --filter "FullyQualifiedName~AutoSaveService_ClosingANoteMidDebounce_StillWrites"

# The fast tests, watched - keep this running while working
dotnet watch test --project tests/YappyNotes.Core.Tests

# Run the app
dotnet run --project src/YappyNotes.App/YappyNotes.App.csproj
scripts/dev-start.sh              # the same in Debug, so F12 developer tools exist
                                  # --watch to restart on a change
                                  # --sandbox for a throwaway database under artifacts/

# Install a release build for this machine into ~/Desktop/tools/yappynotes
scripts/deploy-local.sh           # or pass another tools folder

# The self-updating installer for one runtime, packed on its own OS
scripts/package-installer.sh linux-x64
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

`scripts/version.sh` is the only reader of the version, and `Directory.Build.props`
the only place it is written: `VersionPrefix`, plus `VersionSuffix` for a
prerelease.

`scripts/package-installer.sh <rid>` packs Velopack's self-updating installer on
top of `package.sh --publish-only`, same stdout rule. `vpk` is pinned in
`dotnet-tools.json` and **packs only for the OS it runs on**, which is why
`release.yml` has an `installers` job per platform while the archives are all
cross-built on Linux. The channel is the runtime identifier; every file name
carries it, which is what lets six sets share one GitHub release. Bump `vpk` and
the `Velopack` package together.

## Architecture

Dependencies run one way and nothing points back:

    App ──► ViewModels ──► Core ◄── Data
     │                              ▲
     └──────────────────────────────┘
       (composition root only)

- **`YappyNotes.Core`** — the domain and the policy. `Note`, `NoteColor`,
  `AppSettings`, `INoteRepository`, `NoteService`, `AutoSaveService`,
  `SettingsService`, `UserPaths`. References nothing but the BCL. Most tests live
  here, and new logic belongs here unless it cannot.
- **`YappyNotes.Data`** — `SqliteNoteRepository`, `NoteDatabase` (the connection
  factory — not named `SqliteConnectionFactory`, because Microsoft.Data.Sqlite
  has an internal type by that name and the collision compiles into a baffling
  "inaccessible due to its protection level"), and `Migrator`. **The only project that contains SQL.** A query anywhere else is a
  bug, not a shortcut.
- **`YappyNotes.ViewModels`** — `ManagerViewModel`, `NoteViewModel`, and
  `IWindowManager`. Ticking belongs here too: `TimeProvider.CreateTimer` is BCL,
  so a view-model can drive a once-a-second repaint without reaching for
  `DispatcherTimer` — but its callback lands on a thread-pool thread, so getting
  back to the UI thread goes through an `IUiDispatcher` seam implemented in the
  app. **Do not add an Avalonia package reference to this project.**
  It is absent on purpose: it is what keeps the view-models testable with `new`
  and no UI thread. If you need a type from Avalonia here, you need an abstraction
  instead.
- **`YappyNotes.App`** — Avalonia views, the `IWindowManager` implementation, and
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

**`NoteTimer` stores when the current stretch began and what was banked before
it, and nothing else.** Start, pause, restart and reset are the only things that
write; the number on screen is computed from those and `now`. Counting writes
cannot prove this — a one-second tick keeps resetting a 750 ms debounce, so a
ticker that *did* ask for a save would still never produce one, and the first
version of that test passed against the bug. `Ticking_ForAnHour_NeverAsksForAWrite`
asserts against the transition callback instead.

**A finished countdown reads 0:00 and turns red; it does not count past zero.**
It showed the overrun as "-2:05" at first and that reads as a fault, reported
from real use. `RemainingAt` still goes negative because `HasFinishedAt` needs
the sign — the clamp is in `DisplayAt` only. The colour is what distinguishes a
finished timer from one that has not started, so do not drop it while keeping the
clamp.

**The timer bar holds the count and its transport, and nothing else.** Everything
you *set* rather than *press* — label, direction, presets, minutes and seconds —
lives in the flyout behind the `…` button. A note is 280px wide by default and
those do not fit beside a count: they were clipped to unreadable stumps when they
tried. A flyout is not bound by the note's width, so it works at any size. Do not
move a setting back onto the bar; `TimerBar_AtAnyNoteWidth_KeepsTheCountAndItsButtonsWhole`
and `TimerBar_HasNothingLeftToClip` are what hold that line.

**The timer bar's icons are drawn, not typed.** Every transport key is the
`Button.transport` style — one size — holding a `Path` in the same 10×10 box.
Font glyphs (⏸ ↻ ⏹ …) each brought their own width and baseline, and a fallback
font's where the main one lacked them, so the keys came out four sizes in real
use. The headless stand-in font measures all glyphs alike, so a size test cannot
catch that; `TimerBar_Icons_AreDrawnNotTyped` is what holds it.

**The timer's settings are locked while it is running** (`CanEdit` disables the
`…` button). Moving the finish line halfway through a countdown is a way to be
confused.

**Setting a countdown's length clears what has already run.** "10 minutes" means
a ten-minute break, not ten minutes minus what you used — keeping the banked time
made a preset button look like it had been ignored. Length is minutes *and*
seconds; zero of both falls back to the shortest length rather than a countdown
that is over before it starts.

**The active note differs by its edge only.** `NoteWindow.MarkActive` darkens and
thickens the border on `Activated`. Do not repaint the paper — a desktop of notes
changing colour as focus moves is a flicker — and do not make it touch the note,
because clicking between windows must cost no disk write (`FocusedNoteTests`).

**A link's allow-list is checked twice, on purpose.** `LinkScanner`'s list — http,
https, mailto — is applied when a link is found, bare or labelled, *and* again in
`AvaloniaLinkLauncher`: that second check is the one line where a string out of a
note reaches the OS shell, and it belongs where the danger is. Do not "tidy away"
the duplication. A labelled link goes through the same list, because a label is
exactly how a `file:` address would be disguised.

**A note's text is Markdown, stored as it was typed and drawn formatted.**
`Content` holds plain Markdown — no schema change, search still matches the words.
`NoteMarkdown` in Core parses it; `FormattedNote` draws it; clicking the text swaps
to the `TextBox` editor. Things about it that were learned rather than planned:

- **Every line is its own block.** Markdown proper joins neighbouring lines into a
  paragraph; on a note that reads as the app eating line breaks. And nothing is an
  error — "5 * 3", `user_first_name` and an unclosed backtick stay what was typed.
- **A run's text is an unbroken piece of the source**, and `MarkdownRun.SourceStart`
  says where. That is the whole of how a click on formatted text puts the caret on
  the same character of the Markdown (`NoteLine.SourceIndexAt`). A theory asserts
  it over mixed content; a new inline construct that breaks it breaks clicking.
- **Checkboxes, bullets and links are characters in the text, not controls in it**,
  so a line wraps as one and a press is answered by hit-testing the line's
  `TextLayout`. `NoteViewModel.PressAsync` decides what a character does;
  `FormattedNote` does not know what a checkbox is. `ToggleTask` checks its position
  against a fresh parse, so a stale one flips nothing.
- **Editing state is never stored**, and switching in and out writes nothing. An
  empty note always shows the editor, which is what makes a new note ready to type.
- **Leaving the editor renders it — except into its own Cut/Copy/Paste menu**, or
  the editor is swapped out from under a paste. The test for that right-clicks for
  real: opening the flyout with `ShowAt` does not move focus, and the test passed
  with the guard deleted. Escape ends editing before a second one closes the note;
  Enter on a formatted note starts editing.
- **`FormattedNote` needs its transparent background** — without one, presses on a
  line fell through to the paper behind and opened the editor at the end — and is
  only as tall as its text. The paper around it is the `ScrollViewer`'s, whose own
  press handler edits at the end. Sizing the note to fill the viewport instead made
  it overflow by the padding, so every note scrolled.
- **A hidden `ScrollViewer` does not lay out its content**, which then still reports
  `IsVisible` and even `IsEffectivelyVisible`. Tests ask the scroll viewer. Several
  "stays formatted" assertions were passing against nothing before that.
- **Ctrl+B and Ctrl+I rewrite the Markdown in Core** (`MarkdownEmphasis`), toggling
  asterisks only, line by line from each line's `TextStart` so a checklist's box
  stays outside the markers, and trimmed of edge spaces because `**milk **` is not
  bold. The window then restores the selection — caret first, because setting
  `CaretIndex` in Avalonia 12 clears the selection, and set last it left a second
  press nothing to unbold.
- **The link bar only shows while editing.** Formatted, links are clickable where
  they are written; the bar is for when the editor's plain text is in the way.

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

**The app lives in the tray, so closing a window never ends it.** `ShutdownMode`
is `OnExplicitShutdown`, and the only way out is the tray's Quit. Two things hold
that together, both checked with a spike rather than read off the API:

- **Quit is `TryShutdown()`, never `Shutdown()`.** Only `TryShutdown` raises
  `ShutdownRequested`, which is where `AppServices.DisposeAsync` writes what the
  debounce is holding. `Shutdown` goes straight to `Exit` and loses it.
  `DesktopAppLifetime_Quit_RaisesTheShutdownRequestThatSaves` holds this.
- **A note window does not hold up a close whose reason is an app shutdown.** A
  close still cancelled when `TryShutdown` looks makes it give up, and nothing
  else would end the process. The flush has already happened by then.
  `NoteWindow.HoldsCloseToFlush`.

- **A cancellation reaching the dispatcher after shutdown has begun is ignored**
  (`ShutdownNoise`), and nothing else is. Avalonia 12.1.2's Linux tray cancels its
  D-Bus watch before marking itself disposed, and the escaped cancellation aborted
  the process on quit, intermittently. Take it out once Avalonia fixes that.

**One running copy per notes folder.** A second copy is not a spare window: it
loads its own `Note` for every note on the desktop and autosaves it, so moving a
note in one writes its older text over what was typed in the other — "one note,
one view-model" across two processes. `SingleInstance` holds a lock on
`yappynotes.lock` in the notes folder, and a start that cannot take it connects to
a named pipe, which makes the running copy run `TrayViewModel.BringForward`, and
exits. Six things about it, each checked:

- **The claim is taken before Velopack runs.** Otherwise a second start's
  auto-apply installs an update over the files of the copy that is running.
- **The lock file is never deleted, and it is not in a temp folder.** Deleting it
  while a third start has it open, or a temp cleaner deleting it under an app that
  runs for weeks, is how two copies each end up holding a lock on a different
  file of the same name. The OS drops the lock when a process dies, `kill -9`
  included.
- **The running copy listens only after its notes are restored.** A wake shows
  every note, and `WindowManager.ShowNoteAsync` checks for an open window before
  its await, so a wake mid-restore would open a note twice.
- **The next pipe server opens before the last one closes.** On Unix a pipe is a
  socket that exists only while a server holds it, and a start that connected in
  the gap was accepted and dropped. `SingleInstanceTests` fails if the order is
  swapped.
- **`Listen` opens the first pipe itself, not on the thread pool.** Opened inside
  the task, it appeared seconds late under a busy pool and a start in that window
  gave up — the suite failed four runs in six. No test pins this down on a quiet
  machine; a run of the whole App suite is what shows it.
- **`Dispose` closes the pipe itself** before it lets go of the lock. Cancelling
  the loop and waiting for it needs a pool thread too, and under load the wait ran
  out with the pipe still open — the folder free and something still answering.

A start that finds the folder claimed and nobody answering exits 1 rather than
starting anyway.

The manager is built fresh by `WindowManager.ShowManager` each time it has been
closed, because Avalonia cannot show a closed window again. Do not "fix" that by
cancelling the manager's close and hiding it: a window that cancels its close
cancels the app's shutdown too.

**An installed copy starts at login, on by default.** `AppSettings.StartAtLogin`
is the setting; `ILoginItem` is the seam, with one implementation per OS in
`LoginItems.cs` — an XDG autostart entry, a Windows `Run` key, a macOS
LaunchAgent. Four things about it:

- **Only a Velopack-installed copy is available.** A build, an archive or
  `deploy-local.sh`'s folder never touches the system, and the checkbox is
  disabled there. Registering a build would start whatever was last built, from a
  folder that may be gone.
- **Every start re-applies the setting** (`LoginStartup.ApplyStoredAsync`). That
  is what registers a first install, since the default is on, and what rewrites
  the entry after the app has moved. A failure is swallowed there, never stopping
  the notes coming back; in the settings window it puts the checkbox back.
- **On Linux the path is the AppImage file**, from `LinuxVelopackLocator`, not
  `Environment.ProcessPath` — that is inside a mount which is gone when the app
  exits. `Exec` is quoted and escaped by the desktop-entry rules (spaces, `%`, `$`
  and the doubled backslash), and `TryExec` makes an entry inert once the AppImage
  is deleted, since deleting it is how an AppImage is uninstalled.
- **Only Windows gets an uninstall hook**, because `OnBeforeUninstallFastCallback`
  runs nowhere else; it removes the `Run` value.

Checked on Linux with a packed AppImage: it registers on first start, the entry
passes `desktop-file-validate`, turning the setting off removes it on the next
start, and a Debug build writes nothing. Windows and macOS are not yet watched.

**An installed copy updates itself, and the restart must be the app's own
shutdown.** `UpdatesViewModel` checks on start (`AppSettings.CheckForUpdates`, on
by default — the plan's "no network" goal was changed for this, deliberately),
downloads what it finds, and offers "Restart to update". Velopack's
`ApplyUpdatesAndRestart` exits the process where it stands, skipping
`ShutdownRequested` and the flush. So `IUpdater.ApplyOnExit` is
`WaitExitThenApplyUpdates` and the quit goes through `IAppLifetime`; a test holds
the order. Three more things, each found by running it rather than reading:

- **`VelopackApp.Run()` comes before `CommandLine`** in `Main`. An installer
  starts the app with its own hook arguments, which `CommandLine` would answer as
  unknown with exit code 2. Only the single-instance claim goes ahead of it, and
  only on a start with no arguments, so an installer's hooks never reach it.
- **Only a normal start applies a pending update** (`CommandLine.MayApplyUpdates`).
  Applying restarts the app, and it once turned `--version` into an install and a
  relaunch just to print a number.
- **`UpdateManager` throws unless a locator is set**, which only `VelopackApp.Run`
  does. `VelopackUpdater` falls back to the platform default so a test, or
  anything else, gets "not installed" rather than an exception.

Only a copy installed by a Velopack installer can update; everything else answers
`CanUpdate` false and never touches the network. `YAPPYNOTES_UPDATE_SOURCE`
points it at a local folder instead of GitHub, which is how an update is tried end
to end without publishing a release.

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

**`NoteRepositoryContract` in `tests/YappyNotes.TestKit` is the definition of an
`INoteRepository`**, derived once for the in-memory fake and once for SQLite so
the two cannot drift. A new repository method goes in the contract first, and both
implementations answer it. Two of its tests exist only to keep the fake honest:
the store round-trips by value, so mutating a note you inserted — or one you were
handed back — must change nothing. `TestKit` is a library, not a test project;
`dotnet test` does not look at it.

**Two guards enforce the architecture, and they have teeth** — both were verified
by breaking the rule on purpose and watching them fail:

- `YappyNotes.Core.Tests/ArchitectureTests` — Core references nothing but the BCL.
- `YappyNotes.ViewModels.Tests/ArchitectureTests` — ViewModels reference no
  Avalonia assembly, and nothing beyond CommunityToolkit.Mvvm.

They read `Assembly.GetReferencedAssemblies()`, which lists what the compiled code
*uses* — the compiler drops a `PackageReference` nothing touches. So an unused
package will not fail them, and a used one will, which is the distinction worth
having. If one fails, its message names the offending assembly; do not make it
pass by widening the allow-list without saying why in the commit.

`scripts/dev-start.sh --sandbox` sets `YAPPYNOTES_DATA_DIR`. **`UserPaths` has to
honour it** when Milestone 1 writes it, or `--sandbox` silently opens the real
`notes.db` and the flag becomes a lie at the worst moment.

Regenerating the solution needs `dotnet new sln --format sln`: the .NET 10 SDK
defaults to the newer `.slnx`, and the docs and scripts all say `yappynotes.sln`.

### The architecture page

`docs/architecture.html` is the architecture with animated figures, served from
GitHub Pages at <https://algorisys-oss.github.io/yappynotes/architecture.html>
(`main`, `/docs`). It animates through tinyfly, which needs JavaScript — so it
cannot be embedded in the README, which does not run any.

`media/keystroke.svg` is the README's version: one hand-written SVG animated with
CSS, which *does* run inside an `<img>` on GitHub. If a figure is worth putting in
the README, it has to be authored that way rather than exported from the page.
Both carry `prefers-reduced-motion` and a static end state.

The page repeats claims about the code, so it goes stale like any other doc. When
something it describes changes, change it too — the layers, the save path, the
timer's stored fields, and the checkable "only C# file that names Data" line are
the parts most likely to rot.

## "Ship it"

When the reader says **ship it** — or **deploy it**, or **publish it** — that one
phrase means all of this, in order:

1. **Be on `main` and green.** Merge whatever branch the work is on the usual way
   (`Merge <branch-name>`), then `dotnet build`, `dotnet test` and
   `dotnet format --verify-no-changes` on the solution. Never release from a red
   suite or a dirty tree, and never from a branch.
2. **Bump `VersionPrefix` in `Directory.Build.props`.** **Minor** unless they say
   otherwise, and set `VersionSuffix` (`beta.1`) for a prerelease or empty it for
   a release. That is the only place a version is written: `scripts/version.sh`
   reads it, the packaging script names the archives from it, and the status bar
   in the manager window shows it — so there is nothing else to keep in step.
3. **Write that version's notes at the top of `CHANGELOG.md`**, in the voice the
   commit messages use: what changed and why it matters to somebody using it, not
   a list of files or a list of merges.
4. **Update the docs to match what now exists.** The status block in `README.md`
   and in this file, the test count, and any milestone that has moved. A release
   is the moment those stop being approximately true.
5. **Commit, and push `main` to `origin`.**
6. **Push the `v<version>` tag, which is what builds the release.**
   `release.yml` packages all six runtime identifiers, **runs the packaged binary
   on Linux, Windows and macOS runners**, and publishes only once each has
   started and answered `--version`. Never build the six by hand and never upload
   them by hand: a release nobody watched start on its own OS is the thing that
   workflow exists to prevent.
7. **Check the run finished and the release has its six archives.** A tag that
   built nothing is worse than no tag, because it looks like a release.

A tag with a suffix — `v0.2.0-beta.1` — is published as a prerelease and does not
take the "latest" slot. That is decided from the tag, not by hand. The tag has to
match `scripts/version.sh` exactly, suffix included; `release.yml` fails before
building anything if it does not.

A tag that fails its smoke jobs publishes nothing, which is the workflow working.
Fix it, delete the tag locally and on `origin`, and push it again — a version
number that never produced a release is free to reuse, and burning one to avoid
deleting a tag leaves a gap someone will later try to explain.

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
`dotnet test yappynotes.sln`, not the project you were working in. A feature that
is half-done at the end of a session stays uncommitted rather than being pushed
behind a flag.

`origin` is <https://github.com/algorisys-oss/yappynotes> — public, so
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
