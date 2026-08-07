using System.Text;

using Grob.Compiler;
using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Vm;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Integration.Tests;

/// <summary>
/// End-to-end (lex, parse, type-check, compile, VM) tests for the D-380/D-400 fix:
/// <c>?.</c> on a method call (<c>xs?.first()</c>) must short-circuit to <c>nil</c> on
/// a nil receiver exactly as the property path (<c>xs?.length</c>) already does,
/// instead of crashing the VM with an internal "Call target is not a function"
/// error. Driven through the full pipeline via the in-process runner pattern from
/// <c>Sprint9IncrementA4Tests</c> — no fixture files, no CLI process spawn.
/// </summary>
public sealed class OptionalChainMethodCallTests {
    private static string NL => Environment.NewLine;

    private static string Run(string source) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var output = new StringWriter(new StringBuilder());
        var vm = new VirtualMachine(output);
        vm.Run(chunk);
        return output.ToString();
    }

    // -----------------------------------------------------------------------
    // The reported case, and the receiver-kind matrix (array/string/numeric/nominal
    // — map is excluded: GrobType has no NullableMap variant, so a nil map receiver
    // cannot be constructed from source today; a separate, pre-existing gap, not
    // part of this fix).
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalCall_NilArrayReceiver_ShortCircuitsToNil() {
        string stdout = Run(
            "xs: int[]? := nil\n" +
            "print(xs?.first())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void OptionalCall_NilStringReceiver_ShortCircuitsToNil() {
        string stdout = Run(
            "s: string? := nil\n" +
            "print(s?.upper())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void OptionalCall_NilNumericReceiver_ShortCircuitsToNil() {
        string stdout = Run(
            "n: int? := nil\n" +
            "print(n?.toString())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void OptionalCall_NilNominalReceiver_ShortCircuitsToNil() {
        string stdout = Run(
            "d: date? := nil\n" +
            "print(d?.addDays(1))\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Regression guards
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalPropertyAccess_NilArrayReceiver_StillShortCircuits() {
        // The pre-existing, already-correct property path this fix must not regress.
        string stdout = Run(
            "xs: int[]? := nil\n" +
            "print(xs?.length)\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void OptionalCall_NonNilReceiver_DispatchesNormally() {
        string stdout = Run(
            "xs: int[]? := [10, 20, 30]\n" +
            "print(xs?.first())\n");

        Assert.Equal($"10{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Argument evaluation — load-bearing: must not evaluate on the nil path.
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalCall_NilReceiver_DoesNotEvaluateArguments() {
        string stdout = Run(
            "fn sideEffect(): int {\n" +
            "    print(\"SIDE EFFECT RAN\")\n" +
            "    return 1\n" +
            "}\n" +
            "xs: int[]? := nil\n" +
            "print(xs?.contains(sideEffect()))\n");

        Assert.Equal($"nil{NL}", stdout);
        Assert.DoesNotContain("SIDE EFFECT RAN", stdout);
    }

    [Fact]
    public void OptionalCall_NonNilReceiver_DoesEvaluateArguments() {
        // Contrast case: proves the guard above suppresses evaluation because the
        // receiver is nil, not because arguments are never evaluated at all.
        string stdout = Run(
            "fn sideEffect(): int {\n" +
            "    print(\"SIDE EFFECT RAN\")\n" +
            "    return 1\n" +
            "}\n" +
            "xs: int[]? := [1, 2, 3]\n" +
            "print(xs?.contains(sideEffect()))\n");

        Assert.Contains("SIDE EFFECT RAN", stdout);
    }

    // -----------------------------------------------------------------------
    // Chained short-circuit — the whole chain skips from the first nil, once.
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalCall_ChainedOnNilReceiver_ShortCircuitsWholeChain() {
        string stdout = Run(
            "a: string[]? := nil\n" +
            "print(a?.first()?.upper())\n");

        Assert.Equal($"nil{NL}", stdout);
    }

    [Fact]
    public void OptionalCall_ChainedOnNonNilReceiver_EvaluatesEachLink() {
        // Array-of-array (not string) deliberately: GetProperty dispatches array/map/
        // struct receivers by their actual runtime kind regardless of static type, but
        // has no arm for a raw String receiver — a real, separate, pre-existing gap
        // (confirmed: `s?.upper()` on a *non-nil* `string?` already crashes today with
        // "opcode GetProperty 'upper' on receiver of kind String not yet implemented",
        // independent of this fix). Reported, not fixed here — out of this brief's scope.
        string stdout = Run(
            "a: int[][]? := [[1, 2, 3]]\n" +
            "print(a?.first()?.first())\n");

        Assert.Equal($"1{NL}", stdout);
    }

    // -----------------------------------------------------------------------
    // Interaction with '??'
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalCall_NilReceiver_NilCoalesceUnwrapsToFallback() {
        string stdout = Run(
            "xs: int[]? := nil\n" +
            "print(xs?.first() ?? 0)\n");

        Assert.Equal($"0{NL}", stdout);
    }
}
