namespace Grob.Core;

/// <summary>
/// Runtime array value. Holds a mutable list of <see cref="GrobValue"/> elements.
/// The full implementation lands with the VM in Sprint 2 Increment B/D.
/// </summary>
public sealed class GrobArray {
    private readonly List<GrobValue> _elements;

    /// <summary>
    /// Initialises a new <see cref="GrobArray"/>, optionally pre-populated with
    /// <paramref name="elements"/>.
    /// </summary>
    public GrobArray(IEnumerable<GrobValue>? elements = null) {
        // D-388 investigated whether this single-spread copy grows by doubling for a
        // statically-IEnumerable<T> source (it does not): the compiler lowers a
        // single-spread `[.. elements]` targeting List<GrobValue> to
        // `new List<GrobValue>(elements)`, and that BCL constructor already fast-paths
        // any runtime-ICollection<T> source (List<T>, T[] — every real caller here) via
        // its own internal check. A genuinely lazy, non-ICollection source still grows
        // by doubling, but no construction site in this codebase passes one.
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
}
