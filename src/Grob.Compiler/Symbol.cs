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
}
