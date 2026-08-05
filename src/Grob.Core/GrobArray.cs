namespace Grob.Core;

/// <summary>
/// Runtime array value. Holds a mutable list of <see cref="GrobValue"/> elements.
/// The full implementation lands with the VM in Sprint 2 Increment B/D.
/// </summary>
public sealed class GrobArray {
    private readonly List<GrobValue> _elements;

    /// <summary>
    /// Per-receiver bound-method cache (D-393 Q2): <c>Grob.Vm.ArrayNatives.GetMethod</c>
    /// consults this before constructing a fresh <see cref="NativeFunction"/> on every
    /// <c>GetProperty</c> dispatch. Lazily created on first bind; no invalidation is
    /// needed — every bound native closes over its receiver by reference and reads live
    /// state per invocation, and no cached delegate captures any per-access VM context
    /// (D-393 Q2's ratified analysis).
    /// </summary>
    private Dictionary<string, NativeFunction>? _methodCache;

    /// <summary>
    /// Initialises a new <see cref="GrobArray"/>, optionally pre-populated with
    /// <paramref name="elements"/>.
    /// </summary>
    public GrobArray(IEnumerable<GrobValue>? elements = null) {
        // D-388 investigated whether this single-spread copy grows by doubling for a
        // statically-IEnumerable<T> source. Measured on the current toolchain, it does
        // not: Roslyn lowers a single-spread `[.. elements]` targeting List<GrobValue>
        // to `new List<GrobValue>(elements)`, and that BCL constructor fast-paths any
        // source whose *runtime* type implements ICollection<T> (List<T>, T[]) via its
        // own internal check — which is what every production construction site passes.
        // Both halves are current implementation behaviour of the compiler and the BCL,
        // not guaranteed contracts. A genuinely lazy, non-ICollection source (an
        // iterator, or a LINQ Select as this constructor's tests cover) does still grow
        // by doubling, so pre-sizing would matter again if one ever reached a hot path.
        _elements = elements is null ? [] : [.. elements];
    }

    /// <summary>Read-only view of the element list.</summary>
    public IReadOnlyList<GrobValue> Elements => _elements;

    /// <summary>Number of elements in the array.</summary>
    public int Count => _elements.Count;

    /// <summary>Gets or sets the element at <paramref name="index"/>.</summary>
    public GrobValue this[int index] {
        get => _elements[index];
        set => _elements[index] = value;
    }

    /// <summary>Appends <paramref name="value"/> to the end of the array.</summary>
    public void Add(GrobValue value) => _elements.Add(value);

    /// <summary>
    /// Inserts <paramref name="value"/> before <paramref name="index"/>. Thin wrapper
    /// over <see cref="List{T}.Insert"/> — no bounds-checking here (Sprint 9 Increment
    /// C0a-2, D-373); the native layer (<c>Grob.Vm.ArrayNatives</c>) checks bounds so
    /// the <c>IndexError</c>/<c>E5101</c> message and catchability stay consistent with
    /// every other array/string bounds fault in the codebase.
    /// </summary>
    /// <param name="index">Zero-based position to insert before; valid range is
    /// <c>0</c> to <see cref="Count"/> inclusive (<see cref="Count"/> appends at the end).</param>
    /// <param name="value">The value to insert.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
    /// <c>0</c> or greater than <see cref="Count"/> — raised by the underlying
    /// <see cref="List{T}.Insert"/> when a caller bypasses the native bounds check.</exception>
    public void Insert(int index, GrobValue value) => _elements.Insert(index, value);

    /// <summary>
    /// Removes the element at <paramref name="index"/>. Thin wrapper over
    /// <see cref="List{T}.RemoveAt"/> — no bounds-checking here (Sprint 9 Increment
    /// C0a-2, D-373); see <see cref="Insert"/>'s remark.
    /// </summary>
    /// <param name="index">Zero-based position of the element to remove; valid range is
    /// <c>0</c> to <see cref="Count"/> exclusive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than
    /// <c>0</c> or greater than or equal to <see cref="Count"/> — raised by the underlying
    /// <see cref="List{T}.RemoveAt"/> when a caller bypasses the native bounds check.</exception>
    public void RemoveAt(int index) => _elements.RemoveAt(index);

    /// <summary>Removes every element, leaving an empty array (Sprint 9 Increment C0a-2, D-373).</summary>
    public void Clear() => _elements.Clear();

    /// <summary>
    /// Returns the cached <see cref="NativeFunction"/> bound to <paramref name="methodName"/>
    /// on this receiver, or <see langword="null"/> if none has been cached yet (D-393 Q2).
    /// </summary>
    internal NativeFunction? GetCachedMethod(string methodName) =>
        _methodCache is not null && _methodCache.TryGetValue(methodName, out NativeFunction? cached)
            ? cached
            : null;

    /// <summary>
    /// Caches <paramref name="method"/> as the bound <see cref="NativeFunction"/> for
    /// <paramref name="methodName"/> on this receiver (D-393 Q2), lazily creating the
    /// backing dictionary on first bind.
    /// </summary>
    internal void CacheMethod(string methodName, NativeFunction method) =>
        (_methodCache ??= new Dictionary<string, NativeFunction>())[methodName] = method;
}
