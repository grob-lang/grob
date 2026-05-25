using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// Human-readable bytecode disassembler. Always compiled — present in Release
/// builds as well as Debug. Authority: D-306, grob-vm-architecture.md §Developer Diagnostics.
///
/// Output format per instruction line:
/// <code>
///   0000 1234 OpCodeName   [operand details]
/// </code>
/// Column 1 (4 chars): byte offset in the chunk.
/// Column 2 (4 chars): source line number, or <c>|</c> when the same line
///   as the preceding instruction (clox convention).
/// Column 3+: opcode name and any operands.
/// For constant-bearing opcodes: constant pool index and resolved value.
///
/// The disassembler never throws on malformed bytecode — it is a debugging
/// tool and must survive corrupt input.
/// </summary>
public static class Disassembler {
    /// <summary>
    /// Disassemble an entire chunk to <paramref name="writer"/>,
    /// printing a header line followed by each instruction.
    /// </summary>
    public static void DisassembleChunk(Chunk chunk, TextWriter writer, string name = "chunk") {
        writer.WriteLine($"== {name} ==");

        int offset = 0;
        while (offset < chunk.Count) {
            offset = DisassembleInstruction(chunk, offset, writer);
        }
    }

    /// <summary>
    /// Disassemble the single instruction at <paramref name="offset"/>.
    /// Returns the offset of the next instruction (i.e. offset + instruction width).
    /// Never throws — unknown opcode bytes produce an "unknown opcode" line.
    /// </summary>
    public static int DisassembleInstruction(Chunk chunk, int offset, TextWriter writer) {
        // --- Byte offset (4 digits, zero-padded) ---
        writer.Write($"{offset:D4} ");

        // --- Source line (4 chars, or "|" when same as previous) ---
        int line = chunk.GetLine(offset);
        if (offset > 0 && chunk.GetLine(offset - 1) == line)
            writer.Write("   | ");
        else
            writer.Write($"{line,4} ");

        byte instruction = chunk.ReadByte(offset);

        if (!Enum.IsDefined(typeof(OpCode), instruction)) {
            writer.WriteLine($"Unknown opcode {instruction}");
            return offset + 1;
        }

        var opCode = (OpCode)instruction;

        return opCode switch {
            // Simple (no operands)
            OpCode.Nil => SimpleInstruction(opCode, offset, writer),
            OpCode.True => SimpleInstruction(opCode, offset, writer),
            OpCode.False => SimpleInstruction(opCode, offset, writer),
            OpCode.Pop => SimpleInstruction(opCode, offset, writer),
            OpCode.Equal => SimpleInstruction(opCode, offset, writer),
            OpCode.NotEqual => SimpleInstruction(opCode, offset, writer),
            OpCode.AddInt => SimpleInstruction(opCode, offset, writer),
            OpCode.AddFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.SubtractInt => SimpleInstruction(opCode, offset, writer),
            OpCode.SubtractFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.MultiplyInt => SimpleInstruction(opCode, offset, writer),
            OpCode.MultiplyFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.DivideInt => SimpleInstruction(opCode, offset, writer),
            OpCode.DivideFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.ModuloInt => SimpleInstruction(opCode, offset, writer),
            OpCode.ModuloFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.NegateInt => SimpleInstruction(opCode, offset, writer),
            OpCode.NegateFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.Concat => SimpleInstruction(opCode, offset, writer),
            OpCode.IntToFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.LessInt => SimpleInstruction(opCode, offset, writer),
            OpCode.LessFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.LessString => SimpleInstruction(opCode, offset, writer),
            OpCode.GreaterInt => SimpleInstruction(opCode, offset, writer),
            OpCode.GreaterFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.GreaterString => SimpleInstruction(opCode, offset, writer),
            OpCode.LessEqualInt => SimpleInstruction(opCode, offset, writer),
            OpCode.LessEqualFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.GreaterEqualInt => SimpleInstruction(opCode, offset, writer),
            OpCode.GreaterEqualFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.Not => SimpleInstruction(opCode, offset, writer),
            OpCode.Return => SimpleInstruction(opCode, offset, writer),
            OpCode.Print => SimpleInstruction(opCode, offset, writer),
            OpCode.CloseUpvalue => SimpleInstruction(opCode, offset, writer),
            OpCode.GetIndex => SimpleInstruction(opCode, offset, writer),
            OpCode.SetIndex => SimpleInstruction(opCode, offset, writer),
            OpCode.NilCoalesce => SimpleInstruction(opCode, offset, writer),
            OpCode.IsNil => SimpleInstruction(opCode, offset, writer),
            OpCode.TryEnd => SimpleInstruction(opCode, offset, writer),
            OpCode.Throw => SimpleInstruction(opCode, offset, writer),
            OpCode.IncrementInt => SimpleInstruction(opCode, offset, writer),
            OpCode.DecrementInt => SimpleInstruction(opCode, offset, writer),
            OpCode.IncrementFloat => SimpleInstruction(opCode, offset, writer),
            OpCode.DecrementFloat => SimpleInstruction(opCode, offset, writer),

            // 1-byte operand: constant pool index
            OpCode.Constant => ConstantInstruction(opCode, chunk, offset, writer),

            // 2-byte operand: constant pool index (long form)
            OpCode.ConstantLong => ConstantLongInstruction(opCode, chunk, offset, writer),

            // 1-byte operand: count
            OpCode.PopN => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.NewArray => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.BuildString => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.Call => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.NewStruct => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.NewAnonStruct => ByteOperandInstruction(opCode, chunk, offset, writer),

            // 1-byte operand: slot/name index
            OpCode.GetLocal => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.SetLocal => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.GetGlobal => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.SetGlobal => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.DefineGlobal => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.GetUpvalue => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.SetUpvalue => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.Closure => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.GetProperty => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.SetProperty => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.Import => ByteOperandInstruction(opCode, chunk, offset, writer),
            OpCode.TryBegin => ByteOperandInstruction(opCode, chunk, offset, writer),

            // 2-byte operand: jump offset
            OpCode.Jump => JumpInstruction(opCode, chunk, offset, writer),
            OpCode.JumpIfFalse => JumpInstruction(opCode, chunk, offset, writer),
            OpCode.JumpIfTrue => JumpInstruction(opCode, chunk, offset, writer),
            OpCode.Loop => JumpInstruction(opCode, chunk, offset, writer),

            // Catch-all (shouldn't be reached because IsDefined guards above, but keeps exhaustiveness)
            _ => UnknownInstruction(instruction, offset, writer),
        };
    }

