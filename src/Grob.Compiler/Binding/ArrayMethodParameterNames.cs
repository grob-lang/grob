namespace Grob.Compiler.Binding;

/// <summary>
/// The parameter names an array method accepts by name, read for the drift check
/// (R-55, D-446 Decision 3). Array methods record no <c>FunctionSignature</c> (R-56), so
/// <c>sort</c>'s <c>descending</c> is the one defaulted array parameter whose name is
/// contract and it lives in a hand-written view; this reads that view so the check
/// follows it and holds no second copy.
/// </summary>
public static class ArrayMethodParameterNames {
    /// <summary>The names of array <c>sort</c>'s defaulted parameters, in parameter order.</summary>
    /// <returns>The names a call may supply by name.</returns>
    public static IReadOnlyList<string> SortDefaulted() {
        var view = new ArraySortParameters();
        var names = new List<string>();
        for (int i = 0; i < view.Count; i++) {
            if (view.HasDefaultAt(i)) {
                names.Add(view.NameAt(i));
            }
        }
        return names;
    }
}
