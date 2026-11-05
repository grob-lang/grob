using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// Builds the <see cref="NativeCatalogue"/> for the first-party stdlib (D-426
/// Decision 2). A host builds one and shares it with every <c>TypeChecker</c> and the
/// <c>VirtualMachine</c> of that host; a compile-only host (<c>grob check</c>, the
/// language server, the tests) builds one over inert capabilities, which is safe
/// because no plugin reads a capability at registration (gate report §1.3).
/// </summary>
public static class StdlibCatalogue {
    /// <summary>Builds a catalogue over the given capabilities.</summary>
    public static NativeCatalogue Build(
            IRandomSource randomSource,
            IEnvironment environment,
            IStandardStreams streams,
            IClock clock,
            bool verbose) {
        var builder = new NativeCatalogueBuilder();
        foreach (IGrobPlugin plugin in StdlibPlugins.Create(randomSource, environment, streams, clock, verbose)) {
            builder.Add(plugin);
        }
        return builder.Build();
    }

    /// <summary>
    /// Builds a catalogue whose natives cannot reach the clock, the environment, the
    /// random source or the standard streams: calling one throws
    /// <see cref="InvalidOperationException"/>. For hosts that only type-check.
    /// </summary>
    public static NativeCatalogue ForCompileOnly() => Build(
        InertCapabilities.RandomSource,
        InertCapabilities.Environment,
        InertCapabilities.Streams,
        InertCapabilities.Clock,
        verbose: false);
}
