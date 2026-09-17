namespace YappyNotes.ViewModels;

/// <summary>
/// The one thing a view-model needs the windowing layer for.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam that lets the view-model layer stay free of Avalonia. A
/// view-model asks for a note to be shown or closed; the app decides what a
/// window is, and a test asserts against the request rather than against a window
/// that would need a UI thread to exist.
/// </para>
/// <para>
/// Notes are named by id rather than passed as a <see cref="NoteViewModel"/> on
/// purpose. Both the manager and the desktop open notes, and if each built its
/// own view-model there would be two <c>Note</c> objects for one note, both being
/// autosaved, and whichever wrote last would quietly undo the other. The
/// implementation keeps one view-model per note and hands the same one back.
/// </para>
/// </remarks>
public interface IWindowManager
{
    /// <summary>
    /// Opens a window for this note, or brings its window forward. A note that is
    /// no longer stored opens nothing.
    /// </summary>
    Task ShowNoteAsync(Guid noteId);

    /// <summary>Closes the window showing this note, if one is open.</summary>
    void CloseNote(Guid noteId);

    /// <summary>Opens the settings window, or brings it forward.</summary>
    void ShowSettings();

    /// <summary>
    /// Opens the manager window, or brings it forward - including after it has
    /// been closed, since the app now outlives it.
    /// </summary>
    void ShowManager();

    /// <summary>
    /// Changes a note's text from outside its window - the manager ticking a to-do.
    /// </summary>
    /// <remarks>
    /// Through the note's own view-model when it has one, never around it. That
    /// view-model's Note is what the autosave holds, possibly with typing not yet
    /// written, and a change written straight to storage would be overwritten by
    /// that autosave moments later. A note that is not on the desktop is stored.
    /// </remarks>
    Task ChangeNoteAsync(Guid noteId, Func<string, string> changeContent);

    /// <summary>The notes that currently have a window open.</summary>
    IReadOnlyCollection<Guid> OpenNotes { get; }
}
