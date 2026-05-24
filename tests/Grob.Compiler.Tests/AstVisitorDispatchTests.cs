using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.AstTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Verifies that <see cref="AstNode.Accept{T}(AstVisitor{T})"/> routes every
/// concrete node to its matching <c>VisitXxx</c> hook.
/// </summary>
public class AstVisitorDispatchTests {
    /// <summary>A visitor that returns the name of the hook the dispatcher chose.</summary>
    private sealed class HookNameVisitor : AstVisitor<string> {
        public override string VisitIntLiteral(IntLiteralExpr node) => nameof(VisitIntLiteral);
        public override string VisitFloatLiteral(FloatLiteralExpr node) => nameof(VisitFloatLiteral);
        public override string VisitStringLiteral(StringLiteralExpr node) => nameof(VisitStringLiteral);
        public override string VisitRawStringLiteral(RawStringLiteralExpr node) => nameof(VisitRawStringLiteral);
        public override string VisitInterpolatedString(InterpolatedStringExpr node) => nameof(VisitInterpolatedString);
        public override string VisitRegexLiteral(RegexLiteralExpr node) => nameof(VisitRegexLiteral);
        public override string VisitBoolLiteral(BoolLiteralExpr node) => nameof(VisitBoolLiteral);
        public override string VisitNilLiteral(NilLiteralExpr node) => nameof(VisitNilLiteral);
        public override string VisitIdentifier(IdentifierExpr node) => nameof(VisitIdentifier);
        public override string VisitUnary(UnaryExpr node) => nameof(VisitUnary);
        public override string VisitBinary(BinaryExpr node) => nameof(VisitBinary);
        public override string VisitGrouping(GroupingExpr node) => nameof(VisitGrouping);
        public override string VisitTernary(TernaryExpr node) => nameof(VisitTernary);
        public override string VisitArrayLiteral(ArrayLiteralExpr node) => nameof(VisitArrayLiteral);
        public override string VisitIndex(IndexExpr node) => nameof(VisitIndex);
        public override string VisitMemberAccess(MemberAccessExpr node) => nameof(VisitMemberAccess);
        public override string VisitCall(CallExpr node) => nameof(VisitCall);
        public override string VisitLambda(LambdaExpr node) => nameof(VisitLambda);
        public override string VisitNumericRange(NumericRangeExpr node) => nameof(VisitNumericRange);
        public override string VisitErrorExpr(ErrorExpr node) => nameof(VisitErrorExpr);

        public override string VisitBlock(BlockStmt node) => nameof(VisitBlock);
        public override string VisitVarDecl(VarDeclStmt node) => nameof(VisitVarDecl);
        public override string VisitAssignment(AssignmentStmt node) => nameof(VisitAssignment);
        public override string VisitCompoundAssignment(CompoundAssignmentStmt node) => nameof(VisitCompoundAssignment);
        public override string VisitIncrement(IncrementStmt node) => nameof(VisitIncrement);
        public override string VisitExpressionStmt(ExpressionStmt node) => nameof(VisitExpressionStmt);
        public override string VisitIf(IfStmt node) => nameof(VisitIf);
        public override string VisitWhile(WhileStmt node) => nameof(VisitWhile);
        public override string VisitForIn(ForInStmt node) => nameof(VisitForIn);
        public override string VisitSelect(SelectStmt node) => nameof(VisitSelect);
        public override string VisitBreak(BreakStmt node) => nameof(VisitBreak);
        public override string VisitContinue(ContinueStmt node) => nameof(VisitContinue);
        public override string VisitReturn(ReturnStmt node) => nameof(VisitReturn);
        public override string VisitTry(TryStmt node) => nameof(VisitTry);
        public override string VisitErrorStmt(ErrorStmt node) => nameof(VisitErrorStmt);

        public override string VisitFnDecl(FnDecl node) => nameof(VisitFnDecl);
        public override string VisitTypeDecl(TypeDecl node) => nameof(VisitTypeDecl);
        public override string VisitParamBlockDecl(ParamBlockDecl node) => nameof(VisitParamBlockDecl);
        public override string VisitImportDecl(ImportDecl node) => nameof(VisitImportDecl);
        public override string VisitConstDecl(ConstDecl node) => nameof(VisitConstDecl);
        public override string VisitReadonlyDecl(ReadonlyDecl node) => nameof(VisitReadonlyDecl);
        public override string VisitErrorDecl(ErrorDecl node) => nameof(VisitErrorDecl);
    }

    private static readonly BlockStmt _emptyBlock = new(R, []);
    private static readonly TypeRef _intType = new(R, "Int", [], false);
    private static readonly HookNameVisitor _visitor = new();

