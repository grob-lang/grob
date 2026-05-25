namespace Grob.Core;

/// <summary>
/// Runtime struct instance. Holds named field values for user-defined types,
/// plugin types and built-in compound types alike.
/// The full implementation lands with the type system in a later sprint.
/// </summary>
public sealed class GrobStruct : IEquatable<GrobStruct> {
    private readonly Dictionary<string, GrobValue> _fields;

    /// <summary>The declared type name, e.g. <c>"Point"</c>.</summary>
    public string TypeName { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobStruct"/> with the given
    /// <paramref name="typeName"/> and optional initial <paramref name="fields"/>.
    /// </summary>
    public GrobStruct(string typeName, IEnumerable<KeyValuePair<string, GrobValue>>? fields = null) {
        ArgumentNullException.ThrowIfNull(typeName);
        if (typeName.Length == 0)
            throw new ArgumentException("Type name must not be empty.", nameof(typeName));
        TypeName = typeName;
        _fields = fields is null
            ? []
            : new Dictionary<string, GrobValue>(fields, StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns the value of the field named <paramref name="name"/>.
    /// Throws <see cref="KeyNotFoundException"/> if the field does not exist.
    /// </summary>
    public GrobValue GetField(string name) {
        ArgumentNullException.ThrowIfNull(name);
        return _fields[name];
    }

    /// <summary>Sets (or adds) the field named <paramref name="name"/> to <paramref name="value"/>.</summary>
    public void SetField(string name, GrobValue value) {
        ArgumentNullException.ThrowIfNull(name);
        _fields[name] = value;
    }

    /// <summary>
    /// Attempts to retrieve the value of <paramref name="name"/>.
    /// Returns <c>true</c> and sets <paramref name="value"/> on success;
    /// <c>false</c> on miss.
    /// </summary>
    public bool TryGetField(string name, out GrobValue value) {
        ArgumentNullException.ThrowIfNull(name);
        return _fields.TryGetValue(name, out value);
    }

    /// <summary>
    /// Field-by-field value equality: same <see cref="TypeName"/>, same field
    /// count, and every field value compares equal.
    /// </summary>
    public bool Equals(GrobStruct? other) {
        if (other is null) return false;
        if (!string.Equals(TypeName, other.TypeName, StringComparison.Ordinal)) return false;
        if (_fields.Count != other._fields.Count) return false;
        foreach (var (key, val) in _fields) {
            if (!other._fields.TryGetValue(key, out var otherVal)) return false;
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
