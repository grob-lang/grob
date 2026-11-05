namespace Grob.Runtime;

/// <summary>
/// A first-party plugin that owns primitive instance members, primitive type-statics or a
/// bare built-in. <see cref="NativeCatalogueBuilder.Add"/> calls
/// <see cref="RegisterPrimitiveMembers"/> after <see cref="IGrobPlugin.Register"/> when a
/// plugin implements this. Internal, so the opt-in is unreachable outside
/// <c>Grob.Stdlib</c> and <c>Grob.Vm</c> (D-426 Decision 3); keeping the plugin code
/// declarative beats a runtime cast of the public registrar inside every primitive plugin.
/// </summary>
internal interface IPrimitiveMemberPlugin : IGrobPlugin {
    /// <summary>Registers this plugin's primitive members against <paramref name="registrar"/>.</summary>
    void RegisterPrimitiveMembers(IPrimitiveMemberRegistrar registrar);
}
