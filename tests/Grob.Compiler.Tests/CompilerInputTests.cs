using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-assertion tests for Sprint 8 Increment C's <c>input()</c> compiler arm
/// (<c>Compiler.Expressions.cs VisitCall</c>). A 1-argument call needs no special
/// handling — it already compiles through the ordinary <c>GetGlobal</c> + argument +
/// <c>Call 1</c> shape any other identifier callee uses. A 0-argument call needs a
/// dedicated arm: the runtime native's own arity is 1 (<c>IoPlugin</c>), so the missing
/// prompt argument is filled with the constant <c>""</c> at the call site before the
/// existing <c>GetGlobal</c> + <c>Call 1</c> shape, mirroring how <c>exit()</c>'s
/// existing 0-or-1-arg statement-position handling fills a missing argument — but as its
/// own arm in the general expression path, since <c>input()</c> is not void and so
/// cannot reuse that void-statement-only special case.
/// </summary>
public sealed class CompilerInputTests {
    private static Chunk CompileSource(string source) {
        var bag = new DiagnosticBag();
        var tokens = Lexer.Scan(source, bag);
        var unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker produced errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return GrobCompiler.Compile(unit, bag);
    }

    private readonly record struct Instr(OpCode Op, int Arg);

    /// <summary>Decodes a chunk into a flat instruction list, resolving string-constant operands.</summary>
    private static List<Instr> Decode(Chunk chunk) {
        var result = new List<Instr>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset++);
            int arg = 0;
            switch (op) {
                case OpCode.Constant:
                case OpCode.GetGlobal:
                case OpCode.SetGlobal:
                case OpCode.DefineGlobal:
                case OpCode.Call:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                case OpCode.Return:
                case OpCode.Pop:
                case OpCode.Nil:
                    break;
                default:
                    throw new InvalidOperationException($"Decode: unhandled opcode {op} in this test's fixtures.");
            }
            result.Add(new Instr(op, arg));
        }
        return result;
    }

    [Fact]
    public void Input_ZeroArguments_FillsMissingPromptWithEmptyStringConstant() {
        Chunk chunk = CompileSource("readonly x := input()\n");

        List<Instr> instrs = Decode(chunk);
        // Order: GetGlobal "input" (callee), Constant "" (filled prompt), Call 1.
        int getGlobalIdx = instrs.FindIndex(i => i.Op == OpCode.GetGlobal);
        Assert.True(getGlobalIdx >= 0, "expected a GetGlobal instruction");
        Assert.Equal("input", chunk.ReadConstant(instrs[getGlobalIdx].Arg).AsString());

        Instr constantInstr = instrs[getGlobalIdx + 1];
        Assert.Equal(OpCode.Constant, constantInstr.Op);
        Assert.Equal(GrobValue.FromString(""), chunk.ReadConstant(constantInstr.Arg));

        Instr callInstr = instrs[getGlobalIdx + 2];
        Assert.Equal(OpCode.Call, callInstr.Op);
        Assert.Equal(1, callInstr.Arg);
    }

    [Fact]
    public void Input_OneArgument_CompilesToOrdinaryGetGlobalArgCall_NoSpecialHandling() {
        Chunk chunk = CompileSource("""readonly x := input("Name: ")""");

        List<Instr> instrs = Decode(chunk);
        int getGlobalIdx = instrs.FindIndex(i => i.Op == OpCode.GetGlobal);
        Assert.True(getGlobalIdx >= 0, "expected a GetGlobal instruction");
        Assert.Equal("input", chunk.ReadConstant(instrs[getGlobalIdx].Arg).AsString());

        Instr constantInstr = instrs[getGlobalIdx + 1];
        Assert.Equal(OpCode.Constant, constantInstr.Op);
        Assert.Equal(GrobValue.FromString("Name: "), chunk.ReadConstant(constantInstr.Arg));

        Instr callInstr = instrs[getGlobalIdx + 2];
        Assert.Equal(OpCode.Call, callInstr.Op);
        Assert.Equal(1, callInstr.Arg);

        // Exactly one Constant in the whole chunk (the supplied prompt) — no
        // second, filled-in "" constant.
        Assert.Single(instrs, i => i.Op == OpCode.Constant);
    }
}
