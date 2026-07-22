using Grob.Core;
using Grob.Runtime;
using Grob.Vm;
using Xunit;

using static Grob.Stdlib.Tests.ChunkBuilders;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Sprint 9 Increment B: <see cref="DatePlugin"/> registers the seven static
/// constructors (<c>now</c>/<c>today</c>/<c>of</c>/<c>ofTime</c>/<c>parse</c>/
/// <c>fromUnixSeconds</c>/<c>fromUnixMillis</c>) and a registered <c>toString()</c>, end to
/// end through a real <see cref="VirtualMachine"/> against a fake <see cref="IClock"/>. The
/// instance property/method surface (<c>year</c>, <c>addDays</c>, ...) is dispatched by
/// <c>Grob.Vm</c>'s <c>OpCode.GetProperty</c> handler directly (exercised here through
/// real bytecode, not a second implementation). Chunks are hand-constructed — this project
/// has no dependency on <c>Grob.Compiler</c>.
/// </summary>
public sealed class DatePluginTests {
    private static readonly DateTime PinnedUtc = new(2026, 4, 5, 13, 30, 0, DateTimeKind.Utc);

    private static VirtualMachine NewRegisteredVm(IClock? clock = null) {
        var vm = new VirtualMachine(new StringWriter());
        new DatePlugin(clock ?? new TestClock(PinnedUtc)).Register(vm);
        return vm;
    }

