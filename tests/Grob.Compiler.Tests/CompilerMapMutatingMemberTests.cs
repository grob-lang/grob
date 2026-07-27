using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 9 Increment C0b-2b (D-378) — the map in-place-mutating
/// member surface. <c>set</c>/<c>remove</c>/<c>clear</c> compile through the same generic
/// <see cref="OpCode.GetProperty"/>-then-<see cref="OpCode.Call"/> shape every other map
/// and array method already proves (D-373, D-377) — confirming no compiler-emission
/// special-casing was needed and no new opcode was added.
/// </summary>
public sealed class CompilerMapMutatingMemberTests {
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
                case OpCode.NewMap:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.GetUpvalue:
                case OpCode.SetUpvalue:
                case OpCode.NewAnonStruct:
                case OpCode.NewStruct:
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

    private static string[] Describe(Chunk chunk) =>
        Decode(chunk).Select(i => $"{i.Op}:{i.Arg}@{chunk.GetLine(i.Offset)}").ToArray();

    private static string[] DescribePool(Chunk chunk) =>
        Enumerable.Range(0, chunk.ConstantCount)
            .Select(i => $"{chunk.ReadConstant(i).Kind}:{chunk.ReadConstant(i)}")
            .ToArray();

    // Every case below compiles this prologue on line 1, so its four instructions and
    // its first three pool entries ("a", 1, "m") are shared by all of them.
    private const string MapPrologue = "m := map<string, int>{\"a\": 1}\n";

    [Fact]
    public void Set_EmitsGetPropertyThenTwoArgumentsThenCallWithTwoArguments() {
        Chunk chunk = CompileSource(MapPrologue + "m.set(\"b\", 2)\n");

        Assert.Equal([
            "Constant:0@1", "Constant:1@1", "NewMap:1@1", "DefineGlobal:2@1",
            "GetGlobal:2@2", "GetProperty:3@2", "Constant:4@2", "Constant:5@2",
            "Call:2@2", "Pop:0@2", "Return:0@3",
        ], Describe(chunk));
        Assert.Equal([
            "String:a", "Int:1", "String:m", "String:set", "String:b", "Int:2",
        ], DescribePool(chunk));
    }

    [Fact]
    public void Remove_EmitsGetPropertyThenArgumentThenCallWithOneArgument() {
        Chunk chunk = CompileSource(MapPrologue + "m.remove(\"a\")\n");

        Assert.Equal([
            "Constant:0@1", "Constant:1@1", "NewMap:1@1", "DefineGlobal:2@1",
            "GetGlobal:2@2", "GetProperty:3@2", "Constant:4@2", "Call:1@2",
            "Pop:0@2", "Return:0@3",
        ], Describe(chunk));
        Assert.Equal([
            "String:a", "Int:1", "String:m", "String:remove", "String:a",
        ], DescribePool(chunk));
    }

    [Fact]
    public void Clear_EmitsGetPropertyThenCallWithZeroArguments() {
        Chunk chunk = CompileSource(MapPrologue + "m.clear()\n");

        Assert.Equal([
            "Constant:0@1", "Constant:1@1", "NewMap:1@1", "DefineGlobal:2@1",
            "GetGlobal:2@2", "GetProperty:3@2", "Call:0@2",
            "Pop:0@2", "Return:0@3",
        ], Describe(chunk));
        Assert.Equal([
            "String:a", "Int:1", "String:m", "String:clear",
        ], DescribePool(chunk));
    }
}
