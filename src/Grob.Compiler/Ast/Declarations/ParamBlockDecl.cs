using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A top-level <c>param</c> block declaring script-level parameters.</summary>
/// <param name="Range">Source range covered by the whole block.</param>
/// <param name="Parameters">The declared script parameters in source order.</param>
public sealed record ParamBlockDecl(
    SourceRange Range,
    IReadOnlyList<Parameter> Parameters) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitParamBlockDecl(this);
}
