using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// The four primitive receivers that can carry instance members (D-426 Decision 3) and
/// the namespace names reserved for them on the public registrar (gate report F8).
/// </summary>
internal static class PrimitiveReceivers {
    private static readonly string[] _names = ["int", "float", "string", "bool"];

    /// <summary><see langword="true"/> when <paramref name="namespaceName"/> is a primitive type name.</summary>
    internal static bool IsReserved(string namespaceName) => Array.IndexOf(_names, namespaceName) >= 0;

    /// <summary>
    /// The receiver's type name as a global-name prefix. Throws
    /// <see cref="ArgumentException"/> for a type that is not a primitive receiver.
    /// </summary>
    internal static string NameOf(GrobType type, string paramName) => type switch {
        GrobType.Int => "int",
        GrobType.Float => "float",
        GrobType.String => "string",
        GrobType.Bool => "bool",
        _ => throw new ArgumentException($"{type} is not a primitive receiver; only int, float, string and bool carry primitive members.", paramName),
    };
}
