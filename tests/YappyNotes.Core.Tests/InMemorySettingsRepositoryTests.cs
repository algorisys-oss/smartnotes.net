using YappyNotes.Core;
using YappyNotes.TestKit;

namespace YappyNotes.Core.Tests;

public sealed class InMemorySettingsRepositoryTests : SettingsRepositoryContract
{
    protected override Task<ISettingsRepository> NewRepositoryAsync()
        => Task.FromResult<ISettingsRepository>(new InMemorySettingsRepository());
}
