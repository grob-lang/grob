using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A placeholder produced where an expression was expected but the parser could
/// not produce one. First-class per §29.2 of the language fundamentals — every
/// visitor must handle it.
/// </summary>
/// <param name="Range">Source range from the first unexpected token to the recovery anchor (exclusive).</param>
/// <param name="Diagnostic">The diagnostic that produced this node.</param>
public sealed record ErrorExpr(
    SourceRange Range,
    Diagnostic Diagnostic) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitErrorExpr(this);
}