    public static IEnumerable<object[]> ExpressionCases => [
        [new IntLiteralExpr(R, 1), nameof(HookNameVisitor.VisitIntLiteral)],
        [new FloatLiteralExpr(R, 1.5), nameof(HookNameVisitor.VisitFloatLiteral)],
        [new StringLiteralExpr(R, "x"), nameof(HookNameVisitor.VisitStringLiteral)],
        [new RawStringLiteralExpr(R, "x"), nameof(HookNameVisitor.VisitRawStringLiteral)],
        [new InterpolatedStringExpr(R, []), nameof(HookNameVisitor.VisitInterpolatedString)],
        [new RegexLiteralExpr(R, "p", ""), nameof(HookNameVisitor.VisitRegexLiteral)],
        [new BoolLiteralExpr(R, true), nameof(HookNameVisitor.VisitBoolLiteral)],
        [new NilLiteralExpr(R), nameof(HookNameVisitor.VisitNilLiteral)],
        [Id("a"), nameof(HookNameVisitor.VisitIdentifier)],
        [new UnaryExpr(R, UnaryOperator.Negate, Int(1)), nameof(HookNameVisitor.VisitUnary)],
        [new BinaryExpr(R, BinaryOperator.Add, Int(1), Int(2)), nameof(HookNameVisitor.VisitBinary)],
        [new GroupingExpr(R, Int(1)), nameof(HookNameVisitor.VisitGrouping)],
        [new TernaryExpr(R, Id("c"), Int(1), Int(2)), nameof(HookNameVisitor.VisitTernary)],
        [new ArrayLiteralExpr(R, []), nameof(HookNameVisitor.VisitArrayLiteral)],
        [new IndexExpr(R, Id("a"), Int(0)), nameof(HookNameVisitor.VisitIndex)],
        [new MemberAccessExpr(R, Id("a"), "m"), nameof(HookNameVisitor.VisitMemberAccess)],
        [new CallExpr(R, Id("f"), []), nameof(HookNameVisitor.VisitCall)],
        [new LambdaExpr(R, [], new LambdaExpressionBody(Int(1))), nameof(HookNameVisitor.VisitLambda)],
        [new NumericRangeExpr(R, Int(1), Int(10), null), nameof(HookNameVisitor.VisitNumericRange)],
        [new ErrorExpr(R, ErrDiag()), nameof(HookNameVisitor.VisitErrorExpr)],
    ];

    public static IEnumerable<object[]> StatementCases => [
          [_emptyBlock, nameof(HookNameVisitor.VisitBlock)],
        [new VarDeclStmt(R, "x", null, Int(1)), nameof(HookNameVisitor.VisitVarDecl)],
        [new AssignmentStmt(R, Id("x"), Int(1)), nameof(HookNameVisitor.VisitAssignment)],
        [new CompoundAssignmentStmt(R, Id("x"), CompoundAssignmentOperator.PlusAssign, Int(1)), nameof(HookNameVisitor.VisitCompoundAssignment)],
        [new IncrementStmt(R, Id("x"), IncrementKind.Increment), nameof(HookNameVisitor.VisitIncrement)],
        [new ExpressionStmt(R, new CallExpr(R, Id("print"), [])), nameof(HookNameVisitor.VisitExpressionStmt)],
        [new IfStmt(R, Id("c"), _emptyBlock, null), nameof(HookNameVisitor.VisitIf)],
        [new WhileStmt(R, Id("c"), _emptyBlock), nameof(HookNameVisitor.VisitWhile)],
        [new ForInStmt(R, ["x"], Id("xs"), _emptyBlock), nameof(HookNameVisitor.VisitForIn)],
        [new SelectStmt(R, Id("s"), [], null), nameof(HookNameVisitor.VisitSelect)],
        [new BreakStmt(R), nameof(HookNameVisitor.VisitBreak)],
        [new ContinueStmt(R), nameof(HookNameVisitor.VisitContinue)],
        [new ReturnStmt(R, null), nameof(HookNameVisitor.VisitReturn)],
        [new TryStmt(R, _emptyBlock, [], null), nameof(HookNameVisitor.VisitTry)],
        [new ErrorStmt(R, ErrDiag()), nameof(HookNameVisitor.VisitErrorStmt)],
    ];

    public static IEnumerable<object[]> DeclarationCases => [
          [new FnDecl(R, "f", [], _intType, _emptyBlock), nameof(HookNameVisitor.VisitFnDecl)],
        [new TypeDecl(R, "T", []), nameof(HookNameVisitor.VisitTypeDecl)],
        [new ParamBlockDecl(R, []), nameof(HookNameVisitor.VisitParamBlockDecl)],
        [new ImportDecl(R, "m", null), nameof(HookNameVisitor.VisitImportDecl)],
        [new ConstDecl(R, "K", null, Int(1)), nameof(HookNameVisitor.VisitConstDecl)],
        [new ReadonlyDecl(R, "K", null, Int(1)), nameof(HookNameVisitor.VisitReadonlyDecl)],
        [new ErrorDecl(R, ErrDiag()), nameof(HookNameVisitor.VisitErrorDecl)],
    ];

    [Theory]
    [MemberData(nameof(ExpressionCases))]
    [MemberData(nameof(StatementCases))]
    [MemberData(nameof(DeclarationCases))]
    public void Accept_RoutesNodeToMatchingVisitHook(AstNode node, string expectedHook) {
        string actual = node.Accept(_visitor);
        Assert.Equal(expectedHook, actual);
    }

    [Fact]
    public void Visit_NullNode_Throws() {
        Assert.Throws<ArgumentNullException>(() => _visitor.Visit(null!));
    }

    [Fact]
    public void DefaultVisit_FromUnoverriddenHook_Throws() {
        ThrowingVisitor v = new();
        Assert.Throws<NotSupportedException>(() => Int(1).Accept(v));
    }

    /// <summary>A visitor that handles only the abstract error hooks; everything else falls through to the default.</summary>
    private sealed class ThrowingVisitor : AstVisitor<int> {
        public override int VisitErrorExpr(ErrorExpr node) => 0;
        public override int VisitErrorStmt(ErrorStmt node) => 0;
        public override int VisitErrorDecl(ErrorDecl node) => 0;
    }
}
