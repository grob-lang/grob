using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// Base type for every statement node — anything executed for its effect.
/// </summary>
/// <param name="Range">The source range covered by the statement.</param>
public abstract record Statement(SourceRange Range) : AstNode(Range);
