# SmartNotes

Desktop sticky notes for Ubuntu, Windows and macOS. Each note is its own window
that stays where you put it, saves itself as you type, and is still there after a
reboot. No account, no sync, no network — everything lives in one SQLite file.

Built with C# / .NET 10 and [Avalonia UI](https://avaloniaui.net), MVVM, and a
repository over SQLite.

> **Status: Milestone 5 done.** Notes now do something: a note can carry a stream
> timer — a countdown for "back in 5:00" or a count-up for how long you have been
> live — that pauses, resumes and restarts, and survives closing the app. Links in
> a note's text are offered beside it, http/https/mailto only. 287 green tests.
>
> **Milestone 4 before it — the MMF — is complete.** Notes live on the desktop
> as their own borderless windows: draggable, resizable, pinnable, recolourable,
> saved as you type, and back where you left them after a restart. The manager
> lists and searches them and holds the archive. Settings, keyboard shortcuts, CI
> and packaging for six runtime identifiers are in. 186 green tests. Next is
> Milestone 5: the stream timer and hyperlinks. Start at [LOOP.md](LOOP.md).

## Documentation

| File | What it is |
| --- | --- |
| [docs/plan.md](docs/plan.md) | What we are building, the architecture review, the milestones |
| [LOOP.md](LOOP.md) | How we build it — the TDD loop and its rules |
| [TODO.md](TODO.md) | Noticed since the plan was written; not scheduled yet |
| [docs/sticky-notes-architecture.md](docs/sticky-notes-architecture.md) | The original whiteboard design, transcribed |
| [docs/sticky-notes-architecture.pdf](docs/sticky-notes-architecture.pdf) | The whiteboard drawing itself |
| [CLAUDE.md](CLAUDE.md) | Conventions, for Claude Code and for people |

## Prerequisites

**.NET 10 SDK.** The build is pinned to `10.0.302` in `global.json`, so that
exact SDK — or a later 10.0.3xx patch — must be installed.

    dotnet --list-sdks

