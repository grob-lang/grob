using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// Parser tests for trailing-comma uniformity (D-421, implemented here) — an optional
/// trailing comma is now accepted in every comma-separated list except a grouping
/// parenthesis and the <c>for k, v in</c> fixed pair. Covers the six constructs that
/// changed (function signature parameters, function-type parameters, generic/map
/// type-argument lists, <c>select</c> case patterns, call arguments, lambda parameters),
/// regression pins for the four constructs that already accepted a trailing comma
/// (array literals and named/anonymous struct construction — switch-expression arms and
/// map literals already have their own pins in <see cref="SwitchExprParserTests"/> and
/// <see cref="ParserMapLiteralTests"/>), the <c>for k, v, in</c> non-list pin, E2209's two
/// new throw sites, and the leading/doubled-comma baseline that stays E2001.
/// </summary>
public sealed class ParserTrailingCommaTests {
    // -----------------------------------------------------------------------
    // Row 1 — function signature parameters (ParseParameterList, terminator RightParen)
    // -----------------------------------------------------------------------

    [Fact]
    public void FnParams_SingleLine_NoTrailingComma_Parses() {
        CompilationUnit unit = ParseOk("fn foo(a: int, b: int): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("b", fn.Parameters[1].Name);
    }

    [Fact]
    public void FnParams_SingleLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("fn foo(a: int, b: int,): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("b", fn.Parameters[1].Name);
    }

    [Fact]
    public void FnParams_MultiLine_NoTrailingComma_Parses() {
        CompilationUnit unit = ParseOk("""
            fn foo(
                a: int,
                b: int
            ): int { return 0 }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("b", fn.Parameters[1].Name);
    }

    [Fact]
    public void FnParams_MultiLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("""
            fn foo(
                a: int,
                b: int,
            ): int { return 0 }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        Assert.Equal(2, fn.Parameters.Count);
        Assert.Equal("a", fn.Parameters[0].Name);
        Assert.Equal("b", fn.Parameters[1].Name);
    }

    [Fact]
    public void FnParams_EmptyList_TrailingCommaAlone_StillRejected() {
        // An empty parameter list is legal ('fn foo(): int'); a bare comma with
        // nothing before it is not a trailing comma on anything — it is caught by
        // the ordinary parameter-name Expect, unaffected by the new guard (the
        // guard only fires once at least one parameter has already been parsed).
        (_, DiagnosticBag bag) = Parse("fn foo(,): int { return 0 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected parameter name", d.Message);
    }

    [Fact]
    public void FnParams_DoubledComma_StillE2001AtSecondComma() {
        (_, DiagnosticBag bag) = Parse("fn foo(a: int,, b: int): int { return 0 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected parameter name", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(15, d.Range.Start.Column); // the second ','
    }

    // -----------------------------------------------------------------------
    // Row 2 — function-type parameters (fn(T1, T2): R, inside ParseTypePrimary)
    // -----------------------------------------------------------------------

    [Fact]
    public void FnTypeParams_SingleLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("fn f(action: fn(int, string,): bool): int { return 0 }\n");
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef paramType = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.Equal(2, paramType.ParameterTypes.Count);
        Assert.Equal("int", paramType.ParameterTypes[0].Name);
        Assert.Equal("string", paramType.ParameterTypes[1].Name);
    }

    [Fact]
    public void FnTypeParams_MultiLine_NoTrailingComma_Parses() {
        CompilationUnit unit = ParseOk("""
            fn f(action: fn(
                int,
                string
            ): bool): int { return 0 }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef paramType = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.Equal(2, paramType.ParameterTypes.Count);
    }

    [Fact]
    public void FnTypeParams_MultiLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("""
            fn f(action: fn(
                int,
                string,
            ): bool): int { return 0 }
            """);
        FnDecl fn = Single<FnDecl>(unit);
        FunctionTypeRef paramType = Assert.IsType<FunctionTypeRef>(fn.Parameters[0].Type);
        Assert.Equal(2, paramType.ParameterTypes.Count);
        Assert.Equal("int", paramType.ParameterTypes[0].Name);
        Assert.Equal("string", paramType.ParameterTypes[1].Name);
    }

    [Fact]
    public void FnTypeParams_DoubledComma_StillE2001() {
        (_, DiagnosticBag bag) = Parse("fn f(action: fn(int,, string): bool): int { return 0 }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected type name", d.Message);
    }

    // -----------------------------------------------------------------------
    // Row 3 — generic / map type-argument list (ParseTypeArgumentList, terminator Greater)
    // -----------------------------------------------------------------------

    [Fact]
    public void TypeArgs_SingleLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("m: map<string, int,> := map<string, int>{ \"a\": 1 }\n");
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        TypeRef type = Assert.IsType<TypeRef>(decl.AnnotatedType);
        Assert.Equal(2, type.TypeArguments.Count);
        Assert.Equal("string", type.TypeArguments[0].Name);
        Assert.Equal("int", type.TypeArguments[1].Name);
    }

    [Fact]
    public void TypeArgs_MultiLine_NoTrailingComma_Parses() {
        // The closer ('>') is kept glued to the last type argument rather than on
        // its own line — see the increment's decisions-log entry for why a bare
        // newline directly before '>' is a separate, pre-existing gap this
        // increment does not touch.
        CompilationUnit unit = ParseOk("""
            m: map<string,
                int> := map<string, int>{ "a": 1 }
            """);
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        TypeRef type = Assert.IsType<TypeRef>(decl.AnnotatedType);
        Assert.Equal(2, type.TypeArguments.Count);
    }

    [Fact]
    public void TypeArgs_MultiLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("""
            m: map<string,
                int,> := map<string, int>{ "a": 1 }
            """);
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        TypeRef type = Assert.IsType<TypeRef>(decl.AnnotatedType);
        Assert.Equal(2, type.TypeArguments.Count);
        Assert.Equal("string", type.TypeArguments[0].Name);
        Assert.Equal("int", type.TypeArguments[1].Name);
    }

    [Fact]
    public void TypeArgs_DoubledComma_StillE2001() {
        (_, DiagnosticBag bag) = Parse("m: map<string,, int> := map<string, int>{}\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected type name", d.Message);
    }

    [Fact]
    public void MapAsCallTypeArgument_TrailingComma_Parses() {
        // D-416 side effect: LooksLikeTypeArgumentList's scan admits ',' in its run
        // and commits to the generic-call reading; before this increment
        // ParseTypeArgumentList then hard-failed on the trailing comma it had just
        // been promised. x.mapAs<T,>() now parses.
        CompilationUnit unit = ParseOk("a := 1\nb := a.mapAs<Employee,>()\n");
        VarDeclStmt bDecl = Assert.IsType<VarDeclStmt>(unit.TopLevel[1]);
        CallExpr call = Assert.IsType<CallExpr>(bDecl.Initializer);
        TypeRef typeArg = Assert.Single(call.TypeArguments);
        Assert.Equal("Employee", typeArg.Name);
    }

    // -----------------------------------------------------------------------
    // Row 5 — select case-pattern list (ParseSelect, terminator LeftBrace)
    // -----------------------------------------------------------------------

    [Fact]
    public void CasePatterns_SingleLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("select (x) {\ncase 200, 201, { a }\n}\n");
        SelectStmt select = Single<SelectStmt>(unit);
        CaseClause clause = Assert.Single(select.Cases);
        Assert.Equal(2, clause.Patterns.Count);
    }

    [Fact]
    public void CasePatterns_MultiLine_NoTrailingComma_Parses() {
        // The block-opening '{' is kept on the same line as the last pattern — see
        // the row-3 note above; the identical pre-existing gap applies to '{' here.
        CompilationUnit unit = ParseOk("""
            select (x) {
            case 200,
            201 { a }
            }
            """);
        SelectStmt select = Single<SelectStmt>(unit);
        CaseClause clause = Assert.Single(select.Cases);
        Assert.Equal(2, clause.Patterns.Count);
    }

    [Fact]
    public void CasePatterns_MultiLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("""
            select (x) {
            case 200,
            201, { a }
            }
            """);
        SelectStmt select = Single<SelectStmt>(unit);
        CaseClause clause = Assert.Single(select.Cases);
        Assert.Equal(2, clause.Patterns.Count);
    }

    [Fact]
    public void CasePatterns_LeadingComma_StillDistinguishableAsE2001() {
        // Pins §1.2's finding: the leading comma is caught by ParsePrimary's
        // default arm (unexpected token) — a structurally different path from the
        // trailing comma's LeftBrace guard — so the two remain distinguishable.
        // ParseSelect's pattern loop has no local recovery wrapper of its own
        // (pre-existing, unrelated to this increment — unlike the map/struct/switch
        // literal loops, D-405/D-406 was never applied here), so a malformed pattern
        // is expected to cascade into a second, top-level-recovery diagnostic. Only
        // the root cause — code, message and position — is this test's concern.
        (_, DiagnosticBag bag) = Parse("x := 1\nselect (x) {\ncase , 200 { a }\n}\ny := 2\n");
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Contains("expected expression", d.Message);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column); // the leading ','
    }

    [Fact]
    public void CasePatterns_TrailingCommaThenGenuinelyMalformedPattern_StillFailsAtThatToken() {
        // A trailing comma must not become a synchronisation anchor: 'case 200, .'
        // is not a legal pattern list under any reading, and the guard (which only
        // fires when the token after the comma is '{') must not swallow it — the
        // root-cause diagnostic still lands on the genuinely malformed token, not on
        // the comma or the '{'. See the note above on ParseSelect's pre-existing lack
        // of a local recovery wrapper for why a second, cascaded diagnostic follows.
        (_, DiagnosticBag bag) = Parse("x := 1\nselect (x) {\ncase 200, . { a }\n}\ny := 2\n");
        Diagnostic d = bag.Diagnostics[0];
        Assert.Equal("E2001", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(11, d.Range.Start.Column); // the stray '.'
    }

    // -----------------------------------------------------------------------
    // Row 8 — call arguments (ParseCallArguments, terminator RightParen)
    // -----------------------------------------------------------------------

    [Fact]
    public void CallArgs_SingleLine_TrailingComma_ParsesToSameAst() {
        Expression e = ExprOf(ParseOk("add(1, 2,)\n"));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal(2, c.Arguments.Count);
    }

    [Fact]
    public void CallArgs_MultiLine_NoTrailingComma_Parses() {
        Expression e = ExprOf(ParseOk("""
            add(
                1,
                2
            )
            """));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal(2, c.Arguments.Count);
    }

    [Fact]
    public void CallArgs_MultiLine_TrailingComma_ParsesToSameAst() {
        Expression e = ExprOf(ParseOk("""
            add(
                1,
                2,
            )
            """));
        CallExpr c = Assert.IsType<CallExpr>(e);
        Assert.Equal(2, c.Arguments.Count);
    }

    [Fact]
    public void CallArgs_DoubledComma_StillE2001AtElementPosition() {
        (_, DiagnosticBag bag) = Parse("foo(1,, 2)\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("unexpected token ',' — expected expression", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(7, d.Range.Start.Column); // the second ','
    }

    // -----------------------------------------------------------------------
    // Row 11 — lambda parameters (ParseLambda, terminator RightParen)
    // -----------------------------------------------------------------------

    [Fact]
    public void LambdaParams_SingleLine_TrailingComma_ParsesToSameAst() {
        Expression e = ExprOf(ParseOk("(a: int, b: int,) => a + b\n"));
        LambdaExpr l = Assert.IsType<LambdaExpr>(e);
        Assert.Equal(2, l.Parameters.Count);
        Assert.Equal("a", l.Parameters[0].Name);
        Assert.Equal("b", l.Parameters[1].Name);
    }

    [Fact]
    public void LambdaParams_MultiLine_NoTrailingComma_Parses() {
        CompilationUnit unit = ParseOk("""
            f := (
                a: int,
                b: int
            ) => a + b
            """);
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        LambdaExpr l = Assert.IsType<LambdaExpr>(decl.Initializer);
        Assert.Equal(2, l.Parameters.Count);
    }

    [Fact]
    public void LambdaParams_MultiLine_TrailingComma_ParsesToSameAst() {
        CompilationUnit unit = ParseOk("""
            f := (
                a: int,
                b: int,
            ) => a + b
            """);
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        LambdaExpr l = Assert.IsType<LambdaExpr>(decl.Initializer);
        Assert.Equal(2, l.Parameters.Count);
        Assert.Equal("a", l.Parameters[0].Name);
        Assert.Equal("b", l.Parameters[1].Name);
    }

    [Fact]
    public void LambdaParams_DoubledComma_StillE2001() {
        (_, DiagnosticBag bag) = Parse("f := (a: int,, b: int) => a + b\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected lambda parameter name", d.Message);
    }

    // -----------------------------------------------------------------------
    // Row 4 — for k, v in — a fixed pair, not a list; stays rejecting (D-421 unchanged)
    // -----------------------------------------------------------------------

    [Fact]
    public void ForIn_ThirdComma_StillE2001AtTheComma() {
        (_, DiagnosticBag bag) = Parse("arr := [1, 2]\nfor i, v, in arr { }\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("expected 'in' in for-loop header", d.Message);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(9, d.Range.Start.Column); // the second ','
    }

    // -----------------------------------------------------------------------
    // Rows 6, 7, 9, 10 — already-correct, regression pins (D-421 Decision 1)
    // -----------------------------------------------------------------------

    [Fact]
    public void Row6_AnonStructFieldInit_TrailingComma_UnchangedAccept() {
        CompilationUnit unit = ParseOk("s := #{ x: 1, y: 2, }\n");
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        AnonStructExpr anon = Assert.IsType<AnonStructExpr>(decl.Initializer);
        Assert.Equal(2, anon.Fields.Count);
    }

    [Fact]
    public void Row6_NamedStructConstruction_TrailingComma_UnchangedAccept() {
        CompilationUnit unit = ParseOk("p := Point { x: 1, y: 2, }\n");
        VarDeclStmt decl = Single<VarDeclStmt>(unit);
        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(decl.Initializer);
        Assert.Equal(2, sc.Fields.Count);
    }

    [Fact]
    public void Row7_SwitchArms_TrailingComma_UnchangedAccept() {
        // See also SwitchExprParserTests.TrailingCommaAfterFinalArm_IsAccepted —
        // this pin lives alongside the rest of this increment's row table for
        // discoverability.
        Expression e = ExprOf(ParseOk("n switch { 1 => 10, _ => 0, }\n"));
        SwitchExprNode sw = Assert.IsType<SwitchExprNode>(e);
        Assert.Equal(2, sw.Arms.Count);
    }

    [Fact]
    public void Row9_ArrayLiteral_TrailingComma_UnchangedAccept() {
        Expression e = ExprOf(ParseOk("[1, 2, 3,]\n"));
        ArrayLiteralExpr arr = Assert.IsType<ArrayLiteralExpr>(e);
        Assert.Equal(3, arr.Elements.Count);
    }

    [Fact]
    public void Row10_MapLiteral_TrailingComma_UnchangedAccept() {
        // See also ParserMapLiteralTests.SingleLineMapLiteral_WithTrailingComma_Parses —
        // this pin lives alongside the rest of this increment's row table too.
        Expression e = ExprOf(ParseOk("map<string, int>{ \"a\": 1, \"b\": 2, }\n"));
        MapLiteralExpr map = Assert.IsType<MapLiteralExpr>(e);
        Assert.Equal(2, map.Entries.Count);
    }

    // -----------------------------------------------------------------------
    // Leading and doubled commas — E2001 by decision (D-421 Decision 2), not omission
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayLiteral_LeadingComma_StillE2001AtElementPosition() {
        (_, DiagnosticBag bag) = Parse("[, 1]\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("unexpected token ',' — expected expression", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(2, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // E2209 — grouping-paren stray comma and empty-argument-list-with-comma
    // -----------------------------------------------------------------------

    [Fact]
    public void GroupingParen_StrayComma_IsE2209() {
        (_, DiagnosticBag bag) = Parse("x := (1,)\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2209", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(8, d.Range.Start.Column); // the ','
    }

    [Fact]
    public void GroupingParen_MultipleCommas_IsE2209AtFirst() {
        // Grob has no tuples: '(1, 2)' is not a two-element tuple, it is a grouping
        // whose single expression is followed by a stray comma — the same mistake
        // as '(1,)', reported at the first comma rather than parsing '2' as if it
        // meant something.
        (_, DiagnosticBag bag) = Parse("x := (1, 2)\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2209", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(8, d.Range.Start.Column);
    }

    [Fact]
    public void EmptyArgumentList_LoneComma_IsE2209() {
        (_, DiagnosticBag bag) = Parse("foo(,)\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2209", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(5, d.Range.Start.Column); // the ','
    }

    [Fact]
    public void ArgumentList_LeadingCommaBeforeRealArgument_StaysE2001NotE2209() {
        // 'foo(, 1)' is not "an argument list whose only content is a comma" — an
        // argument follows the comma, so this is the ordinary leading-comma mistake
        // (an element is missing before it), not the E2209 empty-list case.
        (_, DiagnosticBag bag) = Parse("foo(, 1)\n");
        Diagnostic d = Assert.Single(bag.Diagnostics);
        Assert.Equal("E2001", d.Code);
        Assert.Equal("unexpected token ',' — expected expression", d.Message);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(5, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Documented examples — verification only, not authoring (D-421 lists these
    // as already-correct target state in grob-language-fundamentals.md §16 and
    // grob-formatter-specification.md §3.2/§3.11/§6).
    // -----------------------------------------------------------------------

    [Fact]
    public void LanguageFundamentals16_FullWorkedExample_Parses() {
        // grob-language-fundamentals.md §16 — every comma-separated list form in
        // one snippet, each carrying a trailing comma. Undefined identifiers
        // (Repo, table, check, code, status) are a type-checker concern, not a
        // parser one — ParseOk exercises the parser layer only.
        ParseOk("""
            items := [1, 2, 3,]                        // array literal
            r := Repo { name: "grob", url: "...", }    // named struct construction
            a := #{ name: "grob", stars: 1200, }       // anonymous struct literal
            m := map<string, string>{
                "key": "value",
            }                                          // map literal
            fn foo(a: int, b: int,): int { }           // function parameters
            foo(1, 2,)                                 // function arguments
            f := (a: int, b: int,) => a + b            // lambda parameters
            h: fn(int, string,): bool := check         // function-type parameters
            rows := table.mapAs<Employee,>()           // type arguments
            select (code) {
                case 200, 201, { print("ok") }         // case pattern list
            }
            label := status switch {
                200 => "ok",
                _   => "other",                        // switch-expression arms
            }
            """);
    }

    [Fact]
    public void FormatterSpec32_WrappedCallExample_Parses() {
        // grob-formatter-specification.md §3.2 — the canonical wrap example.
        ParseOk("""
            result := someFunction(
                a_very_long_argument,
                another_long_one,
                yet_another,
                plus_one_more,
            )
            """);
    }

    [Fact]
    public void FormatterSpec311_FourParameterSignature_Parses() {
        // grob-formatter-specification.md §3.11 — the four-parameter signature
        // ending 'headers: ... = #{},'.
        ParseOk("""
            fn send_request(
                url:     string,
                method:  string              = "GET",
                timeout: int                 = 30,
                headers: map<string, string> = #{},
            ): Response { }
            """);
    }

    [Fact]
    public void FormatterSpec6_WorkedExampleOutput_Parses() {
        // grob-formatter-specification.md §6 — the worked example's formatter
        // output. Decisive per D-421: a call whose single argument wraps is
        // still a multi-line argument list, and that one argument takes the
        // trailing comma.
        Expression e = ExprOf(ParseOk("""
            print(
                issues
                    .filter(i => date.parse(i.created_at) < cutoff)
                    .select(i => #{
                        number: i.number,
                        title:  i.title,
                        age:    date.parse(i.created_at).daysUntil(date.today()),
                        author: i.user.login,
                    })
                    .formatAs.table(),
            )
            """));
        CallExpr print = Assert.IsType<CallExpr>(e);
        Assert.Single(print.Arguments);
    }
}
