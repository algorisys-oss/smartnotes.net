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
}

public enum AppTheme
{
    /// <summary>Whatever the desktop is set to.</summary>
    System,
    Light,
    Dark,
}
