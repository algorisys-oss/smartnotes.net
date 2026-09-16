using SmartNotes.Core;

namespace SmartNotes.TestKit;

/// <summary>
/// What every ISettingsRepository must do, whatever it stores settings in.
/// Derived once per implementation, for the same reason NoteRepositoryContract is.
/// </summary>
public abstract class SettingsRepositoryContract
{
    protected abstract Task<ISettingsRepository> NewRepositoryAsync();

    [Fact]
    public async Task GetAllAsync_BeforeAnythingIsSaved_ReturnsNothing()
    {
        var repository = await NewRepositoryAsync();

        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task SetAsync_ThenGetAll_ReturnsWhatWasSaved()
    {
        var repository = await NewRepositoryAsync();

        await repository.SetAsync("theme", "Dark");

        Assert.Equal("Dark", (await repository.GetAllAsync())["theme"]);
    }

    [Fact]
    public async Task SetAsync_ForAKeyAlreadySaved_ReplacesItRatherThanAddingASecond()
    {
        var repository = await NewRepositoryAsync();
        await repository.SetAsync("theme", "Dark");

        await repository.SetAsync("theme", "Light");

        var all = await repository.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Light", all["theme"]);
    }

    [Fact]
    public async Task SetAsync_WithSeveralKeys_KeepsThemAllApart()
    {
        var repository = await NewRepositoryAsync();

        await repository.SetAsync("theme", "Dark");
        await repository.SetAsync("defaultColor", "Green");

        var all = await repository.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("Dark", all["theme"]);
        Assert.Equal("Green", all["defaultColor"]);
    }

    [Fact]
    public async Task SetAsync_WithAnEmptyValue_StoresItRatherThanDroppingTheKey()
    {
        var repository = await NewRepositoryAsync();

        await repository.SetAsync("theme", string.Empty);

        Assert.Equal(string.Empty, (await repository.GetAllAsync())["theme"]);
    }
}
