using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A single <c>key: value</c> entry inside a map-literal expression (D-376).</summary>
/// <param name="Range">Source range covering the whole <c>"key": value</c> pair.</param>
/// <param name="Key">
/// The key as a decoded plain string (escape sequences already resolved) — mirrors
/// <see cref="FieldInit"/>'s convention of storing the extracted key as a string, not a
/// node, since v1 map keys are string-literal-only (no expression key is ever parsed).
/// </param>
/// <param name="Value">The supplied value expression.</param>
public sealed record MapEntry(SourceRange Range, string Key, Expression Value);
