using System.Globalization;

using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Factory and property/method accessors for the <c>date</c> instance surface (Sprint 9
/// Increment B, D-354/D-355): the nine properties (read directly by
/// <see cref="OpCode.GetProperty"/>) and the method surface (bound at
/// <see cref="OpCode.GetProperty"/> dispatch time, exactly as
/// <see cref="GuidNatives.GetMethod"/> binds guid's instance methods). A <c>date</c>
/// value is a <see cref="GrobStruct"/> with <c>TypeName</c> <c>"date"</c> and exactly one
/// field, <see cref="ValueFieldName"/>, holding a round-trip-formatted
/// <see cref="DateTimeOffset"/> string (D-303's "boxed <see cref="DateTimeOffset"/>"
/// realised as a hidden field, since <see cref="GrobStruct"/> can only ever hold named
/// <see cref="GrobValue"/> fields — the same realisation <see cref="GuidNatives"/> uses
/// for a boxed <see cref="Guid"/>). Own copy of the field/type-name constants — DAG
/// siblings, <c>Grob.Vm</c> and <c>Grob.Stdlib</c> share no code — must stay in lockstep
/// with <c>Grob.Stdlib.DatePlugin</c>'s equivalents.
/// </summary>
internal static class DateNatives {
    /// <summary>The hidden field name storing a <c>date</c> value's round-trip string form.</summary>
    internal const string ValueFieldName = "__value";

    /// <summary>The struct type name every <c>date</c> value carries.</summary>
    internal const string TypeName = "date";

    // Round-trip format: preserves the offset exactly (unlike "o", which also carries
    // fractional-second precision this type does not model) and is unambiguous to parse
    // back with DateTimeStyles.RoundtripKind.
    private const string RoundTripFormat = "yyyy-MM-ddTHH:mm:sszzz";

    // The canonical ISO-8601 rendering (ValueDisplay's registered toString(), toIso(),
    // toIsoDateTime()). Unlike DateTime, DateTimeOffset's 'K' specifier is equivalent to
    // 'zzz' — it never renders "Z" for a zero offset, always "+00:00" — so the zero-offset
    // case is handled explicitly below rather than relying on 'K'.
    private const string IsoOffsetFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string IsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";
    private const string IsoDateOnlyFormat = "yyyy-MM-dd";

    private static readonly DateTimeOffset UnixEpoch = DateTimeOffset.UnixEpoch;

    /// <summary>Constructs the runtime <c>date</c> value for <paramref name="value"/>.</summary>
    internal static GrobValue FromDateTimeOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        TypeName,
        [new KeyValuePair<string, GrobValue>(
            ValueFieldName, GrobValue.FromString(value.ToString(RoundTripFormat, CultureInfo.InvariantCulture)))]));

    /// <summary>The <see cref="DateTimeOffset"/> <paramref name="receiver"/> stores.</summary>
    internal static DateTimeOffset ToDateTimeOffset(GrobStruct receiver) => DateTimeOffset.ParseExact(
        receiver.GetField(ValueFieldName).AsString(), RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    /// <summary>The canonical ISO-8601 string (registered <c>toString()</c>, <c>toIsoDateTime()</c>).</summary>
    internal static string IsoDateTimeString(GrobStruct receiver) {
        DateTimeOffset value = ToDateTimeOffset(receiver);
        string format = value.Offset == TimeSpan.Zero ? IsoUtcFormat : IsoOffsetFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>The date-only ISO-8601 string (<c>toIso()</c>) — <c>"yyyy-MM-dd"</c>.</summary>
    private static string IsoDateString(GrobStruct receiver) =>
        ToDateTimeOffset(receiver).ToString(IsoDateOnlyFormat, CultureInfo.InvariantCulture);

    // -----------------------------------------------------------------------
    // Properties — read directly by OpCode.GetProperty.
    // -----------------------------------------------------------------------

    internal static GrobValue GetYear(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).Year);
    internal static GrobValue GetMonth(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).Month);
    internal static GrobValue GetDay(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).Day);
    internal static GrobValue GetHour(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).Hour);
    internal static GrobValue GetMinute(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).Minute);
    internal static GrobValue GetSecond(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).Second);
    internal static GrobValue GetDayOfYear(GrobStruct r) => GrobValue.FromInt(ToDateTimeOffset(r).DayOfYear);
    internal static GrobValue GetDayOfWeek(GrobStruct r) => GrobValue.FromString(ToDateTimeOffset(r).DayOfWeek.ToString());

    /// <summary><c>d.utcOffset</c> — the offset from UTC, in minutes.</summary>
    internal static GrobValue GetUtcOffset(GrobStruct r) =>
        GrobValue.FromInt((long)ToDateTimeOffset(r).Offset.TotalMinutes);

    /// <summary>
    /// Returns the bound <see cref="NativeFunction"/> for <paramref name="methodName"/> on
    /// <paramref name="receiver"/>, or <see langword="null"/> when the name is not a
    /// <c>date</c> instance method. Unlike <see cref="GuidNatives.GetMethod"/>'s all
    /// zero-arity methods, most of these bind a real arity and read positional arguments.
    /// </summary>
    internal static NativeFunction? GetMethod(string methodName, GrobStruct receiver) =>
        methodName switch {
            "addDays" => new NativeFunction("addDays", 1,
                (args, _) => FromDateTimeOffset(ToDateTimeOffset(receiver).AddDays(args[0].AsInt()))),
            // checked: CodeRabbit review, PR #143 — see DatePlugin.date.of's identical
            // rationale (an unchecked long-to-int narrowing could silently wrap into a
            // wrong but valid-looking month count).
            "addMonths" => new NativeFunction("addMonths", 1,
                (args, _) => FromDateTimeOffset(ToDateTimeOffset(receiver).AddMonths(checked((int)args[0].AsInt())))),
            "addHours" => new NativeFunction("addHours", 1,
                (args, _) => FromDateTimeOffset(ToDateTimeOffset(receiver).AddHours(args[0].AsInt()))),
            "addMinutes" => new NativeFunction("addMinutes", 1,
                (args, _) => FromDateTimeOffset(ToDateTimeOffset(receiver).AddMinutes(args[0].AsInt()))),
            "isBefore" => new NativeFunction("isBefore", 1,
                (args, _) => GrobValue.FromBool(ToDateTimeOffset(receiver) < ToDateTimeOffset(args[0].AsStruct()))),
            "isAfter" => new NativeFunction("isAfter", 1,
                (args, _) => GrobValue.FromBool(ToDateTimeOffset(receiver) > ToDateTimeOffset(args[0].AsStruct()))),
            "toIso" => new NativeFunction("toIso", 0,
                (_, _) => GrobValue.FromString(IsoDateString(receiver))),
            "toIsoDateTime" => new NativeFunction("toIsoDateTime", 0,
                (_, _) => GrobValue.FromString(IsoDateTimeString(receiver))),
            "format" => new NativeFunction("format", 1,
                (args, _) => GrobValue.FromString(
                    ToDateTimeOffset(receiver).ToString(args[0].AsString(), CultureInfo.InvariantCulture))),
            "toUnixSeconds" => new NativeFunction("toUnixSeconds", 0,
                (_, _) => GrobValue.FromInt(ToDateTimeOffset(receiver).ToUnixTimeSeconds())),
            "toUnixMillis" => new NativeFunction("toUnixMillis", 0,
                (_, _) => GrobValue.FromInt(ToDateTimeOffset(receiver).ToUnixTimeMilliseconds())),
            "toUtc" => new NativeFunction("toUtc", 0,
                (_, _) => FromDateTimeOffset(ToDateTimeOffset(receiver).ToUniversalTime())),
            "toLocal" => new NativeFunction("toLocal", 0,
                (_, _) => FromDateTimeOffset(ToDateTimeOffset(receiver).ToLocalTime())),
            "toZone" => new NativeFunction("toZone", 1,
                (args, _) => FromDateTimeOffset(ToZone(ToDateTimeOffset(receiver), args[0].AsString()))),
            "toDateOnly" => new NativeFunction("toDateOnly", 0,
                (_, _) => FromDateTimeOffset(ToDateOnly(ToDateTimeOffset(receiver)))),
            "toTimeOnly" => new NativeFunction("toTimeOnly", 0,
                (_, _) => FromDateTimeOffset(ToTimeOnly(ToDateTimeOffset(receiver)))),
            "daysUntil" => new NativeFunction("daysUntil", 1,
                (args, _) => GrobValue.FromInt(DaysBetween(ToDateTimeOffset(receiver), ToDateTimeOffset(args[0].AsStruct())))),
            "daysSince" => new NativeFunction("daysSince", 1,
                (args, _) => GrobValue.FromInt(DaysBetween(ToDateTimeOffset(args[0].AsStruct()), ToDateTimeOffset(receiver)))),
            _ => null,
        };

    /// <summary><c>d.toZone(zone)</c> — converts to the IANA/Windows time zone named <paramref name="zoneId"/>.</summary>
    private static DateTimeOffset ToZone(DateTimeOffset value, string zoneId) {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return TimeZoneInfo.ConvertTime(value, zone);
    }

    /// <summary><c>d.toDateOnly()</c> — hour/minute/second zeroed, year/month/day and offset kept.</summary>
    private static DateTimeOffset ToDateOnly(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

    /// <summary>
    /// <c>d.toTimeOnly()</c> — hour/minute/second kept, date anchored to the Unix epoch
    /// (1970-01-01, D-354) rather than left undefined ("zero day" has no meaning in the
    /// Gregorian calendar).
    /// </summary>
    private static DateTimeOffset ToTimeOnly(DateTimeOffset value) =>
        new(UnixEpoch.Year, UnixEpoch.Month, UnixEpoch.Day, value.Hour, value.Minute, value.Second, value.Offset);

    /// <summary>Whole days from <paramref name="from"/> to <paramref name="to"/> — negative if reversed (D-117).</summary>
    private static long DaysBetween(DateTimeOffset from, DateTimeOffset to) => (long)(to - from).TotalDays;
}
