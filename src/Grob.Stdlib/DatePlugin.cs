using System.Globalization;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>date</c> module (D-107/D-108/D-354/D-355 — Sprint 9 Increment B): the static
/// constructors (<c>now</c>/<c>today</c> on <see cref="IClock"/>, <c>of</c>/<c>ofTime</c>,
/// <c>parse</c> throwing <c>ParseError</c> through the native-throw seam,
/// <c>fromUnixSeconds</c>/<c>fromUnixMillis</c>) and, since D-426 Decision 3, the
/// <c>date</c> named type itself: its nine instance properties, eighteen instance methods
/// and the renderer that makes <c>ValueDisplay</c> (D-336) print the canonical ISO-8601
/// form. The type checker reads the member signatures and the VM dispatches the bindings
/// from the catalogue this registration builds. A <c>date</c> runtime value is a
/// <see cref="GrobStruct"/> named <c>"date"</c> with exactly one hidden field
/// (<see cref="ValueFieldName"/>) holding a <see cref="DateTimeOffset"/> formatted with
/// <see cref="DateLayout.RoundTripFormat"/>. <c>Grob.Vm.DateNatives</c> reads the same
/// layout for the comparison opcodes (D-367) and cannot share this code (DAG siblings), so
/// <see cref="ValueFieldName"/>/<see cref="TypeName"/> stay in lockstep with its
/// constants by inspection (R-19).
/// </summary>
public sealed class DatePlugin : IGrobPlugin {
    /// <summary>The hidden field name storing a <c>date</c> value's round-trip string form.</summary>
    internal const string ValueFieldName = "__value";

    /// <summary>The struct type name every <c>date</c> value carries.</summary>
    internal const string TypeName = "date";

    // The canonical ISO-8601 rendering (the registered renderer, toIso(), toIsoDateTime()).
    // Unlike DateTime, DateTimeOffset's 'K' specifier is equivalent to 'zzz' — it never
    // renders "Z" for a zero offset, always "+00:00" — so the zero-offset case is handled
    // explicitly in IsoDateTimeString rather than relying on 'K'.
    private const string IsoOffsetFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string IsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";
    private const string IsoDateOnlyFormat = "yyyy-MM-dd";

    private static readonly SignatureType.Concrete IntType = SignatureType.Of(GrobType.Int);
    private static readonly SignatureType.Concrete StringType = SignatureType.Of(GrobType.String);
    private static readonly SignatureType.Concrete BoolType = SignatureType.Of(GrobType.Bool);
    private static readonly SignatureType.Concrete DateType = SignatureType.Named(GrobType.Struct, TypeName);

    private readonly IClock _clock;

