using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// A <c>??</c> expression used as an operand runs by the type the checker gave it (R-78,
/// D-456): an <c>int</c> result beside a <c>float</c> is widened, and a <c>float</c> result
/// chooses the <c>float</c> opcode. Each case is one of R-78's reproducers or a sibling shape,
/// run through the CLI.
/// </summary>
public sealed class NilCoalesceOperandTests {
    private static (string Stdout, string Stderr, int ExitCode) RunSource(string source) {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.grob");
        File.WriteAllText(path, source);
        try {
            var stdout = new StringWriter(new StringBuilder());
            var stderr = new StringWriter(new StringBuilder());
            int exitCode = new RunCommand(stdout, stderr, new StringReader(string.Empty), verbose: false).Run(path);
            return (stdout.ToString(), stderr.ToString(), exitCode);
        } finally {
            File.Delete(path);
        }
    }

    private static string[] Lines(string stdout) =>
        stdout.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');

    private static void AssertRuns(string source, params string[] expected) {
        (string stdout, string stderr, int exitCode) = RunSource(source);

        Assert.Equal(string.Empty, stderr);
        Assert.Equal(0, exitCode);
        Assert.Equal(expected, Lines(stdout));
    }

    [Fact]
    public void IntNilCoalesce_SubtractedFromAFloat_IsWidened() =>
        AssertRuns("""
            n: int? := 7
            x := 2.25 - (n ?? 3)
            print(x)
            """, "-4.75");

    [Fact]
    public void NilWithAFloatFallback_DividedByAnInt_DividesAsFloat() =>
        AssertRuns("""
            x := (nil ?? 10.0) / 4
            print(x)
            """, "2.5");

    [Fact]
    public void IntNilCoalesce_ComparedWithAFloat_ComparesAsFloat() =>
        AssertRuns("""
            n: int? := nil
            print((n ?? 3) < 2.5)
            print((n ?? 2) < 2.5)
            """, "false", "true");

    [Fact]
    public void FloatNilCoalesce_AddedToAnInt_AddsAsFloat() =>
        AssertRuns("""
            m: float? := nil
            print(1 + (m ?? 0.5))
            """, "1.5");

    [Fact]
    public void IntNilCoalesce_AddedIntoAFloatBinding_IsWidened() =>
        AssertRuns("""
            x := 1.5
            n: int? := 2
            x += n ?? 1
            print(x)
            """, "3.5");

    [Fact]
    public void IntNilCoalesce_AsATernaryArmBesideAFloatArm_IsWidened() =>
        AssertRuns("""
            n: int? := nil
            c := true
            y := c ? (n ?? 1) : 2.5
            print(y + 0.5)
            """, "1.5");

    [Fact]
    public void StringNilCoalesce_ConcatenatedEitherSide_Concatenates() =>
        AssertRuns("""
            s: string? := nil
            print((s ?? "") + ":")
            print("a" + (s ?? "b"))
            """, ":", "ab");
}
