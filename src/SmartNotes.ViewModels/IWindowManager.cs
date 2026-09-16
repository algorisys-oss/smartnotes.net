namespace SmartNotes.ViewModels;

/// <summary>
/// The one thing a view-model needs the windowing layer for.
/// </summary>
/// <remarks>
/// This is the seam that lets the view-model layer stay free of Avalonia. A
/// view-model asks for a note to be shown or closed; the app decides what a
/// window is. A test asserts against the request rather than against a window
/// that would need a UI thread to exist.
/// </remarks>
public interface IWindowManager
{
    /// <summary>Opens a window for this note, or brings its window forward.</summary>
    void ShowNote(NoteViewModel note);

    /// <summary>Closes the window showing this note, if one is open.</summary>
    void CloseNote(Guid noteId);
}
