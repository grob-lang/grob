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
    public void Insert(int index, GrobValue value) => _elements.Insert(index, value);

    /// <summary>
    /// Removes the element at <paramref name="index"/>. Thin wrapper over
    /// <see cref="List{T}.RemoveAt"/> — no bounds-checking here (Sprint 9 Increment
    /// C0a-2, D-373); see <see cref="Insert"/>'s remark.
    /// </summary>
    public void RemoveAt(int index) => _elements.RemoveAt(index);

    /// <summary>Removes every element, leaving an empty array (Sprint 9 Increment C0a-2, D-373).</summary>
    public void Clear() => _elements.Clear();
}
