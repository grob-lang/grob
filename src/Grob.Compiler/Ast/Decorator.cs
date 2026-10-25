using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A single validation decorator attached to a <see cref="Declarations.ParamDecl"/>
/// (D-424 Decision 1): <c>"@" identifier [ "(" [ argument-list ] ")" ]</c>.
/// Decorators are a <c>param</c>-only construct (§19) — a decorator anywhere else
/// is <see cref="ErrorCatalog.E4002"/>.
/// <para>
/// Before D-424 a decorator stack was scanned and discarded by the parser, so
/// nothing downstream could see one and the whole decorator surface shipped
/// unvalidated. This node is what the type checker validates against §19's
/// seven-decorator table (D-411).
/// </para>
/// <para>
/// <b><see cref="Arguments"/> are ordinary expressions, deliberately.</b> The
/// restriction to literals is real, but it belongs to the type checker, not the
/// parse layer: <c>@minLength(x)</c> must produce a semantic diagnostic at the
/// argument's own position saying what is wrong with it, rather than a parse
/// error saying an expression was not expected. It also keeps §29 recovery
/// uniform — a malformed decorator is an ordinary malformed construct with no
/// bespoke recovery path. The type checker visits the arguments, so §3.1.1's
/// <c>ResolvedType</c>/<c>Declaration</c> invariant holds on any identifier
/// appearing in one, with D-311's sentinels on the error path.
/// </para>
/// </summary>
/// <param name="Range">Source range covering the whole decorator, from its
/// <c>@</c> through its closing <c>)</c> when it has an argument list.</param>
/// <param name="Name">The decorator name, without the leading <c>@</c>.</param>
/// <param name="Arguments">The argument expressions in source order; empty when
/// the decorator has no argument list, or an empty one.</param>
public sealed record Decorator(
    SourceRange Range,
    string Name,
    IReadOnlyList<Expression> Arguments) : AstNode(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitDecorator(this);
}
