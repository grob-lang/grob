using Grob.Compiler.Ast;
using Grob.Compiler.Ast.Declarations;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 9 Increment B — the <c>date</c> plugin type (D-354/D-355).
/// Covers signature-position type identity (distinct from other structs), the namespace-call
/// surface (D-342) for the seven static constructors, instance property/method dispatch
/// (including the arity/type-checked method arguments guid's all-zero-arity family never
/// needed), the <c>&lt;</c>/<c>&gt;</c>/<c>&lt;=</c>/<c>&gt;=</c> date-vs-date comparison gate
/// (D-354's <c>LessDate</c>/<c>GreaterDate</c> authorisation) and §3.1.1.
/// </summary>
public sealed class TypeCheckerDateTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

    // -----------------------------------------------------------------------
    // Signature-position type identity — date is Struct, distinct from string.
    // -----------------------------------------------------------------------

    [Fact]
    public void DateParameter_Annotation_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn describe(d: date): date {
                return d
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void DateField_Annotation_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            type Event {
                occurredAt: date
            }
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void DateAnnotatedBinding_AssignedString_ReportsSingleE0001() {
        DiagnosticBag bag = Check("""d: date := "not-a-date" """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0001.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(12, diag.Range.Start.Column);
    }

    [Fact]
    public void DateParameter_AssignedUnrelatedStruct_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            fn take(d: date): void {}
            c := Config { host: "example.com" }
            take(c)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(6, diag.Range.Start.Column);
    }

    [Fact]
    public void DateValue_ComparedToString_ReportsSingleE0002() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly r := d == "not-a-date"
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void DateValue_ComparedToDate_Equality_ResolvesToBool_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            a := date.now()
            b := date.now()
            readonly r := a == b
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Namespace-call surface (D-342) — date.now()/today()/of()/ofTime()/parse()/
    // fromUnixSeconds()/fromUnixMillis().
    // -----------------------------------------------------------------------

    [Fact]
    public void Now_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly d := date.now()");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Today_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly d := date.today()");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Of_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly d := date.of(2026, 4, 5)");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void OfTime_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly d := date.ofTime(2026, 4, 5, 14, 30, 0)");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Parse_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly d := date.parse("2026-04-05")""");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // parse()'s optional pattern argument (D-358) — a defaulted trailing
    // parameter on a namespace native. Proves CheckNativeCall's required/full
    // arity range: 1 argument (the pre-D-358 form) and 2 (input + pattern) are
    // both valid; 0 and 3 are E0003; a supplied pattern is still type-checked.
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_CallWithPattern_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("""readonly d := date.parse("05/04/2026", "dd/MM/yyyy")""");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Parse_ZeroArguments_ReportsSingleE0003WithRangeMessage() {
        DiagnosticBag bag = Check("readonly x := date.parse()");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal("'date.parse' expects between 1 and 2 arguments, but 0 were supplied.", diag.Message);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void Parse_ThreeArguments_ReportsSingleE0003WithRangeMessage() {
        DiagnosticBag bag = Check("""readonly x := date.parse("a", "b", "c")""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal("'date.parse' expects between 1 and 2 arguments, but 3 were supplied.", diag.Message);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void Parse_RegistryEntry_DoesNotDeclareVariadicElementType() {
        // Defaults and a variadic tail are different, mutually exclusive shapes
        // (NamespaceRegistry.NativeMember's doc comment) — a registry-authoring
        // invariant for the one entry that carries defaults this increment.
        var member = (NamespaceRegistry.NativeMember)NamespaceRegistry.TryGetMember("date", "parse")!;
        Assert.Null(member.VariadicElementType);
    }

    [Fact]
    public void Parse_PatternArgumentWrongType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""readonly x := date.parse("2026-04-05", 123)""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(
            "Argument 2 to 'date.parse' has type 'int', which is not assignable to parameter of type 'string'.",
            diag.Message);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(40, diag.Range.Start.Column);
    }

    [Fact]
    public void Parse_NamedArgument_ReportsSingleE0011() {
        // NativeMember carries no parameter names and emission preserves source order, so a
        // named argument cannot bind — it would otherwise slot "dd/MM/yyyy" into the input
        // parameter and let the pattern take its "" default (CodeRabbit review, PR #154).
        // Reject with E0011 (positional-only), matching RejectNamedPrimitiveArgs.
        DiagnosticBag bag = Check("""readonly x := date.parse(pattern: "dd/MM/yyyy")""");
        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0011.Code, diag.Code);
        Assert.Equal(
            "'date.parse' does not accept named arguments; pass them positionally.",
            diag.Message);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(26, diag.Range.Start.Column);
    }

    [Fact]
    public void FromUnixSeconds_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly d := date.fromUnixSeconds(0)");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void FromUnixMillis_Call_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("readonly d := date.fromUnixMillis(0)");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void DateAsValue_ReportsSingleE1004() {
        DiagnosticBag bag = Check("readonly x := date");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1004.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void UnknownNamespaceMember_ReportsSingleE1003() {
        DiagnosticBag bag = Check("readonly x := date.nope()");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1003.Code, diag.Code);
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Instance property dispatch — year/month/day/hour/minute/second/dayOfYear/
    // utcOffset (Int), dayOfWeek (String).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("year")]
    [InlineData("month")]
    [InlineData("day")]
    [InlineData("hour")]
    [InlineData("minute")]
    [InlineData("second")]
    [InlineData("dayOfYear")]
    [InlineData("utcOffset")]
    public void IntProperty_ResolvesToInt_NoDiagnostics(string property) {
        var (unit, bag) = TypeCheckSource($$"""
            d := date.now()
            readonly v := d.{{property}}
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        var ma = new MemberAccessCollector();
        ma.Visit(unit);
        MemberAccessExpr node = Assert.Single(ma.Nodes, n => n.Member == property);
        Assert.Equal(GrobType.Int, node.ResolvedFieldType);
    }

    [Fact]
    public void DayOfWeek_ResolvesToString_NoDiagnostics() {
        var (unit, bag) = TypeCheckSource("""
            d := date.now()
            readonly v := d.dayOfWeek
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        var ma = new MemberAccessCollector();
        ma.Visit(unit);
        MemberAccessExpr node = Assert.Single(ma.Nodes, n => n.Member == "dayOfWeek");
        Assert.Equal(GrobType.String, node.ResolvedFieldType);
    }

    [Fact]
    public void UnknownBareProperty_ReportsSingleE1002() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly v := d.nope
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Instance method dispatch — arity and per-argument type checked (unlike guid's
    // all-zero-arity family).
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("addDays")]
    [InlineData("addMonths")]
    [InlineData("addHours")]
    [InlineData("addMinutes")]
    public void AddUnitMethod_WithIntArgument_ResolvesToStruct_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            d := date.now()
            readonly next := d.{method}(1)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("addDays")]
    [InlineData("addMonths")]
    [InlineData("addHours")]
    [InlineData("addMinutes")]
    public void AddUnitMethod_NegativeArgument_ResolvesToStruct_NoDiagnostics(string method) {
        // D-354: subtraction is addX(-n) uniformly — no minusDays/minusMonths/etc.
        DiagnosticBag bag = Check($"""
            d := date.now()
            readonly prev := d.{method}(-1)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void AddDays_WrongArgumentType_ReportsSingleE0004() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly next := d.addDays("nope")
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(28, diag.Range.Start.Column);
    }

    [Fact]
    public void AddDays_NoArguments_ReportsSingleE0003() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly next := d.addDays()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0003.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(18, diag.Range.Start.Column);
    }

    [Fact]
    public void ChainedAddDays_ResultBoundLocal_ResolvesInstanceMemberAccess() {
        // _callResultStructNames threading — a :=-bound addDays() result must itself
        // resolve further date member access.
        DiagnosticBag bag = Check("""
            d := date.now()
            next := d.addDays(1)
            readonly y := next.year
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("toUtc")]
    [InlineData("toLocal")]
    [InlineData("toDateOnly")]
    [InlineData("toTimeOnly")]
    public void ZeroArgConversionMethod_ResolvesToStruct_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            d := date.now()
            readonly next := d.{method}()
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void ToZone_WithStringArgument_ResolvesToStruct_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly next := d.toZone("Europe/London")
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("isBefore")]
    [InlineData("isAfter")]
    public void OrderingMethod_WithDateArgument_ResolvesToBool_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            a := date.now()
            b := date.now()
            readonly r := a.{method}(b)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("isBefore", 26)]
    [InlineData("isAfter", 25)]
    public void OrderingMethod_WithNonDateArgument_ReportsSingleE0004(string method, int column) {
        DiagnosticBag bag = Check($"""
            a := date.now()
            readonly r := a.{method}(5)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    [Theory]
    [InlineData("isBefore", 26)]
    [InlineData("isAfter", 25)]
    public void OrderingMethod_WithUnrelatedStructArgument_ReportsSingleE0004(string method, int column) {
        DiagnosticBag bag = Check($$"""
            type Config {
                host: string
            }
            a := date.now()
            c := Config { host: "example.com" }
            readonly r := a.{{method}}(c)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0004.Code, diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(column, diag.Range.Start.Column);
    }

    [Theory]
    [InlineData("daysUntil")]
    [InlineData("daysSince")]
    public void IntervalMethod_WithDateArgument_ResolvesToInt_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            a := date.now()
            b := date.now()
            readonly n := a.{method}(b)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("toIso")]
    [InlineData("toIsoDateTime")]
    public void ZeroArgStringMethod_ResolvesToString_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            d := date.now()
            readonly s := d.{method}()
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void Format_WithStringArgument_ResolvesToString_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly s := d.format("dd MMM yyyy")
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Theory]
    [InlineData("toUnixSeconds")]
    [InlineData("toUnixMillis")]
    public void ZeroArgIntMethod_ResolvesToInt_NoDiagnostics(string method) {
        DiagnosticBag bag = Check($"""
            d := date.now()
            readonly n := d.{method}()
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void UnknownMethod_Call_ReportsSingleE1002() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly s := d.nope()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    [Fact]
    public void DateParameter_InstanceMemberAccess_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            fn describe(d: date): string {
                return d.toIso()
            }
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void NullableDateParameter_NonOptionalMethodCall_ReportsSingleE0101() {
        // CodeRabbit review, PR #143: ResolveMemberAccessCall (the method-CALL path) had
        // no nullable guard at all, unlike VisitMemberAccess's plain property-access path
        // — d.toIso() on a date? silently resolved Unknown instead of erroring E0101.
        DiagnosticBag bag = Check("""
            fn describe(d: date?): string {
                return d.toIso()
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0101.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(12, diag.Range.Start.Column);
    }

    [Fact]
    public void NullableDateParameter_OptionalMethodCall_ResolvesPermissively() {
        DiagnosticBag bag = Check("""
            fn describe(d: date?): void {
                s := d?.toIso()
            }
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // date-vs-date comparison — D-354, LessDate/GreaterDate authorisation.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("<=")]
    [InlineData(">=")]
    public void DateVsDate_RelationalOperator_ResolvesToBool_NoDiagnostics(string op) {
        DiagnosticBag bag = Check($"""
            a := date.now()
            b := date.now()
            readonly r := a {op} b
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void DirectDateCalls_NotStoredInBindings_LessThan_ResolvesToBool_NoDiagnostics() {
        // CodeRabbit review, PR #143: GetStructTypeName previously had no CallExpr arm,
        // so a direct struct-returning call not yet bound to a variable lost its nominal
        // date identity, failing IsDateStructPair and falling through to E0002 — the most
        // natural comparison shape (comparing two constructor calls directly) didn't work.
        DiagnosticBag bag = Check("readonly r := date.now() < date.today()");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void ChainedMethodCallResult_NotStoredInBinding_LessThan_ResolvesToBool_NoDiagnostics() {
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly r := d.addDays(1) < d.addDays(2)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void ChainedCallResult_DirectPropertyAccess_ResolvesWithNoDiagnostics() {
        // The same _callResultStructNames gap also blocked direct property access on a
        // call result with no intervening `:=` (d.addDays(1).year).
        DiagnosticBag bag = Check("""
            d := date.now()
            readonly y := d.addDays(1).year
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void DateVsInt_LessThan_ReportsSingleE0002() {
        DiagnosticBag bag = Check("""
            a := date.now()
            readonly r := a < 5
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // date-vs-date equality — D-357/D-367, the IsDateEquality annotation the
    // compiler's emission relies on to safely select EqualDate (see
    // BinaryExpr.IsDateEquality's doc comment). ResolveComparison's `==`/`!=` branch
    // stays permissive for any matching Struct pair (D-169 generic equality) — the
    // annotation is set alongside that, purely to tell the compiler which sites are
    // specifically nominal-date, never narrowing what type-checks.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    public void DateVsDate_EqualityOperator_SetsIsDateEqualityTrue(string op) {
        var (unit, bag) = TypeCheckSource($$"""
            a := date.now()
            b := date.now()
            readonly r := a {{op}} b
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.True(comparison.IsDateEquality);
    }

    // -----------------------------------------------------------------------
    // Nullable date? equality (CodeRabbit review, PR #157): date? is an
    // already-supported, already-tested type (NullableDateParameter_* below) — the
    // NullableStruct-vs-NullableStruct pair must also set IsDateEquality, or a
    // date?-vs-date? comparison silently falls back to the pre-D-357 offset-sensitive
    // generic Equal for every nullable-typed date. The mixed date-vs-date? pairing
    // (different GrobType tags) was already E0002 before this fix and stays that way —
    // unaffected, so no test needed for it here.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("==")]
    [InlineData("!=")]
    public void NullableDateVsNullableDate_EqualityOperator_SetsIsDateEqualityTrue(string op) {
        var (unit, bag) = TypeCheckSource($$"""
            fn f(a: date?, b: date?): bool {
                return a {{op}} b
            }
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.True(comparison.IsDateEquality);
    }

    [Fact]
    public void NullableDateVsNil_Equality_ResolvesToBool_ButIsDateEqualityFalse() {
        // x == nil (§20) is the nil-literal comparison, not a date-vs-date pair —
        // IsDateEquality must stay false so this never reaches EqualDate, which
        // cannot handle a bare nil operand (AsStruct() would fault).
        var (unit, bag) = TypeCheckSource("""
            fn f(a: date?): bool {
                return a == nil
            }
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.False(comparison.IsDateEquality);
    }

    [Fact]
    public void DateVsUnrelatedStruct_Equality_ResolvesToBool_ButIsDateEqualityFalse() {
        // D-169's generic struct equality still type-checks two mismatched struct
        // types (GrobType.Struct is a flat tag) — that permissiveness is unchanged.
        // IsDateEquality must stay false here so the compiler's emission does not
        // mistake this for a date-vs-date comparison.
        var (unit, bag) = TypeCheckSource("""
            type Config {
                host: string
            }
            a := date.now()
            c := Config { host: "example.com" }
            readonly r := a == c
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.False(comparison.IsDateEquality);
    }

    [Fact]
    public void UnrelatedStructVsUnrelatedStruct_Equality_IsDateEqualityFalse() {
        var (unit, bag) = TypeCheckSource("""
            type Config {
                host: string
            }
            a := Config { host: "a" }
            b := Config { host: "b" }
            readonly r := a == b
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.False(comparison.IsDateEquality);
    }

    [Fact]
    public void IntEquality_IsDateEqualityFalse() {
        var (unit, bag) = TypeCheckSource("readonly r := 1 == 2");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.False(comparison.IsDateEquality);
    }

    [Fact]
    public void DateVsDate_LessThan_IsDateEqualityFalse() {
        // IsDateEquality is an equality-only annotation — a relational comparison on
        // the same nominal-date pair must not set it (LessDate/GreaterDate selection
        // is unrelated, driven by the pre-existing IsDateStructPair-gated invariant).
        var (unit, bag) = TypeCheckSource("""
            a := date.now()
            b := date.now()
            readonly r := a < b
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");

        var binaries = new BinaryExprCollector();
        binaries.Visit(unit);
        BinaryExpr comparison = Assert.Single(binaries.Nodes);
        Assert.False(comparison.IsDateEquality);
    }

    [Fact]
    public void DateVsUnrelatedStruct_LessThan_ReportsSingleE0002() {
        DiagnosticBag bag = Check("""
            type Config {
                host: string
            }
            a := date.now()
            c := Config { host: "example.com" }
            readonly r := a < c
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E0002.Code, diag.Code);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(15, diag.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // §3.1.1 — every identifier node carries a non-null ResolvedType/Declaration.
    // -----------------------------------------------------------------------

    [Fact]
    public void DateNamespaceReceiver_AnnotatesWithoutErroring() {
        var (unit, _) = TypeCheckSource("readonly d := date.now()");

        var identifiers = new IdentifierCollector();
        identifiers.Visit(unit);
        IdentifierExpr dateRef = identifiers.Identifiers.First(i => i.Name == "date");
        Assert.NotNull(dateRef.Declaration);
        Assert.IsType<NamespaceDecl>(dateRef.Declaration);
    }

    // -----------------------------------------------------------------------
    // Regression — existing array/struct arms unaffected by the new date arms.
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayHigherOrderMethod_Unchanged_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("""
            arr := [1, 2, 3]
            arr.select(x => x)
            """);
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    [Fact]
    public void IntComparison_Unchanged_ResolvesWithNoDiagnostics() {
        DiagnosticBag bag = Check("readonly r := 1 < 2");
        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // -----------------------------------------------------------------------
    // Test helpers.
    // -----------------------------------------------------------------------

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

    private sealed class MemberAccessCollector : AstWalker {
        public List<MemberAccessExpr> Nodes { get; } = [];
        public override Unit VisitMemberAccess(MemberAccessExpr node) {
            Nodes.Add(node);
            Visit(node.Target);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private sealed class BinaryExprCollector : AstWalker {
        public List<BinaryExpr> Nodes { get; } = [];
        public override Unit VisitBinary(BinaryExpr node) {
            Nodes.Add(node);
            Visit(node.Left);
            Visit(node.Right);
            return default;
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }
}
