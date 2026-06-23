using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 5 Increment D — closure (category-4 capture)
/// compiler emission. A capturing lambda must emit <see cref="OpCode.Closure"/>
/// (not <see cref="OpCode.Constant"/>) together with isLocal/index descriptor bytes,
/// and the lambda body must use <see cref="OpCode.GetUpvalue"/>/<see cref="OpCode.SetUpvalue"/>
/// rather than <see cref="OpCode.GetLocal"/>/<see cref="OpCode.GetGlobal"/> for the
/// captured variable. Non-capturing lambdas must continue to emit the cheaper
/// <see cref="OpCode.Constant"/> path.
/// </summary>
public sealed class CompilerClosureTests {
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

    /// <summary>
    /// Decodes <paramref name="chunk"/> into a flat instruction list with offsets and
    /// operands. Handles the variable-length <see cref="OpCode.Closure"/> encoding
    /// (pool-index byte + N×2 descriptor bytes keyed by
    /// <see cref="BytecodeFunction.UpvalueCount"/>).
    /// </summary>
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
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                case OpCode.Closure:
                    // Variable-length: pool-index byte then UpvalueCount×2 descriptor bytes.
                    arg = chunk.ReadByte(offset++);
                    if (chunk.ReadConstant(arg).TryAsFunction(out GrobFunction? gf) &&
                        gf is BytecodeFunction closureFn) {
                        offset += closureFn.UpvalueCount * 2;
                    }
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
    /// Returns the <see cref="BytecodeFunction"/> with <paramref name="name"/> found
    /// as a <see cref="OpCode.Constant"/> in the top-level <paramref name="chunk"/>.
    /// </summary>
    private static BytecodeFunction FindFunctionByName(Chunk chunk, string name) =>
        Decode(chunk)
            .Where(i => i.Op == OpCode.Constant)
            .Select(i => chunk.ReadConstant(i.Arg))
            .Where(v => v.IsFunction)
            .Select(v => v.AsFunction())
            .OfType<BytecodeFunction>()
            .First(f => f.Name == name);

    /// <summary>
    /// Returns the <see cref="BytecodeFunction"/> stored at pool index
    /// <paramref name="poolIdx"/> in <paramref name="chunk"/> — the fn object
    /// referenced directly by a <see cref="OpCode.Closure"/> operand.
    /// </summary>
    private static BytecodeFunction FnAtPoolIndex(Chunk chunk, int poolIdx) {
        GrobValue val = chunk.ReadConstant(poolIdx);
        Assert.True(val.IsFunction, "Constant at pool index is not a function.");
        GrobFunction fn = val.AsFunction();
        Assert.IsType<BytecodeFunction>(fn);
        return (BytecodeFunction)fn;
    }

    /// <summary>
    /// Walks the AST and collects every <see cref="IdentifierExpr"/> node.
    /// Used by the §3.1.1 invariant tests.
    /// </summary>
    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    // Source used by tests 1–4: a fn whose body contains a capturing lambda.
    // 'count' is a local of makeCounter at slot 0; the lambda captures it (category 4).
    private const string CounterSource = """
        fn makeCounter(): int {
          count := 0
          inc := () => {
            count = count + 1
            count
          }
          count
        }
        """;

    // -----------------------------------------------------------------------
    // Test 1: capturing lambda emits Closure opcode, not Constant.
    // -----------------------------------------------------------------------

    [Fact]
    public void CapturingLambda_EmitsClosureOpcode() {
        Chunk top = CompileSource(CounterSource);
        BytecodeFunction makeCounter = FindFunctionByName(top, "makeCounter");
        List<OpCode> ops = Opcodes(makeCounter.Bytecode);

        // The lambda captures 'count' → must emit Closure, not a bare Constant.
        Assert.Contains(OpCode.Closure, ops);

        // Guard: the lambda must be reachable only through Closure, never also as a
        // function-valued Constant (which would mean a second, un-captured copy).
        bool anyFunctionConstant = Decode(makeCounter.Bytecode)
            .Where(i => i.Op == OpCode.Constant)
            .Select(i => makeCounter.Bytecode.ReadConstant(i.Arg))
            .Any(v => v.IsFunction);
        Assert.False(anyFunctionConstant,
            "Capturing lambda must not also be emitted as a function-valued Constant.");
    }

