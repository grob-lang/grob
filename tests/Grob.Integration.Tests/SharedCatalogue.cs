using Grob.Runtime;
using Grob.Stdlib;

namespace Grob.Integration.Tests;

/// <summary>
/// The one stdlib <see cref="NativeCatalogue"/> every <c>TypeChecker</c> in this assembly
/// reads (D-426, gate report §1.5). Built once, over inert capabilities, on first use; the
/// catalogue is immutable, so xUnit's parallel test classes share it safely.
/// </summary>
internal static class SharedCatalogue {
    /// <summary>The compile-only stdlib catalogue.</summary>
    internal static NativeCatalogue Stdlib { get; } = StdlibCatalogue.ForCompileOnly();
}
