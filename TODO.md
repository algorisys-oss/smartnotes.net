# TODO

Things worth doing that are not scheduled into a milestone yet.
[docs/plan.md](docs/plan.md) holds the plan; this holds what has been noticed
since, so it stops living in someone's head.

## Polish

_Nothing open right now._

Done: **the focused note looks different** — a darker, slightly thicker edge
while the window is active, and only the edge, because repainting the paper on
focus turns a desktop full of notes into a flicker. See `NoteWindow.MarkActive`
and `FocusedNoteTests`.

## Known limitations, deliberately accepted for now

- **The manager's list is rebuilt when the window is brought forward**, so a note
  edited in its own window keeps its old preview until you come back to the
  manager. The alternative is a change-notification web between windows, which is
  more to get wrong than it is worth at this size.
- **Search is `LIKE` over title and content.** FTS5 is the upgrade if a real
  library ever gets slow; a virtual table is not worth it for a few hundred notes.
