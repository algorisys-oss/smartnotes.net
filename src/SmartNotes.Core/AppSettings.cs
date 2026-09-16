namespace SmartNotes.Core;

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
}

public enum AppTheme
{
    /// <summary>Whatever the desktop is set to.</summary>
    System,
    Light,
    Dark,
}
