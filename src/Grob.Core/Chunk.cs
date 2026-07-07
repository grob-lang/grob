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
    private readonly List<StructTypeDescriptor> _structTypes = [];
    private readonly List<TryRegion> _tryRegions = [];

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
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="line"/> is less than <c>1</c> (lines are
    /// always 1-based and never optional) or when <paramref name="column"/>
    /// is negative. Columns are 1-based when present, or <c>0</c> for
    /// "unknown"; negative values are never valid source metadata.
    /// </exception>
    public void WriteByte(byte value, int line, int column) {
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
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

    /// <summary>
    /// Register a struct type descriptor and return its 1-byte table index.
    /// Called by the compiler when emitting a <c>NewStruct</c> instruction.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when more than 256 struct types are registered in a single chunk.
    /// </exception>
    public byte AddStructType(StructTypeDescriptor descriptor) {
        if (_structTypes.Count >= 256)
            throw new InvalidOperationException("Struct type table overflow: a single chunk may hold at most 256 struct type descriptors.");
        _structTypes.Add(descriptor);
        return (byte)(_structTypes.Count - 1);
    }

    /// <summary>The number of struct type descriptors registered in this chunk.</summary>
    public int StructTypeCount => _structTypes.Count;

    /// <summary>Read the struct type descriptor at <paramref name="index"/>.</summary>
    public StructTypeDescriptor GetStructType(byte index) => _structTypes[index];

    // ----- Handler table (Sprint 7 Increment B) -----

    /// <summary>
    /// Reserve a <see cref="TryRegion"/> table slot and return its index. The
    /// region's bounds and handlers are not yet known when <see cref="OpCode.TryBegin"/>
    /// is emitted (the compiler is only starting to compile the try body), so the
    /// index is reserved first and the entry filled in later via <see cref="SetTryRegion"/> —
    /// the same two-phase reserve-then-patch shape the compiler uses for forward jump
    /// targets. The placeholder entry has no handlers.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when more than 256 try regions are registered in a single chunk —
    /// <see cref="OpCode.TryBegin"/>'s operand is a single byte.
    /// </exception>
    public int AddTryRegion() {
        if (_tryRegions.Count >= 256)
            throw new InvalidOperationException("Try region table overflow: a single chunk may hold at most 256 try regions.");
        _tryRegions.Add(new TryRegion(0, 0, []));
        return _tryRegions.Count - 1;
    }

    /// <summary>Overwrite the reserved try-region entry at <paramref name="index"/> with its resolved bounds and handlers.</summary>
    public void SetTryRegion(int index, TryRegion region) {
        ArgumentNullException.ThrowIfNull(region);
        _tryRegions[index] = region;
    }

    /// <summary>The number of try regions registered in this chunk.</summary>
    public int TryRegionCount => _tryRegions.Count;

    /// <summary>Read the try region at <paramref name="index"/>.</summary>
    public TryRegion GetTryRegion(int index) => _tryRegions[index];

    /// <summary>
    /// Overwrite the byte at <paramref name="offset"/> with <paramref name="value"/>.
    /// Used by the compiler's backpatch helper (<c>PatchJump</c>) to fill in the
    /// placeholder bytes written by <c>EmitJump</c> once the jump target is known.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="offset"/> is outside the range
    /// <c>[0, Count)</c>.
    /// </exception>
    public void PatchByte(int offset, byte value) {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(offset, _code.Count);
        _code[offset] = value;
    }
}
