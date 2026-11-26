using System.Globalization;

using Grob.Core;
using Grob.Runtime;

using static Grob.Stdlib.Signatures;

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
/// <see cref="GrobStruct"/> laid out as <see cref="DateLayout"/> defines it, the one
/// definition <c>Grob.Vm.DateNatives</c> also reads for the comparison opcodes (D-367).
/// </summary>
public sealed class DatePlugin : IGrobPlugin {
    // The canonical ISO-8601 rendering (the registered renderer, toIso(), toIsoDateTime()).
    // Unlike DateTime, DateTimeOffset's 'K' specifier is equivalent to 'zzz' — it never
    // renders "Z" for a zero offset, always "+00:00" — so the zero-offset case is handled
    // explicitly in IsoDateTimeString rather than relying on 'K'.
    private const string IsoOffsetFormat = "yyyy-MM-ddTHH:mm:sszzz";
    private const string IsoUtcFormat = "yyyy-MM-ddTHH:mm:ss'Z'";
    private const string IsoDateOnlyFormat = "yyyy-MM-dd";

    /// <summary>The parameter name the four date-comparison methods share (Sonar S1192: one spelling).</summary>
    private const string OtherParameter = "other";

    private static readonly SignatureType.Concrete DateType = SignatureType.Named(GrobType.Struct, DateLayout.TypeName);

    private readonly IClock _clock;

    /// <summary>
    /// Initialises the plugin with the <see cref="IClock"/> whose instant <c>now</c>/<c>today</c>
    /// read and whose local zone every "local" conversion resolves against (R-32).
    /// </summary>
    public DatePlugin(IClock clock) {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Name => "date";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative(Signature("date.now", DateType),
            new NativeFunction("date.now", 0, (_, _) => FromDateTimeOffset(Now())));

        registrar.RegisterNative(Signature("date.today", DateType), new NativeFunction("date.today", 0, (_, _) => {
            // CodeRabbit review, PR #143: midnight's own UTC offset, not `now`'s — on a
            // day the local zone's offset changes (a DST transition), the two can differ.
            DateTimeOffset now = Now();
            var midnight = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Unspecified);
            return FromDateTimeOffset(new DateTimeOffset(midnight, _clock.LocalZone.GetUtcOffset(midnight)));
        }));

        registrar.RegisterNative(Signature("date.of", DateType, Param("year", IntType), Param("month", IntType), Param("day", IntType)),
            new NativeFunction("date.of", 3, (args, _) => {
                // checked: CodeRabbit review, PR #143 — an unchecked long-to-int narrowing
                // could silently wrap an out-of-range component into an unrelated, wrong but
                // valid-looking date instead of failing. checked turns that into a
                // deterministic OverflowException rather than silently wrong data; a full
                // translation into a catchable Grob diagnostic is deferred to the Sprint
                // 9/10 adversarial hardening pass (D-353), which covers this systematically
                // across every Sprint 9 stdlib module, not date alone.
                var local = checked(new DateTime((int)args[0].AsInt(), (int)args[1].AsInt(), (int)args[2].AsInt(),
                    0, 0, 0, DateTimeKind.Unspecified));
                return FromDateTimeOffset(new DateTimeOffset(local, _clock.LocalZone.GetUtcOffset(local)));
            }));

        registrar.RegisterNative(Signature("date.ofTime", DateType,
                Param("year", IntType), Param("month", IntType), Param("day", IntType),
                Param("hour", IntType), Param("minute", IntType), Param("second", IntType)),
            new NativeFunction("date.ofTime", 6, (args, _) => {
                // checked — see date.of above.
                var local = checked(new DateTime(
                    (int)args[0].AsInt(), (int)args[1].AsInt(), (int)args[2].AsInt(),
                    (int)args[3].AsInt(), (int)args[4].AsInt(), (int)args[5].AsInt(), DateTimeKind.Unspecified));
                return FromDateTimeOffset(new DateTimeOffset(local, _clock.LocalZone.GetUtcOffset(local)));
            }));

        // D-358: pattern is the compiler-synthesised second argument — "" (the
        // pre-existing one-argument behaviour) selects ISO-8601; a non-empty pattern
        // selects an exact parse. D-176: a string with an explicit offset preserves it; one
        // without is local time, which since R-32 means the clock's zone (DateParse).
        registrar.RegisterNative(Signature("date.parse", DateType,
                Param("value", StringType), Optional("pattern", StringType, GrobValue.FromString(""))),
            new NativeFunction("date.parse", 2, (args, _) => {
                string s = args[0].AsString();
                string pattern = args[1].AsString();
                DateTimeOffset parsed;
                if (pattern.Length == 0) {
                    if (!DateParse.TryParseIso(s, _clock.UtcNow, _clock.LocalZone, out parsed)) {
                        throw new NativeFaultException(
                            "ParseError", ErrorCatalog.E5702.Code, $"date.parse: '{s}' is not a valid date.");
                    }
                } else if (!DateParse.TryParsePattern(s, pattern, _clock.UtcNow, _clock.LocalZone, out parsed)) {
                    throw new NativeFaultException("ParseError", ErrorCatalog.E5702.Code,
                        $"date.parse: '{s}' does not match pattern '{pattern}'.");
                }
                return FromDateTimeOffset(parsed);
            }));

