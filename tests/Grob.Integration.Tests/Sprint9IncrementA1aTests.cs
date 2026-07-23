using System.Text;

using Grob.Cli;
using Grob.Compiler;
using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment A1a (D-369) integration tests — the <c>int</c>/<c>float</c>/<c>bool</c>
/// instance-member surfaces end to end through the full pipeline (lex -> parse ->
/// type-check -> compile -> VM, stdlib plugins auto-registered at startup, per
/// <c>Sprint8IncrementBTests</c>'s shape). Closes the release-gate blocker the
/// advertised-vs-built corpus audit found: the validation scripts in
/// <c>grob-sample-scripts.md</c> call <c>.roundTo(2)</c>/<c>.roundTo(1)</c>/<c>.toString()</c>
/// on <c>int</c>/<c>float</c> receivers, which had no dispatch anywhere before this
/// increment registered <see cref="Grob.Core.PrimitiveMembers.PrimitiveMemberRegistry.Int"/>/
/// <c>.Float</c>/<c>.Bool</c> and <see cref="Grob.Stdlib.NumericMethodsPlugin"/>.
/// </summary>
public sealed class Sprint9IncrementA1aTests {
    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // In-process pipeline runner — writes source to a real .grob temp file so
    // RunCommand exercises the exact CLI path (plugin auto-registration included),
    // mirroring Sprint8IncrementCTests.RunSource.
    // -----------------------------------------------------------------------

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

    private static string RunAndAssertSuccess(string source) {
        (string stdout, string stderr, int exitCode) = RunSource(source);
        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        return stdout;
    }

