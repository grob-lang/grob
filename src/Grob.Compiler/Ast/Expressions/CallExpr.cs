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

    /// <summary>
    /// Set by the type checker (D-362) to this call's statically resolved return type —
    /// mirroring <see cref="IndexExpr.ElementType"/> (D-359) and
    /// <see cref="MemberAccessExpr.ResolvedFieldType"/> — at every call shape whose return
    /// type is known: a direct user <c>FnDecl</c> call, a function-typed-variable call, a
    /// namespace-qualified native call, and a registered-named-type instance-method call.
    /// Stays <see cref="GrobType.Unknown"/> (the default) for a call whose result is
    /// genuinely unresolvable statically — a void-returning array higher-order method
    /// (<c>each</c>) or a call on an <c>Unknown</c>-typed receiver. The compiler's
    /// <c>GetExprType</c> reads this field directly rather than re-deriving the type.
    /// </summary>
    public GrobType ResolvedReturnType { get; set; } = GrobType.Unknown;

    /// <summary>
    /// Set by the type checker when this call resolves to a primitive-receiver
    /// instance-method call (D-066's compile-time-sugar model, <c>PrimitiveMemberRegistry</c>) —
    /// the qualified native name (e.g. <c>"string.split"</c>) the compiler rewrites the
    /// call to, receiver injected as arg[0]. <see langword="null"/> for every other call.
    /// </summary>
    public string? ResolvedPrimitiveNativeName { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitCall(this);
}
