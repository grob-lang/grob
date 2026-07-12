namespace Grob.Runtime;

/// <summary>
/// The contract every Grob plugin implements — first-party stdlib modules
/// (<c>Grob.Stdlib</c>) and third-party plugins alike (Sprint 8 Increment A;
/// <c>grob-vm-architecture.md</c> "Plugins and Native Functions"). A plugin registers its
/// natives and namespace constants against an <see cref="IPluginRegistrar"/> when loaded;
/// it never touches the VM's concrete type or the operating system directly — OS access
/// goes through the capability interfaces (<see cref="IStandardStreams"/>,
/// <see cref="IEnvironment"/>, <see cref="IClock"/>, <see cref="IRandomSource"/>), injected
/// separately by the host (D-343, refining D-319).
/// </summary>
public interface IGrobPlugin {
    /// <summary>The plugin's name, for diagnostics and the plugin-loader log.</summary>
    string Name { get; }

    /// <summary>Registers this plugin's natives and constants against <paramref name="registrar"/>.</summary>
    void Register(IPluginRegistrar registrar);
}
