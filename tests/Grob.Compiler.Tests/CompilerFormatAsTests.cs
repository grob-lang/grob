using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Compiler-emission tests for Sprint 8 Increment E — <c>formatAs</c>. Both the function
/// form (<c>formatAs.table(items)</c>) and the chained form (<c>items.formatAs.table()</c>)
/// compile to the identical shape: <c>GetGlobal "formatAs.&lt;method&gt;"</c> (the callee,
/// pushed first), the receiver, the synthesised columns array (one <c>Constant</c> per
/// name plus <see cref="OpCode.NewArray"/>), then <see cref="OpCode.Call"/> with a fixed
/// operand of 2 — proving the chained form is genuinely a compile-time rewrite to the
/// function form, not a separate code path with separate bytecode.
/// </summary>
public sealed class CompilerFormatAsTests {
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

    /// <summary>Returns the single <see cref="BytecodeFunction"/> constant in a chunk's pool.</summary>
    private static BytecodeFunction SingleFunctionConstant(Chunk chunk) {
        var functions = new List<BytecodeFunction>();
        List<Instr> instrs = Decode(chunk);
        foreach (Instr instr in instrs.Where(i => i.Op == OpCode.Constant)) {
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

    private const string ItemType = """
        type Item {
            name: string
            price: float
        }
        """;

    [Fact]
    public void FunctionForm_Table_CompilesToGetGlobal_Receiver_ColumnsArray_Call2() {
        Chunk topLevel = CompileSource($$"""
            {{ItemType}}
            fn report(items: Item[]): string {
                return formatAs.table(items)
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;

        List<Instr> instrs = Decode(chunk);

        Instr getGlobal = Assert.Single(instrs, i => i.Op == OpCode.GetGlobal &&
            chunk.ReadConstant(i.Arg).TryAsString(out string? s) && s == "formatAs.table");
        int getGlobalIndex = instrs.IndexOf(getGlobal);

        // Receiver ('items', a local parameter) immediately follows the callee.
        Assert.Equal(OpCode.GetLocal, instrs[getGlobalIndex + 1].Op);

        // Then the synthesised columns array: two string Constants, then NewArray(2).
        Instr constName = instrs[getGlobalIndex + 2];
        Instr constPrice = instrs[getGlobalIndex + 3];
        Instr newArray = instrs[getGlobalIndex + 4];
        Assert.Equal(OpCode.Constant, constName.Op);
        Assert.Equal("name", chunk.ReadConstant(constName.Arg).AsString());
        Assert.Equal(OpCode.Constant, constPrice.Op);
        Assert.Equal("price", chunk.ReadConstant(constPrice.Arg).AsString());
        Assert.Equal(OpCode.NewArray, newArray.Op);
        Assert.Equal(2, newArray.Arg);

        // Then Call with a fixed operand of 2 (items, columns) regardless of source form.
        Instr call = instrs[getGlobalIndex + 5];
        Assert.Equal(OpCode.Call, call.Op);
        Assert.Equal(2, call.Arg);
    }

    [Fact]
    public void ChainedForm_Table_CompilesToIdenticalShapeAsFunctionForm() {
        Chunk functionFormTopLevel = CompileSource($$"""
            {{ItemType}}
            fn report(items: Item[]): string {
                return formatAs.table(items)
            }
            """);
        Chunk chainedFormTopLevel = CompileSource($$"""
            {{ItemType}}
            fn report(items: Item[]): string {
                return items.formatAs.table()
            }
            """);

        List<Instr> functionFormOps = Decode(SingleFunctionConstant(functionFormTopLevel).Bytecode)
            .Select(i => i with { Arg = 0 }).ToList();
        List<Instr> chainedFormOps = Decode(SingleFunctionConstant(chainedFormTopLevel).Bytecode)
            .Select(i => i with { Arg = 0 }).ToList();

        // Same opcode sequence (constant-pool indices differ trivially by declaration
        // order, so compared with operands zeroed) — the chained form is a genuine
        // compile-time rewrite to the function form's bytecode shape, not a separate path.
        Assert.Equal(functionFormOps, chainedFormOps);
    }

    [Fact]
    public void ChainedForm_ArityIsAlwaysTwo_RegardlessOfSourceArgumentCount() {
        // formatAs.csv(items) — one source-level argument — still compiles to Call(2):
        // the runtime native's arity is fixed, the source overloads collapse to it.
        Chunk topLevel = CompileSource($$"""
            {{ItemType}}
            fn report(items: Item[]): string {
                return formatAs.csv(items)
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;

        Instr call = Assert.Single(Decode(chunk), i => i.Op == OpCode.Call);
        Assert.Equal(2, call.Arg);
    }

    [Fact]
    public void ChainedForm_ExplicitColumns_EmitsSelectedOrderNotDeclarationOrder() {
        Chunk topLevel = CompileSource("""
            type Item {
                name: string
                price: float
                qty: int
            }
            fn report(items: Item[]): string {
                return items.formatAs.table(columns: ["price", "name"])
            }
            """);
        Chunk chunk = SingleFunctionConstant(topLevel).Bytecode;

        List<Instr> instrs = Decode(chunk);
        Instr newArray = Assert.Single(instrs, i => i.Op == OpCode.NewArray);
        int newArrayIndex = instrs.IndexOf(newArray);
        Assert.Equal(2, newArray.Arg);
        Assert.Equal("price", chunk.ReadConstant(instrs[newArrayIndex - 2].Arg).AsString());
        Assert.Equal("name", chunk.ReadConstant(instrs[newArrayIndex - 1].Arg).AsString());
    }
}
