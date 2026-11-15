using Grob.Core;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.TestCatalogue;

namespace Grob.Stdlib.Tests;

/// <summary>
/// R-32: every <c>date</c> native that needs "here" reads <see cref="Grob.Runtime.IClock.LocalZone"/>,
/// never the host zone or the host clock. The injected zone is <see cref="TestZones.PlusThree"/>,
/// whose offsets (+03:00, +04:00 in summer) no CI host runs in, so a native that reads the host
/// or computes in UTC fails. Each case renders its result through <c>toIsoDateTime()</c>, so the
/// assertion pins the wall time and the offset together.
/// </summary>
public sealed class DatePluginZoneTests {
    // 21:30Z on 10 January: 00:30 on 11 January in +03:00, but 23:30 on 10 January in +02:00.
    // The two dates differ, so a time-only parse shows which zone supplied its date.
    private static readonly DateTime Winter = new(2026, 1, 10, 21, 30, 0, DateTimeKind.Utc);

    // Noon UTC on the test zone's spring-forward day: 16:00 at +04:00, while that day's
    // midnight was still +03:00.
    private static readonly DateTime TransitionDay = new(2026, 3, 29, 12, 0, 0, DateTimeKind.Utc);

    // 21:30Z on 10 January 2030: a year no host clock reads while these tests run, so a date
    // or year the parser took from the host shows.
    private static readonly DateTime FarFuture = new(2030, 1, 10, 21, 30, 0, DateTimeKind.Utc);

    private static string Run(DateTime utcNow, string callee, params GrobValue[] args) =>
        RunThen(utcNow, callee, null, args);

