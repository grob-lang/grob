using System.Globalization;

using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Construction, canonical-string and instant conversion for the <c>date</c> runtime
/// representation. Instance property/method dispatch lives in <c>Grob.Stdlib</c>'s
/// <c>DatePlugin</c>, which registers the <c>date</c> named type into the catalogue the
/// VM reads (D-356, D-426 Decision 3) — this class holds only the field-layout
/// accessors, the one <c>Grob.Vm.Tests</c> constructs fixture values through, and
/// <see cref="ToDateTimeOffset"/>, which <see cref="OpCode.LessDate"/>/
/// <see cref="OpCode.GreaterDate"/> still call directly (D-357's instant-based comparison
/// is intrinsic to the VM and reads the hidden field itself; D-367). A <c>date</c> value
/// is a <see cref="GrobStruct"/> with <see cref="DateLayout.TypeName"/> and exactly one field,
/// <see cref="DateLayout.ValueFieldName"/>, holding a round-trip-formatted <see cref="DateTimeOffset"/>
/// string (D-303's "boxed <see cref="DateTimeOffset"/>" realised as a hidden field, since
/// <see cref="GrobStruct"/> can only ever hold named <see cref="GrobValue"/> fields — the
/// same realisation <see cref="GuidNatives"/> uses for a boxed <see cref="Guid"/>). The
/// layout is defined once, in <c>Grob.Core</c>'s <see cref="DateLayout"/>, which the VM
/// and the plugin both read (R-19).
/// </summary>
internal static class DateNatives {
    // The canonical ISO-8601 rendering the VM tests build expected output from. Unlike
    // DateTime, DateTimeOffset's 'K' specifier is equivalent to 'zzz' — it never renders
    // "Z" for a zero offset, always "+00:00" — so the zero-offset case is handled
    // explicitly below rather than relying on 'K'.
    private const string IsoOffsetFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string IsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";

    /// <summary>Constructs the runtime <c>date</c> value for <paramref name="value"/>.</summary>
    internal static GrobValue FromDateTimeOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        DateLayout.TypeName,
        [new KeyValuePair<string, GrobValue>(
            DateLayout.ValueFieldName, GrobValue.FromString(value.ToString(DateLayout.RoundTripFormat, CultureInfo.InvariantCulture)))]));

    /// <summary>The <see cref="DateTimeOffset"/> <paramref name="receiver"/> stores.</summary>
    internal static DateTimeOffset ToDateTimeOffset(GrobStruct receiver) => DateTimeOffset.ParseExact(
        receiver.GetField(DateLayout.ValueFieldName).AsString(), DateLayout.RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    /// <summary>The canonical ISO-8601 string (the plugin's registered renderer and <c>toIsoDateTime()</c> produce the same form).</summary>
    internal static string IsoDateTimeString(GrobStruct receiver) {
        DateTimeOffset value = ToDateTimeOffset(receiver);
        string format = value.Offset == TimeSpan.Zero ? IsoUtcFormat : IsoOffsetFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
