using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>The catch-all pattern <c>_</c>. Matches any value.</summary>
/// <param name="Range">Source range covered by the pattern.</param>
public sealed record CatchAllPattern(SourceRange Range) : SwitchPattern(Range);
