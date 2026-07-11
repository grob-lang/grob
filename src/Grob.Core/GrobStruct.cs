namespace Grob.Core;

/// <summary>
/// Runtime struct instance. Holds named field values for user-defined types,
/// plugin types and built-in compound types alike.
/// </summary>
/// <remarks>
/// Fields are stored in declaration/construction order and exposed through
/// <see cref="Fields"/>, so a display service can render them source-shaped
/// (D-336). Lookup stays O(1) via a parallel name-to-index map. Value equality
/// and hashing remain order-independent — two structs with the same type name and
/// the same field values compare equal regardless of field order.
/// </remarks>
public sealed class GrobStruct : IEquatable<GrobStruct> {
    // Ordered field storage. _fields is the source-order list the display service
    // reads; _index maps a field name to its position for O(1) lookup and update.
    private readonly List<KeyValuePair<string, GrobValue>> _fields;
    // A cached read-only view over _fields, exposed through Fields so a host caller
    // cannot cast the result back to the backing list and mutate it (which would
    // leave _index stale). The view is live: later SetField appends remain visible.
    private readonly IReadOnlyList<KeyValuePair<string, GrobValue>> _fieldsView;
    private readonly Dictionary<string, int> _index;

    /// <summary>The declared type name, e.g. <c>"Point"</c>.</summary>
    public string TypeName { get; }

    /// <summary>
    /// <c>true</c> when this value came from an anonymous-struct literal
    /// (<c>#{ … }</c>, D-114); <c>false</c> for a named user-defined type. The
    /// display service uses this to choose between <c>Name { … }</c> and
    /// <c>#{ … }</c> rendering (D-336).
    /// </summary>
    public bool IsAnonymous { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobStruct"/> with the given
    /// <paramref name="typeName"/>, optional initial <paramref name="fields"/> and
    /// an <paramref name="isAnonymous"/> flag (default <c>false</c>).
    /// </summary>
    public GrobStruct(
        string typeName,
        IEnumerable<KeyValuePair<string, GrobValue>>? fields = null,
        bool isAnonymous = false) {
        ArgumentNullException.ThrowIfNull(typeName);
        if (typeName.Length == 0)
            throw new ArgumentException("Type name must not be empty.", nameof(typeName));
        TypeName = typeName;
        IsAnonymous = isAnonymous;
        _fields = [];
        _fieldsView = _fields.AsReadOnly();
        _index = new Dictionary<string, int>(StringComparer.Ordinal);
        if (fields is not null) {
            foreach (var (key, value) in fields)
                SetField(key, value);
        }
    }

    /// <summary>
    /// The struct's fields in declaration/construction order — the order they were
    /// supplied at construction, with any later-added field appended. Reflects the
    /// current value of every field. Read-only; never mutated by callers.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, GrobValue>> Fields => _fieldsView;

    /// <summary>
    /// Returns the value of the field named <paramref name="name"/>.
    /// Throws <see cref="KeyNotFoundException"/> if the field does not exist.
    /// </summary>
    public GrobValue GetField(string name) {
        ArgumentNullException.ThrowIfNull(name);
        if (_index.TryGetValue(name, out int pos))
            return _fields[pos].Value;
        throw new KeyNotFoundException($"Field '{name}' not found on struct '{TypeName}'.");
    }

    /// <summary>Sets (or adds) the field named <paramref name="name"/> to <paramref name="value"/>.</summary>
    public void SetField(string name, GrobValue value) {
        ArgumentNullException.ThrowIfNull(name);
        if (_index.TryGetValue(name, out int pos)) {
            _fields[pos] = new KeyValuePair<string, GrobValue>(name, value);
            return;
        }
        _index[name] = _fields.Count;
        _fields.Add(new KeyValuePair<string, GrobValue>(name, value));
    }

    /// <summary>
    /// Attempts to retrieve the value of <paramref name="name"/>.
    /// Returns <c>true</c> and sets <paramref name="value"/> on success;
    /// <c>false</c> on miss.
    /// </summary>
    public bool TryGetField(string name, out GrobValue value) {
        ArgumentNullException.ThrowIfNull(name);
        if (_index.TryGetValue(name, out int pos)) {
            value = _fields[pos].Value;
            return true;
        }
        value = default;
        return false;
    }

    /// <summary>
    /// Field-by-field value equality: same <see cref="TypeName"/>, same field
    /// count, and every field value compares equal. Order-independent.
    /// </summary>
    public bool Equals(GrobStruct? other) {
        if (other is null) return false;
        if (!string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)) return false;
        if (_fields.Count != other._fields.Count) return false;
        foreach (var (key, val) in _fields) {
            if (!other.TryGetField(key, out var otherVal)) return false;
            // Use Equals (not the == operator) so that NaN-containing structs are
            // reflexively equal — required by the .NET Equals/GetHashCode contract
            // for use as collection keys. IEEE 754 NaN semantics live on operator==.
            if (!val.Equals(otherVal)) return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GrobStruct s && Equals(s);

    /// <inheritdoc/>
    public override int GetHashCode() {
        var hc = new HashCode();
        hc.Add(TypeName, StringComparer.Ordinal);
        foreach (var (key, val) in _fields.OrderBy(kv => kv.Key, StringComparer.Ordinal)) {
            hc.Add(key, StringComparer.Ordinal);
            hc.Add(val);
        }
        return hc.ToHashCode();
    }
}
