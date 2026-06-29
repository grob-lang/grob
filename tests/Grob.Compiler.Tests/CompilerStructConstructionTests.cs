using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 6 Increment B — named struct construction.
/// Asserts that field values (supplied or default) are emitted in declaration order
/// followed by <see cref="OpCode.NewStruct"/> with the correct type-index operand.
/// </summary>
public sealed class CompilerStructConstructionTests {
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

    // Decode instruction stream into (opcode, operand) pairs.
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
    // Field emission order — all supplied
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_AllFieldsSupplied_EmitsInDeclarationOrder() {
        // Fields supplied in reverse order at the site; must emit in declaration order.
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { port: 9000, host: "example.com" }
            """);

        List<Instr> instrs = Decode(chunk);
        int newStructIdx = instrs.FindIndex(i => i.Op == OpCode.NewStruct);
        Assert.True(newStructIdx >= 0, "no NewStruct instruction found");

        // Two Constant instructions before NewStruct: first must be host (string),
        // second must be port (int) — declaration order, not source order.
        // Walk backwards from NewStruct to find the two constants.
        var consts = new List<GrobValue>();
        for (int i = newStructIdx - 1; i >= 0; i--) {
            if (instrs[i].Op == OpCode.Constant || instrs[i].Op == OpCode.ConstantLong) {
                consts.Insert(0, chunk.ReadConstant(instrs[i].Arg));
                if (consts.Count == 2) break;
            }
        }

        Assert.Equal(2, consts.Count);
        Assert.True(consts[0].IsString, "first emitted value should be 'host' (string)");
        Assert.Equal("example.com", consts[0].AsString());
        Assert.True(consts[1].IsInt, "second emitted value should be 'port' (int)");
        Assert.Equal(9000L, consts[1].AsInt());
    }

    // -----------------------------------------------------------------------
    // Default emission — omitted defaulted field fills in at construction site
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_OmittedDefaultField_EmitsDefaultExpression() {
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int = 80
            }
            readonly c := Config { host: "example.com" }
            """);

        List<Instr> instrs = Decode(chunk);
        int newStructIdx = instrs.FindIndex(i => i.Op == OpCode.NewStruct);
        Assert.True(newStructIdx >= 0, "no NewStruct instruction found");

        // host = "example.com" then port = 80 (from default).
        var consts = new List<GrobValue>();
        for (int i = newStructIdx - 1; i >= 0; i--) {
            if (instrs[i].Op == OpCode.Constant || instrs[i].Op == OpCode.ConstantLong) {
                consts.Insert(0, chunk.ReadConstant(instrs[i].Arg));
                if (consts.Count == 2) break;
            }
        }

        Assert.Equal(2, consts.Count);
        Assert.Equal("example.com", consts[0].AsString());
        Assert.Equal(80L, consts[1].AsInt());
    }

    [Fact]
    public void StructConstruction_SuppliedFieldOverride_DoesNotEmitDefault() {
        // When a field is supplied, the default must NOT be emitted.
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int = 80
            }
            readonly c := Config { host: "h", port: 9999 }
            """);

        List<Instr> instrs = Decode(chunk);

        // 80 (the default) must not appear in the constant pool that precedes NewStruct.
        bool has80 = instrs.Any(i =>
            (i.Op == OpCode.Constant || i.Op == OpCode.ConstantLong) &&
            chunk.ReadConstant(i.Arg).IsInt &&
            chunk.ReadConstant(i.Arg).AsInt() == 80L);
        Assert.False(has80, "default value 80 should not be emitted when port is supplied");
    }

    // -----------------------------------------------------------------------
    // NewStruct type-index operand and descriptor
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_NewStruct_TypeIndexIsZero_FirstTypeRegistered() {
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            }
            readonly c := Config { host: "localhost" }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr ns = instrs.Single(i => i.Op == OpCode.NewStruct);
        Assert.Equal(0, ns.Arg);  // first registered type has index 0
        StructTypeDescriptor desc = chunk.GetStructType(0);
        Assert.Equal("Config", desc.TypeName);
        Assert.Equal(["host"], desc.FieldNames);
    }

    // -----------------------------------------------------------------------
    // Nested construction — inner NewStruct before outer NewStruct
    // -----------------------------------------------------------------------

    [Fact]
    public void StructConstruction_Nested_EmitsInnerNewStructThenOuter() {
        Chunk chunk = CompileSource("""
            type Inner {
            x: int
            }
            type Outer {
            inner: Inner
            }
            readonly o := Outer { inner: Inner { x: 42 } }
            """);

        List<Instr> instrs = Decode(chunk);
        var newStructs = instrs.Where(i => i.Op == OpCode.NewStruct).ToList();
        Assert.Equal(2, newStructs.Count);
        // Inner must be emitted first (it's a sub-expression), Outer second.
        Assert.Equal("Inner", chunk.GetStructType((byte)newStructs[0].Arg).TypeName);
        Assert.Equal("Outer", chunk.GetStructType((byte)newStructs[1].Arg).TypeName);
    }
}
