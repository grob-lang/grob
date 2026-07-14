using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// <c>print</c>/<c>exit</c> stay the D-343 formalisation placeholder — they are built-in
/// from Sprint 2 and stay on their existing dedicated <c>OpCode.Print</c>/<c>OpCode.Exit</c>
/// opcodes, never converted into <c>NativeFunction</c>s dispatched through <c>Call</c>.
/// Sprint 8 Increment C gives this plugin its first real callable: the bare (no-namespace)
/// <c>input</c> native, registered under the plain name <c>"input"</c> (no
/// <c>"module."</c> prefix — <see cref="IPluginRegistrar.RegisterNative"/>'s own doc comment
/// anticipates this "a qualified name … or a bare name for a top-level built-in" case).
/// Writes its prompt argument to <see cref="IStandardStreams.Out"/> with <c>Write</c> (never
/// <c>WriteLine</c> — a prompt has no trailing newline of its own), flushes, then reads one
/// line from <see cref="IStandardStreams.In"/>. A <see langword="null"/> line (stdin closed
/// or exhausted) becomes a catchable <c>IoError</c> via the native-throw seam (D-342),
/// reusing the residual <see cref="ErrorCatalog.E5305"/> — no more specific existing code
/// covers "stdin closed", and allocating a new one is out of this increment's scope. The
/// compiler always supplies exactly one argument (a 0-argument script-level call is filled
/// with <c>""</c> at the call site, <c>Compiler.Expressions.cs VisitCall</c>), so this
/// native's own arity is 1, never 0.
/// </summary>
public sealed class IoPlugin : IGrobPlugin {
    private readonly IStandardStreams _streams;

    /// <summary>Initialises the plugin with the <see cref="IStandardStreams"/> <c>input</c> reads/writes through.</summary>
    public IoPlugin(IStandardStreams streams) {
        ArgumentNullException.ThrowIfNull(streams);
        _streams = streams;
    }

    /// <inheritdoc/>
    public string Name => "io";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("input", new NativeFunction("input", 1, (args, _) => {
            _streams.Out.Write(args[0].AsString());
            _streams.Out.Flush();

            string? line = _streams.In.ReadLine();
            if (line is null) {
                throw new NativeFaultException("IoError", ErrorCatalog.E5305.Code,
                    "input(): stdin is closed");
            }
            return GrobValue.FromString(line);
        }));
    }
}
