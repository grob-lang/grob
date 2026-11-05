using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Construction and canonical-string access for the <c>guid</c> runtime
/// representation. Instance property/method dispatch lives in <c>Grob.Stdlib</c>'s
/// <c>GuidPlugin</c>, which registers the <c>guid</c> named type into the catalogue the
/// VM reads (D-356, D-426 Decision 3) — this class holds only the field-layout accessors
/// <c>Grob.Vm.Tests</c> constructs fixture values through (the layout itself is
/// <c>Grob.Core</c>'s <see cref="GuidLayout"/>; R-19). A <c>guid</c> value is a <see cref="GrobStruct"/> with
/// <see cref="GuidLayout.TypeName"/> and exactly one field, <see cref="GuidLayout.ValueFieldName"/>,
/// holding the canonical lowercase-hyphenated string form (D-303's "boxed
/// <see cref="Guid"/>" realised as a hidden field, since <see cref="GrobStruct"/> can
/// only ever hold named <see cref="GrobValue"/> fields).
/// </summary>
internal static class GuidNatives {
    /// <summary>Constructs the runtime <c>guid</c> value for the canonical string <paramref name="canonical"/>.</summary>
    internal static GrobValue FromCanonicalString(string canonical) =>
        GrobValue.FromStruct(new GrobStruct(
            GuidLayout.TypeName,
            [new KeyValuePair<string, GrobValue>(GuidLayout.ValueFieldName, GrobValue.FromString(canonical))]));

    /// <summary>The canonical lowercase-hyphenated string stored on <paramref name="receiver"/>.</summary>
    internal static string CanonicalString(GrobStruct receiver) => receiver.GetField(GuidLayout.ValueFieldName).AsString();
}
