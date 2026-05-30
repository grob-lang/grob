namespace Grob.Core;

/// <summary>
/// The phase/category an error code belongs to. Mirrors the category map in
/// grob-error-codes.md and the thousands-digit scheme in ADR-0014.
/// </summary>
public enum ErrorCategory {
    /// <summary>Type-system errors (E0001–E0999).</summary>
    Type,

    /// <summary>Name-resolution errors (E1001–E1999).</summary>
    NameResolution,

    /// <summary>Syntax errors (E2001–E2999).</summary>
    Syntax,

    /// <summary>Module and import errors (E3001–E3999).</summary>
    Module,

    /// <summary>Parameter and decorator errors (E4001–E4999).</summary>
    ParamDecorator,

    /// <summary>Runtime errors (E5001–E5999).</summary>
    Runtime,

    /// <summary>Internal compiler/VM errors (E9001–E9999).</summary>
    Internal,
}

/// <summary>
/// Lifecycle status of a code under ADR-0017. Pre-release codes may change
/// until v1.0; stable codes are immutable.
/// </summary>
public enum ErrorStatus {
    /// <summary>Pre-release code; may change until v1.0.</summary>
    PreRelease,

    /// <summary>Stable code; immutable under ADR-0017.</summary>
    Stable,

    /// <summary>Retained but discouraged; slated for removal.</summary>
    Deprecated,

    /// <summary>Withdrawn code; must not be reused.</summary>
    Retired,
}

/// <summary>
/// Whether a diagnostic stops compilation or is advisory. Most codes are
/// errors; a small number (for example E1101 shadowed declaration) are warnings.
/// </summary>
public enum DiagnosticSeverity {
    /// <summary>A diagnostic that stops compilation or execution.</summary>
    Error,

    /// <summary>An advisory diagnostic that does not stop compilation.</summary>
    Warning,
}

/// <summary>
/// The GrobError leaf a runtime code corresponds to (D-284 ten-leaf hierarchy).
/// Null for compile-time codes, which do not throw.
/// </summary>
public enum GrobErrorLeaf {
    /// <summary>Arithmetic faults such as overflow or division by zero.</summary>
    ArithmeticError,

    /// <summary>Index out of range.</summary>
    IndexError,

    /// <summary>Nil dereference at runtime.</summary>
    NilError,

    /// <summary>I/O failures.</summary>
    IoError,

    /// <summary>Network failures.</summary>
    NetworkError,

    /// <summary>JSON parsing or serialisation failures.</summary>
    JsonError,

    /// <summary>Subprocess failures.</summary>
    ProcessError,

    /// <summary>Parse failures at runtime.</summary>
    ParseError,

    /// <summary>Key or member lookup failures.</summary>
    LookupError,

    /// <summary>Residual catch-all runtime failure.</summary>
    RuntimeError,
}

/// <summary>
/// The compile-time-constant metadata for one error code. One descriptor per
/// row of the error-code registry (grob-error-codes.md). Carries everything
/// that is fixed for the code; the call-site-specific message is supplied when
/// a <see cref="Diagnostic"/> is raised against the descriptor.
/// </summary>
/// <param name="Code">The Exxxx identifier, for example "E0002".</param>
/// <param name="Title">Short title rendered after error[Exxxx]:.</param>
/// <param name="Category">The category/phase the code belongs to.</param>
/// <param name="Status">Lifecycle status under ADR-0017.</param>
/// <param name="Severity">Whether the diagnostic is an error or a warning.</param>
/// <param name="Throws">
/// The GrobError leaf for runtime codes; null for compile-time codes.
/// </param>
public sealed record ErrorDescriptor(
    string Code,
    string Title,
    ErrorCategory Category,
    ErrorStatus Status,
    DiagnosticSeverity Severity,
    GrobErrorLeaf? Throws);
