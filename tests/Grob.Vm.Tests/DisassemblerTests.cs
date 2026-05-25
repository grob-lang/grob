using System.Text;
using Grob.Core;
using Grob.Vm;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// Disassembler tests — all against hand-constructed chunks.
/// No compiler, no VM dispatch loop: those arrive in later increments.
/// </summary>
public sealed class DisassemblerTests {
    // ----- Helpers -----

    private static string Disassemble(Chunk chunk, string name = "chunk") {
        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        Disassembler.DisassembleChunk(chunk, writer, name);
        return sb.ToString();
    }

    private static string[] Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
              .Select(l => l.TrimEnd('\r'))
              .ToArray();

    // ----- 2 + 3 * 4 chunk — full listing -----

    /// <summary>
    /// Hand-constructs the bytecode for the sub-expression <c>2 + 3 * 4</c>
    /// (constants, typed arithmetic, Return) and asserts the full listing.
    ///
    /// Stack trace at runtime (not tested here, but illustrates the bytecode):
    ///   Constant 2  → stack: [2]
    ///   Constant 3  → stack: [2, 3]
    ///   Constant 4  → stack: [2, 3, 4]
    ///   MultiplyInt → stack: [2, 12]
    ///   AddInt      → stack: [14]
    ///   Return      → stack: []
    /// </summary>
    [Fact]
    public void DisassembleChunk_TwoPlusThreeTimesFor_CorrectListing() {
        var chunk = BuildTwoPlusThreeTimesForChunk();
        var output = Disassemble(chunk, "two-plus-three-times-four");
        var lines = Lines(output);

        // Header
        Assert.Equal("== two-plus-three-times-four ==", lines[0]);

        // 0000    1 Constant                0 '2'
        Assert.Equal("0000    1 Constant                0 '2'", lines[1]);

        // 0002    | Constant                1 '3'    (same source line → | marker)
        Assert.Equal("0002    | Constant                1 '3'", lines[2]);

        // 0004    | Constant                2 '4'
        Assert.Equal("0004    | Constant                2 '4'", lines[3]);

        // 0006    | MultiplyInt
        Assert.Equal("0006    | MultiplyInt", lines[4]);

        // 0007    | AddInt
        Assert.Equal("0007    | AddInt", lines[5]);

        // 0008    | Return
        Assert.Equal("0008    | Return", lines[6]);

        Assert.Equal(7, lines.Length);
    }

