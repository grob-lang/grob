using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser tests for generic type arguments at a call site (D-416, closing D-415's
/// Gap A) — <c>x.mapAs&lt;Employee&gt;()</c> and the free-function form
/// <c>mapAs&lt;Employee&gt;(x)</c>. Covers the happy-path type-argument shapes, the
/// <c>&lt;</c>-as-type-argument-opener-versus-comparison-operator disambiguation (the
/// bounded, non-consuming lookahead <see cref="Parser.LooksLikeTypeArgumentList"/>,
/// mirroring <see cref="Parser.LooksLikeMapLiteral"/>'s D-376 precedent), and error
/// recovery for a malformed type-argument list.
/// </summary>
public sealed class ParserCallTypeArgumentsTests {
    // -----------------------------------------------------------------------
    // Happy path — type arguments at a call site
    // -----------------------------------------------------------------------

    [Fact]
    public void MemberAccessCall_SingleTypeArgument_Parses() {
        Expression e = ExprOf(ParseOk("a.mapAs<Employee>()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        MemberAccessExpr callee = Assert.IsType<MemberAccessExpr>(c.Callee);
        Assert.Equal("mapAs", callee.Member);
        Assert.Equal("a", Assert.IsType<IdentifierExpr>(callee.Target).Name);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        Assert.Equal("Employee", typeArg.Name);
        Assert.Empty(c.Arguments);
    }

    [Fact]
    public void MemberAccessCall_ArrayTypeArgument_Parses() {
        Expression e = ExprOf(ParseOk("a.mapAs<Employee[]>()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        ArrayTypeRef arrayArg = Assert.IsType<ArrayTypeRef>(typeArg);
        Assert.Equal("Employee", arrayArg.ElementType.Name);
    }

    [Fact]
    public void MemberAccessCall_NestedGenericTypeArgument_Parses() {
        // Proves the two-'>' close (Lexer.ScanGreater never emits a shift token, so
        // 'map<string, int>>' lexes as two separate Greater tokens) works identically
        // from this new call-site position, exactly as it already does inside an
        // ordinary type annotation.
        Expression e = ExprOf(ParseOk("a.mapAs<map<string, int>>()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        Assert.Equal("map", typeArg.Name);
        Assert.Equal(2, typeArg.TypeArguments.Count);
        Assert.Equal("string", typeArg.TypeArguments[0].Name);
        Assert.Equal("int", typeArg.TypeArguments[1].Name);
    }

    [Fact]
    public void MemberAccessCall_MultipleTypeArguments_Parse() {
        Expression e = ExprOf(ParseOk("a.convert<A, B>()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal(2, c.TypeArguments.Count);
        Assert.Equal("A", c.TypeArguments[0].Name);
        Assert.Equal("B", c.TypeArguments[1].Name);
    }

    [Fact]
    public void MemberAccessCall_TypeArgumentWithNonEmptyCallArgs_Parses() {
        // The half of Gap A the D-415 isolation missed: a non-empty argument list
        // silently misparsed as a comparison feeding a call before this fix. Now it
        // parses as a single generic call carrying both the type argument and the
        // call argument.
        Expression e = ExprOf(ParseOk("a.mapAs<Employee>(x)\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        Assert.Equal("Employee", typeArg.Name);
        CallArgument arg = Assert.Single(c.Arguments);
        Assert.Equal("x", Assert.IsType<IdentifierExpr>(arg.Value).Name);
    }

    [Fact]
    public void FreeFunctionCall_SingleTypeArgument_Parses() {
        // The other half of Gap A's true scope: the free-function form is affected
        // identically to the member-access form — the determinant is argument-list
        // arity, not receiver shape.
        Expression e = ExprOf(ParseOk("mapAs<Employee>(x)\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal("mapAs", Assert.IsType<IdentifierExpr>(c.Callee).Name);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        Assert.Equal("Employee", typeArg.Name);
        CallArgument arg = Assert.Single(c.Arguments);
        Assert.Equal("x", Assert.IsType<IdentifierExpr>(arg.Value).Name);
    }

    [Fact]
    public void FreeFunctionCall_SingleTypeArgument_EmptyArgs_Parses() {
        Expression e = ExprOf(ParseOk("mapAs<Employee>()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        Assert.Equal("Employee", typeArg.Name);
        Assert.Empty(c.Arguments);
    }

    [Fact]
    public void Call_NoTypeArguments_IsUnchanged() {
        Expression e = ExprOf(ParseOk("f(1, 2)\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Empty(c.TypeArguments);
        Assert.Equal(2, c.Arguments.Count);
    }

    [Fact]
    public void EmptyCall_NoTypeArguments_IsUnchanged() {
        Expression e = ExprOf(ParseOk("f()\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Empty(c.TypeArguments);
        Assert.Empty(c.Arguments);
    }

    // -----------------------------------------------------------------------
    // Disambiguation — the wrongly-decided case, chosen deliberately (D-416)
    // -----------------------------------------------------------------------

    [Fact]
    public void ChainedRelational_ClosesThenCall_ParsesAsGenericCallNotComparison() {
        // 'a < b > (c)' is lexically ambiguous with a one-type-argument generic call
        // to 'a'. D-080 (users cannot declare generics, so every legal generic call is
        // immediately invoked) makes "the run closes, and '(' immediately follows" a
        // safe trigger — D-416 deliberately chooses the generic-call reading here, the
        // one case in the ambiguity space this rule decides against ordinary
        // three-term relational chaining. Rewritable with explicit parens,
        // '(a < b) > (c)', per §7's "parentheses override precedence at any level".
        Expression e = ExprOf(ParseOk("a < b > (c)\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal("a", Assert.IsType<IdentifierExpr>(c.Callee).Name);
        TypeRef typeArg = Assert.Single(c.TypeArguments);
        Assert.Equal("b", typeArg.Name);
        CallArgument arg = Assert.Single(c.Arguments);
        Assert.Equal("c", Assert.IsType<IdentifierExpr>(arg.Value).Name);
    }

    [Fact]
    public void PlainLessComparison_StillParsesAsComparison() {
        Expression e = ExprOf(ParseOk("a < b\n"));
        BinaryExpr cmp = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Less, cmp.Operator);
    }

    [Fact]
    public void PlainGreaterComparison_StillParsesAsComparison() {
        Expression e = ExprOf(ParseOk("a > b\n"));
        BinaryExpr cmp = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Greater, cmp.Operator);
    }

    [Fact]
    public void AssignedComparison_StillParsesAsComparison() {
        CompilationUnit unit = ParseOk("a := 1\nb := 2\nx := a<b\n");
        VarDeclStmt xDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[2]);
        BinaryExpr cmp = Assert.IsType<BinaryExpr>(xDecl.Initializer);
        Assert.Equal(BinaryOperator.Less, cmp.Operator);
    }

    [Fact]
    public void ComparisonThenEquality_StillParsesUnaffected() {
        // Regression pin for the existing ParserExpressionTests.Comparison_Then_
        // Equality_Binding fixture ('a < b == c'): the scan's accepted token set
        // excludes '==', so it bails immediately and this falls straight through —
        // proving the new lookahead cannot misfire on a chain that never reaches a
        // second relational operator at all.
        Expression e = ExprOf(ParseOk("a < b == c\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Equal, outer.Operator);
        BinaryExpr left = Assert.IsType<BinaryExpr>(outer.Left);
        Assert.Equal(BinaryOperator.Less, left.Operator);
    }

    [Fact]
    public void ThreeTermRelationalChain_ClosesOnFirstGreater_StillParsesAsComparisons() {
        // 'a < b > c > (d)' — the scan tracks depth over a single '<'/'>' pair, so it
        // returns at the *first* '>' (depth 1 -> 0); the token immediately following
        // that first '>' is 'c' (an Identifier), not '(', so the scan fails and the
        // whole chain falls through to ParseComparison unchanged: ((a < b) > c) > (d).
        // Pins that the rule does not over-fire on a chain longer than one relational
        // pair just because a '(' appears somewhere later in it.
        Expression e = ExprOf(ParseOk("a < b > c > (d)\n"));
        BinaryExpr outer = Assert.IsType<BinaryExpr>(e);
        Assert.Equal(BinaryOperator.Greater, outer.Operator);
        BinaryExpr middle = Assert.IsType<BinaryExpr>(outer.Left);
        Assert.Equal(BinaryOperator.Greater, middle.Operator);
        BinaryExpr inner = Assert.IsType<BinaryExpr>(middle.Left);
        Assert.Equal(BinaryOperator.Less, inner.Operator);
        Assert.IsType<GroupingExpr>(outer.Right);
    }

    // -----------------------------------------------------------------------
    // Error recovery — malformed type-argument list
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyTypeArgumentList_RecoversWithOneDiagnosticAndContinues() {
        // 'a.mapAs<>()' — the scan sees '<' immediately followed by '>' (closing at
        // depth 0 on the very first token), and that '>' is immediately followed by
        // '(', so the scan succeeds and commits to ParseTypeArgumentList(), which then
        // fails on its own well-formed 'expected type name' diagnostic — a materially
        // better diagnostic than the pre-fix misparse's "unexpected token ')' —
        // expected expression" against the empty call parens, because the parser is
        // now looking at the actual mistake.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("a := 1\nb := a.mapAs<>()\nc := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected type name", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(14, d.Range.Start.Column); // the '>' immediately after '<'

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("c", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void UnclosedTypeArgumentList_FollowedByOpenParen_FallsThroughUnchanged() {
        // 'a.mapAs<Employee(' — the scan's accepted token set does not include '(', so
        // it bails as soon as it meets the stray '(' before any closing '>' is found.
        // The scan therefore never commits, and this input takes exactly the same
        // fallback path it took before this increment: '<' falls to ParseComparison,
        // 'Employee(' is parsed as a call whose ')' is never found anywhere in the
        // file. Confirms the new lookahead adds no new failure mode for a genuinely
        // unclosed run — this is pre-existing, unrelated §29 behaviour (the same
        // shape as ParserParamDeclRecoveryTests'
        // MalformedDefault_LeavesBracketPermanentlyOpen_RecoversAtNextTopLevelKeyword):
        // the never-closed '(' disables the newline anchor (BracketDepth stays > 0)
        // for the rest of the file, so recovery skips past the ordinary statement
        // 'c := 2' — silently, ordinary-statement-level swallow is exactly what §29
        // already documents for this shape and is not new here — landing at the next
        // *top-level keyword* anchor instead. Exactly one diagnostic, no cascade, and
        // the well-formed 'fn' below is not lost.
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("a := 1\nb := a.mapAs<Employee(\nc := 2\nfn good(): int { return 1 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);

        FnDecl tail = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", tail.Name);
    }

    [Fact]
    public void MalformedTypeArgumentList_SubsequentDeclaration_TypeChecksCleanly() {
        // Full-pipeline proof (mirrors ParserMapLiteralTests' equivalent): the
        // statement after a malformed type-argument list still parses and type-checks
        // independently, with no cascaded diagnostic and no under-annotated node.
        const string src = "a := 1\nb := a.mapAs<>()\nc := 2\nd := c + 1\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", parseDiagnostic.Code);

        new TypeChecker(bag).Check(unit);

        Diagnostic onlyDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Same(parseDiagnostic, onlyDiagnostic);

        VarDeclStmt dDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        BinaryExpr sum = Assert.IsType<BinaryExpr>(dDecl.Initializer);
        IdentifierExpr cRef = Assert.IsType<IdentifierExpr>(sum.Left);
        Assert.Equal("c", cRef.Name);
        Assert.NotEqual(GrobType.Error, cRef.ResolvedType);
        Assert.NotNull(cRef.Declaration);
        Assert.NotSame(UnresolvedDecl.Instance, cRef.Declaration);
    }
}
