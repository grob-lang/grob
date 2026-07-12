using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The formalisation placeholder for <c>print</c>/<c>exit</c> (D-343). They are built-in
/// from Sprint 2 and stay on their existing dedicated <c>OpCode.Print</c>/<c>OpCode.Exit</c>
/// opcodes — they are not converted into <c>NativeFunction</c>s dispatched through
/// <c>Call</c>, so this plugin registers no callable. It exists so the plugin
/// auto-registration pass has a uniform place documenting where the I/O capability seam
/// (<see cref="IStandardStreams"/>, consumed directly by the VM's <c>OpCode.Print</c>
/// handler) sits, alongside every other core module.
/// </summary>
public sealed class IoPlugin : IGrobPlugin {
    /// <inheritdoc/>
    public string Name => "io";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);
        // Intentionally empty: print/exit are formalised via the IStandardStreams
        // capability the VM consumes directly, not via native registration.
    }
}
