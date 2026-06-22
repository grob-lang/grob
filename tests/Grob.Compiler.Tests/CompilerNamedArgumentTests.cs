using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 5 Increment B — the reorder-and-fill call
/// emission for named and default arguments.
/// </summary>
/// <remarks>
/// The compiler emits arguments in parameter declaration order regardless of the
/// source order of named arguments, and materialises omitted defaults into their
/// slots, so the (unchanged) <see cref="OpCode.Call"/> hands the callee a fully
/// bound positional list. These tests disassemble the call site and assert the
/// argument value emissions and the <see cref="OpCode.Call"/> operand directly.
/// </remarks>
public sealed class CompilerNamedArgumentTests {
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
    /// Returns a description of each argument value emission between the callee load
    /// (the last <see cref="OpCode.GetGlobal"/> before the call) and the
    /// <see cref="OpCode.Call"/> — int constants as their value, bools as "true" /
    /// "false" — in emission order, which must be parameter declaration order.
    /// </summary>
    private static List<string> ArgEmissions(Chunk chunk) {
        List<Instr> instrs = Decode(chunk);
        int callIdx = instrs.FindIndex(i => i.Op == OpCode.Call);
        Assert.True(callIdx >= 0, "no Call instruction found");
        int calleeIdx = instrs.FindLastIndex(callIdx, i => i.Op == OpCode.GetGlobal);
        Assert.True(calleeIdx >= 0, "no callee GetGlobal before Call");

        var emissions = new List<string>();
        for (int i = calleeIdx + 1; i < callIdx; i++) {
            switch (instrs[i].Op) {
                case OpCode.Constant:
                case OpCode.ConstantLong:
                    GrobValue v = chunk.ReadConstant(instrs[i].Arg);
                    emissions.Add(v.IsInt ? v.AsInt().ToString() : v.ToString() ?? "?");
                    break;
                case OpCode.True:
                    emissions.Add("true");
                    break;
                case OpCode.False:
                    emissions.Add("false");
                    break;
                default:
                    break;
            }
        }
        return emissions;
    }

    private static int CallArgCount(Chunk chunk) =>
        Assert.Single(Decode(chunk), i => i.Op == OpCode.Call).Arg;

    // -----------------------------------------------------------------------
    // Omitted default — materialised into its slot.
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_WithDefault_Omitted_EmitsDefaultInPosition() {
        Chunk chunk = CompileSource("""
            fn f(a: int, b: int = 99): int {
            return a + b
            }
            f(1)
            """);
        Assert.Equal(["1", "99"], ArgEmissions(chunk));
        Assert.Equal(2, CallArgCount(chunk));
    }

    // -----------------------------------------------------------------------
    // Named argument out of source order — reordered into parameter order, with
    // the skipped default materialised in between.
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_WithNamedArg_EmitsInParameterOrder() {
        Chunk chunk = CompileSource("""
            fn f(a: int, b: int = 99, c: bool = true): int {
            return a + b
            }
            f(1, c: false)
            """);
        // Source order is a, c; emission order must be a, b(default), c.
        Assert.Equal(["1", "99", "false"], ArgEmissions(chunk));
        Assert.Equal(3, CallArgCount(chunk));
    }

    // -----------------------------------------------------------------------
    // Named override of a default — the supplied value, not the default.
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_NamedOverride_EmitsSuppliedValue() {
        Chunk chunk = CompileSource("""
            fn f(a: int, b: int = 99): int {
            return a + b
            }
            f(1, b: 20)
            """);
        Assert.Equal(["1", "20"], ArgEmissions(chunk));
        Assert.Equal(2, CallArgCount(chunk));
    }

    // -----------------------------------------------------------------------
    // Pure positional call to a no-default function — the fast path, source order.
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_PurePositional_EmitsSourceOrder() {
        Chunk chunk = CompileSource("""
            fn f(a: int, b: int): int {
            return a + b
            }
            f(10, 20)
            """);
        Assert.Equal(["10", "20"], ArgEmissions(chunk));
        Assert.Equal(2, CallArgCount(chunk));
    }
}
