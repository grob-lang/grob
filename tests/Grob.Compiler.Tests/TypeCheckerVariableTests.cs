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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0001");
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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E1001");
    }

    [Fact]
    public void Assignment_TypeMismatch_EmitsE0001() {
        DiagnosticBag bag = Check("""
            x := 5
            x = "hello"
            """);
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0001");
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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E1102");
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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0002");
    }

    [Fact]
    public void CompoundAssignment_UndeclaredVariable_EmitsE1001() {
        DiagnosticBag bag = Check("x += 1");
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E1001");
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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0201");
    }

    [Fact]
    public void CompoundAssignment_ToConst_EmitsE0201() {
        DiagnosticBag bag = Check("""
            const MAX := 100
            MAX += 1
            """);
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0201");
    }

    [Fact]
    public void Increment_ToConst_EmitsE0201() {
        DiagnosticBag bag = Check("""
            const n := 0
            n++
            """);
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0201");
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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E0002");
    }

    [Fact]
    public void Increment_Undeclared_EmitsE1001() {
        DiagnosticBag bag = Check("x++");
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E1001");
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
        Assert.True(bag.HasErrors);
        Assert.Contains(bag.Errors, d => d.Code == "E1001");
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
}
