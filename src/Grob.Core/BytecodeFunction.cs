namespace Grob.Core;

/// <summary>
/// A user-defined <c>fn</c> compiled to its own <see cref="Chunk"/> of bytecode
/// (Sprint 5 Increment A). The compiler produces one per <c>fn</c> declaration
/// and stores it as a <see cref="GrobValueKind.Function"/> constant; the VM reads
/// <see cref="Bytecode"/> when a <see cref="OpCode.Call"/> dispatches into it.
/// </summary>
/// <remarks>
/// Lives in <c>Grob.Core</c> — the only assembly both <c>Grob.Compiler</c> (which
/// writes the chunk) and <c>Grob.Vm</c> (which executes it) reference — so the
/// strict DAG holds: neither layer needs to see the other to share the function.
/// </remarks>
public sealed class BytecodeFunction : GrobFunction {
    /// <summary>The compiled body of this function — its own instruction stream and constant pool.</summary>
    public Chunk Bytecode { get; }

    /// <summary>
    /// The number of upvalues this function captures (Sprint 5 Increment D).
    /// The VM reads this to know how many <c>isLocal/index</c> descriptor pairs
    /// follow the pool-index byte in the <see cref="OpCode.Closure"/> instruction.
    /// Zero for plain <see cref="BytecodeFunction"/>s compiled without capture.
    /// </summary>
    public int UpvalueCount { get; }

    /// <summary>
    /// Initialises a new <see cref="BytecodeFunction"/> with the given
    /// <paramref name="name"/>, <paramref name="arity"/> and compiled
    /// <paramref name="bytecode"/>. <paramref name="upvalueCount"/> is the number
    /// of captured upvalues (default 0 for non-capturing functions).
    /// </summary>
    public BytecodeFunction(string name, int arity, Chunk bytecode, int upvalueCount = 0)
        : base(name, arity) {
        ArgumentNullException.ThrowIfNull(bytecode);
        Bytecode = bytecode;
        UpvalueCount = upvalueCount;
    }
}
