using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment A1b (D-370) integration tests — the <c>int</c>/<c>float</c>
/// type-static functions (<c>min</c>/<c>max</c>/<c>clamp</c>) end to end through the
/// full pipeline (lex -> parse -> type-check -> compile -> VM, stdlib plugins
/// auto-registered at startup), mirroring <c>Sprint9IncrementA1aTests</c>'s shape.
/// Completes the numeric surface Increment A1a began: these six are namespace-receiver
/// calls (<c>int.min(a, b)</c>), not instance methods.
/// </summary>
public sealed class Sprint9IncrementA1bTests {
    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // In-process pipeline runner — writes source to a real .grob temp file so
    // RunCommand exercises the exact CLI path (plugin auto-registration included).
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
    // int.min / int.max — happy paths.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(10, 20, "10")]
    [InlineData(20, 10, "10")]
    [InlineData(-5, 5, "-5")]
    public void IntMin_ReturnsSmaller(int a, int b, string expected) {
        string stdout = RunAndAssertSuccess($"print(int.min({a}, {b}))\n");
        Assert.Equal(expected + NL, stdout);
    }

    [Theory]
    [InlineData(10, 20, "20")]
    [InlineData(20, 10, "20")]
    [InlineData(-5, 5, "5")]
    public void IntMax_ReturnsLarger(int a, int b, string expected) {
        string stdout = RunAndAssertSuccess($"print(int.max({a}, {b}))\n");
        Assert.Equal(expected + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // int.clamp — at and outside both bounds.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(150, 0, 100, "100")]
    [InlineData(-10, 0, 100, "0")]
    [InlineData(50, 0, 100, "50")]
    [InlineData(0, 0, 100, "0")]
    [InlineData(100, 0, 100, "100")]
    public void IntClamp_ClampsToRange(int v, int lo, int hi, string expected) {
        string stdout = RunAndAssertSuccess($"print(int.clamp({v}, {lo}, {hi}))\n");
        Assert.Equal(expected + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // float.min / float.max — happy paths.
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatMin_ReturnsSmaller() {
        string stdout = RunAndAssertSuccess("print(float.min(1.5, 2.5))\n");
        Assert.Equal("1.5" + NL, stdout);
    }

    [Fact]
    public void FloatMax_ReturnsLarger() {
        string stdout = RunAndAssertSuccess("print(float.max(1.5, 2.5))\n");
        Assert.Equal("2.5" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // float.clamp — at and outside both bounds.
    // -----------------------------------------------------------------------

    [Fact]
    public void FloatClamp_AboveHi_ClampsToHi() {
        string stdout = RunAndAssertSuccess("print(float.clamp(1.5, 0.0, 1.0))\n");
        Assert.Equal("1.0" + NL, stdout);
    }

    [Fact]
    public void FloatClamp_BelowLo_ClampsToLo() {
        string stdout = RunAndAssertSuccess("print(float.clamp(-0.5, 0.0, 1.0))\n");
        Assert.Equal("0.0" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Operand typing (D-362) — a result used inline as an arithmetic operand,
    // proving the typed-opcode selection falls out of the existing mechanism.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntMaxResultPlusOne_ProducesCorrectIntValue() {
        string stdout = RunAndAssertSuccess("print(int.max(3, 5) + 1)\n");
        Assert.Equal("6" + NL, stdout);
    }

    [Fact]
    public void FloatMinResultTimesTwo_ProducesCorrectFloatValue() {
        string stdout = RunAndAssertSuccess("print(float.min(3.0, 5.0) * 2.0)\n");
        Assert.Equal("6.0" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Try/catch — clamp's inverted-range fault is catchable in Grob source, not
    // just at the CLI's top level (the fixture-driven tests below cover the
    // unhandled/CLI-level path).
    // -----------------------------------------------------------------------

    [Fact]
    public void IntClamp_LoGreaterThanHi_IsCatchableAsArithmeticError() {
        string stdout = RunAndAssertSuccess("""
            try {
                int.clamp(5, 10, 0)
            }
            catch (e: ArithmeticError) {
                print("caught: ${e.message}")
            }
            """);
        Assert.StartsWith("caught:", stdout);
    }

    [Fact]
    public void FloatClamp_LoGreaterThanHi_IsCatchableAsArithmeticError() {
        string stdout = RunAndAssertSuccess("""
            try {
                float.clamp(0.5, 1.0, 0.0)
            }
            catch (e: ArithmeticError) {
                print("caught: ${e.message}")
            }
            """);
        Assert.StartsWith("caught:", stdout);
    }

    // -----------------------------------------------------------------------
    // Fault paths, unhandled — each fixture's inverted-range clamp reaches the
    // CLI's top-level diagnostic formatting as E5001/ArithmeticError, exit 1.
    // -----------------------------------------------------------------------

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-9a1b", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void IntClamp_InvertedRange_Unhandled_ProducesE5001AndExitsOne() {
        (string stdout, string stderr, int exitCode) = RunFile("int-clamp-inverted-range.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5001]:", stderr);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void FloatClamp_InvertedRange_Unhandled_ProducesE5001AndExitsOne() {
        (string stdout, string stderr, int exitCode) = RunFile("float-clamp-inverted-range.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5001]:", stderr);
        Assert.Equal(1, exitCode);
    }

    // -----------------------------------------------------------------------
    // NaN / signed-zero — pinned .NET Math.Min/Max semantics, proven end to end.
    // -----------------------------------------------------------------------

    // Grob's number-literal grammar has no exponent notation and no direct
    // NaN/Infinity spelling (Lexer.ScanNumber: a float literal is digits, a
    // literal '.', then digits — no 'e'/'E' suffix), and float division/modulo
    // by exact zero always faults (D-273) rather than propagating NaN. NaN is
    // constructed the same way Sprint9IncrementA1aTests' huge-magnitude
    // overflow test constructs an out-of-range float: a plain decimal literal
    // with a 309-digit integer part exceeds double.MaxValue
    // (~1.7976931348623157e308) and parses straight to double.PositiveInfinity
    // (Parser.cs's double.TryParse, no overflow exception) — Infinity - Infinity
    // is NaN by IEEE 754, neither step touching the guarded division/modulo
    // opcodes.
    private static string HugeFloatLiteral => new string('9', 309) + ".0";

    [Fact]
    public void FloatMin_WithNaN_PrintsNaN() {
        string stdout = RunAndAssertSuccess(
            $"huge := {HugeFloatLiteral}\n" +
            "nanValue := huge - huge\n" +
            "print(float.min(nanValue, 1.0))\n");
        Assert.Equal("NaN" + NL, stdout);
    }

    [Fact]
    public void FloatMax_WithNaNAsSecondArgument_PrintsNaN() {
        string stdout = RunAndAssertSuccess(
            $"huge := {HugeFloatLiteral}\n" +
            "nanValue := huge - huge\n" +
            "print(float.max(1.0, nanValue))\n");
        Assert.Equal("NaN" + NL, stdout);
    }

    [Fact]
    public void FloatMin_PositiveThenNegativeZero_PrintsNegativeZero() {
        string stdout = RunAndAssertSuccess("print(float.min(0.0, -0.0))\n");
        Assert.Equal("-0.0" + NL, stdout);
    }

    [Fact]
    public void FloatMax_NegativeThenPositiveZero_PrintsPositiveZero() {
        string stdout = RunAndAssertSuccess("print(float.max(-0.0, 0.0))\n");
        Assert.Equal("0.0" + NL, stdout);
    }
}
