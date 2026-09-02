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
    // Nullable value type argument — 'map<K, V?>' (D-327's '?' suffix)
    // -----------------------------------------------------------------------

    [Fact]
    public void MapLiteral_NullableValueTypeArgument_Parses() {
        // ParseTypeRef's suffix loop (D-327) accepts '?' on any type, so 'map<string, int?>'
        // is a well-formed type-argument list. The literal lookahead must admit the '?'
        // token or the whole literal silently degrades to a 'map' identifier comparison.
        Expression e = ExprOf(ParseOk("map<string, int?>{ \"a\": 1 }\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        Assert.Equal(2, map.TypeArguments.TypeArguments.Count);
        Assert.Equal("int", map.TypeArguments.TypeArguments[1].Name);
        Assert.True(map.TypeArguments.TypeArguments[1].IsNullable);
        Assert.Equal("a", Assert.Single(map.Entries).Key);
    }

    [Fact]
    public void MapLiteral_NullableArrayValueTypeArgument_Parses() {
        Expression e = ExprOf(ParseOk("map<string, int[]?>{ \"a\": [1] }\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        Assert.Equal(2, map.TypeArguments.TypeArguments.Count);
        Assert.True(map.TypeArguments.TypeArguments[1].IsNullable);
    }

    [Fact]
    public void MapAsIdentifier_TernaryComparison_StillParsesAsComparison() {
        // Guards the '?' widening above: 'map < a ? b : c' is a comparison feeding a
        // ternary, not a map literal. The lookahead admits '?' but not ':', so the scan
        // still bails out and 'map' stays an ordinary identifier.
        CompilationUnit unit = ParseOk("map := 5\na := 1\nb := 2\nc := 3\nr := map < a ? b : c\n");
        VarDeclStmt rDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[4]);
        TernaryExpr ternary = Assert.IsType<TernaryExpr>(rDecl.Initializer);
        BinaryExpr cmp = Assert.IsType<BinaryExpr>(ternary.Condition);
        Assert.Equal(BinaryOperator.Less, cmp.Operator);
        Assert.Equal("map", Assert.IsType<IdentifierExpr>(cmp.Left).Name);
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
        // D-405: ParseMapEntryOrError — the call-site local-recovery wrapper around
        // ParseMapEntry — catches the key failure while ParseMapLiteral's own entry
        // loop (the frame that owns the still-open '{') is still on the call stack,
        // so recovery resynchronises to this literal's own ',' or '}' directly
        // rather than letting the exception escape to the top-level recovery
        // wrapper. Before the fix this produced a second, phantom "unexpected
        // token '}'" diagnostic, because Synchronise() ran three frames further
        // out, with the frame that owned the '{' already unwound off the stack —
        // see D-405 for the full trace. The malformed entry is omitted from the
        // literal, not replaced by a placeholder node.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{a: 1}\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected string literal key", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);

        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(map.Entries);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_InterpolatedStringKey_RecoversAndParsingContinues() {
        // "${x}" lexes as StringStart/InterpStart/.../InterpEnd/StringEnd — a genuine
        // TokenKind.StringStart run — so ParseMapEntryKey takes its *second* rejection
        // branch: ParseInterpolatedString() runs to completion (consuming the whole
        // key, cursor left sitting on the ':' that follows it), and only then does the
        // parts.All(p is StringTextPart) check fail (the key has a StringExpressionPart),
        // raising E2001 via FailAt at the key's start location. ParseMapEntryOrError
        // (D-405) still recovers locally to this literal's own ',' or '}' from there —
        // exactly one diagnostic, no phantom.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{\"${x}\": 1}\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected string literal key", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_RawStringKey_RecoversAndParsingContinues() {
        // A backtick literal lexes as the single TokenKind.RawStringLiteral token — not
        // a StringStart/StringEnd run at all — so ParseMapEntryKey fails at its *first*
        // guard (!Check(TokenKind.StringStart)) before any string parsing starts, the
        // same branch the plain-identifier key case exercises above. ParseMapEntryOrError
        // (D-405) recovers locally exactly as in that case — exactly one diagnostic.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{`k`: 1}\nx := 2\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected string literal key", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(23, d.Range.Start.Column);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(2L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_TwoDistinctMalformedKeys_ProducesTwoDiagnosticsNoneSwallowed() {
        // Load-bearing (D-405): proves the fix removes the phantom duplicate without
        // suppressing a second, genuinely independent mistake in the same literal.
        // Before the fix this source also produced exactly 2 diagnostics — but one of
        // them was the phantom "unexpected token '}'" and 'bar''s own mistake was
        // never reported at all, because the entire literal was abandoned by a single
        // top-level Synchronise() sweep after 'foo' failed. After the fix both 'foo'
        // and 'bar' are independently reported and the phantom is gone.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{foo: 1, bar: 2}\nx := 3\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic first = bag.Diagnostics[0];
        Assert.Equal("E2001", first.Code);
        Assert.Equal("expected string literal key", first.Message);
        Assert.Equal(1, first.Range.Start.Line);
        Assert.Equal(23, first.Range.Start.Column); // 'foo'

        Diagnostic second = bag.Diagnostics[1];
        Assert.Equal("E2001", second.Code);
        Assert.Equal("expected string literal key", second.Message);
        Assert.Equal(1, second.Range.Start.Line);
        Assert.Equal(31, second.Range.Start.Column); // 'bar'

        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(map.Entries);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(3L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_UnterminatedAfterMalformedKey_ReportsBothRootCauses() {
        // Adversarial edge case (malformed input never throws, recovery never loops):
        // a malformed key AND a missing closing '}' are two genuinely independent
        // mistakes. The local resync (SkipToNextLiteralElementBoundary) has no ','
        // or '}' at this nesting level to find and safely runs out at EOF — no
        // infinite loop — and the subsequent Expect(RightBrace) then reports the
        // missing brace as its own diagnostic: a real second root cause D-300 says
        // must not be suppressed, not a phantom. Before D-405 this combined case
        // reported only the key mistake — the missing-brace problem was silently
        // absorbed by the (now call-site-scoped, no longer whole-statement)
        // Synchronise() sweep running all the way to EOF.
        (CompilationUnit unit, DiagnosticBag bag) = Parse("m := map<string, int>{foo: 1\n");
        Assert.Equal(2, bag.Diagnostics.Count);

        Diagnostic key = bag.Diagnostics[0];
        Assert.Equal("E2001", key.Code);
        Assert.Equal("expected string literal key", key.Message);
        Assert.Equal(1, key.Range.Start.Line);
        Assert.Equal(23, key.Range.Start.Column); // 'foo'

        // The missing-brace diagnostic is pinned at EOF, which the source's trailing
        // newline puts at line 2, column 1.
        Diagnostic brace = bag.Diagnostics[1];
        Assert.Equal("E2001", brace.Code);
        Assert.Equal("expected '}' to close map literal", brace.Message);
        Assert.Equal(2, brace.Range.Start.Line);
        Assert.Equal(1, brace.Range.Start.Column);

        Assert.NotNull(unit);
    }

    [Fact]
    public void MalformedEntry_SubsequentStatement_TypeChecksCleanly() {
        // Full-pipeline proof that recovery is not just "the parser doesn't crash" —
        // the statement after the malformed literal must both parse and type-check
        // with no further diagnostics, confirming the omitted malformed entry leaves
        // the MapLiteralExpr in a shape the type checker accepts (an empty-entries
        // map literal is already legal — see EmptyMapLiteral_Parses above).
        const string src = "m := map<string, int>{a: 1}\nx := 2\ny := x + 1\n";
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(src, bag);
        Assert.Empty(bag.Diagnostics);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        Diagnostic parseDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", parseDiagnostic.Code);
        Assert.Equal("expected string literal key", parseDiagnostic.Message);
        Assert.Equal(1, parseDiagnostic.Range.Start.Line);
        Assert.Equal(23, parseDiagnostic.Range.Start.Column); // 'a'

        new TypeChecker(bag).Check(unit);

        Diagnostic onlyDiagnostic = Assert.Single(bag.Diagnostics);
        Assert.Same(parseDiagnostic, onlyDiagnostic);

        // Section 3.1.1 / D-311: "no further diagnostics" alone would still pass if
        // the checker had left 'x' unresolved, so assert the LSP-enabling fields on
        // the identifier itself — recovery must not leave the well-formed tail
        // under-annotated.
        VarDeclStmt yDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        BinaryExpr sum = Assert.IsType<BinaryExpr>(yDecl.Initializer);
        IdentifierExpr xRef = Assert.IsType<IdentifierExpr>(sum.Left);
        Assert.Equal("x", xRef.Name);
        // GrobType is a value type, so Assert.NotNull is meaningless — assert that
        // the checker set a non-error type instead.
        Assert.NotEqual(GrobType.Error, xRef.ResolvedType);
        Assert.NotNull(xRef.Declaration);
        Assert.NotSame(UnresolvedDecl.Instance, xRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Error recovery — delimiters the abandoned entry opened before it failed (D-405)
    // -----------------------------------------------------------------------

    [Fact]
    public void MalformedEntry_ValueClosesItsOwnBracketPair_RecoversAtOuterCommaOnly() {
        // Regression (PR #191 review): SkipToNextLiteralElementBoundary must start
        // from the delimiter nesting the abandoned entry had already opened before it
        // failed, not from zero. 'foo(1 2)' fails inside the argument list with '('
        // already consumed, so a from-zero scan met the ')' first, drove its counter
        // negative, and thereafter matched neither the entry ',' nor the literal's own
        // '}' — swallowing the rest of the file, including the well-formed 'x := 9'.
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("m := map<string, int>{\"a\": foo(1 2), \"b\": 3}\nx := 9\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected ')' to close call", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(34, d.Range.Start.Column); // the stray '2'

        // The malformed entry is omitted, but the ',' after the now-closed ')' is a
        // genuine outer boundary, so '"b": 3' is still recovered as a real entry.
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        MapEntry entry = Assert.Single(map.Entries);
        Assert.Equal("b", entry.Key);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(9L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }

    [Fact]
    public void MalformedEntry_ValueLeavesBracketPairOpen_DoesNotReuseInnerComma() {
        // Regression (PR #191 review), the other half of the same defect: here '(' is
        // consumed and never closed, so every ',' that follows belongs to the open
        // paren, not to the entry list. A from-zero scan stopped at the first inner
        // ',' and wrongly promoted '"b": 2' to an outer entry. Carrying the nesting in
        // means the scan stops only at the literal's own '}' — which cannot close a
        // '(' — leaving it unconsumed for Expect(RightBrace). The comma inside the
        // still-open '(' now raises E2209 (D-421 Decision 2) rather than the
        // pre-D-421 generic "expected ')'" — the recovery mechanics this test pins
        // are otherwise unchanged.
        (CompilationUnit unit, DiagnosticBag bag) =
            Parse("m := map<string, int>{\"a\": (1, \"b\": 2}\nx := 9\n");

        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2209", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(30, d.Range.Start.Column); // the ',' inside the still-open '('

        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(Assert.IsType<VarDeclStmt>(unit.TopLevel[0]).Initializer);
        Assert.Empty(map.Entries);

        VarDeclStmt tail = Assert.IsType<VarDeclStmt>(unit.TopLevel[^1]);
        Assert.Equal("x", tail.Name);
        Assert.Equal(9L, Assert.IsType<IntLiteralExpr>(tail.Initializer).Value);
    }
}
