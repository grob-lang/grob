namespace Grob.Core;

/// <summary>
/// Runtime map value. Holds string-keyed <see cref="GrobValue"/> entries.
/// The full implementation lands with the VM in a later sprint.
/// </summary>
public sealed class GrobMap {
    private readonly Dictionary<string, GrobValue> _entries = new(StringComparer.Ordinal);

    /// <summary>Read-only view of the underlying entry dictionary.</summary>
    public IReadOnlyDictionary<string, GrobValue> Entries => _entries;

    /// <summary>Gets or sets the value associated with <paramref name="key"/>.</summary>
    public GrobValue this[string key] {
        get => _entries[key];
        set => _entries[key] = value;
    }

    /// <summary>
    /// Attempts to retrieve the value for <paramref name="key"/>.
    /// Returns <c>true</c> and sets <paramref name="value"/> on success; returns
    /// <c>false</c> and sets <paramref name="value"/> to <c>default</c> on miss.
    /// </summary>
    public bool TryGetValue(string key, out GrobValue value) =>
        _entries.TryGetValue(key, out value);

    /// <summary>Sets (or overwrites) the entry at <paramref name="key"/> to <paramref name="value"/>.</summary>
    public void Set(string key, GrobValue value) => _entries[key] = value;
}
