using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 9 Increment C0a-2 (D-373) — the array in-place-
/// mutating member surface. All four (<c>append</c>/<c>insert</c>/<c>remove</c>/
/// <c>clear</c>) compile through the same generic <see cref="OpCode.GetProperty"/>-then-
/// <see cref="OpCode.Call"/> shape every other array method already proves — confirming
/// no compiler-emission special-casing was needed.
/// </summary>
public sealed class CompilerArrayMutatingMemberTests {
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
                case OpCode.PopN:
                case OpCode.IncrementInt:
                case OpCode.DecrementInt:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                case OpCode.NewArray:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.GetUpvalue:
                case OpCode.SetUpvalue:
                case OpCode.NewAnonStruct:
                case OpCode.NewStruct:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                case OpCode.Closure:
                    arg = chunk.ReadByte(offset++);
                    if (chunk.ReadConstant(arg).TryAsFunction(out GrobFunction? gf) &&
                        gf is BytecodeFunction closureFn) {
                        offset += closureFn.UpvalueCount * 2;
                    }
                    break;
                default:
                    break;
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    [Theory]
    [InlineData("xs.append(3)\n", "append", 1)]
    [InlineData("xs.insert(0, 3)\n", "insert", 2)]
    [InlineData("xs.remove(0)\n", "remove", 1)]
    [InlineData("xs.clear()\n", "clear", 0)]
    public void MutatingCall_EmitsGetPropertyThenCallWithExpectedArgCount(
            string tail, string expectedName, int expectedArgCount) {
        Chunk chunk = CompileSource("xs: int[] := [1, 2]\n" + tail);

        List<Instr> instrs = Decode(chunk);
        int propIdx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        Assert.True(propIdx >= 0, "no GetProperty instruction found");
        Assert.Equal(expectedName, chunk.ReadConstant(instrs[propIdx].Arg).AsString());

        int callIdx = instrs.FindIndex(propIdx, i => i.Op == OpCode.Call);
        Assert.True(callIdx >= 0, "no Call instruction found after GetProperty");
        Assert.Equal(expectedArgCount, instrs[callIdx].Arg);
    }
}
