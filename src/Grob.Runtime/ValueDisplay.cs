using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using Grob.Core;

namespace Grob.Runtime;

/// <summary>
/// Renders a <see cref="GrobValue"/> as human-readable text (D-336). Two entry points
/// share one recursive core:
/// <list type="bullet">
///   <item><description><see cref="Display(GrobValue)"/> — the top-level, public-facing
///     form (what <c>print()</c> and a REPL prompt use). A top-level string renders
///     unquoted.</description></item>
///   <item><description><see cref="Inspect(GrobValue)"/> — the nested form a composite
///     recurses into for its elements. A string renders quoted and escaped, which is
///     what distinguishes the string <c>"8080"</c> from the int <c>8080</c> inside a
///     composite.</description></item>
/// </list>
/// <para>
/// Dispatch precedence is load-bearing and fixed in this order: (1) <c>nil</c>
/// (any position); (2) a registered <c>toString()</c> via the injected
/// <see cref="IValueToStringRegistry"/>; (3) scalars (<c>bool</c>, <c>int</c>,
/// <c>float</c>); (4) <c>string</c> (position-dependent quoting); (5) <c>Function</c>
/// (its signature); (6) composites (<c>Struct</c>, <c>Array</c>, <c>Map</c>),
/// structural, recursing into elements via <see cref="Inspect(GrobValue)"/>. Step 2
/// precedes step 6 deliberately: per D-297 every plugin type and user <c>type</c> shares
/// the <c>Struct</c> discriminator, so if the structural arm ran before the registry
/// lookup, a credential-bearing struct (D-159) would render field-by-field and leak its
/// payload. Placing the registry lookup first makes that guarantee hold by
/// construction, not by convention.
/// </para>
/// </summary>
public sealed class ValueDisplay {
    /// <summary>
    /// Backstop recursion limit for composite nesting. A structure deeper than this
    /// (reachable only via a manufactured runtime cycle of some length, since the type
    /// checker already rejects non-terminating type cycles) renders <c>...</c> at the
    /// cutoff rather than recursing further.
    /// </summary>
    internal const int MaxDepth = 32;

    private readonly IValueToStringRegistry _registry;

    /// <summary>Creates a display service with no registered <c>toString()</c> types.</summary>
    public ValueDisplay() : this(NullRegistry.Instance) {
    }

