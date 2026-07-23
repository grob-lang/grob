using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 9 Increment B's date-vs-date comparison support
/// (D-354): the compiler's opcode-selection default for any non-Float/non-String
/// category is <c>Int</c> (<see cref="Compiler.ComparisonCategory"/>), so a
/// <c>Struct</c>-typed date comparison reaching the ordinary path unchanged would
/// silently emit <c>LessInt</c>/<c>GreaterInt</c> against two struct receivers — these
/// tests pin the exact, complete instruction sequence (opcodes, operand bytes, the
/// constant pool and the source-line array), not merely opcode presence, so a
/// wrong-opcode or wrong-operand regression cannot hide behind a presence check
/// (CodeRabbit review, PR #143 — Grob.Compiler.Tests's own convention is to assert
/// bytecode exactly, per <c>tests/CLAUDE.md</c>).
/// </summary>
public sealed class CompilerDateComparisonTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    private const string TwoDates = """
        a := date.now()
        b := date.now()
        """;

    // Shared prologue every test source below compiles: emitted identically regardless
    // of the trailing comparison operator, so its shape is asserted once here and each
    // test asserts only the comparison tail — instruction offsets after the prologue.
    private static void AssertTwoDateBindingsPrologue(Chunk chunk) {
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(0));
        Assert.Equal(0, chunk.ReadByte(1));
        Assert.Equal(OpCode.Call, (OpCode)chunk.ReadByte(2));
        Assert.Equal(0, chunk.ReadByte(3));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(4));
        Assert.Equal(1, chunk.ReadByte(5));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(6));
        Assert.Equal(2, chunk.ReadByte(7));
        Assert.Equal(OpCode.Call, (OpCode)chunk.ReadByte(8));
        Assert.Equal(0, chunk.ReadByte(9));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(10));
        Assert.Equal(3, chunk.ReadByte(11));

        for (int offset = 0; offset <= 11; offset++) {
            Assert.Equal(1 + offset / 6, chunk.GetLine(offset)); // offsets 0-5 -> line 1, 6-11 -> line 2
        }

        Assert.Equal(5, chunk.ConstantCount);
        Assert.Equal("date.now", chunk.ReadConstant(0).AsString());
        Assert.Equal("a", chunk.ReadConstant(1).AsString());
        Assert.Equal("date.now", chunk.ReadConstant(2).AsString());
        Assert.Equal("b", chunk.ReadConstant(3).AsString());
        Assert.Equal("x", chunk.ReadConstant(4).AsString());
    }

    [Fact]
    public void DateLess_EmitsExactGetGlobalGetGlobalLessDateSequence() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a < b");
        AssertTwoDateBindingsPrologue(chunk);

        // 12: GetGlobal 1 (a), 14: GetGlobal 3 (b), 16: LessDate, 17: DefineGlobal 4 (x), 19: Return.
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(12));
        Assert.Equal(1, chunk.ReadByte(13));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(14));
        Assert.Equal(3, chunk.ReadByte(15));
        Assert.Equal(OpCode.LessDate, (OpCode)chunk.ReadByte(16));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(17));
        Assert.Equal(4, chunk.ReadByte(18));
        Assert.Equal(OpCode.Return, (OpCode)chunk.ReadByte(19));
        Assert.Equal(20, chunk.Count);

        for (int offset = 12; offset < chunk.Count; offset++) {
            Assert.Equal(3, chunk.GetLine(offset));
        }
    }

    [Fact]
    public void DateGreater_EmitsExactGetGlobalGetGlobalGreaterDateSequence() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a > b");
        AssertTwoDateBindingsPrologue(chunk);

        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(12));
        Assert.Equal(1, chunk.ReadByte(13));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(14));
        Assert.Equal(3, chunk.ReadByte(15));
        Assert.Equal(OpCode.GreaterDate, (OpCode)chunk.ReadByte(16));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(17));
        Assert.Equal(4, chunk.ReadByte(18));
        Assert.Equal(OpCode.Return, (OpCode)chunk.ReadByte(19));
        Assert.Equal(20, chunk.Count);
    }

    [Fact]
    public void DateLessEqual_EmitsExactGreaterDateThenNotSequence_NeverLessEqualInt() {
        // The closed enum has no LessEqualDate — <= lowers to the strict > plus Not,
        // mirroring the pre-existing string lowering. Asserting the exact sequence
        // (not just "GreaterDate appears somewhere") pins that Not is the very next
        // instruction, with no LessEqualInt/LessEqualFloat sneaking in instead.
        Chunk chunk = CompileSource(TwoDates + "\nx := a <= b");
        AssertTwoDateBindingsPrologue(chunk);

        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(12));
        Assert.Equal(1, chunk.ReadByte(13));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(14));
        Assert.Equal(3, chunk.ReadByte(15));
        Assert.Equal(OpCode.GreaterDate, (OpCode)chunk.ReadByte(16));
        Assert.Equal(OpCode.Not, (OpCode)chunk.ReadByte(17));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(18));
        Assert.Equal(4, chunk.ReadByte(19));
        Assert.Equal(OpCode.Return, (OpCode)chunk.ReadByte(20));
        Assert.Equal(21, chunk.Count);
    }

    [Fact]
    public void DateGreaterEqual_EmitsExactLessDateThenNotSequence_NeverGreaterEqualInt() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a >= b");
        AssertTwoDateBindingsPrologue(chunk);

        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(12));
        Assert.Equal(1, chunk.ReadByte(13));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(14));
        Assert.Equal(3, chunk.ReadByte(15));
        Assert.Equal(OpCode.LessDate, (OpCode)chunk.ReadByte(16));
        Assert.Equal(OpCode.Not, (OpCode)chunk.ReadByte(17));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(18));
        Assert.Equal(4, chunk.ReadByte(19));
        Assert.Equal(OpCode.Return, (OpCode)chunk.ReadByte(20));
        Assert.Equal(21, chunk.Count);
    }

    // -----------------------------------------------------------------------
    // date-vs-date equality (D-357/D-367) — EqualDate, never the generic Equal;
    // != lowers to EqualDate + Not, mirroring the <=/>= lowering above.
    // -----------------------------------------------------------------------

    [Fact]
    public void DateEqual_EmitsExactGetGlobalGetGlobalEqualDateSequence_NeverEqual() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a == b");
        AssertTwoDateBindingsPrologue(chunk);

        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(12));
        Assert.Equal(1, chunk.ReadByte(13));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(14));
        Assert.Equal(3, chunk.ReadByte(15));
        Assert.Equal(OpCode.EqualDate, (OpCode)chunk.ReadByte(16));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(17));
        Assert.Equal(4, chunk.ReadByte(18));
        Assert.Equal(OpCode.Return, (OpCode)chunk.ReadByte(19));
        Assert.Equal(20, chunk.Count);
    }

    [Fact]
    public void DateNotEqual_EmitsExactEqualDateThenNotSequence_NeverNotEqual() {
        Chunk chunk = CompileSource(TwoDates + "\nx := a != b");
        AssertTwoDateBindingsPrologue(chunk);

        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(12));
        Assert.Equal(1, chunk.ReadByte(13));
        Assert.Equal(OpCode.GetGlobal, (OpCode)chunk.ReadByte(14));
        Assert.Equal(3, chunk.ReadByte(15));
        Assert.Equal(OpCode.EqualDate, (OpCode)chunk.ReadByte(16));
        Assert.Equal(OpCode.Not, (OpCode)chunk.ReadByte(17));
        Assert.Equal(OpCode.DefineGlobal, (OpCode)chunk.ReadByte(18));
        Assert.Equal(4, chunk.ReadByte(19));
        Assert.Equal(OpCode.Return, (OpCode)chunk.ReadByte(20));
        Assert.Equal(21, chunk.Count);
    }

    [Fact]
    public void GuidEqual_StillEmitsGenericEqual_UnaffectedByDateCarveOut() {
        // D-357 is date-scoped: a nominal guid == guid pair must keep emitting the
        // generic Equal opcode, not EqualDate — confirmed by using guid.parse's own
        // Struct-typed result rather than date's.
        Chunk chunk = CompileSource("""
            a := guid.parse("11111111-1111-1111-1111-111111111111")
            b := guid.parse("11111111-1111-1111-1111-111111111111")
            x := a == b
            """);

        Assert.Contains(OpCode.Equal, EmittedOpCodes(chunk));
        Assert.DoesNotContain(OpCode.EqualDate, EmittedOpCodes(chunk));
    }

    [Fact]
    public void UnrelatedStructEqual_StillEmitsGenericEqual_UnaffectedByDateCarveOut() {
        Chunk chunk = CompileSource("""
            type Config {
                host: string
            }
            a := Config { host: "x" }
            b := Config { host: "y" }
            c := a == b
            """);

        Assert.Contains(OpCode.Equal, EmittedOpCodes(chunk));
        Assert.DoesNotContain(OpCode.EqualDate, EmittedOpCodes(chunk));
    }

    // Walks the instruction stream skipping each opcode's real 1-byte operand width —
    // Grob.Compiler.Tests has no dependency on Grob.Vm's Disassembler (the DAG: a test
    // project references exactly one production project), so this is a small local
    // table covering only the opcodes these two equality fixtures can emit. The other
    // tests in this file assert exact byte-for-byte sequences; this one only needs
    // "which opcode landed", not "at which offset".
    private static IReadOnlyList<OpCode> EmittedOpCodes(Chunk chunk) {
        var opCodes = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var opCode = (OpCode)chunk.ReadByte(offset);
            opCodes.Add(opCode);
            offset += opCode switch {
                OpCode.GetGlobal or OpCode.SetGlobal or OpCode.DefineGlobal
                    or OpCode.GetLocal or OpCode.SetLocal
                    or OpCode.Constant or OpCode.Call or OpCode.NewStruct => 2,
                _ => 1,
            };
        }
        return opCodes;
    }
}