    private static string RunThen(DateTime utcNow, string callee, string? method, params GrobValue[] args) {
        var vm = new VirtualMachine(
            new StringWriter(), Catalogue(new DatePlugin(new TestClock(utcNow, TestZones.PlusThree))));
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)chunk.AddConstant(GrobValue.FromString(callee)), 1);
        foreach (GrobValue arg in args) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)chunk.AddConstant(arg), 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)args.Length, 1);
        foreach (string name in method is null ? ["toIsoDateTime"] : new[] { method, "toIsoDateTime" }) {
            chunk.WriteOpCode(OpCode.GetProperty, 1);
            chunk.WriteByte((byte)chunk.AddConstant(GrobValue.FromString(name)), 1);
            chunk.WriteOpCode(OpCode.Call, 1);
            chunk.WriteByte(0, 1);
        }
        chunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(chunk);
        return vm.Stack.Peek().AsString();
    }

    private static GrobValue Str(string s) => GrobValue.FromString(s);

    private static GrobValue Int(long i) => GrobValue.FromInt(i);

    [Fact]
    public void Now_InjectedZone_ConvertsTheClockIntoTheZone() {
        Assert.Equal("2026-01-11T00:30:00+03:00", Run(Winter, "date.now"));
    }

    [Fact]
    public void Now_InjectedZoneInSummerTime_TakesTheDaylightOffset() {
        Assert.Equal("2026-03-29T16:00:00+04:00", Run(TransitionDay, "date.now"));
    }

    [Fact]
    public void Today_InjectedZone_TakesTheZonesDate() {
        Assert.Equal("2026-01-11T00:00:00+03:00", Run(Winter, "date.today"));
    }

    [Fact]
    public void Today_InjectedZoneTransitionDay_UsesMidnightsOwnOffset() {
        // Midnight on the spring-forward day is still standard time, though now is not.
        Assert.Equal("2026-03-29T00:00:00+03:00", Run(TransitionDay, "date.today"));
    }

    [Theory]
    [InlineData(2026, 1, 1, "2026-01-01T00:00:00+03:00")]
    [InlineData(2026, 7, 1, "2026-07-01T00:00:00+04:00")]
    public void Of_InjectedZone_TakesTheZonesOffsetForThatDate(long year, long month, long day, string expected) {
        Assert.Equal(expected, Run(Winter, "date.of", Int(year), Int(month), Int(day)));
    }

    [Theory]
    [InlineData(2026, 7, 1, 12, 0, 0, "2026-07-01T12:00:00+04:00")]
    [InlineData(2026, 1, 1, 12, 0, 0, "2026-01-01T12:00:00+03:00")]
    // 02:30 on the spring-forward day does not exist locally; GetUtcOffset resolves an invalid
    // time to the standard offset, as TimeZoneInfo.Local did before R-32.
    [InlineData(2026, 3, 29, 2, 30, 0, "2026-03-29T02:30:00+03:00")]
    public void OfTime_InjectedZone_TakesTheZonesOffsetForThatLocalTime(
            long year, long month, long day, long hour, long minute, long second, string expected) {
        Assert.Equal(expected, Run(Winter, "date.ofTime",
            Int(year), Int(month), Int(day), Int(hour), Int(minute), Int(second)));
    }

    [Fact]
    public void ToLocal_InjectedZone_ConvertsIntoTheZone() {
        Assert.Equal("2026-01-11T00:30:00+03:00",
            RunThen(Winter, "date.parse", "toLocal", Str("2026-01-10T21:30:00Z"), Str("")));
    }

    [Theory]
    // Offset-free: the zone's offset for that local time, whatever the host's zone.
    [InlineData("2026-07-01T12:00:00", "2026-07-01T12:00:00+04:00")]
    [InlineData("2026-01-01T12:00:00", "2026-01-01T12:00:00+03:00")]
    [InlineData("2026-07-01", "2026-07-01T00:00:00+04:00")]
    // An explicit offset is kept.
    [InlineData("2026-07-01T12:00:00+02:00", "2026-07-01T12:00:00+02:00")]
    [InlineData("2026-07-01T12:00:00-0530", "2026-07-01T12:00:00-05:30")]
    [InlineData("2026-07-01T12:00:00Z", "2026-07-01T12:00:00Z")]
    // Time only, no offset: today in the injected zone, from the injected clock.
    [InlineData("14:30", "2026-01-11T14:30:00+03:00")]
    // Time only, explicit offset: today in the input's own offset (ruling 3, .NET's convention).
    [InlineData("14:30+02:00", "2026-01-10T14:30:00+02:00")]
    [InlineData("14:30Z", "2026-01-10T14:30:00Z")]
    public void Parse_IsoForm_InjectedZone_ResolvesAgainstTheClockAndZone(string input, string expected) {
        Assert.Equal(expected, Run(Winter, "date.parse", Str(input), Str("")));
    }

    [Theory]
    [InlineData("01/07/2026 12:00", "dd/MM/yyyy HH:mm", "2026-07-01T12:00:00+04:00")]
    [InlineData("01/07/2026 12:00 +02:00", "dd/MM/yyyy HH:mm zzz", "2026-07-01T12:00:00+02:00")]
    [InlineData("2026-07-01T12:00:00Z", "yyyy-MM-ddTHH:mm:ssK", "2026-07-01T12:00:00Z")]
    [InlineData("14:30", "HH:mm", "2026-01-11T14:30:00+03:00")]
    [InlineData("14:30 +02:00", "HH:mm zzz", "2026-01-10T14:30:00+02:00")]
    // A quoted or escaped z/K is a literal, not an offset specifier.
    [InlineData("2026-07-01 z", "yyyy-MM-dd 'z'", "2026-07-01T00:00:00+04:00")]
    [InlineData("2026-07-01 K", "yyyy-MM-dd \"K\"", "2026-07-01T00:00:00+04:00")]
    [InlineData("2026-07-01 z", "yyyy-MM-dd \\z", "2026-07-01T00:00:00+04:00")]
    public void Parse_PatternForm_InjectedZone_ResolvesAgainstTheClockAndZone(
            string input, string pattern, string expected) {
        Assert.Equal(expected, Run(Winter, "date.parse", Str(input), Str(pattern)));
    }

    [Theory]
    // A zone designator the lexical split does not recognise keeps that designator's offset.
    [InlineData("Wed, 01 Jul 2026 12:00:00 GMT", "", "2026-07-01T12:00:00Z")]
    // So does a standard format whose offset specifier the pattern scan cannot see.
    [InlineData("2026-07-01T12:00:00.0000000+02:00", "o", "2026-07-01T12:00:00+02:00")]
    public void Parse_UnrecognisedZoneDesignator_KeepsTheDesignatorsOffset(
            string input, string pattern, string expected) {
        Assert.Equal(expected, Run(Winter, "date.parse", Str(input), Str(pattern)));
    }

    [Theory]
    // K accepts an absent offset; the input has none, so the injected zone supplies it.
    [InlineData("2026-07-01T12:00:00", "yyyy-MM-ddTHH:mm:ssK", "2026-07-01T12:00:00+04:00")]
    [InlineData("2026-07-01T12:00:00+02:00", "yyyy-MM-ddTHH:mm:ssK", "2026-07-01T12:00:00+02:00")]
    // %K is the same specifier written as a token; removing it must leave a valid pattern.
    [InlineData("2026-07-01T12:00:00", "yyyy-MM-ddTHH:mm:ss%K", "2026-07-01T12:00:00+04:00")]
    // The standard formats u, r and R mean UTC, though their Z or GMT is a literal to DateTime.
    [InlineData("2026-01-01 14:30:00Z", "u", "2026-01-01T14:30:00Z")]
    [InlineData("Thu, 01 Jan 2026 14:30:00 GMT", "r", "2026-01-01T14:30:00Z")]
    [InlineData("Thu, 01 Jan 2026 14:30:00 GMT", "R", "2026-01-01T14:30:00Z")]
    public void Parse_OffsetDecidedByTheInput_NotByThePattern(string input, string pattern, string expected) {
        Assert.Equal(expected, Run(Winter, "date.parse", Str(input), Str(pattern)));
    }

    [Theory]
    // A missing date or year comes from the injected clock, never the host's.
    [InlineData("14:30 GMT", "", "2030-01-10T14:30:00Z")]
    [InlineData("07-01", "MM-dd", "2030-07-01T00:00:00+04:00")]
    [InlineData("01 Jul 14:30", "dd MMM HH:mm", "2030-07-01T14:30:00+04:00")]
    [InlineData("07-01 +02:00", "MM-dd zzz", "2030-07-01T00:00:00+02:00")]
    // ddd names the day of the week, dd the day of the month: 1 July 2030 is a Monday.
    [InlineData("Mon, 01 Jul 14:30", "ddd, dd MMM HH:mm", "2030-07-01T14:30:00+04:00")]
    // The standard month-day format has no year either.
    [InlineData("July 01", "M", "2030-07-01T00:00:00+04:00")]
    public void Parse_MissingDateOrYear_TakesItFromTheInjectedClock(string input, string pattern, string expected) {
        Assert.Equal(expected, Run(FarFuture, "date.parse", Str(input), Str(pattern)));
    }

    [Fact]
    public void Parse_MissingYearWithOffset_TakesTheYearInThatOffset() {
        // 23:30Z on 31 December 2029 is already 2030 at +02:00.
        var newYearsEve = new DateTime(2029, 12, 31, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal("2030-07-01T00:00:00+02:00", Run(newYearsEve, "date.parse", Str("07-01 +02:00"), Str("MM-dd zzz")));
    }

    [Fact]
    public void Parse_MissingYearWithOffsetAndWeekday_ValidatesTheWeekdayInThatYear() {
        // 1 July 2030 is a Monday; 1 July 2029 was a Sunday. The UTC year, 2029, must not reject
        // a weekday that the input's own offset puts in 2030.
        var newYearsEve = new DateTime(2029, 12, 31, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal("2030-07-01T00:00:00+02:00",
            Run(newYearsEve, "date.parse", Str("Mon, 01 Jul +02:00"), Str("ddd, dd MMM zzz")));
    }

    [Theory]
    [InlineData("99:99+02:00", "")]
    [InlineData("2026-07-01T12:00:00+99:00", "")]
    [InlineData("14:30", "HH:mm zzz")]
    public void Parse_MalformedExplicitOffsetInput_ThrowsCatchableParseError(string input, string pattern) {
        var vm = new VirtualMachine(
            new StringWriter(), Catalogue(new DatePlugin(new TestClock(Winter, TestZones.PlusThree))));

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(ChunkBuilders.BuildCallChunk("date.parse", Str(input), Str(pattern))));

        Assert.Equal(ErrorCatalog.E5702.Code, ex.Code);
    }
}
