namespace Grob.Core.PrimitiveMembers;

/// <summary>
/// The compile-time-only instance-surface table for every primitive
/// <see cref="GrobType"/> (D-066, this increment's <c>string</c> proving case) —
/// consulted by <c>Grob.Compiler</c>'s <c>ResolveMemberAccessCall</c>/
/// <c>VisitMemberAccess</c> to rewrite a primitive-receiver method call or property
/// access to a qualified native call, receiver injected as arg[0]. Parallel to
/// <see cref="NamedTypes.NamedTypeRegistry"/> (D-356), not an extension of it — a
/// primitive is never <see cref="GrobValueKind.Struct"/>, so there is no
/// <c>GetProperty</c>/<c>Bind</c> runtime dispatch here, only a compile-time rewrite.
/// <c>string</c>, <c>int</c>, <c>float</c> and <c>bool</c> (D-369) are the registered
/// entries.
/// </summary>
public static class PrimitiveMemberRegistry {
    /// <summary>The <c>string</c> instance-surface entry.</summary>
    public static PrimitiveMemberEntry String { get; } = BuildStringEntry();

    /// <summary>The <c>int</c> instance-surface entry (D-369).</summary>
    public static PrimitiveMemberEntry Int { get; } = BuildIntEntry();

    /// <summary>The <c>float</c> instance-surface entry (D-369).</summary>
    public static PrimitiveMemberEntry Float { get; } = BuildFloatEntry();

    /// <summary>The <c>bool</c> instance-surface entry (D-369).</summary>
    public static PrimitiveMemberEntry Bool { get; } = BuildBoolEntry();

    // Declared after String/Int/Float/Bool so their static-initializer values are
    // already set — C# initializes static fields/auto-properties in textual
    // declaration order (mirrors NamedTypeRegistry's identical Guid/Date-then-_entries
    // ordering).
    private static readonly IReadOnlyDictionary<GrobType, PrimitiveMemberEntry> _entries =
        new Dictionary<GrobType, PrimitiveMemberEntry> {
            [String.ReceiverType] = String,
            [Int.ReceiverType] = Int,
            [Float.ReceiverType] = Float,
            [Bool.ReceiverType] = Bool,
        };

    /// <summary>
    /// Every registered qualified native name across every entry, flattened — the set
    /// the agreement test diffs against live plugin registration.
    /// </summary>
    public static IReadOnlyList<string> AllQualifiedNativeNames { get; } = [
        .. String.Properties.Values.Select(p => p.QualifiedNativeName),
        .. String.Methods.Values.Select(m => m.QualifiedNativeName),
        .. Int.Properties.Values.Select(p => p.QualifiedNativeName),
        .. Int.Methods.Values.Select(m => m.QualifiedNativeName),
        .. Float.Properties.Values.Select(p => p.QualifiedNativeName),
        .. Float.Methods.Values.Select(m => m.QualifiedNativeName),
        .. Bool.Properties.Values.Select(p => p.QualifiedNativeName),
        .. Bool.Methods.Values.Select(m => m.QualifiedNativeName),
    ];

    /// <summary>
    /// Looks up the registered entry for <paramref name="receiverType"/>. Returns
    /// <c>false</c> when the type has no registered primitive-member surface yet
    /// (every primitive besides <c>string</c>, today).
    /// </summary>
    public static bool TryGet(GrobType receiverType, out PrimitiveMemberEntry entry) =>
        _entries.TryGetValue(receiverType, out entry!);

    // -----------------------------------------------------------------------
    // string — grob-type-registry.md's `string` section, now complete (D-365
    // wires padLeft/padRight/truncate onto D-364's default-argument-fill
    // mechanism, the second of its three designed consumers to be wired).
    // -----------------------------------------------------------------------

