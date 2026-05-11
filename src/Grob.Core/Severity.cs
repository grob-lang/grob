namespace Grob.Core;

/// <summary>
/// The severity of a <see cref="Diagnostic"/>.
/// Numeric values match LSP DiagnosticSeverity ordering.
/// </summary>
public enum Severity {
    /// <summary>A fatal error that prevents compilation or execution.</summary>
    Error = 1,

    /// <summary>A non-fatal issue that may indicate a problem.</summary>
    Warning = 2,

    /// <summary>An informational message.</summary>
    Info = 3,

    /// <summary>A hint for the user, typically a style or improvement suggestion.</summary>
    Hint = 4,
}
