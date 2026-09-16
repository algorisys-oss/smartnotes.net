using Avalonia;
using Avalonia.Styling;
using SmartNotes.Core;
using SmartNotes.ViewModels;

namespace SmartNotes.App;

/// <summary>Puts a chosen theme onto the running application.</summary>
/// <remarks>
/// Note windows pin themselves to Light and are unaffected: a note is paper, and
/// a dark sticky note is a different product.
/// </remarks>
public sealed class ThemeApplier : IThemeApplier
{
    public void Apply(AppTheme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = Variant(theme);
        }
    }

    public static ThemeVariant Variant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
