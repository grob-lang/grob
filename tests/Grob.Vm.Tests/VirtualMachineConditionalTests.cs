using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM dispatch-loop tests for Sprint 4 Increment A — conditional execution.
/// All chunks are hand-constructed; no compiler dependency.
/// </summary>
/// <remarks>
/// Tests verify that the VM correctly executes the jump-based patterns emitted
/// by the compiler for <c>if</c>/<c>else</c>, <c>&amp;&amp;</c>/<c>||</c>
/// short-circuit and the ternary <c>?:</c>.  The jump opcodes (<see cref="OpCode.Jump"/>,
/// <see cref="OpCode.JumpIfFalse"/>, <see cref="OpCode.JumpIfTrue"/>) were
/// implemented in Sprint 3D and are fully dispatched; these tests exercise them
/// in the new conditional patterns.
/// </remarks>
public sealed class VirtualMachineConditionalTests {
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
    /// Writes a 2-byte big-endian forward-jump offset (the operand format used
    /// by <see cref="OpCode.Jump"/>, <see cref="OpCode.JumpIfFalse"/> and
    /// <see cref="OpCode.JumpIfTrue"/>).
    /// </summary>
    private static void WriteJumpOffset(Chunk chunk, int offset, int line) {
        chunk.WriteByte((byte)(offset >> 8), line);
        chunk.WriteByte((byte)(offset & 0xFF), line);
    }

