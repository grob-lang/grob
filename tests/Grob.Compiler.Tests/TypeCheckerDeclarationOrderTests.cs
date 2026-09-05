using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// §19's declaration-order rules, given their first throw sites by D-424
/// Decision 5. The rules have been normative since April 2026 and nothing
/// enforced them: an <c>import</c> after a <c>param</c>, or a <c>param</c> after
/// a <c>fn</c>, compiled clean.
/// <para>
/// The check is a linear walk over <c>CompilationUnit.TopLevel</c>, which is
/// already a flat ordered list, classifying each item into §19's categories —
/// <c>import</c> (1), <c>param</c> (2), <c>type</c> and <c>fn</c> (3, unordered
/// relative to each other), everything else including <c>const</c>,
/// <c>readonly</c> and statements (5) — and reporting a backward step.
/// </para>
/// <para>
/// Two constraints from D-424 Decision 5 are pinned here: diagnostics come out
/// in source order, and an ordering error never suppresses the rest of the check
/// (D-039's two-mode rule — a misordered file still reports every other error it
/// contains).
/// </para>
/// </summary>
public sealed class TypeCheckerDeclarationOrderTests {
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
    // E2201 — `import` after any later category (§19: "import statements must
    // appear before any other declarations or code").
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("param p: int\nimport io\n", 2, "the 'param' declaration on line 1")]
    [InlineData("type T {\n    a: int\n}\nimport io\n", 4, "the 'type' declaration on line 1")]
    [InlineData("fn f(): int { return 1 }\nimport io\n", 2, "the 'fn' declaration on line 1")]
    [InlineData("const K := 1\nimport io\n", 2, "the 'const' declaration on line 1")]
    [InlineData("readonly R := 1\nimport io\n", 2, "the 'readonly' declaration on line 1")]
    [InlineData("x := 1\nimport io\n", 2, "the top-level statement on line 1")]
    public void ImportAfterAnyLaterCategory_IsE2201(string source, int line, string after) {
        DiagnosticBag bag = Check(source);

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2201", d.Code);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
        // The message names what the declaration came after, and where.
        Assert.Contains(after, d.Message, StringComparison.Ordinal);
    }

    // -----------------------------------------------------------------------
    // E2202 — `param` after `type`, `fn`, `const`, `readonly` or a top-level
    // statement. `const` and `readonly` are top-level code (category 5, D-412).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("type T {\n    a: int\n}\nparam p: int\n", 4, "the 'type' declaration on line 1")]
    [InlineData("fn f(): int { return 1 }\nparam p: int\n", 2, "the 'fn' declaration on line 1")]
    [InlineData("const K := 1\nparam p: int\n", 2, "the 'const' declaration on line 1")]
    [InlineData("readonly R := 1\nparam p: int\n", 2, "the 'readonly' declaration on line 1")]
    [InlineData("x := 1\nparam p: int\n", 2, "the top-level statement on line 1")]
    public void ParamAfterAnyLaterCategory_IsE2202(string source, int line, string after) {
        DiagnosticBag bag = Check(source);

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2202", d.Code);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
        Assert.Contains(after, d.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The declaration's range now covers its decorator stack (D-424 Decision
    /// 1), so a misordered decorated `param` reports at the first `@`, not at the
    /// `param` keyword below it.
    /// </summary>
    [Fact]
    public void MisorderedDecoratedParam_ReportsAtTheDecoratorStack() {
        DiagnosticBag bag = Check("x := 1\n@secure\nparam token: string\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2202", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Legal orderings stay clean.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("import io\nimport std.io as Io\n")]
    [InlineData("import io\nparam p: int\n")]
    [InlineData("param a: int\nparam b: int\n")]
    [InlineData("param a: int\ntype T {\n    f: int\n}\n")]
    [InlineData("param a: int\nfn f(): int { return 1 }\n")]
    [InlineData("param a: int\nconst K := 1\n")]
    [InlineData("param a: int\nreadonly R := 1\n")]
    [InlineData("param a: int\nx := 1\n")]
    // §19: `type` and `fn` may appear in any order relative to each other.
    [InlineData("type T {\n    f: int\n}\nfn f(): int { return 1 }\n")]
    [InlineData("fn f(): int { return 1 }\ntype T {\n    f: int\n}\n")]
    // The canonical §19 shape, end to end.
    [InlineData("import io\n\n@secure\nparam token: string\n\nparam days: int = 30\n\n"
        + "type Repo {\n    name: string\n}\n\nfn helper(): int { return 1 }\n\nconst K := 1\nx := 1\n")]
    public void CorrectlyOrderedFile_ProducesNoOrderingDiagnostic(string source) {
        DiagnosticBag bag = Check(source);

        Assert.DoesNotContain(bag.Diagnostics, d => d.Code is "E2201" or "E2202");
    }

    /// <summary>
    /// A blank line or a comment inside or around the parameter group is not a
    /// significant line (§19) and never ends it — including the blank line
    /// <c>grob fmt</c> requires around a decorated declaration (D-413). Neither
    /// reaches the AST at all, so the walk cannot see them; asserted so that a
    /// future change which does put them in the tree fails here.
    /// </summary>
    [Fact]
    public void BlankAndCommentLinesDoNotEndTheParameterGroup() {
        DiagnosticBag bag = Check(
            "param a: int\n\n// a comment\n\n@secure\nparam b: string\n\nparam c: int\n");

        Assert.DoesNotContain(bag.Diagnostics, d => d.Code is "E2201" or "E2202");
    }

    // -----------------------------------------------------------------------
    // D-039's two-mode rule (D-424 Decision 5's second constraint).
    // -----------------------------------------------------------------------

    /// <summary>
    /// A misordered file still reports every other error it contains, and the
    /// diagnostics come out in source order — which is why the walk is folded
    /// into pass 2's source-order visit rather than run as a pre-pass. A pre-pass
    /// would emit the ordering error ahead of the type error on line 1.
    /// </summary>
    [Fact]
    public void MisorderedParamPlusThreeTypeErrors_ReportsFourDiagnosticsInSourceOrder() {
        DiagnosticBag bag = Check(
            "a := nope1\n" +
            "param p: int\n" +
            "b := nope2\n" +
            "c := nope3\n");

        Assert.Equal(4, bag.Diagnostics.Count);
        Assert.Equal(["E1001", "E2202", "E1001", "E1001"],
            bag.Diagnostics.Select(d => d.Code));
        Assert.Equal([1, 2, 3, 4], bag.Diagnostics.Select(d => d.Range.Start.Line));
    }

    [Fact]
    public void SeveralOrderingErrors_AreAllReported_InSourceOrder() {
        DiagnosticBag bag = Check(
            "fn f(): int { return 1 }\n" +
            "param p: int\n" +
            "import io\n" +
            "param q: int\n");

        Assert.Equal(["E2202", "E2201", "E2202"], bag.Diagnostics.Select(d => d.Code));
        Assert.Equal([2, 3, 4], bag.Diagnostics.Select(d => d.Range.Start.Line));
    }

    // -----------------------------------------------------------------------
    // §29: a parser error placeholder is category-neutral. It neither trips the
    // check nor advances the high-water mark, so a recovered parse error never
    // cascades an ordering error onto the declaration below it.
    // -----------------------------------------------------------------------

    [Fact]
    public void ErrorDeclBetweenTwoParams_DoesNotCascadeAnOrderingError() {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan("param bad\nparam y: int\n", bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);

        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E4201", parseDiagnostic.Code);
        Assert.IsType<ErrorDecl>(unit.TopLevel[0]);

        new TypeChecker(bag).Check(unit);

        Assert.Same(parseDiagnostic, Assert.Single(bag.Diagnostics));
    }

    [Fact]
    public void EmptyFile_ProducesNoOrderingDiagnostic() {
        DiagnosticBag bag = Check("");
        Assert.Empty(bag.Diagnostics);
    }
}
