namespace Grob.Core.NamedTypes;

/// <summary>
/// One positional parameter of a <see cref="NamedTypeMethod"/> — the declared
/// <see cref="GrobType"/> plus whether it is checked as a primitive/flat type or as
/// a same-nominal-type identity (<see cref="NamedTypeParameterKind.NominalSelf"/>).
/// </summary>
/// <param name="Type">The declared parameter type.</param>
/// <param name="Kind">How the parameter is checked — defaults to <see cref="NamedTypeParameterKind.Primitive"/>.</param>
public sealed record NamedTypeParameter(GrobType Type, NamedTypeParameterKind Kind = NamedTypeParameterKind.Primitive);
