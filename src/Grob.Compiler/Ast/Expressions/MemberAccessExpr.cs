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
    /// <summary>
    /// The <see cref="GrobType"/> of the accessed field, set by the type checker.
    /// Defaults to <see cref="GrobType.Unknown"/> until type checking completes.
    /// </summary>
    public GrobType ResolvedFieldType { get; set; } = GrobType.Unknown;

    /// <summary>
    /// When <see cref="ResolvedFieldType"/> is <see cref="GrobType.Struct"/>,
    /// holds the declared type name of the field so that a nested access
    /// (<c>a.b.c</c>) can resolve the next step via the <see cref="UserTypeRegistry"/>.
    /// </summary>
    public string? ResolvedStructTypeName { get; set; }

    /// <summary>
    /// Set by the type checker when this bare (non-call) access resolves to a
    /// primitive-receiver instance property (D-066, <c>PrimitiveMemberRegistry</c>) —
    /// the qualified native name (e.g. <c>"string.length"</c>) the compiler rewrites the
    /// access to, called with the receiver as its sole argument. <see langword="null"/>
    /// for every other access.
    /// </summary>
    public string? ResolvedPrimitiveNativeName { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitMemberAccess(this);
}
