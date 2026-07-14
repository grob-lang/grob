using Grob.Core;
using Grob.Runtime;

namespace Grob.Stdlib;

/// <summary>
/// The severity ordering <see cref="LogPlugin"/>'s threshold check compares against —
/// <c>Debug &lt; Info &lt; Warning &lt; Error</c>. Deliberately a plain internal enum
/// rather than a public surface: the four levels are exposed to Grob scripts only as the
/// lowercase strings <c>log.setLevel</c> recognises, never as a first-class value.
/// </summary>
public enum LogLevel {
    /// <summary>Verbose diagnostic detail; visible only under <c>--verbose</c> (or an explicit <c>log.setLevel("debug")</c>).</summary>
    Debug,

    /// <summary>General informational messages — the default threshold.</summary>
    Info,

    /// <summary>Recoverable but noteworthy conditions.</summary>
    Warning,

    /// <summary>Failures serious enough to always report.</summary>
    Error,
}

/// <summary>
/// The <c>log</c> module (D-342): four-level stderr logging gated by a mutable threshold,
/// through the injected <see cref="IStandardStreams"/> capability (D-343). The CLI host
/// picks the initial threshold — <see cref="LogLevel.Debug"/> under <c>--verbose</c>,
/// <see cref="LogLevel.Info"/> otherwise — via the constructor; <c>log.setLevel</c> then
/// lets a running script change it. A message writes to <see cref="IStandardStreams.Error"/>
/// only when its own level is at or above the current threshold; it is never routed to
/// <see cref="IStandardStreams.Out"/>, keeping program output and diagnostics on separate
/// streams (the same convention <c>RunCommand</c>'s class doc already documents for
/// <c>print</c> vs diagnostics). <c>log.setLevel</c> recognises exactly the four lowercase
/// level names (matching the native function names); any other string is a silent no-op —
/// the spec is silent on invalid levels, and a no-op (rather than throwing or allocating a
/// new error code) keeps a typo in a log call from crashing an otherwise-working script.
/// Registers exactly the qualified names listed in the compile-time twin,
/// <c>NamespaceRegistry</c>'s <c>log</c> entry in <c>Grob.Compiler</c>.
/// </summary>
public sealed class LogPlugin : IGrobPlugin {
    private readonly IStandardStreams _streams;
    private LogLevel _threshold;

    /// <summary>Initialises the plugin with the streams it writes to and its starting threshold.</summary>
    public LogPlugin(IStandardStreams streams, LogLevel initialLevel) {
        ArgumentNullException.ThrowIfNull(streams);
        _streams = streams;
        _threshold = initialLevel;
    }

    /// <inheritdoc/>
    public string Name => "log";

    /// <inheritdoc/>
    public void Register(IPluginRegistrar registrar) {
        ArgumentNullException.ThrowIfNull(registrar);

        registrar.RegisterNative("log.debug", new NativeFunction("log.debug", 1, (args, _) =>
            WriteAtLevel(LogLevel.Debug, args[0].AsString())));

        registrar.RegisterNative("log.info", new NativeFunction("log.info", 1, (args, _) =>
            WriteAtLevel(LogLevel.Info, args[0].AsString())));

        registrar.RegisterNative("log.warning", new NativeFunction("log.warning", 1, (args, _) =>
            WriteAtLevel(LogLevel.Warning, args[0].AsString())));

        registrar.RegisterNative("log.error", new NativeFunction("log.error", 1, (args, _) =>
            WriteAtLevel(LogLevel.Error, args[0].AsString())));

        registrar.RegisterNative("log.setLevel", new NativeFunction("log.setLevel", 1, (args, _) => {
            if (TryParseLevel(args[0].AsString(), out LogLevel level)) _threshold = level;
            return GrobValue.Nil;
        }));
    }

    private GrobValue WriteAtLevel(LogLevel level, string message) {
        if (level >= _threshold) _streams.Error.WriteLine(message);
        return GrobValue.Nil;
    }

    private static bool TryParseLevel(string name, out LogLevel level) {
        switch (name) {
            case "debug": level = LogLevel.Debug; return true;
            case "info": level = LogLevel.Info; return true;
            case "warning": level = LogLevel.Warning; return true;
            case "error": level = LogLevel.Error; return true;
            default: level = default; return false;
        }
    }
}
