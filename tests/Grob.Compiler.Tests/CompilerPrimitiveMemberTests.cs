using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Core.PrimitiveMembers;
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
    // Complete-chunk contract — the whole emitted instruction sequence, so a stray or
    // reordered opcode around the dispatch cannot slip through a windowed spot-check.
    // -----------------------------------------------------------------------

    [Fact]
    public void ZeroArgMethod_EmitsExactCompleteInstructionSequence() {
        Chunk chunk = SingleFunctionConstant(CompileSource("""
            fn run(s: string): string {
                return s.trim()
            }
            """)).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Assert.Equal(
            [OpCode.GetGlobal, OpCode.GetLocal, OpCode.Call, OpCode.Return, OpCode.Nil, OpCode.Return],
            instrs.Select(i => i.Op));
        Assert.Equal("string.trim", chunk.ReadConstant(instrs[0].Arg).AsString());
        Assert.Equal(0, instrs[1].Arg);   // receiver injected as local slot 0
        Assert.Equal(1, instrs[2].Arg);   // Call arity = receiver only
    }

    // -----------------------------------------------------------------------
    // Every registered string member — each registry entry proves its qualified-native
    // rewrite, receiver-first argument order and Call arity (data-driven off the registry
    // so a new member is covered the moment it is registered).
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> AllStringMembers() {
        PrimitiveMemberEntry entry = PrimitiveMemberRegistry.String;
        foreach (PrimitiveMemberProperty p in entry.Properties.Values) {
            yield return [$"v := s.{p.Name}", p.QualifiedNativeName, 1];
        }
        foreach (PrimitiveMemberMethod m in entry.Methods.Values) {
            string args = string.Join(", ", m.ParameterTypes.Select(ArgLiteral));
            yield return [$"s.{m.Name}({args})", m.QualifiedNativeName, m.ParameterTypes.Count + 1];
        }
    }

    private static string ArgLiteral(GrobType parameterType) =>
        parameterType == GrobType.Int ? "1" : "\"x\"";

    [Theory]
    [MemberData(nameof(AllStringMembers))]
    public void EveryRegistryMember_LowersToQualifiedNativeReceiverFirstCall(
            string statement, string qualifiedName, int expectedCallArity) {
        Chunk chunk = SingleFunctionConstant(CompileSource($$"""
            fn run(s: string): void {
                {{statement}}
            }
            """)).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Assert.DoesNotContain(instrs, i => i.Op == OpCode.GetProperty);

        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal &&
            chunk.ReadConstant(i.Arg).TryAsString(out string? s) && s == qualifiedName);
        int idx = instrs.IndexOf(getGlobal);

        Assert.Equal(OpCode.GetLocal, instrs[idx + 1].Op);
        Assert.Equal(0, instrs[idx + 1].Arg);   // receiver injected as arg[0]

        Instr call = Assert.Single(instrs, i => i.Op == OpCode.Call);
        Assert.Equal(expectedCallArity, call.Arg);
    }

    // -----------------------------------------------------------------------
    // padLeft/padRight/truncate omitted-optional-argument form (D-365) — the
    // NativeDefaultArgumentFill synthesised constant must land after the supplied
    // argument and before Call, and Call's operand must be the full declared arity
    // (receiver included), not the source call's own argument count.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("padLeft", "string.padLeft", " ")]
    [InlineData("padRight", "string.padRight", " ")]
    [InlineData("truncate", "string.truncate", "...")]
    public void DefaultParameterMethod_OmittedArgument_SynthesisesDefault_CallArityThree(
            string method, string qualifiedName, string expectedDefault) {
        Chunk topLevel = CompileSource($$"""
            fn run(s: string): string {
                return s.{{method}}(3)
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        List<Instr> instrs = Decode(chunk);

        // Full sequence: GetGlobal callee, receiver GetLocal, supplied-arg Constant,
        // synthesised-default Constant, Call 3, then the fn's own Return/Nil/Return tail.
        Assert.Equal(
            [OpCode.GetGlobal, OpCode.GetLocal, OpCode.Constant, OpCode.Constant, OpCode.Call,
                OpCode.Return, OpCode.Nil, OpCode.Return],
            instrs.Select(i => i.Op));

        Assert.Equal(qualifiedName, chunk.ReadConstant(instrs[0].Arg).AsString());
        Assert.Equal(0, instrs[1].Arg);
        Assert.Equal(GrobValue.FromInt(3), chunk.ReadConstant(instrs[2].Arg));
        Assert.Equal(GrobValue.FromString(expectedDefault), chunk.ReadConstant(instrs[3].Arg));
        Assert.Equal(3, instrs[4].Arg);
    }

    [Theory]
    [InlineData("padLeft", "string.padLeft")]
    [InlineData("padRight", "string.padRight")]
    [InlineData("truncate", "string.truncate")]
    public void DefaultParameterMethod_BothArgumentsSupplied_NoSynthesisedConstant_CallArityThree(
            string method, string qualifiedName) {
        Chunk topLevel = CompileSource($$"""
            fn run(s: string): string {
                return s.{{method}}(3, "x")
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;
        List<Instr> instrs = Decode(chunk);

        Assert.Equal(
            [OpCode.GetGlobal, OpCode.GetLocal, OpCode.Constant, OpCode.Constant, OpCode.Call,
                OpCode.Return, OpCode.Nil, OpCode.Return],
            instrs.Select(i => i.Op));

        Assert.Equal(qualifiedName, chunk.ReadConstant(instrs[0].Arg).AsString());
        Assert.Equal(GrobValue.FromInt(3), chunk.ReadConstant(instrs[2].Arg));
        Assert.Equal(GrobValue.FromString("x"), chunk.ReadConstant(instrs[3].Arg));
        Assert.Equal(3, instrs[4].Arg);
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
