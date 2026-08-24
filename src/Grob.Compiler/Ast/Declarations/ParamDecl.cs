using Grob.Core;

namespace Grob.Compiler.Ast.Declarations;

/// <summary>
/// A single top-level <c>param</c> declaration (D-410). One node per <c>param</c>
/// keyword — there is no enclosing block, so unlike <c>TypeDecl</c>'s fields or a
/// function's parameters this is not a group node. Consecutive <c>param</c>
/// declarations form a <b>parameter group by contiguity</b> (§19 of
/// <c>grob-language-fundamentals.md</c>), which is a parsing-loop property, not
/// an AST relationship — exactly as consecutive <c>import</c> declarations are
/// each their own <see cref="ImportDecl"/>. The type annotation is mandatory
/// (parameters are never inferred), so <see cref="Type"/> is non-nullable, unlike
/// <see cref="Parameter.Type"/> which is nullable to allow lambda inference.
/// <c>param</c> bindings are implicitly <c>readonly</c> (§24); decorator capture
/// and parameter binding are Sprint 10.
/// </summary>
/// <param name="Range">Source range covered by the declaration (from the <c>param</c>
/// keyword, not any preceding decorator stack — decorators are parsed and skipped,
/// not yet captured into the AST).</param>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The mandatory declared type.</param>
/// <param name="DefaultValue">The default value expression, or <see langword="null"/>
/// when the parameter has no default.</param>
public sealed record ParamDecl(
    SourceRange Range,
    string Name,
    TypeRef Type,
    Expression? DefaultValue) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitParamDecl(this);
}
