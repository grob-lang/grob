using System.Text;

using Grob.Cli;
using Grob.Compiler;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// Sprint 9 Increment A4b (D-360) integration tests — compound assignment
/// (<c>obj.field op= v</c>) and increment/decrement (<c>obj.field++</c>/<c>--</c>) on
/// member (field) targets, closing the sibling silent-drop gap D-359 confirmed but left
/// unfixed. Driven end to end (lex, parse, type-check, compile, VM) to prove a real
/// <c>.grob</c> script observes the read-modify-write, the evaluate-once receiver
/// semantics, and the inherited runtime-error paths — before this increment the
/// statement compiled to nothing, so no such script could reach any of this.
/// </summary>
public sealed class Sprint9IncrementA4bTests {
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

    private const string ConfigType = "type Config {\ncount: int\n}\n";

    [Fact]
    public void MemberCompoundAssignAllOperators_MutateInPlace() {
        string stdout = Run(
            ConfigType +
            "c := Config { count: 10 }\n" +
            "c.count += 5\n" +
            "print(c.count)\n" +
            "c.count -= 3\n" +
            "print(c.count)\n" +
            "c.count *= 2\n" +
            "print(c.count)\n" +
            "c.count /= 4\n" +
            "print(c.count)\n" +
            "c.count %= 3\n" +
            "print(c.count)\n");

        Assert.Equal(
            "15" + NL + "12" + NL + "24" + NL + "6" + NL + "0" + NL,
            stdout);
    }

    [Fact]
    public void MemberIncrementAndDecrement_MutateInPlace() {
        string stdout = Run(
            ConfigType +
            "c := Config { count: 10 }\n" +
            "c.count++\n" +
            "print(c.count)\n" +
            "c.count--\n" +
            "c.count--\n" +
            "print(c.count)\n");

        Assert.Equal("11" + NL + "9" + NL, stdout);
    }

    [Fact]
    public void FloatMemberCompoundAssign_WidensIntRhsToFloat() {
        string stdout = Run("type FConfig {\namount: float\n}\nf := FConfig { amount: 1.0 }\nf.amount += 1\nprint(f.amount)\n");

        Assert.Equal("2.0" + NL, stdout);
    }

    [Fact]
    public void ChainedTarget_CompoundAssignAndIncrementMutateTheCorrectNestedField() {
        string stdout = Run(
            "type Inner {\nc: int\n}\n" +
            "type Outer {\nb: Inner\n}\n" +
            "a := Outer { b: Inner { c: 1 } }\n" +
            "a.b.c += 100\n" +
            "a.b.c++\n" +
            "print(a.b.c)\n");

        Assert.Equal("102" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Evaluate-once — the load-bearing case named in the A4b kickoff prompt.
    // -----------------------------------------------------------------------

    [Fact]
    public void SideEffectingReceiverExpression_IsEvaluatedExactlyOnce() {
        string stdout = Run(
            ConfigType +
            "c := Config { count: 10 }\n" +
            "callCount := 0\n" +
            "fn getObj(): Config {\n" +
            "    callCount = callCount + 1\n" +
            "    return c\n" +
            "}\n" +
            "getObj().count += 1\n" +
            "print(c.count)\n" +
            "print(callCount)\n");

        Assert.Equal("11" + NL + "1" + NL, stdout);
    }

    // -----------------------------------------------------------------------
    // Runtime errors inherited through the reused typed binary opcodes —
    // unhandled, so the CLI's top-level diagnostic formatting is exercised.
    // -----------------------------------------------------------------------

    private static string FixturePath(string name) =>
        Path.Join(AppContext.BaseDirectory, "fixtures", "sprint-9a4b", name);

    private static (string Stdout, string Stderr, int ExitCode) RunFile(string fixtureName) {
        var stdout = new StringWriter(new StringBuilder());
        var stderr = new StringWriter(new StringBuilder());
        int exitCode = new RunCommand(stdout, stderr).Run(FixturePath(fixtureName));
        return (stdout.ToString(), stderr.ToString(), exitCode);
    }

    [Fact]
    public void ModuloAssign_ByZero_Unhandled_ProducesE5003AndExitsOne() {
        (string stdout, string stderr, int exitCode) =
            RunFile("member-compound-assign-modulo-by-zero.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5003]:", stderr);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void PlusAssign_IntOverflow_Unhandled_ProducesE5001AndExitsOne() {
        (string stdout, string stderr, int exitCode) =
            RunFile("member-compound-assign-overflow.grob");

        Assert.Equal(string.Empty, stdout);
        Assert.StartsWith("error[E5001]:", stderr);
        Assert.Equal(1, exitCode);
    }
}