    // ----- Instruction helpers -----

    private static int SimpleInstruction(OpCode opCode, int offset, TextWriter writer) {
        writer.WriteLine(opCode.ToString());
        return offset + 1;
    }

    private static int ConstantInstruction(OpCode opCode, Chunk chunk, int offset, TextWriter writer) {
        if (offset + 1 >= chunk.Count) {
            writer.WriteLine($"{opCode} <truncated>");
            return offset + 1;
        }

        byte constantIndex = chunk.ReadByte(offset + 1);
        GrobValue value = SafeReadConstant(chunk, constantIndex);
        writer.WriteLine($"{opCode,-20} {constantIndex,4} '{value}'");
        return offset + 2;
    }

    private static int ConstantLongInstruction(OpCode opCode, Chunk chunk, int offset, TextWriter writer) {
        if (offset + 2 >= chunk.Count) {
            writer.WriteLine($"{opCode} <truncated>");
            return offset + 1;
        }

        // Big-endian 2-byte index
        int constantIndex = (chunk.ReadByte(offset + 1) << 8) | chunk.ReadByte(offset + 2);
        GrobValue value = SafeReadConstant(chunk, constantIndex);
        writer.WriteLine($"{opCode,-20} {constantIndex,4} '{value}'");
        return offset + 3;
    }

    private static int ByteOperandInstruction(OpCode opCode, Chunk chunk, int offset, TextWriter writer) {
        if (offset + 1 >= chunk.Count) {
            writer.WriteLine($"{opCode} <truncated>");
            return offset + 1;
        }

        byte operand = chunk.ReadByte(offset + 1);
        writer.WriteLine($"{opCode,-20} {operand,4}");
        return offset + 2;
    }

    private static int JumpInstruction(OpCode opCode, Chunk chunk, int offset, TextWriter writer) {
        if (offset + 2 >= chunk.Count) {
            writer.WriteLine($"{opCode} <truncated>");
            return offset + 1;
        }

        // Big-endian 2-byte jump offset
        int jump = (chunk.ReadByte(offset + 1) << 8) | chunk.ReadByte(offset + 2);
        writer.WriteLine($"{opCode,-20} {jump,4}");
        return offset + 3;
    }

    private static int UnknownInstruction(byte instruction, int offset, TextWriter writer) {
        writer.WriteLine($"Unknown opcode {instruction}");
        return offset + 1;
    }

    private static GrobValue SafeReadConstant(Chunk chunk, int index) {
        if (index < 0 || index >= chunk.ConstantCount)
            return GrobValue.FromString($"<invalid index {index}>");
        return chunk.ReadConstant(index);
    }
}
