namespace Grob.Compiler.Binding;

/// <summary>
/// Array <c>sort</c>'s parameter list as the binder sees it: <c>fn</c>, required, then
/// <c>descending</c>, which defaults to <c>false</c> (<c>grob-type-registry.md</c>,
/// Chris's ruling S2 of 21 November). Array methods record no signature (R-56), so this
/// is the one hand-written view; it lets <c>descending</c> be passed by name on the same
/// binder as every other callee, where every other array method refuses a name.
/// </summary>
internal readonly struct ArraySortParameters : IBindableParameters {
    /// <inheritdoc/>
    public int Count => 2;

    /// <inheritdoc/>
    public string NameAt(int index) => index == 0 ? "fn" : "descending";

    /// <inheritdoc/>
    public bool HasDefaultAt(int index) => index == 1;
}
