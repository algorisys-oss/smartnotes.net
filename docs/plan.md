# SmartNotes Plan

Desktop sticky notes for Ubuntu, Windows and macOS. C# / .NET 10, Avalonia UI,
SQLite. MVVM with a repository behind it.

[sticky-notes-architecture.md](sticky-notes-architecture.md) is the first draft
this plan reviews. [LOOP.md](../LOOP.md) is how we build it; this file is what we
build and why.

## Product goals

A note you open is a window on your desktop, not a row in a list. It stays where
you put it, it looks the way you left it, and it is still there after a reboot.
Nothing is ever saved by pressing a button. There is no account, no sync and no
network — the whole product is one SQLite file you could copy to a USB stick.

The bar for "done" is that someone uses it instead of the sticky notes app their
OS already ships.

## Review of the first draft

The draft is sound. Four layers, dependencies running one way, an interface in
front of storage — that is the right shape and this plan keeps it. What follows
is what the drawing leaves open, with the call we are making.

### What is right, and stays

**A window per note, plus a manager window.** This is the whole product idea. A
canvas holding note-shaped rectangles inside one window would be easier to build
and would be a different, worse application.

**`INoteRepository` with `SqliteNoteRepository` behind it.** Not because we
expect a second implementation to ship, but because the in-memory fake is what
makes the service and view-model tests run in milliseconds without a file on
disk. That is the layer earning its keep on day one.

**Async all the way down.** `GetAllAsync`/`InsertAsync`/… are right even though
SQLite is local and fast. A note window must never block the UI thread on a
disk write, and retrofitting async through a repository later is miserable.

**`AutoSaveService` as its own thing.** Debouncing a keystroke into a write is a
policy with edge cases — a note closed mid-debounce must still save — and it
deserves to be tested away from both the window and the database.

### What the draft leaves open, and the calls we are making

**ViewModels get their own project.** The drawing has a ViewModel layer; a folder
inside the app project would let an Avalonia type leak into a view-model the
first time someone needs a colour or a screen bound, and the layer would quietly
stop being a layer. `SmartNotes.ViewModels` does not reference Avalonia, so that
mistake is a compile error instead of a code review. It also means every
view-model test runs without starting a UI.

**`WindowManager` is an interface in ViewModels, implemented in the app.**
`ManagerViewModel.NewNoteCommand` has to open a window, which is the one place
the layer would have to reach upward. `IWindowManager` is that seam: the
view-model asks for a note to be shown, and the test asserts against a fake that
recorded the ask.

**CommunityToolkit.Mvvm, not ReactiveUI.** Source-generated `[ObservableProperty]`
and `[RelayCommand]` keep the view-models as plain objects that a test can
construct with `new`. ReactiveUI is the more powerful tool and the wrong trade
here — the app has no stream composition to speak of, and it would put a
scheduler between every test and its assertion.

**`Microsoft.Data.Sqlite` directly, with hand-written migrations.** No EF Core.
The schema is two tables, the queries are the six in the drawing, and startup
time is a feature for an app that should feel like it was already running.
Migrations are numbered SQL steps gated on `PRAGMA user_version` — see below.

**Notes are archived, not deleted.** The drawing has an "Archived notes" entry
point and a `DeleteCommand`, which only fit together if delete means archive.
`DeleteAsync` in the repository is the hard delete that purging an archived note
uses; the command on a note window archives. Losing a note to a mis-click is the
one bug this app cannot afford.

**Window geometry belongs to the note.** The bootstrap step "restore note
windows" needs somewhere to restore *from*, and the drawing's `NoteViewModel` has
no X/Y/W/H. They go on the note row. A note is its window; splitting its position
into a second store would mean two things to keep in step.

