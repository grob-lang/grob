using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A single <c>case</c> clause inside a <see cref="SelectStmt"/>. One clause
/// can list multiple patterns (<c>case A, B, C { ... }</c>).
/// </summary>
/// <param name="Range">Source range covered by the clause.</param>
/// <param name="Patterns">The pattern expressions; never empty.</param>
/// <param name="Body">The block executed when any pattern matches.</param>
public sealed record CaseClause(
    SourceRange Range,
    IReadOnlyList<Expression> Patterns,
    BlockStmt Body);
