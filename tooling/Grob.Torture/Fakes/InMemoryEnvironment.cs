using Grob.Runtime;

namespace Grob.Torture.Fakes;

/// <summary>
/// An environment held in memory and empty at the start of every run. It absorbs
/// <c>env.set</c>, which the CLI's system environment writes to the real process.
/// </summary>
internal sealed class InMemoryEnvironment : IEnvironment {
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string? Get(string key) => _variables.GetValueOrDefault(key);

    /// <inheritdoc/>
    public void Set(string key, string value) => _variables[key] = value;

    /// <inheritdoc/>
    public bool Has(string key) => _variables.ContainsKey(key);

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, string> All() => new Dictionary<string, string>(_variables, StringComparer.Ordinal);
}
