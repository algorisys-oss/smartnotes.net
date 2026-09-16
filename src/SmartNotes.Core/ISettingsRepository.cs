namespace SmartNotes.Core;

/// <summary>
/// Key-value storage for settings.
/// </summary>
/// <remarks>
/// Strings rather than typed values, because the storage should not need
/// changing every time a setting is added - <see cref="SettingsService"/> owns
/// what the strings mean.
/// </remarks>
public interface ISettingsRepository
{
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Stores a value, replacing any already under that key.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}
