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
/// <c>param</c> bindings are implicitly <c>readonly</c> (§24); parameter
/// <i>binding</i> — supplying and validating a value — is Sprint 10 (R-15).
/// </summary>
/// <param name="Range">Source range covered by the declaration, <b>including any
/// preceding decorator stack</b> (D-424 Decision 1). Before D-424 the range
/// started at the <c>param</c> keyword, which is why a diagnostic about a
/// decorator could not point at the declaration it belongs to.</param>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The mandatory declared type.</param>
/// <param name="DefaultValue">The default value expression, or <see langword="null"/>
/// when the parameter has no default.</param>
/// <param name="Decorators">The decorator stack in source order, empty when the
/// declaration is undecorated. Decorators are a <c>param</c>-only construct
/// (§19, D-424 Decision 2).</param>
public sealed record ParamDecl(
    SourceRange Range,
    string Name,
    TypeRef Type,
    Expression? DefaultValue,
    IReadOnlyList<Decorator> Decorators) : Declaration(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitParamDecl(this);
}
