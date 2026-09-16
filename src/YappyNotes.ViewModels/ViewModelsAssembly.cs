using System.Reflection;

namespace YappyNotes.ViewModels;

/// <summary>
/// An anchor for tests and tooling that need a handle on this assembly without
/// depending on whichever view-model happens to live in it today.
/// </summary>
public static class ViewModelsAssembly
{
    public static Assembly Reference { get; } = typeof(ViewModelsAssembly).Assembly;
}
