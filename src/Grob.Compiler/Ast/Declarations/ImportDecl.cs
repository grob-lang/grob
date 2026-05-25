using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>A top-level <c>import</c> declaration with optional <c>as</c> alias.</summary>
/// <param name="Range">Source range covered by the declaration.</param>
/// <param name="ModulePath">The module path text exactly as it appeared in source.</param>
/// <param name="Alias">The optional alias supplied with <c>as Name</c>.</param>
public sealed record ImportDecl(
    SourceRange Range,
    string ModulePath,
    string? Alias) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitImportDecl(this);
}
