namespace Grob.Core;

/// <summary>
/// Uncatchable internal signal thrown by the <c>exit(code)</c> built-in to
/// unwind the VM call stack and deliver a process exit code to the host.
/// This is not a user-visible error — it is a control-flow signal.
/// The host (e.g. <c>RunCommand</c>) catches this and returns its
/// <see cref="Code"/> as the process exit code.
/// </summary>
public sealed class GrobExitException : Exception {
    /// <summary>The exit code the script requested.</summary>
    public int Code { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobExitException"/> with the requested
    /// exit <paramref name="code"/>.
    /// </summary>
    public GrobExitException(int code) : base($"exit({code})") {
        Code = code;
    }
}
