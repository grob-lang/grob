using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 4 Increment C — the <c>for...in</c> lowering.
/// </summary>
/// <remarks>
/// Each form lowers to a <c>while</c> machine over the Increment B loop-context
/// model. These tests confirm the comparison opcode matches the iteration
/// direction (<c>LessInt</c> for arrays/maps, <c>LessEqualInt</c> for an
/// ascending range, <c>GreaterEqualInt</c> for a descending range), that the map
/// keys array is materialised once before the loop, and that <c>continue</c>
/// targets the increment step (a forward <c>Jump</c> landing on the counter
/// update) rather than the condition — otherwise the counter never advances.
/// <para>
/// The map form is built from a hand-constructed AST because there is no map
/// literal in the v1 parser (out-of-scope parser work); the iterable is an
/// identifier annotated <see cref="GrobType.Map"/>, exactly what the type checker
/// would have produced.
/// </para>
/// </remarks>
public sealed class CompilerForInTests {
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
                case OpCode.IncrementFloat:
                case OpCode.DecrementFloat:
                case OpCode.GetProperty:
                case OpCode.SetProperty:
                case OpCode.NewArray:
                case OpCode.NewStruct:
                case OpCode.NewAnonStruct:
                case OpCode.BuildString:
                case OpCode.Call:
                case OpCode.Closure:
                case OpCode.GetUpvalue:
                case OpCode.SetUpvalue:
                case OpCode.Import:
                case OpCode.TryBegin:
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

    // -----------------------------------------------------------------------
    // Array forms — i < arr.length, IncrementInt counter
    // -----------------------------------------------------------------------

    [Fact]
    public void ArraySingle_UsesLessIntAndArrayOps() {
        List<OpCode> ops = Opcodes(CompileSource("for item in [1, 2, 3] {\nprint(item)\n}"));
        Assert.Contains(OpCode.NewArray, ops);   // the literal materialises an array
        Assert.Contains(OpCode.GetProperty, ops); // arr.length
        Assert.Contains(OpCode.GetIndex, ops);    // arr[i]
        Assert.Contains(OpCode.LessInt, ops);     // i < length — strict, exclusive
        Assert.Contains(OpCode.IncrementInt, ops);
        Assert.Contains(OpCode.Loop, ops);
        Assert.DoesNotContain(OpCode.LessEqualInt, ops);
    }

    [Fact]
    public void ArrayIndex_UsesLessIntAndArrayOps() {
        List<OpCode> ops = Opcodes(CompileSource("for i, item in [1, 2, 3] {\nprint(i)\n}"));
        Assert.Contains(OpCode.GetProperty, ops);
        Assert.Contains(OpCode.GetIndex, ops);
        Assert.Contains(OpCode.LessInt, ops);
        Assert.Contains(OpCode.IncrementInt, ops);
    }

    // -----------------------------------------------------------------------
    // Range forms — inclusive <=, step direction
    // -----------------------------------------------------------------------

    [Fact]
    public void AscendingRange_UsesLessEqualIntAndIncrement() {
        List<OpCode> ops = Opcodes(CompileSource("for i in 0..3 {\nprint(i)\n}"));
        Assert.Contains(OpCode.LessEqualInt, ops); // inclusive end bound
        Assert.Contains(OpCode.IncrementInt, ops);
        Assert.Contains(OpCode.Loop, ops);
        Assert.DoesNotContain(OpCode.GetProperty, ops); // ranges read no length
        Assert.DoesNotContain(OpCode.GetIndex, ops);
    }

    [Fact]
    public void AscendingRangeWithStep_UsesLessEqualIntAndAddInt() {
        List<OpCode> ops = Opcodes(CompileSource("for i in 0..10 step 5 {\nprint(i)\n}"));
        Assert.Contains(OpCode.LessEqualInt, ops);
        Assert.Contains(OpCode.AddInt, ops); // i += step
        Assert.DoesNotContain(OpCode.IncrementInt, ops); // stepped, not ++1
    }

    [Fact]
    public void DescendingRangeWithNegativeStep_UsesGreaterEqualIntAndAddInt() {
        List<OpCode> ops = Opcodes(CompileSource("for i in 3..0 step -1 {\nprint(i)\n}"));
        Assert.Contains(OpCode.GreaterEqualInt, ops); // descending compares with >=
        Assert.Contains(OpCode.AddInt, ops);          // i += (negative) step
        Assert.DoesNotContain(OpCode.LessEqualInt, ops);
    }

    // -----------------------------------------------------------------------
    // continue targets the increment step (forward Jump onto the counter update)
    // -----------------------------------------------------------------------

    [Fact]
    public void ContinueInsideForIn_JumpsToTheIncrementStep() {
        Chunk chunk = CompileSource("for i in 0..3 {\ncontinue\n}");
        List<Instr> instrs = Decode(chunk);

        // The only forward Jump in a for...in lowering is the continue.
        Instr jump = instrs.Single(i => i.Op == OpCode.Jump);
        int target = jump.Offset + 3 + jump.Arg; // opcode(1) + operand(2) + offset

        // The instruction landed on must be the counter increment, not the condition.
        Instr landed = instrs.Single(i => i.Offset == target);
        Assert.Equal(OpCode.IncrementInt, landed.Op);
    }

    // -----------------------------------------------------------------------
    // Map form (hand-built AST) — keys materialised once, k=keys[i], v=map[k]
    // -----------------------------------------------------------------------

    private static Chunk CompileMapForIn(BlockStmt body) {
        var iterable = new IdentifierExpr(SourceRange.Unknown, "m") {
            ResolvedType = GrobType.Map,
        };
        var forIn = new ForInStmt(SourceRange.Unknown, ["k", "v"], iterable, body);
        var unit = new CompilationUnit(SourceRange.Unknown, [forIn]);
        DiagnosticBag bag = new();
        return GrobCompiler.Compile(unit, bag);
    }

    [Fact]
    public void Map_MaterialisesKeysOnceBeforeTheLoop() {
        Chunk chunk = CompileMapForIn(new BlockStmt(SourceRange.Unknown, []));
        List<Instr> instrs = Decode(chunk);

        // Two GetProperty: the keys materialisation (once, pre-loop) and the
        // keys.length read inside the condition.
        List<Instr> getProps = instrs.Where(i => i.Op == OpCode.GetProperty).ToList();
        Assert.Equal(2, getProps.Count);

        // The keys materialisation must sit before the loop's backward Jump.
        int loopOffset = instrs.Single(i => i.Op == OpCode.Loop).Offset;
        Assert.True(getProps[0].Offset < loopOffset);
    }

    [Fact]
    public void Map_ReadsKeyThenValueByIndex() {
        Chunk chunk = CompileMapForIn(new BlockStmt(SourceRange.Unknown, []));
        List<OpCode> ops = Opcodes(chunk);

        // k = keys[i] and v = map[k] — two index reads per iteration.
        Assert.Equal(2, ops.Count(o => o == OpCode.GetIndex));
        Assert.Contains(OpCode.LessInt, ops); // i < keys.length
        Assert.Contains(OpCode.IncrementInt, ops);
    }
}
