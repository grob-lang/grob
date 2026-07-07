using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Compiler / disassembler-shape tests for Sprint 7 Increment A — <c>throw</c>
/// emission. A thrown construction compiles to the field values (in declaration
/// order), then <see cref="OpCode.NewStruct"/> with the right type index, then
/// <see cref="OpCode.Throw"/>. Decoded from the raw instruction stream (mirroring
/// <c>CompilerStructConstructionTests</c>'s <c>Decode</c> helper) rather than via
/// <c>Grob.Vm.Disassembler</c> — <c>Grob.Compiler.Tests</c> does not reference
/// <c>Grob.Vm</c>, preserving the DAG boundary (invariant 1) in test code too.
/// </summary>
public sealed class CompilerThrowTests {
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

    // Decode instruction stream into (opcode, operand) pairs — mirrors
    // CompilerStructConstructionTests.Decode.
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

    [Fact]
    public void Throw_ConstructedException_DisassemblesToFieldsThenNewStructThenThrow() {
        Chunk chunk = CompileSource("""
            throw IoError { message: "not found" }
            """);

        List<Instr> instrs = Decode(chunk);
        int constIdx = instrs.FindIndex(i => i.Op == OpCode.Constant || i.Op == OpCode.ConstantLong);
        int newStructIdx = instrs.FindIndex(i => i.Op == OpCode.NewStruct);
        int throwIdx = instrs.FindIndex(i => i.Op == OpCode.Throw);

        Assert.True(constIdx >= 0, "expected a Constant instruction for the message field");
        Assert.True(newStructIdx > constIdx, "NewStruct must follow the field-value push");
        Assert.True(throwIdx > newStructIdx, "Throw must follow NewStruct");
    }

    [Fact]
    public void Throw_ConstructedException_NewStructUsesCorrectTypeIndex() {
        Chunk chunk = CompileSource("""
            throw IoError { message: "not found" }
            """);

        List<Instr> instrs = Decode(chunk);
        Instr ns = instrs.Single(i => i.Op == OpCode.NewStruct);
        StructTypeDescriptor descriptor = chunk.GetStructType((byte)ns.Arg);
        Assert.Equal("IoError", descriptor.TypeName);
        Assert.Equal(["message", "location"], descriptor.FieldNames);
    }

    [Fact]
    public void Throw_BoundIdentifier_EmitsLoadThenThrow_NoSecondNewStruct() {
        // throw e (an already-bound GrobError value) — no construction at the
        // throw site itself, just a load followed by Throw.
        Chunk chunk = CompileSource("""
            readonly e := IoError { message: "x" }
            throw e
            """);

        List<Instr> instrs = Decode(chunk);
        // Exactly one NewStruct — the readonly's own initialiser, not a second one
        // synthesised at the throw site.
        Assert.Single(instrs, i => i.Op == OpCode.NewStruct);
        Instr throwInstr = instrs.Single(i => i.Op == OpCode.Throw);
        Instr newStructInstr = instrs.Single(i => i.Op == OpCode.NewStruct);
        Assert.True(throwInstr.Offset > newStructInstr.Offset);
    }
}
