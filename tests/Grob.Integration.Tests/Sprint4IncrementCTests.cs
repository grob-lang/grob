using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 4 Increment C end-to-end tests — <c>for...in</c> iteration. Each test
/// drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
/// <remarks>
/// Array and numeric-range forms (including <c>step</c> and descending) run from
/// source here. The map form has no source construction path in v1 — there is no
/// map literal in the parser (out-of-scope parser work) — so map iteration is
/// proven at the VM level in <c>Grob.Vm.Tests.VirtualMachineForInTests</c>.
/// </remarks>
public sealed class Sprint4IncrementCTests {
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

    // -----------------------------------------------------------------------
    // Array forms
    // -----------------------------------------------------------------------

    [Fact]
    public void ArraySingle_IteratesEveryElementInOrder() {
        string stdout = Run("""
            for item in [10, 20, 30] {
                print(item)
            }
            """);
        Assert.Equal($"10{NL}20{NL}30{NL}", stdout);
    }

    [Fact]
    public void ArrayIndex_YieldsZeroBasedIndexAndElement() {
        string stdout = Run("""
            for i, item in [100, 200, 300] {
                print(i)
                print(item)
            }
            """);
        Assert.Equal($"0{NL}100{NL}1{NL}200{NL}2{NL}300{NL}", stdout);
    }

    [Fact]
    public void EmptyArray_RunsZeroIterations() {
        string stdout = Run("""
            for item in [] {
                print(item)
            }
            print(99)
            """);
        Assert.Equal($"99{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Numeric ranges
    // -----------------------------------------------------------------------

    [Fact]
    public void AscendingRange_IsInclusiveOfBothBounds() {
        string stdout = Run("""
            for i in 0..3 {
                print(i)
            }
            """);
        Assert.Equal($"0{NL}1{NL}2{NL}3{NL}", stdout);
    }

    [Fact]
    public void SteppedRange_AdvancesByStep() {
        string stdout = Run("""
            for i in 0..10 step 5 {
                print(i)
            }
            """);
        Assert.Equal($"0{NL}5{NL}10{NL}", stdout);
    }

    [Fact]
    public void DescendingRange_CountsDownInclusive() {
        string stdout = Run("""
            for i in 3..0 step -1 {
                print(i)
            }
            """);
        Assert.Equal($"3{NL}2{NL}1{NL}0{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Loop control — continue advances the counter, break exits
    // -----------------------------------------------------------------------

    [Fact]
    public void Continue_AdvancesTheCounterAndSkipsTheRest() {
        string stdout = Run("""
            for i in 0..5 {
                if (i == 2) {
                    continue
                }
                print(i)
            }
            """);
        Assert.Equal($"0{NL}1{NL}3{NL}4{NL}5{NL}", stdout);
    }

    [Fact]
    public void Break_ExitsTheLoop() {
        string stdout = Run("""
            for i in 0..9 {
                if (i == 3) {
                    break
                }
                print(i)
            }
            """);
        Assert.Equal($"0{NL}1{NL}2{NL}", stdout);
    }

    [Fact]
    public void NestedForIn_LoopControlResolvesToInnermost() {
        string stdout = Run("""
            for i in 0..1 {
                for j in 0..2 {
                    if (j == 1) {
                        continue
                    }
                    print(i * 10 + j)
                }
            }
            """);
        // inner j: 0 (print), 1 (skip), 2 (print) for each i in {0, 1}
        Assert.Equal($"0{NL}2{NL}10{NL}12{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Combined script — array, stepped range, break and continue together
    // -----------------------------------------------------------------------

    [Fact]
    public void CombinedScript_ArrayAndSteppedRangeWithBreakAndContinue() {
        string stdout = Run("""
            for n in [1, 2, 3] {
                if (n == 2) {
                    continue
                }
                print(n)
            }
            for i in 0..100 step 10 {
                if (i == 30) {
                    break
                }
                print(i)
            }
            """);
        Assert.Equal($"1{NL}3{NL}0{NL}10{NL}20{NL}", stdout);
    }
}
