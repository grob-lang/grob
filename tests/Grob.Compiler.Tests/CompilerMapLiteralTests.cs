using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for map-literal construction (D-376). Asserts that each entry
/// emits a key-constant / value pair followed by <see cref="OpCode.NewMap"/> with the
/// correct entry-count operand — mirrors <c>CompilerAnonStructTests</c>.
/// </summary>
public sealed class CompilerMapLiteralTests {
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
                case OpCode.NewAnonStruct:
                case OpCode.NewMap:
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

    // -----------------------------------------------------------------------
    // Emission shape — NewMap with correct entry count
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_TwoEntries_EmitsNewMapWithCount2() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{ "a": 1, "b": 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        int idx = instrs.FindIndex(i => i.Op == OpCode.NewMap);
        Assert.True(idx >= 0, "no NewMap instruction found");
        Assert.Equal(2, instrs[idx].Arg);
    }

    [Fact]
    public void MapLiteral_SingleEntry_EmitsNewMapWithCount1() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{ "a": 1 }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr newMap = Assert.Single(instrs, i => i.Op == OpCode.NewMap);
        Assert.Equal(1, newMap.Arg);
    }

    [Fact]
    public void MapLiteral_ZeroEntries_EmitsNewMapWithCount0() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{}
            """);

        List<Instr> instrs = Decode(chunk);
        Instr newMap = Assert.Single(instrs, i => i.Op == OpCode.NewMap);
        Assert.Equal(0, newMap.Arg);
    }

    // -----------------------------------------------------------------------
    // Emission shape — key/value pairs precede NewMap
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_TwoEntries_EmitsKeyValuePairsBeforeNewMap() {
        // For map<string, int>{ "a": 1, "b": 2 } the emitted sequence is:
        //   Constant(a_idx), Constant(1_idx), Constant(b_idx), Constant(2_idx), NewMap(2)
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{ "a": 1, "b": 2 }
            """);

        List<Instr> instrs = Decode(chunk);
        int mapIdx = instrs.FindIndex(i => i.Op == OpCode.NewMap);
        Assert.True(mapIdx >= 4, "expected at least 4 instructions before NewMap (2 key/value pairs)");

        bool hasA = false, hasB = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            GrobValue c = chunk.ReadConstant(i);
            if (c.IsString && c.AsString() == "a") hasA = true;
            if (c.IsString && c.AsString() == "b") hasB = true;
        }
        Assert.True(hasA, "key 'a' not found in constant pool");
        Assert.True(hasB, "key 'b' not found in constant pool");
    }

    // -----------------------------------------------------------------------
    // Nested map literal — each level gets its own NewMap
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_ValueIsAnonStruct_EmitsOneNewMapAndOneNewAnonStruct() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{ "a": 1 }
            readonly body := #{ tags: m }
            """);

        List<Instr> instrs = Decode(chunk);
        Assert.Equal(1, instrs.Count(i => i.Op == OpCode.NewMap));
        Assert.Equal(1, instrs.Count(i => i.Op == OpCode.NewAnonStruct));
    }
}
