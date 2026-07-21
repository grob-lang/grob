namespace Grob.Core.PrimitiveMembers;

/// <summary>
/// One instance method on a <see cref="PrimitiveMemberEntry"/> — the declared
/// signature the type checker validates a call against, plus the qualified native
/// name the compiler rewrites the call to (D-066). Unlike
/// <see cref="NamedTypes.NamedTypeMethod"/> there is no <c>Bind</c> delegate and no
/// <c>ReturnsNominalSelf</c> flag — a primitive method never resolves to another
/// nominal type, and dispatch is a compile-time rewrite (receiver injected as the
/// native's arg[0]) rather than a runtime-bound <c>NativeFunction</c>. Parameters are
/// a plain <see cref="GrobType"/> list, not <see cref="NamedTypes.NamedTypeParameter"/>:
/// no primitive parameter needs the nominal-self identity check that wrapper exists
/// for (every parameter here is a flat primitive type).
/// </summary>
/// <param name="Name">The method name as written in Grob source (e.g. <c>trim</c>).</param>
/// <param name="ParameterTypes">The declared positional parameter types, in call order.</param>
/// <param name="ReturnType">The declared return type.</param>
/// <param name="QualifiedNativeName">
/// The registered native the compiler rewrites this call to (e.g. <c>"string.split"</c>),
/// called with the receiver as arg[0] followed by the call's own arguments.
/// </param>
/// <param name="ParameterDefaults">
/// Per-parameter default-value metadata (D-358), mirroring
/// <see cref="NamedTypes.NamedTypeMethod.ParameterDefaults"/>. Present on the schema
/// but unused this increment — <see langword="null"/> for every current entry until
/// the default-argument call-site synthesis mechanism (<c>padLeft</c>/<c>padRight</c>/
/// <c>truncate</c>) is built.
/// </param>
public sealed record PrimitiveMemberMethod(
    string Name,
    IReadOnlyList<GrobType> ParameterTypes,
    GrobType ReturnType,
    string QualifiedNativeName,
    IReadOnlyList<GrobValue?>? ParameterDefaults = null);
