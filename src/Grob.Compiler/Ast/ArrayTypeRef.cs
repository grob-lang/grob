using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A type-reference of the form <c>T[]</c>, <c>T[][]</c> or <c>T[]?</c> (D-327).
/// <see cref="TypeRef.Name"/> is always <c>"[]"</c> — distinct from the <c>"array"</c>
/// identifier arm in <see cref="TypeChecker.ResolveTypeRef"/>.
/// </summary>
/// <param name="Range">Source range from the start of the element type to the closing <c>]</c> (or <c>?</c>).</param>
/// <param name="ElementType">The type-ref for the element — may itself be an <see cref="ArrayTypeRef"/> for nested arrays.</param>
/// <param name="IsNullable"><see langword="true"/> when a <c>?</c> suffix follows the <c>]</c>.</param>
public sealed record ArrayTypeRef(
    SourceRange Range,
    TypeRef ElementType,
    bool IsNullable)
    : TypeRef(Range, "[]", [], IsNullable);
