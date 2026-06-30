using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 6 Increment C — struct field access and assignment.
/// Verifies that field reads emit <see cref="OpCode.GetProperty"/> and field writes
/// emit <see cref="OpCode.SetProperty"/>, each with the correct name-index operand
/// pointing to the string constant in the pool.
/// </summary>
public sealed class CompilerFieldAccessTests {
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
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
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

    // -----------------------------------------------------------------------
    // GetProperty emission — field reads
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccess_EmitsGetProperty_WithHostName() {
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "example.com", port: 8080 }
            readonly h := c.host
            """);

        List<Instr> instrs = Decode(chunk);
        int idx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        Assert.True(idx >= 0, "no GetProperty instruction found");
        int namePoolIdx = instrs[idx].Arg;
        GrobValue nameConst = chunk.ReadConstant(namePoolIdx);
        Assert.True(nameConst.IsString, "GetProperty operand should be a string constant");
        Assert.Equal("host", nameConst.AsString());
    }

    [Fact]
    public void FieldAccess_EmitsGetProperty_GetExprType_ReflectsFieldType() {
        // GetProperty for an int field; verify the constant pool index points to "port".
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int
            }
            readonly c := Config { host: "x", port: 8080 }
            readonly p := c.port
            """);

        List<Instr> instrs = Decode(chunk);
        int idx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        Assert.True(idx >= 0, "no GetProperty instruction found");
        Assert.Equal("port", chunk.ReadConstant(instrs[idx].Arg).AsString());
    }

    // -----------------------------------------------------------------------
    // SetProperty emission — field writes
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_EmitsSetProperty_WithFieldName() {
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "example.com", port: 8080 }
            c.host = "localhost"
            """);

        List<Instr> instrs = Decode(chunk);
        int idx = instrs.FindIndex(i => i.Op == OpCode.SetProperty);
        Assert.True(idx >= 0, "no SetProperty instruction found");
        int namePoolIdx = instrs[idx].Arg;
        GrobValue nameConst = chunk.ReadConstant(namePoolIdx);
        Assert.True(nameConst.IsString, "SetProperty operand should be a string constant");
        Assert.Equal("host", nameConst.AsString());
    }

    [Fact]
    public void FieldAssign_EmitsReceiverThenValueThenSetProperty() {
        // The instruction ordering must be: receiver (GetGlobal c), value, SetProperty.
        Chunk chunk = CompileSource("""
            type Config {
            host: string
            port: int
            }
            c := Config { host: "example.com", port: 8080 }
            c.host = "new"
            """);

        List<Instr> instrs = Decode(chunk);
        int setIdx = instrs.FindIndex(i => i.Op == OpCode.SetProperty);
        Assert.True(setIdx >= 1, "SetProperty must have instructions before it");
        // Instruction before SetProperty must push the value ("new").
        OpCode beforeSet = instrs[setIdx - 1].Op;
        Assert.True(
            beforeSet == OpCode.Constant || beforeSet == OpCode.ConstantLong,
            $"expected Constant before SetProperty, got {beforeSet}");
        // Two instructions before SetProperty must push the receiver.
        Assert.True(setIdx >= 2, "SetProperty must have at least two instructions before it");
        OpCode beforeValue = instrs[setIdx - 2].Op;
        Assert.True(
            beforeValue == OpCode.GetGlobal || beforeValue == OpCode.GetLocal,
            $"expected GetGlobal/GetLocal before value push, got {beforeValue}");
    }

    // -----------------------------------------------------------------------
    // Nested field assignment — a.b.c = v emits GetProperty then SetProperty
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAssign_Nested_EmitsGetPropertyThenSetProperty() {
        Chunk chunk = CompileSource("""
            type Address {
            city: string
            }
            type Person {
            name: string
            address: Address
            }
            p := Person { name: "Alice", address: Address { city: "London" } }
            p.address.city = "Paris"
            """);

        List<Instr> instrs = Decode(chunk);
        int getIdx = instrs.FindIndex(i => i.Op == OpCode.GetProperty);
        int setIdx = instrs.FindIndex(i => i.Op == OpCode.SetProperty);
        Assert.True(getIdx >= 0, "no GetProperty instruction found");
        Assert.True(setIdx > getIdx, "SetProperty must come after GetProperty");
        Assert.Equal("address", chunk.ReadConstant(instrs[getIdx].Arg).AsString());
        Assert.Equal("city", chunk.ReadConstant(instrs[setIdx].Arg).AsString());
    }
}
