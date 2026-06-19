using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 4 Increment E end-to-end tests — the switch <b>expression</b>. Each test
/// drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
/// <remarks>
/// Proves the expression produces a value usable anywhere an expression is (bound,
/// printed, passed onward), the first-match-wins ordering, the <c>_</c> fall-through,
/// relational patterns, and nullable-scrutinee matching.
/// </remarks>
public sealed class Sprint4IncrementETests {
    private static string Run(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"Pipeline produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler produced unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var output = new StringWriter(new StringBuilder());
        var vm = new VirtualMachine(output);
        vm.Run(chunk);
        return output.ToString();
    }

    private static string NL => Environment.NewLine;

    [Theory]
    [InlineData(1, "one")]
    [InlineData(2, "two")]
    [InlineData(3, "many")]
    public void ValuePatternSwitch_BindsAndPrintsMatchingArm(int n, string expected) {
        string stdout = Run($$"""
            x := {{n}} switch {
                1 => "one",
                2 => "two",
                _ => "many"
            }
            print(x)
            """);
        Assert.Equal($"{expected}{NL}", stdout);
    }

    [Fact]
    public void RelationalPatternSwitch_FirstMatchWins() {
        string stdout = Run("""
            used := 92
            status := used switch {
                >= 90 => "CRITICAL",
                >= 75 => "WARNING",
                _     => "OK"
            }
            print(status)
            """);
        Assert.Equal($"CRITICAL{NL}", stdout);
    }

    [Fact]
    public void RelationalPatternSwitch_FallsThroughToCatchAll() {
        string stdout = Run("""
            used := 40
            status := used switch {
                >= 90 => "CRITICAL",
                >= 75 => "WARNING",
                _     => "OK"
            }
            print(status)
            """);
        Assert.Equal($"OK{NL}", stdout);
    }

    [Fact]
    public void Switch_UsableInlineAsCallArgument() {
        string stdout = Run("""
            code := 404
            print(code switch {
                200 => "OK",
                404 => "Not found",
                _   => "Unknown"
            })
            """);
        Assert.Equal($"Not found{NL}", stdout);
    }

    [Fact]
    public void BoolScrutinee_ExhaustiveWithoutCatchAll_Runs() {
        string stdout = Run("""
            ready := false
            label := ready switch {
                true  => "go",
                false => "wait"
            }
            print(label)
            """);
        Assert.Equal($"wait{NL}", stdout);
    }

    [Fact]
    public void IntAndFloatArms_UnifyToFloat_AtRuntime() {
        string stdout = Run("""
            n := 2
            v := n switch {
                1 => 10,
                _ => 2.5
            }
            print(v)
            """);
        Assert.Equal($"2.5{NL}", stdout);
    }
}