If you do not have it, install from
[dot.net/download](https://dotnet.microsoft.com/download/dotnet/10.0), or on
Linux with the install script:

    curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0
    export PATH="$HOME/.dotnet:$PATH"        # add this to your shell profile

Having .NET 9 or 11 installed alongside is fine; `global.json` picks the right one.

**Linux desktop libraries.** Avalonia renders through Skia on X11 and needs a few
system libraries that a headless server image will not have:

    sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1

Wayland sessions work through XWayland with no extra setup.

**Windows and macOS** need nothing beyond the SDK.

**No database to install.** SQLite is embedded — `Microsoft.Data.Sqlite` carries
the native library — so there is no server and no connection string.

## Project layout

    smartnotes.sln
    global.json                       SDK pin
    Directory.Build.props             version and shared build settings
    src/
      SmartNotes.Core/                domain, services, INoteRepository, UserPaths
      SmartNotes.Data/                SqliteNoteRepository, Migrator
      SmartNotes.ViewModels/          ManagerViewModel, NoteViewModel, IWindowManager
      SmartNotes.App/                 Avalonia views, WindowManager, bootstrap
    tests/
      SmartNotes.TestKit/             the fake and the INoteRepository contract
      SmartNotes.Core.Tests/
      SmartNotes.Data.Tests/
      SmartNotes.ViewModels.Tests/
      SmartNotes.App.Tests/           headless Avalonia
    scripts/
      dev-start.sh                    run in Debug, optionally watched and sandboxed
    docs/

Dependencies run one way — `App` → `ViewModels` → `Core`, and `Data` → `Core`,
with `App` referencing `Data` only to wire it up at startup.
`SmartNotes.ViewModels` has no reference to Avalonia, deliberately; see
[docs/plan.md](docs/plan.md).

## Running in dev mode

### 1. Clone and restore

    git clone <repository-url> smartnotes.net
    cd smartnotes.net
    dotnet restore smartnotes.sln

Restore needs network access the first time. After that the packages are cached
in `~/.nuget/packages` and you can work offline.

### 2. Build

    dotnet build smartnotes.sln

A clean build should be warning-free. Warnings are not errors in this repo, but a
new one is something you introduced — read it.

### 3. Run the app

    dotnet run --project src/SmartNotes.App/SmartNotes.App.csproj

Or, preferably, the dev script:

    scripts/dev-start.sh

which is the same run in `Debug` — so the Avalonia developer tools are compiled
in — and adds two flags worth knowing:

    scripts/dev-start.sh --watch      # rebuild and restart on any source change
    scripts/dev-start.sh --sandbox    # use a throwaway database under artifacts/

Use `--sandbox` whenever you are about to change the schema or test the
first-run experience. It means the notes you have been using all week are not the
ones your migration is about to rewrite.

### 4. Inspect the running UI

In a `Debug` build, **F12** opens the Avalonia developer tools: the live visual
tree, every property on the selected control, and the bindings that are failing
silently. Most "why is this control invisible" questions are one F12 away.

### 5. Run the tests while you work

The loop in [LOOP.md](LOOP.md) assumes a watcher on the fast tests in a second
terminal:

    dotnet watch test --project tests/SmartNotes.Core.Tests

and the whole suite before you commit:

    dotnet test smartnotes.sln

### 6. Format before you commit

    dotnet format smartnotes.sln

`dotnet format` is the formatter of record. There is no `.editorconfig` to argue
with.

## Running tests

    dotnet test smartnotes.sln                          # everything
    dotnet test tests/SmartNotes.Core.Tests             # one project
    dotnet test smartnotes.sln --filter "FullyQualifiedName~AutoSaveService"
    dotnet test smartnotes.sln --filter "FullyQualifiedName~NoteService_SavingANote_StampsModifiedUtc"

Everything runs on a headless machine, including the UI tests — they use
`Avalonia.Headless.XUnit` and never open a window. There is nothing to skip and
nothing that needs a display, so a full green run on CI means the same as a full
green run on your desk.

The suite is **xunit v3**, and all four test projects are on it deliberately.
`Avalonia.Headless.XUnit` 12.x is built against `xunit.v3.extensibility.core`, and
under a v2 runner its `[AvaloniaFact]` attribute is not discovered at all: the
project reports no tests and the overall run still passes. That is a failure mode
worth knowing about, because nothing goes red when it happens. If you add a test
project, copy an existing `.csproj` rather than `dotnet new xunit`, which still
scaffolds v2. Test projects are `OutputType=Exe` because xunit v3 requires it.

Data tests create a real SQLite file in a temp directory and delete it afterwards.
If a run is interrupted you may find strays under `$TMPDIR`; they are harmless.

## Continuous integration

`.github/workflows/ci.yml` builds, tests and format-checks every push to `main`
and every pull request, then packages all six runtime identifiers. Ubuntu only:
the whole suite is headless and nothing in it needs a display, so a green run on
CI means the same as a green run on your desk.

## Packaging

    scripts/package.sh linux-x64

builds a self-contained release and prints **the archive path on stdout and
nothing else** — build logs go to stderr, so a caller can capture the path with
`$(...)`. The six runtime identifiers are `linux-x64`, `linux-arm64`, `win-x64`,
`win-arm64`, `osx-x64`, `osx-arm64`; Windows gets a `.zip`, everything else a
`.tar.gz`, under `artifacts/`.

Self-contained, so there is no .NET runtime to install first — the archive is
about 45 MB and unpacks to a folder you run `SmartNotes.App` from. Pass
`--publish-only` to get the unarchived folder instead, which is what a `.deb` or
an `.app` bundle would build on.

**Not single-file.** Avalonia's native libraries want to be real files on disk,
and a sticky-notes app is not worth the debugging that hiding them invites.

The version comes from `VersionPrefix` in `Directory.Build.props`, read by
`scripts/version.sh`. That is the only place it is written down.

## Where SmartNotes keeps your files

Resolved by `UserPaths` in `SmartNotes.Core`, following each platform's
convention rather than dropping a dotfile in `$HOME`:

| | Database and settings |
| --- | --- |
| Linux | `~/.local/share/SmartNotes/notes.db` (or `$XDG_DATA_HOME/SmartNotes/`) |
| Windows | `%APPDATA%\SmartNotes\notes.db` |
| macOS | `~/Library/Application Support/SmartNotes/notes.db` |

That file is the entire application state. Copy it to back up your notes; delete
it to start over.

Inspect it with any SQLite client:

    sqlite3 ~/.local/share/SmartNotes/notes.db
    sqlite> .schema notes
    sqlite> select Id, Title, ModifiedUtc from notes where IsArchived = 0;
    sqlite> pragma user_version;        -- the schema version Migrator has reached

Do not have the app running when you write to it.

## Troubleshooting

**`The specified SDK version '10.0.302' ... was not found`** — install the .NET 10
SDK, or relax `global.json` if you know why you are doing that.

**The app builds but no window appears on Linux** — you are probably on a machine
with no display, or missing the X11 libraries above. Check `echo $DISPLAY`. Over
SSH you need `ssh -X`.

**`Unable to load shared library 'libSkiaSharp'`** — the Avalonia native
dependencies did not restore. `dotnet restore --force` usually fixes it.

**Bindings silently do nothing** — run in Debug and press F12. Avalonia does not
throw on a binding to a property that does not exist; the developer tools are
where it tells you.

**A migration fails on your machine only** — your database is at a schema version
a migration no longer expects, most likely because a migration was edited rather
than added. Run with `--sandbox` to confirm against a fresh database, then fix it
forward with a new migration.

## Contributing

Work happens on a feature branch, test-first, merged back with a
`Merge <branch-name>` commit. Commit messages are prose explaining the decision —
what you built and why that approach — not a list of changed files.

Read [LOOP.md](LOOP.md) before the first commit and [CLAUDE.md](CLAUDE.md) before
the second.

## Licence

MIT — see [LICENSE](LICENSE). Use it, change it, ship it; keep the copyright
notice.

Everything SmartNotes ships is MIT too: Avalonia, CommunityToolkit.Mvvm,
`Microsoft.Data.Sqlite` and the `Microsoft.Extensions.*` packages. xunit is
Apache-2.0, which is MIT-compatible and in any case only ever runs the tests —
it is not distributed with the app.
