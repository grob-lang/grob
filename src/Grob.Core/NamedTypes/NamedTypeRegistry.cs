using System.Globalization;

namespace Grob.Core.NamedTypes;

/// <summary>
/// The compile-time-and-runtime-shared instance-surface table for every nominal,
/// <see cref="GrobValueKind.Struct"/>-discriminated type (D-356). Hand-authored, one
/// entry per type, consulted by <c>Grob.Compiler</c>'s annotation resolvers and
/// method/property validators, <c>Grob.Vm</c>'s <c>OpCode.GetProperty</c> instance
/// dispatch, and (via each plugin's <c>Register</c>) <c>ValueDisplay</c>'s
/// <c>toString()</c> lookup — replacing six previously hand-rolled, string-matched
/// dispatch arms per type. Lives in <c>Grob.Core</c> because it is the only assembly
/// all three consumers reference without creating a <c>Grob.Compiler</c>&lt;-&gt;
/// <c>Grob.Vm</c> edge (the same reasoning <see cref="NativeFunction"/> documents).
/// <c>guid</c> and <c>date</c> are the first two entries, migrated behaviour-preserving
/// from the former <c>Grob.Vm.GuidNatives</c>/<c>DateNatives</c> arms; <c>File</c> through
/// <c>ProcessResult</c> land as further entries in later increments. Governs the
/// instance surface only — static constructors (<c>date.now()</c>, <c>guid.parse()</c>)
/// remain <c>NamespaceRegistry</c> (<c>Grob.Compiler</c>) entries; the two registries
/// compose.
/// </summary>
public static class NamedTypeRegistry {
    private const string GuidValueFieldName = "__value";
    private const string DateValueFieldName = "__value";

    // Matches Grob.Vm.DateNatives's former RoundTripFormat / Grob.Stdlib.DatePlugin's
    // own copy — the hidden field's round-trip storage format. Neither of those two
    // remaining copies (construction only, out of this increment's instance-surface
    // scope) can reference this one; all three must stay in lockstep by inspection.
    private const string DateRoundTripFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string DateIsoOffsetFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string DateIsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";
    private const string DateIsoDateOnlyFormat = "yyyy-MM-dd";

    private static readonly DateTimeOffset DateUnixEpoch = DateTimeOffset.UnixEpoch;

    /// <summary>The <c>guid</c> instance-surface entry (Sprint 8 Increment D).</summary>
    public static NamedTypeEntry Guid { get; } = BuildGuidEntry();

    /// <summary>The <c>date</c> instance-surface entry (Sprint 9 Increment B, D-354/D-355).</summary>
    public static NamedTypeEntry Date { get; } = BuildDateEntry();

    // Declared after Guid/Date so their static-initializer values are already set —
    // C# initializes static fields/auto-properties in textual declaration order.
    private static readonly Dictionary<string, NamedTypeEntry> _entries =
        new(StringComparer.Ordinal) { [Guid.CanonicalName] = Guid, [Date.CanonicalName] = Date };

    /// <summary>Every registered canonical type name, in registration order.</summary>
    public static IReadOnlyList<string> Names { get; } = ["guid", "date"];

    /// <summary>
    /// Looks up the registered entry for <paramref name="canonicalName"/>. Returns
    /// <c>false</c> when the name is not a registered nominal type.
    /// </summary>
    public static bool TryGet(string canonicalName, out NamedTypeEntry entry) =>
        _entries.TryGetValue(canonicalName, out entry!);

    // -----------------------------------------------------------------------
    // guid — reproduces Grob.Vm.GuidNatives exactly.
    // -----------------------------------------------------------------------

    private static NamedTypeEntry BuildGuidEntry() {
        Dictionary<string, NamedTypeProperty> properties = new(StringComparer.Ordinal) {
            ["version"] = new NamedTypeProperty("version", GrobType.Int,
                r => GrobValue.FromInt(GuidToGuid(r).Version)),
            ["isEmpty"] = new NamedTypeProperty("isEmpty", GrobType.Bool,
                r => GrobValue.FromBool(GuidToGuid(r) == System.Guid.Empty)),
        };

        Dictionary<string, NamedTypeMethod> methods = new(StringComparer.Ordinal) {
            ["toString"] = new NamedTypeMethod("toString", [], GrobType.String, false,
                r => new NativeFunction("toString", 0, (_, _) => GrobValue.FromString(GuidCanonicalString(r)))),
            ["toUpperString"] = new NamedTypeMethod("toUpperString", [], GrobType.String, false,
                r => new NativeFunction("toUpperString", 0,
                    (_, _) => GrobValue.FromString(GuidCanonicalString(r).ToUpperInvariant()))),
            ["toCompactString"] = new NamedTypeMethod("toCompactString", [], GrobType.String, false,
                r => new NativeFunction("toCompactString", 0,
                    (_, _) => GrobValue.FromString(GuidToGuid(r).ToString("N")))),
        };

        return new NamedTypeEntry("guid", properties, methods, v => GuidCanonicalString(v.AsStruct()));
    }

