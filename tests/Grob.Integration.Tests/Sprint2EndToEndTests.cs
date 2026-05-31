using System.Text;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 2 end-to-end tests: Lexer → Parser → TypeChecker → Compiler → VM.
/// Each test drives the full pipeline from source text to observed standard
/// output, verifying that all layers are correctly wired together at the
/// assembly boundary (D-307, D-308).
/// </summary>
public sealed class Sprint2EndToEndTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs the complete pipeline for <paramref name="source"/> and returns
    /// observed stdout. Fails the test immediately if the type-check or compile
    /// step produces diagnostics.
    /// </summary>
    private static string Run(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"Pipeline produced unexpected errors: {string.Join("; ", bag.Errors)}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        var output = new StringWriter(new StringBuilder());
        var vm = new VirtualMachine(output);
        vm.Run(chunk);
        return output.ToString();
    }

    /// <summary>
    /// Runs the pipeline and returns the <see cref="DiagnosticBag"/> without
    /// executing the VM.  Used for tests that expect type errors.
    /// </summary>
    private static DiagnosticBag TypeCheck(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    // -----------------------------------------------------------------------
    // Acceptance witnesses
    // -----------------------------------------------------------------------

    [Fact]
    public void PrintTwoPlusThreeTimesFour_OutputsFourteen() {
        // The canonical Sprint 2 acceptance example (D-307).
        string stdout = Run("print(2 + 3 * 4)");
        Assert.Equal($"14{Environment.NewLine}", stdout);
    }

    [Fact]
    public void PrintFiveMinusTwo_OutputsThree() {
        string stdout = Run("print(5 - 2)");
        Assert.Equal($"3{Environment.NewLine}", stdout);
    }

    [Fact]
    public void PrintIntMultiply_OutputsProduct() {
        string stdout = Run("print(6 * 7)");
        Assert.Equal($"42{Environment.NewLine}", stdout);
    }

    [Fact]
    public void PrintIntDivide_OutputsTruncatedQuotient() {
        string stdout = Run("print(7 / 2)");
        Assert.Equal($"3{Environment.NewLine}", stdout);
    }

    [Fact]
    public void PrintIntModulo_OutputsRemainder() {
        string stdout = Run("print(7 % 3)");
        Assert.Equal($"1{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Float arithmetic
    // -----------------------------------------------------------------------

    [Fact]
    public void PrintFloatAdd_OutputsFloat() {
        string stdout = Run("print(1.5 + 2.5)");
        Assert.Equal($"4{Environment.NewLine}", stdout);
    }

    [Fact]
    public void PrintMixedArithmetic_IntPlusFloat_OutputsFloat() {
        string stdout = Run("print(1 + 0.5)");
        Assert.Equal($"1.5{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Unary negation
    // -----------------------------------------------------------------------

    [Fact]
    public void PrintNegateInt_OutputsNegativeValue() {
        string stdout = Run("print(-5)");
        Assert.Equal($"-5{Environment.NewLine}", stdout);
    }

    [Fact]
    public void PrintNegateFloat_OutputsNegativeFloat() {
        string stdout = Run("print(-1.5)");
        Assert.Equal($"-1.5{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // String concatenation
    // -----------------------------------------------------------------------

    [Fact]
    public void PrintStringConcat_OutputsCombinedString() {
        string stdout = Run("print(\"hello\" + \" world\")");
        Assert.Equal($"hello world{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Multiple print statements
    // -----------------------------------------------------------------------

    [Fact]
    public void MultiplePrintStatements_EachOutputsOnOwnLine() {
        string stdout = Run("print(1)\nprint(2)\nprint(3)");
        Assert.Equal($"1{Environment.NewLine}2{Environment.NewLine}3{Environment.NewLine}", stdout);
    }

    // -----------------------------------------------------------------------
    // Type-error guard — pipeline must NOT execute when type checker fails
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeMismatch_StringPlusInt_EmitsDiagnosticAndPipelineHalts() {
        // "hello" + 42 is incompatible — the type checker must flag it and
        // the test must not reach the VM.
        DiagnosticBag bag = TypeCheck("\"hello\" + 42");
        Assert.True(bag.HasErrors, "Expected type error for string + int");
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0002", error.Code);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(1, error.Range.Start.Column); // BinaryExpr range starts at '"' of "hello"
    }
}
