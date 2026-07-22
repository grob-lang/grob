using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A binary expression — arithmetic, comparison, logical, or nil-coalescing.</summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Operator">The binary operator.</param>
/// <param name="Left">The left-hand operand.</param>
/// <param name="Right">The right-hand operand.</param>
public sealed record BinaryExpr(
    SourceRange Range,
    BinaryOperator Operator,
    Expression Left,
    Expression Right) : Expression(Range) {
    /// <summary>
    /// Set by the type checker (D-357/D-367) when <see cref="Operator"/> is
    /// <see cref="BinaryOperator.Equal"/> or <see cref="BinaryOperator.NotEqual"/> and
    /// both operands are nominally <c>date</c>. <see cref="GrobType.Struct"/> is a flat
    /// tag (D-303) that does not itself distinguish <c>date</c> from any other struct,
    /// and unlike the relational operators — whose <c>Struct</c>-vs-<c>Struct</c>
    /// acceptance is already gated to nominal <c>date</c> pairs only, so the flat tag
    /// safely implies <c>date</c> at the compiler's opcode-selection point — the
    /// checker's <c>==</c>/<c>!=</c> acceptance stays deliberately permissive for
    /// <em>any</em> matching struct pair (D-169 generic equality for user types), so
    /// <c>Struct</c> alone does not imply <c>date</c> there. This annotation is the
    /// compiler's signal for exactly that case, mirroring
    /// <see cref="MemberAccessExpr.ResolvedStructTypeName"/>'s checker-to-compiler
    /// handoff pattern. Always <see langword="false"/> for every other operator or
    /// operand pairing, including a <c>date</c>-vs-<c>date</c> relational comparison.
    /// </summary>
    public bool IsDateEquality { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitBinary(this);
}
