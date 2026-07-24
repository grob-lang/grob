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

    // One-byte-operand opcodes: the trailing byte is a pool/slot/count index, not a
    // second opcode. Mirrors Disassembler's ByteOperandInstruction classification so
    // the decoder advances past the operand rather than mis-reading it as the next
    // instruction (CodeRabbit PR #159 — the arithmetic-selection assertions below are
    // only meaningful if the decoder stays byte-aligned across GetGlobal/Call/etc.).
    private static readonly HashSet<OpCode> OneByteOperand = [
        OpCode.Constant, OpCode.Call, OpCode.GetGlobal, OpCode.SetGlobal, OpCode.DefineGlobal,
        OpCode.GetLocal, OpCode.SetLocal, OpCode.GetUpvalue, OpCode.SetUpvalue,
        OpCode.GetProperty, OpCode.SetProperty, OpCode.PopN, OpCode.NewArray, OpCode.BuildString,
        OpCode.NewStruct, OpCode.NewAnonStruct, OpCode.Import, OpCode.TryBegin,
        OpCode.IncrementInt, OpCode.DecrementInt, OpCode.IncrementFloat, OpCode.DecrementFloat,
    ];

    // Two-byte-operand opcodes: a 16-bit pool index or jump distance.
    private static readonly HashSet<OpCode> TwoByteOperand = [
        OpCode.ConstantLong, OpCode.Jump, OpCode.JumpIfFalse, OpCode.JumpIfTrue, OpCode.Loop,
    ];

    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            result.Add(op);
            offset++;
            if (op == OpCode.Closure) {
                // Variable width: pool-index byte + UpvalueCount×2 capture descriptors.
                byte fnIdx = chunk.ReadByte(offset);
                offset += 1;
                if (chunk.ReadConstant(fnIdx).TryAsFunction(out GrobFunction? gf) && gf is BytecodeFunction fn)
                    offset += fn.UpvalueCount * 2;
            } else if (TwoByteOperand.Contains(op)) {
                offset += 2;
            } else if (OneByteOperand.Contains(op)) {
                offset += 1;
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
    // Sprint 9 Increment A1b (D-370): the int/float type-static namespace-native
    // calls need zero type-checker or compiler changes for correct operand typing —
    // ResolveNamespaceMemberCall already sets CallExpr.ResolvedReturnType generically
    // for every namespace-native call (D-362), the same mechanism math.sqrt proves.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntMaxAsOperand_PlusOne_SelectsAddIntNotAddFloat() {
        Chunk chunk = CompileSource("x := int.max(1, 2) + 1\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.AddInt, ops);
        Assert.DoesNotContain(OpCode.AddFloat, ops);
    }

    [Fact]
    public void FloatMinAsOperand_TimesTwo_SelectsMulFloatNotMulInt() {
        Chunk chunk = CompileSource("x := float.min(1.0, 2.0) * 2.0\n");
        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.MultiplyFloat, ops);
        Assert.DoesNotContain(OpCode.MultiplyInt, ops);
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
