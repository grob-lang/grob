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

    /// <summary>
    /// The stream the <c>input()</c> built-in (Sprint 8 Increment C) reads from —
    /// <c>Console.In</c> in production, a caller-supplied <see cref="TextReader"/> in
    /// tests or an embedding host. A <see langword="null"/> line from
    /// <see cref="TextReader.ReadLine"/> (stdin closed/exhausted) is the native's own
    /// concern to translate into a catchable fault; this interface only names the source.
    /// </summary>
    TextReader In { get; }
}
