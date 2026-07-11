using System.Text;

using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 4 Increment A end-to-end tests — forward-jump conditionals.
/// Covers <c>if</c>/<c>else if</c>/<c>else</c>, <c>&amp;&amp;</c>/<c>||</c>
/// short-circuit, and the ternary <c>?:</c>.  Each test drives the full
/// pipeline: Lexer → Parser → TypeChecker → Compiler → VM.
/// </summary>
public sealed class Sprint4IncrementATests {
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
    // if — with bool variable
    // -----------------------------------------------------------------------

    /// <summary>
    /// An <c>if</c> with a bool variable condition executes the correct branch.
    /// </summary>
    [Fact]
    public void If_BoolVariable_ExecutesThenBranch() {
        string stdout = Run("""
            flag := true
            if (flag) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"yes{NL}", stdout);
    }

    [Fact]
    public void If_FalseBoolVariable_ExecutesElseBranch() {
        string stdout = Run("""
            flag := false
            if (flag) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"no{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // if — else if / else chain
    // -----------------------------------------------------------------------

    /// <summary>
    /// A three-arm <c>if</c>/<c>else if</c>/<c>else</c> chain executes the
    /// correct arm when each condition is met.
    /// </summary>
    [Fact]
    public void IfElseIfElse_FirstArmTrue_PrintsFirst() {
        string stdout = Run("""
            a := true
            b := false
            if (a) {
                print("first")
            } else if (b) {
                print("second")
            } else {
                print("third")
            }
            """);
        Assert.Equal($"first{NL}", stdout);
    }

    [Fact]
    public void IfElseIfElse_SecondArmTrue_PrintsSecond() {
        string stdout = Run("""
            a := false
            b := true
            if (a) {
                print("first")
            } else if (b) {
                print("second")
            } else {
                print("third")
            }
            """);
        Assert.Equal($"second{NL}", stdout);
    }

    [Fact]
    public void IfElseIfElse_AllFalse_PrintsElse() {
        string stdout = Run("""
            a := false
            b := false
            if (a) {
                print("first")
            } else if (b) {
                print("second")
            } else {
                print("third")
            }
            """);
        Assert.Equal($"third{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // if — with comparison condition
    // -----------------------------------------------------------------------

    /// <summary>
    /// An <c>if</c> with a comparison condition uses the correct branch based
    /// on the integer comparison result.
    /// </summary>
    [Fact]
    public void If_IntComparison_ExecutesCorrectBranch() {
        string stdout = Run("""
            x := 5
            y := 3
            if (x > y) {
                print("greater")
            } else {
                print("less")
            }
            """);
        Assert.Equal($"greater{NL}", stdout);
    }

    /// <summary>
    /// String <c>&lt;=</c> lowers to <c>!(a &gt; b)</c>. End-to-end it must produce the
    /// correct boolean for the true, equal and false cases — proving the lowering is
    /// semantically equivalent, not just the right opcode shape.
    /// </summary>
    [Theory]
    [InlineData("\"apple\"", "\"banana\"", "yes")]   // less than → true
    [InlineData("\"apple\"", "\"apple\"", "yes")]    // equal → true
    [InlineData("\"banana\"", "\"apple\"", "no")]    // greater → false
    public void If_StringLessEqual_ExecutesCorrectBranch(string left, string right, string expected) {
        string stdout = Run($$"""
            if ({{left}} <= {{right}}) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"{expected}{NL}", stdout);
    }

    /// <summary>
    /// String <c>&gt;=</c> lowers to <c>!(a &lt; b)</c>; same equivalence check.
    /// </summary>
    [Theory]
    [InlineData("\"banana\"", "\"apple\"", "yes")]   // greater than → true
    [InlineData("\"apple\"", "\"apple\"", "yes")]    // equal → true
    [InlineData("\"apple\"", "\"banana\"", "no")]    // less → false
    public void If_StringGreaterEqual_ExecutesCorrectBranch(string left, string right, string expected) {
        string stdout = Run($$"""
            if ({{left}} >= {{right}}) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"{expected}{NL}", stdout);
    }

    /// <summary>
    /// A mixed-arm ternary (<c>int</c>/<c>float</c>) unifies to <c>float</c>; whichever
    /// arm runs must leave a float on the stack so a parent float operation succeeds.
    /// Regression: before arm-coercion the int then-arm crashed the VM with a kind
    /// mismatch when consumed by <c>+ 1.0</c> (AddFloat over an int value).
    /// </summary>
    [Theory]
    [InlineData("true", "3.0")]    // then-arm 2 (→ 2.0) + 1.0 = 3.0
    [InlineData("false", "4.0")]   // else-arm 3.0 + 1.0 = 4.0
    public void Ternary_MixedArms_CoercesToFloatAtRuntime(string cond, string expected) {
        string stdout = Run($"""
            x := ({cond} ? 2 : 3.0) + 1.0
            print(x)
            """);
        Assert.Equal($"{expected}{NL}", stdout);
    }

    [Fact]
    public void IfElseIfElse_WithComparisons_ProducesCorrectBranch() {
        string stdout = Run("""
            x := 5
            y := 3
            if (x < y) {
                print("less")
            } else if (x == y) {
                print("equal")
            } else {
                print("greater")
            }
            """);
        Assert.Equal($"greater{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // && — short-circuit integration
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>&amp;&amp;</c> short-circuits: when the left operand is <c>false</c>,
    /// the right is not evaluated.
    /// </summary>
    [Fact]
    public void And_ShortCircuit_FalseLeft_ResultIsFalse() {
        string stdout = Run("""
            a := false
            b := true
            if (a && b) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"no{NL}", stdout);
    }

    [Fact]
    public void And_BothTrue_ResultIsTrue() {
        string stdout = Run("""
            a := true
            b := true
            if (a && b) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"yes{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // || — short-circuit integration
    // -----------------------------------------------------------------------

    /// <summary>
    /// <c>||</c> short-circuits: when the left operand is <c>true</c>,
    /// the right is not evaluated.
    /// </summary>
    [Fact]
    public void Or_ShortCircuit_TrueLeft_ResultIsTrue() {
        string stdout = Run("""
            a := true
            b := false
            if (a || b) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"yes{NL}", stdout);
    }

    [Fact]
    public void Or_BothFalse_ResultIsFalse() {
        string stdout = Run("""
            a := false
            b := false
            if (a || b) {
                print("yes")
            } else {
                print("no")
            }
            """);
        Assert.Equal($"no{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Ternary — integration
    // -----------------------------------------------------------------------

    /// <summary>
    /// A ternary expression with a <c>bool</c> condition produces the then-arm
    /// value when the condition is true.
    /// </summary>
    [Fact]
    public void Ternary_TrueCondition_PrintsThenValue() {
        string stdout = Run("""
            flag := true
            x := flag ? 1 : 2
            print(x)
            """);
        Assert.Equal($"1{NL}", stdout);
    }

    [Fact]
    public void Ternary_FalseCondition_PrintsElseValue() {
        string stdout = Run("""
            flag := false
            x := flag ? 1 : 2
            print(x)
            """);
        Assert.Equal($"2{NL}", stdout);
    }

    [Fact]
    public void Ternary_StringArms_Integration() {
        string stdout = Run("""
            ok := true
            msg := ok ? "pass" : "fail"
            print(msg)
            """);
        Assert.Equal($"pass{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Nested if
    // -----------------------------------------------------------------------

    /// <summary>
    /// Nested <c>if</c> statements execute independently: inner condition is only
    /// evaluated when the outer condition is true.
    /// </summary>
    [Fact]
    public void NestedIf_BothTrue_ExecutesInnerThen() {
        string stdout = Run("""
            outer := true
            inner := true
            if (outer) {
                if (inner) {
                    print("both")
                } else {
                    print("outer only")
                }
            } else {
                print("neither")
            }
            """);
        Assert.Equal($"both{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Type-checker integration — non-bool condition errors
    // -----------------------------------------------------------------------

    /// <summary>
    /// A non-<c>bool</c> <c>if</c> condition must produce E0001 through the
    /// full pipeline (type-check stage).
    /// </summary>
    [Fact]
    public void If_NonBoolCondition_ProducesE0001() {
        DiagnosticBag bag = TypeCheck("""
            x := 42
            if (x) {
                print("bad")
            }
            """);
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }

    /// <summary>
    /// Non-unifying ternary arms produce E0001 through the full pipeline.
    /// </summary>
    [Fact]
    public void Ternary_NonUnifyingArms_ProducesE0001() {
        DiagnosticBag bag = TypeCheck("""x := true ? 1 : "bad" """);
        Assert.True(bag.HasErrors);
        Assert.Single(bag.Errors, e => e.Code == "E0001");
    }
}
