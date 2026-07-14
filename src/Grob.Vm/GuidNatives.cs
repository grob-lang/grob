using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Factory and property accessors for the <c>guid</c> instance surface (Sprint 8
/// Increment D): <c>version</c>/<c>isEmpty</c> (properties, read directly by
/// <see cref="OpCode.GetProperty"/>) and <c>toString</c>/<c>toUpperString</c>/
/// <c>toCompactString</c> (methods, bound at <see cref="OpCode.GetProperty"/> dispatch
/// time exactly as <see cref="ArrayNatives.GetMethod"/> binds array higher-order
/// methods). A <c>guid</c> value is a <see cref="GrobStruct"/> with <c>TypeName</c>
/// <c>"guid"</c> and exactly one field, <see cref="ValueFieldName"/>, holding the
/// canonical lowercase-hyphenated string form (D-303's "boxed <c>System.Guid</c>"
/// realised as a hidden field, since <see cref="GrobStruct"/> can only ever hold named
/// <see cref="GrobValue"/> fields — see the Increment D plan's runtime-storage
/// reconciliation). This keeps value equality, hashing and <c>ValueDisplay</c>'s cycle
/// detection all working through the unmodified existing <see cref="GrobStruct"/>
/// machinery.
/// </summary>
internal static class GuidNatives {
    /// <summary>
    /// The hidden field name storing a <c>guid</c> value's canonical string form. Not a
    /// spellable Grob identifier (leading double underscore), and <c>guid</c> is never a
    /// user-declarable <c>type</c>, so no user-constructed struct can collide with it.
    /// </summary>
    internal const string ValueFieldName = "__value";

    /// <summary>The struct type name every <c>guid</c> value carries.</summary>
    internal const string TypeName = "guid";

    /// <summary>Constructs the runtime <c>guid</c> value for the canonical string <paramref name="canonical"/>.</summary>
    internal static GrobValue FromCanonicalString(string canonical) =>
        GrobValue.FromStruct(new GrobStruct(
            TypeName,
            [new KeyValuePair<string, GrobValue>(ValueFieldName, GrobValue.FromString(canonical))]));

    /// <summary>The canonical lowercase-hyphenated string stored on <paramref name="receiver"/>.</summary>
    internal static string CanonicalString(GrobStruct receiver) => receiver.GetField(ValueFieldName).AsString();

    /// <summary>The parsed <see cref="Guid"/> value <paramref name="receiver"/> stores.</summary>
    internal static Guid ToGuid(GrobStruct receiver) => Guid.Parse(CanonicalString(receiver));

    /// <summary><c>id.version</c> — 4, 5 or 7.</summary>
    internal static GrobValue GetVersion(GrobStruct receiver) => GrobValue.FromInt(ToGuid(receiver).Version);

    /// <summary><c>id.isEmpty</c> — true when the value is all zeros.</summary>
    internal static GrobValue GetIsEmpty(GrobStruct receiver) => GrobValue.FromBool(ToGuid(receiver) == Guid.Empty);

    /// <summary>
    /// Returns the bound <see cref="NativeFunction"/> for the given
    /// <paramref name="methodName"/> on <paramref name="receiver"/>, or
    /// <see langword="null"/> when the name is not a <c>guid</c> instance method.
    /// </summary>
    internal static NativeFunction? GetMethod(string methodName, GrobStruct receiver) =>
        methodName switch {
            "toString" => new NativeFunction("toString", 0,
                (_, _) => GrobValue.FromString(CanonicalString(receiver))),
            "toUpperString" => new NativeFunction("toUpperString", 0,
                (_, _) => GrobValue.FromString(CanonicalString(receiver).ToUpperInvariant())),
            "toCompactString" => new NativeFunction("toCompactString", 0,
                (_, _) => GrobValue.FromString(ToGuid(receiver).ToString("N"))),
            _ => null,
        };
}
