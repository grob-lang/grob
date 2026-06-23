using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// A runtime closure: a <see cref="BytecodeFunction"/> paired with the
/// <see cref="Upvalue"/> array that holds its captured variables
/// (Sprint 5 Increment D, D-115).
/// </summary>
/// <remarks>
/// <para>Created by the <see cref="OpCode.Closure"/> arm of the VM dispatch loop.
/// Inherits <see cref="GrobFunction"/> so it can be stored in
/// <see cref="GrobValue"/> and passed to <see cref="OpCode.Call"/> without
/// special-casing the value representation.</para>
/// <para>Lives in <c>Grob.Vm</c> only — the compiler never sees this type (the
/// strict DAG: <c>Grob.Compiler</c> and <c>Grob.Vm</c> never reference each
/// other). The compiler emits the <see cref="OpCode.Closure"/> opcode and its
/// descriptor bytes; the VM creates <see cref="Closure"/> at runtime.</para>
/// </remarks>
internal sealed class Closure : GrobFunction {
    /// <summary>The compiled bytecode function wrapped by this closure.</summary>
    internal BytecodeFunction Function { get; }

    /// <summary>
    /// The captured variables. Index <c>i</c> corresponds to upvalue slot <c>i</c>
    /// as referenced by <see cref="OpCode.GetUpvalue"/> and
    /// <see cref="OpCode.SetUpvalue"/> in the function body. The array length
    /// equals <see cref="BytecodeFunction.UpvalueCount"/>.
    /// </summary>
    internal Upvalue[] Upvalues { get; }

    /// <summary>
    /// Creates a closure wrapping <paramref name="fn"/> with the pre-populated
    /// <paramref name="upvalues"/> array built by the <see cref="OpCode.Closure"/>
    /// dispatch arm.
    /// </summary>
    internal Closure(BytecodeFunction fn, Upvalue[] upvalues)
        : base(fn.Name, fn.Arity) {
        Function = fn;
        Upvalues = upvalues;
    }
}
