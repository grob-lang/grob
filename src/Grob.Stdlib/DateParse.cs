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
/// <see cref="DateTimeStyles.NoCurrentDateDefault"/> reports "no date" as 1 January of year
/// 1, so an input naming that exact date is read as time-only. No script dates anything to
/// year 1; the edge is stated rather than guarded.
/// </remarks>
internal static partial class DateParse {
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
            // DateTimeOffset parser keeps that designator's offset.
            return DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
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
        if (HasSpecifier(pattern, "zK")) {
            if (!DateTimeOffset.TryParseExact(input, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) {
                return false;
            }
            if (!HasSpecifier(pattern, "yMd")) {
                result = TodayInOwnOffset(result, utcNow);
            }
            return true;
        }
        if (!DateTime.TryParseExact(
                input, pattern, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out DateTime local)) {
            return Fail(out result);
        }
        if (local.Kind != DateTimeKind.Unspecified) {
            // A standard format whose designator the specifier scan cannot see ("o", "u"):
            // the DateTimeOffset parser keeps the input's offset.
            return DateTimeOffset.TryParseExact(input, pattern, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }
        result = InZone(local, utcNow, zone);
        return true;
    }

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

    /// <summary>
    /// True when <paramref name="pattern"/> uses one of <paramref name="specifiers"/> as a
    /// format specifier: outside a quoted literal and not escaped by a backslash.
    /// </summary>
    private static bool HasSpecifier(string pattern, string specifiers) {
        char quote = '\0';
        for (int i = 0; i < pattern.Length; i++) {
            char c = pattern[i];
            if (quote != '\0') {
                quote = c == quote ? '\0' : quote;
            } else if (c is '\'' or '"') {
                quote = c;
            } else if (c == '\\') {
                i++;
            } else if (specifiers.Contains(c)) {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// A trailing <c>Z</c> or <c>±hh[:]mm</c> directly after a clock time, so a date's own
    /// hyphens (<c>2026-07-01</c>) never read as an offset.
    /// </summary>
    [GeneratedRegex(@"^(?<body>.*\d{1,2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?)\s*(?:Z|[+-]\d{2}(?::?\d{2})?)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TrailingIsoOffset();
}