    /// <summary>Initialises the plugin with the <see cref="IClock"/> <c>now</c>/<c>today</c> read from.</summary>
    public DatePlugin(IClock clock) {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => "date";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("date.now", new NativeFunction("date.now", 0, (_, _) => FromDateTimeOffset(Now())));

        registrar.RegisterNative("date.today", new NativeFunction("date.today", 0, (_, _) => {
            // CodeRabbit review, PR #143: midnight's own UTC offset, not `now`'s — on a
            // day the local zone's offset changes (a DST transition), the two can differ.
            DateTimeOffset now = Now();
            var midnight = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return FromDateTimeOffset(new DateTimeOffset(midnight, TimeZoneInfo.Local.GetUtcOffset(midnight)));
        }));

        registrar.RegisterNative("date.of", new NativeFunction("date.of", 3, (args, _) => {
            // checked: CodeRabbit review, PR #143 — an unchecked long-to-int narrowing
            // could silently wrap an out-of-range component into an unrelated, wrong but
            // valid-looking date instead of failing. checked turns that into a
            // deterministic OverflowException rather than silently wrong data; a full
            // translation into a catchable Grob diagnostic is deferred to the Sprint
            // 9/10 adversarial hardening pass (D-353), which covers this systematically
            // across every Sprint 9 stdlib module, not date alone.
            var local = checked(new DateTime((int)args[0].AsInt(), (int)args[1].AsInt(), (int)args[2].AsInt(),
                0, 0, 0, DateTimeKind.Unspecified));
            return FromDateTimeOffset(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)));
        }));

        registrar.RegisterNative("date.ofTime", new NativeFunction("date.ofTime", 6, (args, _) => {
            // checked — see date.of above.
            var local = checked(new DateTime(
                (int)args[0].AsInt(), (int)args[1].AsInt(), (int)args[2].AsInt(),
                (int)args[3].AsInt(), (int)args[4].AsInt(), (int)args[5].AsInt(), DateTimeKind.Unspecified));
            return FromDateTimeOffset(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)));
        }));

        // D-358: pattern is the compiler-synthesised second argument — "" (the
        // pre-existing one-argument behaviour) selects ISO-8601 via TryParse; a
        // non-empty pattern selects DateTimeOffset.ParseExact.
        registrar.RegisterNative("date.parse", new NativeFunction("date.parse", 2, (args, _) => {
            string s = args[0].AsString();
            string pattern = args[1].AsString();
            DateTimeOffset parsed;
            if (pattern.Length == 0) {
                // D-176: a string with an explicit offset preserves it; one without is
                // interpreted as local time — DateTimeStyles.AssumeLocal gives exactly that.
                if (!DateTimeOffset.TryParse(
                        s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed)) {
                    throw new NativeFaultException(
                        "ParseError", ErrorCatalog.E5702.Code, $"date.parse: '{s}' is not a valid date.");
                }
            } else {
                if (!DateTimeOffset.TryParseExact(
                        s, pattern, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsed)) {
                    throw new NativeFaultException("ParseError", ErrorCatalog.E5702.Code,
                        $"date.parse: '{s}' does not match pattern '{pattern}'.");
                }
            }
            return FromDateTimeOffset(parsed);
        }));

        registrar.RegisterNative("date.fromUnixSeconds", new NativeFunction("date.fromUnixSeconds", 1,
            (args, _) => FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(args[0].AsInt()))));

        registrar.RegisterNative("date.fromUnixMillis", new NativeFunction("date.fromUnixMillis", 1,
            (args, _) => FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(args[0].AsInt()))));

        registrar.RegisterNamedType(BuildNamedType());
    }

    // -----------------------------------------------------------------------
    // The date instance surface (D-356, moved here from NamedTypeRegistry by D-426
    // Decision 3). Parameter names follow the increment's gate report §8; a parameter
    // or return of the type itself is the nominal DateType, which the checker matches
    // by name rather than by the Struct tag every named type shares.
    // -----------------------------------------------------------------------

    private static NamedTypeDefinition BuildNamedType() => new(
        TypeName,
        [
            IntProperty("year", d => d.Year),
            IntProperty("month", d => d.Month),
            IntProperty("day", d => d.Day),
            IntProperty("hour", d => d.Hour),
            IntProperty("minute", d => d.Minute),
            IntProperty("second", d => d.Second),
            IntProperty("dayOfYear", d => d.DayOfYear),
            new NamedTypePropertyBinding(new PropertySignature("dayOfWeek", StringType),
                r => GrobValue.FromString(ToDateTimeOffset(r).DayOfWeek.ToString())),
            IntProperty("utcOffset", d => (long)d.Offset.TotalMinutes),
        ],
        [
            Method("addDays", [new SignatureParameter("days", IntType)], DateType,
                (d, args) => FromDateTimeOffset(d.AddDays(args[0].AsInt()))),
            // checked: mirrors the original DateNatives.GetMethod arm (CodeRabbit review,
            // PR #143) — an unchecked long-to-int narrowing could silently wrap into a
            // wrong but valid-looking month count.
            Method("addMonths", [new SignatureParameter("months", IntType)], DateType,
                (d, args) => FromDateTimeOffset(d.AddMonths(checked((int)args[0].AsInt())))),
            Method("addHours", [new SignatureParameter("hours", IntType)], DateType,
                (d, args) => FromDateTimeOffset(d.AddHours(args[0].AsInt()))),
            Method("addMinutes", [new SignatureParameter("minutes", IntType)], DateType,
                (d, args) => FromDateTimeOffset(d.AddMinutes(args[0].AsInt()))),
            Method("isBefore", [new SignatureParameter("other", DateType)], BoolType,
                (d, args) => GrobValue.FromBool(d < ToDateTimeOffset(args[0].AsStruct()))),
            Method("isAfter", [new SignatureParameter("other", DateType)], BoolType,
                (d, args) => GrobValue.FromBool(d > ToDateTimeOffset(args[0].AsStruct()))),
            Method("toIso", [], StringType,
                (d, _) => GrobValue.FromString(d.ToString(IsoDateOnlyFormat, CultureInfo.InvariantCulture))),
            Method("toIsoDateTime", [], StringType, (d, _) => GrobValue.FromString(IsoDateTimeString(d))),
            Method("format", [new SignatureParameter("pattern", StringType)], StringType,
                (d, args) => GrobValue.FromString(d.ToString(args[0].AsString(), CultureInfo.InvariantCulture))),
            Method("toUnixSeconds", [], IntType, (d, _) => GrobValue.FromInt(d.ToUnixTimeSeconds())),
            Method("toUnixMillis", [], IntType, (d, _) => GrobValue.FromInt(d.ToUnixTimeMilliseconds())),
            Method("toUtc", [], DateType, (d, _) => FromDateTimeOffset(d.ToUniversalTime())),
            Method("toLocal", [], DateType, (d, _) => FromDateTimeOffset(d.ToLocalTime())),
            Method("toZone", [new SignatureParameter("zone", StringType)], DateType,
                (d, args) => FromDateTimeOffset(TimeZoneInfo.ConvertTime(d, TimeZoneInfo.FindSystemTimeZoneById(args[0].AsString())))),
            Method("toDateOnly", [], DateType,
                (d, _) => FromDateTimeOffset(new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, d.Offset))),
            Method("toTimeOnly", [], DateType, (d, _) => FromDateTimeOffset(new DateTimeOffset(
                DateTimeOffset.UnixEpoch.Year, DateTimeOffset.UnixEpoch.Month, DateTimeOffset.UnixEpoch.Day,
                d.Hour, d.Minute, d.Second, d.Offset))),
            Method("daysUntil", [new SignatureParameter("other", DateType)], IntType,
                (d, args) => GrobValue.FromInt(DaysBetween(d, ToDateTimeOffset(args[0].AsStruct())))),
            Method("daysSince", [new SignatureParameter("other", DateType)], IntType,
                (d, args) => GrobValue.FromInt(DaysBetween(ToDateTimeOffset(args[0].AsStruct()), d))),
        ],
        v => IsoDateTimeString(ToDateTimeOffset(v.AsStruct())));

    private static NamedTypePropertyBinding IntProperty(string name, Func<DateTimeOffset, long> read) =>
        new(new PropertySignature(name, IntType), r => GrobValue.FromInt(read(ToDateTimeOffset(r))));

    /// <summary>
    /// A method binding whose body sees the receiver's instant. The receiver is parsed
    /// when the bound function runs, as the former registry's binders did.
    /// </summary>
    private static NamedTypeMethodBinding Method(
            string name, SignatureParameter[] parameters, SignatureType.Concrete returnType,
            Func<DateTimeOffset, GrobValue[], GrobValue> body) =>
        new(new FunctionSignature(name, parameters, returnType),
            receiver => new NativeFunction(name, parameters.Length, (args, _) => body(ToDateTimeOffset(receiver), args)));

    // -----------------------------------------------------------------------
    // Runtime representation — a GrobStruct with one hidden field. Must stay in
    // lockstep with Grob.Vm.DateNatives (DAG siblings; no shared code possible).
    // -----------------------------------------------------------------------

    /// <summary>The current instant, converted from <see cref="IClock"/>'s UTC reading to local time (D-176).</summary>
    private DateTimeOffset Now() => new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToLocalTime();

    private static GrobValue FromDateTimeOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        TypeName,
        [new KeyValuePair<string, GrobValue>(
            ValueFieldName, GrobValue.FromString(value.ToString(DateLayout.RoundTripFormat, CultureInfo.InvariantCulture)))]));

    private static DateTimeOffset ToDateTimeOffset(GrobStruct receiver) => DateTimeOffset.ParseExact(
        receiver.GetField(ValueFieldName).AsString(), DateLayout.RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static string IsoDateTimeString(DateTimeOffset value) {
        string format = value.Offset == TimeSpan.Zero ? IsoUtcFormat : IsoOffsetFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static long DaysBetween(DateTimeOffset from, DateTimeOffset to) => (long)(to - from).TotalDays;
}
