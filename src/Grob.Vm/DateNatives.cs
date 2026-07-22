using System.Globalization;

using Grob.Core;
using Grob.Core.NamedTypes;

namespace Grob.Vm;

/// <summary>
/// Construction, canonical-string and instant conversion for the <c>date</c> runtime
/// representation. Instance property/method dispatch itself moved to
/// <see cref="Grob.Core.NamedTypes.NamedTypeRegistry"/> (D-356) — this class now holds
/// only the field-layout constants, the accessor <c>Grob.Vm.Tests</c> constructs
/// fixture values through, and <see cref="ToDateTimeOffset"/>, which
/// <see cref="OpCode.LessDate"/>/<see cref="OpCode.GreaterDate"/> still call directly
/// (D-357's instant-based comparison is deliberately untouched by D-356's migration).
/// A <c>date</c> value is a <see cref="GrobStruct"/> with <c>TypeName</c> <c>"date"</c>
/// and exactly one field, <see cref="ValueFieldName"/>, holding a round-trip-formatted
/// <see cref="DateTimeOffset"/> string (D-303's "boxed <see cref="DateTimeOffset"/>"
/// realised as a hidden field, since <see cref="GrobStruct"/> can only ever hold named
/// <see cref="GrobValue"/> fields — the same realisation <see cref="GuidNatives"/> uses
/// for a boxed <see cref="Guid"/>). The round-trip format constant itself is
/// <see cref="NamedTypeRegistry.RoundTripFormat"/> (consolidated there by D-357/D-367 —
/// previously an independent copy here, drifting-hazard-prone alongside
/// <c>Grob.Stdlib.DatePlugin</c>'s own former copy).
/// </summary>
internal static class DateNatives {
    /// <summary>The hidden field name storing a <c>date</c> value's round-trip string form.</summary>
    internal const string ValueFieldName = "__value";

    /// <summary>The struct type name every <c>date</c> value carries.</summary>
    internal const string TypeName = "date";

    // The canonical ISO-8601 rendering (ValueDisplay's registered toString(), toIso(),
    // toIsoDateTime()). Unlike DateTime, DateTimeOffset's 'K' specifier is equivalent to
    // 'zzz' — it never renders "Z" for a zero offset, always "+00:00" — so the zero-offset
    // case is handled explicitly below rather than relying on 'K'.
    private const string IsoOffsetFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string IsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    /// <summary>Constructs the runtime <c>date</c> value for <paramref name="value"/>.</summary>
    internal static GrobValue FromDateTimeOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        TypeName,
        [new KeyValuePair<string, GrobValue>(
            ValueFieldName, GrobValue.FromString(value.ToString(NamedTypeRegistry.RoundTripFormat, CultureInfo.InvariantCulture)))]));

    /// <summary>The <see cref="DateTimeOffset"/> <paramref name="receiver"/> stores.</summary>
    internal static DateTimeOffset ToDateTimeOffset(GrobStruct receiver) => DateTimeOffset.ParseExact(
        receiver.GetField(ValueFieldName).AsString(), NamedTypeRegistry.RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    /// <summary>The canonical ISO-8601 string (registered <c>toString()</c>, <c>toIsoDateTime()</c>).</summary>
    internal static string IsoDateTimeString(GrobStruct receiver) {
        DateTimeOffset value = ToDateTimeOffset(receiver);
        string format = value.Offset == TimeSpan.Zero ? IsoUtcFormat : IsoOffsetFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
