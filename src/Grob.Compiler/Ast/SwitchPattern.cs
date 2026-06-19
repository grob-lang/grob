using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A pattern in a switch-expression arm (§3.1, D-277). Three forms are legal in
/// v1: a <see cref="ValuePattern"/>, a <see cref="RelationalPattern"/> and the
/// catch-all <see cref="CatchAllPattern"/>.
/// </summary>
/// <param name="Range">Source range covered by the pattern.</param>
public abstract record SwitchPattern(SourceRange Range);

/// <summary>
/// A value pattern — a constant expression of the scrutinee's type (a literal, a
/// <c>const</c>-bound identifier, or <c>nil</c> when the scrutinee is nullable).
/// The arm matches when the scrutinee equals <paramref name="Value"/>.
/// </summary>
/// <param name="Range">Source range covered by the pattern.</param>
/// <param name="Value">The constant the scrutinee is compared against.</param>
public sealed record ValuePattern(SourceRange Range, Expression Value) : SwitchPattern(Range);

/// <summary>
/// A relational pattern — <c>&gt;= expr</c>, <c>&gt; expr</c>, <c>&lt;= expr</c> or
/// <c>&lt; expr</c> — legal only on an ordered scrutinee. The arm matches when
/// <c>scrutinee <paramref name="Op"/> <paramref name="Operand"/></c> holds.
/// </summary>
/// <param name="Range">Source range covered by the pattern.</param>
/// <param name="Op">The relational operator (<c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c>).</param>
/// <param name="Operand">The constant right-hand operand of the comparison.</param>
public sealed record RelationalPattern(
    SourceRange Range,
    BinaryOperator Op,
    Expression Operand) : SwitchPattern(Range);

/// <summary>The catch-all pattern <c>_</c>. Matches any value.</summary>
/// <param name="Range">Source range covered by the pattern.</param>
public sealed record CatchAllPattern(SourceRange Range) : SwitchPattern(Range);
