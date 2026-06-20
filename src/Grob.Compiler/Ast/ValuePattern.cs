using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A value pattern — a constant expression of the scrutinee's type (a literal, a
/// <c>const</c>-bound identifier, or <c>nil</c> when the scrutinee is nullable).
/// The arm matches when the scrutinee equals <paramref name="Value"/>.
/// </summary>
/// <param name="Range">Source range covered by the pattern.</param>
/// <param name="Value">The constant the scrutinee is compared against.</param>
public sealed record ValuePattern(SourceRange Range, Expression Value) : SwitchPattern(Range);
