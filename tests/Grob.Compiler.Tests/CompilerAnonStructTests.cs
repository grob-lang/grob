using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 6 Increment D — anonymous struct literals.
/// Asserts that each field emits a name-constant / value pair followed by
/// <see cref="OpCode.NewAnonStruct"/> with the correct field-count operand.
/// </summary>
public sealed class CompilerAnonStructTests {
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
    // Emission shape — NewAnonStruct with correct field count
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_TwoFields_EmitsNewAnonStructWithCount2() {
        Chunk chunk = CompileSource("""
            readonly p := #{ name: "Alice", salary: 50000 }
            """);

        List<Instr> instrs = Decode(chunk);
        int idx = instrs.FindIndex(i => i.Op == OpCode.NewAnonStruct);
        Assert.True(idx >= 0, "no NewAnonStruct instruction found");
        Assert.Equal(2, instrs[idx].Arg);
    }

    [Fact]
    public void AnonStruct_SingleField_EmitsNewAnonStructWithCount1() {
        Chunk chunk = CompileSource("""
            readonly p := #{ name: "Alice" }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr newAnon = Assert.Single(instrs, i => i.Op == OpCode.NewAnonStruct);
        Assert.Equal(1, newAnon.Arg);
    }

    [Fact]
    public void AnonStruct_ZeroFields_EmitsNewAnonStructWithCount0() {
        Chunk chunk = CompileSource("""
            readonly p := #{ }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr newAnon = Assert.Single(instrs, i => i.Op == OpCode.NewAnonStruct);
        Assert.Equal(0, newAnon.Arg);
    }

    // -----------------------------------------------------------------------
    // Emission shape — name/value pairs precede NewAnonStruct
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_TwoFields_EmitsNameValuePairsBeforeNewAnonStruct() {
        // For #{ name: "Alice", salary: 50000 } the emitted sequence is:
        //   Constant(name_idx), Constant(value_idx), Constant(salary_idx), Constant(value_idx), NewAnonStruct(2)
        Chunk chunk = CompileSource("""
            readonly p := #{ name: "Alice", salary: 50000 }
            """);

        List<Instr> instrs = Decode(chunk);
        int anonIdx = instrs.FindIndex(i => i.Op == OpCode.NewAnonStruct);
        Assert.True(anonIdx >= 4, "expected at least 4 instructions before NewAnonStruct (2 name/value pairs)");

        // The two instructions immediately before each value load are Constant (field name strings).
        // Verify both field names appear as string constants in the pool.
        bool hasName = false, hasSalary = false;
        for (int i = 0; i < chunk.ConstantCount; i++) {
            GrobValue c = chunk.ReadConstant(i);
            if (c.IsString && c.AsString() == "name") hasName = true;
            if (c.IsString && c.AsString() == "salary") hasSalary = true;
        }
        Assert.True(hasName, "field name 'name' not found in constant pool");
        Assert.True(hasSalary, "field name 'salary' not found in constant pool");
    }

    // -----------------------------------------------------------------------
    // Nested anonymous struct — each level gets its own NewAnonStruct
    // -----------------------------------------------------------------------

    [Fact]
    public void AnonStruct_Nested_EmitsTwoNewAnonStructInstructions() {
        Chunk chunk = CompileSource("""
            readonly body := #{ properties: #{ mode: "Incremental" } }
            """);

        List<Instr> instrs = Decode(chunk);
        int count = instrs.Count(i => i.Op == OpCode.NewAnonStruct);
        Assert.Equal(2, count);
    }

}
