using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

/// <summary>
/// What a start does about the login item: makes the system match the setting.
/// </summary>
/// <remarks>
/// On every start, not just when the checkbox changes. The first start of an
/// installed copy is what registers it, since the setting is on by default; and
/// an app that has moved - an AppImage dragged to another folder, a .app to
/// /Applications - rewrites the entry with where it is now.
/// </remarks>
public class LoginStartupTests
{
    private readonly InMemorySettingsRepository _repository = new();
    private readonly SettingsService _settings;
    private readonly FakeLoginItem _loginItem = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public LoginStartupTests() => _settings = new SettingsService(_repository);

    [Fact]
    public async Task ApplyStoredAsync_OnTheFirstStartOfAnInstalledCopy_AddsItToLogin()
    {
        await LoginStartup.ApplyStoredAsync(_settings, _loginItem, Token);

        Assert.Equal([true], _loginItem.Sets);
    }

    /// <summary>
    /// Off means off in the system too, including an entry left from before it
    /// was turned off somewhere the checkbox could not reach.
    /// </summary>
    [Fact]
    public async Task ApplyStoredAsync_WithStartAtLoginTurnedOff_TakesItOutOfLogin()
    {
        await _settings.SaveAsync(new AppSettings { StartAtLogin = false }, Token);

        await LoginStartup.ApplyStoredAsync(_settings, _loginItem, Token);

        Assert.Equal([false], _loginItem.Sets);
    }

    [Fact]
    public async Task ApplyStoredAsync_OnACopyThatIsNotInstalled_TouchesNothing()
    {
        _loginItem.IsAvailable = false;

        await LoginStartup.ApplyStoredAsync(_settings, _loginItem, Token);

        Assert.Empty(_loginItem.Sets);
    }

    /// <summary>
    /// This runs on the way to restoring the notes. Failing to register for login
    /// is a reason to start without it, never a reason not to start.
    /// </summary>
    [Fact]
    public async Task ApplyStoredAsync_WhenTheSystemRefuses_StillReturns()
    {
        _loginItem.Failure = new IOException("read-only");

        var exception = await Record.ExceptionAsync(
            () => LoginStartup.ApplyStoredAsync(_settings, _loginItem, Token));

        Assert.Null(exception);
    }
}
