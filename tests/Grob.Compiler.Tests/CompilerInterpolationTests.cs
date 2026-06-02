using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-assertion tests for Sprint 3 Increment E — string interpolation.
/// Covers: segment-push order, <see cref="OpCode.BuildString"/> fragment count,
/// no-slot optimisation, implicit conversion of non-string slots, and
/// <c>E0102</c> (nullable interpolation compile error). (D-279)
/// </summary>
public sealed class CompilerInterpolationTests {
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

    private static DiagnosticBag CheckSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    /// <summary>
    /// Returns total byte size of the instruction starting at <paramref name="offset"/>.
    /// Handles all 1-byte-operand opcodes including <see cref="OpCode.BuildString"/>.
    /// </summary>
    private static int InstructionSize(Chunk chunk, int offset) {
        var op = (OpCode)chunk.ReadByte(offset);
        return 1 + op switch {
            OpCode.Constant => 1,
            OpCode.ConstantLong => 2,
            OpCode.GetGlobal
                or OpCode.SetGlobal
                or OpCode.DefineGlobal
                or OpCode.GetLocal
                or OpCode.SetLocal
                or OpCode.BuildString => 1,
            OpCode.GetProperty
                or OpCode.SetProperty => 1,
            OpCode.Jump
                or OpCode.JumpIfFalse
                or OpCode.JumpIfTrue => 2,
            _ => 0,
        };
    }

    /// <summary>
    /// Reads all opcodes from <paramref name="chunk"/> up to and including the
    /// first <see cref="OpCode.Return"/>, skipping operand bytes correctly.
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
    // No-slot optimisation: plain string compiles to a single constant — no BuildString.
    // -----------------------------------------------------------------------

    [Fact]
    public void PlainString_NoSlots_EmitsSingleConstantNoBuildString() {
        Chunk chunk = CompileSource("\"hello world\"");

        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.DoesNotContain(OpCode.BuildString, ops);
        Assert.Equal([OpCode.Constant, OpCode.Pop, OpCode.Return], ops);
        Assert.Equal("hello world", chunk.ReadConstant(0).AsString());
    }

