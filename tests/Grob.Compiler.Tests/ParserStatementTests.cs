using Grob.Compiler.Ast;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

public class ParserStatementTests {
    [Fact]
    public void VarDecl_WithoutAnnotation() {
        CompilationUnit unit = ParseOk("x := 1\n");
        VarDeclStmt v = Single<VarDeclStmt>(unit);
        Assert.Equal("x", v.Name);
        Assert.Null(v.AnnotatedType);
    }

    [Fact]
    public void VarDecl_WithAnnotation() {
        CompilationUnit unit = ParseOk("x: Int := 1\n");
        VarDeclStmt v = Single<VarDeclStmt>(unit);
        Assert.NotNull(v.AnnotatedType);
        Assert.Equal("Int", v.AnnotatedType!.Name);
    }

    [Fact]
    public void Assignment_PlainEquals() {
        CompilationUnit unit = ParseOk("x = 1\n");
        AssignmentStmt a = Single<AssignmentStmt>(unit);
        Assert.IsType<IdentifierExpr>(a.Target);
    }

    [Theory]
    [InlineData("x += 1", CompoundAssignmentOperator.PlusAssign)]
    [InlineData("x -= 1", CompoundAssignmentOperator.MinusAssign)]
    [InlineData("x *= 1", CompoundAssignmentOperator.StarAssign)]
    [InlineData("x /= 1", CompoundAssignmentOperator.SlashAssign)]
    [InlineData("x %= 1", CompoundAssignmentOperator.PercentAssign)]
    public void CompoundAssignment(string src, CompoundAssignmentOperator op) {
        CompilationUnit unit = ParseOk(src + "\n");
        CompoundAssignmentStmt s = Single<CompoundAssignmentStmt>(unit);
        Assert.Equal(op, s.Operator);
    }

    [Theory]
    [InlineData("x++", IncrementKind.Increment)]
    [InlineData("x--", IncrementKind.Decrement)]
    public void IncrementStatements(string src, IncrementKind kind) {
        CompilationUnit unit = ParseOk(src + "\n");
        IncrementStmt s = Single<IncrementStmt>(unit);
        Assert.Equal(kind, s.Kind);
    }

    [Fact]
    public void If_Else_If_Else() {
        CompilationUnit unit = ParseOk(
            "if a { 1 } else if b { 2 } else { 3 }\n");
        IfStmt outer = Single<IfStmt>(unit);
        IfStmt inner = Assert.IsType<IfStmt>(outer.Else);
        Assert.IsType<BlockStmt>(inner.Else);
    }

    [Fact]
    public void While_Body_Parses() {
        CompilationUnit unit = ParseOk("while c { x = x + 1 }\n");
        WhileStmt w = Single<WhileStmt>(unit);
        Assert.Single(w.Body.Statements);
    }

    [Fact]
    public void ForIn_OneVar_Collection() {
        CompilationUnit unit = ParseOk("for x in xs { x }\n");
        ForInStmt f = Single<ForInStmt>(unit);
        Assert.Equal(["x"], f.Variables);
        Assert.IsType<IdentifierExpr>(f.Iterable);
    }

    [Fact]
    public void ForIn_TwoVars_IndexCollection() {
        CompilationUnit unit = ParseOk("for i, x in xs { x }\n");
        ForInStmt f = Single<ForInStmt>(unit);
        Assert.Equal(["i", "x"], f.Variables);
    }

    [Fact]
    public void ForIn_NumericRange_WithStep() {
        CompilationUnit unit = ParseOk("for i in 1..10 step 2 { i }\n");
        ForInStmt f = Single<ForInStmt>(unit);
        NumericRangeExpr r = Assert.IsType<NumericRangeExpr>(f.Iterable);
        Assert.NotNull(r.Step);
    }

    [Fact]
    public void Return_WithoutValue() {
        CompilationUnit unit = ParseOk("fn f(): Void { return }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ReturnStmt r = (ReturnStmt)fn.Body.Statements[0];
        Assert.Null(r.Value);
    }

    [Fact]
    public void Return_WithValue() {
        CompilationUnit unit = ParseOk("fn f(): Int { return 42 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        ReturnStmt r = (ReturnStmt)fn.Body.Statements[0];
        Assert.Equal(42L, Assert.IsType<IntLiteralExpr>(r.Value!).Value);
    }

    [Fact]
    public void BreakAndContinue() {
        CompilationUnit unit = ParseOk("while c { break\ncontinue }\n");
        WhileStmt w = Single<WhileStmt>(unit);
        Assert.IsType<BreakStmt>(w.Body.Statements[0]);
        Assert.IsType<ContinueStmt>(w.Body.Statements[1]);
    }

    [Fact]
    public void Try_Catch_Finally() {
        CompilationUnit unit = ParseOk(
            "try { 1 } catch (e: Error) { 2 } finally { 3 }\n");
        TryStmt t = Single<TryStmt>(unit);
        Assert.Single(t.Catches);
        Assert.Equal("e", t.Catches[0].ExceptionVariable);
        Assert.Equal("Error", t.Catches[0].ExceptionType!.Name);
        Assert.NotNull(t.Finally);
    }

    [Fact]
    public void Try_CatchAll_NoParens() {
        CompilationUnit unit = ParseOk("try { 1 } catch { 2 }\n");
        TryStmt t = Single<TryStmt>(unit);
        CatchClause c = Assert.Single(t.Catches);
        Assert.Null(c.ExceptionType);
        Assert.Null(c.ExceptionVariable);
    }

    [Fact]
    public void Select_With_Cases_And_Default() {
        CompilationUnit unit = ParseOk(
            "select x {\ncase 1 { a }\ncase 2, 3 { b }\ndefault { c }\n}\n");
        SelectStmt s = Single<SelectStmt>(unit);
        Assert.Equal(2, s.Cases.Count);
        Assert.Equal(2, s.Cases[1].Patterns.Count);
        Assert.NotNull(s.Default);
    }
}
