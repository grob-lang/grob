namespace Grob.Runtime;

/// <summary>
/// The lexical identifier rule the signature types validate names against: a leading
/// letter or underscore followed by letters, digits or underscores. It restates the
/// lexer's <c>IsIdentStart</c>/<c>IsIdentContinue</c> pair rather than referencing it,
/// because <c>Grob.Runtime</c> cannot see <c>Grob.Compiler</c> (the DAG). A qualified
/// name is one or more identifiers joined by single dots (<c>"math.sqrt"</c>,
/// <c>"guid.namespaces.dns"</c>).
/// </summary>
internal static class Identifiers {
    /// <summary><see langword="true"/> when <paramref name="value"/> is a single identifier.</summary>
    internal static bool IsIdentifier(ReadOnlySpan<char> value) {
        if (value.IsEmpty || !IsStart(value[0])) return false;
        for (int i = 1; i < value.Length; i++) {
            if (!IsContinue(value[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// <see langword="true"/> when <paramref name="value"/> is one or more identifiers
    /// joined by single dots. A leading, trailing or doubled dot fails.
    /// </summary>
    internal static bool IsQualifiedName(string value) {
        foreach (Range segment in value.AsSpan().Split('.')) {
            if (!IsIdentifier(value.AsSpan(segment))) return false;
        }
        return true;
    }

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is
    /// <see langword="null"/> and <see cref="ArgumentException"/> when it is not a single
    /// identifier. <paramref name="what"/> names the thing in the message
    /// (<c>"A parameter name"</c>).
    /// </summary>
    internal static void RequireIdentifier(string value, string paramName, string what) {
        ArgumentNullException.ThrowIfNull(value, paramName);
        if (!IsIdentifier(value)) {
            throw new ArgumentException($"{what} must be an identifier; '{value}' is not.", paramName);
        }
    }

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/> when <paramref name="value"/> is
    /// <see langword="null"/> and <see cref="ArgumentException"/> when it is not a
    /// qualified name.
    /// </summary>
    internal static void RequireQualifiedName(string value, string paramName, string what) {
        ArgumentNullException.ThrowIfNull(value, paramName);
        if (!IsQualifiedName(value)) {
            throw new ArgumentException(
                $"{what} must be an identifier or dot-separated identifiers; '{value}' is not.", paramName);
        }
    }

    private static bool IsStart(char c) => c == '_' || char.IsLetter(c);

    private static bool IsContinue(char c) => c == '_' || char.IsLetterOrDigit(c);
}
