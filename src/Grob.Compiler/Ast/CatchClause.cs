using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A single <c>catch</c> clause attached to a <see cref="TryStmt"/>.
/// </summary>
/// <param name="Range">Source range covered by the clause.</param>
/// <param name="ExceptionType">The declared exception type to match, or <see langword="null"/> for a catch-all.</param>
/// <param name="ExceptionVariable">The bound exception variable, or <see langword="null"/> when no name is captured.</param>
/// <param name="Body">The block executed when the clause matches.</param>
public sealed record CatchClause(
    SourceRange Range,
    TypeRef? ExceptionType,
    string? ExceptionVariable,
    BlockStmt Body);
