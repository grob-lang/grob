using Grob.Runtime;
using Grob.Stdlib;

namespace Grob.Cli;

/// <summary>
/// The composition root's stdlib plugin list (Sprint 8 Increment A). Given the small,
/// fixed set of plugins at this point in the build, auto-registration is an explicit
/// list here rather than reflection-based assembly scanning — later Sprint 8 increments
/// append further plugins (<c>env</c>/<c>log</c> in C, <c>guid</c> in D, <c>formatAs</c>
/// in E). <see cref="RegisterAll"/> takes an <see cref="IRandomSource"/> (Increment B)
/// because <see cref="MathPlugin"/>'s <c>random</c>/<c>randomInt</c>/<c>randomSeed</c>
/// natives need one per VM run — the composition root constructs a fresh
/// <see cref="SystemRandomSource"/> per run/session (D-343) rather than this list holding
/// a single shared instance across the process lifetime.
/// </summary>
internal static class PluginRegistration {
    /// <summary>Registers every stdlib plugin against <paramref name="registrar"/>, in a fixed order.</summary>
    internal static void RegisterAll(IPluginRegistrar registrar, IRandomSource randomSource) {
        ArgumentNullException.ThrowIfNull(randomSource);

        IReadOnlyList<IGrobPlugin> plugins = [
            new MathPlugin(randomSource),
            new PathPlugin(),
            new StringsPlugin(),
            new IoPlugin(),
        ];
        foreach (IGrobPlugin plugin in plugins) plugin.Register(registrar);
    }
}
