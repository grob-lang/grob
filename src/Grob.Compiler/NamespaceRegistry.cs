using Grob.Compiler.Ast.Declarations;
using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// The compile-time member table for every core stdlib module namespace (D-342). Hand
/// authored, one entry per namespace member, updated in lockstep with the corresponding
/// runtime <c>IGrobPlugin</c> registration in <c>Grob.Stdlib</c> — the same
/// two-hand-maintained-mirrors-plus-agreement-test shape D-308 established for
/// <see cref="ErrorCatalog"/> against <c>grob-error-codes.md</c>. <c>Grob.Compiler</c> has
/// no reference to <c>Grob.Stdlib</c> (the DAG forbids it), so this table — not a shared
/// runtime registry — is what the type checker validates a namespace member access
/// against; a native's actual C# implementation is resolved only at VM startup, by
/// qualified name, via <c>GetGlobal</c>.
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

    /// <summary>The number of registered namespaces, for global-scope dictionary pre-sizing.</summary>
    internal static int Count => _namespaces.Count;

    // Per-name TypeChecker.RegisterNamespaces registration objects, built once and shared
    // across every compile — mirrors ExceptionHierarchy's _typeDecls/_symbols caching
    // (D-338). TypeChecker.Check runs RegisterNamespaces unconditionally on every compile
    // regardless of whether the source references any namespace; NamespaceDecl is an
    // immutable record and Symbol carries identical content (Type is always
    // GrobType.Unknown, DeclaredAt is always SourceLocation.Unknown, DeclarationNode is the
    // shared NamespaceDecl below) on every run, so re-synthesising 7 NamespaceDecl + 7
    // Symbol objects per compile was a fixed allocation the smallest compile benchmarks pay
    // disproportionately for (GitHub Actions run 29392344084 — Compile_TwoExpressions
    // breached the per-sprint gate on both time and allocation).
    private static readonly IReadOnlyDictionary<string, NamespaceDecl> _namespaceDecls =
        _namespaces.Keys.ToDictionary(name => name, name => new NamespaceDecl(name), StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, Symbol> _symbols =
        _namespaces.Keys.ToDictionary(
            name => name,
            name => new Symbol {
                Name = name,
                Type = GrobType.Unknown,
                DeclaredAt = SourceLocation.Unknown,
                DeclarationNode = _namespaceDecls[name],
            },
            StringComparer.Ordinal);

    /// <summary>The shared <see cref="Symbol"/> for <paramref name="name"/>.</summary>
    internal static Symbol SymbolFor(string name) => _symbols[name];

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
