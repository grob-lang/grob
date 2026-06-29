using Grob.Core;

namespace Grob.Compiler.Ast.Expressions;

/// <summary>A single field initialiser inside a struct-construction expression.</summary>
/// <param name="Range">Source range covering the whole <c>name: value</c> pair.</param>
/// <param name="Name">The field name as it appears at the construction site.</param>
/// <param name="Value">The supplied value expression.</param>
public sealed record FieldInit(SourceRange Range, string Name, Expression Value);
