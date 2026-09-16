using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.Time.Testing;
using YappyNotes.App.Views;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.App.Tests;

public class LinkTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryNoteRepository _repository = new();
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public LinkTests()
    {
        _notes = new NoteService(_repository, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
    }

    private NoteWindow OpenWith(string content)
    {
        var note = _notes.CreateAsync().GetAwaiter().GetResult();
        note.Content = content;

        var window = new NoteWindow(new NoteViewModel(
            note, _notes, _autoSave, new FakeWindowManager(), _clock, new InlineUiDispatcher()));
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaFact]
    public void NoteWindow_WithNoLinksInIt_ShowsNoLinkBar()
    {
        var window = OpenWith("just milk and bread");

        Assert.False(window.FindControl<ItemsControl>("LinkBar")!.IsVisible);
    }

    [AvaloniaFact]
    public void NoteWindow_WithLinksInIt_OffersThemUnderTheNote()
    {
        var window = OpenWith("stream https://twitch.tv/rajesh and https://example.com/plan");

        var bar = window.FindControl<ItemsControl>("LinkBar")!;
        Assert.True(bar.IsVisible);
        Assert.Equal(2, ((IReadOnlyList<LinkSpan>)bar.ItemsSource!).Count);
    }

    [AvaloniaFact]
    public void NoteWindow_WithADangerousSchemeInIt_OffersNothing()
    {
        var window = OpenWith("open file:///etc/passwd or javascript:alert(1)");

        Assert.False(window.FindControl<ItemsControl>("LinkBar")!.IsVisible);
    }

    /// <summary>
    /// The last line of defence. Even handed a URI directly, the launcher refuses
    /// anything off the allow-list rather than passing it to the shell.
    /// </summary>
    [AvaloniaTheory]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-msdt:/id")]
    [InlineData("smb://192.168.0.1/share")]
    public async Task AvaloniaLinkLauncher_ForASchemeOffTheAllowList_RefusesEvenWithAWindow(string dangerous)
    {
        var window = OpenWith("anything");
        var launcher = new AvaloniaLinkLauncher(() => window);

        Assert.False(await launcher.OpenAsync(new Uri(dangerous)));
    }

    [AvaloniaFact]
    public async Task AvaloniaLinkLauncher_WithNoWindowYet_RefusesRatherThanThrowing()
    {
        var launcher = new AvaloniaLinkLauncher(() => null);

        Assert.False(await launcher.OpenAsync(new Uri("https://example.com")));
    }
}
