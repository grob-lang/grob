using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;
using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// Bytecode-shape tests for Sprint 9 Increment A — the <c>VisitIndex</c> array index
/// read emission (D-345, D-348). <c>arr[i]</c> compiles to the receiver, the index
/// expression, then <see cref="OpCode.GetIndex"/> over the existing value-stack
/// discipline; <c>matrix[r][c]</c> chains two <see cref="OpCode.GetIndex"/>
/// instructions via nested <see cref="IndexExpr"/> nodes. No new opcode is emitted.
/// </summary>
public sealed class CompilerIndexTests {
    private static CompilationUnit CheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        Assert.False(bag.HasErrors,
            $"TypeChecker errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        return unit;
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
                    arg = chunk.ReadByte(offset);
                    offset += 1;
                    break;
                default:
                    break; // zero-operand opcode, including GetIndex
            }
            result.Add(new Instr(here, op, arg));
        }
        return result;
    }

    private static List<OpCode> Opcodes(Chunk chunk) => Decode(chunk).Select(i => i.Op).ToList();

    // -----------------------------------------------------------------------
    // arr[0] on a literal array — receiver + index + GetIndex, no new opcode
    // -----------------------------------------------------------------------

    [Fact]
    public void LiteralArrayIndex_EmitsReceiverIndexThenGetIndex() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\nx := arr[0]\n");
        List<OpCode> ops = Opcodes(chunk);

        Assert.Equal(
            [
                OpCode.Constant, OpCode.Constant, OpCode.Constant, OpCode.NewArray, OpCode.DefineGlobal, // arr := [10, 20, 30]
                OpCode.GetGlobal, OpCode.Constant, OpCode.GetIndex, OpCode.DefineGlobal,                 // x := arr[0]
                OpCode.Return, // implicit top-level return
            ],
            ops);
    }

    [Fact]
    public void ArrayIndex_OperandsPointAtTheCorrectReceiverAndIndexConstants() {
        Chunk chunk = CompileSource("arr := [10, 20, 30]\nx := arr[1]\n");
        List<Instr> instrs = Decode(chunk);

        Instr getGlobal = instrs.Single(i => i.Op == OpCode.GetGlobal);
        Assert.Equal("arr", chunk.ReadConstant(getGlobal.Arg).AsString());

        Instr getIndex = instrs.Single(i => i.Op == OpCode.GetIndex);
        Instr indexConstant = instrs[instrs.IndexOf(getIndex) - 1];
        Assert.Equal(OpCode.Constant, indexConstant.Op);
        Assert.Equal(1L, chunk.ReadConstant(indexConstant.Arg).AsInt());
    }

    // -----------------------------------------------------------------------
    // matrix[r][c] (D-112 multi-dimensional) — chained index reads
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedMatrixIndex_EmitsTwoGetIndexInstructionsInSourceOrder() {
        Chunk chunk = CompileSource("matrix := [[1, 2], [3, 4]]\nr := 0\nc := 1\nx := matrix[r][c]\n");
        List<Instr> instrs = Decode(chunk);

        // The final statement's tail: GetGlobal(matrix), GetGlobal(r), GetIndex,
        // GetGlobal(c), GetIndex, DefineGlobal(x) — skip the trailing implicit
        // top-level Return.
        List<Instr> tail = instrs.SkipLast(1).TakeLast(6).ToList();
        Assert.Equal(
            [OpCode.GetGlobal, OpCode.GetGlobal, OpCode.GetIndex, OpCode.GetGlobal, OpCode.GetIndex, OpCode.DefineGlobal],
            tail.Select(i => i.Op).ToList());

        Assert.Equal("matrix", chunk.ReadConstant(tail[0].Arg).AsString());
        Assert.Equal("r", chunk.ReadConstant(tail[1].Arg).AsString());
        Assert.Equal("c", chunk.ReadConstant(tail[3].Arg).AsString());
        Assert.Equal(2, instrs.Count(i => i.Op == OpCode.GetIndex));
    }

    // -----------------------------------------------------------------------
    // The §3.1.1 identifier invariant is unaffected by indexing through it —
    // arr's own IdentifierExpr node inside arr[0] still carries the correct
    // ResolvedType/Declaration (IndexExpr itself carries neither property;
    // §3.1.1 scopes the invariant to identifier nodes, not every expression).
    // -----------------------------------------------------------------------

    [Fact]
    public void IndexTarget_IdentifierNodeKeepsResolvedTypeAndDeclarationInvariant() {
        CompilationUnit unit = CheckSource("arr := [1, 2, 3]\nx := arr[0]\n");

        var arrDecl = unit.TopLevel.OfType<VarDeclStmt>().Single(s => s.Name == "arr");
        var xDecl = unit.TopLevel.OfType<VarDeclStmt>().Single(s => s.Name == "x");

        var indexExpr = Assert.IsType<IndexExpr>(xDecl.Initializer);
        var target = Assert.IsType<IdentifierExpr>(indexExpr.Target);

        Assert.Equal(GrobType.Array, target.ResolvedType);
        Assert.NotNull(target.Declaration);
        Assert.Same(arrDecl, target.Declaration);
    }
}
