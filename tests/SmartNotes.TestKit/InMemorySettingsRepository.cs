using SmartNotes.Core;

namespace SmartNotes.TestKit;

/// <summary>An ISettingsRepository in a dictionary, for tests above the storage layer.</summary>
public sealed class InMemorySettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _values = [];

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_values));

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        _values[key] = value;
        return Task.CompletedTask;
    }
}
