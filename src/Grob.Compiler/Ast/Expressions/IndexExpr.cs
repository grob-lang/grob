using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>An indexed access expression — <c>target[index]</c>.</summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Target">The collection being indexed.</param>
/// <param name="Index">The index expression.</param>
public sealed record IndexExpr(
    SourceRange Range,
    Expression Target,
    Expression Index) : Expression(Range) {
    /// <summary>
    /// The <see cref="GrobType"/> of the indexed element, set by the type checker.
    /// Defaults to <see cref="GrobType.Unknown"/> until type checking completes
    /// (e.g. a map receiver, whose value type is not tracked).
    /// </summary>
    public GrobType ElementType { get; set; } = GrobType.Unknown;

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIndex(this);
}
