using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>
/// A placeholder produced where a top-level declaration was expected but the
/// parser could not produce one. First-class per §29.2 — every visitor must
/// handle it.
/// </summary>
/// <param name="Range">Source range from the first unexpected token to the recovery anchor (exclusive).</param>
/// <param name="Diagnostic">The diagnostic that produced this node.</param>
public sealed record ErrorDecl(
    SourceRange Range,
    Diagnostic Diagnostic) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitErrorDecl(this);
}
