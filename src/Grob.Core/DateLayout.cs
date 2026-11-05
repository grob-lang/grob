namespace Grob.Core;

/// <summary>
/// The <c>date</c> runtime layout, defined once for the three assemblies that build or
/// read a <c>date</c> value directly: <c>Grob.Vm</c>'s comparison intrinsics (D-367),
/// <c>Grob.Stdlib</c>'s <c>DatePlugin</c>, which constructs the values, and the type
/// checker's <c>date</c> comparison rule. They are DAG siblings, so <c>Grob.Core</c> is
/// the only home all of them can read it from. A <c>date</c> value is a
/// <c>GrobStruct</c> with <see cref="TypeName"/> and exactly one field,
/// <see cref="ValueFieldName"/>, holding a <see cref="DateTimeOffset"/> formatted with
/// <see cref="RoundTripFormat"/>. <see cref="RoundTripFormat"/> was consolidated by
/// D-357/D-367 onto <c>NamedTypeRegistry</c> and relocated here when D-426 change group 7
/// deleted the registry; the field and type names joined it in change group 8 (R-19).
/// </summary>
public static class DateLayout {
    /// <summary>The struct type name every <c>date</c> value carries.</summary>
    public const string TypeName = "date";

    /// <summary>
    /// The hidden field storing a <c>date</c> value's round-trip string form. Not a
    /// spellable Grob identifier (leading double underscore), and <c>date</c> is never a
    /// user-declarable <c>type</c>, so no user-constructed struct can collide with it.
    /// </summary>
    public const string ValueFieldName = "__value";

    /// <summary>
    /// The hidden field's round-trip storage format: preserves the offset exactly and is
    /// unambiguous to parse back with <see cref="System.Globalization.DateTimeStyles.None"/>.
    /// </summary>
    public const string RoundTripFormat = "yyyy-MM-ddTHH:mm:sszzz";
}
