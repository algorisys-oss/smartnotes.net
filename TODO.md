# TODO

Things worth doing that are not scheduled into a milestone yet.
[docs/plan.md](docs/plan.md) holds the plan; this holds what has been noticed
since, so it stops living in someone's head.

## Polish

- **The focused note should look a little different from the others.** With half
  a dozen notes open on a desktop, nothing says which one the keystrokes are
  going into. Probably a slightly stronger border or a marginally darker title
  strip while the window is active — small enough that it does not turn a sticky
  note into a form, clear enough to see without looking for it. `Window` raises
  `Activated` and `Deactivated`, so the state is available; what it should look
  like is the open question, and worth trying two or three ways before picking.

## Known limitations, deliberately accepted for now

- **The manager's list is rebuilt when the window is brought forward**, so a note
  edited in its own window keeps its old preview until you come back to the
  manager. The alternative is a change-notification web between windows, which is
  more to get wrong than it is worth at this size.
- **Search is `LIKE` over title and content.** FTS5 is the upgrade if a real
  library ever gets slow; a virtual table is not worth it for a few hundred notes.
