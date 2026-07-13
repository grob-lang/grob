namespace Grob.Runtime;

/// <summary>
/// The capability interface for process environment variables (Sprint 8 Increment A,
/// D-343, refining D-319). Declared here so the seam exists; the <c>env</c> module
/// (Increment C) is its first consumer. An OS-backed implementation wraps
/// <see cref="System.Environment"/>; a test or embedding host supplies a synthetic map.
/// </summary>
public interface IEnvironment {
    /// <summary>The value of <paramref name="key"/>, or <see langword="null"/> if unset.</summary>
    string? Get(string key);

    /// <summary>Sets <paramref name="key"/> to <paramref name="value"/> for this process.</summary>
    void Set(string key, string value);

    /// <summary><see langword="true"/> when <paramref name="key"/> is set.</summary>
    bool Has(string key);

    /// <summary>Every environment variable visible to this process.</summary>
    IReadOnlyDictionary<string, string> All();
}
