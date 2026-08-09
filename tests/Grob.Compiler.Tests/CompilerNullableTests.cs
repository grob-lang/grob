using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-assertion tests for Sprint 3 Increment D — nullable compilation.
/// Covers eager <c>??</c> (no jump) and <c>?.</c> short-circuit
/// (IsNil + forward-jump backpatch).
/// </summary>
public sealed class CompilerNullableTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors, $"TypeChecker produced errors: {string.Join("; ", bag.Errors)}");
        return GrobCompiler.Compile(unit, bag);
    }

    /// <summary>
    /// Reads all opcodes from <paramref name="chunk"/> up to and including the
    /// first <see cref="OpCode.Return"/>, advancing correctly past variable-length
    /// operands.
    /// </summary>
    private static List<OpCode> ReadOpcodes(Chunk chunk) {
        var result = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            result.Add(op);
            offset += InstructionSize(chunk, offset);
            if (op == OpCode.Return) break;
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // ?? (NilCoalesce) — eager: both operands compiled, then NilCoalesce opcode.
    // The ABSENCE of any Jump opcodes proves eager evaluation (D-271).
    // -----------------------------------------------------------------------

    [Fact]
    public void NilCoalesce_Eager_EmitsNoJumps() {
        // nil ?? 42  — no jump between the two operands
        Chunk chunk = CompileSource("nil ?? 42");

        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.DoesNotContain(OpCode.Jump, ops);
        Assert.DoesNotContain(OpCode.JumpIfFalse, ops);
        Assert.DoesNotContain(OpCode.JumpIfTrue, ops);
    }

    [Fact]
    public void NilCoalesce_Eager_EmitsNilCoalesceOpcode() {
        Chunk chunk = CompileSource("nil ?? 42");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.NilCoalesce, ops);
    }

    [Fact]
    public void NilCoalesce_LeftOperandCompiledFirst() {
        // The left operand (nil) is pushed before the right (42).
        Chunk chunk = CompileSource("nil ?? 42");

        List<OpCode> ops = ReadOpcodes(chunk);
        int nilIdx = ops.IndexOf(OpCode.Nil);
        int coalIdx = ops.IndexOf(OpCode.NilCoalesce);
        Assert.True(nilIdx < coalIdx, "Nil must appear before NilCoalesce");
    }

    [Fact]
    public void NilCoalesce_NullableVar_CompilesFully() {
        // x: int? := nil; x ?? 0
        Chunk chunk = CompileSource("""
            x: int? := nil
            x ?? 0
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.NilCoalesce, ops);
        Assert.DoesNotContain(OpCode.JumpIfTrue, ops);
        Assert.DoesNotContain(OpCode.JumpIfFalse, ops);
        Assert.DoesNotContain(OpCode.Jump, ops);
    }

    // -----------------------------------------------------------------------
    // ?. optional chaining — IsNil + JumpIfTrue + Pop + GetProperty + Jump + Pop
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalDot_EmitsIsNilAndJumps() {
        // D-403: the receiver moved from 'int? := nil' with a placeholder '.member' name
        // onto 'int[]? := nil' with the real array property '.length' (Sprint 9
        // Increment C0a-1, D-371). D-403's type-checker fix makes '?.' property access
        // genuinely dispatch against the receiver's underlying type instead of staying
        // permissively Unknown — and 'int' (via PrimitiveMemberRegistry) has zero bare
        // properties, so ANY property name on a nullable int now correctly raises E1002,
        // which CompileSource's own bag.HasErrors assertion turns into a test failure
        // before the bytecode-shape assertions below ever run. This file only ever
        // needed a real nullable receiver with a real bare property to prove the
        // emission shape (IsNil/JumpIfTrue/Pop/GetProperty/Jump/Pop) — the receiver
        // kind and property name were never load-bearing to that shape.
        Chunk chunk = CompileSource("""
            xs: int[]? := nil
            xs?.length
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.IsNil, ops);
        Assert.Contains(OpCode.JumpIfTrue, ops);
        Assert.Contains(OpCode.Jump, ops);
        Assert.Contains(OpCode.GetProperty, ops);
    }

    [Fact]
    public void OptionalDot_EmitsTwoPopOpcodes_ForBoolCleanup() {
        // Each path (nil/non-nil) needs one Pop to discard the IsNil bool.
        // D-403: see OptionalDot_EmitsIsNilAndJumps for why the receiver moved off
        // 'int?'/'.member' onto a real nullable-array property.
        Chunk chunk = CompileSource("""
            xs: int[]? := nil
            xs?.length
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        int popCount = ops.Count(op => op == OpCode.Pop);
        // Two Pops: one on the non-nil path (pop false bool before GetProperty),
        // one on the nil path (pop true bool, leaving nil receiver as result).
        // Plus one Pop after the expression-statement discards the result.
        Assert.True(popCount >= 2, $"Expected >= 2 Pops but got {popCount}");
    }

    [Fact]
    public void OptionalDot_StructureOrder_IsNilBeforeJumpIfTrue() {
        // D-403: see OptionalDot_EmitsIsNilAndJumps for why the receiver moved off
        // 'int?'/'.member' onto a real nullable-array property.
        Chunk chunk = CompileSource("""
            xs: int[]? := nil
            xs?.length
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        int isNilIdx = ops.IndexOf(OpCode.IsNil);
        int jumpIfTrueIdx = ops.IndexOf(OpCode.JumpIfTrue);
        int getPropIdx = ops.IndexOf(OpCode.GetProperty);

        Assert.True(isNilIdx < jumpIfTrueIdx,
            "IsNil must appear before JumpIfTrue");
        Assert.True(jumpIfTrueIdx < getPropIdx,
            "JumpIfTrue must appear before GetProperty");
    }

    [Fact]
    public void PlainDot_DoesNotEmitIsNilOrJumps() {
        // Non-nullable '.' access emits only GetProperty, no nil-guard machinery.
        // Receiver history: originally 'int' (Sprint 3 Increment D); moved onto an array
        // receiver when Sprint 9 Increment A1a (D-369) registered int as a
        // primitive-member receiver, so 'int.member' legitimately raises E1002 for an
        // unknown property; moved onto a map receiver when Sprint 9 Increment C0a-1
        // (D-371) registered array's own length/isEmpty properties, making
        // 'array.member' legitimately raise E1002 too; moved a third time here, onto a
        // function-typed receiver, when Sprint 9 Increment C0b-2a (D-377) registered
        // map's own query members, making 'map.member' legitimately raise E1002 too.
        // Confirmed by direct read of every GrobType arm in VisitMemberAccess (D-377):
        // 'fn(...): R' is the sole remaining receiver kind reaching this fall-through,
        // and — unlike int/array/map, each "not yet built" — it is terminal:
        // grob-type-registry.md states permanently "Members. None. A function type has
        // no properties, no methods and no constructor syntax," so no future increment
        // will register function members and force a fourth move.
        Chunk chunk = CompileSource("f: fn(): int := () => 1\nf.member");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.IsNil, ops);
        Assert.DoesNotContain(OpCode.JumpIfTrue, ops);
    }

    // -----------------------------------------------------------------------
    // EmitJump / PatchJump — the helpers are internal so test via observable
    // bytecode structure: after a JumpIfTrue the offset must land exactly at
    // the nil-path Pop (not inside an operand or past the end of the chunk).
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalDot_JumpIfTrueOffset_LandsAtNilPathPop() {
        // Compile a minimal ?. expression and verify the backpatch is correct:
        // the JumpIfTrue must skip exactly over [Pop, GetProperty, byte, Jump, byte, byte]
        // and land at the nil-path Pop.
        // D-403: see OptionalDot_EmitsIsNilAndJumps for why the receiver moved off
        // 'int?'/'.member' onto a real nullable-array property.
        Chunk chunk = CompileSource("""
            xs: int[]? := nil
            xs?.length
            """);

        // Walk the chunk to find the JumpIfTrue opcode and its offset.
        int offset = 0;
        int jumpIfTrueOffset = -1;
        int jumpOffset16 = -1;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            if (op == OpCode.JumpIfTrue) {
                jumpIfTrueOffset = offset;
                int hi = chunk.ReadByte(offset + 1);
                int lo = chunk.ReadByte(offset + 2);
                jumpOffset16 = (hi << 8) | lo;
                break;
            }
            offset += InstructionSize(chunk, offset);
        }

        Assert.True(jumpIfTrueOffset >= 0, "JumpIfTrue not found");

        // The jump target byte is: jumpIfTrueOffset + 3 (opcode + 2 bytes) + jumpOffset16.
        int jumpTarget = jumpIfTrueOffset + 3 + jumpOffset16;

        // The target must be a Pop opcode (the nil-path bool cleanup).
        Assert.True(jumpTarget < chunk.Count,
            $"Jump target {jumpTarget} is past chunk end {chunk.Count}");
        Assert.Equal((byte)OpCode.Pop, chunk.ReadByte(jumpTarget));
    }

    /// <summary>Returns the total byte size of the instruction at <paramref name="offset"/>.</summary>
    private static int InstructionSize(Chunk chunk, int offset) {
        var op = (OpCode)chunk.ReadByte(offset);
        return 1 + op switch {
            OpCode.Constant => 1,
            OpCode.ConstantLong => 2,
            OpCode.GetGlobal
                or OpCode.SetGlobal
                or OpCode.DefineGlobal
                or OpCode.GetLocal
                or OpCode.SetLocal => 1,
            OpCode.GetProperty
                or OpCode.SetProperty => 1,
            OpCode.Jump
                or OpCode.JumpIfFalse
                or OpCode.JumpIfTrue => 2,
            _ => 0,
        };
    }
}
