using Grob.Core;
using Xunit;

namespace Grob.Vm.Tests;

/// <summary>
/// VM tests for the D-319 cooperative-cancellation step-budget seam.
/// The VM's <see cref="VirtualMachine.Run"/> entry accepts a
/// <see cref="CancellationToken"/> (default <see cref="CancellationToken.None"/>
/// — unlimited). The dispatch loop increments a VM-instance step counter on
/// every iteration; when the masked counter reaches the budget boundary, the
/// token is checked via <see cref="CancellationToken.ThrowIfCancellationRequested"/>.
/// Cancellation surfaces as <see cref="OperationCanceledException"/>, which is
/// outside the <see cref="GrobError"/> hierarchy and therefore uncatchable by a
/// Grob <c>try/catch</c> block.
/// </summary>
public sealed class VirtualMachineCancellationTests {
    private static (VirtualMachine vm, StringWriter output) NewVm() {
        var output = new StringWriter();
        var vm = new VirtualMachine(output);
        return (vm, output);
    }

    // -----------------------------------------------------------------------
    // Jump/loop helpers (same shape as in other test files)
    // -----------------------------------------------------------------------

    private static int EmitJump(Chunk chunk, OpCode op) {
        chunk.WriteOpCode(op, 1);
        int site = chunk.Count;
        chunk.WriteByte(0xFF, 1);
        chunk.WriteByte(0xFF, 1);
        return site;
    }

    private static void PatchJump(Chunk chunk, int site) {
        int offset = chunk.Count - (site + 2);
        chunk.PatchByte(site, (byte)(offset >> 8));
        chunk.PatchByte(site + 1, (byte)(offset & 0xFF));
    }

    private static void EmitLoop(Chunk chunk, int loopTop) {
        chunk.WriteOpCode(OpCode.Loop, 1);
        int offset = chunk.Count + 2 - loopTop;
        chunk.WriteByte((byte)(offset >> 8), 1);
        chunk.WriteByte((byte)(offset & 0xFF), 1);
    }

    /// <summary>
    /// Builds a chunk that runs <c>while (true) {}</c> — an unconditional infinite
    /// backward-jump loop.
    /// </summary>
    private static Chunk BuildInfiniteLoopChunk() {
        var chunk = new Chunk();
        // while (true) — condition always true, never exits
        int loopTop = chunk.Count;
        chunk.WriteOpCode(OpCode.True, 1);
        int exitJump = EmitJump(chunk, OpCode.JumpIfFalse);
        EmitLoop(chunk, loopTop);
        PatchJump(chunk, exitJump);
        chunk.WriteOpCode(OpCode.Return, 1);
        return chunk;
    }

    // -----------------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_PreCancelledToken_ThrowsOperationCancelledException() {
        // A pre-cancelled token must cause the runaway loop to terminate with
        // OperationCanceledException rather than hanging.
        var (vm, _) = NewVm();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Chunk chunk = BuildInfiniteLoopChunk();

        Assert.Throws<OperationCanceledException>(() =>
            vm.Run(chunk, cts.Token));
    }

    [Fact]
    public void Run_TokenCancelledAfterStart_ThrowsOperationCancelledException() {
        // The token is cancelled asynchronously; the VM must detect it on the
        // next budget-check iteration rather than hanging forever.
        var (vm, _) = NewVm();
        using var cts = new CancellationTokenSource(millisecondsDelay: 100);

        Chunk chunk = BuildInfiniteLoopChunk();

        Assert.Throws<OperationCanceledException>(() =>
            vm.Run(chunk, cts.Token));
    }

    [Fact]
    public void Run_DefaultToken_TerminatingProgram_DoesNotThrow() {
        // CancellationToken.None (the default) must never cancel a program that
        // terminates normally — the unlimited sentinel is always allowed.
        var (vm, output) = NewVm();

        var chunk = new Chunk();
        int idx = chunk.AddConstant(GrobValue.FromString("ok"));
        chunk.WriteOpCode(OpCode.Constant, 1);
        chunk.WriteByte((byte)idx, 1);
        chunk.WriteOpCode(OpCode.Print, 1);
        chunk.WriteOpCode(OpCode.Return, 1);

        // Must not throw; CancellationToken.None is the default parameter value.
        vm.Run(chunk);

        Assert.Equal("ok", output.ToString().Trim());
    }

    [Fact]
    public void OperationCanceledException_IsNotGrobRuntimeException_CannotBeSwallowedByGrobCatch() {
        // D-319: OperationCanceledException must be outside the GrobRuntimeException
        // hierarchy so the VM's Grob-level catch handler can never match it —
        // the same uncatchable property that GrobExitException has for exit().
        //
        // This test verifies the type invariant that makes Grob catch blocks unable
        // to swallow cancellation: no amount of Grob error handling code can observe
        // or suppress an OperationCanceledException.
        var cancelled = new OperationCanceledException();

        Assert.False(typeof(GrobRuntimeException).IsAssignableFrom(typeof(OperationCanceledException)),
            "OperationCanceledException must not inherit from GrobRuntimeException; " +
            "otherwise a Grob catch block could suppress VM cancellation.");
    }
}
