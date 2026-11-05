using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// Shorthand the first-party plugins write their native signatures with (D-426 change
/// groups 5 and 6): the concrete types the stdlib surface uses and three parameter shapes. Each
/// plugin imports it with <c>using static</c>, so a registration reads as its signature,
/// <c>Signature("math.pow", FloatType, Param("base", FloatType), Param("exp", FloatType))</c>.
/// Parameter names follow the gate report's §8.
/// </summary>
internal static class Signatures {
    internal static readonly SignatureType.Concrete IntType = SignatureType.Of(GrobType.Int);
    internal static readonly SignatureType.Concrete NullableIntType = SignatureType.Of(GrobType.NullableInt);
    internal static readonly SignatureType.Concrete FloatType = SignatureType.Of(GrobType.Float);
    internal static readonly SignatureType.Concrete NullableFloatType = SignatureType.Of(GrobType.NullableFloat);
    internal static readonly SignatureType.Concrete StringType = SignatureType.Of(GrobType.String);
    internal static readonly SignatureType.Concrete NullableStringType = SignatureType.Of(GrobType.NullableString);
    internal static readonly SignatureType.Concrete BoolType = SignatureType.Of(GrobType.Bool);
    internal static readonly SignatureType.Concrete NilType = SignatureType.Of(GrobType.Nil);
    internal static readonly SignatureType.Concrete MapType = SignatureType.Of(GrobType.Map);
    internal static readonly SignatureType.Concrete StructType = SignatureType.Of(GrobType.Struct);

    /// <summary>An array of any element: the runtime shape <c>formatAs</c> takes (gate report F2).</summary>
    internal static readonly SignatureType.Concrete ArrayType = SignatureType.Of(GrobType.Array);

    /// <summary>
    /// <c>string[]</c>. Declared, not enforced: the checker passes any array here, as it
    /// did before signatures (gate report F4).
    /// </summary>
    internal static readonly SignatureType.Concrete StringArrayType = SignatureType.ArrayOf(StringType);

    /// <summary>
    /// A signature: <paramref name="name"/> is qualified for a namespace native
    /// (<c>math.sqrt</c>) and bare for a primitive member or built-in (<c>trim</c>).
    /// </summary>
    internal static FunctionSignature Signature(
            string name, SignatureType.Concrete returnType, params SignatureParameter[] parameters) =>
        new(name, parameters, returnType);

    /// <summary>A required positional parameter.</summary>
    internal static SignatureParameter Param(string name, SignatureType.Concrete type) => new(name, type);

    /// <summary>A trailing parameter a call may omit; the compiler fills <paramref name="defaultValue"/> (D-358).</summary>
    internal static SignatureParameter Optional(string name, SignatureType.Concrete type, GrobValue defaultValue) =>
        new(name, type, defaultValue: defaultValue);

    /// <summary>A variable-length tail; a call supplies at least one argument for it (gate report F9).</summary>
    internal static SignatureParameter Variadic(string name, SignatureType.Concrete elementType) =>
        new(name, elementType, isVariadic: true);
}
