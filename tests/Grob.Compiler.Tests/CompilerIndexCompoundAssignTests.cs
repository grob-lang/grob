using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Sprint 9 Increment A4 (D-359) — compound assignment (<c>arr[i] op= v</c>) and
/// increment/decrement (<c>arr[i]++</c>/<c>--</c>) on index targets, closing the
/// silent-drop gap D-350 named. The receiver and index expressions are evaluated
/// exactly once, stashed in reserved temp locals, then read-modify-write composes
/// the existing <see cref="OpCode.GetIndex"/>/<see cref="OpCode.SetIndex"/> and the
/// ordinary typed binary opcodes — no new opcode is emitted.
/// </summary>
public sealed class CompilerIndexCompoundAssignTests {
    private static CompilationUnit CheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return unit;
    }

    private static DiagnosticBag TypeCheckDiagnostics(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

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
                case OpCode.NewArray:
                case OpCode.PopN:
                case OpCode.Call:
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break; // zero-operand opcode, including GetIndex/SetIndex
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    private static List<OpCode> Opcodes(Chunk chunk) => Decode(chunk).Select(i => i.Op).ToList();

    // -----------------------------------------------------------------------
    // arr[0] += 5 — evaluate-once receiver/index, read-modify-write, cleanup.
    // Exact opcode sequence, operand values (constant pool, temp slots) and the
    // emitted source line, per the compiler-tests bytecode contract.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayCompoundAssign_EmitsEvaluateOnceReadModifyWriteThenCleanup() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0] += 5\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal, // arr := [...]
                OpCode.GetGlobal, OpCode.Constant,                  // Ra = arr, Ia = 0
                OpCode.GetLocal, OpCode.GetLocal,                   // SetIndex's eventual receiver/index operands
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,  // read current value: arr[0]
                OpCode.Constant, OpCode.AddInt,                     // + 5
                OpCode.SetIndex,                                    // write back
                OpCode.PopN,                                        // release Ra/Ia
                OpCode.Return,
            ],
            ops);
    }

    [Fact]
    public void ArrayCompoundAssign_OperandsPointAtTheCorrectIndexAndRhsConstants() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0] += 5\n");
        List<Instr> instrs = Decode(chunk);

        // Ia's Constant (the index 0), immediately after Ra's GetGlobal.
        Instr getGlobal = instrs.Single(i => i.Op == OpCode.GetGlobal);
        Instr indexConstant = instrs[instrs.IndexOf(getGlobal) + 1];
        Assert.Equal(OpCode.Constant, indexConstant.Op);
        Assert.Equal(0L, chunk.ReadConstant(indexConstant.Arg).AsInt());

        // The RHS constant (5), immediately before the binary opcode.
        Instr addInt = instrs.Single(i => i.Op == OpCode.AddInt);
        Instr rhsConstant = instrs[instrs.IndexOf(addInt) - 1];
        Assert.Equal(OpCode.Constant, rhsConstant.Op);
        Assert.Equal(5L, chunk.ReadConstant(rhsConstant.Arg).AsInt());
    }

    [Fact]
    public void ArrayCompoundAssign_BothGetLocalPairsReferenceTheSameReceiverAndIndexSlots() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0] += 5\n");
        List<Instr> instrs = Decode(chunk);
        List<Instr> getLocals = instrs.Where(i => i.Op == OpCode.GetLocal).ToList();

        Assert.Equal(4, getLocals.Count);
        Assert.Equal(getLocals[0].Arg, getLocals[2].Arg); // receiver slot (Ra) reused
        Assert.Equal(getLocals[1].Arg, getLocals[3].Arg); // index slot (Ia) reused
        Assert.NotEqual(getLocals[0].Arg, getLocals[1].Arg);
    }

    [Fact]
    public void ArrayCompoundAssign_ReleasesBothTempLocalsViaPopNOfTwo() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0] += 5\n");
        Instr popN = Decode(chunk).Single(i => i.Op == OpCode.PopN);
        Assert.Equal(2, popN.Arg);
    }

    [Fact]
    public void ArrayCompoundAssign_SetIndexIsEmittedOnTheStatementsSourceLine() {
        // Source has the declaration on line 1 and the compound assignment on line 2.
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0] += 5\n");
        Instr setIndex = Decode(chunk).Single(i => i.Op == OpCode.SetIndex);
        Assert.Equal(2, chunk.GetLine(setIndex.Offset));
    }

    [Theory]
    [InlineData("-=", OpCode.SubtractInt)]
    [InlineData("*=", OpCode.MultiplyInt)]
    [InlineData("/=", OpCode.DivideInt)]
    [InlineData("%=", OpCode.ModuloInt)]
    public void ArrayCompoundAssign_EmitsExactOpcodesAndOperandsForEachOperator(string op, OpCode expectedBinaryOp) {
        Chunk chunk = CompileSource($"arr := [10, 20, 30]\narr[0] {op} 5\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant,
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, expectedBinaryOp,
                OpCode.SetIndex,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);

        List<Instr> instrs = Decode(chunk);
        Instr binaryOpInstr = instrs.Single(i => i.Op == expectedBinaryOp);
        Instr rhsConstant = instrs[instrs.IndexOf(binaryOpInstr) - 1];
        Assert.Equal(5L, chunk.ReadConstant(rhsConstant.Arg).AsInt());
        Instr popN = instrs.Single(i => i.Op == OpCode.PopN);
        Assert.Equal(2, popN.Arg);
    }

    // -----------------------------------------------------------------------
    // arr[0]++ / arr[0]-- — lowers to arr[0] += 1 / arr[0] -= 1 (int literal).
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayIncrement_EmitsExactOpcodesWithLiteralOneAndAddInt() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0]++\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant,
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetIndex,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);

        Instr constantBeforeAdd = Decode(chunk).Last(i => i.Op == OpCode.Constant);
        Assert.Equal(1L, chunk.ReadConstant(constantBeforeAdd.Arg).AsInt());
    }

    [Fact]
    public void ArrayDecrement_EmitsExactOpcodesWithLiteralOneAndSubtractInt() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\narr[0]--\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal,
                OpCode.GetGlobal, OpCode.Constant,
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, OpCode.SubtractInt,
                OpCode.SetIndex,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);

        Instr constantBeforeSubtract = Decode(chunk).Last(i => i.Op == OpCode.Constant);
        Assert.Equal(1L, chunk.ReadConstant(constantBeforeSubtract.Arg).AsInt());
    }

    // -----------------------------------------------------------------------
    // Float element — int RHS/literal widens to float (mirrors identifier path).
    // Covers all five compound operators, not just +=.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("+=", OpCode.AddFloat)]
    [InlineData("-=", OpCode.SubtractFloat)]
    [InlineData("*=", OpCode.MultiplyFloat)]
    [InlineData("/=", OpCode.DivideFloat)]
    [InlineData("%=", OpCode.ModuloFloat)]
    public void FloatArrayCompoundAssign_EmitsExactOpcodesWithIntToFloatCoercion(string op, OpCode expectedFloatOp) {
        Chunk chunk = CompileSource($"farr := [1.0, 2.0]\nfarr[0] {op} 1\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal, // farr := [1.0, 2.0]
                OpCode.GetGlobal, OpCode.Constant,
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, OpCode.IntToFloat, // RHS literal 1 widened to float
                expectedFloatOp,
                OpCode.SetIndex,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);
    }

    // -----------------------------------------------------------------------
    // Regression (sibling finding 3): GetExprType now resolves an IndexExpr's
    // element type, so a *plain* binary op over a float array element selects
    // float arithmetic, not the Unknown-defaults-to-int fallback.
    // -----------------------------------------------------------------------

    [Fact]
    public void PlainBinaryOp_FloatArrayIndexOperand_SelectsFloatArithmeticNotInt() {
        Chunk chunk = CompileSource("farr := [1.0, 2.0]\nx := farr[0] + 1\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Contains(OpCode.IntToFloat, ops);
        Assert.Contains(OpCode.AddFloat, ops);
        Assert.DoesNotContain(OpCode.AddInt, ops);
    }

    // -----------------------------------------------------------------------
    // Chained target: matrix[r][c] += v — inner receiver read once, then the
    // evaluate-once temp-local machine over the outer index. Asserted as an
    // exact opcode tail (mirroring CompilerIndexTests's chained-assignment
    // precedent), since the preceding matrix/r/c declarations are unrelated to
    // this increment's emission and would only obscure the shape under test.
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedMatrixCompoundAssign_EmitsExactTailReadingInnerReceiverOnceThenSettingIndex() {
        Chunk chunk = CompileSource("matrix := [[1, 2], [3, 4]]\nr := 0\nc := 1\nmatrix[r][c] += 9\n");
        List<Instr> instrs = Decode(chunk);
        List<OpCode> tail = instrs.Select(i => i.Op).SkipLast(1).TakeLast(13).ToList();

        Assert.Equal(
            [
                OpCode.GetGlobal, OpCode.GetGlobal, OpCode.GetIndex, // matrix[r] receiver read once (Ra)
                OpCode.GetGlobal,                                    // c (Ia)
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,   // current value: (matrix[r])[c]
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetIndex,
                OpCode.PopN,
            ],
            tail);

        Instr addInt = instrs.Single(i => i.Op == OpCode.AddInt);
        Instr constantNine = instrs[instrs.IndexOf(addInt) - 1];
        Assert.Equal(OpCode.Constant, constantNine.Op);
        Assert.Equal(9L, chunk.ReadConstant(constantNine.Arg).AsInt());
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.GetIndex));
        Assert.Equal(1, instrs.Count(i => i.Op == OpCode.SetIndex));
    }

    // -----------------------------------------------------------------------
    // Evaluate-once — the load-bearing case: a side-effecting receiver and a
    // side-effecting index expression are each visited exactly once. Asserted
    // as an exact opcode tail for the same reason as the chained case above.
    // -----------------------------------------------------------------------

    [Fact]
    public void CallReceiverAndCallIndexCompoundAssign_EmitsExactTailWithEachCallOnce() {
        Chunk chunk = CompileSource(
            "arr := [10, 20, 30]\n" +
            "fn getArr(): int[] { return arr }\n" +
            "fn nextIndex(): int { return 0 }\n" +
            "getArr()[nextIndex()] += 1\n");
        List<Instr> instrs = Decode(chunk);
        List<OpCode> tail = instrs.Select(i => i.Op).SkipLast(1).TakeLast(13).ToList();

        Assert.Equal(
            [
                OpCode.GetGlobal, OpCode.Call, // getArr() — receiver (Ra)
                OpCode.GetGlobal, OpCode.Call, // nextIndex() — index (Ia)
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetIndex,
                OpCode.PopN,
            ],
            tail);

        List<Instr> calls = instrs.Where(i => i.Op == OpCode.Call).ToList();
        Assert.Equal(2, calls.Count);
        Assert.All(calls, c => Assert.Equal(0, c.Arg)); // both nullary calls

        Instr addInt = instrs.Single(i => i.Op == OpCode.AddInt);
        Instr rhsConstant = instrs[instrs.IndexOf(addInt) - 1];
        Assert.Equal(OpCode.Constant, rhsConstant.Op);
        Assert.Equal(1L, chunk.ReadConstant(rhsConstant.Arg).AsInt());
    }

    // -----------------------------------------------------------------------
    // Type-check diagnostics — no new error code: E0002 (binary/increment
    // mismatch) and E0204 (readonly root), exactly as the identifier-target
    // and plain index-write paths already raise them. Every case asserts the
    // full diagnostic contract (code, 1-based line and column), per the
    // project's diagnostic-assertion convention.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("farr := [1.0, 2.0]\nfarr[0]++\n")]
    [InlineData("s := [\"a\", \"b\"]\ns[0]++\n")]
    public void IndexIncrement_NonIntElement_EmitsE0002(string source) {
        DiagnosticBag bag = TypeCheckDiagnostics(source);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void IndexCompoundAssign_MismatchedElementAndRhsTypes_EmitsE0002() {
        DiagnosticBag bag = TypeCheckDiagnostics("s := [\"a\", \"b\"]\ns[0] *= 2\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void IndexCompoundAssign_ReadonlyArray_EmitsE0204() {
        DiagnosticBag bag = TypeCheckDiagnostics("readonly arr := [1, 2, 3]\narr[0] += 1\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void IndexIncrement_ReadonlyArray_EmitsE0204() {
        DiagnosticBag bag = TypeCheckDiagnostics("readonly arr := [1, 2, 3]\narr[0]++\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0204", diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void IndexCompoundAssign_ValidIntArray_ProducesNoDiagnostics() {
        DiagnosticBag bag = TypeCheckDiagnostics("arr := [1, 2, 3]\narr[0] += 1\narr[1]++\narr[2]--\n");
        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Map target (hand-built AST — no map literal in the v1 parser, D-350's
    // established convention): stays permissive (Unknown element), compiles
    // to the same evaluate-once shape as the array case, no new opcode.
    // -----------------------------------------------------------------------

    [Fact]
    public void MapIndexCompoundAssign_HandBuiltTarget_EmitsExactEvaluateOnceShape() {
        var mapId = new IdentifierExpr(SourceRange.Unknown, "m") { ResolvedType = GrobType.Map };
        var keyLiteral = new StringLiteralExpr(SourceRange.Unknown, "k");
        var indexTarget = new IndexExpr(SourceRange.Unknown, mapId, keyLiteral);
        var compound = new CompoundAssignmentStmt(
            SourceRange.Unknown, indexTarget, CompoundAssignmentOperator.PlusAssign,
            new IntLiteralExpr(SourceRange.Unknown, 1));
        var unit = new CompilationUnit(SourceRange.Unknown, [compound]);

        DiagnosticBag bag = new();
        Chunk chunk = GrobCompiler.Compile(unit, bag);

        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<Instr> instrs = Decode(chunk);
        List<OpCode> ops = instrs.Select(i => i.Op).ToList();

        Assert.Equal(
            [
                OpCode.GetGlobal, OpCode.Constant,                  // Ra = m, Ia = "k"
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, OpCode.AddInt,                     // Unknown element defaults to int
                OpCode.SetIndex,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);

        Instr popN = instrs.Single(i => i.Op == OpCode.PopN);
        Assert.Equal(2, popN.Arg);
    }

    [Fact]
    public void MapIndexIncrement_HandBuiltTarget_EmitsExactEvaluateOnceShape() {
        var mapId = new IdentifierExpr(SourceRange.Unknown, "m") { ResolvedType = GrobType.Map };
        var keyLiteral = new StringLiteralExpr(SourceRange.Unknown, "k");
        var indexTarget = new IndexExpr(SourceRange.Unknown, mapId, keyLiteral);
        var increment = new IncrementStmt(SourceRange.Unknown, indexTarget, IncrementKind.Increment);
        var unit = new CompilationUnit(SourceRange.Unknown, [increment]);

        DiagnosticBag bag = new();
        Chunk chunk = GrobCompiler.Compile(unit, bag);

        Assert.False(bag.HasErrors,
            $"Compiler errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.GetGlobal, OpCode.Constant,
                OpCode.GetLocal, OpCode.GetLocal,
                OpCode.GetLocal, OpCode.GetLocal, OpCode.GetIndex,
                OpCode.Constant, OpCode.AddInt,
                OpCode.SetIndex,
                OpCode.PopN,
                OpCode.Return,
            ],
            ops);
    }
}
