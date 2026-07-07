using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Compiler / disassembler-shape tests for Sprint 7 Increment B — <c>try</c>/<c>catch</c>
/// emission. A <c>try</c> compiles to <see cref="OpCode.TryBegin"/> (operand: handler-table
/// index), the body, a backpatched <see cref="OpCode.Jump"/> over the catch bodies, each
/// catch body in source order, then <see cref="OpCode.TryEnd"/>. The handler table records
/// the region's bounds and ordered handlers. Decoded from the raw instruction stream
/// (mirroring <c>CompilerThrowTests</c>'s <c>Decode</c> helper) rather than via
/// <c>Grob.Vm.Disassembler</c> — <c>Grob.Compiler.Tests</c> does not reference <c>Grob.Vm</c>,
/// preserving the DAG boundary in test code too.
/// </summary>
public sealed class CompilerTryCatchTests {
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

    [Fact]
    public void TryCatch_DisassemblesTo_TryBegin_Body_Jump_CatchBody_TryEnd() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        int tryBegin = instrs.FindIndex(i => i.Op == OpCode.TryBegin);
        int jump = instrs.FindIndex(i => i.Op == OpCode.Jump);
        int tryEnd = instrs.FindIndex(i => i.Op == OpCode.TryEnd);

        Assert.True(tryBegin >= 0, "expected a TryBegin instruction");
        Assert.True(jump > tryBegin, "the skip-catches Jump must follow TryBegin");
        Assert.True(tryEnd > jump, "TryEnd must follow the skip-catches Jump");
    }

    [Fact]
    public void TryCatch_HandlerTable_RecordsBoundsExcludingCatchBodies() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBegin = instrs.Single(i => i.Op == OpCode.TryBegin);
        Instr jump = instrs.Single(i => i.Op == OpCode.Jump);

        TryRegion region = chunk.GetTryRegion(tryBegin.Arg);
        Assert.Equal(tryBegin.Offset + 2, region.StartOffset); // opcode byte + 1-byte operand
        Assert.Equal(jump.Offset, region.EndOffset); // body ends where the skip-jump starts
    }

    [Fact]
    public void TryCatch_HandlerTable_TypedCatch_RecordsExactLeafName() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBegin = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBegin.Arg);

        CatchHandler handler = Assert.Single(region.Handlers);
        Assert.False(handler.IsCatchAll);
        Assert.Equal(["IoError"], handler.MatchTypeNames);
    }

    [Fact]
    public void TryCatch_HandlerTable_CatchAll_HasNoMatchTypeNames() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch e { y := 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBegin = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBegin.Arg);

        CatchHandler handler = Assert.Single(region.Handlers);
        Assert.True(handler.IsCatchAll);
        Assert.Empty(handler.MatchTypeNames);
    }

    [Fact]
    public void TryCatch_HandlerTable_GrobErrorRootCatch_MatchesAllElevenNames() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: GrobError) { y := 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBegin = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBegin.Arg);

        CatchHandler handler = Assert.Single(region.Handlers);
        Assert.Equal(11, handler.MatchTypeNames.Count);
        Assert.Contains("GrobError", handler.MatchTypeNames);
        Assert.Contains("IoError", handler.MatchTypeNames);
    }

    [Fact]
    public void TryCatch_MultipleHandlers_RecordedInSourceOrderWithDistinctOffsets() {
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 } catch f { z := 3 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBegin = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBegin.Arg);

        Assert.Equal(2, region.Handlers.Count);
        Assert.False(region.Handlers[0].IsCatchAll);
        Assert.Equal(["IoError"], region.Handlers[0].MatchTypeNames);
        Assert.True(region.Handlers[1].IsCatchAll);
        Assert.True(region.Handlers[1].HandlerOffset > region.Handlers[0].HandlerOffset);
    }

    [Fact]
    public void TryCatch_SiblingHandlers_ShareTheSameBindingSlot() {
        // Exactly one handler ever runs per throw, so reusing the slot across
        // sibling catch clauses on the same region is correct, not a collision.
        Chunk chunk = CompileSource("""
            try { x := 1 } catch (e: IoError) { y := 2 } catch f { z := 3 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr tryBegin = instrs.Single(i => i.Op == OpCode.TryBegin);
        TryRegion region = chunk.GetTryRegion(tryBegin.Arg);

        Assert.Equal(region.Handlers[0].BindingSlot, region.Handlers[1].BindingSlot);
    }
}
