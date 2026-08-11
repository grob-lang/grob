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

        // PR #189 review (CodeRabbit): the receiver changed above, so this — the anchor
        // test for the whole '?.' shape — now asserts the complete emitted chunk rather
        // than probing it with Contains, per tests/CLAUDE.md ("given source text, assert
        // the exact bytecode emitted — opcodes, operands, constant-pool contents, and the
        // line-number array"). The three tests below stay narrow on purpose: each pins one
        // property of this same sequence (Pop count, ordering, backpatch arithmetic) and
        // names it, so a break points at the property that broke.
        Assert.Equal(
            [
                (OpCode.Nil, -1, 1),            // xs := nil
                (OpCode.DefineGlobal, 0, 1),    //   → constant 0, "xs"
                (OpCode.GetGlobal, 0, 2),       // xs?.length — push the receiver
                (OpCode.IsNil, -1, 2),          //   nil test, leaves receiver + bool
                (OpCode.JumpIfTrue, 6, 2),      //   nil → skip the property read (to offset 15)
                (OpCode.Pop, -1, 2),            //   non-nil path: discard the false bool
                (OpCode.GetProperty, 1, 2),     //   → constant 1, "length"
                (OpCode.Jump, 1, 2),            //   skip the nil path's own cleanup
                (OpCode.Pop, -1, 2),            //   nil path: discard the true bool
                (OpCode.Pop, -1, 2),            // expression statement discards the result
                (OpCode.Return, -1, 2),
            ],
            ReadInstructions(chunk));

        Assert.Equal(2, chunk.ConstantCount);
        Assert.Equal("xs", chunk.ReadConstant(0).AsString());
        Assert.Equal("length", chunk.ReadConstant(1).AsString());
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
        // Two Pops for the bool: one on the non-nil path (pop false before GetProperty),
        // one on the nil path (pop true, leaving the nil receiver as the result). Plus one
        // Pop for the expression statement discarding the result — three exactly.
        // PR #189 review (CodeRabbit): was 'popCount >= 2', an inequality that would have
        // stayed green if a path lost its cleanup Pop and leaked a bool onto the stack.
        Assert.Equal(3, popCount);
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

    /// <summary>
    /// Decodes <paramref name="chunk"/> into one entry per instruction — opcode, decoded
    /// operand (<c>-1</c> when the instruction takes none) and the source line the byte is
    /// attributed to — so a test can assert the emitted sequence exactly rather than probing
    /// it with <c>Contains</c>. Stops after the first <see cref="OpCode.Return"/>.
    /// </summary>
    private static List<(OpCode Op, int Operand, int Line)> ReadInstructions(Chunk chunk) {
        List<(OpCode Op, int Operand, int Line)> result = [];
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            int size = InstructionSize(chunk, offset);
            int operand = size switch {
                2 => chunk.ReadByte(offset + 1),
                3 => (chunk.ReadByte(offset + 1) << 8) | chunk.ReadByte(offset + 2),
                _ => -1,
            };
            result.Add((op, operand, chunk.GetLine(offset)));
            offset += size;
            if (op == OpCode.Return) break;
        }
        return result;
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
