using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>
/// Singleton sentinel assigned to <see cref="Expressions.IdentifierExpr.Declaration"/>
/// by the type checker when the identifier could not be resolved (D-311). Symmetric with
/// <see cref="GrobType.Error"/> on the type side (§29.3): one shared instance,
/// allocation-free at every error path, no per-error synthetic node required.
/// </summary>
/// <remarks>
/// The LSP go-to-definition handler checks for
/// <c>ReferenceEquals(node.Declaration, UnresolvedDecl.Instance)</c> and returns an
/// "unresolved" response rather than a null pointer. Code that only needs to distinguish
/// "resolved" from "not resolved" uses <c>node.Declaration is not UnresolvedDecl</c>;
/// code that only needs to confirm the §3.1.1 non-null invariant holds works
/// unchanged — <see cref="Instance"/> satisfies a null-check.
/// </remarks>
public sealed record UnresolvedDecl() : Declaration(SourceRange.Unknown) {
    /// <summary>The one shared instance. All unresolved identifiers share this reference.</summary>
    public static readonly UnresolvedDecl Instance = new();

    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitUnresolvedDecl(this);
}
