using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>Records what a view-model asked to open, without opening anything.</summary>
public sealed class FakeLinkLauncher : ILinkLauncher
{
    public List<Uri> Opened { get; } = [];

    public Task<bool> OpenAsync(Uri uri)
    {
        Opened.Add(uri);
        return Task.FromResult(true);
    }
}
