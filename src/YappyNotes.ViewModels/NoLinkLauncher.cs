namespace YappyNotes.ViewModels;

/// <summary>
/// Opens nothing.
/// </summary>
/// <remarks>
/// The default for a view-model built without a launcher, which is every test
/// that has nothing to do with links. Returning false rather than throwing keeps
/// a missing launcher from turning a click into a crash.
/// </remarks>
internal sealed class NoLinkLauncher : ILinkLauncher
{
    public Task<bool> OpenAsync(Uri uri) => Task.FromResult(false);
}
