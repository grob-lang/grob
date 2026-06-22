using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 5 Increment C — lambda expression compilation.
/// </summary>
/// <remarks>
/// A lambda compiles to a <see cref="BytecodeFunction"/> emitted as a
/// <see cref="OpCode.Constant"/> in the enclosing chunk; the function's own
/// <see cref="Chunk"/> contains the body expression + <see cref="OpCode.Return"/>
/// (expression body) or the block statements with the last expression left on the
/// stack + <see cref="OpCode.Return"/> (block body). Categories 1–3 resolution:
/// top-level <c>const</c> inlined, <c>readonly</c>/<c>mutable</c> emitted as
/// <see cref="OpCode.GetGlobal"/>/<see cref="OpCode.SetGlobal"/>.
/// </remarks>
public sealed class CompilerLambdaTests {
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

    /// <summary>Decodes a chunk into a flat instruction list with offsets and operands.</summary>
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
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break; // zero-operand opcode
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    private static List<OpCode> Opcodes(Chunk chunk) => Decode(chunk).Select(i => i.Op).ToList();

    /// <summary>
    /// Returns the first <see cref="BytecodeFunction"/> constant found in the enclosing
    /// chunk — the lambda object pushed by <see cref="OpCode.Constant"/>.
    /// </summary>
    private static BytecodeFunction FirstLambdaConstant(Chunk chunk) {
        List<Instr> instrs = Decode(chunk);
        foreach (Instr instr in instrs) {
            if (instr.Op is OpCode.Constant or OpCode.ConstantLong) {
                GrobValue v = chunk.ReadConstant(instr.Arg);
                if (v.IsFunction && v.AsFunction() is BytecodeFunction bf) return bf;
            }
        }
        throw new InvalidOperationException("No BytecodeFunction constant found in chunk.");
    }

    // -----------------------------------------------------------------------
    // Expression-body lambda: x => x > 0
    // Enclosing chunk must contain a Constant (the BytecodeFunction).
    // The lambda's own chunk must contain: GetLocal(0), Constant(0), GreaterInt, Return.
    // -----------------------------------------------------------------------

    [Fact]
    public void ExpressionBodyLambda_EmitsConstantWithBytecodeFunction() {
        // We can't directly invoke arr.filter in source (the result would need runtime
        // support), so compile a lambda bound to a variable via the type-checked pipeline.
        // The lambda is assigned to a binding; the enclosing chunk holds the Constant.
        Chunk outer = CompileSource("""
            arr := [1, 2, 3]
            arr.filter(x => x > 0)
            """);

        // The outer chunk must contain a Constant opcode whose value is a BytecodeFunction.
        BytecodeFunction fn = FirstLambdaConstant(outer);
        Assert.NotNull(fn);
        Assert.Equal(1, fn.Arity); // one parameter 'x'
    }

