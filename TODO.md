# TODO

Things worth doing that are not scheduled into a milestone yet.
[docs/plan.md](docs/plan.md) holds the plan; this holds what has been noticed
since, so it stops living in someone's head.

## Polish

- **The colours are still missing from the body's right-click menu.** The body
  is a `TextBox`, and Fluent gives it its own Cut/Copy/Paste flyout, so
  right-clicking most of a note never reaches the note's context menu. The
  header's colour button is the way in now; adding the swatches to that text
  flyout too, or a shortcut that cycles colours, would cover the rest.

Done: **recolouring is visible** — a dot in the title strip painted the note's
colour opens a flyout of swatches, the current one marked. It used to be
reachable only from a context menu hardly anyone could find. See
`NoteColourWindowTests`.

Done: **the focused note looks different** — a darker, slightly thicker edge
while the window is active, and only the edge, because repainting the paper on
focus turns a desktop full of notes into a flicker. See `NoteWindow.MarkActive`
and `FocusedNoteTests`.

## Known limitations, deliberately accepted for now

- **Updates download the whole release, about 50 MB.** Velopack makes delta
  packages when the previous release is in the output folder at pack time;
  `release.yml` does not fetch it first (`vpk download github`), so there are
  none. Worth doing once releases are frequent.
- **The macOS installer and app are not signed or notarised**, and the Windows
  `Setup.exe` is not signed. Gatekeeper and SmartScreen will warn on first run.
  Signing needs certificates held as repository secrets.
- **The Windows and macOS updaters have never been watched update.** The Linux
  one was, end to end, against a local feed. The release workflow proves the
  Windows and macOS portable builds start, not that one replaces itself.

- **On a Linux desktop with no tray host, closing the manager hides the app.**
  The tray icon is a StatusNotifierItem, which GNOME shows only through the
  AppIndicator extension (Ubuntu ships it on; stock GNOME does not). Without one
  there is no icon, and once the manager is closed the app keeps running with
  nothing to click. Starting it again brings the manager back, so launching the
  app is the way in.

- **The manager's list is rebuilt when the window is brought forward**, so a note
  edited in its own window keeps its old preview until you come back to the
  manager. The alternative is a change-notification web between windows, which is
  more to get wrong than it is worth at this size.
- **Search is `LIKE` over title and content.** FTS5 is the upgrade if a real
  library ever gets slow; a virtual table is not worth it for a few hundred notes.
