using System.Reflection;
using YappyNotes.Core;

namespace YappyNotes.Core.Tests;

/// <summary>
/// Core is what the fast tests run against, so a dependency added here is a
/// dependency added to every one of them. This is the guard.
/// </summary>
public class ArchitectureTests
{
    [Fact]
    public void Core_WhenCompiled_ReferencesNothingButTheBcl()
    {
        var thirdParty = AssemblyReferences.ThirdPartyIn(CoreAssembly.Reference);

        Assert.True(
            thirdParty.Count == 0,
            $"YappyNotes.Core must reference nothing but the BCL, but it now uses: "
            + $"{string.Join(", ", thirdParty)}. Move whatever needed it into Data or App, "
            + "or put an abstraction in Core and the dependency behind it.");
    }
}

internal static class AssemblyReferences
{
    /// <summary>
    /// What an assembly actually uses, minus the framework.
    /// </summary>
    /// <remarks>
    /// GetReferencedAssemblies lists what the compiled code uses - the compiler
    /// drops a PackageReference nothing touches. That is the right behaviour
    /// here: an unused package is a build-file tidiness problem, while a *used*
    /// one is the architectural break worth failing over.
    /// </remarks>
    internal static IReadOnlyList<string> ThirdPartyIn(Assembly assembly)
        => assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => !name.StartsWith("System", StringComparison.Ordinal)
                        && !name.StartsWith("Microsoft.CSharp", StringComparison.Ordinal)
                        && !name.StartsWith("YappyNotes.", StringComparison.Ordinal)
                        && name != "netstandard"
                        && name != "mscorlib")
            .Order(StringComparer.Ordinal)
            .ToList();
}
