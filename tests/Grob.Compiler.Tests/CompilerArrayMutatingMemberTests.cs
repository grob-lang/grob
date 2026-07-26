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
/// no compiler-emission special-casing was needed. Each test asserts the complete chunk
/// contract for its source: the full ordered instruction stream (offset, opcode, operand
/// and line for every instruction) and the entire constant pool — so an incorrect
/// intervening instruction, a wrong operand byte, a stray constant or a mislabelled
/// source line cannot slip past a partial spot-check.
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

    private readonly record struct Instr(int Offset, OpCode Op, int Arg, int Line);

    private static List<Instr> Decode(Chunk chunk) {
        var result = new List<Instr>();
        int offset = 0;
        while (offset < chunk.Count) {
            int here = offset;
            int line = chunk.GetLine(here);
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
            result.Add(new Instr(here, op, arg, line));
        }
        return result;
    }

    /// <summary>Renders the whole constant pool to a stable, order-preserving projection —
    /// discriminator plus value — so the assertion pins both the entries and their pool
    /// indices without depending on <see cref="GrobValue"/>'s equality semantics.</summary>
    private static List<string> Constants(Chunk chunk) {
        var result = new List<string>(chunk.ConstantCount);
        for (int i = 0; i < chunk.ConstantCount; i++) {
            GrobValue value = chunk.ReadConstant(i);
            result.Add(value.Kind switch {
                GrobValueKind.Int => $"Int:{value.AsInt()}",
                GrobValueKind.String => $"String:\"{value.AsString()}\"",
                _ => value.Kind.ToString(),
            });
        }
        return result;
    }

    // The shared prologue every case emits for `xs: int[] := [1, 2]` on line 1: push
    // both element constants, build the array, define the global. Pool indices #0/#1 are
    // the elements, #2 is the "xs" global name.
    private static readonly Instr[] Prologue = [
        new(0, OpCode.Constant, 0, 1),
        new(2, OpCode.Constant, 1, 1),
        new(4, OpCode.NewArray, 2, 1),
        new(6, OpCode.DefineGlobal, 2, 1),
        new(8, OpCode.GetGlobal, 2, 2),
        new(10, OpCode.GetProperty, 3, 2),
    ];

    [Fact]
    public void Append_EmitsFullChunk_GetPropertyArgumentCallOneArgument() {
        Chunk chunk = CompileSource("xs: int[] := [1, 2]\nxs.append(3)\n");

        Assert.Equal([
            .. Prologue,
            new(12, OpCode.Constant, 4, 2),  // argument 3
            new(14, OpCode.Call, 1, 2),
            new(16, OpCode.Pop, 0, 2),       // expression-statement result discarded
            new(17, OpCode.Return, 0, 3),
        ], Decode(chunk));
        Assert.Equal(["Int:1", "Int:2", "String:\"xs\"", "String:\"append\"", "Int:3"], Constants(chunk));
    }

    [Fact]
    public void Insert_EmitsFullChunk_TwoArgumentsCallTwoArguments() {
        Chunk chunk = CompileSource("xs: int[] := [1, 2]\nxs.insert(0, 3)\n");

        Assert.Equal([
            .. Prologue,
            new(12, OpCode.Constant, 4, 2),  // index argument 0
            new(14, OpCode.Constant, 5, 2),  // value argument 3
            new(16, OpCode.Call, 2, 2),
            new(18, OpCode.Pop, 0, 2),
            new(19, OpCode.Return, 0, 3),
        ], Decode(chunk));
        Assert.Equal(
            ["Int:1", "Int:2", "String:\"xs\"", "String:\"insert\"", "Int:0", "Int:3"],
            Constants(chunk));
    }

    [Fact]
    public void Remove_EmitsFullChunk_OneArgumentCallOneArgument() {
        Chunk chunk = CompileSource("xs: int[] := [1, 2]\nxs.remove(0)\n");

        Assert.Equal([
            .. Prologue,
            new(12, OpCode.Constant, 4, 2),  // index argument 0
            new(14, OpCode.Call, 1, 2),
            new(16, OpCode.Pop, 0, 2),
            new(17, OpCode.Return, 0, 3),
        ], Decode(chunk));
        Assert.Equal(["Int:1", "Int:2", "String:\"xs\"", "String:\"remove\"", "Int:0"], Constants(chunk));
    }

    [Fact]
    public void Clear_EmitsFullChunk_NoArgumentsCallZeroArguments() {
        Chunk chunk = CompileSource("xs: int[] := [1, 2]\nxs.clear()\n");

        Assert.Equal([
            .. Prologue,
            new(12, OpCode.Call, 0, 2),
            new(14, OpCode.Pop, 0, 2),
            new(15, OpCode.Return, 0, 3),
        ], Decode(chunk));
        Assert.Equal(["Int:1", "Int:2", "String:\"xs\"", "String:\"clear\""], Constants(chunk));
    }
}
