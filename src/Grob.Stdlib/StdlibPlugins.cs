using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The twelve first-party plugins in their fixed registration order (D-426 Decision 2,
/// gate report §1.4.2). Every host that composes a stdlib — the CLI's run and REPL
/// commands, the benchmarks, the tests and a compile-only catalogue — builds the list
/// here, so the order and the capability wiring are written once. The capabilities are
/// injected (D-343): a host supplies the OS-backed ones, a compile-only host supplies
/// inert ones, and no plugin touches a capability at registration (gate report §1.3).
/// </summary>
public static class StdlibPlugins {
    /// <summary>
    /// Creates the plugins. <paramref name="verbose"/> selects <see cref="LogPlugin"/>'s
    /// initial threshold (<c>--verbose</c>).
    /// </summary>
    public static IReadOnlyList<IGrobPlugin> Create(
            IRandomSource randomSource,
            IEnvironment environment,
            IStandardStreams streams,
            IClock clock,
            bool verbose) {
        ArgumentNullException.ThrowIfNull(randomSource);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(streams);
        ArgumentNullException.ThrowIfNull(clock);

        return [
            new MathPlugin(randomSource),
            new PathPlugin(),
            new StringsPlugin(),
            new EnvPlugin(environment),
            new LogPlugin(streams, verbose ? LogLevel.Debug : LogLevel.Info),
            new IoPlugin(streams),
            new GuidPlugin(randomSource, clock),
            new DatePlugin(clock),
            new FormatAsPlugin(),
            new StringMethodsPlugin(),
            new NumericMethodsPlugin(),
            new NumericStaticsPlugin(),
        ];
    }
}
