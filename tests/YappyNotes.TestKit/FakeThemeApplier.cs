using YappyNotes.Core;
using YappyNotes.ViewModels;

namespace YappyNotes.TestKit;

/// <summary>Records the themes a view-model asked for, without an Application existing.</summary>
public sealed class FakeThemeApplier : IThemeApplier
{
    public List<AppTheme> Applied { get; } = [];

    public void Apply(AppTheme theme) => Applied.Add(theme);
}
