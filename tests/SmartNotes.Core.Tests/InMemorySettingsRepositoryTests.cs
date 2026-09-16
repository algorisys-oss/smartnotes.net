using SmartNotes.Core;
using SmartNotes.TestKit;

namespace SmartNotes.Core.Tests;

public sealed class InMemorySettingsRepositoryTests : SettingsRepositoryContract
{
    protected override Task<ISettingsRepository> NewRepositoryAsync()
        => Task.FromResult<ISettingsRepository>(new InMemorySettingsRepository());
}
