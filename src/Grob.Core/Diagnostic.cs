namespace Grob.Core;

/// <summary>
/// A single diagnostic — an error, warning, info message, or hint — reported during compilation or execution.
/// </summary>
public sealed record class Diagnostic(string Code, string Message, SourceRange Range, Severity Severity) {
    /// <summary>The error code, e.g. <c>E0001</c>.</summary>
    public string Code { get; init; } = ValidateCode(Code);

    /// <summary>The human-readable message.</summary>
    public string Message { get; init; } = ValidateMessage(Message);

    /// <summary>The source range the diagnostic points to.</summary>
    public SourceRange Range { get; init; } = Range;

    /// <summary>The severity of the diagnostic.</summary>
    public Severity Severity { get; init; } = Severity;

    /// <inheritdoc/>
    public override string ToString() =>
        $"{Severity.ToString().ToLowerInvariant()}[{Code}]: {Message}\n  --> {Range}";

    /// <summary>
    /// Raises a diagnostic against a catalog <see cref="ErrorDescriptor"/> at a
    /// source range, with a call-site-specific <paramref name="message"/>. This is
    /// the sanctioned way to construct a diagnostic: the code and severity come from
    /// the descriptor, so no <c>Exxxx</c> literal is written at a call site (D-308).
    /// Keep <paramref name="message"/> free of the code and title; it carries only
    /// the specifics (the actual types, names or counts involved).
    /// </summary>
    /// <param name="descriptor">The catalog descriptor for the code being raised. Must not be null.</param>
    /// <param name="range">The source range the diagnostic points to.</param>
    /// <param name="message">The call-site-specific message body.</param>
    public static Diagnostic Of(ErrorDescriptor descriptor, SourceRange range, string message) {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new Diagnostic(descriptor.Code, message, range, ToSeverity(descriptor.Severity));
    }

    /// <summary>
    /// Convenience overload pinning the diagnostic at a single
    /// <paramref name="location"/> (a zero-width range), for call sites that hold a
    /// point rather than a span.
    /// </summary>
    /// <param name="descriptor">The catalog descriptor for the code being raised. Must not be null.</param>
    /// <param name="location">The source location the diagnostic points to.</param>
    /// <param name="message">The call-site-specific message body.</param>
    public static Diagnostic Of(ErrorDescriptor descriptor, SourceLocation location, string message) =>
        Of(descriptor, new SourceRange(location), message);

    /// <summary>
    /// Maps a descriptor's registry classification onto the LSP-aligned
    /// <see cref="Severity"/> the diagnostic pipeline carries.
    /// </summary>
    private static Severity ToSeverity(DiagnosticSeverity severity) => severity switch {
        DiagnosticSeverity.Warning => Severity.Warning,
        _ => Severity.Error,
    };

    private static string ValidateCode(string code) {
        ArgumentNullException.ThrowIfNull(code);
        if (code.Length == 0)
            throw new ArgumentException("A diagnostic code must not be empty.", nameof(code));
        return code;
    }

    private static string ValidateMessage(string message) {
        ArgumentNullException.ThrowIfNull(message);
        return message;
    }
}
