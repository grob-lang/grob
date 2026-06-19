using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 4 Increment B end-to-end tests — <c>while</c> loops and loop control.
/// Each test drives the full pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint4IncrementBTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

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

    private static DiagnosticBag TypeCheck(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // while — basic execution
    // -----------------------------------------------------------------------

    /// <summary>
    /// A counted <c>while</c> loop prints values 3, 2, 1 then exits.
    /// Verifies correct iteration count and that the condition is re-evaluated
    /// each time.
    /// </summary>
    [Fact]
    public void WhileLoop_Countdown_PrintsThreeTwoOne() {
        string stdout = Run("""
            i := 3
            while (i > 0) {
                print(i)
                i = i - 1
            }
            """);
        Assert.Equal($"3{NL}2{NL}1{NL}", stdout);
    }

    /// <summary>
    /// A <c>while</c> whose condition is immediately <c>false</c> must execute
    /// zero iterations and produce no output.
    /// </summary>
    [Fact]
    public void WhileLoop_FalseCondition_ZeroIterations() {
        string stdout = Run("""
            while (false) {
                print("never")
            }
            """);
        Assert.Equal("", stdout);
    }

    /// <summary>
    /// A <c>while</c> loop with a bool variable condition executes as long as
    /// the variable remains true.
    /// </summary>
    [Fact]
    public void WhileLoop_BoolVariable_ExecutesWhileTrue() {
        string stdout = Run("""
            flag := true
            count := 0
            while (flag) {
                count = count + 1
                if (count >= 3) {
                    flag = false
                }
            }
            print(count)
            """);
        Assert.Equal($"3{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // while — break
    // -----------------------------------------------------------------------

    /// <summary>
    /// A <c>break</c> exits the loop immediately on the first matching iteration,
    /// leaving remaining iterations unexecuted.
    /// </summary>
    [Fact]
    public void WhileLoop_Break_ExitsEarly() {
        string stdout = Run("""
            i := 1
            while (i <= 5) {
                if (i == 3) {
                    break
                }
                print(i)
                i = i + 1
            }
            """);
        // Prints 1, 2 then breaks at i==3 before the print executes.
        Assert.Equal($"1{NL}2{NL}", stdout);
    }

    /// <summary>
    /// Multiple <c>break</c> paths inside the same loop all exit cleanly to
    /// the same post-loop position.
    /// </summary>
    [Fact]
    public void WhileLoop_TwoBreakPaths_BothExitCorrectly() {
        string stdout = Run("""
            i := 0
            result := "none"
            while (i < 10) {
                i = i + 1
                if (i == 2) {
                    result = "two"
                    break
                }
                if (i == 5) {
                    result = "five"
                    break
                }
            }
            print(result)
            """);
        // Hits i==2 first.
        Assert.Equal($"two{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // while — continue
    // -----------------------------------------------------------------------

    /// <summary>
    /// A <c>continue</c> skips the rest of the loop body and re-evaluates the
    /// condition.  Values that trigger <c>continue</c> are not printed.
    /// </summary>
    [Fact]
    public void WhileLoop_Continue_SkipsEvenNumbers() {
        string stdout = Run("""
            i := 0
            while (i < 6) {
                i = i + 1
                if (i % 2 == 0) {
                    continue
                }
                print(i)
            }
            """);
        // Odd values: 1, 3, 5
        Assert.Equal($"1{NL}3{NL}5{NL}", stdout);
    }

    /// <summary>
    /// After a <c>continue</c>, the condition is re-evaluated and the loop
    /// exits normally when the condition becomes false.
    /// </summary>
    [Fact]
    public void WhileLoop_Continue_LoopStillExitsNormally() {
        string stdout = Run("""
            i := 0
            while (i < 4) {
                i = i + 1
                if (i == 2) {
                    continue
                }
                print(i)
            }
            print("done")
            """);
        // Prints 1, skips 2 (continue), prints 3, 4, then exits.
        Assert.Equal($"1{NL}3{NL}4{NL}done{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // while — nested loops resolve break/continue to innermost
    // -----------------------------------------------------------------------

    /// <summary>
    /// A <c>break</c> inside an inner loop must exit only the inner loop.
    /// The outer loop must continue running normally.
    /// </summary>
    [Fact]
    public void NestedWhile_InnerBreak_DoesNotExitOuterLoop() {
        string stdout = Run("""
            outer := 0
            while (outer < 3) {
                outer = outer + 1
                inner := 0
                while (inner < 3) {
                    inner = inner + 1
                    if (inner == 2) {
                        break
                    }
                }
                print(outer)
            }
            """);
        // Outer runs 3 times, inner always breaks at 2 — prints "1 2 3"
        Assert.Equal($"1{NL}2{NL}3{NL}", stdout);
    }

    /// <summary>
    /// A <c>continue</c> inside an inner loop must skip only the rest of the
    /// inner loop body for that iteration; the outer loop is unaffected.
    /// </summary>
    [Fact]
    public void NestedWhile_InnerContinue_DoesNotAffectOuterLoop() {
        string stdout = Run("""
            outer := 0
            while (outer < 2) {
                outer = outer + 1
                inner := 0
                while (inner < 3) {
                    inner = inner + 1
                    if (inner == 2) {
                        continue
                    }
                    print(inner)
                }
            }
            """);
        // Each outer iteration prints 1, 3 (skipping 2 via continue). Two outer rounds.
        Assert.Equal($"1{NL}3{NL}1{NL}3{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // while — locals inside loop body are correctly scoped
    // -----------------------------------------------------------------------

    /// <summary>
    /// A local variable declared inside a <c>while</c> body must be scoped to
    /// the body — the variable is not visible after the loop exits.
    /// </summary>
    [Fact]
    public void WhileLoop_LocalInBody_ScopedToBody() {
        string stdout = Run("""
            i := 0
            while (i < 3) {
                msg := "iter"
                i = i + 1
            }
            print(i)
            """);
        // 'msg' is local to the body; 'i' survives.
        Assert.Equal($"3{NL}", stdout);
    }

    /// <summary>
    /// A local declared inside a <c>while</c> body is not visible after the loop:
    /// referencing it produces E1001 (undefined identifier), proving the body
    /// scope closes at the loop exit.
    /// </summary>
    [Fact]
    public void WhileLoop_LocalInBody_NotVisibleAfterLoop() {
        DiagnosticBag bag = TypeCheck("""
            i := 0
            while (i < 3) {
                msg := "iter"
                i = i + 1
            }
            print(msg)
            """);
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E1001");
    }

    // -----------------------------------------------------------------------
    // Diagnostics — break / continue outside loop
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>break</c> outside any loop and any <c>select</c> must produce exactly E2212
    /// — the generic out-of-loop code (D-315). E2211 is reserved for <c>break</c>
    /// inside a <c>select</c>.
    /// </summary>
    [Fact]
    public void Break_OutsideLoop_ProducesE2212() {
        DiagnosticBag bag = TypeCheck("break");
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E2212");
    }

    /// <summary>
    /// <c>continue</c> outside any loop must produce exactly E2212.
    /// </summary>
    [Fact]
    public void Continue_OutsideLoop_ProducesE2212() {
        DiagnosticBag bag = TypeCheck("continue");
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E2212");
    }

    /// <summary>
    /// A <c>while</c> with a non-<c>bool</c> condition must produce E0001.
    /// </summary>
    [Fact]
    public void While_NonBoolCondition_ProducesE0001() {
        DiagnosticBag bag = TypeCheck("""
            x := 42
            while (x) { }
            """);
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }
}
