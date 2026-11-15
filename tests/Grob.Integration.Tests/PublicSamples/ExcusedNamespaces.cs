namespace Grob.Integration.Tests.PublicSamples;

/// <summary>
/// The closed list of namespaces whose E1001 a public sample may carry (D-430 Decision 3). The
/// core modules not yet built must stay absent from the stdlib catalogue, the error-examples
/// harness's guard, so building one removes it from the list. The Sprint 11 plugin namespaces are
/// checked by strictness alone, as D-428 checks <c>Sprint 11</c>.
/// </summary>
internal static class ExcusedNamespaces {
    /// <summary>The core modules not yet built.</summary>
    public static IReadOnlyList<string> CoreModules { get; } = ["fs", "json", "csv", "process", "regex"];

    /// <summary>The Sprint 11 plugin namespaces.</summary>
    public static IReadOnlyList<string> PluginNamespaces { get; } = ["http", "auth", "crypto", "zip"];

    /// <summary>Every excused namespace.</summary>
    public static IReadOnlySet<string> All { get; } =
        CoreModules.Concat(PluginNamespaces).ToHashSet(StringComparer.Ordinal);

    /// <summary>One problem for each listed core module that the catalogue now registers.</summary>
    public static IReadOnlyList<string> RegisteredCoreModules(Func<string, bool> isRegisteredNamespace) =>
        [.. CoreModules.Where(isRegisteredNamespace)
            .Select(ns => $"namespace `{ns}` is registered in the stdlib catalogue; remove it from the excused list")];
}
