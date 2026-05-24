using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A variable declaration via <c>:=</c> with optional type annotation.
/// Always introduces a new mutable binding in the current scope.
/// </summary>
/// <param name="Range">Source range covered by the declaration statement.</param>
/// <param name="Name">The bound variable name.</param>
/// <param name="AnnotatedType">The optional declared type — <c>name: Type := value</c>.</param>
/// <param name="Initializer">The required initialiser expression.</param>
public sealed record VarDeclStmt(
    SourceRange Range,
    string Name,
    TypeRef? AnnotatedType,
    Expression Initializer) : Statement(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitVarDecl(this);
}
