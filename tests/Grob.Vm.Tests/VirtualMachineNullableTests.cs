using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 3 Increment D — nil handling, control
/// flow and property access opcodes.  All chunks are hand-constructed; no
/// compiler dependency.
/// </summary>
public sealed class VirtualMachineNullableTests {
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    private static byte ConstByte(Chunk chunk, GrobValue value) =>
        (byte)chunk.AddConstant(value);

    /// <summary>
    /// Writes a 2-byte big-endian forward-jump offset for opcodes that use it
    /// (<see cref="OpCode.Jump"/>, <see cref="OpCode.JumpIfFalse"/>,
    /// <see cref="OpCode.JumpIfTrue"/>).
    /// </summary>
    private static void WriteJumpOffset(Chunk chunk, int offset, int line) {
        chunk.WriteByte((byte)(offset >> 8), line);
        chunk.WriteByte((byte)(offset & 0xFF), line);
    }

    // -------------------------------------------------------------------------
    // IsNil — peeks top, pushes bool
    // -------------------------------------------------------------------------

    [Fact]
    public void IsNil_OnNilValue_PushesTrue_LeavesReceiverOnStack() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.IsNil, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // IsNil peeked — stack now has [nil, true].
        Assert.Equal(2, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void IsNil_OnNonNilValue_PushesFalse() {
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromInt(42));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.IsNil, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Stack: [42, false]
        Assert.Equal(2, vm.Stack.Count);
        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -------------------------------------------------------------------------
    // NilCoalesce — eager; pops right then left, result is left when non-nil
    // -------------------------------------------------------------------------

    [Fact]
    public void NilCoalesce_LeftNil_ReturnsRight() {
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromInt(42));
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.NilCoalesce, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void NilCoalesce_LeftNonNil_ReturnsLeft() {
        var chunk = new Chunk();
        byte ciLeft = ConstByte(chunk, GrobValue.FromInt(7));
        byte ciRight = ConstByte(chunk, GrobValue.FromInt(99));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciLeft, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciRight, 1);
        chunk.WriteOpCode(OpCode.NilCoalesce, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(7L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void NilCoalesce_BothNil_ReturnsNil() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.NilCoalesce, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void NilCoalesce_LeavesExactlyOneValueOnStack() {
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromString("default"));
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.NilCoalesce, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
    }

    // -------------------------------------------------------------------------
    // Jump — unconditional forward jump
    // -------------------------------------------------------------------------

    [Fact]
    public void Jump_SkipsInstructions_ExecutesCodeAfterTarget() {
        // Chunk layout:
        //   Jump 0x00 0x03    <- skip the next 3 bytes
        //   Nil               <- should be skipped
        //   Return            <- should be skipped (2 bytes with offset of Jump)
        //   Constant ci       <- should run
        //   Return
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromInt(99));

        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 2, 1);   // skip: Nil(1) + Return(1) = 2 bytes
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.Return, 1); // this Return is the skip target's Return, just padding
        // After jump:
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Nil was skipped; 99 is on the stack.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(99L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void Jump_ZeroOffset_FallsThrough() {
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromInt(1));
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 0, 1);  // zero offset: no skip
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    // -------------------------------------------------------------------------
    // JumpIfFalse — pops condition; jumps when false
    // -------------------------------------------------------------------------

