using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment B integration tests: the <c>date</c> primitive type and module end
/// to end through the full pipeline (lex -> parse -> type-check -> compile -> VM, stdlib
/// plugins auto-registered at startup) — construction, parsing, instance members,
/// arithmetic with negative arguments (D-354 — no <c>minusDays</c>), <c>&lt;</c>/<c>&gt;</c>
/// comparison (D-354's <c>LessDate</c>/<c>GreaterDate</c>), value equality,
/// <c>ValueDisplay</c> rendering, and the two exercised diagnostics (E0002 compile-time
/// type mismatch, E5702 runtime <c>ParseError</c>).
/// </summary>
public sealed class Sprint9IncrementBTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    // -----------------------------------------------------------------------
    // Construction and instance members.
    // -----------------------------------------------------------------------

    [Fact]
    public void Of_YearMonthDay_RoundTrip() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            d := date.of(2026, 4, 5)
            print(d.year)
            print(d.month)
            print(d.day)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"2026{Environment.NewLine}4{Environment.NewLine}5{Environment.NewLine}", stdout);
    }

    [Fact]
    public void ToIso_RendersDateOnlyForm() {
        (string stdout, string stderr, int exitCode) = RunSource(
            """print(date.of(2026, 4, 5).toIso())""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("2026-04-05" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Arithmetic — D-354: addDays/addMonths/addHours/addMinutes accept a negative n
    // to subtract, uniformly (no minusDays).
    // -----------------------------------------------------------------------

    [Fact]
    public void AddDays_NegativeArgument_SubtractsDays() {
        (string stdout, string stderr, int exitCode) = RunSource(
            """print(date.of(2026, 4, 5).addDays(-1).day)""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("4" + Environment.NewLine, stdout);
    }

    [Fact]
    public void AddMonths_PositiveArgument_AdvancesMonth() {
        (string stdout, string stderr, int exitCode) = RunSource(
            """print(date.of(2026, 4, 5).addMonths(2).month)""" + "\n");

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("6" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Comparison — D-354: LessDate/GreaterDate opcodes.
    // -----------------------------------------------------------------------

    [Fact]
    public void LessThan_EarlierDate_IsTrue() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.of(2026, 1, 1)
            b := date.of(2026, 1, 2)
            print(a < b)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("true" + Environment.NewLine, stdout);
    }

    [Fact]
    public void IsBefore_MatchesLessThanOperator() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.of(2026, 1, 1)
            b := date.of(2026, 1, 2)
            print(a.isBefore(b) == (a < b))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("true" + Environment.NewLine, stdout);
    }

    [Fact]
    public void DaysUntil_ComputesInterval() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.of(2026, 1, 1)
            b := date.of(2026, 1, 11)
            print(a.daysUntil(b))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("10" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Equality — D-357/D-367: instant-based, restoring trichotomy. This is the exact
    // shape the decision's own worked example uses (date.now() vs date.now().toUtc()).
    // -----------------------------------------------------------------------

    [Fact]
    public void EqualityOperator_SameInstant_DifferentOffset_ViaToUtc_RestoresTrichotomy() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.parse("2026-04-05T14:30:00+01:00")
            b := a.toUtc()
            print(a == b)
            print(a < b)
            print(a > b)
            print(a != b)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            $"true{Environment.NewLine}false{Environment.NewLine}false{Environment.NewLine}false{Environment.NewLine}",
            stdout);
    }

    [Fact]
    public void EqualityOperator_DifferentInstants_IsFalse() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            a := date.of(2026, 1, 1)
            b := date.of(2026, 1, 2)
            print(a == b)
            print(a != b)
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal($"false{Environment.NewLine}true{Environment.NewLine}", stdout);
    }

    // CodeRabbit review, PR #157: date? is an already-supported nullable type — its
    // equality must also be instant-based, and must not crash when an operand is
    // actually nil at runtime (the exact defect a naive fix would have reintroduced).
    [Fact]
    public void EqualityOperator_NullableDate_SameInstantAndNilCases_AllCorrect() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            fn eq(a: date?, b: date?): bool {
                return a == b
            }
            x := date.parse("2026-04-05T14:30:00+01:00")
            y := x.toUtc()
            print(eq(x, y))
            print(eq(nil, nil))
            print(eq(x, nil))
            print(eq(nil, y))
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(
            $"true{Environment.NewLine}true{Environment.NewLine}false{Environment.NewLine}false{Environment.NewLine}",
            stdout);
    }

    // -----------------------------------------------------------------------
    // Display — print() and string interpolation render the canonical string.
    // -----------------------------------------------------------------------

    [Fact]
    public void StringInterpolation_Date_CallsToStringImplicitly() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            d := date.parse("2026-04-05T14:30:00Z")
            print("Occurred at: ${d}")
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal("Occurred at: 2026-04-05T14:30:00Z" + Environment.NewLine, stdout);
    }

    // -----------------------------------------------------------------------
    // Compile-time distinctness — date == string is a type mismatch, not a runtime fault.
    // -----------------------------------------------------------------------

    [Fact]
    public void DateComparedToString_IsCompileErrorE0002() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            d := date.now()
            print(d == "not-a-date")
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0002", stderr);
    }

    [Fact]
    public void DateLessThanInt_IsCompileErrorE0002() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            d := date.now()
            print(d < 5)
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E0002", stderr);
    }

    // -----------------------------------------------------------------------
    // Runtime parse failure — E5702 (ParseError), catchable.
    // -----------------------------------------------------------------------

    [Fact]
    public void ParseMalformedVariable_Unhandled_ExitsOneWithTopLevelDiagnostic() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            s := "not-a-date"
            date.parse(s)
            """);

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("E5702", stderr);
    }

    [Fact]
    public void ParseMalformedVariable_CaughtAsParseError() {
        (string stdout, string stderr, int exitCode) = RunSource("""
            s := "not-a-date"
            try {
                date.parse(s)
            } catch (e: ParseError) {
                print("caught: ${e.message}")
            }
            """);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr);
        Assert.StartsWith("caught:", stdout);
    }
}
