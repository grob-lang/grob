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

    private readonly record struct Instr(int Offset, OpCode Op, int Arg, int Line);

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
            result.Add(new Instr(here, op, arg, chunk.GetLine(here)));
        }
        return result;
    }

    private static string[] StringConstants(Chunk chunk) {
        var result = new string[chunk.ConstantCount];
        for (int i = 0; i < chunk.ConstantCount; i++) {
            GrobValue value = chunk.ReadConstant(i);
            result[i] = value.IsString ? value.AsString() : value.ToString();
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // The guard shape — xs?.first() must be wrapped exactly like xs?.length.
    //
    // Asserted as the complete Chunk contract (CodeRabbit, PR #184): every
    // instruction's offset, opcode, decoded operand and source line, plus the
    // whole constant pool. A count-only assertion cannot catch a mis-patched
    // jump operand, a wrong Call arity or a line-number regression; this can.
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalChainCall_EmitsExactGuardedBytecode() {
        Chunk chunk = CompileSource("""
            xs: int[]? := nil
            print(xs?.first())
            """);

        Assert.Equal(["xs", "first"], StringConstants(chunk));

        // Two guards, both the property path's proven IsNil/JumpIfTrue/Pop/<op>/
        // Jump/Pop shape. The first (offsets 5-15) is VisitMemberAccess's
        // pre-existing property/method-bind guard, unchanged by this fix. The
        // second (offsets 16-26) is D-400's new call-site guard: without it a nil
        // xs falls through to an unconditional Call and crashes the VM.
        Instr[] expected = [
            new(0, OpCode.Nil, 0, 1),           // xs := nil
            new(1, OpCode.DefineGlobal, 0, 1),  // -> "xs"
            new(3, OpCode.GetGlobal, 0, 2),     // receiver

            new(5, OpCode.IsNil, 0, 2),         // guard 1: property path
            new(6, OpCode.JumpIfTrue, 6, 2),    // -> offset 15 (nil cleanup Pop)
            new(9, OpCode.Pop, 0, 2),           // pop false
            new(10, OpCode.GetProperty, 1, 2),  // -> "first"
            new(12, OpCode.Jump, 1, 2),         // -> offset 16 (skip nil cleanup)
            new(15, OpCode.Pop, 0, 2),          // pop true; nil stays as result

            new(16, OpCode.IsNil, 0, 2),        // guard 2: D-400 call site
            new(17, OpCode.JumpIfTrue, 6, 2),   // -> offset 26 (nil cleanup Pop)
            new(20, OpCode.Pop, 0, 2),          // pop false
            new(21, OpCode.Call, 0, 2),         // 0 arguments
            new(23, OpCode.Jump, 1, 2),         // -> offset 27 (skip nil cleanup)
            new(26, OpCode.Pop, 0, 2),          // pop true; nil stays as result

            new(27, OpCode.Print, 0, 2),
            new(28, OpCode.Return, 0, 2),  // implicit trailing return, last source line
        ];

        List<Instr> actual = Decode(chunk);
        Assert.Equal(expected, actual);

        // Jump operands are encoded relative to the instruction *after* the
        // 2-byte operand, so pin the resolved absolute targets as well — that is
        // what a mis-patched PatchJump actually corrupts, and it is the assertion
        // the operand literals above cannot make on their own.
        Assert.Equal(
            [
                (OpCode.JumpIfTrue, 15),  // guard 1 -> its nil-cleanup Pop
                (OpCode.Jump, 16),        // guard 1 -> guard 2's IsNil
                (OpCode.JumpIfTrue, 26),  // guard 2 -> its nil-cleanup Pop
                (OpCode.Jump, 27),        // guard 2 -> Print
            ],
            ResolveJumpTargets(actual));
    }

    /// <summary>
    /// Resolves each forward jump to the absolute offset it lands on, and asserts
    /// that offset is the start of a real decoded instruction (never mid-operand).
    /// </summary>
    private static List<(OpCode Op, int Target)> ResolveJumpTargets(List<Instr> instrs) {
        var starts = instrs.Select(i => i.Offset).ToHashSet();
        var result = new List<(OpCode, int)>();
        foreach (Instr instr in instrs) {
            if (instr.Op is not (OpCode.Jump or OpCode.JumpIfTrue or OpCode.JumpIfFalse)) continue;
            int target = instr.Offset + 3 + instr.Arg;
            Assert.Contains(target, starts);
            result.Add((instr.Op, target));
        }
        return result;
    }

    [Fact]
    public void NonOptionalCall_EmitsNoNilGuard() {
        // Regression guard: the fix is additive — xs.first() (no '?.') on a
        // non-nullable array keeps its old unguarded shape.
        Chunk chunk = CompileSource("""
            xs: int[] := [1, 2, 3]
            print(xs.first())
            """);

        List<Instr> instrs = Decode(chunk);
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.IsNil);
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.JumpIfTrue);
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.Jump);

        Instr call = Assert.Single(instrs, i => i.Op == OpCode.Call);
        Assert.Equal(0, call.Arg);
        Assert.Equal(2, call.Line);
    }

    // -----------------------------------------------------------------------
    // Argument evaluation — load-bearing: must only happen on the non-nil path.
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalChainCallArguments_EmitOnlyOnNonNilBranch() {
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
        Assert.Equal(lastJumpIfTrueIdx + 1, guardPopIdx);

        int sideEffectGetGlobalIdx = instrs.FindIndex(
            i => i.Op == OpCode.GetGlobal &&
                 chunk.ReadConstant(i.Arg).IsString &&
                 chunk.ReadConstant(i.Arg).AsString() == "sideEffect");
        Assert.True(sideEffectGetGlobalIdx > guardPopIdx,
            "sideEffect() must only be emitted after the call-site guard's false-branch Pop");

        // Two Calls emit: sideEffect()'s own (0 args, inside the guarded branch)
        // then the outer optional contains() (1 arg). Pin both arities — a wrong
        // Call operand is exactly what a count-only assertion cannot see.
        Assert.Equal([0, 1], instrs.Where(i => i.Op == OpCode.Call).Select(i => i.Arg));
    }

    // -----------------------------------------------------------------------
    // The primitive-rewrite boundary (CodeRabbit, PR #184).
    //
    // D-400 rests on the invariant that CallExpr.ResolvedPrimitiveNativeName is
    // only ever populated for a statically NON-nullable primitive receiver:
    // PrimitiveMemberRegistry is keyed by GrobType.String/Int/Float/Bool, never
    // their Nullable* variants. A nullable primitive receiver therefore takes the
    // generic guarded tail, not EmitPrimitiveMemberCall — which is why the D-400
    // guard covers s?.upper() without a primitive-specific arm. Pin it here so a
    // future registry change that keys a Nullable* variant cannot silently route
    // a nullable receiver around the guard.
    // -----------------------------------------------------------------------

    [Fact]
    public void NullablePrimitiveOptionalCall_TakesGuardedPathNotPrimitiveRewrite() {
        Chunk chunk = CompileSource("""
            s: string? := nil
            print(s?.upper())
            """);

        // The primitive rewrite would add a "string.upper" qualified-native
        // constant and emit an unguarded GetGlobal/Call pair. It must not fire.
        Assert.Equal(["s", "upper"], StringConstants(chunk));

        List<Instr> instrs = Decode(chunk);
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.IsNil));
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.JumpIfTrue));

        // Byte-for-byte the same guarded shape the array receiver gets: the
        // emission is receiver-type-agnostic (D-400).
        Assert.Equal(
            Decode(CompileSource("""
                xs: int[]? := nil
                print(xs?.first())
                """)).Select(i => (i.Offset, i.Op, i.Arg, i.Line)),
            instrs.Select(i => (i.Offset, i.Op, i.Arg, i.Line)));
    }
}
