using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>
/// A sentinel declaration node used by the type checker to pre-register
/// built-in functions (<c>print</c>, <c>exit</c>, <c>input</c>) in the
/// global scope before the first pass runs (D-270).
/// Not produced by the parser and never part of a <see cref="CompilationUnit"/>'s
/// <c>TopLevel</c> list. It is stored as the <see cref="Symbol.DeclarationNode"/>
/// of a built-in symbol so that go-to-definition in the LSP has a typed node
/// to return rather than <see langword="null"/>.
/// </summary>
/// <param name="BuiltinName">The built-in function name exactly as it appears in source.</param>
public sealed record BuiltinDecl(string BuiltinName) : Declaration(SourceRange.Unknown) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitBuiltinDecl(this);
}
