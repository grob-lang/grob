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
    /// <summary>A namespace constant member (e.g. <c>math.pi</c>) and its declared type.</summary>
    internal sealed record ConstantMember(GrobType Type);

    /// <summary>
    /// A namespace native member (e.g. <c>math.sqrt</c>) and its positional signature.
    /// v1 core-module natives take no named or defaulted arguments, so arity/type
    /// validation is a straight positional check — the smaller counterpart of the
    /// named-argument binding machinery <c>CheckCall</c> uses for user-defined <c>fn</c>s.
    /// <paramref name="VariadicElementType"/> is non-null for the one native that needs a
    /// variable-length tail (<c>path.join</c>, Sprint 8 Increment B): when set, at least one
    /// argument beyond <see cref="ParameterTypes"/>'s fixed prefix is required, and every
    /// argument from that point on is checked against this type instead of a fixed slot.
    /// </summary>
    internal sealed record NativeMember(
        IReadOnlyList<GrobType> ParameterTypes,
        GrobType ReturnType,
        GrobType? VariadicElementType = null);

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
        };

    /// <summary>Every registered namespace name.</summary>
    internal static IEnumerable<string> NamespaceNames => _namespaces.Keys;

    /// <summary>The number of registered namespaces, for global-scope dictionary pre-sizing.</summary>
    internal static int Count => _namespaces.Count;

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
