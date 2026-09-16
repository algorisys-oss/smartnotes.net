using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(YappyNotes.App.Tests.TestAppBuilder))]

namespace YappyNotes.App.Tests;

/// <summary>
/// How [AvaloniaFact] brings the application up. Headless: the tests run on CI
/// and on a machine with no display, and nothing in the suite should need a
/// window server to answer.
/// </summary>
public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<global::YappyNotes.App.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
