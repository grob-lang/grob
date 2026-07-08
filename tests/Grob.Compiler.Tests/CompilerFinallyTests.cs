using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Compiler / disassembler-shape tests for Sprint 7 Increment C — the
/// compiler-emitted <c>finally</c> copy at the try/catch normal-completion
/// convergence point (the existing <c>exitJumps</c> target in
/// <c>Compiler.VisitTry</c>, right before <see cref="OpCode.TryEnd"/>). Covers
/// normal completion of the try body and normal completion of every catch body
/// (§27's "when it runs", items 1 and 3) — a single compiled copy, since both
/// paths already converge there. The <c>return</c>/<c>break</c>/<c>continue</c>
/// emission chains and the VM's <c>finallyOffset</c> exceptional-path arm are
/// covered elsewhere (the emission-chain design is escalated to
/// <c>grob-unwind-specialist</c>). Decoded from the raw instruction stream —
/// <c>Grob.Compiler.Tests</c> does not reference <c>Grob.Vm</c>.
/// </summary>
public sealed class CompilerFinallyTests {
    private static Chunk CompileSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return chunk;
    }

    private readonly record struct Instr(int Offset, OpCode Op, int Arg);

    private static List<Instr> Decode(Chunk chunk) {
        var result = new List<Instr>();
        int offset = 0;
        while (offset < chunk.Count) {
            int here = offset;
            var op = (OpCode)chunk.ReadByte(offset++);
            int arg = 0;
            switch (op) {
                case OpCode.ConstantLong:
                case OpCode.Jump:
                case OpCode.JumpIfFalse:
                case OpCode.JumpIfTrue:
                case OpCode.Loop:
                    arg = (chunk.ReadByte(offset) << 8) | chunk.ReadByte(offset + 1);
                    offset += 2;
                    break;
                case OpCode.Constant:
                case OpCode.DefineGlobal:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.GetLocal:
                case OpCode.SetLocal:
                case OpCode.Call:
                case OpCode.NewStruct:
                case OpCode.TryBegin:
                case OpCode.PopN:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break;
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    /// <summary>Resolves a forward-<see cref="OpCode.Jump"/> instruction's target
    /// offset, mirroring <c>Compiler.PatchJump</c>'s offset arithmetic.</summary>
    private static int JumpTarget(Instr jump) => jump.Offset + 3 + jump.Arg;

    /// <summary>
    /// Locates every <see cref="OpCode.Constant"/>/<see cref="OpCode.ConstantLong"/>
    /// instruction pushing the int literal <paramref name="marker"/> — used as a
    /// distinctive, mechanism-agnostic marker for "this finally body ran here",
    /// since the emission-chain design (compiler-side enclosing-region stack,
    /// slot accounting) is not this test's concern, only the observable count and
    /// ordering of each finally's compiled copy.
    /// </summary>
    private static List<Instr> FindIntConstant(Chunk chunk, List<Instr> instrs, long marker) =>
        instrs.Where(i => (i.Op == OpCode.Constant || i.Op == OpCode.ConstantLong)
            && chunk.ReadConstant(i.Arg).IsInt && chunk.ReadConstant(i.Arg).AsInt() == marker).ToList();

    [Fact]
    public void Finally_NoFinally_RegionFinallyOffsetIsSentinel() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        TryRegion region = chunk.GetTryRegion(instrs.Single(i => i.Op == OpCode.TryBegin).Arg);

        Assert.Equal(-1, region.FinallyOffset);
    }

    [Fact]
    public void Finally_WithCatch_RegionFinallyOffsetIsSet() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 } finally { z := 3 }
            """);

        List<Instr> instrs = Decode(chunk);
        TryRegion region = chunk.GetTryRegion(instrs.Single(i => i.Op == OpCode.TryBegin).Arg);

        Assert.True(region.FinallyOffset >= 0, "expected FinallyOffset to be set when a finally is present");
    }

    [Fact]
    public void Finally_EmittedAtConvergencePoint_BeforeTryEnd() {
        // The try body's own skip-catches Jump, and every non-last catch's own
        // exit Jump, converge on ONE point (the existing 'exitJumps' target).
        // The finally body is emitted once there, immediately before TryEnd —
        // covering both normal try completion and normal catch completion.
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 } finally { z := 3 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBeginInstr = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBeginInstr.Arg);
        Instr tryEnd = instrs.Single(i => i.Op == OpCode.TryEnd);
        Instr skipCatchesJump = instrs.Single(i => i.Op == OpCode.Jump && i.Offset > tryBeginInstr.Offset
            && i.Offset < region.Handlers[0].HandlerOffset);

        Assert.Equal(region.FinallyOffset, JumpTarget(skipCatchesJump));
        Assert.True(region.FinallyOffset > region.Handlers[0].HandlerOffset,
            "the finally body must be emitted after the catch body, at the convergence point");
        Assert.True(region.FinallyOffset < tryEnd.Offset,
            "the finally body must be emitted before TryEnd");
    }

    [Fact]
    public void Finally_OnlyFinally_NoCatch_EmittedBeforeTryEnd() {
        // No catches means no skip-catches Jump at all (existing behaviour) — the
        // try body falls straight through into the finally body, then TryEnd.
        Chunk chunk = CompileSource("try { x := 1 } finally { y := 2 }\n");

        List<Instr> instrs = Decode(chunk);
        Instr tryBeginInstr = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBeginInstr.Arg);
        Instr tryEnd = instrs.Single(i => i.Op == OpCode.TryEnd);

        Assert.Equal(region.EndOffset, region.FinallyOffset);
        Assert.True(region.FinallyOffset < tryEnd.Offset);
        Assert.DoesNotContain(instrs, i => i.Op == OpCode.Jump);
    }

    [Fact]
    public void Finally_MultipleCatches_AllConvergeOnSameFinallyOffset() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 } catch f { z := 3 } finally { w := 4 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBeginInstr = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBeginInstr.Arg);

        // The try body's skip-catches Jump and the first (non-last) catch's own
        // exit Jump both land on the finally.
        List<Instr> jumps = instrs.Where(i => i.Op == OpCode.Jump).ToList();
        Assert.Equal(2, jumps.Count);
        Assert.All(jumps, j => Assert.Equal(region.FinallyOffset, JumpTarget(j)));
    }

    /// <summary>
    /// Locates a top-level named function's own compiled <see cref="Chunk"/> — the
    /// <see cref="OpCode.Constant"/>/<see cref="OpCode.ConstantLong"/> instruction
    /// immediately preceding the <see cref="OpCode.DefineGlobal"/> that binds
    /// <paramref name="name"/> holds the <c>BytecodeFunction</c> constant
    /// (<c>Compiler.VisitFnDecl</c>: <c>EmitConstant</c> then <c>DefineGlobal</c>).
    /// A named function compiles to its own <see cref="Chunk"/>, never inlined into
    /// the top-level chunk — a <c>return</c> inside it is invisible to a caller
    /// decoding the top-level chunk alone.
    /// </summary>
    private static Chunk NamedFunctionChunk(Chunk topLevel, List<Instr> topLevelInstrs, string name) {
        Instr defineGlobal = topLevelInstrs.Single(i =>
            i.Op == OpCode.DefineGlobal && topLevel.ReadConstant(i.Arg).AsString() == name);
        Instr fnConstant = topLevelInstrs.Last(i =>
            (i.Op == OpCode.Constant || i.Op == OpCode.ConstantLong) && i.Offset < defineGlobal.Offset);
        GrobFunction fn = topLevel.ReadConstant(fnConstant.Arg).AsFunction();
        Assert.IsType<BytecodeFunction>(fn);
        return ((BytecodeFunction)fn).Bytecode;
    }

    // -----------------------------------------------------------------------
    // Pending-finally-chain emission at return/break/continue (escalated to
    // grob-unwind-specialist — sub-problem 1). These assertions are
    // mechanism-agnostic: they check the observable ordering of each
    // finally's compiled copy via a distinctive int-literal marker, not any
    // particular slot-accounting implementation. Count assertions are framed
    // as "at least one copy exists at the crossing site" rather than an exact
    // total, since the pre-existing normal-completion convergence copy
    // (Increment C's already-built baseline) is emitted unconditionally
    // regardless of whether the try body's own bytecode can reach it — a try
    // body whose only statement is the early exit still gets that baseline
    // copy, so an exact count conflates it with the new crossing copy. The
    // "must NOT run" tests use an exact count instead, since any correctly
    // scoped implementation contributes only that same baseline, once.
    // -----------------------------------------------------------------------

    [Fact]
    public void Return_CrossingOneTryFinally_EmitsFinallyBodyBeforeReturn() {
        Chunk chunk = CompileSource("""
            fn f(): int {
                try { return 1 } finally { y := 777 }
                return 2
            }
            """);

        List<Instr> topLevelInstrs = Decode(chunk);
        Chunk fnChunk = NamedFunctionChunk(chunk, topLevelInstrs, "f");
        List<Instr> fnInstrs = Decode(fnChunk);
        List<Instr> marker = FindIntConstant(fnChunk, fnInstrs, 777);
        Instr firstReturn = fnInstrs.First(i => i.Op == OpCode.Return);

        Assert.Contains(marker, m => m.Offset < firstReturn.Offset);
    }

    [Fact]
    public void Break_DirectlyInsideTryFinally_EmitsFinallyBodyBeforeBreakJump() {
        Chunk chunk = CompileSource("""
            while (true) {
                try { break } finally { y := 777 }
            }
            """);

        List<Instr> instrs = Decode(chunk);
        List<Instr> marker = FindIntConstant(chunk, instrs, 777);
        // No catches on this try (no exitJumps) and no other forward Jump in this
        // source — the only OpCode.Jump is the break's own.
        Instr breakJump = instrs.Single(i => i.Op == OpCode.Jump);

        Assert.Contains(marker, m => m.Offset < breakJump.Offset);
    }

    [Fact]
    public void Continue_DirectlyInsideTryFinally_EmitsFinallyBodyBeforeContinueLoop() {
        Chunk chunk = CompileSource("""
            while (true) {
                try { continue } finally { y := 777 }
            }
            """);

        List<Instr> instrs = Decode(chunk);
        List<Instr> marker = FindIntConstant(chunk, instrs, 777);
        // 'continue' on a while (no forward-continue form) emits a backward
        // OpCode.Loop targeting the condition — it appears in the byte stream
        // before the loop's own end-of-body backward Loop (which follows the
        // rest of the — unreachable, since continue always transfers — body).
        Instr continueLoop = instrs.First(i => i.Op == OpCode.Loop);

        Assert.Contains(marker, m => m.Offset < continueLoop.Offset);
    }

    [Fact]
    public void Return_CrossingNestedTryFinally_EmitsBothBodiesInnerThenOuterBeforeReturn() {
        Chunk chunk = CompileSource("""
            fn f(): int {
                try {
                    try { return 1 } finally { y := 111 }
                } finally { z := 222 }
                return 2
            }
            """);

        List<Instr> topLevelInstrs = Decode(chunk);
        Chunk fnChunk = NamedFunctionChunk(chunk, topLevelInstrs, "f");
        List<Instr> fnInstrs = Decode(fnChunk);
        List<Instr> inner = FindIntConstant(fnChunk, fnInstrs, 111);
        List<Instr> outer = FindIntConstant(fnChunk, fnInstrs, 222);
        Instr firstReturn = fnInstrs.First(i => i.Op == OpCode.Return);

        // At least one copy of each, at the crossing site: the inner finally's
        // crossing copy strictly before the outer finally's crossing copy,
        // which is strictly before the Return it precedes.
        Assert.Contains(inner, m => m.Offset < firstReturn.Offset);
        Assert.Contains(outer, m => m.Offset < firstReturn.Offset);
        int innerCrossing = inner.Where(m => m.Offset < firstReturn.Offset).Min(m => m.Offset);
        int outerCrossing = outer.Where(m => m.Offset < firstReturn.Offset).Min(m => m.Offset);
        Assert.True(innerCrossing < outerCrossing, "inner finally must run before outer finally");
    }

    [Fact]
    public void Break_TargetingLoopOutsideOneInterveningTryFinally_RunsThatFinallyAtBreakSite() {
        // The (only) while loop is the break's target; the try wraps the break
        // directly, so breaking crosses — and must run — this try's finally.
        Chunk chunk = CompileSource("""
            while (true) {
                try { if (true) { break } } finally { y := 333 }
            }
            """);

        List<Instr> instrs = Decode(chunk);
        List<Instr> marker = FindIntConstant(chunk, instrs, 333);
        // The bare 'if' with no else emits only JumpIfFalse (patched to fall
        // through); the break's own forward Jump is the only OpCode.Jump.
        Instr breakJump = instrs.Single(i => i.Op == OpCode.Jump);

        Assert.Contains(marker, m => m.Offset < breakJump.Offset);
    }

    [Fact]
    public void Break_TargetingLoopNestedInsideTryFinally_DoesNotAddAnExtraOuterFinallyCopy() {
        // The break targets the INNER while (nearest enclosing loop), which is
        // nested inside the try — the break never leaves the try/finally
        // construct, so no crossing copy is added. Only the pre-existing
        // normal-completion convergence copy (Increment C baseline) remains —
        // exactly once.
        Chunk chunk = CompileSource("""
            try {
                while (true) {
                    break
                }
            } finally { y := 444 }
            """);

        List<Instr> instrs = Decode(chunk);
        List<Instr> marker = FindIntConstant(chunk, instrs, 444);

        Assert.Single(marker);
    }

    [Fact]
    public void Break_TargetingOuterLoopThroughOneInterveningTryFinally_RunsOnlyThatOne() {
        // Two nested loops, each with its own try/finally. The break targets
        // the INNER loop, so the inner try's finally (f1) gets a crossing copy
        // at the break site (in addition to its own baseline convergence
        // copy) — but the outer try's finally (f2) is never crossed, so it
        // keeps only its single baseline convergence copy.
        Chunk chunk = CompileSource("""
            while (true) {
                try {
                    while (true) {
                        try { break } finally { f1 := 111 }
                    }
                } finally { f2 := 222 }
            }
            """);

        List<Instr> instrs = Decode(chunk);
        List<Instr> f1 = FindIntConstant(chunk, instrs, 111);
        List<Instr> f2 = FindIntConstant(chunk, instrs, 222);
        Instr breakJump = instrs.Single(i => i.Op == OpCode.Jump);

        Assert.Contains(f1, m => m.Offset < breakJump.Offset);
        Assert.Single(f2);
    }
}
