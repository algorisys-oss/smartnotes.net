using System.Reflection;

namespace YappyNotes.App;

/// <summary>
/// What version this build is.
/// </summary>
/// <remarks>
/// Read from the assembly rather than written down anywhere, so it can only ever
/// agree with <c>VersionPrefix</c> in <c>Directory.Build.props</c> — which stays
/// the one place a version is set. The informational version carries any suffix
/// (<c>-beta.1</c>); the assembly version does not, which is why it is only the
/// fallback.
/// </remarks>
public static class AppVersion
{
    public static string Current { get; } = Read();

    private static string Read()
    {
        var assembly = typeof(AppVersion).Assembly;

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // The SDK appends "+<commit sha>" when the build knows one. That is
            // for a build log, not for a status bar.
            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus < 0 ? informational : informational[..plus];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
