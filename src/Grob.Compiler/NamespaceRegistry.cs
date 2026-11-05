using System.Diagnostics.CodeAnalysis;

using Grob.Core;
using Grob.Runtime;

namespace Grob.Compiler;

/// <summary>
/// The compile-time member table for every core stdlib module namespace (D-342). Hand
/// authored, one entry per namespace member, updated in lockstep with the corresponding
/// runtime <c>IGrobPlugin</c> registration in <c>Grob.Stdlib</c> — the same
/// two-hand-maintained-mirrors-plus-agreement-test shape D-308 established for
/// <see cref="ErrorCatalog"/> against <c>grob-error-codes.md</c>. Transitional since
/// D-426 change group 4: the type checker reads the host's <c>NativeCatalogue</c> first,
/// through <see cref="NamespaceSymbols"/>, and falls back to this table only for members
/// the catalogue does not yet hold. Change group 5 moves every entry into its plugin's
/// signature and deletes the table.
/// </summary>
internal static class NamespaceRegistry {
    /// <summary>
    /// A namespace constant member (e.g. <c>math.pi</c>) and its declared type.
    /// <paramref name="NamedTypeName"/> is non-null when <paramref name="Type"/> is
    /// <see cref="GrobType.Struct"/> (or its nullable variant) and the constant names a
    /// specific plugin-owned struct type (<c>guid.empty</c>, Sprint 8 Increment D) — no
    /// namespace constant returned a <c>Struct</c>-kind value before, so this channel did
    /// not previously exist. <see cref="TypeChecker"/> threads it into
    /// <c>MemberAccessExpr.ResolvedStructTypeName</c> the same way a user struct field's
    /// name is threaded (D-297).
    /// </summary>
    internal sealed record ConstantMember(GrobType Type, string? NamedTypeName = null);

