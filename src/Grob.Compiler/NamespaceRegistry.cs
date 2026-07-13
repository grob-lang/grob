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
    /// </summary>
    internal sealed record NativeMember(IReadOnlyList<GrobType> ParameterTypes, GrobType ReturnType);

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> _namespaces =
        new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.Ordinal) {
            ["math"] = new Dictionary<string, object>(StringComparer.Ordinal) {
                ["pi"] = new ConstantMember(GrobType.Float),
                ["sqrt"] = new NativeMember([GrobType.Float], GrobType.Float),
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
