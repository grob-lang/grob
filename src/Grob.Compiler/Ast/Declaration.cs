using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// Base type for every top-level declaration node — <c>fn</c>, <c>type</c>,
/// <c>param</c>, <c>import</c>, <c>const</c>, <c>readonly</c>.
/// </summary>
/// <param name="Range">The source range covered by the declaration.</param>
public abstract record Declaration(SourceRange Range) : AstNode(Range);
