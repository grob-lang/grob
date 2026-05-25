using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// Root of the Grob AST hierarchy. Every node carries the <see cref="SourceRange"/>
/// it was parsed from and dispatches to an <see cref="AstVisitor{T}"/> via
/// <see cref="Accept{T}(AstVisitor{T})"/>.
/// </summary>
/// <param name="Range">The source range covered by this node.</param>
public abstract record AstNode(SourceRange Range) {
    /// <summary>
    /// Dispatches to the appropriate <c>VisitXxx</c> method on <paramref name="visitor"/>.
    /// </summary>
    /// <typeparam name="T">The visitor's return type.</typeparam>
    /// <param name="visitor">The visitor to dispatch to. Must not be <see langword="null"/>.</param>
    /// <returns>The value returned by the visitor.</returns>
    public abstract T Accept<T>(AstVisitor<T> visitor);
}