    /// <summary>
    /// Creates a display service backed by <paramref name="registry"/>. Internal — real
    /// registrations are a later increment's job; this seam exists today so
    /// <see cref="ValueDisplay"/> can be tested against the security-ordering guarantee
    /// without a real plugin type in the tree.
    /// </summary>
    internal ValueDisplay(IValueToStringRegistry registry) {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// Renders <paramref name="value"/> for a top-level position — the form <c>print()</c>
    /// uses. A top-level string is unquoted; everything else renders identically to
    /// <see cref="Inspect(GrobValue)"/>.
    /// </summary>
    public string Display(GrobValue value) => RenderTop(value, quoteStringsHere: false);

    /// <summary>
    /// Renders <paramref name="value"/> for a nested position — what a composite's
    /// elements, fields, keys and values recurse through. A string is quoted and escaped.
    /// </summary>
    internal string Inspect(GrobValue value) => RenderTop(value, quoteStringsHere: true);

    private string RenderTop(GrobValue value, bool quoteStringsHere) {
        var sb = new StringBuilder();
        Render(value, quoteStringsHere, sb, new RenderState(), depth: 0);
        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Recursive core (D-336 dispatch precedence, steps 1-6).
    // -----------------------------------------------------------------------

    private void Render(GrobValue value, bool quoteStrings, StringBuilder sb, RenderState state, int depth) {
        // Step 1 — nil, any position.
        if (value.IsNil) {
            sb.Append("nil");
            return;
        }

        // Step 2 — registered toString() wins before any scalar or structural arm below
        // (the credential-ordering guarantee described on the type).
        if (_registry.TryToString(value, out string? rendered)) {
            sb.Append(rendered);
            return;
        }

        switch (value.Kind) {
            // Step 3 — scalars.
            case GrobValueKind.Bool:
                sb.Append(value.AsBool() ? "true" : "false");
                return;
            case GrobValueKind.Int:
                sb.Append(value.AsInt().ToString(CultureInfo.InvariantCulture));
                return;
            case GrobValueKind.Float:
                sb.Append(FormatFloat(value.AsFloat()));
                return;

            // Step 4 — string, position-dependent.
            case GrobValueKind.String:
                RenderString(value.AsString(), quoteStrings, sb);
                return;

            // Step 5 — function, by its erased signature.
            case GrobValueKind.Function:
                sb.Append(FormatFunctionSignature(value.AsFunction()));
                return;

            // Step 6 — composites, structural, recursing via Inspect.
            case GrobValueKind.Struct:
                RenderStruct(value.AsStruct(), sb, state, depth);
                return;
            case GrobValueKind.Array:
                RenderArray(value.AsArray(), sb, state, depth);
                return;
            case GrobValueKind.Map:
                RenderMap(value.AsMap(), sb, state, depth);
                return;

            default:
                ThrowUnreachableKind(value.Kind);
                return;
        }
    }

    [ExcludeFromCodeCoverage(Justification =
        "GrobValueKind is a closed nine-variant enum (Grob.Core/GrobValueKind.cs); Nil " +
        "is handled before this switch and the remaining eight variants (Bool, Int, " +
        "Float, String, Array, Map, Struct, Function) are all cased explicitly above. " +
        "Unreachable via any valid GrobValue; retained as a belt-and-braces guard " +
        "against a future variant landing without a matching Render arm.")]
    private static void ThrowUnreachableKind(GrobValueKind kind) =>
        throw new GrobInternalException($"ValueDisplay.Render: unhandled GrobValueKind '{kind}'.");

    // -----------------------------------------------------------------------
    // Float — round-trip, decimal point, invariant culture, pinned non-finite spellings.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Internal (not private) so <c>Grob.Stdlib</c>'s <c>float.toString()</c> native can
    /// reuse this exact formatting rather than carrying a second, driftable copy
    /// (D-369) — <c>Grob.Runtime.csproj</c> grants <c>Grob.Stdlib</c> the same
    /// <c>InternalsVisibleTo</c> access already granted to <c>Grob.Vm</c> for the
    /// identical reason (D-336).
    /// </summary>
    internal static string FormatFloat(double value) {
        // Pinned literal spellings rather than InvariantCulture's NaN/Infinity symbols —
        // explicit and independent of any future change to those symbol tables.
        if (double.IsNaN(value)) return "NaN";
        if (double.IsPositiveInfinity(value)) return "Infinity";
        if (double.IsNegativeInfinity(value)) return "-Infinity";

        // .NET's default double.ToString() (no format specifier) is shortest
        // round-trippable since .NET Core 3.0 — exactly the round-trip guarantee this
        // rendering needs. InvariantCulture is pinned explicitly so an ambient
        // de-DE/fr-FR host culture cannot substitute a comma for the decimal point.
        string text = value.ToString(CultureInfo.InvariantCulture);
        bool hasDecimalMarker = text.Contains('.') || text.Contains('e') || text.Contains('E');
        return hasDecimalMarker ? text : text + ".0";
    }

    // -----------------------------------------------------------------------
    // String — position-dependent quoting; escapes ", \ and newline only.
    // -----------------------------------------------------------------------

    private static void RenderString(string raw, bool quoteStrings, StringBuilder sb) {
        if (!quoteStrings) {
            sb.Append(raw);
            return;
        }
        sb.Append('"');
        foreach (char c in raw) {
            switch (c) {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                default: sb.Append(c); break;
            }
        }
        sb.Append('"');
    }

    // -----------------------------------------------------------------------
    // Function — erased signature spelling: fn(T1, T2, …): R.
    // -----------------------------------------------------------------------

    private static string FormatFunctionSignature(GrobFunction fn) {
        var sb = new StringBuilder("fn(");
        for (int i = 0; i < fn.ParameterTypes.Count; i++) {
            if (i > 0) sb.Append(", ");
            sb.Append(SpellType(fn.ParameterTypes[i]));
        }
        sb.Append("): ").Append(SpellType(fn.ReturnType));
        return sb.ToString();
    }

    private static string SpellType(GrobType type) => type switch {
        GrobType.Int => "int",
        GrobType.Float => "float",
        GrobType.String => "string",
        GrobType.Bool => "bool",
        GrobType.Nil => "nil",
        GrobType.NullableInt => "int?",
        GrobType.NullableFloat => "float?",
        GrobType.NullableString => "string?",
        GrobType.NullableBool => "bool?",
        GrobType.Array => "array",
        GrobType.NullableArray => "array?",
        GrobType.Map => "map",
        GrobType.Function => "fn",
        GrobType.NullableFunction => "fn?",
        GrobType.Struct => "struct",
        GrobType.NullableStruct => "struct?",
        GrobType.AnonStruct => "struct",
        GrobType.NullableAnonStruct => "struct?",
        _ => "unknown",
    };

    // -----------------------------------------------------------------------
    // Composites — structural, source-shaped, cycle-safe, depth-capped (D-336).
    //
    // Cycle detection: reference-identity only (not GrobStruct's overridden value
    // equality, which would false-positive on two distinct but field-equal structs) —
    // RenderState.Visited is a HashSet<object> keyed by ReferenceEqualityComparer.
    //
    // Lazy allocation: a composite's own reference is added to the visited set only
    // when at least one of its own fields/elements is itself a composite ("nests") —
    // determined by a cheap Kind check over its children before recursing. A scalar
    // value or a struct whose fields are all scalars therefore never touches the
    // visited set, and RenderState.Visited stays null for a render that touches no
    // nested composite at all.
    // -----------------------------------------------------------------------

    private sealed class RenderState {
        public HashSet<object>? Visited;
    }

    private static HashSet<object> EnsureVisited(RenderState state) =>
        state.Visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);

    private static bool IsComposite(GrobValueKind kind) =>
        kind is GrobValueKind.Struct or GrobValueKind.Array or GrobValueKind.Map;

    private void RenderStruct(GrobStruct s, StringBuilder sb, RenderState state, int depth) {
        if (state.Visited?.Contains(s) == true) {
            sb.Append("<cycle>");
            return;
        }
        if (depth >= MaxDepth) {
            sb.Append("...");
            return;
        }

        bool nests = s.Fields.Any(f => IsComposite(f.Value.Kind));
        if (nests) EnsureVisited(state).Add(s);

        var parts = new List<string>(s.Fields.Count);
        var inner = new StringBuilder();
        foreach (var field in s.Fields) {
            inner.Clear();
            inner.Append(field.Key).Append(": ");
            Render(field.Value, quoteStrings: true, inner, state, depth + 1);
            parts.Add(inner.ToString());
        }

        string prefix = s.IsAnonymous ? "#" : s.TypeName + " ";
        AppendBraced(sb, prefix, parts);

        if (nests) state.Visited!.Remove(s);
    }

    private void RenderArray(GrobArray arr, StringBuilder sb, RenderState state, int depth) {
        if (state.Visited?.Contains(arr) == true) {
            sb.Append("<cycle>");
            return;
        }
        if (depth >= MaxDepth) {
            sb.Append("...");
            return;
        }

        bool nests = arr.Elements.Any(e => IsComposite(e.Kind));
        if (nests) EnsureVisited(state).Add(arr);

        sb.Append('[');
        for (int i = 0; i < arr.Elements.Count; i++) {
            if (i > 0) sb.Append(", ");
            Render(arr.Elements[i], quoteStrings: true, sb, state, depth + 1);
        }
        sb.Append(']');

        if (nests) state.Visited!.Remove(arr);
    }

    private void RenderMap(GrobMap map, StringBuilder sb, RenderState state, int depth) {
        if (state.Visited?.Contains(map) == true) {
            sb.Append("<cycle>");
            return;
        }
        if (depth >= MaxDepth) {
            sb.Append("...");
            return;
        }

        IReadOnlyList<string> keys = map.InsertionOrderKeys;
        bool nests = keys.Any(k => IsComposite(map[k].Kind));
        if (nests) EnsureVisited(state).Add(map);

        var parts = new List<string>(keys.Count);
        var inner = new StringBuilder();
        foreach (string key in keys) {
            inner.Clear();
            RenderString(key, quoteStrings: true, inner);
            inner.Append(": ");
            Render(map[key], quoteStrings: true, inner, state, depth + 1);
            parts.Add(inner.ToString());
        }
        AppendBraced(sb, prefix: "", parts);

        if (nests) state.Visited!.Remove(map);
    }

    /// <summary>
    /// Appends <c>prefix{ part, part, … }</c>, or <c>prefix{ }</c> when
    /// <paramref name="parts"/> is empty — the shared brace form for a named struct,
    /// an anonymous struct and a map.
    /// </summary>
    private static void AppendBraced(StringBuilder sb, string prefix, IReadOnlyList<string> parts) {
        sb.Append(prefix).Append('{').Append(' ');
        for (int i = 0; i < parts.Count; i++) {
            if (i > 0) sb.Append(", ");
            sb.Append(parts[i]);
        }
        if (parts.Count > 0) sb.Append(' ');
        sb.Append('}');
    }
}
