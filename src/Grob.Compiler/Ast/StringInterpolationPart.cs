using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// One part of an <see cref="InterpolatedStringExpr"/>: either a literal text
/// segment (<see cref="StringTextPart"/>) or an embedded expression
/// (<see cref="StringExpressionPart"/>).
/// </summary>
/// <param name="Range">Source range covered by the part.</param>
public abstract record StringInterpolationPart(SourceRange Range);
