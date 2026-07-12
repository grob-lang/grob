using Grob.Runtime;
using Grob.Stdlib;

namespace Grob.Cli;

/// <summary>
/// The composition root's stdlib plugin list (Sprint 8 Increment A). Given the small,
/// fixed set of plugins at this point in the build, auto-registration is an explicit
/// list here rather than reflection-based assembly scanning — later Sprint 8 increments
/// append to <see cref="All"/> as they add plugins (<c>path</c>/<c>strings</c> in B,
/// <c>env</c>/<c>log</c> in C, <c>guid</c> in D, <c>formatAs</c> in E).
/// </summary>
internal static class PluginRegistration {
    /// <summary>Every stdlib plugin, registered in this order at VM startup.</summary>
    internal static readonly IReadOnlyList<IGrobPlugin> All = [
        new MathPlugin(),
        new IoPlugin(),
    ];

    /// <summary>Registers every plugin in <see cref="All"/> against <paramref name="registrar"/>.</summary>
    internal static void RegisterAll(IPluginRegistrar registrar) {
        foreach (IGrobPlugin plugin in All) plugin.Register(registrar);
    }
}
