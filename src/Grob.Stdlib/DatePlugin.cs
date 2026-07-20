using System.Globalization;

using Grob.Core;
using Grob.Core.NamedTypes;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The <c>date</c> module (D-107/D-108/D-354/D-355 — Sprint 9 Increment B): the static
/// constructors (<c>now</c>/<c>today</c> on <see cref="IClock"/>, <c>of</c>/<c>ofTime</c>,
/// <c>parse</c> throwing <c>ParseError</c> through the native-throw seam,
/// <c>fromUnixSeconds</c>/<c>fromUnixMillis</c>), and the registered <c>toString()</c> that
/// makes <c>ValueDisplay</c> (D-336) render the canonical ISO-8601 form. A <c>date</c>
/// runtime value is a <see cref="GrobStruct"/> named <c>"date"</c> with exactly one hidden
/// field (<see cref="ValueFieldName"/>) holding a round-trip-formatted
/// <see cref="DateTimeOffset"/> string — this is the only place outside <c>Grob.Vm</c>'s
/// <c>DateNatives</c> that convention is spelled out (the two cannot share code:
/// <c>Grob.Stdlib</c> and <c>Grob.Vm</c> are DAG siblings, neither referencing the other),
/// so it must stay in lockstep with <c>DateNatives.ValueFieldName</c>/<c>TypeName</c> and
/// its round-trip format string. Registers exactly the qualified names listed in the
/// compile-time twin, <c>NamespaceRegistry</c>'s <c>date</c> entry in <c>Grob.Compiler</c>.
/// The instance property/method surface (<c>year</c>, <c>addDays</c>, ...) is dispatched
/// entirely by <c>Grob.Vm</c>'s <c>OpCode.GetProperty</c> handler via <c>DateNatives</c> —
/// this plugin registers only the namespace-qualified statics and the display renderer.
/// </summary>
public sealed class DatePlugin : IGrobPlugin {
    /// <summary>The hidden field name storing a <c>date</c> value's round-trip string form.</summary>
    internal const string ValueFieldName = "__value";

    /// <summary>The struct type name every <c>date</c> value carries.</summary>
    internal const string TypeName = "date";

    // Must match Grob.Vm.DateNatives's private RoundTripFormat exactly — both sides
    // produce/consume the same runtime string, and neither project can reference the
    // other's constant.
    private const string RoundTripFormat = "yyyy-MM-ddTHH:mm:sszzz";

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

        registrar.RegisterNative("date.parse", new NativeFunction("date.parse", 1, (args, _) => {
            string s = args[0].AsString();
            // D-176: a string with an explicit offset preserves it; one without is
            // interpreted as local time — DateTimeStyles.AssumeLocal gives exactly that.
            if (!DateTimeOffset.TryParse(
                    s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTimeOffset parsed)) {
                throw new NativeFaultException(
                    "ParseError", ErrorCatalog.E5702.Code, $"date.parse: '{s}' is not a valid date.");
            }
            return FromDateTimeOffset(parsed);
        }));

        registrar.RegisterNative("date.fromUnixSeconds", new NativeFunction("date.fromUnixSeconds", 1,
            (args, _) => FromDateTimeOffset(DateTimeOffset.FromUnixTimeSeconds(args[0].AsInt()))));

        registrar.RegisterNative("date.fromUnixMillis", new NativeFunction("date.fromUnixMillis", 1,
            (args, _) => FromDateTimeOffset(DateTimeOffset.FromUnixTimeMilliseconds(args[0].AsInt()))));

        // D-356: the renderer itself now lives on the NamedTypeRegistry entry (the
        // single source of truth also consulted by Grob.Compiler/Grob.Vm) — this call
        // only wires it into ValueDisplay's runtime registry, preserving the D-336
        // credential-ordering guarantee unchanged.
        registrar.RegisterToString(NamedTypeRegistry.Date.CanonicalName, NamedTypeRegistry.Date.ToStringRenderer);
    }

    // -----------------------------------------------------------------------
    // Runtime representation — a GrobStruct with one hidden field. Must stay in
    // lockstep with Grob.Vm.DateNatives (DAG siblings; no shared code possible).
    // -----------------------------------------------------------------------

    /// <summary>The current instant, converted from <see cref="IClock"/>'s UTC reading to local time (D-176).</summary>
    private DateTimeOffset Now() => new DateTimeOffset(DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc)).ToLocalTime();

    private static GrobValue FromDateTimeOffset(DateTimeOffset value) => GrobValue.FromStruct(new GrobStruct(
        TypeName,
        [new KeyValuePair<string, GrobValue>(
            ValueFieldName, GrobValue.FromString(value.ToString(RoundTripFormat, CultureInfo.InvariantCulture)))]));

}
