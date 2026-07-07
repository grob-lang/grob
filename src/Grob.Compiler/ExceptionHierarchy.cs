using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// The Sprint 7 v1 exception hierarchy (D-284): <c>GrobError</c> is the root; ten
/// leaves are its direct children. These are built-in nominal types, not user
/// <c>type</c> declarations — <see cref="TypeChecker.RegisterExceptionHierarchy"/>
/// seeds them into the symbol table and the user-type registry before pass 1 runs,
/// so the existing Sprint 6B construction path (<c>ResolveConstructionTypeName</c>,
/// <c>TypeCheckFieldValues</c>, <c>CollectSuppliedFields</c>,
/// <c>EmitMissingFieldErrors</c>) resolves and constructs them completely
/// unmodified (D-043).
/// </summary>
internal static class ExceptionHierarchy {
    /// <summary>The root of the hierarchy.</summary>
    internal const string Root = "GrobError";

    // Every name maps to its direct parent; the root maps to null. Declared
    // root-first, then leaves in D-284's declared order — nothing in this
    // increment's registration actually depends on that order (each entry is
    // seeded independently), but it documents the intent for whoever reads it
    // alongside D-284's diagram.
    private static readonly Dictionary<string, string?> _parents = new(StringComparer.Ordinal) {
        [Root] = null,
        ["IoError"] = Root,
        ["NetworkError"] = Root,
        ["JsonError"] = Root,
        ["ProcessError"] = Root,
        ["NilError"] = Root,
        ["ArithmeticError"] = Root,
        ["IndexError"] = Root,
        ["ParseError"] = Root,
        ["LookupError"] = Root,
        ["RuntimeError"] = Root,
    };

    /// <summary>Every hierarchy type name: the root, then the ten leaves.</summary>
    internal static IReadOnlyCollection<string> AllNames => _parents.Keys;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="from"/> is
    /// <paramref name="to"/> or a (transitive) subtype of it — walked
    /// from → parent → … → root → <see langword="null"/>, reflexive on an exact
    /// name match. Never a flat/exact-match special case: an unrecognised
    /// <paramref name="from"/> (not a hierarchy member at all) walks zero steps
    /// and returns <see langword="false"/> unless it equals <paramref name="to"/>
    /// directly, so an arbitrary struct type is rejected without bespoke logic —
    /// and a deeper post-MVP hierarchy needs no rewrite here, only more entries in
    /// <see cref="_parents"/>.
    /// </summary>
    internal static bool IsSubtypeOf(string from, string to) {
        string? current = from;
        while (current is not null) {
            if (string.Equals(current, to, StringComparison.Ordinal)) return true;
            current = _parents.GetValueOrDefault(current);
        }
        return false;
    }

    // -----------------------------------------------------------------------
    // D-274 field shapes. 'location' carries GrobType.Unknown — there is no
    // Grob-nameable type for SourceLocation? anywhere in the spec corpus, so it
    // is represented the same permissive way TypeChecker.RegisterBuiltinFn
    // already represents print/exit/input. TypeCheckFieldValues's existing
    // 'fieldInfo.Kind != GrobType.Unknown' guard skips assignability checking for
    // it with zero special-casing. The runtime overwrites it unconditionally
    // when Throw executes (VirtualMachine.cs) — the checker permits a
    // user-supplied 'location' at a construction site (so the field is
    // nameable) but does not enforce it.
    // -----------------------------------------------------------------------

    private static readonly ResolvedFieldInfo MessageField =
        new("message", GrobType.String, null, SourceRange.Unknown, IsRequired: true);

    private static readonly ResolvedFieldInfo LocationField =
        new("location", GrobType.Unknown, null, SourceRange.Unknown, IsRequired: false);

    private static readonly ResolvedFieldInfo StatusCodeField =
        new("statusCode", GrobType.NullableInt, null, SourceRange.Unknown, IsRequired: false);

    private static readonly IReadOnlyList<ResolvedFieldInfo> _defaultFields = [MessageField, LocationField];
    private static readonly IReadOnlyList<ResolvedFieldInfo> _networkErrorFields =
        [MessageField, LocationField, StatusCodeField];

    /// <summary>
    /// The resolved field list for <paramref name="name"/> (D-274): every
    /// hierarchy member carries <c>message</c> and the runtime-set
    /// <c>location</c>; <c>NetworkError</c> additionally carries
    /// <c>statusCode</c>.
    /// </summary>
    internal static IReadOnlyList<ResolvedFieldInfo> FieldsFor(string name) =>
        name == "NetworkError" ? _networkErrorFields : _defaultFields;

    // AST TypeField mirrors of the fields above, used only to synthesise the
    // TypeDecl node the Sprint 6B construction path (Compiler.VisitStructConstruction)
    // reads to emit field values in declaration order. 'location' and
    // 'statusCode' carry a nil default so an omitted optional field still has
    // something to emit at a construction site that does not supply it — the VM
    // overwrites 'location' unconditionally when Throw executes regardless.

    private static readonly TypeField MessageTypeField =
        new(SourceRange.Unknown, "message", new TypeRef(SourceRange.Unknown, "string", [], false), null);

    private static readonly TypeField LocationTypeField =
        new(SourceRange.Unknown, "location",
            new TypeRef(SourceRange.Unknown, "SourceLocation", [], true),
            new NilLiteralExpr(SourceRange.Unknown));

    private static readonly TypeField StatusCodeTypeField =
        new(SourceRange.Unknown, "statusCode",
            new TypeRef(SourceRange.Unknown, "int", [], true),
            new NilLiteralExpr(SourceRange.Unknown));

    private static readonly IReadOnlyList<TypeField> _defaultTypeFields = [MessageTypeField, LocationTypeField];
    private static readonly IReadOnlyList<TypeField> _networkErrorTypeFields =
        [MessageTypeField, LocationTypeField, StatusCodeTypeField];

    /// <summary>
    /// The synthesised <see cref="TypeField"/> list for <paramref name="name"/>,
    /// mirroring <see cref="FieldsFor"/> — used to build the sentinel
    /// <see cref="TypeDecl"/> each hierarchy member is registered with.
    /// </summary>
    internal static IReadOnlyList<TypeField> TypeFieldsFor(string name) =>
        name == "NetworkError" ? _networkErrorTypeFields : _defaultTypeFields;
}
