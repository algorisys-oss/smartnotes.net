using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>Counts requests to quit, without ending the test run.</summary>
public sealed class FakeAppLifetime : IAppLifetime
{
    public int QuitRequests { get; private set; }

    /// <summary>Runs at the moment of quitting, to see what had happened by then.</summary>
    public Action? OnQuit { get; set; }

    public void Quit()
    {
        QuitRequests++;
        OnQuit?.Invoke();
    }
}