    private static Chunk BuildGetPropertyOnCallChunk(string calleeName, string propertyName, params GrobValue[] args) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);

        int[] argIndexes = [.. args.Select(chunk.AddConstant)];
        foreach (int argIdx in argIndexes) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)args.Length, 1);

        int propIdx = chunk.AddConstant(GrobValue.FromString(propertyName));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)propIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    private static Chunk BuildMethodCallOnCallChunk(
            string calleeName, string methodName, GrobValue[] calleeArgs, params GrobValue[] methodArgs) {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString(calleeName));
        chunk.WriteOpCode(OpCode.GetGlobal, 1);
        chunk.WriteByte((byte)calleeIdx, 1);

        foreach (int argIdx in calleeArgs.Select(chunk.AddConstant)) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)calleeArgs.Length, 1);

        int propIdx = chunk.AddConstant(GrobValue.FromString(methodName));
        chunk.WriteOpCode(OpCode.GetProperty, 1);
        chunk.WriteByte((byte)propIdx, 1);

        foreach (int argIdx in methodArgs.Select(chunk.AddConstant)) {
            chunk.WriteOpCode(OpCode.Constant, 1);
            chunk.WriteByte((byte)argIdx, 1);
        }
        chunk.WriteOpCode(OpCode.Call, 1);
        chunk.WriteByte((byte)methodArgs.Length, 1);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    [Fact]
    public void Name_IsDate() {
        Assert.Equal("date", new DatePlugin(new TestClock(PinnedUtc)).Name);
    }

    // -----------------------------------------------------------------------
    // now()/today() — read the injected IClock, converted to local time (D-176).
    // -----------------------------------------------------------------------

    [Fact]
    public void Now_ReadsFromPinnedClock() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.now", "toUnixSeconds", []));

        Assert.Equal(new DateTimeOffset(PinnedUtc).ToUnixTimeSeconds(), vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Today_ZerosTimeComponent() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk("date.today", "hour"));
        Assert.Equal(0, vm.Stack.Peek().AsInt());

        vm.Run(BuildGetPropertyOnCallChunk("date.today", "minute"));
        Assert.Equal(0, vm.Stack.Peek().AsInt());

        vm.Run(BuildGetPropertyOnCallChunk("date.today", "second"));
        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Today_KeepsDateComponents() {
        var vm = NewRegisteredVm();
        DateTimeOffset localNow = new DateTimeOffset(PinnedUtc).ToLocalTime();

        vm.Run(BuildGetPropertyOnCallChunk("date.today", "year"));
        Assert.Equal(localNow.Year, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Today_UsesMidnightsOwnUtcOffset_NotNows() {
        // CodeRabbit review, PR #143: date.today() previously reused `now`'s offset for
        // midnight, wrong on a day the local zone's offset changes (a DST transition).
        // TimeZoneInfo.Local is not mockable, so this cannot force an actual transition
        // day, but it does pin the implementation to computing midnight's own offset
        // rather than reusing `now`'s — the two happen to coincide outside a transition.
        var vm = NewRegisteredVm();
        DateTimeOffset localNow = new DateTimeOffset(PinnedUtc).ToLocalTime();
        var midnight = new DateTime(localNow.Year, localNow.Month, localNow.Day, 0, 0, 0, DateTimeKind.Unspecified);
        long expectedOffsetMinutes = (long)TimeZoneInfo.Local.GetUtcOffset(midnight).TotalMinutes;

        vm.Run(BuildGetPropertyOnCallChunk("date.today", "utcOffset"));

        Assert.Equal(expectedOffsetMinutes, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // of()/ofTime() — construction.
    // -----------------------------------------------------------------------

    [Fact]
    public void Of_ConstructsDateWithZeroedTime() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk(
            "date.of", "hour", GrobValue.FromInt(2026), GrobValue.FromInt(4), GrobValue.FromInt(5)));

        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Of_ConstructsExpectedDateComponents() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk(
            "date.of", "day", GrobValue.FromInt(2026), GrobValue.FromInt(4), GrobValue.FromInt(5)));

        Assert.Equal(5, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void OfTime_ConstructsExpectedTimeComponents() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk(
            "date.ofTime", "minute",
            GrobValue.FromInt(2026), GrobValue.FromInt(4), GrobValue.FromInt(5),
            GrobValue.FromInt(14), GrobValue.FromInt(30), GrobValue.FromInt(0)));

        Assert.Equal(30, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Of_MonthOutsideIntRange_ThrowsCatchableArithmeticErrorRatherThanWrapping() {
        // CodeRabbit review, PR #143: an unchecked long-to-int narrowing would have
        // silently wrapped this into some other, wrong but valid-looking month instead
        // of failing. checked() lets the VM's existing generic OverflowException ->
        // GrobArithmeticException(E5001) safety net (VirtualMachine.cs's RunDispatch
        // catch block) turn it into a real, catchable Grob diagnostic for free — not a
        // raw, unhandled CLR OverflowException.
        var vm = NewRegisteredVm();

        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(BuildCallChunk(
            "date.of", GrobValue.FromInt(2026), GrobValue.FromInt(long.MaxValue), GrobValue.FromInt(5))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }

    [Fact]
    public void AddMonths_ArgumentOutsideIntRange_ThrowsCatchableArithmeticErrorRatherThanWrapping() {
        var vm = NewRegisteredVm();

        GrobArithmeticException ex = Assert.Throws<GrobArithmeticException>(() => vm.Run(BuildMethodCallOnCallChunk(
            "date.now", "addMonths", [], GrobValue.FromInt(long.MaxValue))));
        Assert.Equal(ErrorCatalog.E5001.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // Parsing.
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_IsoStringWithOffset_PreservesOffset() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk("date.parse", "utcOffset",
            GrobValue.FromString("2026-04-05T14:30:00+02:00"), GrobValue.FromString("")));

        Assert.Equal(120, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Parse_IsoStringWithOffset_RoundTripsComponents() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk("date.parse", "hour",
            GrobValue.FromString("2026-04-05T14:30:00+02:00"), GrobValue.FromString("")));

        Assert.Equal(14, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Parse_InvalidString_ThrowsCatchableParseError() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() =>
            vm.Run(BuildCallChunk("date.parse", GrobValue.FromString("not-a-date"), GrobValue.FromString(""))));

        Assert.Equal(ErrorCatalog.E5702.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // parse()'s optional pattern argument (D-358) — arity 2, hand-built directly
    // (the compiler's default-fill for the 1-argument source form is exercised in
    // Grob.Compiler.Tests, not here).
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_TwoArgumentsEmptyPattern_StillParsesIso() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk("date.parse", "utcOffset",
            GrobValue.FromString("2026-04-05T14:30:00+02:00"), GrobValue.FromString("")));

        Assert.Equal(120, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Parse_ExplicitPattern_ParsesViaParseExact() {
        var vm = NewRegisteredVm();
        GrobValue[] args = [GrobValue.FromString("05/04/2026"), GrobValue.FromString("dd/MM/yyyy")];

        vm.Run(BuildGetPropertyOnCallChunk("date.parse", "day", args));
        Assert.Equal(5, vm.Stack.Peek().AsInt());

        vm.Run(BuildGetPropertyOnCallChunk("date.parse", "month", args));
        Assert.Equal(4, vm.Stack.Peek().AsInt());

        vm.Run(BuildGetPropertyOnCallChunk("date.parse", "year", args));
        Assert.Equal(2026, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Parse_StringMismatchedWithExplicitPattern_ThrowsCatchableParseError() {
        var vm = NewRegisteredVm();

        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(BuildCallChunk(
            "date.parse", GrobValue.FromString("not-a-date"), GrobValue.FromString("dd/MM/yyyy"))));

        Assert.Equal(ErrorCatalog.E5702.Code, ex.Code);
    }

    // -----------------------------------------------------------------------
    // Unix epoch round-trips.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToUnixSeconds_FromUnixSeconds_RoundTrips() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "date.fromUnixSeconds", "toUnixSeconds", [GrobValue.FromInt(1_700_000_000)]));

        Assert.Equal(1_700_000_000, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ToUnixMillis_FromUnixMillis_RoundTrips() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "date.fromUnixMillis", "toUnixMillis", [GrobValue.FromInt(1_700_000_000_000)]));

        Assert.Equal(1_700_000_000_000, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void FromUnixSeconds_ReturnsUtcValue() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk(
            "date.fromUnixSeconds", "utcOffset", GrobValue.FromInt(0)));

        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Remaining instance method surface — addHours/addMinutes/isAfter/
    // toIsoDateTime/format/daysSince/dayOfYear.
    // -----------------------------------------------------------------------

    [Fact]
    public void AddHours_AdvancesHour() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "addHours",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")], GrobValue.FromInt(2)));

        var propChunk = new Chunk();
        GrobValue advanced = vm.Stack.Peek();
        int constIdx = propChunk.AddConstant(advanced);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("hour"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(16, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void AddMinutes_NegativeArgument_MovesBackward() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "addMinutes",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")], GrobValue.FromInt(-15)));

        var propChunk = new Chunk();
        GrobValue moved = vm.Stack.Peek();
        int constIdx = propChunk.AddConstant(moved);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("minute"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(15, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void IsAfter_LaterDate_IsTrue() {
        var vm = NewRegisteredVm();
        var chunk = new Chunk();
        int laterIdx = chunk.AddConstant(GrobValue.FromString("2026-01-02T00:00:00+00:00"));
        int earlierIdx = chunk.AddConstant(GrobValue.FromString("2026-01-01T00:00:00+00:00"));
        int parseIdx = chunk.AddConstant(GrobValue.FromString("date.parse"));
        int emptyPatternIdx = chunk.AddConstant(GrobValue.FromString(""));

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)parseIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)laterIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)emptyPatternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);

        int isAfterIdx = chunk.AddConstant(GrobValue.FromString("isAfter"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)isAfterIdx, 1);

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)parseIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)earlierIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)emptyPatternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);

        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void DaysSince_EarlierDate_IsPositive() {
        var vm = NewRegisteredVm();
        var chunk = new Chunk();
        int lateIdx = chunk.AddConstant(GrobValue.FromString("2026-01-11T00:00:00+00:00"));
        int earlyIdx = chunk.AddConstant(GrobValue.FromString("2026-01-01T00:00:00+00:00"));
        int parseIdx = chunk.AddConstant(GrobValue.FromString("date.parse"));
        int emptyPatternIdx = chunk.AddConstant(GrobValue.FromString(""));

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)parseIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)lateIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)emptyPatternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);

        int daysSinceIdx = chunk.AddConstant(GrobValue.FromString("daysSince"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)daysSinceIdx, 1);

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)parseIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)earlyIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)emptyPatternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);

        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.Equal(10, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ToIsoDateTime_RendersFullForm() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "toIsoDateTime",
            [GrobValue.FromString("2026-04-05T14:30:00+01:00"), GrobValue.FromString("")]));

        Assert.Equal("2026-04-05T14:30:00+01:00", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void Format_WithCustomPattern_RendersExpectedForm() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "date.parse", "format",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")],
            GrobValue.FromString("dd MMM yyyy")));

        Assert.Equal("05 Apr 2026", vm.Stack.Peek().AsString());
    }

    [Fact]
    public void GetProperty_DayOfYear_ReadsFromStoredInstant() {
        var vm = NewRegisteredVm();
        vm.Run(BuildGetPropertyOnCallChunk(
            "date.of", "dayOfYear", GrobValue.FromInt(2026), GrobValue.FromInt(1), GrobValue.FromInt(15)));

        Assert.Equal(15, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // toUtc/toLocal/toZone.
    // -----------------------------------------------------------------------

    [Fact]
    public void ToLocal_RoundTripsThroughToUtc() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "toUtc",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")]));
        GrobValue utcDate = vm.Stack.Peek();

        var localChunk = new Chunk();
        int constIdx = localChunk.AddConstant(utcDate);
        localChunk.WriteOpCode(OpCode.Constant, 1); localChunk.WriteByte((byte)constIdx, 1);
        int toLocalIdx = localChunk.AddConstant(GrobValue.FromString("toLocal"));
        localChunk.WriteOpCode(OpCode.GetProperty, 1); localChunk.WriteByte((byte)toLocalIdx, 1);
        localChunk.WriteOpCode(OpCode.Call, 1); localChunk.WriteByte(0, 1);
        int hourIdx = localChunk.AddConstant(GrobValue.FromString("hour"));
        localChunk.WriteOpCode(OpCode.GetProperty, 1); localChunk.WriteByte((byte)hourIdx, 1);
        localChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(localChunk);

        Assert.Equal(new DateTimeOffset(2026, 4, 5, 14, 30, 0, TimeSpan.Zero).ToLocalTime().Hour, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ToUtc_ZeroesOffset() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "toUtc",
            [GrobValue.FromString("2026-04-05T14:30:00+02:00"), GrobValue.FromString("")]));
        GrobValue utcDate = vm.Stack.Peek();

        var propChunk = new Chunk();
        int constIdx = propChunk.AddConstant(utcDate);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("utcOffset"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ToZone_ConvertsToNamedZone() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk(
            "date.parse", "toZone",
            [GrobValue.FromString("2026-04-05T12:00:00Z"), GrobValue.FromString("")], GrobValue.FromString("UTC")));

        var propChunk = new Chunk();
        GrobValue zoned = vm.Stack.Peek();
        int constIdx = propChunk.AddConstant(zoned);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("hour"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(12, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // toDateOnly/toTimeOnly (D-354).
    // -----------------------------------------------------------------------

    [Fact]
    public void ToDateOnly_ZeroesTimeKeepsDate() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "toDateOnly",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")]));

        var propChunk = new Chunk();
        GrobValue dateOnly = vm.Stack.Peek();
        int constIdx = propChunk.AddConstant(dateOnly);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("hour"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(0, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ToTimeOnly_AnchorsDateToUnixEpoch() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "toTimeOnly",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")]));

        var propChunk = new Chunk();
        GrobValue timeOnly = vm.Stack.Peek();
        int constIdx = propChunk.AddConstant(timeOnly);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("year"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(1970, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void ToTimeOnly_KeepsTimeComponents() {
        var vm = NewRegisteredVm();
        vm.Run(BuildMethodCallOnCallChunk("date.parse", "toTimeOnly",
            [GrobValue.FromString("2026-04-05T14:30:00+00:00"), GrobValue.FromString("")]));

        var propChunk = new Chunk();
        GrobValue timeOnly = vm.Stack.Peek();
        int constIdx = propChunk.AddConstant(timeOnly);
        propChunk.WriteOpCode(OpCode.Constant, 1); propChunk.WriteByte((byte)constIdx, 1);
        int nameIdx = propChunk.AddConstant(GrobValue.FromString("hour"));
        propChunk.WriteOpCode(OpCode.GetProperty, 1); propChunk.WriteByte((byte)nameIdx, 1);
        propChunk.WriteOpCode(OpCode.Return, 1);
        vm.Run(propChunk);

        Assert.Equal(14, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // daysUntil/daysSince (D-117) — neither throws on direction reversal.
    // -----------------------------------------------------------------------

    [Fact]
    public void DaysUntil_LaterDate_IsPositive() {
        var vm = NewRegisteredVm();
        var chunk = new Chunk();
        int earlyIdx = chunk.AddConstant(GrobValue.FromString("2026-01-01T00:00:00+00:00"));
        int lateIdx = chunk.AddConstant(GrobValue.FromString("2026-01-11T00:00:00+00:00"));
        int parseIdx = chunk.AddConstant(GrobValue.FromString("date.parse"));
        int emptyPatternIdx = chunk.AddConstant(GrobValue.FromString(""));

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)parseIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)earlyIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)emptyPatternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);

        int daysUntilIdx = chunk.AddConstant(GrobValue.FromString("daysUntil"));
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte((byte)daysUntilIdx, 1);

        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)parseIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)lateIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)emptyPatternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);

        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(1, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        vm.Run(chunk);

        Assert.Equal(10, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Display — registered toString().
    // -----------------------------------------------------------------------

    [Fact]
    public void Print_RegisteredToString_RendersCanonicalIsoString() {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString("date.parse"));
        int argIdx = chunk.AddConstant(GrobValue.FromString("2026-04-05T14:30:00+01:00"));
        int patternIdx = chunk.AddConstant(GrobValue.FromString(""));
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)argIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)patternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var output = new StringWriter();
        var freshVm = new VirtualMachine(output);
        new DatePlugin(new TestClock(PinnedUtc)).Register(freshVm);
        freshVm.Run(chunk);

        Assert.Equal($"2026-04-05T14:30:00+01:00{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public void Print_RegisteredToString_UtcZeroOffset_RendersZ() {
        var chunk = new Chunk();
        int calleeIdx = chunk.AddConstant(GrobValue.FromString("date.parse"));
        int argIdx = chunk.AddConstant(GrobValue.FromString("2026-04-05T14:30:00Z"));
        int patternIdx = chunk.AddConstant(GrobValue.FromString(""));
        chunk.WriteOpCode(OpCode.GetGlobal, 1); chunk.WriteByte((byte)calleeIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)argIdx, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)patternIdx, 1);
        chunk.WriteOpCode(OpCode.Call, 1); chunk.WriteByte(2, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var output = new StringWriter();
        var freshVm = new VirtualMachine(output);
        new DatePlugin(new TestClock(PinnedUtc)).Register(freshVm);
        freshVm.Run(chunk);

        Assert.Equal($"2026-04-05T14:30:00Z{Environment.NewLine}", output.ToString());
    }

    // -----------------------------------------------------------------------
    // No direct DateTime.Now/DateTimeOffset.Now — the IClock seam is the only path.
    // -----------------------------------------------------------------------

    [Fact]
    public void Now_DoesNotReflectRealSystemClock_OnlyThePinnedOne() {
        // CodeRabbit review, PR #143: midnight UTC on 1 January 2099 converts to
        // 31 December 2098 in a negative-offset local zone, so the expected year is
        // computed from the pinned clock's own local conversion rather than hardcoded.
        var farFutureClock = new TestClock(new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var vm = NewRegisteredVm(farFutureClock);
        vm.Run(BuildGetPropertyOnCallChunk("date.now", "year"));

        long expectedYear = new DateTimeOffset(farFutureClock.UtcNow).ToLocalTime().Year;
        Assert.Equal(expectedYear, vm.Stack.Peek().AsInt());
    }
}
