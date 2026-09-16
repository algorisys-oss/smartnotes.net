# Sticky Notes — Architecture (first draft)

This is a text transcription of `sticky-notes-architecture.pdf`, which is a
hand-drawn mind map exported from a whiteboard and carries no text layer — you
cannot grep it, diff it, or review it in a pull request. The drawing stays as the
record of the original session; this file is the version we edit. When the two
disagree, this one wins.

Everything below is what the drawing says, with nothing added. Decisions taken
*since* the drawing live in [plan.md](plan.md), not here.

## Context

    Live streaming my open source work
      └── Building a new product (Sticky Note X, Ubuntu / Windows / Mac) — vibe architecting
            ├── Build an editor
            └── Build a sticky notes app        ← this repository

## The stack

    STICKY NOTES APP  ──────────►  SQLite DB
    C# / .NET 10 / Avalonia UI

## Application / Bootstrap

On start, in order:

- Initialise DB
- Run migrations
- Create services
- Load existing notes
- Restore note windows

## UI Layer

    ┌──────────────────┬──────────────┐
    │ Manager Window   │ NoteWindow   │
    └──────────────────┴──────────────┘

The Manager Window owns three entry points:

- Search notes
- Create notes
- Archived notes

## ViewModel Layer

    ManagerViewModel              NoteViewModel
    ────────────────              ─────────────
    - Notes                       - ID
    - SearchText                  - Title
    - NewNoteCommand              - Content
    - DeleteCommand               - Color
    - SearchCommand               - IsAlwaysOnTop
                                  - SaveCommand
                                  - DeleteCommand

## Application Services

    NoteService        WindowManager

    AutoSaveService    Settings:
                         Theme
                         Default colours

## Repository Layer

    INoteRepository ──────────► SqliteNoteRepository
          │
          ▼
    - GetAllAsync()
    - GetByIdAsync()
    - SearchAsync()
    - InsertAsync()
    - UpdateAsync()
    - DeleteAsync()

## MMF

The drawing marks **MMF (Minimum Marketable Features)** as a topic without
listing them. [plan.md](plan.md) fills that in — it is the one open question the
drawing poses rather than answers.
