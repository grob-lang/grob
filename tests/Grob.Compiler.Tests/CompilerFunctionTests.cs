using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 5 Increment A — <c>fn</c> declaration, call and
/// return emission.
/// </summary>
/// <remarks>
/// A <c>fn</c> compiles to a <see cref="BytecodeFunction"/> with its own
/// <see cref="Chunk"/> — parameters occupy the first local slots and the body ends
/// with a <see cref="OpCode.Return"/>. A call emits the callee, the arguments in
/// order, then <see cref="OpCode.Call"/> with the argument count as its operand.
/// The function value is stored as a constant and bound with
/// <see cref="OpCode.DefineGlobal"/> in the enclosing chunk.
/// </remarks>
public sealed class CompilerFunctionTests {
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

    /// <summary>Returns the single <see cref="BytecodeFunction"/> constant in a chunk's pool.</summary>
    private static BytecodeFunction SingleFunctionConstant(Chunk chunk) {
        var functions = new List<BytecodeFunction>();
        // The constant pool is not directly enumerable; recover the function via the
        // DefineGlobal-preceding Constant load, reading the constant index operand.
        List<Instr> instrs = Decode(chunk);
        for (int i = 0; i < instrs.Count; i++) {
            if (instrs[i].Op is OpCode.Constant or OpCode.ConstantLong) {
                GrobValue v = chunk.ReadConstant(instrs[i].Arg);
                if (v.IsFunction && v.AsFunction() is BytecodeFunction bf) functions.Add(bf);
            }
        }
        return Assert.Single(functions);
    }

    // -----------------------------------------------------------------------
    // fn declaration → BytecodeFunction with its own chunk
    // -----------------------------------------------------------------------

    [Fact]
    public void FnDecl_CompilesToBytecodeFunctionConstant_BoundWithDefineGlobal() {
        Chunk chunk = CompileSource("""
            fn add(a: int, b: int): int {
            return a + b
            }
            """);

        List<Instr> instrs = Decode(chunk);
        // The script chunk loads the function constant and binds it as a global.
        Assert.Contains(instrs, i => i.Op is OpCode.Constant or OpCode.ConstantLong);
        Assert.Contains(instrs, i => i.Op == OpCode.DefineGlobal);

        BytecodeFunction fn = SingleFunctionConstant(chunk);
        Assert.Equal("add", fn.Name);
        Assert.Equal(2, fn.Arity);
    }

    [Fact]
    public void FnDecl_CarriesErasedSignature_ForDisplay() {
        Chunk chunk = CompileSource("""
            fn tag(n: int, s: string): bool {
            return true
            }
            """);

        BytecodeFunction fn = SingleFunctionConstant(chunk);
        Assert.Equal(new[] { GrobType.Int, GrobType.String }, fn.ParameterTypes);
        Assert.Equal(GrobType.Bool, fn.ReturnType);
    }

    [Fact]
    public void FnDecl_NoParameters_CarriesEmptyParamsAndReturnType() {
        Chunk chunk = CompileSource("""
            fn answer(): int {
            return 42
            }
            """);

        BytecodeFunction fn = SingleFunctionConstant(chunk);
        Assert.Empty(fn.ParameterTypes);
        Assert.Equal(GrobType.Int, fn.ReturnType);
    }

    [Fact]
    public void FnDecl_NullableAndCollectionParams_CarryNullableAndCollectionKinds() {
        Chunk chunk = CompileSource("""
            fn f(a: int?, b: string[]): float {
            return 1.0
            }
            """);

        BytecodeFunction fn = SingleFunctionConstant(chunk);
        Assert.Equal(new[] { GrobType.NullableInt, GrobType.Array }, fn.ParameterTypes);
        Assert.Equal(GrobType.Float, fn.ReturnType);
    }

    [Fact]
    public void FnBody_ParametersOccupyFirstSlots_AndEndsWithReturn() {
        Chunk chunk = CompileSource("""
            fn add(a: int, b: int): int {
            return a + b
            }
            """);
        BytecodeFunction fn = SingleFunctionConstant(chunk);
        List<Instr> body = Decode(fn.Bytecode);

        // a + b reads slot 0 then slot 1 (parameters in declaration order).
        List<Instr> getLocals = body.Where(i => i.Op == OpCode.GetLocal).ToList();
        Assert.Equal(2, getLocals.Count);
        Assert.Equal(0, getLocals[0].Arg);
        Assert.Equal(1, getLocals[1].Arg);

        Assert.Contains(body, i => i.Op == OpCode.AddInt);
        // The body ends with a Return (the explicit one; the safety-net Return is last overall).
        Assert.Equal(OpCode.Return, body[^1].Op);
    }

    [Fact]
    public void FnBody_FallthroughWithoutReturn_GetsImplicitNilReturn() {
        // No explicit return — the compiler appends Nil + Return so control fall-off
        // is defined (returns nil) rather than running past the end of the chunk.
        Chunk chunk = CompileSource("""
            fn noop(): int {
            x := 1
            }
            """);
        BytecodeFunction fn = SingleFunctionConstant(chunk);
        List<Instr> body = Decode(fn.Bytecode);

        Assert.Equal(OpCode.Return, body[^1].Op);
        Assert.Equal(OpCode.Nil, body[^2].Op);
    }

