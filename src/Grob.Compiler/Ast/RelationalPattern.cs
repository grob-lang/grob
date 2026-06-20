using Grob.Core;

namespace Grob.Compiler.Ast;

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
