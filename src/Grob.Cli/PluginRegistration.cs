using Grob.Runtime;
using Grob.Stdlib;

namespace Grob.Cli;

/// <summary>
/// The composition root's stdlib wiring (Sprint 8 Increment A; D-426 Decision 2). Builds
/// the one <see cref="NativeCatalogue"/> a run or a REPL session shares between its
/// <c>TypeChecker</c> and its <c>VirtualMachine</c>, over the OS-backed capabilities —
/// a fresh <see cref="SystemRandomSource"/>, <see cref="SystemEnvironment"/> and
/// <see cref="SystemClock"/> per run (D-343) — and the caller's streams. The plugin list
/// itself is composed in <c>Grob.Stdlib</c> (<see cref="StdlibPlugins"/>), so every host
/// builds the same twelve plugins in the same order; this class only supplies what the
/// operating system provides.
/// </summary>
internal static class PluginRegistration {
    /// <summary>
    /// Builds the catalogue for one run or session. <paramref name="verbose"/> selects
    /// <see cref="LogPlugin"/>'s initial threshold (<c>--verbose</c>).
    /// </summary>
    internal static NativeCatalogue BuildCatalogue(IStandardStreams streams, bool verbose) =>
        StdlibCatalogue.Build(new SystemRandomSource(), new SystemEnvironment(), streams, new SystemClock(), verbose);
}
