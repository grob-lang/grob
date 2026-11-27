using System.Text.RegularExpressions;

namespace Grob.Torture;

/// <summary>
/// Removes the values from an exception's message and keeps its shape (gate report §4.7):
/// paths become <c>&lt;path&gt;</c>, quoted text <c>'…'</c> or <c>"…"</c>, and numbers <c>#</c>.
/// Kind and type names stay, since they discriminate: R-63's <c>expected Float, actual Int</c>
/// is not R-60's <c>kind Int is not callable</c>.
/// </summary>
internal static partial class MessageNormaliser {
    [GeneratedRegex(@"[A-Za-z]:[\\/][^\s'""]*|(?<![\w.])/[\w.-]+(?:/[\w.-]+)*/?")]
    private static partial Regex PathPattern();

    [GeneratedRegex(@"'[^']*'")]
    private static partial Regex SingleQuotedPattern();

    [GeneratedRegex(@"""[^""]*""")]
    private static partial Regex DoubleQuotedPattern();

    [GeneratedRegex(@"(?<![\w.])-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?(?!\w)")]
    private static partial Regex NumberPattern();

    /// <summary>The message with its values removed.</summary>
    public static string Normalise(string message) {
        string text = PathPattern().Replace(message, "<path>");
        text = SingleQuotedPattern().Replace(text, "'…'");
        text = DoubleQuotedPattern().Replace(text, "\"…\"");
        return NumberPattern().Replace(text, "#");
    }
}
