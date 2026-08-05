namespace Grob.Core;

/// <summary>
/// Runtime map value. Holds string-keyed <see cref="GrobValue"/> entries in
/// insertion order — the order <c>for k, v in m</c> iteration walks (Sprint 4
/// Increment C). Backed by <see cref="OrderedDictionary{TKey, TValue}"/> so the
/// insertion-order contract is guaranteed, not incidental.
/// </summary>
public sealed class GrobMap {
    private readonly OrderedDictionary<string, GrobValue> _entries =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Per-receiver bound-method cache (D-393 Q2): <c>Grob.Vm.MapNatives.GetMethod</c>
    /// consults this before constructing a fresh <see cref="NativeFunction"/> on every
    /// <c>GetProperty</c> dispatch. Mirrors <see cref="GrobArray"/>'s identical field —
    /// see its remarks for the invalidation-free rationale.
    /// </summary>
    private Dictionary<string, NativeFunction>? _methodCache;

    /// <summary>Read-only view of the underlying entry dictionary.</summary>
    public IReadOnlyDictionary<string, GrobValue> Entries => _entries;

    /// <summary>
    /// The map's keys in insertion order — the key set <c>for k, v in m</c>
    /// materialises once before iterating (Sprint 4 Increment C). This is the live
    /// ordered-key view of the backing dictionary, not a copy: the caller
    /// snapshots it (the VM builds a <c>GrobArray</c>) rather than retaining it.
    /// </summary>
    public IReadOnlyList<string> InsertionOrderKeys => _entries.Keys;

    /// <summary>
    /// The map's values in insertion order, index-aligned with <see cref="InsertionOrderKeys"/>
    /// (Sprint 9 Increment C0b-2a, D-377). The live ordered-value view of the backing
    /// dictionary, not a copy — mirrors <see cref="InsertionOrderKeys"/>'s identical contract.
    /// </summary>
    public IReadOnlyList<GrobValue> InsertionOrderValues => _entries.Values;

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

    /// <summary>
    /// Removes the entry at <paramref name="key"/>, if present (Sprint 9 Increment
    /// C0b-2b, D-378). No-op if absent — the opposite of the array's bounds-checked,
    /// throwing <c>remove(index)</c> (D-373).
    /// </summary>
    public void Remove(string key) => _entries.Remove(key);

    /// <summary>Removes all entries (Sprint 9 Increment C0b-2b, D-378).</summary>
    public void Clear() => _entries.Clear();

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
