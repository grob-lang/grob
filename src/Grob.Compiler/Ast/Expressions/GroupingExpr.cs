using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A parenthesised expression — preserved in the AST for pretty-printing fidelity.</summary>
/// <param name="Range">Source range covered by the parentheses and the inner expression.</param>
/// <param name="Inner">The grouped expression.</param>
public sealed record GroupingExpr(SourceRange Range, Expression Inner) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitGrouping(this);
}
