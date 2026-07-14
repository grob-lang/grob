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
    /// <summary>
    /// Set by the type checker (Sprint 8 Increment E) when this call resolves to a
    /// <c>formatAs.table</c>/<c>list</c>/<c>csv</c> call — the function form
    /// (<c>formatAs.table(items)</c>) or the chained form (<c>items.formatAs.table()</c>),
    /// both resolved through the same <c>ResolveFormatAsCall</c>. Carries the ordered
    /// column-name list the compile-time field-registry lookup derived (possibly empty,
    /// never <see langword="null"/> once set), so the compiler can emit it as the
    /// synthesised second argument without re-deriving it — the runtime native never
    /// reflects over the value. <see langword="null"/> for every other call.
    /// </summary>
    public IReadOnlyList<string>? ResolvedFormatAsColumns { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitCall(this);
}
