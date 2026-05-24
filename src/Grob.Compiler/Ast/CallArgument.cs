using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A single argument at a <see cref="CallExpr"/> call site. Carries an optional
/// argument name to support the v1 named-argument syntax.
/// </summary>
/// <param name="Range">Source range covered by the argument.</param>
/// <param name="Name">The argument name when supplied with <c>name: value</c>, otherwise <see langword="null"/>.</param>
/// <param name="Value">The argument expression.</param>
public sealed record CallArgument(
    SourceRange Range,
    string? Name,
    Expression Value);