    // -----------------------------------------------------------------------
    // if — true-branch execution
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the condition is <c>true</c>, the then-branch executes (not the else-branch).
    /// <para>
    /// Chunk shape: True → JumpIfFalse → Constant(42) → Jump → Constant(99) → Return.
    /// Expected result: 42 on the stack.
    /// </para>
    /// </summary>
    [Fact]
    public void IfTrue_ExecutesThenBranch() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));
        byte ci99 = ConstByte(chunk, GrobValue.FromInt(99));

        // [0]  True
        // [1]  JumpIfFalse [2,3]
        // [4]  Constant ci42     ← then-branch
        // [6]  Jump [7,8]
        // [9]  Constant ci99     ← else-branch
        // [11] Return
        chunk.WriteOpCode(OpCode.True, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);   // from pos 4 to pos 9 = 5 bytes (Constant+idx+Jump+2)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 2, 1);   // from pos 9 to pos 11 = 2 bytes (Constant+idx)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci99, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // if — false-branch execution
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the condition is <c>false</c>, the else-branch executes (not the then-branch).
    /// </summary>
    [Fact]
    public void IfFalse_ExecutesElseBranch() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));
        byte ci99 = ConstByte(chunk, GrobValue.FromInt(99));

        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);   // skip then-branch (Constant+idx+Jump+2 = 5)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 2, 1);   // skip else-branch (Constant+idx = 2)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci99, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(99L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // if — no-else fall-through
    // -----------------------------------------------------------------------

    /// <summary>
    /// When a condition is <c>false</c> and there is no else-block, execution falls
    /// through cleanly with nothing pushed by the then-branch.
    /// </summary>
    [Fact]
    public void IfFalse_NoElse_FallsThrough() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));

        // [0]  False
        // [1]  JumpIfFalse [2,3]  → jumps over Constant+idx (2 bytes)
        // [4]  Constant ci42      ← skipped
        // [6]  Return
        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 2, 1);   // skip Constant+idx = 2 bytes
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(0, vm.Stack.Count);
    }

    // -----------------------------------------------------------------------
    // && — short-circuit when left is false
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the left operand of <c>&amp;&amp;</c> is <c>false</c>, <see cref="OpCode.JumpIfFalse"/>
    /// pops the left value and jumps directly to the synthesised <c>false</c> result.
    /// The right-operand code must NOT execute.
    /// </summary>
    [Fact]
    public void AndShortCircuit_FalseLeft_SkipsRight() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));

        // Shape: False → JumpIfFalse → Constant(42) → Jump → False → Return
        // [0]  False                 ← left operand
        // [1]  JumpIfFalse [2,3]     ← if false, jump to false_label
        // [4]  Constant ci42         ← right operand (should be skipped)
        // [6]  Jump [7,8]            ← skip false_label
        // [9]  False                 ← false_label: synthesised false result
        // [10] Return
        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);   // from pos 4 to pos 9 (Constant+idx+Jump+2 = 5)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 1, 1);   // from pos 10 to pos 11 — skip false_label's 1 byte? No...

        // Recalculating: after Jump at [6,7,8], next instr is at [9] = False.
        // Jump offset is from pos 9 to pos 10 = 1 byte (just past the False opcode).
        // But Return is at [10], so Jump should jump 1 byte to land at Return.
        // Wait: Jump lands at pos 9 + offset. If Jump's operand [7,8]=1, IP after reading = 9,
        // then 9 + 1 = 10. So False at [9], then Return at [10].
        // But we need Jump to land PAST the False at [9], i.e., at [10].
        // So offset = 10 - 9 = 1. ✓
        chunk.WriteOpCode(OpCode.False, 1);  // false_label [9]
        chunk.WriteOpCode(OpCode.Return, 1); // [10]

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // The right operand (42) was not pushed; only the synthesised false is on the stack.
        Assert.Equal(1, vm.Stack.Count);
        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // && — right operand evaluated when left is true
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the left operand of <c>&amp;&amp;</c> is <c>true</c>, the right operand
    /// is evaluated and its value becomes the result.
    /// </summary>
    [Fact]
    public void AndShortCircuit_TrueLeft_EvaluatesRight() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));

        // Same shape as above but with True at [0].
        chunk.WriteOpCode(OpCode.True, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 1, 1);
        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Left was true → right operand (42) reached → on stack.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // || — short-circuit when left is true
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the left operand of <c>||</c> is <c>true</c>, <see cref="OpCode.JumpIfTrue"/>
    /// peeks the value (leaves it on the stack) and jumps past the right operand.
    /// </summary>
    [Fact]
    public void OrShortCircuit_TrueLeft_SkipsRight() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));

        // [0]  True                 ← left operand
        // [1]  JumpIfTrue [2,3]     ← peek; if true, jump to end (peeks — leaves on stack)
        // [4]  Pop                  ← discard the peeked false value (not reached here)
        // [5]  Constant ci42        ← right operand (not reached here)
        // [7]  Return               ← end
        chunk.WriteOpCode(OpCode.True, 1);
        chunk.WriteOpCode(OpCode.JumpIfTrue, 1);
        WriteJumpOffset(chunk, 3, 1);   // from pos 4 to pos 7 (Pop=1, Constant+idx=2 → 3)
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // JumpIfTrue peeked true and jumped; left value (true) stays on the stack.
        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // || — right operand evaluated when left is false
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the left operand of <c>||</c> is <c>false</c>, the false value is
    /// discarded (via <see cref="OpCode.Pop"/>) and the right operand is evaluated.
    /// </summary>
    [Fact]
    public void OrShortCircuit_FalseLeft_EvaluatesRight() {
        var chunk = new Chunk();
        byte ci42 = ConstByte(chunk, GrobValue.FromInt(42));

        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.JumpIfTrue, 1);
        WriteJumpOffset(chunk, 3, 1);
        chunk.WriteOpCode(OpCode.Pop, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci42, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        // Left was false → Pop discarded it → right operand (42) is on the stack.
        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(42L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Ternary — true condition yields then-arm
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the ternary condition is <c>true</c>, the then-arm value is pushed
    /// and the else-arm is never evaluated.
    /// </summary>
    [Fact]
    public void Ternary_TrueCondition_YieldsThenValue() {
        var chunk = new Chunk();
        byte ci1 = ConstByte(chunk, GrobValue.FromInt(1));
        byte ci2 = ConstByte(chunk, GrobValue.FromInt(2));

        // [0]  True                 ← condition
        // [1]  JumpIfFalse [2,3]    → pos 9
        // [4]  Constant ci1         ← then-arm
        // [6]  Jump [7,8]           → pos 11
        // [9]  Constant ci2         ← else-arm
        // [11] Return
        chunk.WriteOpCode(OpCode.True, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);   // from pos 4 to pos 9 (Constant+idx+Jump+2 = 5)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci1, 1);
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 2, 1);   // from pos 9 to pos 11 (Constant+idx = 2)
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(1L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Ternary — false condition yields else-arm
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the ternary condition is <c>false</c>, the else-arm value is pushed
    /// and the then-arm is never evaluated.
    /// </summary>
    [Fact]
    public void Ternary_FalseCondition_YieldsElseValue() {
        var chunk = new Chunk();
        byte ci1 = ConstByte(chunk, GrobValue.FromInt(1));
        byte ci2 = ConstByte(chunk, GrobValue.FromInt(2));

        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.JumpIfFalse, 1);
        WriteJumpOffset(chunk, 5, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci1, 1);
        chunk.WriteOpCode(OpCode.Jump, 1);
        WriteJumpOffset(chunk, 2, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci2, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.Equal(2L, vm.Stack.Peek().AsInt());
    }

    // -----------------------------------------------------------------------
    // Boolean logic — Not / Equal / NotEqual
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="OpCode.Not"/> inverts <c>true</c> to <c>false</c>.
    /// Chunk: True → Not → Return.
    /// </summary>
    [Fact]
    public void Not_True_ReturnsFalse() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.True, 1);
        chunk.WriteOpCode(OpCode.Not, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.False(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.Not"/> inverts <c>false</c> to <c>true</c>.
    /// </summary>
    [Fact]
    public void Not_False_ReturnsTrue() {
        var chunk = new Chunk();
        chunk.WriteOpCode(OpCode.False, 1);
        chunk.WriteOpCode(OpCode.Not, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.Equal"/> returns <c>true</c> for two identical ints.
    /// Chunk: Constant(5) → Constant(5) → Equal → Return.
    /// </summary>
    [Fact]
    public void Equal_SameInts_ReturnsTrue() {
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.Equal"/> returns <c>false</c> for two different ints.
    /// </summary>
    [Fact]
    public void Equal_DifferentInts_ReturnsFalse() {
        var chunk = new Chunk();
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.Equal, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.False(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.NotEqual"/> returns <c>true</c> for two different ints.
    /// </summary>
    [Fact]
    public void NotEqual_DifferentInts_ReturnsTrue() {
        var chunk = new Chunk();
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.NotEqual, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.NotEqual"/> returns <c>false</c> for two identical ints.
    /// </summary>
    [Fact]
    public void NotEqual_SameInts_ReturnsFalse() {
        var chunk = new Chunk();
        byte ci = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci, 1);
        chunk.WriteOpCode(OpCode.NotEqual, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.Equal(1, vm.Stack.Count);
        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // Integer comparison opcodes
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="OpCode.LessInt"/>: 3 &lt; 5 is <c>true</c>.
    /// </summary>
    [Fact]
    public void LessInt_ThreeLessThanFive_ReturnsTrue() {
        var chunk = new Chunk();
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.LessInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.LessInt"/>: 5 &lt; 3 is <c>false</c>.
    /// </summary>
    [Fact]
    public void LessInt_FiveNotLessThanThree_ReturnsFalse() {
        var chunk = new Chunk();
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);
        chunk.WriteOpCode(OpCode.LessInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.False(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.LessEqualInt"/>: 5 &lt;= 5 is <c>true</c>.
    /// </summary>
    [Fact]
    public void LessEqualInt_Equal_ReturnsTrue() {
        var chunk = new Chunk();
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.LessEqualInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.GreaterInt"/>: 5 &gt; 3 is <c>true</c>.
    /// </summary>
    [Fact]
    public void GreaterInt_FiveGreaterThanThree_ReturnsTrue() {
        var chunk = new Chunk();
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);
        chunk.WriteOpCode(OpCode.GreaterInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.GreaterEqualInt"/>: 3 &gt;= 5 is <c>false</c>.
    /// </summary>
    [Fact]
    public void GreaterEqualInt_ThreeNotGreaterEqualFive_ReturnsFalse() {
        var chunk = new Chunk();
        byte ci3 = ConstByte(chunk, GrobValue.FromInt(3));
        byte ci5 = ConstByte(chunk, GrobValue.FromInt(5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci3, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(ci5, 1);
        chunk.WriteOpCode(OpCode.GreaterEqualInt, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.False(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // Float comparison opcodes
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="OpCode.LessFloat"/>: 1.5 &lt; 2.5 is <c>true</c>.
    /// </summary>
    [Fact]
    public void LessFloat_OnepointfiveLessThanTwopointfive_ReturnsTrue() {
        var chunk = new Chunk();
        byte cf15 = ConstByte(chunk, GrobValue.FromFloat(1.5));
        byte cf25 = ConstByte(chunk, GrobValue.FromFloat(2.5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf15, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf25, 1);
        chunk.WriteOpCode(OpCode.LessFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.LessEqualFloat"/>: 2.5 &lt;= 2.5 is <c>true</c>.
    /// </summary>
    [Fact]
    public void LessEqualFloat_Equal_ReturnsTrue() {
        var chunk = new Chunk();
        byte cf25 = ConstByte(chunk, GrobValue.FromFloat(2.5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf25, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf25, 1);
        chunk.WriteOpCode(OpCode.LessEqualFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.GreaterFloat"/>: 2.5 &gt; 1.5 is <c>true</c>.
    /// </summary>
    [Fact]
    public void GreaterFloat_TwopointfiveGreaterThanOnepointfive_ReturnsTrue() {
        var chunk = new Chunk();
        byte cf15 = ConstByte(chunk, GrobValue.FromFloat(1.5));
        byte cf25 = ConstByte(chunk, GrobValue.FromFloat(2.5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf25, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf15, 1);
        chunk.WriteOpCode(OpCode.GreaterFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.GreaterEqualFloat"/>: 2.5 &gt;= 2.5 is <c>true</c>.
    /// </summary>
    [Fact]
    public void GreaterEqualFloat_Equal_ReturnsTrue() {
        var chunk = new Chunk();
        byte cf25 = ConstByte(chunk, GrobValue.FromFloat(2.5));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf25, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(cf25, 1);
        chunk.WriteOpCode(OpCode.GreaterEqualFloat, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    // -----------------------------------------------------------------------
    // String comparison opcodes
    // -----------------------------------------------------------------------

    /// <summary>
    /// <see cref="OpCode.LessString"/>: "apple" &lt; "banana" is <c>true</c> (ordinal).
    /// </summary>
    [Fact]
    public void LessString_AppleLessThanBanana_ReturnsTrue() {
        var chunk = new Chunk();
        byte csA = ConstByte(chunk, GrobValue.FromString("apple"));
        byte csB = ConstByte(chunk, GrobValue.FromString("banana"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(csA, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(csB, 1);
        chunk.WriteOpCode(OpCode.LessString, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }

    /// <summary>
    /// <see cref="OpCode.GreaterString"/>: "banana" &gt; "apple" is <c>true</c> (ordinal).
    /// </summary>
    [Fact]
    public void GreaterString_BananaGreaterThanApple_ReturnsTrue() {
        var chunk = new Chunk();
        byte csA = ConstByte(chunk, GrobValue.FromString("apple"));
        byte csB = ConstByte(chunk, GrobValue.FromString("banana"));

        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(csB, 1);
        chunk.WriteOpCode(OpCode.Constant, 1); chunk.WriteByte(csA, 1);
        chunk.WriteOpCode(OpCode.GreaterString, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        var (vm, _) = NewVm();
        vm.Run(chunk);

        Assert.True(vm.Stack.Peek().AsBool());
    }
}
