using System.Text;
using System.Text.RegularExpressions;

namespace Grob.Core;

/// <summary>
/// The <c>Regex</c> and <c>Match</c> runtime layouts (D-432, D-440), defined once for the
/// assemblies that build or read them: the type checker, which validates a pattern
/// (E0602) with the options it will run under; the compiler, which emits a regex literal
/// as a constant <c>Regex</c> struct; and <c>Grob.Stdlib</c>'s <c>RegexPlugin</c>, which
/// matches. They are DAG siblings, so <c>Grob.Core</c> is the only home all of them can
/// read it from.
/// </summary>
/// <remarks>
/// A <c>Regex</c> is a <see cref="GrobStruct"/> with <see cref="RegexTypeName"/> and three
/// hidden fields: the pattern, the normalised flags and the timeout in seconds. A
/// <c>Match</c> carries <see cref="MatchTypeName"/> and five. No field name is a spellable
/// Grob identifier (leading double underscore), and a named type's properties are
/// read-only (E0204), so no script can alter a value's hidden state.
/// </remarks>
public static class RegexLayout {
    /// <summary>The struct type name every <c>Regex</c> value carries.</summary>
    public const string RegexTypeName = "Regex";

    /// <summary>The struct type name every <c>Match</c> value carries.</summary>
    public const string MatchTypeName = "Match";

    /// <summary>A <c>Regex</c>'s hidden field holding its source pattern.</summary>
    public const string PatternFieldName = "__pattern";

    /// <summary>A <c>Regex</c>'s hidden field holding its normalised flags.</summary>
    public const string FlagsFieldName = "__flags";

    /// <summary>A <c>Regex</c>'s hidden field holding its timeout in seconds; 0 is no limit.</summary>
    public const string TimeoutFieldName = "__timeout";

    /// <summary>A <c>Match</c>'s hidden field holding the matched text.</summary>
    public const string ValueFieldName = "__value";

    /// <summary>A <c>Match</c>'s hidden field holding its zero-based position in the input.</summary>
    public const string IndexFieldName = "__index";

    /// <summary>A <c>Match</c>'s hidden field holding the matched text's length.</summary>
    public const string LengthFieldName = "__length";

    /// <summary>A <c>Match</c>'s hidden field holding every numbered group's text.</summary>
    public const string GroupsFieldName = "__groups";

    /// <summary>A <c>Match</c>'s hidden field mapping each participating named group to its text.</summary>
    public const string NamedGroupsFieldName = "__named";

    /// <summary>
    /// The limit on every matching operation unless <c>regex.compile</c> sets another
    /// (D-432 Decision 8).
    /// </summary>
    public const long DefaultTimeoutSeconds = 5;

    /// <summary>
    /// The largest timeout .NET accepts, in whole seconds: a match timeout must be
    /// below <see cref="int.MaxValue"/> milliseconds. A larger value is a
    /// <c>ParseError</c> at run time (D-440).
    /// </summary>
    public const long MaxTimeoutSeconds = (int.MaxValue - 1L) / 1000;

    /// <summary>
    /// Normalises a flag string to its deduplicated canonical form, <c>i</c> before
    /// <c>m</c> (D-440). Fails on any other character; the flags are <c>i</c> and
    /// <c>m</c> only (D-089).
    /// </summary>
    public static bool TryNormaliseFlags(string flags, out string normalised) {
        bool ignoreCase = false;
        bool multiline = false;
        foreach (char flag in flags) {
            switch (flag) {
                case 'i': ignoreCase = true; break;
                case 'm': multiline = true; break;
                default:
                    normalised = "";
                    return false;
            }
        }
        normalised = (ignoreCase ? "i" : "") + (multiline ? "m" : "");
        return true;
    }

    /// <summary>
    /// The .NET options for normalised <paramref name="flags"/>. Matching is always
    /// culture-invariant, so <c>i</c> means the same on every machine (D-432 Decision 4).
    /// </summary>
    public static RegexOptions ToOptions(string flags) {
        RegexOptions options = RegexOptions.CultureInvariant;
        if (flags.Contains('i')) options |= RegexOptions.IgnoreCase;
        if (flags.Contains('m')) options |= RegexOptions.Multiline;
        return options;
    }

    /// <summary>
    /// Converts a timeout in seconds to a .NET match timeout: <c>0</c> is no limit, as
    /// <c>process.run</c>'s is. Fails on a negative value or one above
    /// <see cref="MaxTimeoutSeconds"/> (D-440).
    /// </summary>
    public static bool TryToTimeout(long seconds, out TimeSpan timeout) {
        if (seconds is < 0 or > MaxTimeoutSeconds) {
            timeout = default;
            return false;
        }
        timeout = seconds == 0 ? Regex.InfiniteMatchTimeout : TimeSpan.FromSeconds(seconds);
        return true;
    }

    /// <summary>
    /// Describes why a pattern failed to parse without quoting it: .NET's own message
    /// embeds the pattern, and a pattern built at run time is a value, which D-077 keeps
    /// out of messages. The description is the error kind in words, for example
    /// "insufficient closing parentheses".
    /// </summary>
    public static string DescribeParseError(RegexParseException exception) {
        string name = exception.Error.ToString();
        StringBuilder words = new(name.Length + 8);
        for (int i = 0; i < name.Length; i++) {
            if (i > 0 && char.IsUpper(name[i])) words.Append(' ');
            words.Append(char.ToLowerInvariant(name[i]));
        }
        return words.ToString();
    }

    /// <summary>
    /// Builds a <c>Regex</c> value. <paramref name="flags"/> must already be normalised
    /// and <paramref name="timeoutSeconds"/> already validated.
    /// </summary>
    public static GrobStruct NewRegex(string pattern, string flags, long timeoutSeconds) =>
        new(RegexTypeName, [
            new(PatternFieldName, GrobValue.FromString(pattern)),
            new(FlagsFieldName, GrobValue.FromString(flags)),
            new(TimeoutFieldName, GrobValue.FromInt(timeoutSeconds)),
        ]);
}
