namespace YappyNotes.Core;

/// <summary>How the app should behave, as opposed to what is in it.</summary>
public sealed class AppSettings
{
    /// <summary>The colour a new note is created in.</summary>
    public NoteColor DefaultNoteColor { get; set; } = NoteColor.Yellow;

    /// <summary>
    /// The manager window's theme. Notes themselves are always light paper - a
    /// dark sticky note is a different product.
    /// </summary>
    public AppTheme Theme { get; set; } = AppTheme.System;

    /// <summary>
    /// Whether to ask GitHub for a newer release on start. The only thing
    /// YappyNotes ever sends over the network, and on unless it is turned off.
    /// </summary>
    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// Whether YappyNotes starts when the reader logs in. On unless turned off: a
    /// sticky note is only useful if it is there, and after a reboot nothing else
    /// brings the notes back. Only an installed copy acts on it - see
    /// <c>ILoginItem</c>.
    /// </summary>
    public bool StartAtLogin { get; set; } = true;
}

public enum AppTheme
{
    /// <summary>Whatever the desktop is set to.</summary>
    System,
    Light,
    Dark,
}
