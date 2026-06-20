using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A pattern in a switch-expression arm (§3.1, D-277). Three forms are legal in
/// v1: a <see cref="ValuePattern"/>, a <see cref="RelationalPattern"/> and the
/// catch-all <see cref="CatchAllPattern"/>.
/// </summary>
/// <param name="Range">Source range covered by the pattern.</param>
public abstract record SwitchPattern(SourceRange Range);
