using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 5 Increment A — positional <c>fn</c> declaration,
/// call-site and return-statement validation.
/// </summary>
/// <remarks>
/// Covers the positional call-site diagnostics (E0003 arity, E0004 argument type),
/// the return-type diagnostic (E0005), the top-level <c>return</c> diagnostic
/// (E2203), forward references resolving through pass-1 registration (D-166), and
/// the §3.1.1 invariant on identifier nodes in a function body and a call.
/// Named arguments and defaults are Increment B and are not exercised here.
/// </remarks>
public sealed class TypeCheckerFunctionTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) {
            Identifiers.Add(node);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static IReadOnlyList<IdentifierExpr> CollectIdentifiers(CompilationUnit unit) {
        IdentifierCollector collector = new();
        collector.Visit(unit);
        return collector.Identifiers;
    }

    // -----------------------------------------------------------------------
    // Well-typed forms — no diagnostics
    // -----------------------------------------------------------------------

    [Fact]
    public void PositionalCall_CorrectArgsAndTypes_TypeChecksClean() {
        DiagnosticBag bag = Check("""
            fn add(a: int, b: int): int {
            return a + b
            }
            x := add(1, 2)
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void ForwardReference_ToFunctionDeclaredLater_Resolves() {
        // 'a' calls 'b', which is declared after it — pass-1 registration (D-166).
        DiagnosticBag bag = Check("""
            fn a(): int {
            return b()
            }
            fn b(): int {
            return 1
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // E0003 — wrong number of arguments
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_TooFewArguments_RaisesE0003() {
        DiagnosticBag bag = Check("""
            fn add(a: int, b: int): int {
            return a + b
            }
            add(1)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void Call_TooManyArguments_RaisesE0003() {
        DiagnosticBag bag = Check("""
            fn add(a: int, b: int): int {
            return a + b
            }
            add(1, 2, 3)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E0004 — argument type mismatch
    // -----------------------------------------------------------------------

    [Fact]
    public void Call_ArgumentTypeMismatch_RaisesE0004() {
        DiagnosticBag bag = Check("""
            fn f(a: int): int {
            return a
            }
            f("x")
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(3, diag.Range.Start.Column); // the "x" argument
    }

    [Fact]
    public void Call_IntArgumentToFloatParameter_Widens_NoError() {
        // int → float is the one implicit widening (D-178); not an E0004.
        DiagnosticBag bag = Check("""
            fn f(a: float): float {
            return a
            }
            f(1)
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // E0005 — return type mismatch
    // -----------------------------------------------------------------------

    [Fact]
    public void Return_TypeMismatch_RaisesE0005() {
        DiagnosticBag bag = Check("""
            fn f(): int {
            return "x"
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0005.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(8, diag.Range.Start.Column); // the "x" return value
    }

    [Fact]
    public void Return_AssignableType_NoError() {
        DiagnosticBag bag = Check("""
            fn f(): float {
            return 1
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Return_BareInNonNullableFunction_RaisesE0005() {
        // 'void' is not a user-declarable return type, so a bare return (yielding
        // nil) does not satisfy a non-nullable declared type.
        DiagnosticBag bag = Check("""
            fn f(): int {
            return
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0005.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column); // the bare return statement
    }

    [Fact]
    public void Return_BareInNullableFunction_NoError() {
        // A nullable return type accepts the nil that a bare return yields.
        DiagnosticBag bag = Check("""
            fn f(): int? {
            return
            }
            """);
        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // E2203 — top-level return
    // -----------------------------------------------------------------------

    [Fact]
    public void Return_AtScriptLevel_RaisesE2203() {
        DiagnosticBag bag = Check("return 5\n");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E2203.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Two-mode errors — the checker never stops at the first diagnostic.
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleCallErrors_AllReported() {
        DiagnosticBag bag = Check("""
            fn f(a: int): int {
            return a
            }
            f()
            f(1, 2)
            f("x")
            """);
        List<Diagnostic> errors = bag.Errors.ToList();
        Assert.Equal(3, errors.Count);
        Assert.Equal(ErrorCatalog.E0003.Code, errors[0].Code);
        Assert.Equal(ErrorCatalog.E0003.Code, errors[1].Code);
        Assert.Equal(ErrorCatalog.E0004.Code, errors[2].Code);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — identifier nodes carry non-null ResolvedType and
    // Declaration after type-check; error paths use the sentinels by reference.
    // -----------------------------------------------------------------------

    [Fact]
    public void Invariant_FunctionBodyAndCall_AllIdentifiersAnnotated() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn add(a: int, b: int): int {
            return a + b
            }
            x := add(1, 2)
            """);
        Assert.False(bag.HasErrors);

        foreach (IdentifierExpr id in CollectIdentifiers(unit)) {
            Assert.NotNull(id.Declaration);
        }

        // Parameter references resolve to int; the callee resolves to the FnDecl.
        IdentifierExpr a = CollectIdentifiers(unit).First(i => i.Name == "a");
        Assert.Equal(GrobType.Int, a.ResolvedType);
        IdentifierExpr callee = CollectIdentifiers(unit).First(i => i.Name == "add");
        Assert.IsType<FnDecl>(callee.Declaration);
    }

    [Fact]
    public void Invariant_UndefinedCallee_UsesSentinelsByReference() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("missing()\n");
        Assert.True(bag.HasErrors);

        IdentifierExpr callee = CollectIdentifiers(unit).First(i => i.Name == "missing");
        Assert.Equal(GrobType.Error, callee.ResolvedType);
        Assert.Same(UnresolvedDecl.Instance, callee.Declaration);
    }

    // -----------------------------------------------------------------------
    // Layer invariant — pathological but parseable calls type-check to a result
    // or a diagnostic, never throw.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("f()\n")]
    [InlineData("f(f())\n")]
    [InlineData("fn f(): int {\nreturn f()\n}\nf(1)\n")]
    [InlineData("fn f(a: int): int {\nreturn a\n}\nf(f(f(1)))\n")]
    public void PathologicalCall_DoesNotThrow(string source) {
        Exception? ex = Record.Exception(() => Check(source));
        Assert.Null(ex);
    }
}
