namespace YappyNotes.ViewModels;

/// <summary>
/// Opens a link in whatever the reader uses for it.
/// </summary>
/// <remarks>
/// A seam for the same reason <see cref="IWindowManager"/> is one, and for a
/// second reason besides: this is where a string out of a note reaches the
/// operating system's shell, so it is worth having somewhere a test can stand.
/// </remarks>
public interface ILinkLauncher
{
    Task<bool> OpenAsync(Uri uri);
}
