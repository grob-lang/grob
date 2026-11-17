using System.Text.RegularExpressions;

using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Core;

namespace Grob.Compiler;

public sealed partial class TypeChecker {
    // -----------------------------------------------------------------------
    // Regex (D-432, D-440): a literal is a Regex value, and every pattern known at
    // compile time is validated by .NET's own parser — E0602, the compile-time twin of
    // the ParseError regex.compile throws at run time.
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// A regex literal is a <c>Regex</c> (a <see cref="GrobType.Struct"/> named by
    /// <see cref="GetStructTypeName"/>), whatever its pattern, so an invalid one reports
    /// E0602 once and does not cascade through the members it is used with.
    /// </remarks>
    public override GrobType VisitRegexLiteral(RegexLiteralExpr node) {
        // E2007 has already reported any flag other than 'i' or 'm'; validate without them.
        string flags = RegexLayout.TryNormaliseFlags(node.Flags, out string normalised) ? normalised : "";
        // The body reaches .NET verbatim, so .NET's offset maps onto the source exactly.
        ValidateRegexPattern(node.Pattern, flags, node.Range, _ => true);
        return GrobType.Struct;
    }

    /// <summary>
    /// E0602 on a string-literal pattern passed to a <c>regex</c> module function or to
    /// <c>regex.compile</c>. Every member but <c>escape</c> takes the pattern first; a
    /// pattern that is not a literal is left to run time (<c>ParseError</c>, E5702).
    /// </summary>
    private void CheckRegexPatternArgument(CallExpr node, string namespaceName, string memberName) {
        if (namespaceName != "regex" || memberName == "escape" || node.Arguments.Count == 0) return;
        CheckRegexPatternLiteral(node.Arguments[0].Value);
    }

    /// <summary>
    /// Validates <paramref name="argument"/> as a pattern when it is a string literal: a
    /// backtick raw string as written, a hole-free double-quoted string after its escapes
    /// are decoded, so it is checked as it will run. Anything else is not a compile-time
    /// pattern and is skipped.
    /// </summary>
    private void CheckRegexPatternLiteral(Expression argument) {
        switch (argument) {
            case RawStringLiteralExpr raw: {
                    bool exact = SpansOneLine(raw.Range, raw.Value.Length);
                    ValidateRegexPattern(raw.Value, "", raw.Range, _ => exact);
                    break;
                }
            case InterpolatedStringExpr { Parts: var parts } quoted when parts.All(p => p is StringTextPart): {
                    string text = string.Concat(parts.OfType<StringTextPart>().Select(p => p.Text));
                    bool exact = SpansOneLine(quoted.Range, text.Length);
                    // Before the first escape, the written and decoded texts coincide.
                    int firstEscape = text.IndexOf('\\');
                    ValidateRegexPattern(StringEscapes.Decode(text), "", quoted.Range,
                        offset => exact && (firstEscape < 0 || firstEscape >= offset));
                    break;
                }
        }
    }

    /// <summary>
    /// Whether a literal is its body between single one-character delimiters on one line,
    /// so a column inside it is the delimiter's column plus one plus an index into the
    /// body. A triple-delimited or multi-line literal is not.
    /// </summary>
    private static bool SpansOneLine(SourceRange range, int bodyLength) =>
        range.Start.Line == range.End.Line && range.End.Column - range.Start.Column + 1 == bodyLength + 2;

    /// <summary>
    /// Builds <paramref name="pattern"/> with the options it will run under and reports
    /// E0602 if .NET rejects it. The caret sits at .NET's offset when
    /// <paramref name="mapsExactly"/> says that offset maps onto the source, and at the
    /// literal's opening delimiter otherwise.
    /// </summary>
    private void ValidateRegexPattern(string pattern, string flags, SourceRange literal, Func<int, bool> mapsExactly) {
        try {
            _ = new Regex(pattern, RegexLayout.ToOptions(flags), TimeSpan.FromSeconds(RegexLayout.DefaultTimeoutSeconds));
        } catch (RegexParseException ex) {
            SourceLocation start = literal.Start;
            SourceLocation caret = mapsExactly(ex.Offset) ? start with { Column = start.Column + 1 + ex.Offset } : start;
            EmitError(ErrorCatalog.E0602,
                $"'{pattern}' is not a valid regex pattern: {RegexLayout.DescribeParseError(ex)}.",
                new SourceRange(caret));
        }
    }
}
