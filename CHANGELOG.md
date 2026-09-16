# Changelog

What changed in each release, and why — not a list of files.

## 0.1.0

The first release. Desktop sticky notes for Ubuntu, Windows and macOS: C#/.NET 10,
Avalonia, SQLite, MVVM with a repository behind it.

**Notes live on the desktop**, each one its own borderless window — drag it by its
title strip, resize it from the corner grip, pin it above other windows, recolour
it from a right-click. It saves itself as you type, and comes back where you left
it after a restart, at the same size and in the same colour. Nothing is ever saved
by pressing a button.

**The manager** lists everything, searches the text, and holds the archive.
Deleting a note archives it; the only route to a real delete goes through there,
so nothing you can press in one action destroys a note still on your desktop.

**A stream timer on a note** — a countdown for "back in 5:00" or a count-up for
how long you have been live. Pause it, resume it, restart it; set the label,
length and direction while it is stopped. It survives closing the app with no
catch-up pass, because what is stored is when the stretch began rather than how
much is left, and the number on screen is worked out from that. A finished
countdown sits at 0:00 in red rather than counting into negative numbers.

**Links in a note's text** are offered beside it. Only `http`, `https` and
`mailto` are recognised — a note is text you can paste from anywhere, and opening
a link hands a string to the operating system's shell.

**Settings**: the colour new notes start in, and the manager's theme. Notes
themselves are always light paper.

Everything lives in one SQLite file you could copy to a USB stick. No account, no
sync, no network.

346 tests, and the repository contract is answered twice — once by an in-memory
store and once by SQLite — so the fake the rest of the suite runs against is
known to behave like the real thing rather than assumed to.

### Note for anyone who used SmartNotes

This app was called SmartNotes until this release. The folder it keeps notes in is
named after it, so the first start moves an existing `SmartNotes` folder across.
It never overwrites: if a `YappyNotes` folder already exists, the old one is left
exactly where it is.
