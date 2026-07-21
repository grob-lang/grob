using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Compiler-emission tests for primitive instance-method/property dispatch (D-066,
/// proven on <c>string</c>). Every shape compiles to <c>GetGlobal "&lt;qualified
/// native&gt;"</c> (the callee, pushed first — the same D-342 namespace-native shape
/// <see cref="CompilerFormatAsTests"/> proves for <c>formatAs</c>), the receiver, then
/// the call's own arguments in source order, then <see cref="OpCode.Call"/> with an
/// operand of <c>1 + argument count</c>. No <see cref="OpCode.GetProperty"/> is ever
/// emitted for a primitive receiver — that opcode has no primitive arm (D-362's gap)
/// and must not gain one; the whole rewrite happens here, at compile time.
/// </summary>
public sealed class CompilerPrimitiveMemberTests {
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

    private static BytecodeFunction SingleFunctionConstant(Chunk chunk) {
        var functions = new List<BytecodeFunction>();
        foreach (Instr instr in Decode(chunk).Where(i => i.Op == OpCode.Constant)) {
            GrobValue v = chunk.ReadConstant(instr.Arg);
            if (v.IsFunction && v.AsFunction() is BytecodeFunction bf) functions.Add(bf);
        }
        return Assert.Single(functions);
    }

    private readonly record struct Instr(OpCode Op, int Arg);

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
                case OpCode.GetProperty:
                case OpCode.GetLocal:
                case OpCode.NewArray:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break;
            }
            result.Add(new Instr(op, arg));
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Zero-argument method — GetGlobal, receiver, Call(1).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("trim", "string.trim")]
    [InlineData("upper", "string.upper")]
    [InlineData("toString", "string.toString")]
    public void ZeroArgMethod_CompilesToGetGlobal_Receiver_Call1(string method, string qualifiedName) {
        Chunk topLevel = CompileSource($$"""
            fn run(s: string): string {
                return s.{{method}}()
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal &&
            chunk.ReadConstant(i.Arg).TryAsString(out string? s) && s == qualifiedName);
        int idx = instrs.IndexOf(getGlobal);

        Assert.Equal(OpCode.GetLocal, instrs[idx + 1].Op);
        Assert.Equal(OpCode.Call, instrs[idx + 2].Op);
        Assert.Equal(1, instrs[idx + 2].Arg);
    }

    // -----------------------------------------------------------------------
    // Property access — identical shape to a zero-arg method (no GetProperty).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("length", "string.length")]
    [InlineData("isEmpty", "string.isEmpty")]
    public void Property_CompilesToGetGlobal_Receiver_Call1_NoGetProperty(string property, string qualifiedName) {
        Chunk topLevel = CompileSource($$"""
            fn run(s: string): int {
                v := s.{{property}}
                return 0
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Assert.DoesNotContain(instrs, i => i.Op == OpCode.GetProperty);

        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal &&
            chunk.ReadConstant(i.Arg).TryAsString(out string? s) && s == qualifiedName);
        int idx = instrs.IndexOf(getGlobal);

        Assert.Equal(OpCode.GetLocal, instrs[idx + 1].Op);
        Assert.Equal(OpCode.Call, instrs[idx + 2].Op);
        Assert.Equal(1, instrs[idx + 2].Arg);
    }

    // -----------------------------------------------------------------------
    // One-argument method — GetGlobal, receiver, arg, Call(2).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("contains", "string.contains", "\"x\"")]
    [InlineData("indexOf", "string.indexOf", "\"x\"")]
    [InlineData("repeat", "string.repeat", "1")]
    public void OneArgMethod_CompilesToGetGlobal_Receiver_Arg_Call2(string method, string qualifiedName, string argLiteral) {
        Chunk topLevel = CompileSource($$"""
            fn run(s: string): void {
                s.{{method}}({{argLiteral}})
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal &&
            chunk.ReadConstant(i.Arg).TryAsString(out string? s) && s == qualifiedName);
        int idx = instrs.IndexOf(getGlobal);

        Assert.Equal(OpCode.GetLocal, instrs[idx + 1].Op);
        Assert.Equal(OpCode.Constant, instrs[idx + 2].Op);
        Assert.Equal(OpCode.Call, instrs[idx + 3].Op);
        Assert.Equal(2, instrs[idx + 3].Arg);
    }

    // -----------------------------------------------------------------------
    // Two-argument method — GetGlobal, receiver, arg, arg, Call(3).
    // -----------------------------------------------------------------------

    [Fact]
    public void Replace_CompilesToGetGlobal_Receiver_TwoArgs_Call3() {
        Chunk topLevel = CompileSource("""
            fn run(s: string): string {
                return s.replace("a", "b")
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal &&
            chunk.ReadConstant(i.Arg).TryAsString(out string? s) && s == "string.replace");
        int idx = instrs.IndexOf(getGlobal);

        Assert.Equal(OpCode.GetLocal, instrs[idx + 1].Op);
        Assert.Equal(OpCode.Constant, instrs[idx + 2].Op);
        Assert.Equal("a", chunk.ReadConstant(instrs[idx + 2].Arg).AsString());
        Assert.Equal(OpCode.Constant, instrs[idx + 3].Op);
        Assert.Equal("b", chunk.ReadConstant(instrs[idx + 3].Arg).AsString());
        Assert.Equal(OpCode.Call, instrs[idx + 4].Op);
        Assert.Equal(3, instrs[idx + 4].Arg);
    }

    // -----------------------------------------------------------------------
    // Numeric-return-as-operand — proves ResolvedReturnType/ResolvedFieldType thread
    // into GetExprType's opcode selection (D-362).
    // -----------------------------------------------------------------------

    [Fact]
    public void IndexOfPlusOne_SelectsAddInt() {
        Chunk topLevel = CompileSource("""
            fn run(s: string): int {
                return s.indexOf("x") + 1
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        Assert.Contains(Decode(chunk), i => i.Op == OpCode.AddInt);
    }

    [Fact]
    public void LengthTimesTwo_SelectsMultiplyInt() {
        Chunk topLevel = CompileSource("""
            fn run(s: string): int {
                return s.length * 2
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        Assert.Contains(Decode(chunk), i => i.Op == OpCode.MultiplyInt);
    }
}
