namespace Grob.Core;

/// <summary>
/// Base type for Grob runtime errors raised by the VM. The two-mode error
/// model (D-284): the compiler/checker collect all errors; the VM stops on
/// the first runtime error. Carries the error code from grob-error-codes.md
/// and the source line from the chunk's per-instruction line array.
/// </summary>
public class GrobRuntimeException : Exception {
    /// <summary>The grob-error-codes.md identifier (e.g. <c>E5001</c>).</summary>
    public string Code { get; }

    /// <summary>The source line attributed to the failing instruction.</summary>
    public int Line { get; }

    /// <summary>
    /// Initialises a new <see cref="GrobRuntimeException"/> with the supplied
    /// error <paramref name="code"/>, source <paramref name="line"/>, and
    /// human-readable <paramref name="message"/>.
    /// </summary>
    public GrobRuntimeException(string code, int line, string message)
        : base(message) {
        Code = code;
        Line = line;
    }
}

/// <summary>
/// Arithmetic runtime error: integer overflow, division by zero, modulo by
/// zero, math domain violations. Maps to the Grob <c>ArithmeticError</c>
/// exception type (D-284).
/// </summary>
public sealed class GrobArithmeticException : GrobRuntimeException {
    /// <summary>
    /// Initialises a new <see cref="GrobArithmeticException"/> with the
    /// supplied error <paramref name="code"/>, source <paramref name="line"/>,
    /// and human-readable <paramref name="message"/>.
    /// </summary>
    public GrobArithmeticException(string code, int line, string message)
        : base(code, line, message) { }
}
