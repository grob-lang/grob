using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// A <c>??</c> expression used as an operand is widened, and its enclosing operator chooses
/// its opcode, by the type the checker gave the <c>??</c> (R-78, D-456). The checker records
/// that type on <see cref="BinaryExpr.NilCoalesceType"/>; before it did, the compiler read the
/// <c>??</c> as unknown and took the other operand's type, so <c>2.25 - (n ?? 3)</c> left the
/// <c>int</c> unwidened and <c>(nil ?? 10.0) / 4</c> chose integer division.
/// </summary>
public sealed class NilCoalesceOperandTypeTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, SharedCatalogue.Stdlib).Check(unit);
        return (unit, bag);
    }

    private static List<OpCode> CompileToOpcodes(string source) {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource(source);
        Assert.False(bag.HasErrors, $"TypeChecker produced errors: {string.Join("; ", bag.Errors)}");
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors, $"Compiler produced errors: {string.Join("; ", bag.Errors)}");

        var ops = new List<OpCode>();
        int offset = 0;
        while (offset < chunk.Count) {
            var op = (OpCode)chunk.ReadByte(offset);
            ops.Add(op);
            offset += 1 + op switch {
                OpCode.Constant or OpCode.GetGlobal or OpCode.SetGlobal or OpCode.DefineGlobal
                    or OpCode.GetLocal or OpCode.SetLocal => 1,
                OpCode.ConstantLong or OpCode.Jump or OpCode.JumpIfFalse or OpCode.JumpIfTrue => 2,
                _ => 0,
            };
            if (op == OpCode.Return) break;
        }
        return ops;
    }

    private static BinaryExpr SingleNilCoalesce(CompilationUnit unit) {
        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        return Assert.Single(binaries.Nodes, b => b.Operator == BinaryOperator.NilCoalesce);
    }

    // -----------------------------------------------------------------------
    // The checker records the type it gave the ??.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("n: int? := 7\nx := n ?? 3", GrobType.Int)]
    [InlineData("x := nil ?? 10.0", GrobType.Float)]
    [InlineData("n: int? := 7\nm: int? := nil\nx := n ?? m", GrobType.NullableInt)]
    [InlineData("s: string? := nil\nx := s ?? \"\"", GrobType.String)]
    public void Checker_RecordsTheTypeItGaveTheNilCoalesce(string source, GrobType expected) {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource(source);
        Assert.False(bag.HasErrors, $"TypeChecker produced errors: {string.Join("; ", bag.Errors)}");

        Assert.Equal(expected, SingleNilCoalesce(unit).NilCoalesceType);
    }

    [Fact]
    public void Checker_LeavesNilCoalesceTypeUnknownOnOtherOperators() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("x := 1 + 2");
        Assert.False(bag.HasErrors);

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        Assert.Equal(GrobType.Unknown, Assert.Single(binaries.Nodes).NilCoalesceType);
    }

    // -----------------------------------------------------------------------
    // The compiler widens a ?? operand and chooses the opcode by that type.
    // -----------------------------------------------------------------------

    [Fact]
    public void IntNullableLeftOperandBesideAFloat_IsWidenedBeforeSubtractFloat() {
        List<OpCode> ops = CompileToOpcodes("n: int? := 7\nx := 2.25 - (n ?? 3)");
        Assert.Equal(
            [
                OpCode.Constant, OpCode.DefineGlobal,  // n := 7
                OpCode.Constant,                       // 2.25
                OpCode.GetGlobal, OpCode.Constant,     // n, 3
                OpCode.NilCoalesce,
                OpCode.IntToFloat,                     // the int ?? result widened
                OpCode.SubtractFloat,
                OpCode.DefineGlobal,
                OpCode.Return,
            ],
            ops);
    }

    [Fact]
    public void NilLeftOperandWithAFloatFallback_ChoosesDivideFloat() {
        List<OpCode> ops = CompileToOpcodes("x := (nil ?? 10.0) / 4");
        Assert.Equal(
            [
                OpCode.Nil, OpCode.Constant,           // nil, 10.0
                OpCode.NilCoalesce,
                OpCode.Constant,                       // 4
                OpCode.IntToFloat,                     // the int divisor widened
                OpCode.DivideFloat,
                OpCode.DefineGlobal,
                OpCode.Return,
            ],
            ops);
    }

    [Fact]
    public void IntNilCoalesceComparedWithAFloat_IsWidenedBeforeLessFloat() {
        List<OpCode> ops = CompileToOpcodes("n: int? := nil\nb := (n ?? 3) < 2.5");
        Assert.Equal(
            [
                OpCode.Nil, OpCode.DefineGlobal,       // n := nil
                OpCode.GetGlobal, OpCode.Constant,     // n, 3
                OpCode.NilCoalesce,
                OpCode.IntToFloat,
                OpCode.Constant,                       // 2.5
                OpCode.LessFloat,
                OpCode.DefineGlobal,
                OpCode.Return,
            ],
            ops);
    }

    [Fact]
    public void IntNilCoalesceAsATernaryArmBesideAFloatArm_IsWidenedInItsArm() {
        List<OpCode> ops = CompileToOpcodes("n: int? := nil\nc := true\ny := c ? (n ?? 1) : 2.5");
        int coalesce = ops.IndexOf(OpCode.NilCoalesce);
        Assert.True(coalesce >= 0, "expected NilCoalesce in the then-arm");
        Assert.Equal(OpCode.IntToFloat, ops[coalesce + 1]);
    }

    [Fact]
    public void IntNilCoalesceAddedIntoAFloatBinding_IsWidenedBeforeAddFloat() {
        List<OpCode> ops = CompileToOpcodes("x := 1.5\nn: int? := 2\nx += n ?? 1");
        int coalesce = ops.IndexOf(OpCode.NilCoalesce);
        Assert.True(coalesce >= 0, "expected NilCoalesce in the compound assignment's value");
        Assert.Equal([OpCode.IntToFloat, OpCode.AddFloat], ops.GetRange(coalesce + 1, 2));
    }

    private sealed class BinaryExprCollector : AstWalker {
        public List<BinaryExpr> Nodes { get; } = [];
        public override Unit VisitBinary(BinaryExpr node) {
            Nodes.Add(node);
            Visit(node.Left);
            Visit(node.Right);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }
}
