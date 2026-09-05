using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// <c>param</c> joins the top-level name space (D-424 Decision 6). Pass 1
/// registered top-level <c>fn</c>, <c>type</c> and value bindings; <c>ParamDecl</c>
/// was absent from that walk and <c>VisitParamDecl</c> registered no symbol,
/// citing Sprint 10.
/// <para>
/// <b>Registration is not binding.</b> Entering the name is what makes E1102 fire
/// on a collision (§19, D-324 extended by D-412) and what makes D-412's
/// coincident-ordering-and-collision cascade expressible. Neither needs a
/// parameter <i>value</i>: supplying and validating one is still Sprint 10
/// (R-15), and <c>VisitParamDecl</c> still contributes no type of its own. The
/// line is between a name and a value, and it was previously drawn around both
/// together.
/// </para>
/// </summary>
public sealed class TypeCheckerParamNameSpaceTests {
    private static DiagnosticBag Check(string source, out CompilationUnit unit) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        Assert.Empty(bag.Diagnostics);
        unit = Parser.Parse(tokens, bag);
        Assert.Empty(bag.Diagnostics);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    private static DiagnosticBag Check(string source) => Check(source, out _);

    // -----------------------------------------------------------------------
    // The name is in the space: a reference resolves rather than raising E1001.
    // -----------------------------------------------------------------------

    [Fact]
    public void ReferenceToAParam_ResolvesToItsDeclaration_NotE1001() {
        DiagnosticBag bag = Check("param token: string\nprint(token)\n", out CompilationUnit unit);

        Assert.Empty(bag.Diagnostics);

        ParamDecl declaration = Assert.IsType<ParamDecl>(unit.TopLevel[0]);
        ExpressionStmt statement = Assert.IsType<ExpressionStmt>(unit.TopLevel[1]);
        CallExpr call = Assert.IsType<CallExpr>(statement.Expression);
        IdentifierExpr reference = Assert.IsType<IdentifierExpr>(Assert.Single(call.Arguments).Value);

        // §3.1.1: the declaration back-reference is the ParamDecl itself, asserted
        // by reference, not merely "not the UnresolvedDecl sentinel" (D-311).
        Assert.Same(declaration, reference.Declaration);
    }

    /// <summary>
    /// A forward reference from a function body works for the same reason every
    /// other top-level name's does: registration happens in pass 1, before any
    /// body is checked.
    /// </summary>
    [Fact]
    public void ParamReferencedFromAFunctionBody_Resolves() {
        DiagnosticBag bag = Check("param token: string\nfn f(): int {\n    print(token)\n    return 1\n}\n");
        Assert.Empty(bag.Diagnostics);
    }

    // -----------------------------------------------------------------------
    // E1102 — the collision fires at the second declaration, whichever kind it
    // is. §19: a correctly placed `param` is always the earlier declaration in a
    // cross-kind collision, so the diagnostic lands on the later form.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("param token: string\nparam token: int\n", 2)]
    [InlineData("param token: string\nconst token := 1\n", 2)]
    [InlineData("param token: string\nreadonly token := 1\n", 2)]
    [InlineData("param token: string\nfn token(): int { return 1 }\n", 2)]
    [InlineData("param token: string\ntype token {\n    a: int\n}\n", 2)]
    [InlineData("param token: string\ntoken := 1\n", 2)]
    public void NameCollidingWithAnEarlierParam_IsE1102AtTheLaterDeclaration(string source, int line) {
        DiagnosticBag bag = Check(source);

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E1102", d.Code);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
    }

    [Fact]
    public void TwoCollidingParams_ReportOnceAtTheSecond() {
        DiagnosticBag bag = Check("param token: string\nparam other: int\nparam token: int\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E1102", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
    }

    [Fact]
    public void DistinctParamNames_ProduceNoCollision() {
        DiagnosticBag bag = Check("param a: string\nparam b: int\nparam c: bool\n");
        Assert.Empty(bag.Diagnostics);
    }

    // -----------------------------------------------------------------------
    // D-412's coincident-ordering-and-collision cascade, both directions.
    //
    // "A misplaced `param` that also collides with an existing name reports ONLY
    // the ordering error (E2202). The `param` is already rejected for its
    // position, so the collision is suppressed rather than reported alongside
    // it: one root cause, one diagnostic, the same principle §29 applies to
    // parser cascades."
    // -----------------------------------------------------------------------

    [Fact]
    public void MisplacedAndColliding_ReportsE2202Alone() {
        DiagnosticBag bag = Check("const token := \"x\"\nparam token: string\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2202", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.DoesNotContain(bag.Diagnostics, x => x.Code == "E1102");
    }

    [Theory]
    [InlineData("fn token(): int { return 1 }\nparam token: string\n")]
    [InlineData("type token {\n    a: int\n}\nparam token: string\n")]
    [InlineData("readonly token := 1\nparam token: string\n")]
    [InlineData("token := 1\nparam token: string\n")]
    public void MisplacedAndColliding_AnyEarlierKind_ReportsE2202Alone(string source) {
        DiagnosticBag bag = Check(source);

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2202", d.Code);
    }

    /// <summary>
    /// The other direction, and the one that proves the suppression is narrow: a
    /// <b>correctly placed</b> colliding <c>param</c> still reports E1102. The
    /// cascade rule keys on the ordering error, not on the declaration being a
    /// <c>param</c>.
    /// </summary>
    [Fact]
    public void CorrectlyPlacedAndColliding_StillReportsE1102() {
        DiagnosticBag bag = Check("param token: string\nparam token: int\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E1102", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.DoesNotContain(bag.Diagnostics, x => x.Code == "E2202");
    }

    /// <summary>
    /// A misplaced <c>param</c> that does <i>not</i> collide reports the ordering
    /// error and nothing else — the suppression is not doing the work here.
    /// </summary>
    [Fact]
    public void MisplacedAndNotColliding_ReportsE2202Alone() {
        DiagnosticBag bag = Check("const other := \"x\"\nparam token: string\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2202", d.Code);
    }
}