    private static string GuidCanonicalString(GrobStruct receiver) => receiver.GetField(GuidValueFieldName).AsString();

    private static System.Guid GuidToGuid(GrobStruct receiver) => System.Guid.Parse(GuidCanonicalString(receiver));

    // -----------------------------------------------------------------------
    // date — reproduces Grob.Vm.DateNatives exactly.
    // -----------------------------------------------------------------------

    private static NamedTypeEntry BuildDateEntry() {
        Dictionary<string, NamedTypeProperty> properties = new(StringComparer.Ordinal) {
            ["year"] = new NamedTypeProperty("year", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).Year)),
            ["month"] = new NamedTypeProperty("month", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).Month)),
            ["day"] = new NamedTypeProperty("day", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).Day)),
            ["hour"] = new NamedTypeProperty("hour", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).Hour)),
            ["minute"] = new NamedTypeProperty("minute", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).Minute)),
            ["second"] = new NamedTypeProperty("second", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).Second)),
            ["dayOfYear"] = new NamedTypeProperty("dayOfYear", GrobType.Int, r => GrobValue.FromInt(DateToOffset(r).DayOfYear)),
            ["dayOfWeek"] = new NamedTypeProperty("dayOfWeek", GrobType.String,
                r => GrobValue.FromString(DateToOffset(r).DayOfWeek.ToString())),
            ["utcOffset"] = new NamedTypeProperty("utcOffset", GrobType.Int,
                r => GrobValue.FromInt((long)DateToOffset(r).Offset.TotalMinutes)),
        };

        NamedTypeParameter intParam = new(GrobType.Int);
        NamedTypeParameter stringParam = new(GrobType.String);
        NamedTypeParameter selfParam = new(GrobType.Struct, NamedTypeParameterKind.NominalSelf);

        Dictionary<string, NamedTypeMethod> methods = new(StringComparer.Ordinal) {
            ["addDays"] = new NamedTypeMethod("addDays", [intParam], GrobType.Struct, true,
                r => new NativeFunction("addDays", 1,
                    (args, _) => DateFromOffset(DateToOffset(r).AddDays(args[0].AsInt())))),
            // checked: mirrors the original DateNatives.GetMethod arm (CodeRabbit review,
            // PR #143) — an unchecked long-to-int narrowing could silently wrap into a
            // wrong but valid-looking month count.
            ["addMonths"] = new NamedTypeMethod("addMonths", [intParam], GrobType.Struct, true,
                r => new NativeFunction("addMonths", 1,
                    (args, _) => DateFromOffset(DateToOffset(r).AddMonths(checked((int)args[0].AsInt()))))),
            ["addHours"] = new NamedTypeMethod("addHours", [intParam], GrobType.Struct, true,
                r => new NativeFunction("addHours", 1,
                    (args, _) => DateFromOffset(DateToOffset(r).AddHours(args[0].AsInt())))),
            ["addMinutes"] = new NamedTypeMethod("addMinutes", [intParam], GrobType.Struct, true,
                r => new NativeFunction("addMinutes", 1,
                    (args, _) => DateFromOffset(DateToOffset(r).AddMinutes(args[0].AsInt())))),
            ["isBefore"] = new NamedTypeMethod("isBefore", [selfParam], GrobType.Bool, false,
                r => new NativeFunction("isBefore", 1,
                    (args, _) => GrobValue.FromBool(DateToOffset(r) < DateToOffset(args[0].AsStruct())))),
            ["isAfter"] = new NamedTypeMethod("isAfter", [selfParam], GrobType.Bool, false,
                r => new NativeFunction("isAfter", 1,
                    (args, _) => GrobValue.FromBool(DateToOffset(r) > DateToOffset(args[0].AsStruct())))),
            ["toIso"] = new NamedTypeMethod("toIso", [], GrobType.String, false,
                r => new NativeFunction("toIso", 0, (_, _) => GrobValue.FromString(DateIsoDateString(r)))),
            ["toIsoDateTime"] = new NamedTypeMethod("toIsoDateTime", [], GrobType.String, false,
                r => new NativeFunction("toIsoDateTime", 0, (_, _) => GrobValue.FromString(DateIsoDateTimeString(r)))),
            ["format"] = new NamedTypeMethod("format", [stringParam], GrobType.String, false,
                r => new NativeFunction("format", 1,
                    (args, _) => GrobValue.FromString(DateToOffset(r).ToString(args[0].AsString(), CultureInfo.InvariantCulture)))),
            ["toUnixSeconds"] = new NamedTypeMethod("toUnixSeconds", [], GrobType.Int, false,
                r => new NativeFunction("toUnixSeconds", 0, (_, _) => GrobValue.FromInt(DateToOffset(r).ToUnixTimeSeconds()))),
            ["toUnixMillis"] = new NamedTypeMethod("toUnixMillis", [], GrobType.Int, false,
                r => new NativeFunction("toUnixMillis", 0, (_, _) => GrobValue.FromInt(DateToOffset(r).ToUnixTimeMilliseconds()))),
            ["toUtc"] = new NamedTypeMethod("toUtc", [], GrobType.Struct, true,
                r => new NativeFunction("toUtc", 0, (_, _) => DateFromOffset(DateToOffset(r).ToUniversalTime()))),
            ["toLocal"] = new NamedTypeMethod("toLocal", [], GrobType.Struct, true,
                r => new NativeFunction("toLocal", 0, (_, _) => DateFromOffset(DateToOffset(r).ToLocalTime()))),
            ["toZone"] = new NamedTypeMethod("toZone", [stringParam], GrobType.Struct, true,
                r => new NativeFunction("toZone", 1,
                    (args, _) => DateFromOffset(DateToZone(DateToOffset(r), args[0].AsString())))),
            ["toDateOnly"] = new NamedTypeMethod("toDateOnly", [], GrobType.Struct, true,
                r => new NativeFunction("toDateOnly", 0, (_, _) => DateFromOffset(DateToDateOnly(DateToOffset(r))))),
            ["toTimeOnly"] = new NamedTypeMethod("toTimeOnly", [], GrobType.Struct, true,
                r => new NativeFunction("toTimeOnly", 0, (_, _) => DateFromOffset(DateToTimeOnly(DateToOffset(r))))),
            ["daysUntil"] = new NamedTypeMethod("daysUntil", [selfParam], GrobType.Int, false,
                r => new NativeFunction("daysUntil", 1,
                    (args, _) => GrobValue.FromInt(DateDaysBetween(DateToOffset(r), DateToOffset(args[0].AsStruct()))))),
            ["daysSince"] = new NamedTypeMethod("daysSince", [selfParam], GrobType.Int, false,
                r => new NativeFunction("daysSince", 1,
                    (args, _) => GrobValue.FromInt(DateDaysBetween(DateToOffset(args[0].AsStruct()), DateToOffset(r))))),
        };

        return new NamedTypeEntry("date", properties, methods, v => DateIsoDateTimeString(v.AsStruct()));
    }

    private static GrobValue DateFromOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        "date",
        [new KeyValuePair<string, GrobValue>(
            DateValueFieldName, GrobValue.FromString(value.ToString(DateRoundTripFormat, CultureInfo.InvariantCulture)))]));

    private static DateTimeOffset DateToOffset(GrobStruct receiver) => DateTimeOffset.ParseExact(
        receiver.GetField(DateValueFieldName).AsString(), DateRoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static string DateIsoDateTimeString(GrobStruct receiver) {
        DateTimeOffset value = DateToOffset(receiver);
        string format = value.Offset == TimeSpan.Zero ? DateIsoUtcFormat : DateIsoOffsetFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static string DateIsoDateString(GrobStruct receiver) =>
        DateToOffset(receiver).ToString(DateIsoDateOnlyFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset DateToZone(DateTimeOffset value, string zoneId) {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return TimeZoneInfo.ConvertTime(value, zone);
    }

    private static DateTimeOffset DateToDateOnly(DateTimeOffset value) =>
        new(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);

    private static DateTimeOffset DateToTimeOnly(DateTimeOffset value) =>
        new(DateUnixEpoch.Year, DateUnixEpoch.Month, DateUnixEpoch.Day, value.Hour, value.Minute, value.Second, value.Offset);

    private static long DateDaysBetween(DateTimeOffset from, DateTimeOffset to) => (long)(to - from).TotalDays;
}
