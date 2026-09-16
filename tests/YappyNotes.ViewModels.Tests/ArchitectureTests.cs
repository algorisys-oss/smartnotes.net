using System.Reflection;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

/// <summary>
/// The view-model layer has no reference to Avalonia on purpose: it is what keeps
/// a view-model a plain object a test builds with `new`, on no UI thread. These
/// tests are why that stays true - see CLAUDE.md.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void ViewModels_WhenCompiled_ReferenceNoAvaloniaAssembly()
    {
        var avalonia = ThirdPartyReferences()
            .Where(name => name.StartsWith("Avalonia", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            avalonia.Count == 0,
            $"A view-model is using Avalonia ({string.Join(", ", avalonia)}). That makes the "
            + "layer untestable without a UI thread. Put an abstraction in Core and implement "
            + "it in App - IWindowManager is the pattern.");
    }

    /// <summary>
    /// A subset check rather than an equality one, deliberately: the compiler
    /// drops a reference the code does not use, so asserting the toolkit is
    /// *present* would fail on a layer that has no bindable property yet. What
    /// must never change is that nothing else gets in.
    /// </summary>
    [Fact]
    public void ViewModels_WhenCompiled_ReferenceNothingBeyondTheMvvmToolkit()
    {
        string[] allowed = ["CommunityToolkit.Mvvm"];

        var unexpected = ThirdPartyReferences().Except(allowed, StringComparer.Ordinal).ToList();

        Assert.True(
            unexpected.Count == 0,
            $"YappyNotes.ViewModels may use {string.Join(", ", allowed)} and nothing else, "
            + $"but it now uses: {string.Join(", ", unexpected)}.");
    }

    private static IReadOnlyList<string> ThirdPartyReferences()
        => ViewModelsAssembly.Reference.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                        && !name.StartsWith("Microsoft.CSharp", StringComparison.Ordinal)
                        && !name.StartsWith("YappyNotes.", StringComparison.Ordinal)
                        && name != "netstandard"
                        && name != "mscorlib")
            .Order(StringComparer.Ordinal)
            .ToList();
}
