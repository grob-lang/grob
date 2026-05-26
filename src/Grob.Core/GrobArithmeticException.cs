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
    /// and human-readable <paramref name="message"/>. <see cref="GrobRuntimeException.Column"/>
    /// is recorded as <c>0</c>.
    /// </summary>
    public GrobArithmeticException(string code, int line, string message)
        : base(code, line, message) { }

    /// <summary>
    /// Initialises a new <see cref="GrobArithmeticException"/> with the
    /// supplied error <paramref name="code"/>, source <paramref name="line"/>,
    /// 1-based <paramref name="column"/>, and human-readable
    /// <paramref name="message"/>.
    /// </summary>
    public GrobArithmeticException(string code, int line, int column, string message)
        : base(code, line, column, message) { }
}
