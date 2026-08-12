using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser tests for anonymous-struct literals (Sprint 6D) — <c>#{ field: value, … }</c>.
/// Covers the well-formed shape and error recovery for a malformed field name
/// (D-405) — the anon-struct half of the map-literal double-diagnostic fix. The
/// named-struct-construction call site (<c>TypeName { … }</c> in <c>ParsePostfix</c>)
/// shares <c>FieldInit</c>/<c>ParseOneFieldInit</c> but is explicitly out of scope for
/// D-405 and is not touched or tested here.
/// </summary>
public sealed class ParserAnonStructLiteralTests {
    // -----------------------------------------------------------------------
    // Well-formed shape
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyAnonStructLiteral_Parses() {
        CompilationUnit unit = ParseOk("s := #{ }\n");
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        AnonStructExpr anon = Assert.IsType<AnonStructExpr>(decl.Initializer);
        Assert.Empty(anon.Fields);
    }

    [Fact]
    public void AnonStructLiteral_WithFields_Parses() {
        CompilationUnit unit = ParseOk("s := #{ name: \"Alice\", age: 30 }\n");
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        AnonStructExpr anon = Assert.IsType<AnonStructExpr>(decl.Initializer);
        Assert.Equal(2, anon.Fields.Count);
        Assert.Equal("name", anon.Fields[0].Name);
        Assert.Equal("age", anon.Fields[1].Name);
    }

    // -----------------------------------------------------------------------
    // Error recovery — malformed field name (D-405)
    // -----------------------------------------------------------------------
    //
    // Unlike ParseMapEntryKey (which has two rejection branches — not-StringStart,
    // and StringStart-but-not-plain), ParseOneFieldInit has exactly one: field
    // names are always required via a single Expect(TokenKind.Identifier, …), so
    // every malformed shape below fails at that same guard, immediately, before
    // any of the field's own value is attempted.

    [Fact]
    public void MalformedField_StringLiteralName_RecoversAndParsingContinues() {
        // ParseFieldInitOrError (D-405) catches the field-name failure while
        // ParseAnonStructLiteral's own field loop — the frame that owns the
        // still-open '{' — is still on the call stack, so recovery resynchronises
        // to this literal's own ',' or '}' directly. Before the fix this produced
        // a second, phantom "unexpected token '}'" diagnostic.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("s := #{ \"foo\": 1 }\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected field name", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);

        AnonStructExpr anon = Assert.IsType<AnonStructExpr>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(anon.Fields);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_InterpolatedStringName_RecoversAndParsingContinues() {
        // "${x}" still lexes as a StringStart run, but ParseOneFieldInit's single
        // Expect(Identifier) guard rejects it immediately — unlike ParseMapEntryKey,
        // it never attempts ParseInterpolatedString() at all. Same recovery as the
        // plain-string case above: exactly one diagnostic.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("s := #{ \"k${x}\": 1 }\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected field name", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_RawStringName_RecoversAndParsingContinues() {
        // A backtick literal lexes as the single TokenKind.RawStringLiteral token —
        // rejected by the same Expect(Identifier) guard as the other two shapes.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("s := #{ `foo`: 1 }\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected field name", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_TwoDistinctMalformedNames_ProducesTwoDiagnosticsNoneSwallowed() {
        // Load-bearing (D-405), the anon-struct twin of the map-literal test of the
        // same shape: proves the fix does not swallow a second, genuinely
        // independent mistake in the same literal.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("s := #{ 1: 1, 2: 2 }\nx := 3\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E2001", first.Code);
        Assert.Equal("expected field name", first.Message);
        Assert.Equal(1, first.Range.Start.Line);
        Assert.Equal(9, first.Range.Start.Column);

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E2001", second.Code);
        Assert.Equal("expected field name", second.Message);
        Assert.Equal(1, second.Range.Start.Line);
        Assert.Equal(15, second.Range.Start.Column);

        AnonStructExpr anon = Assert.IsType<AnonStructExpr>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(anon.Fields);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(3L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedField_SubsequentStatement_TypeChecksCleanly() {
        // Full-pipeline proof: the statement after the malformed literal both
        // parses and type-checks with no further diagnostics — an empty-fields
        // anon-struct is already legal (see EmptyAnonStructLiteral_Parses above).
        const string src = "s := #{ 1: 1 }\nx := 2\ny := x + 1\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", parseDiagnostic.Code);

        new TypeChecker(bag).Check(unit);

        Diagnostic onlyDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Same(parseDiagnostic, onlyDiagnostic);
    }
}
