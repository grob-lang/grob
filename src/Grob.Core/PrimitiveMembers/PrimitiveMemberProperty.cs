namespace Grob.Core.PrimitiveMembers;

/// <summary>
/// One instance property on a <see cref="PrimitiveMemberEntry"/> — the declared
/// <see cref="GrobType"/> and the qualified native name a bare property access
/// (<c>s.length</c>) rewrites to at compile time (D-066: primitive method/property
/// access is compile-time sugar, never a runtime dispatch). Unlike
/// <see cref="NamedTypes.NamedTypeProperty"/>, there is no <c>Get</c> delegate here —
/// the receiver is injected as the qualified native's sole argument rather than bound
/// through a <c>GrobStruct</c> accessor, since a primitive is never
/// <see cref="GrobValueKind.Struct"/>.
/// </summary>
/// <param name="Name">The property name as written in Grob source (e.g. <c>length</c>).</param>
/// <param name="Type">The property's declared type.</param>
/// <param name="QualifiedNativeName">
/// The registered native the compiler rewrites this access to (e.g. <c>"string.length"</c>),
/// called with the receiver as its sole argument.
/// </param>
public sealed record PrimitiveMemberProperty(string Name, GrobType Type, string QualifiedNativeName);
