using System.Reflection;

namespace YappyNotes.Core;

/// <summary>
/// An anchor for tests and tooling that need a handle on this assembly without
/// depending on whichever type happens to live in it today.
/// </summary>
public static class CoreAssembly
{
    public static Assembly Reference { get; } = typeof(CoreAssembly).Assembly;
}
