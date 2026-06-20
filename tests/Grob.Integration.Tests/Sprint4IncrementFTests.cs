using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 4 Increment F integration tests — the calculator smoke script close-gate.
/// Drives the full pipeline via <see cref="RunCommand"/> to prove the end-to-end
/// Sprint 1–4 language surface works together.
/// </summary>
public sealed class Sprint4IncrementFTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string NL => Environment.NewLine;

    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-4f", name);
    }

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    // -----------------------------------------------------------------------
    // Sprint 4 close-gate — grob run calculator.grob
    // -----------------------------------------------------------------------

    /// <summary>
    /// Close-gate for Sprint 4: <c>grob run calculator.grob</c> must produce the
    /// exact gold-master output that exercises the full Sprint 1–4 language surface —
    /// <c>const</c>/<c>readonly</c> declarations, <c>while</c> with <c>break</c> and
    /// <c>continue</c>, <c>for...in</c> (array single-ident, two-ident, numeric range
    /// with <c>step</c>), <c>select</c> with <c>continue</c> in a case, switch
    /// expressions, <c>if</c>/<c>else if</c>/<c>else</c>, <c>&amp;&amp;</c>/<c>||</c>,
    /// ternary and string interpolation.
    /// </summary>
    [Fact]
    public void CalculatorGrob_RunFile_ProducesGoldMasterOutput() {
        (string stdout, string stderr, int exitCode) = RunFile("calculator.grob");

        string expected =
            $"Calculator: 6 steps{NL}" +
            $"  + 10 -> 10{NL}" +
            $"  - 3 -> 7{NL}" +
            $"  * 5 -> 35{NL}" +
            $"  + 8 -> 43{NL}" +
            $"  - 1 -> 42{NL}" +
            $"range: high{NL}" +
            $"Pos check: yes{NL}" +
            $"Operands:{NL}" +
            $"  10{NL}" +
            $"  3{NL}" +
            $"  5{NL}" +
            $"  8{NL}" +
            $"  1{NL}" +
            $"Even 0..8:{NL}" +
            $"  0{NL}" +
            $"  2{NL}" +
            $"  4{NL}" +
            $"  6{NL}" +
            $"  8{NL}" +
            $"Skip 3:{NL}" +
            $"  1{NL}" +
            $"  2{NL}" +
            $"  4{NL}" +
            $"Operations:{NL}" +
            $"  [0] +{NL}" +
            $"  [1] -{NL}" +
            $"  [2] *{NL}" +
            $"  [3] +{NL}" +
            $"  [4] -{NL}" +
            $"Result: +42 grade=A{NL}";

        Assert.Equal(0, exitCode);
        Assert.Equal(expected, stdout);
        Assert.Equal(string.Empty, stderr);
    }
}
