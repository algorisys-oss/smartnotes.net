namespace YappyNotes.ViewModels;

/// <summary>
/// Whether the operating system starts YappyNotes when the reader logs in.
/// </summary>
/// <remarks>
/// <para>
/// A seam for the same reason as <see cref="IUpdater"/>: the three platforms do
/// this three different ways - an autostart entry, a Run key, a LaunchAgent - and
/// none of it belongs in a view-model or a test.
/// </para>
/// <para>
/// Only an installed copy is <see cref="IsAvailable"/>. A build run from source
/// registering itself would start whatever was last built, at every login, from a
/// folder that might not be there next week.
/// </para>
/// </remarks>
public interface ILoginItem
{
    /// <summary>False when this copy was not installed, so has nowhere stable to start from.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Adds this copy to what starts at login, or takes it out. Either way it is
    /// safe to repeat, and adding rewrites the entry with where the app is now.
    /// </summary>
    void Set(bool startAtLogin);
}
