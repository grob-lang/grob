using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 4 Increment B — <c>while</c> loop execution.
/// All chunks are hand-constructed; no compiler dependency.
/// </summary>
/// <remarks>
/// Tests verify that <see cref="OpCode.Loop"/> correctly jumps backward to the
/// loop condition, that the iteration count is exact (no off-by-one), and that
/// <c>break</c> (a forward <see cref="OpCode.Jump"/> out of the loop) exits before
/// the end-of-body <see cref="OpCode.Loop"/> fires.
/// </remarks>
public sealed class VirtualMachineLoopTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    /// <summary>
    /// Writes a 2-byte big-endian forward-jump offset (operand for
    /// <see cref="OpCode.Jump"/>, <see cref="OpCode.JumpIfFalse"/>).
    /// </summary>
    private static void WriteJumpOffset(Chunk chunk, int offset, int line) {
        chunk.WriteByte((byte)(offset >> 8), line);
        chunk.WriteByte((byte)(offset & 0xFF), line);
    }

    /// <summary>
    /// Writes a 2-byte big-endian backward-jump offset for <see cref="OpCode.Loop"/>.
    /// Must be called immediately after the <see cref="OpCode.Loop"/> opcode byte has
    /// been written but before its two operand bytes (so <c>chunk.Count</c> points at
    /// the first operand byte).
    /// <para>
    /// Formula: <c>offset = (chunk.Count + 2) − loopStart</c>, matching
    /// <c>Compiler.EmitLoop</c>.  The VM subtracts this from the instruction pointer
    /// after reading the two bytes, landing exactly at <paramref name="loopStart"/>.
    /// </para>
    /// </summary>
    private static void WriteLoopOffset(Chunk chunk, int loopStart, int line) {
        int offset = (chunk.Count + 2) - loopStart;
        chunk.WriteByte((byte)(offset >> 8), line);
        chunk.WriteByte((byte)(offset & 0xFF), line);
    }

    // -----------------------------------------------------------------------
    // Loop — backward jump targets correct position
    // -----------------------------------------------------------------------

    /// <summary>
    /// A single <see cref="OpCode.Loop"/> instruction must jump backward by the
    /// encoded offset and land at position 0 (the loop-top).
    /// <para>
    /// Chunk: False → JumpIfFalse(exit) → Loop(back-to-False) → Return.
    /// The condition is immediately false, so the loop body (the Loop opcode)
    /// is never reached at runtime — but the Loop offset is still encoded
    /// correctly and the disassembler / verifier can inspect it.
    /// </para>
    /// <para>
    /// The actual backward-jump behaviour is verified by
    /// <see cref="WhileLoop_ThreeIterations_CounterReachesThree"/>.
    /// </para>
    /// </summary>
    [Fact]
    public void Loop_Opcode_BackwardOffsetEncoding_IsConsistent() {
        // Layout:
        // [0]  False
        // [1]  JumpIfFalse
        // [2,3] offset → lands at [7] = Return
        // [4]  Loop
        // [5,6] offset = 7 (ip_after=7, target=0, offset=7)
        // [7]  Return
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 3, 1);       // skip Loop + 2 bytes → offset 3 to reach [7]
        chunk.WriteOpCode(OpCode.Loop, 1);
        WriteLoopOffset(chunk, 0, 1);       // back to [0]; ip_after=7, offset=7
        chunk.WriteOpCode(OpCode.Return, 1);

        // Verify offset encoded in [5,6] is 7 (= 7 - 0)
        int hi = chunk.ReadByte(5);
        int lo = chunk.ReadByte(6);
        Assert.Equal(7, (hi << 8) | lo);

        // Run: condition is false, JumpIfFalse takes us to Return immediately.
        var (vm, _) = NewVm();
        vm.Run(chunk);
        Assert.Equal(0, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // while — counted iteration
    // -----------------------------------------------------------------------

    /// <summary>
    /// A counted <c>while</c> loop must execute exactly three iterations —
    /// no more, no fewer.
    /// <para>
    /// Simulates: <c>i := 0; while (i &lt; 3) { i++ }</c>
    /// </para>
    /// <para>
    /// Chunk layout (slot 0 = counter, initial value 0):
    /// </para>
    /// <code>
    ///  [0,1]   Constant ci0         — push 0 (slot 0 = counter)
    ///  [2,3]   GetLocal 0           — loopStart: push counter    ← condition top
    ///  [4,5]   Constant ci3         — push 3
    ///  [6]     LessInt              — pop 2, push bool
    ///  [7,8,9] JumpIfFalse exit=5   — exit to [15] if false; ip_after=10
    ///  [10,11] IncrementInt 0       — slot 0++
    ///  [12,13,14] Loop back=13      — ip_after=15, target=2; offset=15−2=13
    ///  [15,16] GetLocal 0           — push final counter (= 3)
    ///  [17]    Return
    /// </code>
    /// </summary>
    [Fact]
    public void WhileLoop_ThreeIterations_CounterReachesThree() {
        var chunk = new Chunk();
        byte ci0 = ConstByte(chunk, GrobValue.FromInt(0));
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));

        // Prolog — push 0 onto stack (becomes local slot 0 = counter)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci0, 1);  // [0,1]

        // loopStart = 2
        int loopStart = chunk.Count;  // = 2
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);    // [2,3]
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);  // [4,5]
        chunk.WriteOpCode(OpCode.LessInt, 1);                            // [6]

        // JumpIfFalse to exit; ip after operands = 10; target = [15] → offset = 15−10 = 5
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);   // [7,8,9]

        // Body: counter++
        chunk.WriteOpCode(OpCode.IncrementInt, 1); chunk.WriteByte(0, 1);  // [10,11]

        // End of body: Loop backward to loopStart
        chunk.WriteOpCode(OpCode.Loop, 1);
        WriteLoopOffset(chunk, loopStart, 1);   // [12,13,14]; offset = 15−2 = 13

        // Exit: push final counter value then Return
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);  // [15,16]
        chunk.WriteOpCode(OpCode.Return, 1);                            // [17]

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Stack: [0 (slot0), 3 (GetLocal result)]
        Assert.Equal(2, vm.Stack.Count);
        Assert.Equal(3L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // while — break exits before end-of-body Loop fires
    // -----------------------------------------------------------------------

    /// <summary>
    /// A <c>break</c> inside a <c>while</c> body must exit the loop without
    /// executing the end-of-body <see cref="OpCode.Loop"/> backward jump.
    /// <para>
    /// Simulates: <c>while (true) { break }</c>
    /// </para>
    /// <para>
    /// Chunk layout:
    /// </para>
    /// <code>
    ///  [0]     True              — condition (always true)
    ///  [1,2,3] JumpIfFalse 6    — exit to [10] if false; ip_after=4; offset=6
    ///  [4,5,6] Jump 3           — break: forward to [10]; ip_after=7; offset=3
    ///  [7,8,9] Loop back=10     — never reached; ip_after=10; offset=10; target=0
    ///  [10]    Return
    /// </code>
    /// </summary>
    [Fact]
    public void WhileLoop_Break_ExitsLoopImmediately() {
        var chunk = new Chunk();

        // loopStart = 0
        chunk.WriteOpCode(OpCode.True, 1);           // [0]
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 6, 1);               // [1,2,3] → exit to [10]; offset = 10−4 = 6
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 3, 1);               // [4,5,6] break → [10]; offset = 10−7 = 3
        chunk.WriteOpCode(OpCode.Loop, 1);
        WriteLoopOffset(chunk, 0, 1);               // [7,8,9] unreachable; offset = 10−0 = 10
        chunk.WriteOpCode(OpCode.Return, 1);        // [10]

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Nothing remains on the stack: True was popped by JumpIfFalse.
        Assert.Equal(0, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // while — continue re-evaluates the condition
    // -----------------------------------------------------------------------

    /// <summary>
    /// A <c>continue</c> inside a <c>while</c> body must jump back to the condition
    /// so the condition is re-evaluated on the next iteration.
    /// <para>
    /// Simulates: <c>i := 0; while (i &lt; 1) { i++; continue; i++ }</c>
    /// The second <c>i++</c> (after continue) must never execute — slot 0 must equal
    /// 1 after the loop, not 2.
    /// </para>
    /// <para>
    /// Chunk layout (slot 0 = i):
    /// </para>
    /// <code>
    ///  [0,1]    Constant ci0         — push 0 (slot 0 = i)
    ///  [2,3]    GetLocal 0           — loopStart
    ///  [4,5]    Constant ci1         — push 1
    ///  [6]      LessInt
    ///  [7,8,9]  JumpIfFalse exit=10  — exit to [20] if false; ip_after=10; offset=10
    ///  [10,11]  IncrementInt 0       — i++
    ///  [12,13,14] Loop continue=13   — continue: back to [2]; ip_after=15; offset=13
    ///  [15,16]  IncrementInt 0       — dead code: never reached by continue
    ///  [17,18,19] Loop body=18       — end of body: back to [2]; ip_after=20; offset=18
    ///  [20,21]  GetLocal 0           — push final i (= 1)
    ///  [22]     Return
    /// </code>
    /// </summary>
    [Fact]
    public void WhileLoop_Continue_SkipsDeadCodeAndReturnsToCondition() {
        var chunk = new Chunk();
        byte ci0 = ConstByte(chunk, GrobValue.FromInt(0));
        byte ci1 = ConstByte(chunk, GrobValue.FromInt(1));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci0, 1);   // [0,1]

        int loopStart = chunk.Count;  // = 2
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);    // [2,3]
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci1, 1);  // [4,5]
        chunk.WriteOpCode(OpCode.LessInt, 1);                            // [6]

        // JumpIfFalse: ip_after = 10; target = [20] (GetLocal 0); offset = 20-10 = 10
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 10, 1);   // [7,8,9]

        // First i++
        chunk.WriteOpCode(OpCode.IncrementInt, 1); chunk.WriteByte(0, 1);  // [10,11]

        // Continue: Loop back to loopStart=2; ip_after=15; offset=15−2=13
        chunk.WriteOpCode(OpCode.Loop, 1);
        WriteLoopOffset(chunk, loopStart, 1);   // [12,13,14]

        // Dead code: second i++ (skipped by continue)
        chunk.WriteOpCode(OpCode.IncrementInt, 1); chunk.WriteByte(0, 1);  // [15,16]

        // End of body Loop: ip_after=20; target=2; offset=20−2=18
        chunk.WriteOpCode(OpCode.Loop, 1);
        WriteLoopOffset(chunk, loopStart, 1);   // [17,18,19]

        // Exit: push final i
        chunk.WriteOpCode(OpCode.GetLocal, 1); chunk.WriteByte(0, 1);  // [20,21]
        chunk.WriteOpCode(OpCode.Return, 1);                            // [22]

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Stack: [0 (slot0 = initial push), 1 (GetLocal = final i)]
        // i must be 1 (first i++ ran once, second i++ was never reached).
        Assert.Equal(2, vm.Stack.Count);
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }
}
