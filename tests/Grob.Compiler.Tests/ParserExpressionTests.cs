using Grob.Compiler.Ast;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

public class ParserExpressionTests {
    [Fact]
    public void IntLiteral_Parses() {
        Expression e = ExprOf(ParseOk("123\n"));
        Assert.Equal(123L, Assert.IsType<IntLiteralExpr>(e).Value);
    }

    [Fact]
    public void HexLiteral_Parses() {
        Expression e = ExprOf(ParseOk("0xFF\n"));
        Assert.Equal(255L, Assert.IsType<IntLiteralExpr>(e).Value);
    }

    [Fact]
    public void FloatLiteral_Parses() {
        Expression e = ExprOf(ParseOk("3.14\n"));
        Assert.Equal(3.14, Assert.IsType<FloatLiteralExpr>(e).Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void BoolLiteral_Parses(string src, bool expected) {
        Expression e = ExprOf(ParseOk(src + "\n"));
        Assert.Equal(expected, Assert.IsType<BoolLiteralExpr>(e).Value);
    }

    [Fact]
    public void NilLiteral_Parses() {
        Expression e = ExprOf(ParseOk("nil\n"));
        Assert.IsType<NilLiteralExpr>(e);
    }

    [Fact]
    public void Identifier_Parses() {
        Expression e = ExprOf(ParseOk("foo\n"));
        Assert.Equal("foo", Assert.IsType<IdentifierExpr>(e).Name);
    }

    [Fact]
    public void Grouping_Parses() {
        Expression e = ExprOf(ParseOk("(1)\n"));
        Assert.IsType<IntLiteralExpr>(Assert.IsType<GroupingExpr>(e).Inner);
    }

    [Fact]
    public void Additive_LeftAssociative() {
        // 1 + 2 + 3  =>  (1 + 2) + 3
        Expression e = ExprOf(ParseOk("1 + 2 + 3\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Add, outer.Operator);
        BinaryExpr inner = Assert.IsType<BinaryExpr>(outer.Left);
        Assert.Equal(BinaryOperator.Add, inner.Operator);
        Assert.Equal(3L, Assert.IsType<IntLiteralExpr>(outer.Right).Value);
    }

    [Fact]
    public void MultiplicativeBindsTighterThanAdditive() {
        // 1 + 2 * 3  =>  1 + (2 * 3)
        Expression e = ExprOf(ParseOk("1 + 2 * 3\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Add, outer.Operator);
        BinaryExpr right = Assert.IsType<BinaryExpr>(outer.Right);
        Assert.Equal(BinaryOperator.Multiply, right.Operator);
    }

    [Fact]
    public void Unary_Not_Stacks() {
        // `--` is tokenised as the decrement operator, not two unary minuses,
        // so we exercise stacked unary through `!` instead.
        Expression e = ExprOf(ParseOk("!!true\n"));
        UnaryExpr u1 = Assert.IsType<UnaryExpr>(e);
        Assert.Equal(UnaryOperator.Not, u1.Operator);
        UnaryExpr u2 = Assert.IsType<UnaryExpr>(u1.Operand);
        Assert.Equal(UnaryOperator.Not, u2.Operator);
    }

    [Fact]
    public void Comparison_Then_Equality_Binding() {
        // a < b == c  =>  (a < b) == c
        Expression e = ExprOf(ParseOk("a < b == c\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Equal, outer.Operator);
        BinaryExpr left = Assert.IsType<BinaryExpr>(outer.Left);
        Assert.Equal(BinaryOperator.Less, left.Operator);
    }

    [Fact]
    public void LogicalAnd_TighterThanOr() {
        // a || b && c  =>  a || (b && c)
        Expression e = ExprOf(ParseOk("a || b && c\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Or, outer.Operator);
        BinaryExpr right = Assert.IsType<BinaryExpr>(outer.Right);
        Assert.Equal(BinaryOperator.And, right.Operator);
    }

    [Fact]
    public void NilCoalesce_LowerThanLogicalOr() {
        // a || b ?? c  =>  (a || b) ?? c
        Expression e = ExprOf(ParseOk("a || b ?? c\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.NilCoalesce, outer.Operator);
        BinaryExpr left = Assert.IsType<BinaryExpr>(outer.Left);
        Assert.Equal(BinaryOperator.Or, left.Operator);
    }

    [Fact]
    public void Ternary_BindsLowerThanNilCoalesce() {
        // a ?? b ? c : d  =>  (a ?? b) ? c : d
        Expression e = ExprOf(ParseOk("a ?? b ? c : d\n"));
        TernaryExpr t = Assert.IsType<TernaryExpr>(e);
        BinaryExpr cond = Assert.IsType<BinaryExpr>(t.Condition);
        Assert.Equal(BinaryOperator.NilCoalesce, cond.Operator);
    }

    [Fact]
    public void Call_Empty_Args() {
        Expression e = ExprOf(ParseOk("f()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.IsType<IdentifierExpr>(c.Callee);
        Assert.Empty(c.Arguments);
    }

    [Fact]
    public void Call_With_PositionalArgs() {
        Expression e = ExprOf(ParseOk("add(1, 2)\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal(2, c.Arguments.Count);
        Assert.All(c.Arguments, a => Assert.Null(a.Name));
    }

    [Fact]
    public void Call_With_NamedArg() {
        Expression e = ExprOf(ParseOk("greet(name: \"sam\")\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        CallArgument arg = Assert.Single(c.Arguments);
        Assert.Equal("name", arg.Name);
    }

    [Fact]
    public void MemberAccess_Chains() {
        Expression e = ExprOf(ParseOk("a.b.c\n"));
        MemberAccessExpr outer = Assert.IsType<MemberAccessExpr>(e);
        Assert.Equal("c", outer.Member);
        MemberAccessExpr inner = Assert.IsType<MemberAccessExpr>(outer.Target);
        Assert.Equal("b", inner.Member);
    }

    [Fact]
    public void Index_Then_Call() {
        Expression e = ExprOf(ParseOk("xs[0]()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        IndexExpr ix = Assert.IsType<IndexExpr>(c.Callee);
        Assert.IsType<IdentifierExpr>(ix.Target);
    }

    [Fact]
    public void ArrayLiteral_Empty() {
        Expression e = ExprOf(ParseOk("[]\n"));
        Assert.Empty(Assert.IsType<ArrayLiteralExpr>(e).Elements);
    }

    [Fact]
    public void ArrayLiteral_WithElements() {
        Expression e = ExprOf(ParseOk("[1, 2, 3]\n"));
        Assert.Equal(3, Assert.IsType<ArrayLiteralExpr>(e).Elements.Count);
    }

    [Fact]
    public void Lambda_BareIdentifier_To_Expression() {
        Expression e = ExprOf(ParseOk("x => x + 1\n"));
        LambdaExpr l = Assert.IsType<LambdaExpr>(e);
        Parameter p = Assert.Single(l.Parameters);
        Assert.Equal("x", p.Name);
        Assert.IsType<LambdaExpressionBody>(l.Body);
    }

    [Fact]
    public void Lambda_Parens_To_Block() {
        Expression e = ExprOf(ParseOk("(a, b) => { return a + b }\n"));
        LambdaExpr l = Assert.IsType<LambdaExpr>(e);
        Assert.Equal(2, l.Parameters.Count);
        Assert.IsType<LambdaBlockBody>(l.Body);
    }

    [Fact]
    public void RawString_Parses() {
        Expression e = ExprOf(ParseOk("`hello`\n"));
        Assert.Equal("hello", Assert.IsType<RawStringLiteralExpr>(e).Value);
    }

    [Fact]
    public void InterpolatedString_Parses() {
        Expression e = ExprOf(ParseOk("\"hi ${name}\"\n"));
        InterpolatedStringExpr s = Assert.IsType<InterpolatedStringExpr>(e);
        // hi-text + ${name} interpolation segment.
        Assert.Equal(2, s.Parts.Count);
        Assert.IsType<StringTextPart>(s.Parts[0]);
        StringExpressionPart sx = Assert.IsType<StringExpressionPart>(s.Parts[1]);
        Assert.Equal("name", Assert.IsType<IdentifierExpr>(sx.Expression).Name);
    }
}
