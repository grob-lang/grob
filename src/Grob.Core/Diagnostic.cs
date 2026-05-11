namespace Grob.Core;

/// <summary>
/// A single diagnostic — an error, warning, info message, or hint — reported during compilation or execution.
/// </summary>
public sealed record class Diagnostic(string Code, string Message, SourceRange Range, Severity Severity) {
    /// <summary>The error code, e.g. <c>E0001</c>.</summary>
    public string Code { get; init; } = ValidateCode(Code);

    /// <summary>The human-readable message.</summary>
    public string Message { get; init; } = ValidateMessage(Message);

    /// <inheritdoc/>
    public override string ToString() =>
        $"{Severity.ToString().ToLowerInvariant()}[{Code}]: {Message}\n  --> {Range}";

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
