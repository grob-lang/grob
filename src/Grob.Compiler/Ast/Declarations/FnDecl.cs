using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A top-level <c>fn</c> declaration.</summary>
/// <param name="Range">Source range covered by the whole declaration.</param>
/// <param name="Name">The function name.</param>
/// <param name="Parameters">The declared parameters.</param>
/// <param name="ReturnType">The declared return type. Required in v1.</param>
/// <param name="Body">The function body.</param>
public sealed record FnDecl(
    SourceRange Range,
    string Name,
    IReadOnlyList<Parameter> Parameters,
    TypeRef ReturnType,
    BlockStmt Body) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitFnDecl(this);
}
