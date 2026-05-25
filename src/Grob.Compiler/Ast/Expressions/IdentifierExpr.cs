using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A bare identifier reference — name resolution happens in Sprint 2+.</summary>
/// <param name="Range">Source range covered by the identifier.</param>
/// <param name="Name">The identifier text exactly as it appeared in source.</param>
public sealed record IdentifierExpr(SourceRange Range, string Name) : Expression(Range) {
    /// <summary>
    /// The resolved type of this identifier. Set by the type checker in Sprint 2;
    /// always <see cref="GrobType.Unknown"/> after Sprint 1 parsing.
    /// </summary>
    public GrobType ResolvedType { get; set; } = GrobType.Unknown;

    /// <summary>
    /// The AST node that declared the name this identifier refers to. Set by the
    /// type checker in Sprint 2; <see langword="null"/> after Sprint 1 parsing.
    /// </summary>
    public AstNode? Declaration { get; set; }

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitIdentifier(this);
}
