using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>
/// A member-access expression — <c>target.member</c> or
/// <c>target?.member</c> (optional chaining, Sprint 3 Increment D).
/// </summary>
/// <param name="Range">Source range covered by the whole expression.</param>
/// <param name="Target">The receiver expression.</param>
/// <param name="Member">The member name being accessed.</param>
/// <param name="IsOptional">
/// <c>true</c> when the operator is <c>?.</c> (optional-chaining access);
/// <c>false</c> for ordinary <c>.</c> access.
/// </param>
public sealed record MemberAccessExpr(
    SourceRange Range,
    Expression Target,
    string Member,
    bool IsOptional = false) : Expression(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitMemberAccess(this);
}
