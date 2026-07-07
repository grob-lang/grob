using Grob.Compiler.Ast;
using Grob.Core;

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
        CompilationUnit unit = ParseOk("x: int := 1\n");
        VarDeclStmt v = Single<VarDeclStmt>(unit);
        Assert.NotNull(v.AnnotatedType);
        Assert.Equal("int", v.AnnotatedType!.Name);
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
            "if (a) { 1 } else if (b) { 2 } else { 3 }\n");
        IfStmt outer = Single<IfStmt>(unit);
        IfStmt inner = Assert.IsType<IfStmt>(outer.Else);
        Assert.IsType<BlockStmt>(inner.Else);
    }

    [Fact]
    public void If_WithoutOpeningParen_IsError() {
        // §1: parentheses around the condition are required. The diagnostic
        // pins at the condition token where the '(' should have been.
        (_, DiagnosticBag bag) = Parse("if a { 1 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(4, d.Range.Start.Column);
    }

    [Fact]
    public void If_WithoutClosingParen_IsError() {
        (_, DiagnosticBag bag) = Parse("if (a { 1 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(7, d.Range.Start.Column);
    }

    [Fact]
    public void While_Body_Parses() {
        CompilationUnit unit = ParseOk("while (c) { x = x + 1 }\n");
        WhileStmt w = Single<WhileStmt>(unit);
        Assert.Single(w.Body.Statements);
    }

    [Fact]
    public void While_WithoutOpeningParen_IsError() {
        // §2: parentheses around the condition are required.
        (_, DiagnosticBag bag) = Parse("while c { c = false }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(7, d.Range.Start.Column);
    }

    [Fact]
    public void While_WithoutClosingParen_IsError() {
        (_, DiagnosticBag bag) = Parse("while (c { c = false }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(10, d.Range.Start.Column);
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
        CompilationUnit unit = ParseOk("while (c) { break\ncontinue }\n");
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
        // D-274/§27: the catch-all form is 'catch <name> { }' — the identifier
        // is required (it binds the caught GrobError). No-parens, no-name is
        // not a form the grammar supports.
        CompilationUnit unit = ParseOk("try { 1 } catch e { 2 }\n");
        TryStmt t = Single<TryStmt>(unit);
        CatchClause c = Assert.Single(t.Catches);
        Assert.Null(c.ExceptionType);
        Assert.Equal("e", c.ExceptionVariable);
    }

    [Theory]
    [InlineData("try { 1 } catch (e) { 2 }\n")]     // D-274: parens + identifier, no type — not a silent "type-only catch"
    [InlineData("try { 1 } catch () { 2 }\n")]      // empty parens header
    [InlineData("try { 1 } catch { 2 }\n")]         // no-parens catch-all with no bound identifier
    public void Catch_MalformedHeader_IsError(string source) {
        (_, DiagnosticBag bag) = Parse(source);
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
    }

    [Fact]
    public void Throw_ConstructedException_Parses() {
        CompilationUnit unit = ParseOk("throw IoError { message: \"x\" }\n");
        ThrowStmt t = Single<ThrowStmt>(unit);
        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(t.Value);
        Assert.Equal("IoError", sc.TypeName);
    }

    [Fact]
    public void Throw_BoundIdentifier_Parses() {
        CompilationUnit unit = ParseOk("throw e\n");
        ThrowStmt t = Single<ThrowStmt>(unit);
        Assert.IsType<IdentifierExpr>(t.Value);
    }

    [Fact]
    public void Throw_MissingOperand_IsError() {
        // The operand is mandatory (D-274) — unlike 'return', there is no bare-throw form.
        (_, DiagnosticBag bag) = Parse("throw\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void Select_With_Cases_And_Default() {
        CompilationUnit unit = ParseOk(
            "select (x) {\ncase 1 { a }\ncase 2, 3 { b }\ndefault { c }\n}\n");
        SelectStmt s = Single<SelectStmt>(unit);
        Assert.Equal(2, s.Cases.Count);
        Assert.Equal(2, s.Cases[1].Patterns.Count);
        Assert.NotNull(s.Default);
    }

    [Fact]
    public void Select_WithoutOpeningParen_IsError() {
        // §3: parentheses around the subject value are required. Since D-320 'select'
        // is a reserved identifier dispatched at statement head, so a leading 'select'
        // not followed by '(' fails at the head itself — the diagnostic points at
        // 'select' (column 1), not at the token after it.
        (_, DiagnosticBag bag) = Parse("select x {\ncase 1 { a }\n}\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
    }

    [Fact]
    public void Select_WithoutClosingParen_IsError() {
        (_, DiagnosticBag bag) = Parse("select (x {\ncase 1 { a }\n}\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(11, d.Range.Start.Column);
    }
}