    [Fact]
    public void PlainString_EmptyString_EmitsSingleEmptyConstant() {
        Chunk chunk = CompileSource("\"\"");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.BuildString, ops);
        Assert.Equal(string.Empty, chunk.ReadConstant(0).AsString());
    }

    // -----------------------------------------------------------------------
    // Escaped dollar — \$ produces a literal '$' text fragment, not a slot.
    // -----------------------------------------------------------------------

    [Fact]
    public void EscapedDollar_ProducesLiteralDollarConstant_NotSlot() {
        // "\$5.00" compiles to the literal string "$5.00" — the lexer resolved \$ to $.
        Chunk chunk = CompileSource("\"\\$5.00\"");

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.DoesNotContain(OpCode.BuildString, ops);
        Assert.Equal([OpCode.Constant, OpCode.Pop, OpCode.Return], ops);
        Assert.Equal("$5.00", chunk.ReadConstant(0).AsString());
    }

    // -----------------------------------------------------------------------
    // "a${x}b" — three fragments: literal, local, literal → BuildString 3
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolatedString_ThreeFragments_EmitsBuildString3() {
        Chunk chunk = CompileSource("""
            x: int := 42
            "a${x}b"
            """);

        List<OpCode> ops = ReadOpcodes(chunk);

        Assert.Contains(OpCode.BuildString, ops);
        // Locate BuildString and read its operand (fragment count = 3).
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            if (op == OpCode.BuildString) {
                byte count = chunk.ReadByte(offset + 1);
                Assert.Equal(3, count);
                break;
            }
            offset += InstructionSize(chunk, offset);
        }
    }

    [Fact]
    public void InterpolatedString_ThreeFragments_OrderIsTextSlotText() {
        // "a${x}b" → Constant("a"), GetGlobal x, Constant("b"), BuildString 3
        // (x is a top-level global; top-level declarations use GetGlobal.)
        Chunk chunk = CompileSource("""
            x: int := 42
            "a${x}b"
            """);

        List<OpCode> ops = ReadOpcodes(chunk);

        // The three fragments immediately before BuildString must be:
        // Constant("a"), GetGlobal(x), Constant("b").
        int buildIdx = ops.LastIndexOf(OpCode.BuildString);
        Assert.True(buildIdx >= 3, "Expected at least 3 opcodes before BuildString");

        Assert.Equal(OpCode.Constant, ops[buildIdx - 3]);
        Assert.Equal(OpCode.GetGlobal, ops[buildIdx - 2]);
        Assert.Equal(OpCode.Constant, ops[buildIdx - 1]);
    }

    // -----------------------------------------------------------------------
    // Single slot: "${n}" where n: int — wraps in BuildString 1
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolatedString_SingleSlot_EmitsBuildString1() {
        Chunk chunk = CompileSource("""
            n: int := 7
            "${n}"
            """);

        List<OpCode> ops = ReadOpcodes(chunk);
        Assert.Contains(OpCode.BuildString, ops);

        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            if (op == OpCode.BuildString) {
                byte count = chunk.ReadByte(offset + 1);
                Assert.Equal(1, count);
                break;
            }
            offset += InstructionSize(chunk, offset);
        }
    }

    // -----------------------------------------------------------------------
    // E0102 — nullable slot is a compile error (D-279)
    // -----------------------------------------------------------------------

    [Fact]
    public void NullableSlot_DirectNullableVar_EmitsE0102() {
        var diag = CheckSource("""
            x: int? := nil
            "${x}"
            """);

        Assert.True(diag.HasErrors, "Expected E0102 but no errors were raised.");
        Assert.Contains(diag.Errors, e => e.Code == "E0102");
    }

    [Fact]
    public void NullableSlot_NullableVarWithCoalesce_NoError() {
        // "${x ?? 0}" — x ?? 0 resolves to int (non-nullable), so no E0102.
        var diag = CheckSource("""
            x: int? := nil
            "${x ?? 0}"
            """);

        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NullableSlot_NullableStringVarWithCoalesce_NoError() {
        // "${s ?? "fallback"}" resolves to string (non-nullable).
        var diag = CheckSource("""
            s: string? := nil
            "${s ?? "fallback"}"
            """);

        Assert.False(diag.HasErrors, $"Unexpected errors: {string.Join("; ", diag.Errors)}");
    }

    [Fact]
    public void NullableSlot_E0102_IsRaisedOnSlotRange() {
        // Verify the error range points into the interpolated string expression.
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan("x: int? := nil\n\"${x}\"", bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);

        var e0102 = bag.Errors.FirstOrDefault(e => e.Code == "E0102");
        Assert.NotNull(e0102);
        Assert.Equal(2, e0102.Range.Start.Line); // on line 2
    }

    // -----------------------------------------------------------------------
    // Compile produces correct constant values for text fragments.
    // -----------------------------------------------------------------------

    [Fact]
    public void InterpolatedString_TextFragmentConstants_AreCorrect() {
        // "prefix-${n}-suffix" — the two text parts are "prefix-" and "-suffix".
        Chunk chunk = CompileSource("""
            n: int := 1
            "prefix-${n}-suffix"
            """);

        // Locate the constants in the pool for the text fragments.
        bool hasPrefix = false;
        bool hasSuffix = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            var val = chunk.ReadConstant(i);
            if (val.TryAsString(out string? s)) {
                if (s == "prefix-") hasPrefix = true;
                if (s == "-suffix") hasSuffix = true;
            }
        }
        Assert.True(hasPrefix, "Expected constant \"prefix-\" in pool");
        Assert.True(hasSuffix, "Expected constant \"-suffix\" in pool");
    }
}