        registrar.RegisterNative(Signature("date.fromUnixSeconds", DateType, Param("seconds", IntType)),
            new NativeFunction("date.fromUnixSeconds", 1,
                (args, _) => FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(args[0].AsInt()))));

        registrar.RegisterNative(Signature("date.fromUnixMillis", DateType, Param("millis", IntType)),
            new NativeFunction("date.fromUnixMillis", 1,
                (args, _) => FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(args[0].AsInt()))));

        registrar.RegisterNamedType(BuildNamedType());
    }

    // -----------------------------------------------------------------------
    // The date instance surface (D-356, moved here from NamedTypeRegistry by D-426
    // Decision 3). Parameter names follow the increment's gate report §8; a parameter
    // or return of the type itself is the nominal DateType, which the checker matches
    // by name rather than by the Struct tag every named type shares.
    // -----------------------------------------------------------------------

    private NamedTypeDefinition BuildNamedType() => new(
        DateLayout.TypeName,
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
            Method("isBefore", [new SignatureParameter(OtherParameter, DateType)], BoolType,
                (d, args) => GrobValue.FromBool(d < ToDateTimeOffset(args[0].AsStruct()))),
            Method("isAfter", [new SignatureParameter(OtherParameter, DateType)], BoolType,
                (d, args) => GrobValue.FromBool(d > ToDateTimeOffset(args[0].AsStruct()))),
            Method("toIso", [], StringType,
                (d, _) => GrobValue.FromString(d.ToString(IsoDateOnlyFormat, CultureInfo.InvariantCulture))),
            Method("toIsoDateTime", [], StringType, (d, _) => GrobValue.FromString(IsoDateTimeString(d))),
            Method("format", [new SignatureParameter("pattern", StringType)], StringType,
                (d, args) => GrobValue.FromString(d.ToString(args[0].AsString(), CultureInfo.InvariantCulture))),
            Method("toUnixSeconds", [], IntType, (d, _) => GrobValue.FromInt(d.ToUnixTimeSeconds())),
            Method("toUnixMillis", [], IntType, (d, _) => GrobValue.FromInt(d.ToUnixTimeMilliseconds())),
            Method("toUtc", [], DateType, (d, _) => FromDateTimeOffset(d.ToUniversalTime())),
            Method("toLocal", [], DateType, (d, _) => FromDateTimeOffset(TimeZoneInfo.ConvertTime(d, _clock.LocalZone))),
            Method("toZone", [new SignatureParameter("zone", StringType)], DateType,
                (d, args) => FromDateTimeOffset(TimeZoneInfo.ConvertTime(d, FindZone(args[0].AsString())))),
            Method("toDateOnly", [], DateType,
                (d, _) => FromDateTimeOffset(new DateTimeOffset(d.Year, d.Month, d.Day, 0, 0, 0, d.Offset))),
            Method("toTimeOnly", [], DateType, (d, _) => FromDateTimeOffset(new DateTimeOffset(
                DateTimeOffset.UnixEpoch.Year, DateTimeOffset.UnixEpoch.Month, DateTimeOffset.UnixEpoch.Day,
                d.Hour, d.Minute, d.Second, d.Offset))),
            Method("daysUntil", [new SignatureParameter(OtherParameter, DateType)], IntType,
                (d, args) => GrobValue.FromInt(DaysBetween(d, ToDateTimeOffset(args[0].AsStruct())))),
            Method("daysSince", [new SignatureParameter(OtherParameter, DateType)], IntType,
                (d, args) => GrobValue.FromInt(DaysBetween(ToDateTimeOffset(args[0].AsStruct()), d))),
        ],
        v => IsoDateTimeString(ToDateTimeOffset(v.AsStruct())));

    /// <summary>
    /// The zone <paramref name="id"/> names. An id the host does not know, or whose data it cannot
    /// read, is the script's bad input, a <c>ParseError</c> (E5702) as an unparseable date string
    /// is (R-69, D-459), not the host exception <see cref="TimeZoneInfo.FindSystemTimeZoneById"/>
    /// throws. An id the host refuses outright, an empty one or one with a null character, is
    /// refused the same way. The message names the function and the problem, never the id (D-077).
    /// </summary>
    private static TimeZoneInfo FindZone(string id) {
        try {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        } catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException) {
            throw new NativeFaultException(
                "ParseError", ErrorCatalog.E5702.Code, "date.toZone: the zone is not a known time zone.");
        }
    }

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

    /// <summary>The current instant, converted from <see cref="IClock"/>'s UTC reading into its local zone (D-176, R-32).</summary>
    private DateTimeOffset Now() => TimeZoneInfo.ConvertTime(
        new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)), _clock.LocalZone);

    private static GrobValue FromDateTimeOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        DateLayout.TypeName,
        [new KeyValuePair<string, GrobValue>(
            DateLayout.ValueFieldName, GrobValue.FromString(value.ToString(DateLayout.RoundTripFormat, CultureInfo.InvariantCulture)))]));

    private static DateTimeOffset ToDateTimeOffset(GrobStruct receiver) => DateTimeOffset.ParseExact(
        receiver.GetField(DateLayout.ValueFieldName).AsString(), DateLayout.RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None);

    private static string IsoDateTimeString(DateTimeOffset value) {
        string format = value.Offset == TimeSpan.Zero ? IsoUtcFormat : IsoOffsetFormat;
        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    private static long DaysBetween(DateTimeOffset from, DateTimeOffset to) => (long)(to - from).TotalDays;
}