    // -----------------------------------------------------------------------
    // Close-on-scope-exit: a captured block-local emits CloseUpvalue before the
    // block's slots are popped, so the upvalue migrates to the heap before the
    // slot can be reused (Addresses PR #88 review — CodeRabbit).
    // -----------------------------------------------------------------------

    [Fact]
    public void CapturedBlockLocal_EmitsCloseUpvalueAtScopeExit() {
        Chunk top = CompileSource("""
            fn test(): int {
              result := 0
              capture := () => 0
              {
                x := 99
                capture = () => x
              }
              y := 7
              result = capture()
              return result
            }
            """);

        BytecodeFunction test = FindFunctionByName(top, "test");
        List<OpCode> ops = Opcodes(test.Bytecode);

        // The block local 'x' is captured, so its scope exit must close the upvalue
        // rather than blindly PopN the slot.
        Assert.Contains(OpCode.CloseUpvalue, ops);
    }

    // -----------------------------------------------------------------------
    // Test 2: the lambda body uses GetUpvalue to read the captured variable.
    // -----------------------------------------------------------------------

    [Fact]
    public void CapturingLambda_LambdaBodyHasGetUpvalue() {
        Chunk top = CompileSource(CounterSource);
        BytecodeFunction makeCounter = FindFunctionByName(top, "makeCounter");

        // Find the Closure opcode, then get the lambda's BytecodeFunction from the pool.
        List<Instr> instrs = Decode(makeCounter.Bytecode);
        Instr closureInstr = Assert.Single(instrs, i => i.Op == OpCode.Closure);
        BytecodeFunction lambdaFn = FnAtPoolIndex(makeCounter.Bytecode, closureInstr.Arg);

        // The lambda body must read 'count' via GetUpvalue, not GetGlobal/GetLocal.
        List<OpCode> lambdaOps = Opcodes(lambdaFn.Bytecode);
        Assert.Contains(OpCode.GetUpvalue, lambdaOps);
        Assert.DoesNotContain(OpCode.GetGlobal, lambdaOps);
    }

    // -----------------------------------------------------------------------
    // Test 3: the lambda body uses SetUpvalue to write the captured variable.
    // -----------------------------------------------------------------------

    [Fact]
    public void CapturingLambda_LambdaBodyHasSetUpvalue() {
        Chunk top = CompileSource(CounterSource);
        BytecodeFunction makeCounter = FindFunctionByName(top, "makeCounter");

        List<Instr> instrs = Decode(makeCounter.Bytecode);
        Instr closureInstr = Assert.Single(instrs, i => i.Op == OpCode.Closure);
        BytecodeFunction lambdaFn = FnAtPoolIndex(makeCounter.Bytecode, closureInstr.Arg);

        List<OpCode> lambdaOps = Opcodes(lambdaFn.Bytecode);
        // 'count = count + 1' must compile to SetUpvalue, not SetGlobal/SetLocal.
        Assert.Contains(OpCode.SetUpvalue, lambdaOps);
        Assert.DoesNotContain(OpCode.SetGlobal, lambdaOps);
    }

    // -----------------------------------------------------------------------
    // Test 4: Closure descriptor bytes encode isLocal=1 and slot 0.
    // -----------------------------------------------------------------------

