using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// A resolved field entry produced by the Sprint 6 A type-declaration pass.
/// Carries the compiler-visible field type and, for struct-typed fields, the
/// declared type name so the §17.1 cycle-detection DFS can follow the edge.
/// </summary>
/// <param name="Name">Field name as declared.</param>
/// <param name="Kind">Resolved <see cref="GrobType"/> for the field's annotation.</param>
/// <param name="NamedTypeName">Non-null iff <paramref name="Kind"/> is
///   <see cref="GrobType.Struct"/> or <see cref="GrobType.NullableStruct"/>.</param>
/// <param name="Range">Source range of the field declaration.</param>
/// <param name="IsRequired"><see langword="true"/> when the field has no default value.</param>
/// <param name="FunctionDescriptor">Structural descriptor for the field's function type
///   when <paramref name="Kind"/> is <see cref="GrobType.Function"/> or
///   <see cref="GrobType.NullableFunction"/>; <see langword="null"/> otherwise.
///   Used by the type checker to enforce structural compatibility (D-326) for function-
///   typed field defaults and construction values.</param>
internal record ResolvedFieldInfo(
    string Name,
    GrobType Kind,
    string? NamedTypeName,
    SourceRange Range,
    bool IsRequired,
    FunctionTypeDescriptor? FunctionDescriptor = null);

/// <summary>
/// A registered user-defined type with its fully resolved field list.
/// Populated during the pass-2 visit to <c>type</c> declarations.
/// </summary>
internal class UserTypeInfo {
    public required string Name { get; init; }
    public required IReadOnlyList<ResolvedFieldInfo> Fields { get; init; }
    public required SourceRange Range { get; init; }
}

/// <summary>
/// Maps user-defined type names to their resolved <see cref="UserTypeInfo"/>.
/// Scoped to a single compilation unit; one instance per <see cref="TypeChecker"/>
/// run.
/// </summary>
internal class UserTypeRegistry {
    private readonly Dictionary<string, UserTypeInfo> _types =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Registers <paramref name="info"/> under its <see cref="UserTypeInfo.Name"/>.
    /// When the name is already present the first registration is kept authoritative
    /// so that duplicate-type E1102 does not displace the first definition's fields
    /// from the phase-2.5 cycle-detection graph.
    /// </summary>
    public void Register(UserTypeInfo info) => _types.TryAdd(info.Name, info);

    /// <summary>Returns the type info for <paramref name="name"/>, or <see langword="null"/>.</summary>
    public UserTypeInfo? TryGet(string name) => _types.GetValueOrDefault(name);

    /// <summary>All registered types, in registration order.</summary>
    public IEnumerable<UserTypeInfo> AllTypes => _types.Values;
}
