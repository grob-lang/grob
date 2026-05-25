namespace Grob.Core;

/// <summary>
/// Discriminates the nine runtime value variants.
/// Representation locked May 2026 (D-303, OQ-005 closed).
/// <c>default(GrobValueKind)</c> is <c>Nil</c> — intentional.
/// </summary>
public enum GrobValueKind : byte {
    /// <summary>The nil / absent value. 0 is deliberate: <c>default(GrobValue)</c> is <c>Nil</c>.</summary>
    Nil = 0,

    /// <summary>A boolean value (<c>true</c> or <c>false</c>).</summary>
    Bool = 1,

    /// <summary>A 64-bit signed integer.</summary>
    Int = 2,

    /// <summary>A 64-bit IEEE 754 double-precision float.</summary>
    Float = 3,

    /// <summary>An immutable Unicode string.</summary>
    String = 4,

    /// <summary>A mutable ordered array of <see cref="GrobValue"/> elements.</summary>
    Array = 5,

    /// <summary>A mutable string-keyed map of <see cref="GrobValue"/> entries.</summary>
    Map = 6,

    /// <summary>
    /// A struct instance — user-defined types, plugin types and built-in
    /// compound types alike. Discrimination between struct sub-types happens
    /// at the type-registry level via the runtime type of the boxed reference.
    /// </summary>
    Struct = 7,

    /// <summary>A Grob lambda or function reference.</summary>
    Function = 8,
}
