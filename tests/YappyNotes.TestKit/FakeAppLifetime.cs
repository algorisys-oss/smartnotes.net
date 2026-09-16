using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>Counts requests to quit, without ending the test run.</summary>
public sealed class FakeAppLifetime : IAppLifetime
{
    public int QuitRequests { get; private set; }

    public void Quit() => QuitRequests++;
}
