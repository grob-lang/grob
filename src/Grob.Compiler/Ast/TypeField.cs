using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>A single field declaration inside a <see cref="TypeDecl"/>.</summary>
/// <param name="Range">Source range covered by the field declaration.</param>
/// <param name="Name">The field name.</param>
/// <param name="Type">The declared field type.</param>
/// <param name="DefaultValue">The default-value expression, or <see langword="null"/> when none was supplied.</param>
public sealed record TypeField(
    SourceRange Range,
    string Name,
    TypeRef Type,
    Expression? DefaultValue);