    [Fact]
    public void FnBody_BareReturn_EmitsNilThenReturn() {
        // A value-less 'return' emits Nil + Return — the early-exit path returns nil.
        // The return type is nullable so the bare return (yielding nil) type-checks.
        Chunk chunk = CompileSource("""
            fn early(n: int): int? {
            if (n < 0) {
            return
            }
            return n
            }
            """);
        BytecodeFunction fn = SingleFunctionConstant(chunk);
        List<Instr> body = Decode(fn.Bytecode);

        // The bare return inside the if-block: a Nil immediately followed by Return.
        bool hasNilReturn = false;
        for (int i = 0; i < body.Count - 1; i++) {
            if (body[i].Op == OpCode.Nil && body[i + 1].Op == OpCode.Return) {
                hasNilReturn = true;
                break;
            }
        }
        Assert.True(hasNilReturn, "expected a Nil followed by Return for the bare return");
    }

    // -----------------------------------------------------------------------
    // call → callee + args + Call argCount
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_EmitsCalleeThenArgsThenCallWithArgCount() {
        Chunk chunk = CompileSource("""
            fn add(a: int, b: int): int {
            return a + b
            }
            add(10, 20)
            """);
        List<Instr> instrs = Decode(chunk);

        Instr call = Assert.Single(instrs, i => i.Op == OpCode.Call);
        Assert.Equal(2, call.Arg); // argument count operand

        // The callee is loaded (GetGlobal add) before the Call, and the result of
        // the expression statement is discarded with Pop.
        int callIdx = instrs.IndexOf(call);
        Assert.Contains(instrs.Take(callIdx), i => i.Op == OpCode.GetGlobal);
        Assert.Equal(OpCode.Pop, instrs[callIdx + 1].Op);
    }

    [Fact]
    public void Call_ZeroArguments_EmitsCallWithZeroOperand() {
        Chunk chunk = CompileSource("""
            fn answer(): int {
            return 42
            }
            answer()
            """);
        Instr call = Assert.Single(Decode(chunk), i => i.Op == OpCode.Call);
        Assert.Equal(0, call.Arg);
    }

    [Fact]
    public void CallResult_UsedInArithmetic_SelectsTypedOpcode() {
        // The call's static type must resolve to the function's return type so the
        // surrounding '+' selects AddInt rather than throwing on an Unknown operand.
        Chunk chunk = CompileSource("""
            fn one(): int {
            return 1
            }
            x := one() + one()
            """);
        Assert.Contains(OpCode.AddInt, Opcodes(chunk));
    }

    // -----------------------------------------------------------------------
    // fn hoisting (D-321): a top-level fn binding is established in a prologue
    // emitted before the first top-level statement, so a statement that calls a
    // function declared later in source resolves and runs.
    // -----------------------------------------------------------------------

    /// <summary>Returns the global name a <see cref="OpCode.DefineGlobal"/> binds.</summary>
    private static string DefineGlobalName(Chunk chunk, Instr defineGlobal) =>
        chunk.ReadConstant(defineGlobal.Arg).AsString();

    [Fact]
    public void ForwardFunctionCall_Compiles_AndBindsFunctionBeforeTheCall() {
        // print(greet()) precedes 'fn greet' in source; the fn DefineGlobal must be
        // hoisted ahead of the Call so the slot is Initialised at the call site.
        Chunk chunk = CompileSource("""
            print(greet())
            fn greet(): string {
            return "hi"
            }
            """);
        List<Instr> instrs = Decode(chunk);

        Instr greetBind = Assert.Single(
            instrs, i => i.Op == OpCode.DefineGlobal && DefineGlobalName(chunk, i) == "greet");
        Instr call = Assert.Single(instrs, i => i.Op == OpCode.Call);

        Assert.True(greetBind.Offset < call.Offset,
            "the fn's DefineGlobal must be emitted in the prologue, before the forward call");
    }

    [Fact]
    public void TopLevelFns_AreHoisted_AheadOfTopLevelStatementCode() {
        // The mutable top-level binding 'x' is declared before the fn in source, but
        // the fn's DefineGlobal is hoisted into the prologue ahead of x's DefineGlobal.
        Chunk chunk = CompileSource("""
            x := 1
            fn f(): int {
            return 2
            }
            """);
        List<Instr> instrs = Decode(chunk);

        Instr fBind = Assert.Single(
            instrs, i => i.Op == OpCode.DefineGlobal && DefineGlobalName(chunk, i) == "f");
        Instr xBind = Assert.Single(
            instrs, i => i.Op == OpCode.DefineGlobal && DefineGlobalName(chunk, i) == "x");

        Assert.True(fBind.Offset < xBind.Offset,
            "the fn prologue runs before any top-level value-binding code");
    }
}