    [Fact]
    public void JumpIfFalse_CondFalse_JumpsAndConditionPopped() {
        // Stack before: [false]; after jump: [99]
        var chunk = new Chunk();
        byte ciTrue = ConstByte(chunk, GrobValue.FromBool(false));
        byte ciSkip = ConstByte(chunk, GrobValue.FromInt(0));     // skipped
        byte ciAfter = ConstByte(chunk, GrobValue.FromInt(99));

        // Push false
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciTrue, 1);
        // JumpIfFalse over the "0" constant load (Constant + 1-byte operand = 2 bytes)
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 2, 1);
        // This is skipped:
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciSkip, 1);
        // Land here:
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciAfter, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Condition was popped; skipped 0; 99 is the only value.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(99L, vm.Stack.Peek().AsInt());
    }

    [Fact]
    public void JumpIfFalse_CondTrue_FallsThrough_ConditionPopped() {
        var chunk = new Chunk();
        byte ciCond = ConstByte(chunk, GrobValue.FromBool(true));
        byte ciValue = ConstByte(chunk, GrobValue.FromInt(7));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciCond, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 2, 1);   // 2 bytes to jump over the Constant below
        // Fall-through path:
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciValue, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Condition popped; 7 pushed.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(7L, vm.Stack.Peek().AsInt());
    }

    // -------------------------------------------------------------------------
    // JumpIfTrue — peeks condition (does NOT pop); jumps when true
    // -------------------------------------------------------------------------

    [Fact]
    public void JumpIfTrue_CondTrue_Jumps_ConditionRemainsOnStack() {
        // Designed for OR short-circuit where condition is also the result.
        // Stack before: [true]; after jump: [true] still (peeked, not popped).
        var chunk = new Chunk();
        byte ciTrue = ConstByte(chunk, GrobValue.FromBool(true));
        byte ciSkip = ConstByte(chunk, GrobValue.FromInt(42));  // skipped

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciTrue, 1);
        // JumpIfTrue skips over Constant+operand (2 bytes)
        chunk.WriteOpCode(OpCode.JumpIfTrue, 1);
        WriteJumpOffset(chunk, 2, 1);
        // This is skipped:
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciSkip, 1);
        // Land here (true is still on the stack from peek):
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // true remains on stack (was peeked, not popped); 42 was skipped.
        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().AsBool());
    }

    [Fact]
    public void JumpIfTrue_CondFalse_FallsThrough_ConditionRemainsOnStack() {
        var chunk = new Chunk();
        byte ciFalse = ConstByte(chunk, GrobValue.FromBool(false));
        byte ciValue = ConstByte(chunk, GrobValue.FromInt(55));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciFalse, 1);
        chunk.WriteOpCode(OpCode.JumpIfTrue, 1);
        WriteJumpOffset(chunk, 0, 1);   // 0-byte skip — just confirms fall-through
        // Fall-through: false still on stack; push 55 as the OR result.
        chunk.WriteOpCode(OpCode.Pop, 1);   // discard false
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciValue, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(55L, vm.Stack.Peek().AsInt());
    }

    // -------------------------------------------------------------------------
    // GetProperty — nil receiver → E5201
    // -------------------------------------------------------------------------

    [Fact]
    public void GetProperty_NilReceiver_ThrowsGrobRuntimeException_WithE5201() {
        var chunk = new Chunk();
        byte nameIdx = ConstByte(chunk, GrobValue.FromString("member"));
        chunk.WriteOpCode(OpCode.Nil, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        GrobRuntimeException ex = Assert.Throws<GrobRuntimeException>(() => vm.Run(chunk));
        Assert.Equal("E5201", ex.Code);
    }

    [Fact]
    public void GetProperty_NonNilReceiver_ThrowsGrobInternalException_NotE5201() {
        // Non-nil receiver is not yet implemented (Sprint 5). The VM must NOT
        // raise E5201 — that code is reserved for the nil-dereference case.
        var chunk = new Chunk();
        byte ciReceiver = ConstByte(chunk, GrobValue.FromInt(42));
        byte nameIdx = ConstByte(chunk, GrobValue.FromString("member"));
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciReceiver, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }

    // -------------------------------------------------------------------------
    // ?. bytecode pattern — hand-built to verify short-circuit at runtime
    // -------------------------------------------------------------------------

    [Fact]
    public void OptionalChain_NilReceiver_ReturnsNil_WithoutHittingGetProperty() {
        // Simulates the bytecode emitted by the compiler for `x?.member` where x is nil.
        //
        // Pattern:
        //   [receiver]            <- push x
        //   IsNil                 <- [receiver, isNilBool]; peeks
        //   JumpIfTrue nil_label  <- if true, jump (condition stays on stack)
        //   Pop                   <- (non-nil path) discard false
        //   GetProperty nameIdx   <- would run on non-nil receiver
        //   Jump end_label
        //   nil_label: Pop        <- (nil path) discard true, receiver (nil) stays
        //   end_label: (Return)
        var chunk = new Chunk();
        byte nameIdx = ConstByte(chunk, GrobValue.FromString("member"));

        // Push nil receiver.
        chunk.WriteOpCode(OpCode.Nil, 1);

        // IsNil: stack = [nil, true]
        chunk.WriteOpCode(OpCode.IsNil, 1);

        // JumpIfTrue nil_label (peeked — true stays on stack, jumps to nil_label).
        // We need to patch this offset after we know the skip distance.
        int jumpIfTruePatch = chunk.Count;
        chunk.WriteOpCode(OpCode.JumpIfTrue, 1);
        chunk.WriteByte(0xFF, 1); // placeholder hi
        chunk.WriteByte(0xFF, 1); // placeholder lo

        // Non-nil path (skipped in this test):
        chunk.WriteOpCode(OpCode.Pop, 1);                               // discard false
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1); // would throw E5201 on nil
        // Jump end_label:
        int jumpPatch = chunk.Count;
        chunk.WriteOpCode(OpCode.Jump, 1);
        chunk.WriteByte(0xFF, 1); chunk.WriteByte(0xFF, 1);

        // nil_label: discard the true bool; receiver (nil) remains beneath.
        int nilLabel = chunk.Count;
        chunk.WriteOpCode(OpCode.Pop, 1);  // discard bool(true)

        // end_label:
        int endLabel = chunk.Count;
        chunk.WriteOpCode(OpCode.Return, 1);

        // Patch JumpIfTrue: distance from (jumpIfTruePatch + 3) to nilLabel.
        int jitOffset = nilLabel - (jumpIfTruePatch + 3);
        chunk.PatchByte(jumpIfTruePatch + 1, (byte)(jitOffset >> 8));
        chunk.PatchByte(jumpIfTruePatch + 2, (byte)(jitOffset & 0xFF));

        // Patch Jump: distance from (jumpPatch + 3) to endLabel.
        int jOffset = endLabel - (jumpPatch + 3);
        chunk.PatchByte(jumpPatch + 1, (byte)(jOffset >> 8));
        chunk.PatchByte(jumpPatch + 2, (byte)(jOffset & 0xFF));

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // The nil receiver itself remains on the stack after the nil path.
        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().IsNil);
    }

    [Fact]
    public void OptionalChain_NonNilReceiver_ReachesGetProperty_ThrowsGrobInternalException() {
        // Same pattern but with a non-nil receiver — the non-nil path runs
        // GetProperty which raises GrobInternalException (not yet implemented,
        // Sprint 5).  Confirms the JumpIfTrue does NOT fire.
        var chunk = new Chunk();
        byte ciReceiver = ConstByte(chunk, GrobValue.FromInt(42));
        byte nameIdx = ConstByte(chunk, GrobValue.FromString("member"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ciReceiver, 1);
        chunk.WriteOpCode(OpCode.IsNil, 1);

        int jumpIfTruePatch = chunk.Count;
        chunk.WriteOpCode(OpCode.JumpIfTrue, 1);
        chunk.WriteByte(0xFF, 1);
        chunk.WriteByte(0xFF, 1);

        // Non-nil path:
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.GetProperty, 1); chunk.WriteByte(nameIdx, 1);

        int jumpPatch = chunk.Count;
        chunk.WriteOpCode(OpCode.Jump, 1);
        chunk.WriteByte(0xFF, 1); chunk.WriteByte(0xFF, 1);

        // nil_label:
        int nilLabel = chunk.Count;
        chunk.WriteOpCode(OpCode.Pop, 1);

        int endLabel = chunk.Count;
        chunk.WriteOpCode(OpCode.Return, 1);

        int jitOffset = nilLabel - (jumpIfTruePatch + 3);
        chunk.PatchByte(jumpIfTruePatch + 1, (byte)(jitOffset >> 8));
        chunk.PatchByte(jumpIfTruePatch + 2, (byte)(jitOffset & 0xFF));

        int jOffset = endLabel - (jumpPatch + 3);
        chunk.PatchByte(jumpPatch + 1, (byte)(jOffset >> 8));
        chunk.PatchByte(jumpPatch + 2, (byte)(jOffset & 0xFF));

        var (vm, _) = NewVm();
        Assert.Throws<GrobInternalException>(() => vm.Run(chunk));
    }
}
