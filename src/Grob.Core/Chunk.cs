namespace Grob.Core;

/// <summary>
/// A compiled bytecode chunk: instruction bytes, constant pool, and per-instruction
/// source line numbers.
///
/// Write surface (<c>WriteByte</c>, <c>AddConstant</c>) is used by the compiler
/// (Sprint 2 Increment D) and by hand-constructed test chunks (Increment A/B).
/// </summary>
public sealed class Chunk {
    private readonly List<byte> _code = [];
    private readonly List<GrobValue> _constants = [];
    private readonly List<int> _lines = [];   // parallel to _code: source line per byte

    // ----- Read surface (Disassembler and VM) -----

    /// <summary>Number of bytes in the instruction stream.</summary>
    public int Count => _code.Count;

    /// <summary>Read a raw byte at the given offset.</summary>
    public byte ReadByte(int offset) => _code[offset];

    /// <summary>Read a constant from the pool by index.</summary>
    public GrobValue ReadConstant(int index) => _constants[index];

    /// <summary>Number of constants in the pool.</summary>
    public int ConstantCount => _constants.Count;

    /// <summary>Source line number for the instruction byte at <paramref name="offset"/>.</summary>
    public int GetLine(int offset) => _lines[offset];

    // ----- Write surface (Compiler / hand-constructed tests) -----

    /// <summary>Append a raw byte attributed to <paramref name="line"/>.</summary>
    public void WriteByte(byte value, int line) {
        _code.Add(value);
        _lines.Add(line);
    }

    /// <summary>Append an opcode byte attributed to <paramref name="line"/>.</summary>
    public void WriteOpCode(OpCode opCode, int line) =>
        WriteByte((byte)opCode, line);

    /// <summary>
    /// Add a value to the constant pool and return its index.
    /// The compiler then writes a <see cref="OpCode.Constant"/> or
    /// <see cref="OpCode.ConstantLong"/> instruction referencing this index.
    /// </summary>
    public int AddConstant(GrobValue value) {
        _constants.Add(value);
        return _constants.Count - 1;
    }
}
