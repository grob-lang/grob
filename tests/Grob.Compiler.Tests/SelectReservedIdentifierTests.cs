using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Expressions;
using Grob.Compiler.Ast.Statements;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Sprint 5 correctness increment (D-320) — <c>select</c> is a reserved identifier,
/// not a hard keyword. It lexes as an identifier, stays legal as a member name after
/// <c>.</c>, is promoted to the select statement only at statement head when followed
/// by <c>(</c>, and may not be a user binding (E1103, shared with <c>formatAs</c>,
/// D-282).
/// </summary>
public sealed class SelectReservedIdentifierTests {
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    // -----------------------------------------------------------------------
    // Parser, positive — the statement form survives un-reserving
    // -----------------------------------------------------------------------

    [Fact]
    public void SelectStatement_StillParses() {
        CompilationUnit unit = ParseOk("select (x) { case 1 { } default { } }");
        SelectStmt stmt = Single<SelectStmt>(unit);
        Assert.Single(stmt.Cases);
        Assert.NotNull(stmt.Default);
    }

    // -----------------------------------------------------------------------
    // Parser, positive — `.select` is now ordinary member access
    // -----------------------------------------------------------------------

    [Fact]
    public void DotSelect_ParsesAsMethodCallOnMemberAccess() {
        CompilationUnit unit = ParseOk("arr.select(f => f.name)");
        CallExpr call = Assert.IsType<CallExpr>(ExprOf(unit));
        MemberAccessExpr member = Assert.IsType<MemberAccessExpr>(call.Callee);
        Assert.Equal("select", member.Member);
        Assert.IsType<IdentifierExpr>(member.Target);
    }

    [Fact]
    public void PipelineChain_WithSelect_Parses() {
        CompilationUnit unit = ParseOk("files.filter(f => f).select(f => f).sort()");
        // Outermost call is .sort(); peeling reaches .select then .filter.
        CallExpr sortCall = Assert.IsType<CallExpr>(ExprOf(unit));
        MemberAccessExpr sortMember = Assert.IsType<MemberAccessExpr>(sortCall.Callee);
        Assert.Equal("sort", sortMember.Member);

        CallExpr selectCall = Assert.IsType<CallExpr>(sortMember.Target);
        MemberAccessExpr selectMember = Assert.IsType<MemberAccessExpr>(selectCall.Callee);
        Assert.Equal("select", selectMember.Member);
    }

    [Fact]
    public void Declaration_WithDotSelectRhs_Parses() {
        CompilationUnit unit = ParseOk("r := arr.select(x => x)");
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        Assert.Equal("r", decl.Name);
        CallExpr call = Assert.IsType<CallExpr>(decl.Initializer);
        MemberAccessExpr member = Assert.IsType<MemberAccessExpr>(call.Callee);
        Assert.Equal("select", member.Member);
    }

    // -----------------------------------------------------------------------
    // Parser, negative — a leading `select` that is neither a select statement
    // nor a binding declaration is a malformed select statement
    // -----------------------------------------------------------------------

    [Fact]
    public void LeadingSelect_NotFollowedByParenOrBinding_IsParseError() {
        (_, DiagnosticBag bag) = Parse("select 5");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E2001", diag.Code);
    }

    // -----------------------------------------------------------------------
    // Type checker, negative — E1103 reserved identifier used as a binding name
    // -----------------------------------------------------------------------

    [Fact]
    public void SelectAsLocalBinding_EmitsE1103() {
        DiagnosticBag bag = Check("select := 5");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1103", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void SelectAsTypedLocalBinding_EmitsE1103() {
        // The typed-declaration form 'select: T := …' also falls through to the
        // declaration path (it is a name use, not a select statement).
        DiagnosticBag bag = Check("select: int := 5");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1103", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void SelectAsFunctionName_EmitsE1103() {
        DiagnosticBag bag = Check("fn select(): int { return 1 }");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1103", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void SelectAsParameterName_EmitsE1103() {
        DiagnosticBag bag = Check("fn f(select: int): int { return select }");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1103", diag.Code);
        Assert.Equal((1, 6), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void SelectAsTypeFieldName_EmitsE1103() {
        DiagnosticBag bag = Check("type T { select: int }");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1103", diag.Code);
        Assert.Equal((1, 10), (diag.Range.Start.Line, diag.Range.Start.Column));
    }

    [Fact]
    public void FormatAsAsLocalBinding_EmitsE1103() {
        // Proves the shared reserved-identifier rule covers formatAs (D-282), not
        // only select (D-320).
        DiagnosticBag bag = Check("formatAs := 1");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1103", diag.Code);
        Assert.Equal((1, 1), (diag.Range.Start.Line, diag.Range.Start.Column));
    }
}
