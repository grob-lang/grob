namespace Grob.Compiler.Ast;

/// <summary>
/// Empty value type used as the return type of <see cref="AstWalker"/> —
/// a stand-in for <c>void</c> that lets the walker plug into the generic
/// <see cref="AstVisitor{T}"/> machinery without inventing a parallel
/// dispatch surface.
/// </summary>
public readonly record struct Unit;