**Where the database lives is per-OS, and resolved in one place.**
`~/.local/share/SmartNotes/notes.db` on Linux, `%APPDATA%\SmartNotes\` on Windows,
`~/Library/Application Support/SmartNotes/` on macOS. `UserPaths` owns that; code
that builds a path from `$HOME` is a bug on two of the three platforms.

### What we are deliberately not building yet

Rich text, images, reminders, tags, sync, sharing, a tray icon, note stacking,
Markdown. Each is a reasonable sticky-notes feature and none of them is what
makes the first version work. They are listed in Milestone 6 so they stop being
re-proposed.

Timers and hyperlinks used to be on that list and are not any more: they are
Milestone 5, and the review below is what the architecture has to hold for them.

## Review: dynamic notes

A note that only holds text is a note you read. The two features below make one a
thing you *watch*, and they arrive after the MMF — but the point of reviewing them
now is that one of them constrains `AutoSaveService`, which Milestone 2 is about
to write. Getting that wrong is expensive; everything else here is cheap whenever
we do it.

### A stream timer on a note

A countdown for "back in 5:00" during a break, or a count-up for "live for
2:14:33". Customisable, pausable, resumable, restartable.

**The whole design turns on one decision: the stored timer does not change while
it runs.** Persist six fields and nothing else —

    Direction      CountDown | CountUp
    Duration       how long a countdown was set for
    Label          "Back in", "Live for"
    StartedAtUtc   when the current running stretch began; null means paused
    Accumulated    time banked from earlier running stretches
    (NoteId)

— and make the displayed number a **pure function of those and `now`**:

    running   remaining = Duration - (Accumulated + (now - StartedAtUtc))
    paused    remaining = Duration - Accumulated

Start sets `StartedAtUtc`. Pause banks the stretch into `Accumulated` and clears
it. Resume sets it again. Restart zeroes `Accumulated`. Finished is not stored at
all — it is `remaining <= 0`, asked whenever anyone looks.

Three things fall out of that, and each one is a problem we then do not have:

- **Ticking never causes a write.** Not because a rule says so, but because there
  is nothing to write: no persisted field changes between one second and the next.
  A design where the note stored "seconds remaining" would hand `AutoSaveService`
  a note that is dirty every second forever, and the fix at that point is a
  special case threaded through the save path.
- **It survives a restart for free.** Close the laptop mid-break, reopen, and the
  countdown is wherever the wall clock says it should be. There is no drift to
  correct and no catch-up pass on startup, because nothing was ever counting — the
  answer was always derived.
- **It is testable without sleeping.** `FakeTimeProvider` advances two hours in a
  microsecond. Every timing test is exact, and none of them is flaky on a loaded
  machine. This is the same `TimeProvider` already threaded through `Note.Create`
  and `NoteService`, so it costs nothing new.

**What it needs that does not exist yet.** Something has to make the *display*
tick once a second. `TimeProvider.CreateTimer` is BCL, so a view-model may use it
without breaking the no-Avalonia rule — checked, it is there on .NET 10. But its
callback arrives on a thread-pool thread and an Avalonia binding must be updated
on the UI thread, so `SmartNotes.ViewModels` needs an `IUiDispatcher` seam
implemented in the app. Milestone 2 needs that anyway, the moment an autosave
completes off-thread; the timer is a second reason to introduce it there rather
than a new cost.

**Storage is its own table**, `note_timers`, keyed on `NoteId`, at most one row
per note. Not six nullable columns on `notes`: most notes have no timer and should
not carry the width, and the next dynamic element should get its own table too
rather than widening `notes` again each time. Adding one is a migration step,
which is cheap and is exactly what the migrator is for.

**One trap this introduces.** `Note.Timer` would be the first reference-typed
member on `Note`, and `Note.Copy()` is what makes a repository round-trip by
value. Copy is trivially correct today because every field is a scalar; the moment
a reference lands it has to deep-copy, or two notes share one timer and pausing a
note's countdown in one window pauses it in the database too. The two contract
tests that catch this for the note's own fields will keep passing while it is
broken, so the contract needs a timer case added at the same time as the field.

### Hyperlinks in a note

**The cheap version needs no schema at all.** Content stays plain text; URLs are
found in it and drawn as clickable. That is a rendering and interaction change,
not a storage one, and it is why this is worth doing before Markdown rather than
as part of it.

Two pieces:

- **`LinkScanner` in Core** — text in, ranges and `Uri`s out. A pure function,
  tested without a window, which is what keeps the rendering code dumb.
- **`ILinkLauncher`**, a seam in the view-model layer, implemented in the app over
  `TopLevel.Launcher.LaunchUriAsync(Uri)` — checked, that exists in Avalonia 12,
  in `Avalonia.Base`.

**The part worth getting right is which URIs we agree to open.** `LaunchUriAsync`
hands the string to the OS shell, note content is text a reader can paste from
anywhere, and a `notes.db` can be copied between machines. So the scheme is
**allow-listed before launching**: `http`, `https`, `mailto`, and nothing else.
Anything unrecognised renders as ordinary text rather than as a link that does
something surprising. `file:` is refused by name because it will open anything on
disk, and unknown schemes are refused as a class because custom protocol handlers
are registered by whatever else is installed and are a well-trodden path from
"clicked a link in a document" to "ran a program". The allow-list lives in Core
with its own tests, not at the call site where the next caller will forget it.

**The fork in the road.** Bare URLs rendered clickable need no schema and no
editor work. Labelled links - `[the docs](https://…)` - are Markdown, and Markdown
is Milestone 6. Milestone 5 does the first; the second arrives with Markdown or
not at all.

### What this review changes now

Nothing in the code. Two things to carry into Milestone 2:

1. **`AutoSaveService` saves on change, never on a schedule.** A per-note debounce
   triggered by a property actually changing, not a sweep that writes whatever
   looks dirty. The timer design above means nothing ticking is ever dirty, and
   these two decisions have to agree or the app writes to disk every second.
2. **Introduce `IUiDispatcher` when autosave first needs it**, knowing the timer
   will be its second caller.

## Minimum Marketable Feature set

The drawing names MMF and leaves it blank. This is the fill-in — the smallest
thing we would let someone else install:

1. Create a note; it appears as its own window, focused, ready to type in.
2. Typing saves by itself, within a second of stopping.
3. Close and reopen the app: every note is back, in the same place, at the same
   size, in the same colour.
4. Change a note's colour from a small palette.
5. Pin a note on top of other windows.
6. Archive a note from its window, and restore it from the manager.
7. The manager lists every note and searches their text.
8. It runs on Ubuntu, Windows and macOS from the same source.

Anything not on that list waits.

## Architecture

Dependencies run one way, and nothing points back:

    App ──► ViewModels ──► Core ◄── Data
     │                              ▲
     └──────────────────────────────┘
       (composition root only)

- **`SmartNotes.Core`** — the domain and the rules. `Note`, `NoteColor`,
  `AppSettings`, the `INoteRepository` contract, and the services that hold
  policy: `NoteService`, `AutoSaveService`, `SettingsService`, plus `UserPaths`.
  It references nothing but the BCL. This is where most of the tests live.
- **`SmartNotes.Data`** — `SqliteNoteRepository`, `NoteDatabase` (the connection
  factory — not named `SqliteConnectionFactory`, because Microsoft.Data.Sqlite
  has an internal type by that name and the collision compiles into a baffling
  "inaccessible due to its protection level"), and `Migrator`. The only project that knows SQL exists.
- **`SmartNotes.ViewModels`** — `ManagerViewModel`, `NoteViewModel`, and the
  `IWindowManager` interface they call through. CommunityToolkit.Mvvm only; no
  Avalonia reference, enforced by the csproj.
- **`SmartNotes.App`** — Avalonia. Views, `WindowManager`, and `Program`/`App`,
  which is the one place that sees every layer at once because it wires them.

### The schema

    notes
      Id            TEXT PRIMARY KEY     -- UUIDv7, generated client-side, sorts by creation
      Title         TEXT NOT NULL
      Content       TEXT NOT NULL
      Color         TEXT NOT NULL
      X, Y          INTEGER NOT NULL     -- window position, screen coordinates
      Width, Height INTEGER NOT NULL
      IsAlwaysOnTop INTEGER NOT NULL     -- 0/1
      IsArchived    INTEGER NOT NULL     -- 0/1
      CreatedUtc    TEXT NOT NULL        -- ISO-8601
      ModifiedUtc   TEXT NOT NULL

    settings
      Key           TEXT PRIMARY KEY
      Value         TEXT NOT NULL

Milestone 5 adds one more table. It is written here so the shape is agreed, and
it is **not** in the migrator yet — the step that creates it is written when the
feature is:

    note_timers                          -- at most one row per note
      NoteId        TEXT PRIMARY KEY REFERENCES notes(Id) ON DELETE CASCADE
      Direction     TEXT NOT NULL        -- CountDown | CountUp
      Duration      INTEGER NOT NULL     -- ticks, for a countdown
      Label         TEXT NOT NULL
      StartedAtUtc  TEXT NULL            -- ISO-8601; null means paused
      Accumulated   INTEGER NOT NULL     -- ticks banked from earlier stretches

Nothing in that row changes while the timer runs, which is the property the whole
feature rests on. `ON DELETE CASCADE` means purging a note takes its timer with
it, which needs `PRAGMA foreign_keys = ON` — per connection, not per database.
Microsoft.Data.Sqlite turns it on by default, checked rather than assumed;
`NoteDatabase` sets it anyway so the behaviour does not rest on a provider
default, and `TimerCascadeTests` asserts the end state.

Ids are made in the app rather than being `INTEGER` rowids, so a note object is
complete before it has ever been written — which is what lets the view-model
tests run against the in-memory fake with no identity rules of their own.

**They are UUIDv7, not random v4** — `Guid.CreateVersion7()`, which .NET has had
since 9. A v7 carries a millisecond timestamp in its high-order bits, so ids sort
into the order the notes were made. Two things follow, and both are the reason to
prefer it:

- `ORDER BY Id` *is* creation order. Listing notes oldest-first needs no second
  column and no index beyond the primary key, and a query ordering by
  `CreatedUtc` is doing by hand what the key already does.
- Inserts land at the right-hand edge of the B-tree instead of scattering through
  it. Random v4 keys fragment the index and dirty a new page per insert; v7 keys
  append. It matters little at two hundred notes and costs nothing to get right
  now, which is the only time it is free.

Stored as text, sortability is plain ASCII ordering — `Guid.ToString()` is
lowercase hex, big-endian, zero-padded, so SQLite's default `BINARY` collation
already gives the right answer. Keep the format exactly that: no `N`/`B`/`P`
variants, no upper case, no bare-hex column, because mixing two spellings in one
column breaks the ordering silently and every row already written stays wrong.

`CreatedUtc` stays even though the id now encodes it. Extracting a timestamp out
of a UUID in SQL is unpleasant, and someone reading the file in a SQL browser
should be able to see when a note was made.

Timestamps are ISO-8601 text in UTC. SQLite has no date type, and text sorts
correctly and stays readable when someone opens the file in a SQL browser.

Search is `LIKE` over title and content for the MMF. FTS5 is the upgrade if a
real library gets slow; it is not worth a virtual table for two hundred notes.

### Migrations

`PRAGMA user_version` holds the schema number. `Migrator` runs the steps above
that number in order, each in a transaction, then sets the new version. Steps are
append-only and never edited once released — an edited migration is a corrupt
database on someone else's machine.

### How a keystroke reaches the disk

    NoteWindow (view)
      │ two-way binding
      ▼
    NoteViewModel.Content
      │ property changed
      ▼
    AutoSaveService.Schedule(note)      debounce ~750ms, per note
      │
      ▼
    NoteService.SaveAsync(note)         sets ModifiedUtc, validates
      │
      ▼
    INoteRepository.UpdateAsync(note)
      │
      ▼
    SqliteNoteRepository                one UPDATE

A note closing flushes its pending save rather than cancelling it. So does app
shutdown, for every note at once.

## Milestones

Each milestone ends with the app running and its tests green. See
[LOOP.md](../LOOP.md) for the order inside one.

### Milestone 0 — Skeleton

Solution, four projects, four test projects, `global.json` pinned to the SDK,
`Directory.Build.props`, `scripts/dev-start.sh`. An Avalonia window that opens and
says nothing. One trivial passing test per test project, so a red suite always
means something we did.

### Milestone 1 — A note that persists

`Note`, `INoteRepository`, the in-memory fake, `SqliteNoteRepository`, `Migrator`,
`UserPaths`, `NoteService`. No UI beyond the skeleton. This milestone is almost
entirely tests, and it is the one that decides whether the rest is pleasant.

### Milestone 2 — A note you can see

`NoteViewModel`, `NoteWindow`, `IWindowManager` and its implementation,
`AutoSaveService`. Create a note, type, close the app, start it again, and the
note comes back where it was. That is MMF items 1–3 and the first moment the
thing is real.

**Moving and resizing are part of this milestone**, and are written down because
they were not the first time. A borderless window gets no frame from the OS and
therefore no title bar to drag and no resize handles either, so both are ours to
provide: a title strip that calls `BeginMoveDrag`, and a corner grip that calls
`BeginResizeDrag`. MMF 3 promises a note comes back "at the same size", which
means nothing unless a reader could choose one.

The trap, found the hard way: a control placed in the title strip fills its cell
and marks presses handled, leaving nowhere to grab the window by. Keep the strip
clear - `NoteWindowDragTests` samples across it and fails if it is not.

### Milestone 3 — The manager

`ManagerViewModel`, `ManagerWindow`, search, the note list, archive and restore.
MMF 6–7.

### Milestone 4 — Finish and ship

Colours, always-on-top, settings and theme, keyboard shortcuts, the empty state,
and packaging for the three platforms. MMF 4–5 and 8. The app stops being a
prototype here.

Done: the colour menu and pin button, `Topmost` actually following the note,
`SettingsService` over its own repository contract, the default-colour and theme
settings, Ctrl+N / Ctrl+P / Ctrl+W / Escape, the manager's empty states, and
`scripts/package.sh` for all six runtime identifiers — verified by unpacking the
linux-x64 archive and watching it start.

Both of the pieces that were open are now in: a settings window reached from the
manager, and `.github/workflows/ci.yml`, which builds, tests and format-checks
every push and pull request and then packages all six runtime identifiers.

**The MMF is complete.** All eight items hold, which is the bar this plan set for
letting someone else install it.

### Milestone 5 — Dynamic notes

The stream timer and hyperlinks, designed in "Review: dynamic notes" above.

The timer first, because it is the one with a shape to get right: `NoteTimer` and
its arithmetic in Core against `FakeTimeProvider`, then the `note_timers` table
and its migration step with the contract extended to cover it, then
`Note.Copy()` deep-copying it, then the view-model tick and the display. Links
after, and they are a smaller job: `LinkScanner` and the scheme allow-list in
Core, `ILinkLauncher` in the app, clickable rendering in the note window.

Both are testable almost all the way down, so this milestone should feel like
Milestone 1 rather than Milestone 2.

Done, and it did. Two things worth carrying forward. The timer's "ticking writes
nothing" property cannot be tested by counting writes — a one-second tick keeps
resetting a 750 ms debounce, so a broken ticker still produces no write; assert
against the transition instead. And links ended up *beside* the note rather than
inside it, because the body is an editable `TextBox` and making it render runs of
formatting is a far larger change than links are worth. Labelled links still wait
on Markdown in Milestone 6.

### Milestone 6 — After the MMF

Not scheduled, kept so they are not re-argued: rich text or Markdown, images,
reminders and alarms, tags and colour-as-category, a tray icon, note stacking and
grouping, export to Markdown or PDF, optional sync, global hotkey for a new note.

## Near-term order

1. Milestone 0, end to end, with `dotnet test` green before any feature exists.
2. `Note` and the in-memory repository, test-first.
3. `Migrator` and `SqliteNoteRepository` against a temp-file database, test-first.
4. `NoteService` over both, so the fake and the real one prove interchangeable.
5. Only then open a window.
