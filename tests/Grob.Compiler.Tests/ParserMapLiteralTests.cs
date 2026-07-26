using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser tests for map-literal construction (D-376) — <c>map&lt;K, V&gt;{ "key": value, … }</c>.
/// Covers the three literal forms, the <c>map</c>-as-bindable-identifier disambiguation
/// (<c>map &lt; x</c> must still parse as a comparison), the header-suppression
/// regressions (this feature must not affect <c>if</c>/<c>while</c>/<c>for</c>/<c>case</c>
/// bodies), source-range correctness, and error recovery.
/// </summary>
public sealed class ParserMapLiteralTests {
    // -----------------------------------------------------------------------
    // The three literal forms
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyMapLiteral_Parses() {
        Expression e = ExprOf(ParseOk("map<string,string>{}\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        Assert.Equal("map", map.TypeArguments.Name);
        Assert.Equal(2, map.TypeArguments.TypeArguments.Count);
        Assert.Equal("string", map.TypeArguments.TypeArguments[0].Name);
        Assert.Equal("string", map.TypeArguments.TypeArguments[1].Name);
        Assert.Empty(map.Entries);
    }

    [Fact]
    public void MultiLineMapLiteral_WithTrailingComma_Parses() {
        CompilationUnit unit = ParseOk("""
            m := map<string, int>{
                "a": 1,
                "b": 2,
            }
            """);
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(decl.Initializer);
        Assert.Equal(2, map.Entries.Count);
        Assert.Equal("a", map.Entries[0].Key);
        Assert.Equal(1L, Assert.IsType<IntLiteralExpr>(map.Entries[0].Value).Value);
        Assert.Equal("b", map.Entries[1].Key);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(map.Entries[1].Value).Value);
    }

    [Fact]
    public void SingleLineMapLiteral_WithTrailingComma_Parses() {
        Expression e = ExprOf(ParseOk("map<string, bool>{ \"verbose\": true, \"dryRun\": false, }\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        Assert.Equal(2, map.Entries.Count);
        Assert.Equal("verbose", map.Entries[0].Key);
        Assert.True(Assert.IsType<BoolLiteralExpr>(map.Entries[0].Value).Value);
        Assert.Equal("dryRun", map.Entries[1].Key);
        Assert.False(Assert.IsType<BoolLiteralExpr>(map.Entries[1].Value).Value);
    }

    // -----------------------------------------------------------------------
    // Disambiguation — 'map' remains an ordinary bindable identifier
    // -----------------------------------------------------------------------

    [Fact]
    public void MapAsIdentifier_ComparisonStillParses() {
        CompilationUnit unit = ParseOk("map := 5\nresult := map < 10\n");
        Assert.Equal(2, unit.TopLevel.Count);

        VarDeclStmt mapDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[0]);
        Assert.Equal("map", mapDecl.Name);
        Assert.Equal(5L, Assert.IsType<IntLiteralExpr>(mapDecl.Initializer).Value);

        VarDeclStmt resultDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        BinaryExpr cmp = Assert.IsType<BinaryExpr>(resultDecl.Initializer);
        Assert.Equal(BinaryOperator.Less, cmp.Operator);
        Assert.Equal("map", Assert.IsType<IdentifierExpr>(cmp.Left).Name);
        Assert.Equal(10L, Assert.IsType<IntLiteralExpr>(cmp.Right).Value);
    }

    [Fact]
    public void MapLiteral_AndMapIdentifier_CoexistInSameFile() {
        // Proves the disambiguation is purely lookahead-driven per occurrence — a bare
        // 'map' identifier and a real 'map<K, V>{ }' literal both parse correctly when
        // they appear in the same compilation unit.
        CompilationUnit unit = ParseOk("map := 5\nm2 := map<string, int>{ \"x\": 1 }\n");
        Assert.Equal(2, unit.TopLevel.Count);
        VarDeclStmt mapDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[0]);
        Assert.IsType<IntLiteralExpr>(mapDecl.Initializer);
        VarDeclStmt m2Decl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(m2Decl.Initializer);
        Assert.Single(map.Entries);
        Assert.Equal("x", map.Entries[0].Key);
    }

    // -----------------------------------------------------------------------
    // Malformed map literal — no following '{' after the type-argument list
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedMapTypeArgs_NoFollowingBrace_RecoversWithOneDiagnostic() {
        // 'map<string, int> 5' — the lookahead requires the closing '>' to be
        // immediately followed by '{'; here it is followed by '5', so this falls
        // through to ordinary expression parsing ('map < string' as a comparison),
        // which then fails at the stray ',' — exactly one E2001, no cascade, and
        // parsing continues cleanly onto the next line.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("map<string, int> 5\nx := 1\n");
        Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", bag.Diagnostics[0].Code);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(1L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    // -----------------------------------------------------------------------
    // Header-suppression regressions — unrelated identifiers/bodies must be unaffected
    // -----------------------------------------------------------------------

    [Fact]
    public void If_WithMapNamedComparisonAndBlockBody_ParsesUnaffected() {
        CompilationUnit unit = ParseOk("someMap := 1\nif (someMap == 1) { }\n");
        IfStmt ifStmt = Assert.IsType<IfStmt>(unit.TopLevel[1]);
        Assert.Empty(ifStmt.Then.Statements);
    }

    [Fact]
    public void While_WithBlockBody_ParsesUnaffected() {
        CompilationUnit unit = ParseOk("cond := true\nwhile (cond) { }\n");
        WhileStmt whileStmt = Assert.IsType<WhileStmt>(unit.TopLevel[1]);
        Assert.Empty(whileStmt.Body.Statements);
    }

    [Fact]
    public void ForIn_BareIdentifierIterable_ParsesUnaffected() {
        CompilationUnit unit = ParseOk("m := #{ a: 1 }\nfor k, v in m { }\n");
        ForInStmt forIn = Assert.IsType<ForInStmt>(unit.TopLevel[1]);
        Assert.IsType<IdentifierExpr>(forIn.Iterable);
    }

    [Fact]
    public void ForIn_MapLiteralAsIterable_LoopBodyNotConsumedByLiteral() {
        // The map literal's own '{ "a": 1 }' braces must not swallow the loop body's
        // separate '{ }' — proving no misfire against the following block.
        CompilationUnit unit = ParseOk("for k, v in map<string,int>{\"a\":1} { }\n");
        ForInStmt forIn = Assert.IsType<ForInStmt>(unit.TopLevel[0]);
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(forIn.Iterable);
        Assert.Single(map.Entries);
        Assert.Empty(forIn.Body.Statements);
    }

    [Fact]
    public void Case_WithBlockBody_ParsesUnaffected() {
        CompilationUnit unit = ParseOk("select (1) {\ncase 1 { }\n}\n");
        SelectStmt select = Assert.IsType<SelectStmt>(unit.TopLevel[0]);
        Assert.Single(select.Cases);
        Assert.Empty(select.Cases[0].Body.Statements);
    }

    // -----------------------------------------------------------------------
    // Source range correctness
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_RangeCoversMapKeywordThroughClosingBrace() {
        // Columns: m(1)a(2)p(3)<(4)s(5)t(6)r(7)i(8)n(9)g(10),(11)s(12)t(13)r(14)i(15)n(16)g(17)>(18){(19)}(20)
        Expression e = ExprOf(ParseOk("map<string,string>{}\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        Assert.Equal(1, map.Range.Start.Line);
        Assert.Equal(1, map.Range.Start.Column);
        Assert.Equal(1, map.Range.End.Line);
        Assert.Equal(20, map.Range.End.Column);
        Assert.Equal(1, map.TypeArguments.Range.Start.Line);
        Assert.Equal(1, map.TypeArguments.Range.Start.Column);
    }

    [Fact]
    public void MapEntry_RangeCoversKeyThroughValue() {
        // Columns: m(1)a(2)p(3)<(4)s(5)t(6)r(7)i(8)n(9)g(10),(11)s(12)t(13)r(14)i(15)n(16)g(17)
        //          >(18){(19)"(20)a(21)"(22):(23)1(24)}(25)
        Expression e = ExprOf(ParseOk("map<string,string>{\"a\":1}\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        MapEntry entry = Assert.Single(map.Entries);
        Assert.Equal("a", entry.Key);
        Assert.Equal(1, entry.Range.Start.Line);
        Assert.Equal(20, entry.Range.Start.Column);
        Assert.Equal(1, entry.Range.End.Line);
        Assert.Equal(24, entry.Range.End.Column);
    }

    // -----------------------------------------------------------------------
    // Error recovery — unterminated literal, malformed (non-string) key
    // -----------------------------------------------------------------------

    [Fact]
    public void UnterminatedMapLiteral_RecoversWithOneDiagnostic() {
        (_, DiagnosticBag bag) = Parse("m := map<string, int>{\"a\": 1\n");
        Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", bag.Diagnostics[0].Code);
    }

    [Fact]
    public void MalformedEntry_NonStringLiteralKey_RecoversAndParsingContinues() {
        // The key diagnostic (E2001, "expected string literal key") is raised before the
        // map literal's own '}' is reached, so the still-open '{' is behind the parser's
        // cursor when Synchronise() runs — Synchronise (§29) treats ANY '}' as an anchor
        // regardless of nesting, so it stops at the literal's own closing brace rather
        // than skipping past it, and the next top-level parse attempt immediately fails
        // again on that leftover '}' ("unexpected token '}'"). This is not specific to map
        // literals: the identical two-diagnostic shape occurs today for a malformed
        // anon-struct field failing before its own '}' (e.g. '#{ .bad: 1 }') — a
        // pre-existing, general limitation of the brace-anchor recovery model shared by
        // every '{ }'-delimited construct, not a defect introduced here. Parsing still
        // recovers cleanly onto the next line — no unbounded cascade.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{a: 1}\nx := 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);
        Assert.All(bag.Diagnostics, d => Assert.Equal("E2001", d.Code));

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_InterpolatedStringKey_RecoversAndParsingContinues() {
        // "${x}" lexes as StringStart/InterpStart/.../InterpEnd/StringEnd — a genuine
        // TokenKind.StringStart run — so ParseMapEntryKey takes the *second* rejection
        // branch: ParseInterpolatedString() runs to completion (consuming the whole
        // key, cursor left sitting on the ':' that follows it), and only then does the
        // parts.All(p is StringTextPart) check fail (the key has a StringExpressionPart),
        // raising E2001 via FailAt at the key's start location. That still leaves the
        // cursor behind the map literal's own unclosed '}': Synchronise (§29) walks
        // forward from ':' through '1' and stops at that '}' as its brace anchor,
        // producing the same two-diagnostic shape as the identifier-key case above (the
        // leftover '}' immediately fails again as "unexpected token '}'") — the anchor
        // is reached regardless of which branch inside ParseMapEntryKey threw, since
        // both leave the cursor before the literal's closing brace.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{\"${x}\": 1}\nx := 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);
        Assert.All(bag.Diagnostics, d => Assert.Equal("E2001", d.Code));

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_RawStringKey_RecoversAndParsingContinues() {
        // A backtick literal lexes as the single TokenKind.RawStringLiteral token — not
        // a StringStart/StringEnd run at all — so ParseMapEntryKey fails at its *first*
        // guard (!Check(TokenKind.StringStart)) before any string parsing starts, the
        // same branch the plain-identifier key case exercises above; the doc comment on
        // ParseMapEntryKey groups "an identifier, a raw string, or a genuinely
        // interpolated string" together as E2001, but a raw string in fact shares the
        // identifier case's code path, not the interpolated case's. The cursor sits on
        // the untouched raw-string token when Fail() fires, and Synchronise (§29) walks
        // forward through ':' and '1' to the map literal's own '}' anchor exactly as in
        // the other malformed-key cases, giving the same two-diagnostic cascade.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{`k`: 1}\nx := 2\n");
        Assert.Equal(2, bag.Diagnostics.Count);
        Assert.All(bag.Diagnostics, d => Assert.Equal("E2001", d.Code));

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }
}
