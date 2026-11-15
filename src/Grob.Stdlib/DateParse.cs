using System.Globalization;
using System.Text.RegularExpressions;

namespace Grob.Stdlib;

/// <summary>
/// <c>date.parse</c>'s resolution against an injected clock and zone (R-32), host-free. The
/// parse splits on whether the input carries an offset, and .NET cannot make that split
/// without reading the host zone: its <see cref="DateTimeOffset"/> parsers give offset-free
/// input the host's offset, and its <see cref="DateTime"/> parsers convert offset-bearing
/// input through the host zone. So the split is lexical. Offset-free input is parsed as a
/// local wall time and given the zone's offset for it; input with an explicit offset keeps
/// that offset. Input with a time and no date takes today's date: in the injected zone when
/// it has no offset, in its own offset when it has one (.NET's convention), both from the
/// injected clock.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DateTimeStyles.NoCurrentDateDefault"/> reports "no date" as 1 January of year
/// 1, so an input naming that exact date is read as time-only. No script dates anything to
/// year 1; the edge is stated rather than guarded.
/// </para>
/// <para>
/// A pattern with a month or day but no year has the injected clock's year appended as a
/// literal before parsing, because .NET fills a missing year from the host clock and
/// <see cref="DateTimeStyles.NoCurrentDateDefault"/> covers only a wholly missing date. The
/// one-argument form has no pattern to extend: a free-form partial date such as
/// <c>"Jul 1"</c> still takes the host's year.
/// </para>
/// </remarks>
internal static partial class DateParse {
    private const string YearSeparator = "|";
    /// <summary>
    /// Parses an ISO-8601 or invariant-culture input. Returns <see langword="false"/> when
    /// <paramref name="input"/> is not a date.
    /// </summary>
    internal static bool TryParseIso(string input, DateTime utcNow, TimeZoneInfo zone, out DateTimeOffset result) {
        Match offset = TrailingIsoOffset().Match(input);
        if (offset.Success) {
            if (!DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
                    || !DateTime.TryParse(offset.Groups["body"].Value, CultureInfo.InvariantCulture,
                        DateTimeStyles.NoCurrentDateDefault, out DateTime wall)) {
                return Fail(out result);
            }
            if (HasNoDate(wall)) {
                result = TodayInOwnOffset(result, utcNow);
            }
            return true;
        }
        if (!DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime local)) {
            return Fail(out result);
        }
        if (local.Kind != DateTimeKind.Unspecified) {
            // A zone designator the lexical split does not recognise (a trailing "GMT"): the
            // DateTimeOffset parser keeps that designator's offset, but dates a time-only input
            // from the host clock, so the injected clock replaces that date.
            if (!DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
                return false;
            }
            if (local.Year == 1) {
                result = TodayInOwnOffset(result, utcNow);
            }
            return true;
        }
        result = InZone(local, utcNow, zone);
        return true;
    }

    /// <summary>
    /// Parses <paramref name="input"/> against a .NET custom or standard format
    /// <paramref name="pattern"/>. Returns <see langword="false"/> when it does not match.
    /// </summary>
    internal static bool TryParsePattern(
            string input, string pattern, DateTime utcNow, TimeZoneInfo zone, out DateTimeOffset result) {
        if (pattern is "u" or "r" or "R") {
            // UTC by definition, though DateTime reads their Z or GMT as a literal.
            return DateTimeOffset.TryParseExact(input, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }
        string custom = pattern is "m" or "M" ? CultureInfo.InvariantCulture.DateTimeFormat.MonthDayPattern : pattern;
        PatternShape shape = Shape(custom);
        if (shape.HasOffset && !(shape.OffsetIsOptional && TryParseLocal(input, WithoutK(custom), shape, utcNow, zone, out _))) {
            return TryParseOffset(input, custom, shape, utcNow, out result);
        }
        string offsetFree = shape.OffsetIsOptional ? WithoutK(custom) : custom;
        if (!TryParseLocal(input, offsetFree, shape, utcNow, zone, out DateTime local)) {
            return Fail(out result);
        }
        if (local.Kind != DateTimeKind.Unspecified) {
            // A standard format whose designator the specifier scan cannot see ("o"): the
            // DateTimeOffset parser keeps the input's offset.
            return DateTimeOffset.TryParseExact(input, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }
        result = InZone(local, utcNow, zone);
        return true;
    }

    /// <summary>
    /// Parses offset-free input, appending today's year in the zone when the pattern names a
    /// month or day but no year.
    /// </summary>
    private static bool TryParseLocal(
            string input, string pattern, PatternShape shape, DateTime utcNow, TimeZoneInfo zone, out DateTime local) {
        (string text, string format) = shape.NeedsYear ? WithYear(input, pattern, TodayIn(zone, utcNow).Year) : (input, pattern);
        return DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out local);
    }

    /// <summary>
    /// Parses input with an explicit offset. A missing year is the year in that offset, so the
    /// offset is read first and the input re-parsed when that year differs from the zone-free guess.
    /// </summary>
    private static bool TryParseOffset(string input, string pattern, PatternShape shape, DateTime utcNow, out DateTimeOffset result) {
        if (!shape.NeedsYear) {
            if (!DateTimeOffset.TryParseExact(input, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
                return false;
            }
            if (!shape.HasDate) {
                result = TodayInOwnOffset(result, utcNow);
            }
            return true;
        }
        int guess = utcNow.Year;
        (string text, string format) = WithYear(input, pattern, guess);
        if (!DateTimeOffset.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
            return false;
        }
        int year = (utcNow + result.Offset).Year;
        return year == guess
            || DateTimeOffset.TryParseExact(WithYear(input, pattern, year).Input, format,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static (string Input, string Pattern) WithYear(string input, string pattern, int year) =>
        (input + YearSeparator + year.ToString("D4", CultureInfo.InvariantCulture), pattern + "'" + YearSeparator + "'yyyy");

    /// <summary>
    /// Gives an offset-free wall time the zone's offset for it, dating it today in the zone
    /// when the input had no date. A time in a daylight-saving gap or overlap takes what
    /// <see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/> gives it, the standard offset.
    /// </summary>
    private static DateTimeOffset InZone(DateTime local, DateTime utcNow, TimeZoneInfo zone) {
        DateTime wall = HasNoDate(local) ? TodayIn(zone, utcNow).Add(local.TimeOfDay) : local;
        return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }

    private static bool Fail(out DateTimeOffset result) {
        result = default;
        return false;
    }

    private static bool HasNoDate(DateTime value) => value.Date == DateTime.MinValue.Date;

    private static DateTime TodayIn(TimeZoneInfo zone, DateTime utcNow) => DateTime.SpecifyKind(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone).Date, DateTimeKind.Unspecified);

    private static DateTimeOffset TodayInOwnOffset(DateTimeOffset parsed, DateTime utcNow) {
        DateTime today = DateTime.SpecifyKind((utcNow + parsed.Offset).Date, DateTimeKind.Unspecified);
        return new DateTimeOffset(today.Add(parsed.TimeOfDay), parsed.Offset);
    }

    /// <summary>What a pattern's specifiers say about the input it parses.</summary>
    /// <param name="HasDate">A year, month or day of the month is named.</param>
    /// <param name="NeedsYear">A month or day is named but no year, which .NET fills from the host.</param>
    /// <param name="HasOffset">A <c>z</c> or <c>K</c> is named.</param>
    /// <param name="OffsetIsOptional">Only <c>K</c> is named, which also accepts an absent offset.</param>
    private readonly record struct PatternShape(bool HasDate, bool NeedsYear, bool HasOffset, bool OffsetIsOptional) {
        /// <summary>A single-character standard format, whose letter is not a custom specifier.</summary>
        public static PatternShape Standard { get; } = new(true, false, false, false);
    }

    private static PatternShape Shape(string pattern) {
        if (pattern.Length == 1) {
            return PatternShape.Standard;
        }
        List<(char Letter, int Index)> specifiers = Specifiers(pattern);
        bool year = specifiers.Exists(s => s.Letter == 'y');
        bool monthOrDay = specifiers.Exists(s => s.Letter == 'M') || HasDayOfMonth(specifiers);
        bool offset = specifiers.Exists(s => s.Letter == 'z');
        bool optional = specifiers.Exists(s => s.Letter == 'K');
        return new PatternShape(year || monthOrDay, monthOrDay && !year, offset || optional, optional && !offset);
    }

    /// <summary>A run of one or two <c>d</c>s; three or more name the day of the week.</summary>
    private static bool HasDayOfMonth(List<(char Letter, int Index)> specifiers) {
        int start = 0;
        while (start < specifiers.Count) {
            int end = start + 1;
            while (end < specifiers.Count && specifiers[end].Letter == specifiers[start].Letter
                    && specifiers[end].Index == specifiers[end - 1].Index + 1) {
                end++;
            }
            if (specifiers[start].Letter == 'd' && end - start <= 2) {
                return true;
            }
            start = end;
        }
        return false;
    }

    private static string WithoutK(string pattern) {
        HashSet<int> drop = [.. Specifiers(pattern).Where(s => s.Letter == 'K').Select(s => s.Index)];
        return string.Concat(pattern.Where((_, index) => !drop.Contains(index)));
    }

    /// <summary>
    /// Each letter of <paramref name="pattern"/> that is a format specifier: outside a quoted
    /// literal and not escaped by a backslash.
    /// </summary>
    private static List<(char Letter, int Index)> Specifiers(string pattern) {
        var found = new List<(char Letter, int Index)>();
        char quote = '\0';
        int i = 0;
        while (i < pattern.Length) {
            char c = pattern[i];
            if (quote != '\0') {
                quote = c == quote ? '\0' : quote;
            } else if (c is '\'' or '"') {
                quote = c;
            } else if (c == '\\') {
                i++;
            } else if (char.IsAsciiLetter(c)) {
                found.Add((c, i));
            }
            i++;
        }
        return found;
    }

    /// <summary>
    /// A trailing <c>Z</c> or <c>±hh[:]mm</c> directly after a clock time, so a date's own
    /// hyphens (<c>2026-07-01</c>) never read as an offset.
    /// </summary>
    [GeneratedRegex(@"^(?<body>.*\d{1,2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?)\s*(?:Z|[+-]\d{2}(?::?\d{2})?)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingIsoOffset();
}
