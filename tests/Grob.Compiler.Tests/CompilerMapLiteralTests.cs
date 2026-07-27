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

    /// <summary>
    /// Renders the constant pool in index order, quoting strings so a string <c>"1"</c> is
    /// distinguishable from the int <c>1</c>.
    /// </summary>
    private static List<string> ConstantsOf(Chunk chunk) {
        List<string> result = [];
        for (int i = 0; i < chunk.ConstantCount; i++) {
            GrobValue v = chunk.ReadConstant(i);
            result.Add(v.IsString ? $"\"{v.AsString()}\"" : v.ToString() ?? "");
        }
        return result;
    }

    /// <summary>The source line recorded against each instruction, in emission order.</summary>
    private static List<int> LinesOf(Chunk chunk) =>
        Decode(chunk).Select(i => chunk.GetLine(i.Offset)).ToList();

    // -----------------------------------------------------------------------
    // Emission shape — the complete bytecode contract for each literal form.
    // Whole-sequence equality, not instruction counts or key-presence probes:
    // these must fail if keys, values or surrounding instructions are reordered
    // (tests/CLAUDE.md — assert the exact bytecode emitted).
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_TwoEntries_EmitsExactBytecode() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{ "a": 1, "b": 2 }
            """);

        // Key then value, in source order, then NewMap(2) — the VM pops 2 pairs LIFO.
        Assert.Equal([
            new Instr(0, OpCode.Constant, 0),      // "a"
            new Instr(2, OpCode.Constant, 1),      // 1
            new Instr(4, OpCode.Constant, 2),      // "b"
            new Instr(6, OpCode.Constant, 3),      // 2
            new Instr(8, OpCode.NewMap, 2),
            new Instr(10, OpCode.DefineGlobal, 4), // "m"
            new Instr(12, OpCode.Return, 0),
        ], Decode(chunk));
        Assert.Equal(["\"a\"", "1", "\"b\"", "2", "\"m\""], ConstantsOf(chunk));
        Assert.Equal([1, 1, 1, 1, 1, 1, 1], LinesOf(chunk));
    }

    [Fact]
    public void MapLiteral_SingleEntry_EmitsExactBytecode() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{ "a": 1 }
            """);

        Assert.Equal([
            new Instr(0, OpCode.Constant, 0),     // "a"
            new Instr(2, OpCode.Constant, 1),     // 1
            new Instr(4, OpCode.NewMap, 1),
            new Instr(6, OpCode.DefineGlobal, 2), // "m"
            new Instr(8, OpCode.Return, 0),
        ], Decode(chunk));
        Assert.Equal(["\"a\"", "1", "\"m\""], ConstantsOf(chunk));
        Assert.Equal([1, 1, 1, 1, 1], LinesOf(chunk));
    }

    [Fact]
    public void MapLiteral_ZeroEntries_EmitsExactBytecode() {
        Chunk chunk = CompileSource("""
            readonly m := map<string, int>{}
            """);

        // No key/value pairs at all — NewMap(0) is the whole construction.
        Assert.Equal([
            new Instr(0, OpCode.NewMap, 0),
            new Instr(2, OpCode.DefineGlobal, 0), // "m"
            new Instr(4, OpCode.Return, 0),
        ], Decode(chunk));
        Assert.Equal(["\"m\""], ConstantsOf(chunk));
        Assert.Equal([1, 1, 1], LinesOf(chunk));
    }

    // -----------------------------------------------------------------------
    // Constant-pool overflow — a key index past 255 must widen, not wrap
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_KeyConstantBeyondByteRange_EmitsConstantLongNotWrappedIndex() {
        // Push the constant pool past 255 before the literal, so the key's index needs a
        // 2-byte operand. A raw (byte) cast would silently wrap it (256 -> 0) and the VM
        // would build the map under whatever string sits at the wrapped index.
        // Pad with distinct string literals rather than distinct globals, and keep the
        // literal itself out of a new binding: the global-name index is an index into the
        // same pool and has its own 1-byte guard (ToByteOperand) that would trip first.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < 300; i++) sb.Append($"print(\"pad{i}\")\n");
        sb.Append("print(map<string, int>{ \"zz\": 1 })\n");

        Chunk chunk = CompileSource(sb.ToString());
        List<Instr> instrs = Decode(chunk);

        int mapIdx = instrs.FindIndex(i => i.Op == OpCode.NewMap);
        Assert.True(mapIdx >= 2, "expected a key/value pair before NewMap");

        // The key constant is emitted two instructions before NewMap (key, then value).
        Instr keyInstr = instrs[mapIdx - 2];
        Assert.Equal(OpCode.ConstantLong, keyInstr.Op);
        Assert.True(keyInstr.Arg > byte.MaxValue,
            $"expected the key index to exceed the 1-byte range, was {keyInstr.Arg}");
        GrobValue key = chunk.ReadConstant(keyInstr.Arg);
        Assert.True(key.IsString);
        Assert.Equal("zz", key.AsString());
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
