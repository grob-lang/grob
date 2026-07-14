using Grob.Runtime;

namespace Grob.Cli;

/// <summary>
/// The composition root's <see cref="IEnvironment"/> implementation (D-343 — the seam was
/// declared in Sprint 8 Increment A; <c>env.*</c> is Increment C's first real consumer).
/// Wraps <see cref="Environment.GetEnvironmentVariable(string)"/>,
/// <see cref="Environment.SetEnvironmentVariable(string, string?)"/> and
/// <see cref="Environment.GetEnvironmentVariables()"/> directly — process-scoped, so a
/// <c>Set</c> is visible to a subsequent <c>Get</c>/<c>Has</c>/<c>All</c> within the same
/// run but does not persist beyond the process (the OS-level contract those BCL members
/// already carry).
/// </summary>
internal sealed class SystemEnvironment : IEnvironment {
    public string? Get(string key) => Environment.GetEnvironmentVariable(key);

    public void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);

    public bool Has(string key) => Environment.GetEnvironmentVariable(key) is not null;

    public IReadOnlyDictionary<string, string> All() {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables()) {
            if (entry.Key is string key && entry.Value is string value) result[key] = value;
        }
        return result;
    }
}
