using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A literal text segment inside an <see cref="InterpolatedStringExpr"/>.</summary>
/// <param name="Range">Source range covered by the segment.</param>
/// <param name="Text">The decoded text (escape sequences already resolved).</param>
public sealed record StringTextPart(
    SourceRange Range,
    string Text) : StringInterpolationPart(Range);
