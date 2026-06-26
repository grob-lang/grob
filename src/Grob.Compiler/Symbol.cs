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
}
