using Grob.Core;

namespace Grob.Vm;

/// <summary>
/// One entry on the VM's call stack (Sprint 5 Increment A). A frame is pushed by
/// <see cref="OpCode.Call"/> and popped by <see cref="OpCode.Return"/>.
/// </summary>
/// <remarks>
/// <para>The fields capture the <em>caller's</em> resume context — the chunk and
/// instruction pointer to return to, and the stack base to restore — so that on
/// <see cref="OpCode.Return"/> the VM can resume exactly where the call left off.
/// The currently executing function's own chunk, instruction pointer and stack
/// base are held in dispatch-loop locals for speed; this frame is what makes the
/// previous context recoverable.</para>
/// <para>The top-level script is <em>not</em> a frame: the frames array holds call
/// frames only, so its capacity is the maximum call depth (D-180 — depth 257
/// overflows a 256-entry array). At script level the dispatch loop runs with a
/// stack base of zero, matching the absolute slot addressing Sprint 3/4 used for
/// top-level locals.</para>
/// </remarks>
internal struct CallFrame {
    /// <summary>The chunk to resume executing in when this frame returns (the caller's chunk).</summary>
    internal Chunk ReturnChunk;

    /// <summary>The instruction pointer to resume at in <see cref="ReturnChunk"/> (just past the caller's <c>Call</c>).</summary>
    internal int ReturnInstructionPointer;

    /// <summary>The stack base to restore for the caller when this frame returns.</summary>
    internal int ReturnStackBase;

    /// <summary>
    /// The <see cref="Closure"/> currently executing in this frame, or
    /// <see langword="null"/> when this frame is running a plain
    /// <see cref="BytecodeFunction"/> (one with no captured upvalues).
    /// Set by the <see cref="OpCode.Closure"/> call path so that
    /// <see cref="OpCode.GetUpvalue"/>/<see cref="OpCode.SetUpvalue"/> can
    /// reach the upvalue array, and so that the <see cref="OpCode.Closure"/>
    /// arm can thread upvalues transitively through enclosing closures.
    /// </summary>
    internal Closure? ActiveClosure;
}