    [Fact]
    public void CapturingLambda_UpvalueDescriptorIsLocalSlotZero() {
        Chunk top = CompileSource(CounterSource);
        BytecodeFunction makeCounter = FindFunctionByName(top, "makeCounter");
        List<Instr> instrs = Decode(makeCounter.Bytecode);
        Instr closureInstr = Assert.Single(instrs, i => i.Op == OpCode.Closure);

        // Layout: Closure <poolIdx:1> <isLocal:1> <slot:1>
        //         closureInstr.Offset  +1          +2        +3
        Chunk fn = makeCounter.Bytecode;
        byte isLocal = fn.ReadByte(closureInstr.Offset + 2);
        byte slot = fn.ReadByte(closureInstr.Offset + 3);

        Assert.Equal(1, isLocal); // capturing a local of the enclosing fn
        Assert.Equal(0, slot);    // 'count' is at slot 0 (first local of makeCounter)
    }

    // -----------------------------------------------------------------------
    // Test 5: transitive capture — inner lambda uses isLocal=false (upvalue chain).
    // -----------------------------------------------------------------------

    [Fact]
    public void TransitiveCapture_InnerLambdaDescriptorIsLocalFalse() {
        // 'outer' has local 'x' (slot 0).
        // 'mid' (a lambda) captures x from outer → its Closure descriptor: isLocal=1, slot=0.
        // 'inner' (a lambda inside mid) captures x from mid's upvalue → isLocal=0, index=0.
        Chunk top = CompileSource("""
            fn outer(): int {
              x := 42
              mid := () => {
                inner := () => x
                x
              }
              mid()
            }
            """);

        BytecodeFunction outerFn = FindFunctionByName(top, "outer");

        // Verify mid's closure descriptor in outer's chunk: isLocal=1.
        List<Instr> outerInstrs = Decode(outerFn.Bytecode);
        Instr midClosure = Assert.Single(outerInstrs, i => i.Op == OpCode.Closure);
        byte midIsLocal = outerFn.Bytecode.ReadByte(midClosure.Offset + 2);
        Assert.Equal(1, midIsLocal); // mid captures outer's local → isLocal=1

        // Verify inner's closure descriptor in mid's chunk: isLocal=0.
        BytecodeFunction midFn = FnAtPoolIndex(outerFn.Bytecode, midClosure.Arg);
        List<Instr> midInstrs = Decode(midFn.Bytecode);
        Instr innerClosure = Assert.Single(midInstrs, i => i.Op == OpCode.Closure);
        byte innerIsLocal = midFn.Bytecode.ReadByte(innerClosure.Offset + 2);
        Assert.Equal(0, innerIsLocal); // inner captures mid's upvalue → isLocal=0
    }

    // -----------------------------------------------------------------------
    // Test 6: non-capturing lambda emits Constant, not Closure (guard test).
    // -----------------------------------------------------------------------

    [Fact]
    public void NonCapturingLambda_EmitsConstantNotClosure() {
        // This lambda only references 'x' (its own parameter) — no enclosing local.
        // Categories 1–3 path is unchanged; Constant must be emitted, not Closure.
        Chunk top = CompileSource("""
            arr := [1, 2, 3]
            arr.filter(x => x > 0)
            """);

        // The top-level chunk has a Constant whose value is the lambda's BytecodeFunction.
        List<OpCode> topOps = Opcodes(top);
        Assert.DoesNotContain(OpCode.Closure, topOps);
        Assert.Contains(OpCode.Constant, topOps);
    }

    // -----------------------------------------------------------------------
    // Test 7: §3.1.1 invariant holds for captured identifiers.
    // -----------------------------------------------------------------------

    [Fact]
    public void CapturingLambda_IdentifierNodes_SatisfySection311Invariant() {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(CounterSource, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");

        var collector = new IdentifierCollector();
        collector.Visit(unit);

        foreach (IdentifierExpr id in collector.Identifiers) {
            // §3.1.1: GrobType is a value type so Assert.NotNull is invalid; verify the
            // TypeChecker set it to a non-error type instead.
            Assert.NotEqual(GrobType.Error, id.ResolvedType);
            // Clean path: the declaration must be a real node, not the unresolved
            // sentinel (D-311). Assert the sentinel by reference.
            Assert.NotNull(id.Declaration);
            Assert.NotSame(UnresolvedDecl.Instance, id.Declaration);
        }
    }
}
