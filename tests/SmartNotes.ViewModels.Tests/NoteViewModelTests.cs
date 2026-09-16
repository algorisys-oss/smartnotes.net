using System.ComponentModel;
using Microsoft.Extensions.Time.Testing;
using SmartNotes.Core;
using SmartNotes.TestKit;
using SmartNotes.ViewModels;

namespace SmartNotes.ViewModels.Tests;

public class NoteViewModelTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly InMemoryNoteRepository _repository = new();
    private readonly CountingNoteRepository _counting;
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;
    private readonly FakeWindowManager _windows = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public NoteViewModelTests()
    {
        _counting = new CountingNoteRepository(_repository);
        _notes = new NoteService(_counting, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, Debounce);
    }

    private async Task<NoteViewModel> NewViewModelAsync()
        => new(await _notes.CreateAsync(Token), _notes, _autoSave, _windows);

    private async Task SettleAsync()
    {
        _clock.Advance(Debounce);
        await _autoSave.WhenIdleAsync();
    }

    private async Task<Note?> StoredAsync(NoteViewModel viewModel)
        => await _repository.GetByIdAsync(viewModel.Id, Token);

    [Fact]
    public async Task NoteViewModel_WhenBuiltFromANote_ShowsWhatTheNoteSays()
    {
        var note = await _notes.CreateAsync(Token);
        note.Title = "Shopping";
        note.Content = "milk";
        note.Color = NoteColor.Blue;
        note.IsAlwaysOnTop = true;
        await _notes.SaveAsync(note, Token);

        var viewModel = new NoteViewModel(note, _notes, _autoSave, _windows);

        Assert.Equal(note.Id, viewModel.Id);
        Assert.Equal("Shopping", viewModel.Title);
        Assert.Equal("milk", viewModel.Content);
        Assert.Equal(NoteColor.Blue, viewModel.Color);
        Assert.True(viewModel.IsAlwaysOnTop);
    }

    [Fact]
    public async Task Content_WhenTyped_EndsUpOnDiskOnceTheTypingStops()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.Content = "milk and bread";
        await SettleAsync();

        Assert.Equal("milk and bread", (await StoredAsync(viewModel))!.Content);
    }

    [Fact]
    public async Task Title_WhenTyped_EndsUpOnDisk()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.Title = "Shopping";
        await SettleAsync();

        Assert.Equal("Shopping", (await StoredAsync(viewModel))!.Title);
    }

    [Fact]
    public async Task MovingAndResizingTheWindow_EndsUpOnDisk()
    {
        // MMF 3: reopen the app and the note is the same size, in the same place.
        var viewModel = await NewViewModelAsync();

        viewModel.X = 120;
        viewModel.Y = 240;
        viewModel.Width = 420;
        viewModel.Height = 380;
        await SettleAsync();

        var stored = (await StoredAsync(viewModel))!;
        Assert.Equal((120, 240, 420, 380), (stored.X, stored.Y, stored.Width, stored.Height));
    }

    [Fact]
    public async Task Color_WhenChanged_EndsUpOnDisk()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.Color = NoteColor.Pink;
        await SettleAsync();

        Assert.Equal(NoteColor.Pink, (await StoredAsync(viewModel))!.Color);
    }

    [Fact]
    public async Task IsAlwaysOnTop_WhenPinned_EndsUpOnDisk()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.IsAlwaysOnTop = true;
        await SettleAsync();

        Assert.True((await StoredAsync(viewModel))!.IsAlwaysOnTop);
    }

    [Fact]
    public async Task Content_SetToWhatItAlreadyWas_DoesNotWriteAnything()
    {
        // A window that re-applies its bindings should not cost a disk write.
        var viewModel = await NewViewModelAsync();
        viewModel.Content = "settled";
        await SettleAsync();
        var writesSoFar = _counting.Updates;

        viewModel.Content = "settled";
        await SettleAsync();

        Assert.Equal(writesSoFar, _counting.Updates);
    }

    [Fact]
    public async Task Content_WhenTyped_TellsTheViewItChanged()
    {
        var viewModel = await NewViewModelAsync();
        var announced = new List<string?>();
        ((INotifyPropertyChanged)viewModel).PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        viewModel.Content = "typed";

        Assert.Contains(nameof(NoteViewModel.Content), announced);
    }

    [Fact]
    public async Task ArchiveCommand_FilesTheNoteAwayAndClosesItsWindow()
    {
        // The delete button. The note keeps its text; only the window goes.
        var viewModel = await NewViewModelAsync();
        viewModel.Content = "not destroyed";

        await viewModel.ArchiveCommand.ExecuteAsync(null);

        var stored = (await StoredAsync(viewModel))!;
        Assert.True(stored.IsArchived);
        Assert.Equal("not destroyed", stored.Content);
        Assert.Equal([viewModel.Id], _windows.Closed);
    }

    [Fact]
    public async Task ArchiveCommand_WritesWhatWasTypedBeforeFilingItAway()
    {
        // Archiving mid-debounce must not lose the sentence in flight.
        var viewModel = await NewViewModelAsync();
        viewModel.Content = "typed a moment ago";

        await viewModel.ArchiveCommand.ExecuteAsync(null);

        Assert.Equal("typed a moment ago", (await StoredAsync(viewModel))!.Content);
    }

    [Fact]
    public async Task CloseAsync_WithTypingStillPending_WritesItBeforeTheWindowGoes()
    {
        var viewModel = await NewViewModelAsync();
        viewModel.Content = "half a sentence";

        await viewModel.CloseAsync();

        Assert.Equal("half a sentence", (await StoredAsync(viewModel))!.Content);
    }

    [Fact]
    public async Task TogglePinCommand_OnAnUnpinnedNote_PinsItAndWritesThat()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.TogglePinCommand.Execute(null);
        await SettleAsync();

        Assert.True(viewModel.IsAlwaysOnTop);
        Assert.True((await StoredAsync(viewModel))!.IsAlwaysOnTop);
    }

    [Fact]
    public async Task TogglePinCommand_Twice_LeavesTheNoteUnpinned()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.TogglePinCommand.Execute(null);
        viewModel.TogglePinCommand.Execute(null);
        await SettleAsync();

        Assert.False(viewModel.IsAlwaysOnTop);
        Assert.False((await StoredAsync(viewModel))!.IsAlwaysOnTop);
    }

    [Fact]
    public async Task SetColorCommand_WithAColour_RepaintsTheNoteAndWritesIt()
    {
        var viewModel = await NewViewModelAsync();

        viewModel.SetColorCommand.Execute(NoteColor.Green);
        await SettleAsync();

        Assert.Equal(NoteColor.Green, viewModel.Color);
        Assert.Equal(NoteColor.Green, (await StoredAsync(viewModel))!.Color);
    }

    [Fact]
    public async Task SetColorCommand_WithTheColourItAlreadyIs_WritesNothing()
    {
        var viewModel = await NewViewModelAsync();
        viewModel.SetColorCommand.Execute(NoteColor.Green);
        await SettleAsync();
        var writesSoFar = _counting.Updates;

        viewModel.SetColorCommand.Execute(NoteColor.Green);
        await SettleAsync();

        Assert.Equal(writesSoFar, _counting.Updates);
    }

    [Fact]
    public async Task AvailableColors_OffersEveryColourANoteCanBe()
    {
        var viewModel = await NewViewModelAsync();

        Assert.Equal(Enum.GetValues<NoteColor>(), viewModel.AvailableColors);
    }
}
