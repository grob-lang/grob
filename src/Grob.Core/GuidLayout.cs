namespace Grob.Core;

/// <summary>
/// The <c>guid</c> runtime layout, defined once for the assemblies that build or read a
/// <c>guid</c> value directly: <c>Grob.Vm</c>'s sort and equality intrinsics and
/// <c>Grob.Stdlib</c>'s <c>GuidPlugin</c>, which constructs the values (R-19, closed by
/// D-426 change group 8). A <c>guid</c> value is a <c>GrobStruct</c> with
/// <see cref="TypeName"/> and exactly one field, <see cref="ValueFieldName"/>, holding the
/// canonical lowercase-hyphenated string form (D-303's "boxed <see cref="Guid"/>"
/// realised as a hidden field).
/// </summary>
public static class GuidLayout {
    /// <summary>The struct type name every <c>guid</c> value carries.</summary>
    public const string TypeName = "guid";

    /// <summary>
    /// The hidden field storing a <c>guid</c> value's canonical string form. Not a
    /// spellable Grob identifier (leading double underscore), and <c>guid</c> is never a
    /// user-declarable <c>type</c>, so no user-constructed struct can collide with it.
    /// </summary>
    public const string ValueFieldName = "__value";
}
