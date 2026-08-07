using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for the D-380/D-400 fix: a method call whose callee is an
/// optional member access (<c>xs?.first()</c>) must short-circuit on a nil receiver
/// the same way <c>xs?.length</c> (plain optional property access) already does —
/// <see cref="OpCode.IsNil"/> / <see cref="OpCode.JumpIfTrue"/> guarding the argument
/// evaluation and the <see cref="OpCode.Call"/>, not just the receiver's own
/// <see cref="OpCode.GetProperty"/>. Regression: see D-380 (crash finding) and D-400
/// (the fix).
/// </summary>
public sealed class CompilerOptionalChainCallTests {
    private static Chunk CompileSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    private readonly record struct Instr(int Offset, OpCode Op, int Arg);

    private static List<Instr> Decode(Chunk chunk) {
        var result = new List<Instr>();
        int offset = 0;
        while (offset < chunk.Count) {
            int here = offset;
            var op = (OpCode)chunk.ReadByte(offset++);
            int arg = 0;
            switch (op) {
                case OpCode.ConstantLong:
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    arg = (chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1);
                    offset += 2;
                    break;
                case OpCode.Constant:
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.PopN:
                case OpCode.IncrementInt:
                case OpCode.DecrementInt:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                case OpCode.NewArray:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.GetUpvalue:
                case OpCode.SetUpvalue:
                case OpCode.NewAnonStruct:
                case OpCode.NewStruct:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                case OpCode.Closure:
                    arg = chunk.ReadByte(offset++);
                    if (chunk.ReadConstant(arg).TryAsFunction(out GrobFunction? gf) &&
                        gf is BytecodeFunction closureFn) {
                        offset += closureFn.UpvalueCount * 2;
                    }
                    break;
                default:
                    break;
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // The guard shape — xs?.first() must be wrapped exactly like xs?.length
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalChainCall_NilReceiver_EmitsSecondNilGuardAroundCall() {
        Chunk chunk = CompileSource("""
            xs: int[]? := nil
            print(xs?.first())
            """);

        List<Instr> instrs = Decode(chunk);

        // One IsNil/JumpIfTrue pair already guards xs?.first()'s own property/
        // method-bind resolution (VisitMemberAccess's pre-existing guard, unchanged
        // by this fix). A *second* pair must now guard the Call itself — without it,
        // a nil xs falls through to an unconditional Call and crashes.
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.IsNil));
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.JumpIfTrue));

        int lastJumpIfTrueIdx = instrs.FindLastIndex(i => i.Op == OpCode.JumpIfTrue);
        int callIdx = instrs.FindIndex(lastJumpIfTrueIdx, i => i.Op == OpCode.Call);
        Assert.True(callIdx > lastJumpIfTrueIdx, "Call must be reachable only past the second (call-site) guard");

        // The guard converges via a Jump that lands after a trailing Pop — mirroring
        // VisitMemberAccess's IsNil/JumpIfTrue/Pop/<op>/Jump/Pop/PatchJump shape.
        int jumpIdx = instrs.FindIndex(callIdx, i => i.Op == OpCode.Jump);
        Assert.True(jumpIdx > callIdx, "expected a Jump immediately after Call to skip the nil-cleanup Pop");

        int popAfterJumpIdx = instrs.FindIndex(jumpIdx, i => i.Op == OpCode.Pop);
        Assert.True(popAfterJumpIdx > jumpIdx, "expected a Pop on the nil-cleanup path (pops the JumpIfTrue's true bool)");
    }

    [Fact]
    public void OptionalChainCall_NonOptionalCall_EmitsNoGuard() {
        // Regression guard: the fix is additive — xs.first() (no '?.') on a
        // non-nullable array keeps its old unguarded shape.
        Chunk chunk = CompileSource("""
            xs: int[] := [1, 2, 3]
            print(xs.first())
            """);

        List<Instr> instrs = Decode(chunk);
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.IsNil);
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.JumpIfTrue);
        Assert.Contains(instrs, i => i.Op == OpCode.Call);
    }

    // -----------------------------------------------------------------------
    // Argument evaluation — load-bearing: must only happen on the non-nil path
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalChainCall_ArgumentEvaluation_OnlyEmittedAfterNilGuardPop() {
        Chunk chunk = CompileSource("""
            fn sideEffect(): int {
                return 1
            }
            xs: int[]? := nil
            print(xs?.contains(sideEffect()))
            """);

        List<Instr> instrs = Decode(chunk);

        // Same fix-detecting shape as the previous test: a second JumpIfTrue must
        // exist to guard the Call/argument-evaluation site, distinct from the
        // pre-existing property/method-bind guard.
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.JumpIfTrue));

        int lastJumpIfTrueIdx = instrs.FindLastIndex(i => i.Op == OpCode.JumpIfTrue);

        // The call-site guard's false-branch Pop (popping JumpIfTrue's peeked bool)
        // must appear before the sideEffect call's own GetGlobal/Call pair — i.e.
        // sideEffect() is only ever reached inside the guarded, non-nil branch.
        int guardPopIdx = instrs.FindIndex(lastJumpIfTrueIdx, i => i.Op == OpCode.Pop);
        Assert.True(guardPopIdx > lastJumpIfTrueIdx, "expected the false-branch Pop right after the call-site JumpIfTrue");

        int sideEffectGetGlobalIdx = instrs.FindIndex(
            i => i.Op == OpCode.GetGlobal &&
                 chunk.ReadConstant(i.Arg).IsString &&
                 chunk.ReadConstant(i.Arg).AsString() == "sideEffect");
        Assert.True(sideEffectGetGlobalIdx > guardPopIdx,
            "sideEffect() must only be emitted after the call-site guard's false-branch Pop");
    }
}
