using Grob.Compiler.Ast;
using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// An entry in the type checker's symbol table. Records the declared name,
/// resolved type, declaration site and the AST node that introduced the binding.
/// Set by the type checker; read by the compiler and (later) the LSP.
/// </summary>
public sealed class Symbol {
    /// <summary>The declared name exactly as it appears in source.</summary>
    public required string Name { get; init; }

    /// <summary>The resolved <see cref="GrobType"/> of this binding.</summary>
    public required GrobType Type { get; init; }

    /// <summary>
    /// The source location of the declaration statement or declaration node.
    /// Corresponds to the <c>DeclaredAt</c> field described in §3.1.1.
    /// </summary>
    public required SourceLocation DeclaredAt { get; init; }

    /// <summary>
    /// The AST node that introduced this binding — the <see cref="VarDeclStmt"/>,
    /// <see cref="FnDecl"/>, <see cref="ConstDecl"/>, <see cref="ReadonlyDecl"/>,
    /// or <see cref="TypeDecl"/> node. This is what <see cref="IdentifierExpr.Declaration"/>
    /// is set to for every identifier that resolves to this symbol.
    /// </summary>
    public required AstNode DeclarationNode { get; init; }

    /// <summary>
    /// <see langword="true"/> when this entry is a pass-1 forward-reference placeholder
    /// for a top-level value binding (D-321), registered before its initialiser type is
    /// known so a function body declared earlier can resolve it (D-166). Pass 2 re-registers
    /// the binding with its inferred type and clears this flag, so the same-scope
    /// redeclaration check (E1102) treats the pass-1 placeholder as not-yet-declared
    /// rather than a duplicate.
    /// </summary>
    public bool Provisional { get; init; }

    /// <summary>
    /// When <see cref="Type"/> is <see cref="GrobType.Function"/> or
    /// <see cref="GrobType.NullableFunction"/>, carries the structural descriptor
    /// (parameter types and return type). <see langword="null"/> for all other types
    /// and for <c>fn</c> declarations, whose return type is tracked separately in
    /// the type-checker's return-type stack (D-326).
    /// </summary>
    public FunctionTypeDescriptor? FunctionDescriptor { get; init; }

    /// <summary>
    /// When <see cref="Type"/> is <see cref="GrobType.Struct"/> or
    /// <see cref="GrobType.NullableStruct"/>, carries the declared user type name so
    /// member access on this binding can resolve fields via the type registry
    /// (mirrors <see cref="FunctionDescriptor"/> for function types). Set for
    /// struct-typed parameters, whose declaration node is the owning <c>FnDecl</c>
    /// rather than the <c>Parameter</c> itself, so the name cannot be recovered from
    /// <see cref="DeclarationNode"/> alone. <see langword="null"/> for all other
    /// types and for <c>:=</c>-inferred struct bindings, which resolve their type
    /// name from the initialiser expression instead.
    /// </summary>
    public string? NamedStructTypeName { get; init; }

    /// <summary>
    /// When <see cref="Type"/> is <see cref="GrobType.Array"/> (or its nullable variant)
    /// and the array's declared element type is a named user <c>type</c>, carries that
    /// element type's name — the array analogue of <see cref="NamedStructTypeName"/>,
    /// which <c>ResolveSignatureType</c>'s <c>ArrayTypeRef</c> arm otherwise discards
    /// (Sprint 8 Increment E, <c>formatAs</c>'s compile-time column derivation). Set only
    /// for a <c>T[]</c>-annotated parameter, whose declaration node is the owning
    /// <c>FnDecl</c> rather than the <c>Parameter</c> itself — mirrors why
    /// <see cref="NamedStructTypeName"/> is threaded onto the symbol rather than recovered
    /// from <see cref="DeclarationNode"/>. <see langword="null"/> for a <c>:=</c>-inferred
    /// array local, whose element shape is instead resolved by peeking the initialiser
    /// expression directly (an array literal, a struct-array-returning call, or an
    /// explicit <c>T[]</c> annotation on the binding itself).
    /// </summary>
    public string? ArrayElementStructTypeName { get; init; }
}
