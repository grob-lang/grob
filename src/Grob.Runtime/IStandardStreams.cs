namespace Grob.Runtime;

/// <summary>
/// The capability interface for Grob's standard I/O streams (Sprint 8 Increment A,
/// D-343, refining D-319). <c>print()</c>'s <c>OpCode.Print</c> handler routes through
/// <see cref="Out"/> instead of touching <see cref="Console"/> directly, so
/// <c>Grob.Stdlib</c> and <c>Grob.Vm</c> never depend on the OS console — the CLI host
/// supplies an OS-backed implementation at the composition root; a test or an embedding
/// host (the LSP, a future playground) supplies its own.
/// </summary>
public interface IStandardStreams {
    /// <summary>The stream <c>print()</c> writes to.</summary>
    TextWriter Out { get; }

    /// <summary>The stream diagnostics and <c>log.*</c> (Increment C) write to.</summary>
    TextWriter Error { get; }
}
