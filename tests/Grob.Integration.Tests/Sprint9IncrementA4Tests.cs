using System.Text;

using Grob.Cli;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment A4 (D-359) integration tests — compound assignment
/// (<c>arr[i] op= v</c>) and increment/decrement (<c>arr[i]++</c>/<c>--</c>) on
/// index targets, closing the silent-drop gap D-350 named. Driven end to end
/// (lex, parse, type-check, compile, VM) to prove a real <c>.grob</c> script
/// observes the read-modify-write, the evaluate-once receiver/index semantics,
/// and the inherited runtime-error paths — before this increment the statement
/// compiled to nothing, so no such script could reach any of this.
/// </summary>
public sealed class Sprint9IncrementA4Tests {
    private static string NL => Environment.NewLine;

    // -----------------------------------------------------------------------
    // In-process pipeline runner for the successful paths.
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

    [Fact]
    public void ArrayCompoundAssignAllOperators_MutateInPlace() {
        string stdout = Run(
            "arr := [10, 20, 30]\n" +
            "arr[0] += 5\n" +
            "arr[1] -= 5\n" +
            "arr[2] *= 2\n" +
            "print(arr[0])\n" +
            "print(arr[1])\n" +
            "print(arr[2])\n" +
            "arr[0] /= 3\n" +
            "arr[0] %= 4\n" +
            "print(arr[0])\n");

        Assert.Equal(
            "15" + NL + "15" + NL + "60" + NL + // += -= *=
            "1" + NL,                            // 15 / 3 = 5, 5 % 4 = 1
            stdout);
    }

    [Fact]
    public void ArrayIncrementAndDecrement_MutateInPlace() {
        string stdout = Run(
            "arr := [10, 20, 30]\n" +
            "arr[0]++\n" +
            "arr[2]--\n" +
            "print(arr[0])\n" +
            "print(arr[2])\n");

        Assert.Equal("11" + NL + "29" + NL, stdout);
    }

    [Fact]
    public void FloatArrayCompoundAssign_WidensIntRhsToFloat() {
        string stdout = Run("farr := [1.0, 2.0]\nfarr[0] += 1\nprint(farr[0])\n");

        Assert.Equal("2.0" + NL, stdout);
    }

    [Fact]
    public void ChainedMatrixTarget_CompoundAssignAndIncrementMutateTheCorrectNestedArray() {
        string stdout = Run(
            "matrix := [[1, 2], [3, 4]]\n" +
            "r := 0\nc := 1\n" +
            "matrix[r][c] += 100\n" +
            "matrix[1][0]++\n" +
            "print(matrix[0][1])\n" +
            "print(matrix[1][0])\n" +
            "print(matrix[0][0])\n"); // untouched

        Assert.Equal("102" + NL + "4" + NL + "1" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Evaluate-once — the load-bearing case named in the A4 kickoff prompt.
    // -----------------------------------------------------------------------

    [Fact]
    public void SideEffectingIndexExpression_IsEvaluatedExactlyOnce() {
        string stdout = Run(
            "arr := [10, 20, 30]\n" +
            "callCount := 0\n" +
            "fn trackedIndex(i: int): int {\n" +
            "    callCount = callCount + 1\n" +
            "    return i\n" +
            "}\n" +
            "arr[trackedIndex(0)] += 1\n" +
            "print(arr[0])\n" +
            "print(callCount)\n");

        Assert.Equal("11" + NL + "1" + NL, stdout);
    }

    [Fact]
    public void SideEffectingReceiverExpression_IsEvaluatedExactlyOnce() {
        string stdout = Run(
            "arr := [10, 20, 30]\n" +
            "callCount := 0\n" +
            "fn getArr(): int[] {\n" +
            "    callCount = callCount + 1\n" +
            "    return arr\n" +
            "}\n" +
            "getArr()[1] += 1\n" +
            "print(arr[1])\n" +
            "print(callCount)\n");

        Assert.Equal("21" + NL + "1" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Runtime errors inherited through the reused typed binary opcodes —
    // unhandled, so the CLI's top-level diagnostic formatting is exercised.
    // -----------------------------------------------------------------------

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-9a4", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void DivideAssign_ByZero_Unhandled_ProducesE5002AndExitsOne() {
        (string stdout, string stderr, int exitCode) =
            RunFile("index-compound-assign-div-by-zero.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5002]:", stderr);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void PlusAssign_IntOverflow_Unhandled_ProducesE5001AndExitsOne() {
        (string stdout, string stderr, int exitCode) =
            RunFile("index-compound-assign-overflow.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5001]:", stderr);
        Assert.Equal(1, exitCode);
    }
}
