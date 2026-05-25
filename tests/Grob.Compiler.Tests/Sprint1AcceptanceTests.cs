using System.IO;

using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Sprint 1 acceptance tests. Each test corresponds to a named acceptance
/// criterion from <c>docs/design/grob-v1-requirements.md</c> §4 (Sprint 1)
/// or to the canonical worked example in
/// <c>docs/design/grob-language-fundamentals.md</c> §29.6.
///
/// Fixtures live in <c>tests/fixtures/sprint-1/</c> and are copied alongside
/// the test binary at build time so each test can resolve them from
/// <see cref="AppContext.BaseDirectory"/>.
///
/// These tests intentionally read the fixtures verbatim — the assertions
/// describe the parser contract, the fixtures describe the failure shape.
/// </summary>
public class Sprint1AcceptanceTests {
    private static string FixturePath(string name) {
        if (Path.IsPathRooted(name))
            throw new ArgumentException($"Fixture name must be a relative path, got: {name}", nameof(name));
        return Path.Combine(AppContext.BaseDirectory, "fixtures", "sprint-1", name);
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(FixturePath(name));

    /// <summary>
    /// Sprint 1 acceptance — malformed expression mid-function. A function
    /// body contains a broken expression (missing RHS of <c>+</c>); the
    /// surrounding functions parse cleanly. The parser must:
    ///   * emit exactly one diagnostic for this failure,
    ///   * place an <see cref="ErrorExpr"/> at the failure site, inside the
    ///     containing <see cref="ReturnStmt"/>, with a non-empty source range,
    ///   * still build complete AST nodes for the surrounding declarations.
    /// </summary>
    [Fact]
    public void MalformedExpressionMidFunction_KeepsSurroundingDeclarationsIntact() {
        string source = ReadFixture("malformed-expression-mid-function.grob");

        (CompilationUnit unit, DiagnosticBag bag) = Parse(source);

        Assert.Equal(1, bag.Count);
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(13, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);

        // Three top-level fn declarations, none of them ErrorDecls.
        Assert.Equal(3, unit.TopLevel.Count);
        Assert.All(unit.TopLevel, n => Assert.IsNotType<ErrorDecl>(n));
        FnDecl first = Assert.IsType<FnDecl>(unit.TopLevel[0]);
        FnDecl broken = Assert.IsType<FnDecl>(unit.TopLevel[1]);
        FnDecl last = Assert.IsType<FnDecl>(unit.TopLevel[2]);
        Assert.Equal("first", first.Name);
        Assert.Equal("broken", broken.Name);
        Assert.Equal("last", last.Name);

        // The failure shape: the broken function body has one statement,
        // a ReturnStmt whose value is the ErrorExpr placeholder. The
        // placeholder's diagnostic must be the one in the bag.
        Statement only = Assert.Single(broken.Body.Statements);
        ReturnStmt ret = Assert.IsType<ReturnStmt>(only);
        ErrorExpr placeholder = Assert.IsType<ErrorExpr>(ret.Value);
        Assert.Same(d, placeholder.Diagnostic);

        // Range must point at real source, not a zero-width sentinel.
        SourceRange r = placeholder.Range;
        Assert.True(r.Start.Line >= 1 && r.Start.Column >= 1,
            $"ErrorExpr range must point at real source; got {r}.");
    }

    /// <summary>
    /// Sprint 1 acceptance — malformed top-level declaration. One <c>fn</c>
    /// keyword has no following identifier; the surrounding declarations
    /// must parse cleanly. The broken declaration becomes an
    /// <see cref="ErrorDecl"/>; exactly one diagnostic is emitted.
    /// </summary>
    [Fact]
    public void MalformedDeclaration_LeavesSubsequentDeclarationsIntact() {
        string source = ReadFixture("malformed-declaration.grob");

        (CompilationUnit unit, DiagnosticBag bag) = Parse(source);

        Assert.Equal(1, bag.Count);
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(9, d.Range.Start.Line);
        Assert.Equal(3, d.Range.Start.Column);

        Assert.Equal(3, unit.TopLevel.Count);
        FnDecl firstFn = Assert.IsType<FnDecl>(unit.TopLevel[0]);
        ErrorDecl broken = Assert.IsType<ErrorDecl>(unit.TopLevel[1]);
        FnDecl lastFn = Assert.IsType<FnDecl>(unit.TopLevel[2]);
        Assert.Equal("first", firstFn.Name);
        Assert.Equal("last", lastFn.Name);
        Assert.Same(d, broken.Diagnostic);

        // The `fn last(...)` body must contain the unchanged return.
        Statement only = Assert.Single(lastFn.Body.Statements);
        ReturnStmt ret = Assert.IsType<ReturnStmt>(only);
        IntLiteralExpr lit = Assert.IsType<IntLiteralExpr>(ret.Value);
        Assert.Equal(99L, lit.Value);
    }

    /// <summary>
    /// Sprint 1 acceptance — multi-error file with no parser cascade. Three
    /// independent root-cause failures interleaved with clean declarations.
    /// The parser must:
    ///   * emit exactly three diagnostics (one per root cause),
    ///   * preserve every well-formed declaration between the errors,
    ///   * not amplify a single failure into multiple diagnostics.
    /// </summary>
    [Fact]
    public void MultiErrorFile_ProducesOneDiagnosticPerRootCause_NoCascade() {
        string source = ReadFixture("multi-error-no-cascade.grob");

        (CompilationUnit unit, DiagnosticBag bag) = Parse(source);

        Assert.Equal(3, bag.Count);
        Assert.All(bag.Diagnostics, diag => {
            Assert.Equal("E2001", diag.Code);
            Assert.Equal(Severity.Error, diag.Severity);
        });

        // Every well-formed declaration is present.
        string[] fnNames = unit.TopLevel.OfType<FnDecl>().Select(f => f.Name).ToArray();
        Assert.Equal(new[] { "first", "broken_expr", "second", "third", "fourth" }, fnNames);

        // Two of the failures became ErrorDecls (the `const := 5`, the @@@).
        // The `broken_expr` function's header parsed fine — its failure lives
        // inside the body as an ErrorExpr, not as an ErrorDecl.
        Assert.Equal(2, unit.TopLevel.OfType<ErrorDecl>().Count());
        // The broken_expr body still holds a single ReturnStmt whose value is ErrorExpr.
        FnDecl brokenExpr = unit.TopLevel.OfType<FnDecl>().Single(f => f.Name == "broken_expr");
        ReturnStmt ret = Assert.IsType<ReturnStmt>(Assert.Single(brokenExpr.Body.Statements));
        Assert.IsType<ErrorExpr>(ret.Value);

        // Diagnostics in source order: broken expression, const without name, garbage tokens.
        Assert.Equal((11, 1), (bag.Diagnostics[0].Range.Start.Line, bag.Diagnostics[0].Range.Start.Column));
        Assert.Equal((15, 7), (bag.Diagnostics[1].Range.Start.Line, bag.Diagnostics[1].Range.Start.Column));
        Assert.Equal((19, 1), (bag.Diagnostics[2].Range.Start.Line, bag.Diagnostics[2].Range.Start.Column));
    }

    /// <summary>
    /// The §29.6 worked example, loaded verbatim from
    /// <c>tests/fixtures/sprint-1/section-29-6-worked-example.grob</c>.
    ///
    /// In Sprint 1 the parser alone runs, so only the first of the two
    /// diagnostics the spec narrates lands — the second (an undefined
    /// identifier on the <c>nonexistent</c> reference) is produced by the
    /// type checker, which arrives in Sprint 2. The assertion here is
    /// scoped to what Sprint 1 delivers: one parser-level diagnostic at the
    /// failure site, the rest of the file parses cleanly, no cascade.
    /// </summary>
    [Fact]
    public void Section29_6_WorkedExample_ProducesOneParserDiagnostic() {
        string source = ReadFixture("section-29-6-worked-example.grob");

        (CompilationUnit unit, DiagnosticBag bag) = Parse(source);

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(Severity.Error, d.Severity);
        Assert.Equal(11, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);

        // Three top-level items: the fn, the x := ..., the y := ... .
        Assert.Equal(3, unit.TopLevel.Count);

        FnDecl add = Assert.IsType<FnDecl>(unit.TopLevel[0]);
        Assert.Equal("add", add.Name);
        ReturnStmt ret = Assert.IsType<ReturnStmt>(Assert.Single(add.Body.Statements));
        ErrorExpr placeholder = Assert.IsType<ErrorExpr>(ret.Value);
        Assert.Same(d, placeholder.Diagnostic);

        VarDeclStmt x = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        Assert.Equal("x", x.Name);
        Assert.IsType<CallExpr>(x.Initializer);

        VarDeclStmt y = Assert.IsType<VarDeclStmt>(unit.TopLevel[2]);
        Assert.Equal("y", y.Name);
        Assert.IsType<BinaryExpr>(y.Initializer);
    }
}
