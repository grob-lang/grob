using Grob.Runtime;

namespace Grob.Stdlib.Tests;

/// <summary>
/// An in-memory <see cref="IEnvironment"/> test double — the same shape the production
/// <c>SystemEnvironment</c> (<c>Grob.Cli</c>) wraps around <see cref="System.Environment"/>,
/// but declared locally since the DAG forbids <c>Grob.Stdlib.Tests</c> from referencing
/// <c>Grob.Cli</c>. Starts from a caller-supplied seed dictionary so a test can assert
/// against known-absent as well as known-present keys.
/// </summary>
internal sealed class FakeEnvironment : IEnvironment {
    private readonly Dictionary<string, string> _vars;

    internal FakeEnvironment(IDictionary<string, string>? seed = null) =>
        _vars = seed is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(seed, StringComparer.Ordinal);

    public string? Get(string key) => _vars.TryGetValue(key, out string? value) ? value : null;

    public void Set(string key, string value) => _vars[key] = value;

    public bool Has(string key) => _vars.ContainsKey(key);

    public IReadOnlyDictionary<string, string> All() => _vars;
}
