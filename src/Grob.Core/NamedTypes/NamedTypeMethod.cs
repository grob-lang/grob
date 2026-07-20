namespace Grob.Core.NamedTypes;

/// <summary>
/// One instance method on a <see cref="NamedTypeEntry"/> — the declared signature the
/// type checker validates a call against, plus the runtime binder VM dispatch calls
/// to produce the bound <see cref="NativeFunction"/>.
/// </summary>
/// <param name="Name">The method name as written in Grob source (e.g. <c>addDays</c>).</param>
/// <param name="Parameters">The declared positional parameters, in call order.</param>
/// <param name="ReturnType">
/// The declared return type. Ignored by the checker when <paramref name="ReturnsNominalSelf"/>
/// is <c>true</c> — the call instead resolves to <see cref="GrobType.Struct"/> with the
/// entry's own nominal name threaded through, so this stays <see cref="GrobType.Struct"/>
/// for a self-returning method (documentation-only in that case).
/// </param>
/// <param name="ReturnsNominalSelf">
/// <c>true</c> when the method returns another value of this entry's own nominal type
/// (e.g. <c>date.addDays</c> returns <c>date</c>) — the checker threads the nominal name
/// through so a <c>:=</c>-bound result resolves further member access, mirroring
/// <c>_callResultStructNames</c>.
/// </param>
/// <param name="Bind">Produces the bound <see cref="NativeFunction"/> for a given receiver.</param>
/// <param name="ParameterDefaults">
/// Per-parameter default-value metadata (D-358). Present on the schema but unused by
/// this increment — <see langword="null"/> or empty for every entry until the
/// default-argument call-site synthesis mechanism is built.
/// </param>
public sealed record NamedTypeMethod(
    string Name,
    IReadOnlyList<NamedTypeParameter> Parameters,
    GrobType ReturnType,
    bool ReturnsNominalSelf,
    Func<GrobStruct, NativeFunction> Bind,
    IReadOnlyList<GrobValue?>? ParameterDefaults = null);
