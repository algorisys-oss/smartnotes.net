using SmartNotes.Core;

namespace SmartNotes.ViewModels;

/// <summary>
/// Puts a theme onto the running application.
/// </summary>
/// <remarks>
/// The seam that keeps the view-model layer free of Avalonia, the same way
/// <see cref="IWindowManager"/> does for windows. A view-model says which theme;
/// the app knows what a theme variant is.
/// </remarks>
public interface IThemeApplier
{
    void Apply(AppTheme theme);
}
