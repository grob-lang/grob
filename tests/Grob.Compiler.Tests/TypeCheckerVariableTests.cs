using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 3 Increment A: variables, assignment,
/// compound assignment, increment/decrement, and block scoping.
/// </summary>
public sealed class TypeCheckerVariableTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
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
        var collector = new IdentifierCollector();
        collector.Visit(unit);
        return collector.Identifiers;
    }

    // -----------------------------------------------------------------------
    // Basic declaration and use
    // -----------------------------------------------------------------------

    [Fact]
    public void VarDecl_IntLiteral_NoErrors() {
        DiagnosticBag bag = Check("x := 5");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void VarDecl_FloatLiteral_NoErrors() {
        DiagnosticBag bag = Check("y := 3.14");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void VarDecl_StringLiteral_NoErrors() {
        DiagnosticBag bag = Check("""s := "hello" """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void VarDecl_TypeAnnotation_MatchingType_NoErrors() {
        DiagnosticBag bag = Check("x: int := 42");
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void VarDecl_TypeAnnotation_MismatchedType_EmitsE0001() {
        DiagnosticBag bag = Check("""x: int := "hello" """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal((1, 11), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // § 3.1.1 invariant — every IdentifierExpr gets ResolvedType + Declaration
    // -----------------------------------------------------------------------

    [Fact]
    public void IdentifierInVarDecl_Initializer_HasResolvedTypeAndDeclaration() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            x := 5
            y := x
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        IReadOnlyList<IdentifierExpr> ids = CollectIdentifiers(unit);
        foreach (IdentifierExpr id in ids) {
            Assert.True(id.ResolvedType != GrobType.Unknown,
                $"Identifier '{id.Name}' has ResolvedType=Unknown");
            Assert.NotNull(id.Declaration);
        }
    }

    [Fact]
    public void IdentifierInAssignment_HasResolvedTypeAndDeclaration() {
        (CompilationUnit unit, DiagnosticBag bag) = TypeCheckSource("""
            x := 5
            x = 10
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
        IReadOnlyList<IdentifierExpr> ids = CollectIdentifiers(unit);
        foreach (IdentifierExpr id in ids.Where(id => id.Name == "x")) {
            Assert.NotEqual(GrobType.Unknown, id.ResolvedType);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // Assignment (=)
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_UndeclaredVariable_EmitsE1001() {
        DiagnosticBag bag = Check("x = 5");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void Assignment_TypeMismatch_EmitsE0001() {
        DiagnosticBag bag = Check("""
            x := 5
            x = "hello"
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0001", diag.Code);
        Assert.Equal((2, 5), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void Assignment_SameType_NoErrors() {
        DiagnosticBag bag = Check("""
            x := 5
            x = 99
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Same-scope redeclaration — E1102
    // -----------------------------------------------------------------------

    [Fact]
    public void VarDecl_SameScopeRedeclaration_EmitsE1102() {
        DiagnosticBag bag = Check("""
            x := 5
            x := 10
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1102", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void VarDecl_DifferentScopes_NoError() {
        // A re-declaration in an inner scope is allowed (shadowing).
        DiagnosticBag bag = Check("""
            x := 1
            { x := 2 }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Compound assignment (+=, -=, *=, /=, %=)
    // -----------------------------------------------------------------------

    [Fact]
    public void CompoundAssignment_IntPlusInt_NoErrors() {
        DiagnosticBag bag = Check("""
            x := 5
            x += 3
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CompoundAssignment_FloatPlusFloat_NoErrors() {
        DiagnosticBag bag = Check("""
            f := 1.0
            f += 2.0
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CompoundAssignment_StringPlusString_NoErrors() {
        DiagnosticBag bag = Check("""
            s := "a"
            s += "b"
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CompoundAssignment_IntPlusString_EmitsE0002() {
        DiagnosticBag bag = Check("""
            x := 5
            x += "bad"
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void CompoundAssignment_IntPlusFloat_EmitsE0002() {
        // int target with float RHS is a precision-loss error (CRabbit #51).
        DiagnosticBag bag = Check("""
            x := 5
            x += 1.0
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void CompoundAssignment_UndeclaredVariable_EmitsE1001() {
        DiagnosticBag bag = Check("x += 1");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Immutability — const and readonly cannot be reassigned
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_ToConst_EmitsE0201() {
        DiagnosticBag bag = Check("""
            const MAX := 100
            MAX = 200
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0201", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void CompoundAssignment_ToConst_EmitsE0201() {
        DiagnosticBag bag = Check("""
            const MAX := 100
            MAX += 1
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0201", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void Increment_ToConst_EmitsE0201() {
        DiagnosticBag bag = Check("""
            const n := 0
            n++
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0201", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Increment / decrement (++/--)
    // -----------------------------------------------------------------------

    [Fact]
    public void Increment_Int_NoErrors() {
        DiagnosticBag bag = Check("""
            i := 0
            i++
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Decrement_Int_NoErrors() {
        DiagnosticBag bag = Check("""
            i := 10
            i--
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void Increment_Float_EmitsE0002() {
        DiagnosticBag bag = Check("""
            f := 3.14
            f++
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void Increment_Undeclared_EmitsE1001() {
        DiagnosticBag bag = Check("x++");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Scope — block local scoping
    // -----------------------------------------------------------------------

    [Fact]
    public void BlockLocal_UseAfterBlock_EmitsE1001() {
        // A variable declared inside a `{ }` block is not visible outside.
        DiagnosticBag bag = Check("""
            { inner := 5 }
            print(inner)
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1001", diag.Code);
        Assert.Equal((2, 7), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void BlockLocal_UseInsideBlock_NoErrors() {
        DiagnosticBag bag = Check("""
            {
              inner := 5
              print(inner)
            }
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Compound /= and %= — coverage of CompoundOpToBinary arms
    // -----------------------------------------------------------------------

    [Fact]
    public void CompoundAssignment_IntDivideInt_NoErrors() {
        DiagnosticBag bag = Check("""
            x := 10
            x /= 2
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CompoundAssignment_IntModuloInt_NoErrors() {
        DiagnosticBag bag = Check("""
            x := 10
            x %= 3
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CompoundAssignment_FloatDivideFloat_NoErrors() {
        DiagnosticBag bag = Check("""
            f := 4.0
            f /= 2.0
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    [Fact]
    public void CompoundAssignment_FloatModuloFloat_NoErrors() {
        DiagnosticBag bag = Check("""
            f := 4.0
            f %= 3.0
            """);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    // -----------------------------------------------------------------------
    // Increment on non-int, non-float types — E0002 via else-if branch
    // -----------------------------------------------------------------------

    [Fact]
    public void Increment_OnString_EmitsE0002() {
        DiagnosticBag bag = Check("""
            s := "hello"
            s++
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0002", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    // -----------------------------------------------------------------------
    // Readonly bindings — E0202
    // -----------------------------------------------------------------------

    [Fact]
    public void Assignment_ToReadonly_EmitsE0202() {
        DiagnosticBag bag = Check("""
            readonly x := 5
            x = 10
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0202", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void CompoundAssignment_ToReadonly_EmitsE0202() {
        DiagnosticBag bag = Check("""
            readonly x := 5
            x += 1
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0202", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void Increment_OnReadonly_EmitsE0202() {
        DiagnosticBag bag = Check("""
            readonly x := 5
            x++
            """);
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0202", diag.Code);
        Assert.Equal((2, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }
}
