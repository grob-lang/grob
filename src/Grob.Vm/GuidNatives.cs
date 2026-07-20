using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Construction and canonical-string access for the <c>guid</c> runtime
/// representation. Instance property/method dispatch itself moved to
/// <see cref="Grob.Core.NamedTypes.NamedTypeRegistry"/> (D-356) — this class now holds
/// only the field-layout constants and the accessor <c>Grob.Vm.Tests</c> constructs
/// fixture values through. A <c>guid</c> value is a <see cref="GrobStruct"/> with
/// <c>TypeName</c> <c>"guid"</c> and exactly one field, <see cref="ValueFieldName"/>,
/// holding the canonical lowercase-hyphenated string form (D-303's "boxed
/// <see cref="Guid"/>" realised as a hidden field, since <see cref="GrobStruct"/> can
/// only ever hold named <see cref="GrobValue"/> fields).
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
}