    private static PrimitiveMemberEntry BuildStringEntry() {
        Dictionary<string, PrimitiveMemberProperty> properties = new(StringComparer.Ordinal) {
            ["length"] = new PrimitiveMemberProperty("length", GrobType.Int, "string.length"),
            ["isEmpty"] = new PrimitiveMemberProperty("isEmpty", GrobType.Bool, "string.isEmpty"),
        };

        Dictionary<string, PrimitiveMemberMethod> methods = new(StringComparer.Ordinal) {
            ["toInt"] = new PrimitiveMemberMethod("toInt", [], GrobType.NullableInt, "string.toInt"),
            ["toFloat"] = new PrimitiveMemberMethod("toFloat", [], GrobType.NullableFloat, "string.toFloat"),
            ["trim"] = new PrimitiveMemberMethod("trim", [], GrobType.String, "string.trim"),
            ["trimStart"] = new PrimitiveMemberMethod("trimStart", [], GrobType.String, "string.trimStart"),
            ["trimEnd"] = new PrimitiveMemberMethod("trimEnd", [], GrobType.String, "string.trimEnd"),
            ["upper"] = new PrimitiveMemberMethod("upper", [], GrobType.String, "string.upper"),
            ["lower"] = new PrimitiveMemberMethod("lower", [], GrobType.String, "string.lower"),
            ["split"] = new PrimitiveMemberMethod("split", [GrobType.String], GrobType.Array, "string.split"),
            ["contains"] = new PrimitiveMemberMethod("contains", [GrobType.String], GrobType.Bool, "string.contains"),
            ["startsWith"] = new PrimitiveMemberMethod("startsWith", [GrobType.String], GrobType.Bool, "string.startsWith"),
            ["endsWith"] = new PrimitiveMemberMethod("endsWith", [GrobType.String], GrobType.Bool, "string.endsWith"),
            ["replace"] = new PrimitiveMemberMethod(
                "replace", [GrobType.String, GrobType.String], GrobType.String, "string.replace"),
            ["indexOf"] = new PrimitiveMemberMethod("indexOf", [GrobType.String], GrobType.Int, "string.indexOf"),
            ["lastIndexOf"] = new PrimitiveMemberMethod("lastIndexOf", [GrobType.String], GrobType.Int, "string.lastIndexOf"),
            ["substring"] = new PrimitiveMemberMethod(
                "substring", [GrobType.Int, GrobType.Int], GrobType.String, "string.substring"),
            ["repeat"] = new PrimitiveMemberMethod("repeat", [GrobType.Int], GrobType.String, "string.repeat"),
            ["left"] = new PrimitiveMemberMethod("left", [GrobType.Int], GrobType.String, "string.left"),
            ["right"] = new PrimitiveMemberMethod("right", [GrobType.Int], GrobType.String, "string.right"),
            ["toString"] = new PrimitiveMemberMethod("toString", [], GrobType.String, "string.toString"),
            ["padLeft"] = new PrimitiveMemberMethod(
                "padLeft", [GrobType.Int, GrobType.String], GrobType.String, "string.padLeft",
                [null, GrobValue.FromString(" ")]),
            ["padRight"] = new PrimitiveMemberMethod(
                "padRight", [GrobType.Int, GrobType.String], GrobType.String, "string.padRight",
                [null, GrobValue.FromString(" ")]),
            ["truncate"] = new PrimitiveMemberMethod(
                "truncate", [GrobType.Int, GrobType.String], GrobType.String, "string.truncate",
                [null, GrobValue.FromString("...")]),
        };

        return new PrimitiveMemberEntry(GrobType.String, properties, methods);
    }

    // -----------------------------------------------------------------------
    // int — grob-type-registry.md's `int` section (D-369): toString/toFloat/abs/
    // format(pattern). No properties.
    // -----------------------------------------------------------------------

    private static PrimitiveMemberEntry BuildIntEntry() {
        Dictionary<string, PrimitiveMemberMethod> methods = new(StringComparer.Ordinal) {
            ["toString"] = new PrimitiveMemberMethod("toString", [], GrobType.String, "int.toString"),
            ["toFloat"] = new PrimitiveMemberMethod("toFloat", [], GrobType.Float, "int.toFloat"),
            ["abs"] = new PrimitiveMemberMethod("abs", [], GrobType.Int, "int.abs"),
            ["format"] = new PrimitiveMemberMethod("format", [GrobType.String], GrobType.String, "int.format"),
        };

        return new PrimitiveMemberEntry(
            GrobType.Int, new Dictionary<string, PrimitiveMemberProperty>(StringComparer.Ordinal), methods);
    }

    // -----------------------------------------------------------------------
    // float — grob-type-registry.md's `float` section (D-369): toString/toInt/round/
    // roundTo(decimals)/floor/ceil/abs/format(pattern) — round/roundTo split per D-368.
    // No properties.
    // -----------------------------------------------------------------------

    private static PrimitiveMemberEntry BuildFloatEntry() {
        Dictionary<string, PrimitiveMemberMethod> methods = new(StringComparer.Ordinal) {
            ["toString"] = new PrimitiveMemberMethod("toString", [], GrobType.String, "float.toString"),
            ["toInt"] = new PrimitiveMemberMethod("toInt", [], GrobType.Int, "float.toInt"),
            ["round"] = new PrimitiveMemberMethod("round", [], GrobType.Int, "float.round"),
            ["roundTo"] = new PrimitiveMemberMethod("roundTo", [GrobType.Int], GrobType.Float, "float.roundTo"),
            ["floor"] = new PrimitiveMemberMethod("floor", [], GrobType.Int, "float.floor"),
            ["ceil"] = new PrimitiveMemberMethod("ceil", [], GrobType.Int, "float.ceil"),
            ["abs"] = new PrimitiveMemberMethod("abs", [], GrobType.Float, "float.abs"),
            ["format"] = new PrimitiveMemberMethod("format", [GrobType.String], GrobType.String, "float.format"),
        };

        return new PrimitiveMemberEntry(
            GrobType.Float, new Dictionary<string, PrimitiveMemberProperty>(StringComparer.Ordinal), methods);
    }

    // -----------------------------------------------------------------------
    // bool — grob-type-registry.md's `bool` section (D-369): toString only.
    // No properties.
    // -----------------------------------------------------------------------

    private static PrimitiveMemberEntry BuildBoolEntry() {
        Dictionary<string, PrimitiveMemberMethod> methods = new(StringComparer.Ordinal) {
            ["toString"] = new PrimitiveMemberMethod("toString", [], GrobType.String, "bool.toString"),
        };

        return new PrimitiveMemberEntry(
            GrobType.Bool, new Dictionary<string, PrimitiveMemberProperty>(StringComparer.Ordinal), methods);
    }
}
