using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// Base type for every expression node — anything that produces a value.
/// </summary>
/// <param name="Range">The source range covered by the expression.</param>
public abstract record Expression(SourceRange Range) : AstNode(Range);
