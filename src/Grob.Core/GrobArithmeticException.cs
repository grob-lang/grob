namespace Grob.Core;

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
