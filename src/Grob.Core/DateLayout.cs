namespace Grob.Core;

/// <summary>
/// The <c>date</c> layout constant shared by the two assemblies that read a <c>date</c>
/// value's hidden field directly: <c>Grob.Vm</c>'s comparison intrinsics (D-367) and
/// <c>Grob.Stdlib</c>'s <c>DatePlugin</c>, which constructs the values. They are DAG
/// siblings, so <c>Grob.Core</c> is the only home both can read it from. Consolidated
/// here by D-357/D-367 (then on <c>NamedTypeRegistry</c>; relocated when D-426 change
/// group 7 moved the <c>date</c> instance surface into its plugin and deleted the
/// registry). The field name and type name are still written in each assembly
/// (R-19, reported, not fixed).
/// </summary>
public static class DateLayout {
    /// <summary>
    /// The hidden field's round-trip storage format: preserves the offset exactly and is
    /// unambiguous to parse back with <see cref="System.Globalization.DateTimeStyles.None"/>.
    /// </summary>
    public const string RoundTripFormat = "yyyy-MM-ddTHH:mm:sszzz";
}
