using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser recovery tests for the braceless <c>param</c> declaration (D-410,
/// replacing <c>ParserParamBlockRecoveryTests.cs</c>'s block-form coverage).
/// D-410's own "net simplification" claim: a braceless <c>param</c> needs no
/// bespoke local recovery wrapper — each declaration is an ordinary top-level
/// item, so §29's existing keyword/'@'-anchored <see cref="Parser.Synchronise"/>
/// (amended by D-415) already covers it via the shared
/// <c>ParseTopLevelItemOrError</c> wrapper. <c>ParseDeclaredParameterOrError</c>
/// and <c>SkipToNextLiteralElementBoundary</c>'s newline mode — D-406's
/// bespoke machinery for the retired block form — are gone from the
/// <c>param</c> path entirely; see <see cref="ParserTypeDeclRecoveryTests"/>
/// for proof the <c>type</c>-body half of that same D-406 machinery is
/// untouched.
/// </summary>
public sealed class ParserParamDeclRecoveryTests {
    [Fact]
    public void MalformedDeclaration_MissingTypeAnnotation_RecoversWithOneDiagnostic() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param bad\nparam y: int\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal("expected ':' after parameter name — the type annotation is mandatory", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);

        Assert.Equal(3, unit.TopLevel.Count);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
        ParamDecl y = Assert.IsType<ParamDecl>(unit.TopLevel[1]);
        Assert.Equal("y", y.Name);
        VarDeclStmt x = Assert.IsType<VarDeclStmt>(unit.TopLevel[2]);
        Assert.Equal("x", x.Name);
    }

    /// <summary>
    /// Load-bearing (D-405/D-406 shape, carried forward): two independently
    /// malformed declarations are both reported — the second mistake is never
    /// swallowed by the first's recovery sweep.
    /// </summary>
    [Fact]
    public void TwoDistinctMalformedDeclarations_ProducesTwoDiagnosticsNoneSwallowed() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param bad1\nparam bad2\nparam y: int\nx := 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E4201", first.Code);
        Assert.Equal(1, first.Range.Start.Line);
        Assert.Equal(11, first.Range.Start.Column);

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E4201", second.Code);
        Assert.Equal(2, second.Range.Start.Line);
        Assert.Equal(11, second.Range.Start.Column);

        Assert.Equal(4, unit.TopLevel.Count);
        ParamDecl y = Assert.IsType<ParamDecl>(unit.TopLevel[2]);
        Assert.Equal("y", y.Name);
        VarDeclStmt x = Assert.IsType<VarDeclStmt>(unit.TopLevel[3]);
        Assert.Equal("x", x.Name);
    }

    [Fact]
    public void MalformedDeclaration_SubsequentDeclaration_TypeChecksCleanly() {
        const string src = "param bad\nfn good(): int { return 1 }\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", parseDiagnostic.Code);
        Assert.Equal(1, parseDiagnostic.Range.Start.Line);
        Assert.Equal(10, parseDiagnostic.Range.Start.Column);

        new TypeChecker(bag).Check(unit);

        Diagnostic onlyDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Same(parseDiagnostic, onlyDiagnostic);

        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[^1]);
        Assert.Equal("good", fn.Name);
    }

    // -----------------------------------------------------------------------
    // A malformed default expression leaving a bracket open (the adversarial
    // case the increment's own test list names explicitly).
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedDefault_ClosesItsOwnBracketPair_RecoversAtNextDeclarationOnly() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param x: int = foo(1 2)\nparam y: int\nfn good(): int { return 2 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(22, d.Range.Start.Column); // the stray '2'

        Assert.Equal(3, unit.TopLevel.Count);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
        ParamDecl y = Assert.IsType<ParamDecl>(unit.TopLevel[1]);
        Assert.Equal("y", y.Name);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[2]);
        Assert.Equal("good", fn.Name);
    }

    /// <summary>
    /// A default expression whose bracket is <b>never</b> closed anywhere in
    /// the file disables the newline anchor permanently (§29's BracketDepth
    /// gate). Recovery still lands correctly at the next 'param' keyword —
    /// baseline correctness for the case D-415's own fix (the next test)
    /// builds on.
    /// </summary>
    [Fact]
    public void MalformedDefault_LeavesBracketPermanentlyOpen_RecoversAtNextTopLevelKeyword() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param x: int = foo(1\nparam y: int\nfn good(): int { return 2 }\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);

        Assert.Equal(3, unit.TopLevel.Count);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
        ParamDecl y = Assert.IsType<ParamDecl>(unit.TopLevel[1]);
        Assert.Equal("y", y.Name);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[2]);
        Assert.Equal("good", fn.Name);
    }

    /// <summary>
    /// The decisive D-415 proof, reproducing the investigation's own empirical
    /// finding against the new grammar. A permanently open bracket (as above)
    /// disables the newline anchor, so the only anchors left before the fix
    /// were '}' and a top-level keyword — <c>param</c> among them, but not
    /// '@'. That let <see cref="Parser.Synchronise"/> skip straight over an
    /// intact decorator stack sitting immediately above the next declaration,
    /// silently discarding it (never reaching <c>SkipParameterDecorators</c>,
    /// which would have caught this decorator's own malformed shape and
    /// raised its own diagnostic). The malformed decorator here — '@(' with
    /// no name — makes the swallow directly observable: fixed, it is caught
    /// and reported as its own root cause (2 diagnostics, 4 top-level items,
    /// including a second <see cref="ErrorDecl"/> for the decorator itself);
    /// broken, it is silently absorbed into the first failure's recovery sweep
    /// with no diagnostic at all (1 diagnostic, 3 top-level items) — see the
    /// mutation-verify note below.
    /// </summary>
    [Fact]
    public void MalformedDefault_DecoratorStackAboveNextDeclaration_IsNotSwallowedByRecovery() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("param x: int = foo(1\n@(\nparam y: string\nfn good(): int { return 2 }\n");

        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic call = bag.Diagnostics[0];
        Assert.Equal("E2001", call.Code);
        Assert.Equal("expected ')' to close call", call.Message);
        Assert.Equal(2, call.Range.Start.Line);
        Assert.Equal(1, call.Range.Start.Column);

        Diagnostic decorator = bag.Diagnostics[1];
        Assert.Equal("E2001", decorator.Code);
        Assert.Equal("expected decorator name after '@'", decorator.Message);
        Assert.Equal(2, decorator.Range.Start.Line);
        Assert.Equal(2, decorator.Range.Start.Column);

        // Two independent root causes, two ErrorDecls — the malformed
        // decorator was not silently merged into the first sweep's range.
        Assert.Equal(4, unit.TopLevel.Count);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
        Assert.IsType<ErrorDecl>(unit.TopLevel[1]);
        ParamDecl y = Assert.IsType<ParamDecl>(unit.TopLevel[2]);
        Assert.Equal("y", y.Name);
        Assert.Equal("string", y.Type.Name);
        FnDecl fn = Assert.IsType<FnDecl>(unit.TopLevel[3]);
        Assert.Equal("good", fn.Name);
    }

    // -----------------------------------------------------------------------
    // EOF safety — malformed input never throws, recovery never loops.
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedDeclaration_AtEndOfFile_ReportsOneDiagnosticAndDoesNotHang() {
        (CompilationUnit unit, DiagnosticBag bag) = Parse("param bad\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
        Assert.NotNull(unit);
        Assert.Single(unit.TopLevel);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);
    }

    // -----------------------------------------------------------------------
    // D-415's '@' anchor is a *top-level `param`* anchor, not a universal one.
    // `SkipParameterDecorators` also serves function parameter lists, so a '@'
    // can legally sit inside a `fn` header. Recovery from a failure earlier in
    // that header must not stop there: stopping hands the top-level loop a '@'
    // it dispatches to `ParseParamDecl`, cascading a second, wholly bogus E4201
    // out of one malformed `fn`. Bracket depth cannot separate the two cases —
    // D-415's own swallow case sits at depth 1 too, on an unclosed bracket left
    // by a failed default. What separates them is which top-level item failed:
    // a decorator stack only ever attaches to a `param`, so '@' is an anchor
    // only while recovering from a `param`/decorator-led item.
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedFnParameterList_NestedDecorator_DoesNotCascadeAParamDiagnostic() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("fn f(a: , @secure b: int): int { return 1 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected type name", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);
        Assert.NotNull(unit);
    }

    /// <summary>
    /// The companion to the above: after recovering from the malformed <c>fn</c>
    /// header, a genuine top-level decorator stack below it still parses. The
    /// narrowed anchor skips the nested '@' without also losing the real one.
    /// </summary>
    [Fact]
    public void MalformedFnParameterList_NestedDecorator_LaterTopLevelDecoratorStackStillParses() {
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("fn f(a: , @secure b: int): int { return 1 }\n@secure\nparam z: int\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);

        ParamDecl z = Assert.IsType<ParamDecl>(unit.TopLevel[^1]);
        Assert.Equal("z", z.Name);
        Assert.Equal("int", z.Type.Name);
    }
}