    /// <summary>
    /// A namespace native member (e.g. <c>math.sqrt</c>) and its positional signature.
    /// Most v1 core-module natives take no named or defaulted arguments, so arity/type
    /// validation is a straight positional check for them — the smaller counterpart of
    /// the named-argument binding machinery <c>CheckCall</c> uses for user-defined
    /// <c>fn</c>s. <paramref name="VariadicElementType"/> is non-null for a native that
    /// needs a variable-length tail (<c>path.join</c>, Sprint 8 Increment B;
    /// <c>guid.newV5</c>, Increment D): when set, at least one argument beyond
    /// <see cref="ParameterTypes"/>'s fixed prefix is required, and every argument from
    /// that point on is checked against this type instead of a fixed slot.
    /// <paramref name="NamedTypeName"/> is non-null when <paramref name="ReturnType"/> is
    /// <see cref="GrobType.Struct"/> (or its nullable variant) and the native returns a
    /// specific plugin-owned struct type (<c>guid.newV4</c>, Increment D) — see
    /// <see cref="ConstantMember.NamedTypeName"/>. <paramref name="ParameterNamedTypeNames"/>
    /// is a parallel, optional list to <see cref="ParameterTypes"/> — a non-null entry at
    /// index <c>i</c> means that fixed parameter must be a specific plugin-owned struct
    /// type by name, not merely <see cref="GrobType.Struct"/> (<c>guid.newV5</c>'s
    /// namespace parameter, which must itself be a <c>guid</c>, not any struct sharing the
    /// flat tag — CodeRabbit review, PR #133). <see langword="null"/> (the default) means
    /// no natives currently need it beyond a fixed prefix of length 0.
    /// <paramref name="ParameterDefaults"/> (D-358) declares an optional trailing run of
    /// <see cref="ParameterTypes"/> as defaulted — a non-null entry at index <c>i</c> is
    /// the compile-time constant a call omitting that argument gets filled with
    /// (<see cref="NativeDefaultArgumentFill"/>); <see langword="null"/> at the leading
    /// (required) indices, non-null only for the trailing optional run. Never combined
    /// with <paramref name="VariadicElementType"/> on the same entry — trailing defaults
    /// and a variable-length tail are different, mutually exclusive shapes.
    /// </summary>
    internal sealed record NativeMember(
        IReadOnlyList<GrobType> ParameterTypes,
        GrobType ReturnType,
        GrobType? VariadicElementType = null,
        string? NamedTypeName = null,
        IReadOnlyList<string?>? ParameterNamedTypeNames = null,
        IReadOnlyList<GrobValue?>? ParameterDefaults = null);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> _namespaces =
        new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.Ordinal) {
            ["math"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["pi"] = new ConstantMember(GrobType.Float),
                ["e"] = new ConstantMember(GrobType.Float),
                ["tau"] = new ConstantMember(GrobType.Float),
                ["sqrt"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["pow"] = new NativeMember([GrobType.Float, GrobType.Float], GrobType.Float),
                ["log"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["log10"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["sin"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["cos"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["tan"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["asin"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["acos"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["atan"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["atan2"] = new NativeMember([GrobType.Float, GrobType.Float], GrobType.Float),
                ["toRadians"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["toDegrees"] = new NativeMember([GrobType.Float], GrobType.Float),
                ["random"] = new NativeMember([], GrobType.Float),
                ["randomInt"] = new NativeMember([GrobType.Int, GrobType.Int], GrobType.Int),
                ["randomSeed"] = new NativeMember([GrobType.Int], GrobType.Nil),
            },
            ["path"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["separator"] = new ConstantMember(GrobType.String),
                ["join"] = new NativeMember([], GrobType.String, GrobType.String),
                ["joinAll"] = new NativeMember([GrobType.Array], GrobType.String),
                ["extension"] = new NativeMember([GrobType.String], GrobType.String),
                ["filename"] = new NativeMember([GrobType.String], GrobType.String),
                ["stem"] = new NativeMember([GrobType.String], GrobType.String),
                ["directory"] = new NativeMember([GrobType.String], GrobType.String),
                ["resolve"] = new NativeMember([GrobType.String], GrobType.String),
                ["normalise"] = new NativeMember([GrobType.String], GrobType.String),
                ["isAbsolute"] = new NativeMember([GrobType.String], GrobType.Bool),
                ["isRelative"] = new NativeMember([GrobType.String], GrobType.Bool),
                ["changeExtension"] = new NativeMember([GrobType.String, GrobType.String], GrobType.String),
            },
            ["strings"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["join"] = new NativeMember([GrobType.Array, GrobType.String], GrobType.String),
            },
            ["env"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["get"] = new NativeMember([GrobType.String], GrobType.NullableString),
                ["require"] = new NativeMember([GrobType.String], GrobType.String),
                ["has"] = new NativeMember([GrobType.String], GrobType.Bool),
                ["set"] = new NativeMember([GrobType.String, GrobType.String], GrobType.Nil),
                ["all"] = new NativeMember([], GrobType.Map),
            },
            ["log"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["debug"] = new NativeMember([GrobType.String], GrobType.Nil),
                ["info"] = new NativeMember([GrobType.String], GrobType.Nil),
                ["warning"] = new NativeMember([GrobType.String], GrobType.Nil),
                ["error"] = new NativeMember([GrobType.String], GrobType.Nil),
                ["setLevel"] = new NativeMember([GrobType.String], GrobType.Nil),
            },
            // Sprint 8 Increment D: guid — the first namespace with Struct-kind members.
            // "namespaces.dns"/"namespaces.url"/"namespaces.oid" are flat member keys
            // (the literal dot is part of the dictionary key), not a nested namespace —
            // TryAnnotateNamespaceReceiver only recognises a bare identifier receiver, so
            // guid.namespaces.dns is resolved as a single two-segment member-name lookup
            // on the "guid" namespace rather than a real nested "guid.namespaces"
            // namespace (D-149).
            ["guid"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["newV4"] = new NativeMember([], GrobType.Struct, NamedTypeName: "guid"),
                ["newV7"] = new NativeMember([], GrobType.Struct, NamedTypeName: "guid"),
                ["newV5"] = new NativeMember(
                    [GrobType.Struct], GrobType.Struct, VariadicElementType: GrobType.String, NamedTypeName: "guid",
                    ParameterNamedTypeNames: ["guid"]),
                ["parse"] = new NativeMember([GrobType.String], GrobType.Struct, NamedTypeName: "guid"),
                ["tryParse"] = new NativeMember([GrobType.String], GrobType.NullableStruct, NamedTypeName: "guid"),
                ["empty"] = new ConstantMember(GrobType.Struct, NamedTypeName: "guid"),
                ["namespaces.dns"] = new ConstantMember(GrobType.Struct, NamedTypeName: "guid"),
                ["namespaces.url"] = new ConstantMember(GrobType.Struct, NamedTypeName: "guid"),
                ["namespaces.oid"] = new ConstantMember(GrobType.Struct, NamedTypeName: "guid"),
            },
            // Sprint 9 Increment B: date is the second namespace with Struct-kind
            // members, after guid. Every static constructor below returns a date struct;
            // the instance property/method surface is resolved separately in
            // TypeChecker.Expressions.cs, keyed off the receiver's struct name, mirroring
            // guid's instance-member arms — this table only models namespace-qualified
            // members, not instance members on an already-resolved date value.
            ["date"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["now"] = new NativeMember([], GrobType.Struct, NamedTypeName: "date"),
                ["today"] = new NativeMember([], GrobType.Struct, NamedTypeName: "date"),
                ["of"] = new NativeMember(
                    [GrobType.Int, GrobType.Int, GrobType.Int], GrobType.Struct, NamedTypeName: "date"),
                ["ofTime"] = new NativeMember(
                    [GrobType.Int, GrobType.Int, GrobType.Int, GrobType.Int, GrobType.Int, GrobType.Int],
                    GrobType.Struct, NamedTypeName: "date"),
                // D-358: pattern is optional and trailing — "" means the pre-existing
                // ISO-8601 behaviour, unchanged; a non-empty pattern selects ParseExact
                // (Grob.Stdlib.DatePlugin).
                ["parse"] = new NativeMember([GrobType.String, GrobType.String], GrobType.Struct,
                    NamedTypeName: "date", ParameterDefaults: [null, GrobValue.FromString("")]),
                ["fromUnixSeconds"] = new NativeMember([GrobType.Int], GrobType.Struct, NamedTypeName: "date"),
                ["fromUnixMillis"] = new NativeMember([GrobType.Int], GrobType.Struct, NamedTypeName: "date"),
            },
            // Sprint 9 Increment A1b: int/float type-static functions (D-370), completing
            // the numeric surface D-369's instance-member registration began. min/max/clamp
            // are namespace-receiver calls (int.min(a, b), not a.min(b)) so they belong here,
            // not in PrimitiveMemberRegistry (Grob.Core) alongside D-369's instance methods —
            // a different compile-time registry for a genuinely different call shape, even
            // though both ultimately register a runtime native under the same "int."/"float."
            // qualified-name prefix (reconciled by the agreement tests, see
            // PrimitiveMemberRegistryAgreementTests and NamespaceRegistryAgreementTests).
            // Registering "int"/"float" as namespace names here means a top-level `int := 5`
            // now collides with the pre-registered NamespaceDecl symbol exactly as `math := 5`
            // already does (E1102) — the same precedent, not a special case; int/float are
            // deliberately NOT added to _reservedIdentifiers, which is reserved for the
            // separate D-320 'formatAs'/'select' reason.
            ["int"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["min"] = new NativeMember([GrobType.Int, GrobType.Int], GrobType.Int),
                ["max"] = new NativeMember([GrobType.Int, GrobType.Int], GrobType.Int),
                ["clamp"] = new NativeMember([GrobType.Int, GrobType.Int, GrobType.Int], GrobType.Int),
            },
            ["float"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["min"] = new NativeMember([GrobType.Float, GrobType.Float], GrobType.Float),
                ["max"] = new NativeMember([GrobType.Float, GrobType.Float], GrobType.Float),
                ["clamp"] = new NativeMember([GrobType.Float, GrobType.Float, GrobType.Float], GrobType.Float),
            },
            // Sprint 8 Increment E: formatAs — registered as a namespace NAME only, so
            // IsNamespace/RegisterNamespaces seed the usual NamespaceDecl sentinel (bare
            // `x := formatAs` correctly falls through to the generic D-342 E1004 arm) and
            // the plain function form (formatAs.table(x)) resolves its receiver via the
            // ordinary TryAnnotateNamespaceReceiver path. The three members (table/list/csv)
            // are NOT modelled here as ConstantMember/NativeMember — they need bespoke
            // per-call-site resolution (compile-time column derivation from the Sprint 6
            // field registry, and the chained-form receiver rewrite) that the generic
            // positional NativeMember model was never meant to stretch to. TypeChecker's
            // formatAs-specific resolution code (see ResolveFormatAsCall) never consults
            // this dictionary's "formatAs" entry — it exists purely for the namespace-name
            // membership check.
            ["formatAs"] = new Dictionary<string, object>(StringComparer.Ordinal),
        };

    /// <summary>
    /// Every registered namespace name. A concrete array, not <c>IEnumerable&lt;string&gt;</c> —
    /// <see cref="Dictionary{TKey, TValue}.KeyCollection"/>'s struct enumerator would box on
    /// every <c>foreach</c> through an interface-typed property (CodeRabbit review, PR #137),
    /// and <see cref="TypeChecker.RegisterNamespaces"/> iterates this unconditionally on every
    /// compile.
    /// </summary>
    internal static readonly string[] NamespaceNames = [.. _namespaces.Keys];

    /// <summary>
    /// Every registered <see cref="NativeMember"/>'s qualified name (<c>"{namespace}.{member}"</c>),
    /// flattened across every namespace — the set the agreement tests diff against live
    /// plugin registration (mirrors <c>PrimitiveMemberRegistry.AllQualifiedNativeNames</c>'s
    /// identical shape, D-370). A <see cref="ConstantMember"/> entry is skipped: it registers
    /// via <c>RegisterConstant</c>, not <c>RegisterNative</c>, so it is a different runtime
    /// signal this table's coverage-direction check should not expect a
    /// <c>RegisterNative</c> call for.
    /// </summary>
    internal static readonly string[] AllQualifiedNativeNames = [
        .. _namespaces.SelectMany(ns => ns.Value
            .Where(member => member.Value is NativeMember)
            .Select(member => $"{ns.Key}.{member.Key}")),
    ];

    // The hand table restated as signatures, built once (D-338), so the checker's
    // namespace call path reads one shape whether a member comes from the catalogue or
    // from here (D-426 change group 4). Parameter names are positional placeholders:
    // nothing reads them, since named arguments to a native are rejected (E0011), and
    // change group 5 replaces every entry with the plugin's own signature.
    private static readonly Dictionary<string, Dictionary<string, FunctionSignature>> _signatures =
        _namespaces.ToDictionary(
            ns => ns.Key,
            ns => ns.Value
                .Where(member => member.Value is NativeMember)
                .ToDictionary(
                    member => member.Key,
                    member => ToSignature($"{ns.Key}.{member.Key}", (NativeMember)member.Value),
                    StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static readonly Dictionary<string, Dictionary<string, SignatureType.Concrete>> _constantTypes =
        _namespaces.ToDictionary(
            ns => ns.Key,
            ns => ns.Value
                .Where(member => member.Value is ConstantMember)
                .ToDictionary(
                    member => member.Key,
                    member => ToConcrete((ConstantMember)member.Value),
                    StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static FunctionSignature ToSignature(string qualifiedName, NativeMember native) {
        var parameters = new List<SignatureParameter>(native.ParameterTypes.Count + 1);
        for (int i = 0; i < native.ParameterTypes.Count; i++) {
            string? namedTypeName = native.ParameterNamedTypeNames is { } names && i < names.Count ? names[i] : null;
            parameters.Add(new SignatureParameter(
                $"p{i + 1}", ConcreteOf(native.ParameterTypes[i], namedTypeName), defaultValue: native.ParameterDefaults?[i]));
        }
        if (native.VariadicElementType is GrobType variadicType) {
            parameters.Add(new SignatureParameter("rest", SignatureType.Of(variadicType), isVariadic: true));
        }
        return new FunctionSignature(qualifiedName, parameters, ConcreteOf(native.ReturnType, native.NamedTypeName));
    }

    private static SignatureType.Concrete ToConcrete(ConstantMember constant) =>
        ConcreteOf(constant.Type, constant.NamedTypeName);

    private static SignatureType.Concrete ConcreteOf(GrobType type, string? namedTypeName) =>
        namedTypeName is null ? SignatureType.Of(type) : SignatureType.Named(type, namedTypeName);

    /// <summary>Looks up the native <paramref name="memberName"/> of <paramref name="namespaceName"/> as a signature.</summary>
    internal static bool TryGetNativeSignature(
            string namespaceName, string memberName, [NotNullWhen(true)] out FunctionSignature? signature) =>
        TryGet(_signatures, namespaceName, memberName, out signature);

    /// <summary>Looks up the type of the constant <paramref name="memberName"/> of <paramref name="namespaceName"/>.</summary>
    internal static bool TryGetConstantType(
            string namespaceName, string memberName, [NotNullWhen(true)] out SignatureType.Concrete? type) =>
        TryGet(_constantTypes, namespaceName, memberName, out type);

    private static bool TryGet<T>(
            Dictionary<string, Dictionary<string, T>> table, string namespaceName, string memberName,
            [NotNullWhen(true)] out T? value) where T : class {
        value = null;
        return table.TryGetValue(namespaceName, out Dictionary<string, T>? members)
            && members.TryGetValue(memberName, out value);
    }

    /// <summary><see langword="true"/> when <paramref name="name"/> is a registered namespace.</summary>
    internal static bool IsNamespace(string name) => _namespaces.ContainsKey(name);

    /// <summary>
    /// Looks up <paramref name="memberName"/> on <paramref name="namespaceName"/>, returning
    /// a <see cref="ConstantMember"/> or <see cref="NativeMember"/> when found, or
    /// <see langword="null"/> when the namespace has no such member (the unknown-member
    /// case — E1003).
    /// </summary>
    internal static object? TryGetMember(string namespaceName, string memberName) =>
        _namespaces.TryGetValue(namespaceName, out IReadOnlyDictionary<string, object>? members) &&
        members.TryGetValue(memberName, out object? member)
            ? member
            : null;
}
