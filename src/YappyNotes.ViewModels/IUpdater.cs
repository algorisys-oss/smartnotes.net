namespace YappyNotes.ViewModels;

/// <summary>
/// Finds, fetches and installs a newer release.
/// </summary>
/// <remarks>
/// The seam that keeps Velopack out of the view-model layer. It holds on to what
/// it found itself, so a view-model only ever sees a version number and never an
/// installer's types.
/// </remarks>
public interface IUpdater
{
    /// <summary>
    /// False when this copy was not installed by a release installer - a build
    /// run from source, an unpacked archive - and so has nothing that could
    /// replace it.
    /// </summary>
    bool CanUpdate { get; }

    /// <summary>The newer version available, or null when this one is the latest.</summary>
    Task<string?> CheckAsync(CancellationToken cancellationToken = default);

    /// <summary>Downloads what the last check found.</summary>
    Task DownloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Arranges for the downloaded release to be installed once this process has
    /// exited, and for the app to be started again afterwards. It does not exit.
    /// </summary>
    void ApplyOnExit();
}
