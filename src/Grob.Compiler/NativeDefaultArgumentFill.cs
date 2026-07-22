using Grob.Core;

namespace Grob.Compiler;

/// <summary>
/// Computes the trailing default constants a native call site must synthesise so the
/// runtime native receives its full, fixed arity regardless of how many trailing
/// optional parameters the source call omitted (D-358). A pure function of the call's
/// own shape — supplied argument count, declared full arity, and the registry's
/// per-parameter defaults — with no <c>Chunk</c>/emission dependency, so it is callable
/// from any native-call emission branch (the namespace-native branch wires it this
/// increment; the primitive-member and named-type-method branches can call it unchanged
/// once they gain default-argument metadata of their own).
/// </summary>
internal static class NativeDefaultArgumentFill {
    /// <summary>
    /// Returns the default constants for the trailing parameters the call omitted, in
    /// parameter order — empty when every parameter was supplied or no defaults are
    /// declared. Throws if a requested slot has no declared default: the type checker's
    /// required/full arity range is what guarantees this never happens for a call that
    /// passed validation.
    /// </summary>
    internal static IReadOnlyList<GrobValue> Resolve(
            int suppliedCount, int fullArity, IReadOnlyList<GrobValue?>? defaults) {
        if (suppliedCount >= fullArity || defaults is null) return [];

        var fill = new List<GrobValue>(fullArity - suppliedCount);
        for (int i = suppliedCount; i < fullArity; i++) {
            fill.Add(defaults[i] ?? throw new InvalidOperationException(
                $"Native default-argument fill requested for parameter {i}, which has no declared default."));
        }
        return fill;
    }
}
