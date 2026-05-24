using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A member-access expression — <c>target.member</c>.</summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Target">The receiver expression.</param>
/// <param name="Member">The member name being accessed.</param>
public sealed record MemberAccessExpr(
    SourceRange Range,
    Expression Target,
    string Member) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitMemberAccess(this);
}
