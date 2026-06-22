using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 5 Increment B — default parameter values and the
/// named-argument calling convention (D-113), with the four dedicated call-site
/// diagnostics E0008–E0011 (D-318).
/// </summary>
/// <remarks>
/// Covers default-expression type-checking at the declaration site (E0004), the
/// call-site binding order (positionals, then named, then defaults), the four
/// binding diagnostics, the reuse of E0003/E0004 on the bound argument set, and the
/// §3.1.1 invariant on identifier nodes introduced by a named/default call.
/// </remarks>
public sealed class TypeCheckerNamedArgumentTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private static string ErrorSummary(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

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

    // -----------------------------------------------------------------------
    // Declaration-site default checking — a default that does not match its
    // parameter type is an E0004 on the default expression.
    // -----------------------------------------------------------------------

    [Fact]
    public void Default_WrongType_RaisesE0004() {
        DiagnosticBag bag = Check("""
            fn f(a: int = "x"): int {
            return a
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column); // the "x" default expression
    }

    [Fact]
    public void Default_ReferencingAnotherParameter_RaisesE1001() {
        // Defaults materialise at the call site (D-113), so they are checked in the
        // enclosing scope — a default cannot reference a sibling parameter. Without
        // this, 'a' would resolve to the parameter at check time but compile against
        // caller scope, a silent miscompile.
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = a + 1): int {
            return a + b
            }
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(23, diag.Range.Start.Column); // the 'a' in 'b: int = a + 1'
    }

    [Fact]
    public void Default_ReferencingEnclosingConst_NoError() {
        // A default may reference an identifier visible at the call site (here a
        // top-level const), which resolves cleanly in the enclosing scope.
        DiagnosticBag bag = Check("""
            const BASE := 10
            fn f(a: int, b: int = BASE): int {
            return a + b
            }
            f(1)
            """);
        Assert.False(bag.HasErrors, ErrorSummary(bag));
    }

    [Fact]
    public void Default_CorrectType_NoError() {
        DiagnosticBag bag = Check("""
            fn f(a: int = 99): int {
            return a
            }
            """);
        Assert.False(bag.HasErrors, ErrorSummary(bag));
    }

    // -----------------------------------------------------------------------
    // Defaults bind and override.
    // -----------------------------------------------------------------------

    [Fact]
    public void Default_Omitted_NoError() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1)
            """);
        Assert.False(bag.HasErrors, ErrorSummary(bag));
    }

    [Fact]
    public void Default_OverriddenPositionally_NoError() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1, 20)
            """);
        Assert.False(bag.HasErrors, ErrorSummary(bag));
    }

    [Fact]
    public void Default_OverriddenByName_NoError() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1, b: 20)
            """);
        Assert.False(bag.HasErrors, ErrorSummary(bag));
    }

    // -----------------------------------------------------------------------
    // E0008 — named argument before positional.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamedBeforePositional_RaisesE0008() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(b: 5, 1)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0008.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(9, diag.Range.Start.Column); // the out-of-order positional 1
    }

    // -----------------------------------------------------------------------
    // E0009 — named argument names a required parameter.
    // -----------------------------------------------------------------------

    [Fact]
    public void NamingRequiredParam_RaisesE0009() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(a: 1)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0009.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(3, diag.Range.Start.Column); // the 'a:' named argument
    }

    // -----------------------------------------------------------------------
    // E0010 — duplicate named argument.
    // -----------------------------------------------------------------------

    [Fact]
    public void DuplicateNamed_RaisesE0010() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10, c: int = 20): int {
            return a + b + c
            }
            f(1, b: 2, b: 3)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0010.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(12, diag.Range.Start.Column); // the second 'b:' named argument
    }

    [Fact]
    public void PositionalThenNamed_Duplicate_RaisesE0010() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1, 2, b: 3)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0010.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(9, diag.Range.Start.Column); // 'b:' names a slot already bound positionally
    }

    // -----------------------------------------------------------------------
    // E0011 — unknown parameter name.
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownName_RaisesE0011() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1, x: 99)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0011.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column); // the 'x:' named argument
    }

    // -----------------------------------------------------------------------
    // Two-mode collection — independent binding errors in one call are all
    // reported; the checker does not stop at the first (no E0008 early return).
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleBindingErrors_AllReported() {
        // 'b: 1' (named) then '2' (positional after named → E0008) then
        // 'x: 3' (unknown name → E0011). Both must surface, not just the first.
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(b: 1, 2, x: 3)
            """);
        List<Diagnostic> errors = bag.Errors.ToList();
        Assert.Equal(2, errors.Count);
        Assert.Contains(errors, d => d.Code == ErrorCatalog.E0008.Code);
        Assert.Contains(errors, d => d.Code == ErrorCatalog.E0011.Code);
    }

    // -----------------------------------------------------------------------
    // E0003 / E0004 still fire on the bound argument set.
    // -----------------------------------------------------------------------

    [Fact]
    public void TooMany_RaisesE0003() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1, 2, 3)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MissingRequired_RaisesE0003() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f()
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void TypeMismatch_OnNamed_RaisesE0004() {
        DiagnosticBag bag = Check("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            f(1, b: "x")
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(4, diag.Range.Start.Line);
        Assert.Equal(9, diag.Range.Start.Column); // the "x" value
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — every identifier node introduced by a named/default
    // call carries a non-null ResolvedType and Declaration after type-check.
    // -----------------------------------------------------------------------

    [Fact]
    public void Invariant_NamedDefaultCall_AllIdentifiersAnnotated() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            fn f(a: int, b: int = 10): int {
            return a + b
            }
            x := f(1, b: 20)
            """);
        Assert.False(bag.HasErrors, ErrorSummary(bag));

        IdentifierCollector collector = new();
        collector.Visit(unit);
        foreach (IdentifierExpr id in collector.Identifiers) {
            Assert.NotNull(id.Declaration);
            Assert.NotEqual(GrobType.Error, id.ResolvedType);
        }

        IdentifierExpr callee = collector.Identifiers.First(i => i.Name == "f");
        Assert.IsType<FnDecl>(callee.Declaration);
    }
}
