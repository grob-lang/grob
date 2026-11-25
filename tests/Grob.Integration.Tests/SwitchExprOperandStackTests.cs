using System.Text;

using Grob.Cli;

using Xunit;

namespace Grob.Integration.Tests;

/// <summary>
/// A switch expression gives its value where operands are already on the stack (R-77,
/// D-454): as a binary operand, a call's argument, a method's receiver, an anonymous
/// struct's field, an interpolated part, an assignment's value and a compound
/// assignment's value, at the top level and in a function's frame. Each case is one of
/// R-77's reproducers or a sibling shape, run through the CLI.
/// </summary>
public sealed class SwitchExprOperandStackTests {
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
    public void Switch_AsRightOperand_AddsToTheLeftOperand() =>
        AssertRuns("""
            y := 1 + (5 switch { 0 => 1, _ => 2 })
            print(y)
            """, "3");

    [Fact]
    public void Switch_AsBothOperands_MultipliesTheirValues() =>
        AssertRuns("""
            print((1 switch { 1 => 4, _ => 0 }) * (2 switch { 2 => 3, _ => 0 }))
            """, "12");

    [Fact]
    public void Switch_AsAnonymousStructField_IsTheFieldsValue() =>
        AssertRuns("""
            s := #{ m0: 5 switch { 0 => 1.5, _ => 2.5 } }
            print(s.m0)
            """, "2.5");

    [Fact]
    public void Switch_AsNativeArgument_IsPassedToTheNative() =>
        AssertRuns("""
            print(math.pow(5 switch { 0 => 1.5, _ => 2.5 }, 1))
            """, "2.5");

    [Fact]
    public void Switch_AsMethodReceiver_ReceivesTheCall() =>
        AssertRuns("""
            print((10 switch { 0 => "", _ => "abc" }).upper())
            """, "ABC");

    [Fact]
    public void Switch_AsPropertyReceiver_ReadsTheProperty() =>
        AssertRuns("""
            print((10 switch { 0 => "", _ => "a" }).isEmpty)
            """, "false");

    [Fact]
    public void Switch_AsInterpolatedPart_FollowsTheLiteralBeforeIt() =>
        AssertRuns("""
            print("v=${4 switch { 4 => "four", _ => "?" }}!")
            """, "v=four!");

    [Fact]
    public void Switch_AsIndexAssignmentValue_WritesTheElement() =>
        AssertRuns("""
            xs := [5, 6]
            xs[0] = ("Hello" switch { "k0" => 8, _ => 1 })
            print(xs[0])
            print(xs[1])
            """, "1", "6");

    [Fact]
    public void Switch_AsFieldAssignmentValue_WritesTheField() =>
        AssertRuns("""
            type T {
                f1: float
            }
            t := T { f1: 0.0 }
            t.f1 = ("ab" switch { "ab" => 1.5, _ => 2.5 })
            print(t.f1)
            """, "1.5");

    [Fact]
    public void Switch_AsCompoundIndexAssignmentValue_AddsToTheElement() =>
        AssertRuns("""
            xs := [10, 20]
            xs[1] += (3 switch { 3 => 5, _ => 0 })
            print(xs[1])
            """, "25");

    [Fact]
    public void Switch_NestedInAnArmBesideAnOperand_GivesBothValues() =>
        AssertRuns("""
            print(1 + (2 switch { 2 => 10 + (3 switch { 3 => 100, _ => 0 }), _ => 0 }))
            """, "111");

    [Fact]
    public void Switch_InAFunctionFrame_LeavesTheLocalsBelowItIntact() =>
        AssertRuns("""
            fn f(a: int): int {
                b := 2
                return a + b * (a switch { 1 => 10, _ => 20 })
            }
            print(f(1))
            print(f(3))
            """, "21", "43");

    [Fact]
    public void Switch_AsABindingsWholeValue_StillBindsTheMatchedArm() =>
        AssertRuns("""
            x := 2 switch { 1 => "one", 2 => "two", _ => "many" }
            print(x)
            """, "two");
}
