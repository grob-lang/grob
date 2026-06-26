using Grob.Core;

namespace Grob.Compiler.Ast;

/// <summary>
/// A syntactic reference to a function type: <c>fn(T1, T2): R</c> (D-326).
/// <see cref="TypeRef.Name"/> is always <c>"fn"</c>; the structural shape is
/// carried in <see cref="ParameterTypes"/> and <see cref="ReturnType"/>.
/// The <c>?</c> suffix on the function itself (<c>(fn(): R)?</c>) is expressed
/// via <see cref="TypeRef.IsNullable"/>; a <c>?</c> on the return type is on the
/// <see cref="ReturnType"/> node instead.
/// </summary>
public sealed record FunctionTypeRef(
    SourceRange Range,
    IReadOnlyList<TypeRef> ParameterTypes,
    TypeRef ReturnType,
    bool IsNullable)
    : TypeRef(Range, "fn", [], IsNullable);
