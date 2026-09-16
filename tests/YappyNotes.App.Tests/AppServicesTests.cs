using Microsoft.Data.Sqlite;
using YappyNotes.App;
using YappyNotes.Core;

namespace YappyNotes.App.Tests;

/// <summary>
/// The bootstrap, with a real database in a temp folder and no window anywhere.
/// </summary>
public sealed class AppServicesTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"yappynotes-app-{Guid.CreateVersion7()}");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private UserPaths Paths => new(_directory);

    [Fact]
    public async Task StartAsync_OnAMachineWithNoDataFolder_MakesOneAndMigratesIntoIt()
    {
        // First run. SQLite reports a missing folder as "unable to open database
        // file", which reads like corruption, so the folder is ours to create.
        Assert.False(Directory.Exists(_directory));

        await using var services = await AppServices.StartAsync(Paths, cancellationToken: Token);

        Assert.True(File.Exists(Paths.DatabaseFile));
        Assert.Empty(await services.Notes.GetActiveAsync(Token));
    }

    [Fact]
    public async Task StartAsync_OnASecondRun_FindsTheNotesTheFirstRunLeft()
    {
        // MMF 3, at the layer where it is actually decided.
        Guid id;
        await using (var first = await AppServices.StartAsync(Paths, cancellationToken: Token))
        {
            var note = await first.Notes.CreateAsync(Token);
            note.Content = "written last time";
            note.X = 300;
            note.Y = 150;
            await first.Notes.SaveAsync(note, Token);
            id = note.Id;
        }

        await using var second = await AppServices.StartAsync(Paths, cancellationToken: Token);

        var restored = Assert.Single(await second.Notes.GetActiveAsync(Token));
        Assert.Equal(id, restored.Id);
        Assert.Equal("written last time", restored.Content);
        Assert.Equal((300, 150), (restored.X, restored.Y));
    }

    [Fact]
    public async Task StartAsync_RunTwiceOverTheSameFolder_DoesNotMigrateOverTheTop()
    {
        await using (var first = await AppServices.StartAsync(Paths, cancellationToken: Token))
        {
            await first.Notes.CreateAsync(Token);
        }

        await using var second = await AppServices.StartAsync(Paths, cancellationToken: Token);

        Assert.Single(await second.Notes.GetActiveAsync(Token));
    }

    [Fact]
    public async Task DisposeAsync_WithTypingStillInTheDebounce_WritesItBeforeTheProcessGoes()
    {
        // What shutdown does. Anything less loses the last sentence typed.
        Guid id;
        await using (var running = await AppServices.StartAsync(Paths, cancellationToken: Token))
        {
            var note = await running.Notes.CreateAsync(Token);
            id = note.Id;

            note.Content = "typed a moment before quitting";
            running.AutoSave.Schedule(note);
            // No clock advance: the debounce has not come up, so only the flush
            // on the way out can save this.
        }

        await using var next = await AppServices.StartAsync(Paths, cancellationToken: Token);
        var restored = await next.Notes.GetActiveAsync(Token);

        Assert.Equal("typed a moment before quitting", Assert.Single(restored).Content);
        Assert.Equal(id, restored[0].Id);
    }

    [Fact]
    public async Task StartAsync_WiresSettingsOntoTheSameDatabaseAsTheNotes()
    {
        await using (var first = await AppServices.StartAsync(Paths, cancellationToken: Token))
        {
            await first.Settings.SaveAsync(
                new AppSettings { DefaultNoteColor = NoteColor.Blue, Theme = AppTheme.Dark }, Token);
        }

        await using var second = await AppServices.StartAsync(Paths, cancellationToken: Token);
        var settings = await second.Settings.LoadAsync(Token);

        Assert.Equal(NoteColor.Blue, settings.DefaultNoteColor);
        Assert.Equal(AppTheme.Dark, settings.Theme);
    }

    [Fact]
    public async Task CreateAsync_AfterTheDefaultColourIsChanged_MakesNotesInIt()
    {
        await using var services = await AppServices.StartAsync(Paths, cancellationToken: Token);
        await services.Settings.SaveAsync(new AppSettings { DefaultNoteColor = NoteColor.Pink }, Token);

        var note = await services.Notes.CreateAsync(Token);

        Assert.Equal(NoteColor.Pink, note.Color);
    }

    /// <summary>
    /// The first start after the rename must find the notes, not an empty folder
    /// beside them.
    /// </summary>
    [Fact]
    public async Task StartAsync_WithNotesLeftUnderTheAppsOldName_PicksThemUp()
    {
        var root = Path.Combine(_directory, "data");
        var legacy = Path.Combine(root, "SmartNotes");
        Directory.CreateDirectory(legacy);

        Guid id;
        await using (var underTheOldName = await AppServices.StartAsync(new UserPaths(legacy), cancellationToken: Token))
        {
            var note = await underTheOldName.Notes.CreateAsync(Token);
            note.Content = "written before the rename";
            await underTheOldName.Notes.SaveAsync(note, Token);
            id = note.Id;
        }

        SqliteConnection.ClearAllPools();

        await using var renamed = await AppServices.StartAsync(
            new UserPaths(Path.Combine(root, "YappyNotes")), cancellationToken: Token);

        var restored = Assert.Single(await renamed.Notes.GetActiveAsync(Token));
        Assert.Equal(id, restored.Id);
        Assert.Equal("written before the rename", restored.Content);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