    // -----------------------------------------------------------------------
    // int — happy paths.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntToString_PrintsInvariantCultureDecimalForm() {
        string stdout = RunAndAssertSuccess("someInt := 42\nprint(someInt.toString())\n");
        Assert.Equal("42" + NL, stdout);
    }

    [Fact]
    public void IntToFloat_WidensToFloat() {
        string stdout = RunAndAssertSuccess("n := 5\nprint(n.toFloat())\n");
        Assert.Equal("5.0" + NL, stdout);
    }

    [Fact]
    public void IntAbs_ReturnsMagnitude() {
        string stdout = RunAndAssertSuccess("n := -5\nprint(n.abs())\n");
        Assert.Equal("5" + NL, stdout);
    }

    [Fact]
    public void IntFormat_RoutesThroughPattern() {
        string stdout = RunAndAssertSuccess("n := 255\nprint(n.format(\"X8\"))\n");
        Assert.Equal("000000FF" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // float — happy paths.
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatToString_PrintsRoundTrippableForm() {
        string stdout = RunAndAssertSuccess("f := 3.5\nprint(f.toString())\n");
        Assert.Equal("3.5" + NL, stdout);
    }

    [Fact]
    public void FloatToInt_Truncates() {
        string stdout = RunAndAssertSuccess("f := 3.9\nprint(f.toInt())\n");
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void FloatRound_ReturnsNearestInt() {
        string stdout = RunAndAssertSuccess("f := 2.6\nprint(f.round())\n");
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void FloatRoundTo_ReturnsDecimalPlaces() {
        string stdout = RunAndAssertSuccess("f := 3.14159\nprint(f.roundTo(2))\n");
        Assert.Equal("3.14" + NL, stdout);
    }

    [Fact]
    public void FloatFloor_RoundsDown() {
        string stdout = RunAndAssertSuccess("f := 2.9\nprint(f.floor())\n");
        Assert.Equal("2" + NL, stdout);
    }

    [Fact]
    public void FloatCeil_RoundsUp() {
        string stdout = RunAndAssertSuccess("f := 2.1\nprint(f.ceil())\n");
        Assert.Equal("3" + NL, stdout);
    }

    [Fact]
    public void FloatAbs_ReturnsMagnitude() {
        string stdout = RunAndAssertSuccess("f := -3.5\nprint(f.abs())\n");
        Assert.Equal("3.5" + NL, stdout);
    }

    [Fact]
    public void FloatFormat_RoutesThroughPattern() {
        string stdout = RunAndAssertSuccess("f := 1234.5\nprint(f.format(\"N2\"))\n");
        Assert.Equal("1,234.50" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // bool — happy path.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("true", "true")]
    [InlineData("false", "false")]
    public void BoolToString_ReturnsLowerCaseSpelling(string literal, string expected) {
        string stdout = RunAndAssertSuccess($"b := {literal}\nprint(b.toString())\n");
        Assert.Equal(expected + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // The release-gate unblock — the exact documented script shapes
    // (grob-sample-scripts.md, .round(n) renamed to .roundTo(n) per D-368).
    // -----------------------------------------------------------------------

    [Fact]
    public void ReleaseGateShape_SizeMbRoundTo2_CompilesAndProducesCorrectValue() {
        string stdout = RunAndAssertSuccess(
            "f := 5242880\n" +
            "size_mb := (f.toFloat() / 1024.0 / 1024.0).roundTo(2)\n" +
            "print(size_mb)\n");
        Assert.Equal("5.0" + NL, stdout);
    }

    [Fact]
    public void ReleaseGateShape_UsedPctRoundTo1_CompilesAndProducesCorrectValue() {
        string stdout = RunAndAssertSuccess(
            "used := 30\n" +
            "total := 40\n" +
            "used_pct := ((used.toFloat() / total.toFloat()) * 100.0).roundTo(1)\n" +
            "print(used_pct)\n");
        Assert.Equal("75.0" + NL, stdout);
    }

    [Fact]
    public void ReleaseGateShape_SomeIntToString_CompilesAndProducesCorrectValue() {
        string stdout = RunAndAssertSuccess("someInt := 2026\nprint(someInt.toString())\n");
        Assert.Equal("2026" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Fault paths — each raises a catchable ArithmeticError (E5001) from Grob source
    // via try/catch, not a host exception (D-353's "fails well" contract).
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatToInt_HugeMagnitude_IsCatchableAsArithmeticError() {
        // Grob's number-literal grammar has no exponent notation (Lexer.ScanNumber:
        // a float requires a literal '.' followed by a digit, no 'e'/'E' suffix), so
        // "1e300" is not expressible directly — a plain decimal literal well beyond
        // long's ~9.2e18 range (25 zeros, ~1e25) exercises the same out-of-range fault.
        string stdout = RunAndAssertSuccess("""
            try {
                f := 10000000000000000000000000.0
                f.toInt()
            }
            catch (e: ArithmeticError) {
                print("caught: ${e.message}")
            }
            """);
        Assert.StartsWith("caught:", stdout);
    }

    [Fact]
    public void FloatToInt_NaN_IsCatchableAsArithmeticError() {
        string stdout = RunAndAssertSuccess("""
            try {
                n := 0.0
                z := 0.0
                f := n / z
                f.toInt()
            }
            catch (e: ArithmeticError) {
                print("caught: ${e.message}")
            }
            """);
        Assert.StartsWith("caught:", stdout);
    }

    [Fact]
    public void FloatToInt_Infinity_IsCatchableAsArithmeticError() {
        string stdout = RunAndAssertSuccess("""
            try {
                n := 1.0
                z := 0.0
                f := n / z
                f.toInt()
            }
            catch (e: ArithmeticError) {
                print("caught: ${e.message}")
            }
            """);
        Assert.StartsWith("caught:", stdout);
    }

    [Fact]
    public void IntAbs_OnLongMinValueLiteral_IsCatchableAsArithmeticError() {
        // Grob has no int.MinValue literal, and the bare digit string
        // "9223372036854775808" (long.MinValue's magnitude) overflows long.Parse before
        // the unary minus is even applied (confirmed empirically — E2001 at parse time),
        // so long.MinValue is constructed at runtime instead: -long.MaxValue - 1, neither
        // step of which itself overflows.
        string stdout = RunAndAssertSuccess("""
            try {
                n := -9223372036854775807 - 1
                n.abs()
            }
            catch (e: ArithmeticError) {
                print("caught: ${e.message}")
            }
            """);
        Assert.StartsWith("caught:", stdout);
    }

    // -----------------------------------------------------------------------
    // Numeric-return-as-operand — D-362's ResolvedReturnType wiring, confirmed to
    // need zero changes for the new receivers.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntAbsResultPlusOne_ProducesCorrectIntValue() {
        string stdout = RunAndAssertSuccess("x := -5\nprint(x.abs() + 1)\n");
        Assert.Equal("6" + NL, stdout);
    }

    [Fact]
    public void FloatRoundResultTimesTwo_ProducesCorrectIntValue() {
        string stdout = RunAndAssertSuccess("f := 3.5\nprint(f.round() * 2)\n");
        Assert.Equal("8" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // float.toString() / print() parity — the ValueDisplay consistency check.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("3.5")]
    [InlineData("3.0")]
    [InlineData("-2.25")]
    [InlineData("0.0")]
    public void FloatToStringAndPrint_AgreeOnTheSameValue(string literal) {
        string stdout = RunAndAssertSuccess($"f := {literal}\nprint(f)\nprint(f.toString())\n");
        string[] lines = stdout.Split(NL, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.Equal(lines[0], lines[1]);
    }

    // -----------------------------------------------------------------------
    // Rounding-boundary tests — .5 midpoints, negative values, roundTo at 0 and a
    // high (but Math.Round-supported) decimal count. AwayFromZero pinned by D-369.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("2.5", "3")]
    [InlineData("-2.5", "-3")]
    [InlineData("0.5", "1")]
    [InlineData("-0.5", "-1")]
    public void FloatRound_MidpointsRoundAwayFromZero(string literal, string expected) {
        string stdout = RunAndAssertSuccess($"f := {literal}\nprint(f.round())\n");
        Assert.Equal(expected + NL, stdout);
    }

    [Fact]
    public void FloatRoundTo_ZeroDecimals_RoundsToWholeNumber() {
        string stdout = RunAndAssertSuccess("f := 2.5\nprint(f.roundTo(0))\n");
        Assert.Equal("3.0" + NL, stdout);
    }

    [Fact]
    public void FloatRoundTo_HighDecimalCount_RoundsCorrectly() {
        string stdout = RunAndAssertSuccess("f := 1.0\nprint(f.roundTo(15))\n");
        Assert.Equal("1.0" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // roundTo() with its required argument omitted — E0003, not an accidental
    // default (roundTo has no ParameterDefaults, unlike padLeft/padRight/truncate).
    // -----------------------------------------------------------------------

    [Fact]
    public void RoundTo_OmittedRequiredArgument_ReportsE0003() {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan("f := 3.5\nreadonly v := f.roundTo()\n", bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);

        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == ErrorCatalog.E0003.Code);
    }
}
