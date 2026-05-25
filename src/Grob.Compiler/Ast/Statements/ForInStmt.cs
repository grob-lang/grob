using Grob.Core;

namespace Grob.Compiler.Ast.Statements;

/// <summary>
/// A <c>for ... in ...</c> loop. Supports the one-variable form
/// (<c>for x in xs</c>), the index form (<c>for i, x in xs</c>) and the
/// map form (<c>for k, v in m</c>). Numeric ranges arrive as a
/// <see cref="NumericRangeExpr"/> in <see cref="Iterable"/>.
/// </summary>
/// <param name="Range">Source range covered by the whole loop.</param>
/// <param name="Variables">The loop variables — one or two names.</param>
/// <param name="Iterable">The expression being iterated over.</param>
/// <param name="Body">The loop body.</param>
public sealed record ForInStmt(
    SourceRange Range,
    IReadOnlyList<string> Variables,
    Expression Iterable,
    BlockStmt Body) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitForIn(this);
}
