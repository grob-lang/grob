using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser-layer tests for the switch expression (§3.1) — AST structure for each
/// pattern form, the lambda-vs-arm-arrow disambiguation, trailing-comma
/// acceptance, postfix binding, and malformed-arm recovery. These isolate the
/// parser from the type checker and compiler.
/// </summary>
public sealed class SwitchExprParserTests {
    [Fact]
    public void ValuePatternSwitch_ParsesToSwitchExprNode() {
        Expression e = ExprOf(ParseOk("code switch { 200 => 1, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.IsType<IdentifierExpr>(sw.Subject);
        Assert.Equal(2, sw.Arms.Count);

        ValuePattern vp = Assert.IsType<ValuePattern>(sw.Arms[0].Pattern);
        Assert.IsType<IntLiteralExpr>(vp.Value);
        Assert.IsType<CatchAllPattern>(sw.Arms[1].Pattern);
    }

    [Fact]
    public void RelationalPattern_ParsesWithOperatorAndOperand() {
        Expression e = ExprOf(ParseOk("n switch { >= 10 => 1, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        RelationalPattern rp = Assert.IsType<RelationalPattern>(sw.Arms[0].Pattern);
        Assert.Equal(BinaryOperator.GreaterEqual, rp.Op);
        Assert.IsType<IntLiteralExpr>(rp.Operand);
    }

    /// <summary>
    /// An identifier pattern followed by <c>=&gt;</c> is the arm arrow, not a lambda
    /// body: <c>x =&gt; 1</c> parses to a value pattern <c>x</c> with result <c>1</c>,
    /// not a <see cref="LambdaExpr"/>. Guards the disambiguation that arm parsing drops
    /// below the lambda precedence level.
    /// </summary>
    [Fact]
    public void IdentifierPatternArrow_IsArmArrowNotLambda() {
        Expression e = ExprOf(ParseOk("n switch { x => 1, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);

        ValuePattern vp = Assert.IsType<ValuePattern>(sw.Arms[0].Pattern);
        IdentifierExpr id = Assert.IsType<IdentifierExpr>(vp.Value);
        Assert.Equal("x", id.Name);
        Assert.IsType<IntLiteralExpr>(sw.Arms[0].Result);
    }

    [Fact]
    public void TrailingCommaAfterFinalArm_IsAccepted() {
        Expression e = ExprOf(ParseOk("n switch { 1 => 10, _ => 0, }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.Equal(2, sw.Arms.Count);
    }

    /// <summary>The subject binds at the postfix level — a member access is the scrutinee.</summary>
    [Fact]
    public void Subject_IsPostfixExpression() {
        Expression e = ExprOf(ParseOk("obj.field switch { 1 => 10, _ => 0 }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.IsType<MemberAccessExpr>(sw.Subject);
    }

    /// <summary>
    /// A malformed arm with no <c>=&gt;</c> produces an E2001 syntax diagnostic with a
    /// source location.
    /// </summary>
    [Fact]
    public void ArmMissingArrow_ProducesE2001() {
        (_, DiagnosticBag bag) = Parse("x := n switch { 1 10 }\n");
        // The root-cause diagnostic points at the token where '=>' was expected.
        Assert.Contains(bag.Errors,
            d => d.Code == "E2001" && d.Range.Start.Line == 1 && d.Range.Start.Column == 19);
    }
}
