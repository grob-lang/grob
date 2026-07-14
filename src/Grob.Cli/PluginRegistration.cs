using Grob.Runtime;
using Grob.Stdlib;

namespace Grob.Cli;

/// <summary>
/// The composition root's stdlib plugin list (Sprint 8 Increment A). Given the small,
/// fixed set of plugins at this point in the build, auto-registration is an explicit
/// list here rather than reflection-based assembly scanning — <c>formatAs</c> (Increment
/// E) is the one still to come. <see cref="RegisterAll"/> takes an
/// <see cref="IRandomSource"/> (Increment B) because <see cref="MathPlugin"/>'s
/// <c>random</c>/<c>randomInt</c>/<c>randomSeed</c> natives need one per VM run — the
/// composition root constructs a fresh <see cref="SystemRandomSource"/> per run/session
/// (D-343) rather than this list holding a single shared instance across the process
/// lifetime. Increment C adds <see cref="IEnvironment"/> (<c>env.*</c>), the injected
/// <see cref="IStandardStreams"/> (<c>log.*</c>'s stderr sink and <c>input()</c>'s
/// stdout/stdin — <see cref="IoPlugin"/> now gives <c>input()</c> a real native
/// registration where it was previously an empty placeholder) and a <c>verbose</c> flag
/// (<c>--verbose</c>, which selects <see cref="LogPlugin"/>'s initial threshold).
/// Increment D adds <see cref="GuidPlugin"/>, reusing the same <see cref="IRandomSource"/>
/// and a fresh <see cref="SystemClock"/> (D-343's first real <see cref="IClock"/>
/// consumer, per-run like <see cref="SystemRandomSource"/>).
/// Increment E adds <see cref="FormatAsPlugin"/> — no capability injection needed, it is
/// pure formatting over already-resolved arguments (D-342's compile-time column
/// derivation) plus the injected <see cref="IPluginRegistrar.RenderValue"/> it captures
/// for itself at <c>Register</c> time.
/// </summary>
internal static class PluginRegistration {
    /// <summary>Registers every stdlib plugin against <paramref name="registrar"/>, in a fixed order.</summary>
    internal static void RegisterAll(
            IPluginRegistrar registrar,
            IRandomSource randomSource,
            IEnvironment environment,
            IStandardStreams streams,
            bool verbose) {
        ArgumentNullException.ThrowIfNull(randomSource);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(streams);

        IReadOnlyList<IGrobPlugin> plugins = [
            new MathPlugin(randomSource),
            new PathPlugin(),
            new StringsPlugin(),
            new EnvPlugin(environment),
            new LogPlugin(streams, verbose ? LogLevel.Debug : LogLevel.Info),
            new IoPlugin(streams),
            new GuidPlugin(randomSource, new SystemClock()),
            new FormatAsPlugin(),
        ];
        foreach (IGrobPlugin plugin in plugins) plugin.Register(registrar);
    }
}