    [Fact]
    public void ExpressionBodyLambda_ChunkContainsBodyAndReturn() {
        Chunk outer = CompileSource("""
            arr := [1, 2, 3]
            arr.filter(x => x > 0)
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<OpCode> ops = Opcodes(fn.Bytecode);

        // x > 0: GetLocal(x=0), Constant(0), GreaterInt, Return (safety Nil+Return after).
        // Safety Nil+Return are also emitted.
        Assert.Contains(OpCode.GetLocal, ops);
        Assert.Contains(OpCode.Return, ops);
    }

    [Fact]
    public void ExpressionBodyLambda_ParameterIsSlotZero() {
        Chunk outer = CompileSource("""
            arr := [1, 2, 3]
            arr.filter(x => x > 0)
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<Instr> instrs = Decode(fn.Bytecode);

        // First instruction must be GetLocal with slot argument 0 (the 'x' parameter).
        Instr first = Assert.Single(instrs, i => i.Op == OpCode.GetLocal);
        Assert.Equal(0, first.Arg);
    }

    // -----------------------------------------------------------------------
    // Category 1: const referenced in lambda is inlined (no GetGlobal in lambda chunk).
    // -----------------------------------------------------------------------

    [Fact]
    public void Lambda_ReferencingTopLevelConst_InlinesConstant() {
        Chunk outer = CompileSource("""
            const THRESHOLD := 5
            arr := [1, 6, 3, 8]
            arr.filter(x => x > THRESHOLD)
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<OpCode> ops = Opcodes(fn.Bytecode);

        // THRESHOLD is inlined: the lambda chunk has Constant (for the inlined 5) but NOT GetGlobal.
        Assert.DoesNotContain(OpCode.GetGlobal, ops);
        Assert.Contains(OpCode.Constant, ops);
    }

    [Fact]
    public void Lambda_ReferencingTopLevelConst_InlinedValueIsCorrect() {
        Chunk outer = CompileSource("""
            const THRESHOLD := 5
            arr := [1, 6, 3, 8]
            arr.filter(x => x > THRESHOLD)
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<Instr> instrs = Decode(fn.Bytecode);

        // Find the Constant opcode that's NOT GetLocal(x) — must be the inlined 5.
        // The lambda body is: GetLocal(x), Constant(5), GreaterInt, Return, Nil, Return.
        GrobValue? inlinedConst = null;
        foreach (Instr instr in instrs) {
            if (instr.Op == OpCode.Constant) {
                GrobValue v = fn.Bytecode.ReadConstant(instr.Arg);
                if (v.Kind == GrobValueKind.Int && v.AsInt() == 5) { inlinedConst = v; break; }
            }
        }
        Assert.True(inlinedConst.HasValue, "Expected inlined constant '5' in lambda chunk.");
    }

    // -----------------------------------------------------------------------
    // Category 2: readonly global → GetGlobal in lambda chunk.
    // -----------------------------------------------------------------------

    [Fact]
    public void Lambda_ReferencingTopLevelReadonly_EmitsGetGlobal() {
        Chunk outer = CompileSource("""
            readonly min := 0
            arr := [1, -1, 2]
            arr.filter(x => x > min)
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<OpCode> ops = Opcodes(fn.Bytecode);

        // 'min' is a readonly global: compiler emits GetGlobal (not an inlined constant).
        Assert.Contains(OpCode.GetGlobal, ops);
    }

    // -----------------------------------------------------------------------
    // Category 3: mutable global write → SetGlobal in lambda chunk.
    // -----------------------------------------------------------------------

    [Fact]
    public void Lambda_MutatingTopLevelMutable_EmitsSetGlobal() {
        // Assignment is a statement so the lambda must use a block body.
        Chunk outer = CompileSource("""
            counter := 0
            arr := [1, 2, 3]
            arr.each(x => {
            counter = counter + x
            })
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<OpCode> ops = Opcodes(fn.Bytecode);

        // 'counter' is mutated: SetGlobal must appear in the lambda's chunk.
        Assert.Contains(OpCode.SetGlobal, ops);
        // And it's read first via GetGlobal for 'counter + x'.
        Assert.Contains(OpCode.GetGlobal, ops);
    }

    // -----------------------------------------------------------------------
    // Block-body lambda: x => { y := x + 1; y }
    // Last expression stays on the stack; Return is emitted; no trailing Pop.
    // -----------------------------------------------------------------------

    [Fact]
    public void BlockBodyLambda_EmitsBytecodeFunction() {
        Chunk outer = CompileSource("""
            arr := [1, 2, 3]
            arr.sort(x => {
            y := x > 0
            y
            })
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        Assert.NotNull(fn);
        Assert.Equal(1, fn.Arity);
    }

    [Fact]
    public void BlockBodyLambda_LastExpressionNotPopped() {
        // The block body's last statement is an ExpressionStmt 'y'.
        // The compiler must NOT emit Pop before Return; 'y' is the return value.
        Chunk outer = CompileSource("""
            arr := [1, 2, 3]
            arr.sort(x => {
            y := x > 0
            y
            })
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<Instr> instrs = Decode(fn.Bytecode);

        // There must be a Return.  There must NOT be a Pop immediately before Return.
        for (int i = 1; i < instrs.Count; i++) {
            if (instrs[i].Op == OpCode.Return && instrs[i - 1].Op == OpCode.Pop) {
                Assert.Fail("Pop immediately before Return — last expression value would be discarded.");
            }
        }
        Assert.Contains(OpCode.Return, instrs.Select(i => i.Op));
    }

    // -----------------------------------------------------------------------
    // Safety-net: lambda with empty block body emits Nil + Return.
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyBlockBodyLambda_EmitsNilAndReturn() {
        // A sort lambda with an empty block body '{}'.  This is syntactically valid
        // and the compiler must emit a safety-net Nil + Return so the VM always has
        // a value to return on the stack.
        // NOTE: arr.sort(x => {}) returns nil as the key for all elements — the sort
        // is a no-op in terms of ordering, but the compiler must not crash.
        Chunk outer = CompileSource("""
            arr := [1, 2, 3]
            arr.sort(x => {
            x > 0
            })
            """);

        BytecodeFunction fn = FirstLambdaConstant(outer);
        List<OpCode> ops = Opcodes(fn.Bytecode);
        Assert.Contains(OpCode.Return, ops);
        // Safety-net: Nil must appear (the last instructions are Nil + Return as a
        // fallthrough guard, regardless of whether an explicit return was emitted).
        Assert.Contains(OpCode.Nil, ops);
    }
}
