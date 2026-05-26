namespace Grob.Core;

/// <summary>
/// Base type for Grob runtime errors raised by the VM. The two-mode error
/// model (D-284): the compiler/checker collect all errors; the VM stops on
/// the first runtime error. Carries the error code from grob-error-codes.md
/// and the source position (line + column) from the chunk's per-instruction
/// position arrays. <see cref="Column"/> is <c>0</c> when no column was
/// recorded for the failing instruction.
/// </summary>
public class GrobRuntimeException : Exception {
    /// <summary>The grob-error-codes.md identifier (e.g. <c>E5001</c>).</summary>
    public string Code { get; }

    /// <summary>The source line attributed to the failing instruction.</summary>
    public int Line { get; }

    /// <summary>
    /// The 1-based source column attributed to the failing instruction.
    /// <c>0</c> indicates the chunk byte was written without a column
    /// (e.g. hand-built test bytecode or synthetic prologue).
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobRuntimeException"/> with the supplied
    /// error <paramref name="code"/>, source <paramref name="line"/>, and
    /// human-readable <paramref name="message"/>. <see cref="Column"/> is
    /// recorded as <c>0</c> ("unknown"); prefer the four-argument constructor
    /// when a column is available.
    /// </summary>
    public GrobRuntimeException(string code, int line, string message)
        : this(code, line, 0, message) { }

    /// <summary>
    /// Initialises a new <see cref="GrobRuntimeException"/> with the supplied
    /// error <paramref name="code"/>, source <paramref name="line"/>,
    /// 1-based <paramref name="column"/>, and human-readable
    /// <paramref name="message"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="code"/> is <see langword="null"/> or empty,
    /// or when <paramref name="message"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="line"/> is less than <c>1</c> or
    /// <paramref name="column"/> is negative.
    /// </exception>
    public GrobRuntimeException(string code, int line, int column, string message)
        : base(message) {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        ArgumentException.ThrowIfNullOrEmpty(message);
        Code = code;
        Line = line;
        Column = column;
    }
}