    private static Chunk BuildTwoPlusThreeTimesForChunk() {
        var chunk = new Chunk();
        int idx2 = chunk.AddConstant(GrobValue.FromInt(2));
        int idx3 = chunk.AddConstant(GrobValue.FromInt(3));
        int idx4 = chunk.AddConstant(GrobValue.FromInt(4));

        // All on source line 1
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)idx2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)idx3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte((byte)idx4, 1);
        chunk.WriteOpCode(OpCode.MultiplyInt, 1);
        chunk.WriteOpCode(OpCode.AddInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        return chunk;
    }

    // ----- Constant pool index and resolved value appear in output -----

    [Fact]
    public void ConstantInstruction_PrintsPoolIndexAndResolvedValue() {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromString("hello"));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)idx, 1);

        var lines = Lines(Disassemble(chunk));

        // Expect: "0000    1 Constant                0 'hello'"
        Assert.Equal("0000    1 Constant                0 'hello'", lines[1]);
    }

    [Fact]
    public void ConstantInstruction_FloatValue_PrintsPoolIndexAndValue() {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromFloat(3.14));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)idx, 1);

        var output = Disassemble(chunk);

        // Pool index 0 and resolved value must both appear somewhere in the output
        Assert.Contains("   0 '", output);
        Assert.Contains("3.14", output);
    }

    // ----- Shared-line marker -----

    [Fact]
    public void SameSourceLine_RendersBarMarkerInsteadOfLineNumber() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.True, 5);    // line 5
        chunk.WriteOpCode(OpCode.False, 5);   // same line 5
        chunk.WriteOpCode(OpCode.Nil, 6);     // new line 6

        var lines = Lines(Disassemble(chunk));

        // lines[0] = header
        Assert.Contains("   5 ", lines[1]);   // first instruction on line 5 → line number
        Assert.Contains("   | ", lines[2]);   // second instruction on same line → | marker
        Assert.Contains("   6 ", lines[3]);   // new source line → line number, not |
    }

    [Fact]
    public void DifferentSourceLines_EachRendersItsOwnLineNumber() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.True, 10);
        chunk.WriteOpCode(OpCode.False, 20);
        chunk.WriteOpCode(OpCode.Nil, 30);

        var lines = Lines(Disassemble(chunk));

        Assert.Contains("  10 ", lines[1]);
        Assert.Contains("  20 ", lines[2]);
        Assert.Contains("  30 ", lines[3]);
    }

    // ----- DisassembleInstruction returns correct next offset -----

    [Fact]
    public void DisassembleInstruction_SimpleOpcode_ReturnsOffsetPlusOne() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Return, 1);

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(1, next);
    }

    [Fact]
    public void DisassembleInstruction_ConstantOpcode_ReturnsOffsetPlusTwo() {
        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromInt(42));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)idx, 1);

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(2, next);
    }

    [Fact]
    public void DisassembleInstruction_JumpOpcode_ReturnsOffsetPlusThree() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Jump, 1);
        chunk.WriteByte(0x00, 1);   // jump offset high byte
        chunk.WriteByte(0x05, 1);   // jump offset low byte

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(3, next);
    }

    [Fact]
    public void DisassembleInstruction_ByteOperandOpcode_ReturnsOffsetPlusTwo() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.GetLocal, 1);
        chunk.WriteByte(0x02, 1);   // slot index

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(2, next);
    }

    [Fact]
    public void DisassembleInstruction_ConstantLongOpcode_ReturnsOffsetPlusThree() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.ConstantLong, 1);
        chunk.WriteByte(0x00, 1);   // high byte of index
        chunk.WriteByte(0x01, 1);   // low byte of index

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(3, next);
    }

    // ----- Unknown / malformed opcode never throws -----

    [Fact]
    public void UnknownOpcodeByte_DoesNotThrow_ProducesUnknownOpcodeLine() {
        var chunk = new Chunk();
        // Write a raw byte that is not a valid OpCode
        chunk.WriteByte(0xFF, 1);

        using var writer = new StringWriter();
        var ex = Record.Exception(() => Disassembler.DisassembleInstruction(chunk, 0, writer));
        Assert.Null(ex);

        string output = writer.ToString();
        Assert.Contains("Unknown opcode", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisassembleChunk_WithUnknownByte_DoesNotThrow() {
        var chunk = new Chunk();
        chunk.WriteByte(0xFE, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        using var writer = new StringWriter();
        var ex = Record.Exception(() => Disassembler.DisassembleChunk(chunk, writer));
        Assert.Null(ex);
    }

    // ----- Truncated operand does not throw -----

    [Fact]
    public void TruncatedConstantOperand_DoesNotThrow() {
        var chunk = new Chunk();
        // Write Constant opcode but no operand byte — simulates corrupt bytecode
        chunk.WriteOpCode(OpCode.Constant, 1);

        using var writer = new StringWriter();
        var ex = Record.Exception(() => Disassembler.DisassembleInstruction(chunk, 0, writer));
        Assert.Null(ex);
    }

    [Fact]
    public void TruncatedConstantLongOperand_EmitsTruncated() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.ConstantLong, 1);
        chunk.WriteByte(0x00, 1);   // only one of the two needed bytes

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(1, next);
        Assert.Contains("truncated", writer.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TruncatedByteOperand_EmitsTruncated() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.GetLocal, 1);   // expects a 1-byte operand

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(1, next);
        Assert.Contains("truncated", writer.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TruncatedJumpOperand_EmitsTruncated() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Jump, 1);
        chunk.WriteByte(0x00, 1);   // only one of the two needed bytes

        using var writer = new StringWriter();
        int next = Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Equal(1, next);
        Assert.Contains("truncated", writer.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidPoolIndex_RendersInvalidPlaceholder() {
        // Constant opcode that refers to an index past the (empty) constant pool.
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte(0x00, 1);

        using var writer = new StringWriter();
        Disassembler.DisassembleInstruction(chunk, 0, writer);

        Assert.Contains("invalid index", writer.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
