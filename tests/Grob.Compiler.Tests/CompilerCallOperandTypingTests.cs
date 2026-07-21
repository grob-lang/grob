using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape regression locks for the <c>GetExprType</c> arithmetic-operand
/// completeness sweep (D-362, closing D-360's <c>CallExpr</c> residue). Before this
/// increment, <c>GetExprType</c> only resolved a call's return type through a bare
/// identifier bound to a user <c>FnDecl</c> — every other call shape silently fell to
/// <see cref="GrobType.Unknown"/>, which <c>EmitArithmetic</c> defaults to
/// <see cref="GrobType.Int"/>. A native/stdlib call or a function-typed-variable call
/// that returns <c>float</c> and is used inline as an arithmetic operand therefore
/// picked <see cref="OpCode.AddInt"/> instead of <see cref="OpCode.AddFloat"/> and
/// skipped the int/float promotion — silently wrong, mirroring D-359's
/// <c>floatArr[i] + 1.0</c> class of bug.
/// </summary>
public sealed class CompilerCallOperandTypingTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors, $"TypeChecker produced errors: {string.Join("; ", bag.Errors)}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors, $"Compiler produced errors: {string.Join("; ", bag.Errors)}");
        return chunk;
    }

    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            result.Add(op);
            offset++;
            switch (op) {
                case OpCode.Constant:
                    offset += 1;
                    break;
                case OpCode.ConstantLong:
                    offset += 2;
                    break;
                default:
                    break;
            }
            if (op == OpCode.Return) break;
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Sub-case 1: native/stdlib call as an arithmetic operand.
    // -----------------------------------------------------------------------

    [Fact]
    public void NativeCallAsOperand_FloatReturningSqrt_SelectsAddFloatNotAddInt() {
        Chunk chunk = CompileSource("x := math.sqrt(4.0) + 1.0\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.AddFloat, ops);
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    // -----------------------------------------------------------------------
    // Sub-case 2: function-typed-variable call as an arithmetic operand.
    // -----------------------------------------------------------------------

    [Fact]
    public void FunctionTypedVariableCallAsOperand_FloatReturn_SelectsAddFloatNotAddInt() {
        Chunk chunk = CompileSource(
            "f: fn(): float := () => 2.5\n" +
            "x := f() + 1.0\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.AddFloat, ops);
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    // -----------------------------------------------------------------------
    // Residue — all three documented permissive-Unknown sources still compile
    // under the Int assumption, not a compiler defect. None of these picks up
    // AddFloat (there is no float type information to recover), but none of
    // them crashes the compiler either.
    // -----------------------------------------------------------------------

    [Fact]
    public void VoidReturningCallAsOperand_ArrayEach_StillCompilesUnderIntAssumption() {
        Chunk chunk = CompileSource("arr := [1, 2, 3]\nx := arr.each((v) => v) + 1\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.AddInt, ops);
    }

    [Fact]
    public void MapElementAsOperand_StillCompilesUnderIntAssumption() {
        Chunk chunk = CompileSource("m := env.all()\nx := m[\"PATH\"] + 1\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.AddInt, ops);
    }

    // -----------------------------------------------------------------------
    // Sub-case 4: string-returning built-in call as a '+' operand (CodeRabbit
    // PR #152). input() resolves to String at type-check but reached GetExprType
    // as Unknown before the fix, so `input() + "x"` fell through the String-concat
    // guard and mis-selected AddInt (an AddInt on a string operand — a runtime
    // type fault). With ResolvedReturnType persisted, both operands read String
    // and the compiler selects Concat.
    // -----------------------------------------------------------------------

    [Fact]
    public void StringReturningCallAsOperand_InputConcat_SelectsConcatNotAddInt() {
        Chunk chunk = CompileSource("x := input() + \"a\"\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.Concat, ops);
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    [Fact]
    public void UnknownReceiverFieldAsOperand_UntypedLambdaParameter_StillCompilesUnderIntAssumption() {
        // The AddInt this residue selects is emitted into the lambda's own nested chunk
        // (compiled as a Closure constant), not the outer chunk ReadOpcodes decodes — the
        // assertion that matters here is that CompileSource (which asserts no TypeChecker
        // or Compiler diagnostics) succeeds without the defensive EmitArithmetic guard
        // firing, i.e. this permissive shape is not mistaken for the struct/lambda-operand
        // compiler-defect case.
        Chunk chunk = CompileSource("arr := [1, 2, 3]\nx := arr.select((v) => v.count + 1)\n");
        Assert.True(chunk.Count > 0);
    }
}
