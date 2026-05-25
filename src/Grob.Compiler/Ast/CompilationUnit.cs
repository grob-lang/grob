using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// The root of a parsed Grob source file. Holds the top-level items in source
/// order — top-level declarations (<c>fn</c>, <c>type</c>, <c>param</c>,
/// <c>import</c>, <c>const</c>, <c>readonly</c>), top-level statements
/// (e.g. <c>x := 1</c> at file scope as in §29.6), or <see cref="ErrorDecl"/>
/// placeholders produced by the error-recovering parser.
/// </summary>
/// <param name="Range">Source range from the start of the file to EOF.</param>
/// <param name="TopLevel">The top-level items in source order. Each is a <see cref="Declaration"/> or a <see cref="Statement"/>.</param>
public sealed record CompilationUnit(
    SourceRange Range,
    IReadOnlyList<AstNode> TopLevel) : AstNode(Range) {
    /// <inheritdoc/>
    public override T Accept<T>(AstVisitor<T> visitor) => visitor.VisitCompilationUnit(this);
}
