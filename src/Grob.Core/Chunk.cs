namespace Grob.Core;

/// <summary>
/// A compiled bytecode chunk: instruction bytes, constant pool, and per-instruction
/// source positions (line + column).
///
/// Write surface (<c>WriteByte</c>, <c>AddConstant</c>) is used by the compiler
/// (Sprint 2 Increment D) and by hand-constructed test chunks (Increment A/B).
/// Source columns are 1-based; <c>0</c> is the sentinel used when a chunk byte
/// has no meaningful column origin (synthetic prologue, hand-built test bytecode
/// that did not supply one, etc.).
/// </summary>
public sealed class Chunk {
    private readonly List<byte> _code = [];
    private readonly List<GrobValue> _constants = [];
    private readonly List<int> _lines = [];     // parallel to _code: source line per byte
    private readonly List<int> _columns = [];   // parallel to _code: source column per byte (0 = unknown)

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

    /// <summary>
    /// Source column number for the instruction byte at <paramref name="offset"/>.
    /// Returns <c>0</c> when no column was supplied at write time. Columns are
    /// 1-based when present.
    /// </summary>
    public int GetColumn(int offset) => _columns[offset];

    // ----- Write surface (Compiler / hand-constructed tests) -----

    /// <summary>
    /// Append a raw byte attributed to <paramref name="line"/>. Column is
    /// recorded as <c>0</c> ("unknown"); prefer the <see cref="WriteByte(byte, int, int)"/>
    /// overload from the compiler so runtime errors can point at the exact
    /// column.
    /// </summary>
    public void WriteByte(byte value, int line) => WriteByte(value, line, 0);

    /// <summary>
    /// Append a raw byte attributed to <paramref name="line"/> and
    /// <paramref name="column"/> (1-based; <c>0</c> means "unknown").
    /// </summary>
    public void WriteByte(byte value, int line, int column) {
        _code.Add(value);
        _lines.Add(line);
        _columns.Add(column);
    }

    /// <summary>Append an opcode byte attributed to <paramref name="line"/>.</summary>
    public void WriteOpCode(OpCode opCode, int line) =>
        WriteByte((byte)opCode, line, 0);

    /// <summary>
    /// Append an opcode byte attributed to <paramref name="line"/> and
    /// <paramref name="column"/> (1-based; <c>0</c> means "unknown").
    /// </summary>
    public void WriteOpCode(OpCode opCode, int line, int column) =>
        WriteByte((byte)opCode, line, column);

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
