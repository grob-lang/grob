using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A function-call expression — <c>callee(arg, arg, ...)</c>.</summary>
/// <param name="Range">Source range covered by the whole call.</param>
/// <param name="Callee">The callee expression.</param>
/// <param name="Arguments">The arguments in source order, possibly with names attached.</param>
public sealed record CallExpr(
    SourceRange Range,
    Expression Callee,
    IReadOnlyList<CallArgument> Arguments) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitCall(this);
}
