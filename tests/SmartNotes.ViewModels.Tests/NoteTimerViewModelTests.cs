using Microsoft.Extensions.Time.Testing;
using SmartNotes.Core;
using SmartNotes.TestKit;
using SmartNotes.ViewModels;

namespace SmartNotes.ViewModels.Tests;

public class NoteTimerViewModelTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly InMemoryNoteRepository _repository = new();
    private readonly CountingNoteRepository _counting;
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;
    private readonly InlineUiDispatcher _ui = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public NoteTimerViewModelTests()
    {
        _counting = new CountingNoteRepository(_repository);
        _notes = new NoteService(_counting, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, Debounce);
    }

    private async Task<NoteViewModel> NoteWithTimerAsync()
    {
        var note = await _notes.CreateAsync(Token);
        note.Timer = new NoteTimer
        {
            Direction = TimerDirection.CountDown,
            Duration = TimeSpan.FromMinutes(5),
            Label = "Back in",
        };
        await _notes.SaveAsync(note, Token);

        return new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager(), _clock, _ui);
    }

    private async Task SettleAsync()
    {
        _clock.Advance(Debounce);
        await _autoSave.WhenIdleAsync();
    }

    [Fact]
    public async Task NoteViewModel_ForANoteWithNoTimer_HasNone()
    {
        var note = await _notes.CreateAsync(Token);
        var viewModel = new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager(), _clock, _ui);

        Assert.Null(viewModel.Timer);
        Assert.False(viewModel.HasTimer);
    }

    [Fact]
    public async Task NoteViewModel_ForANoteWithATimer_ShowsItStopped()
    {
        var viewModel = await NoteWithTimerAsync();

        Assert.NotNull(viewModel.Timer);
        Assert.True(viewModel.HasTimer);
        Assert.Equal("5:00", viewModel.Timer.Display);
        Assert.Equal("Back in", viewModel.Timer.Label);
        Assert.False(viewModel.Timer.IsRunning);
    }

    [Fact]
    public async Task StartCommand_ThenTimePassing_CountsDownOnScreen()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromSeconds(61));

        Assert.Equal("3:59", viewModel.Timer.Display);
    }

    /// <summary>
    /// The property the whole feature rests on: ticking asks for no write.
    /// </summary>
    /// <remarks>
    /// Asserted against the callback rather than against the number of rows
    /// written, deliberately. Counting writes cannot see this: a tick every
    /// second keeps resetting a 750ms debounce, so a ticker that *did* ask for a
    /// save would still never produce one, and the test would pass while the
    /// design was broken. It did, until this was rewritten.
    /// </remarks>
    [Fact]
    public void Ticking_ForAnHour_NeverAsksForAWrite()
    {
        var asks = 0;
        var timer = new NoteTimer { Duration = TimeSpan.FromHours(3) };
        using var viewModel = new NoteTimerViewModel(timer, _clock, _ui, () => asks++);

        viewModel.StartCommand.Execute(null);
        var asksAfterStarting = asks;
        _clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(1, asksAfterStarting);
        Assert.Equal(asksAfterStarting, asks);
    }

    /// <summary>
    /// The same property end to end: an hour of ticking leaves the autosave with
    /// nothing to flush.
    /// </summary>
    [Fact]
    public async Task Ticking_ForAnHour_LeavesNothingPendingToFlush()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);
        await SettleAsync();
        var writesAfterStarting = _counting.Updates;

        for (var minute = 0; minute < 60; minute++)
        {
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        await _autoSave.FlushAllAsync(Token);

        Assert.Equal(writesAfterStarting, _counting.Updates);
    }

    [Fact]
    public async Task StartCommand_IsATransitionAndSoIsWritten()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.StartCommand.Execute(null);
        await SettleAsync();

        Assert.True((await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer!.IsRunning);
    }

    [Fact]
    public async Task PauseCommand_StopsTheCountAndWritesIt()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(2));

        viewModel.Timer.PauseCommand.Execute(null);
        await SettleAsync();

        Assert.False(viewModel.Timer.IsRunning);
        Assert.Equal("3:00", viewModel.Timer.Display);

        var stored = (await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer!;
        Assert.False(stored.IsRunning);
        Assert.Equal(TimeSpan.FromMinutes(2), stored.Accumulated);
    }

    [Fact]
    public async Task RestartCommand_PutsItBackToTheTopAndKeepsGoing()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(4));

        viewModel.Timer.RestartCommand.Execute(null);

        // Read before settling: settling advances the clock, and a running timer
        // is supposed to have moved on by then.
        Assert.Equal("5:00", viewModel.Timer.Display);
        Assert.True(viewModel.Timer.IsRunning);

        await SettleAsync();
        var stored = (await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer!;
        Assert.True(stored.IsRunning);
        Assert.Equal(TimeSpan.Zero, stored.Accumulated);
    }

    [Fact]
    public async Task Display_WhileRunning_IsRefreshedThroughTheUiDispatcher()
    {
        // A timer callback arrives on a thread-pool thread; a binding must be
        // updated on the UI one.
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromSeconds(3));

        Assert.True(_ui.Posted > 0);
    }

    [Fact]
    public async Task Display_WhileStopped_IsNotBeingRefreshedAtAll()
    {
        // Nothing is changing, so nothing should be waking up once a second.
        var viewModel = await NoteWithTimerAsync();
        var postsBefore = _ui.Posted;

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(postsBefore, _ui.Posted);
    }

    [Fact]
    public async Task HasFinished_OnceTheCountdownRunsOut_IsTrue()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);

        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.True(viewModel.Timer.HasFinished);
    }

    [Fact]
    public async Task Display_PastTheEnd_SitsAtZeroRatherThanGoingNegative()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);

        _clock.Advance(TimeSpan.FromMinutes(7).Add(TimeSpan.FromSeconds(5)));

        Assert.Equal("0:00", viewModel.Timer.Display);
        Assert.True(viewModel.Timer.HasFinished);
    }

    [Fact]
    public async Task AddTimerCommand_OnANoteWithout_GivesItOneAndWritesThat()
    {
        var note = await _notes.CreateAsync(Token);
        var viewModel = new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager(), _clock, _ui);

        viewModel.AddTimerCommand.Execute(null);
        await SettleAsync();

        Assert.True(viewModel.HasTimer);
        Assert.NotNull((await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer);
    }

    [Fact]
    public async Task RemoveTimerCommand_TakesItOffAndWritesThat()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.RemoveTimerCommand.Execute(null);
        await SettleAsync();

        Assert.False(viewModel.HasTimer);
        Assert.Null((await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer);
    }

    [Fact]
    public async Task Dispose_StopsTheTickerRatherThanLeavingItRunning()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromSeconds(2));

        viewModel.Timer.Dispose();
        var postsAfterDisposing = _ui.Posted;
        _clock.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(postsAfterDisposing, _ui.Posted);
    }

    [Fact]
    public async Task Label_WhenRenamed_ShowsItAndWritesIt()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.Label = "Grabbing coffee";
        await SettleAsync();

        Assert.Equal("Grabbing coffee", viewModel.Timer.Label);
        Assert.Equal("Grabbing coffee", (await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer!.Label);
    }

    [Fact]
    public async Task DurationMinutes_WhenChanged_ChangesWhatTheCountStartsFrom()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.DurationMinutes = 12;
        await SettleAsync();

        Assert.Equal("12:00", viewModel.Timer.Display);
        Assert.Equal(TimeSpan.FromMinutes(12), (await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer!.Duration);
    }

    [Fact]
    public async Task DurationMinutes_SetToWhatItAlreadyIs_WritesNothing()
    {
        var viewModel = await NoteWithTimerAsync();
        await SettleAsync();
        var writesSoFar = _counting.Updates;

        viewModel.Timer!.DurationMinutes = 5;
        await SettleAsync();

        Assert.Equal(writesSoFar, _counting.Updates);
    }

    [Fact]
    public async Task DurationMinutes_SetToSomethingSilly_IsHeldToSomethingUsable()
    {
        // A zero-minute or negative countdown is finished before it starts.
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.DurationMinutes = 0;
        Assert.True(viewModel.Timer.DurationMinutes >= 1);

        viewModel.Timer.DurationMinutes = -5;
        Assert.True(viewModel.Timer.DurationMinutes >= 1);
    }

    [Fact]
    public async Task ToggleDirectionCommand_TurnsACountdownIntoACountUp()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.ToggleDirectionCommand.Execute(null);
        await SettleAsync();

        Assert.False(viewModel.Timer.IsCountingDown);
        Assert.Equal("0:00", viewModel.Timer.Display);
        Assert.Equal(
            TimerDirection.CountUp,
            (await _repository.GetByIdAsync(viewModel.Id, Token))!.Timer!.Direction);
    }

    [Fact]
    public async Task ToggleDirectionCommand_Twice_IsBackToACountdown()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.ToggleDirectionCommand.Execute(null);
        viewModel.Timer.ToggleDirectionCommand.Execute(null);

        Assert.True(viewModel.Timer.IsCountingDown);
        Assert.Equal("5:00", viewModel.Timer.Display);
    }

    [Fact]
    public async Task CanEdit_WhileTheTimerIsRunning_IsFalse()
    {
        // Changing the length of a countdown halfway through it is a way to be
        // confused rather than a feature.
        var viewModel = await NoteWithTimerAsync();
        Assert.True(viewModel.Timer!.CanEdit);

        viewModel.Timer.StartCommand.Execute(null);

        Assert.False(viewModel.Timer.CanEdit);
    }

    [Fact]
    public async Task CanEdit_OnceItIsPausedAgain_IsTrue()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(1));

        viewModel.Timer.PauseCommand.Execute(null);

        Assert.True(viewModel.Timer.CanEdit);
    }

    [Fact]
    public async Task SetDurationCommand_WithAPreset_UsesIt()
    {
        var viewModel = await NoteWithTimerAsync();

        viewModel.Timer!.SetDurationCommand.Execute(15);
        await SettleAsync();

        Assert.Equal("15:00", viewModel.Timer.Display);
    }

    [Fact]
    public async Task DurationMinutes_ChangedWhilePaused_CountsFromTheNewLengthLessWhatWasUsed()
    {
        var viewModel = await NoteWithTimerAsync();
        viewModel.Timer!.StartCommand.Execute(null);
        _clock.Advance(TimeSpan.FromMinutes(2));
        viewModel.Timer.PauseCommand.Execute(null);

        viewModel.Timer.DurationMinutes = 10;

        Assert.Equal("8:00", viewModel.Timer.Display);
    }
}
