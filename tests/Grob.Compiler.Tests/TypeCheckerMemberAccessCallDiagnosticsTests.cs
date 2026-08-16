using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for D-408 — bringing <c>ResolveMemberAccessCall</c>'s diagnostic
/// behaviour into line with <c>VisitMemberAccess</c>'s (the property-access path), the
/// two remaining findings D-380 recorded but did not fix (finding 1 was already closed
/// by D-402; not covered here).
/// </summary>
/// <remarks>
/// <para>
/// Finding 2: an ordinary user <c>type</c> struct or an anonymous struct (<c>#{ }</c>)
/// has no method surface in v1 (D-043/D-080). A call through an unrecognised member
/// name used to type-check clean and crash the VM at emission; it now raises E1002,
/// the same code and wording <c>ResolveStructFieldAccess</c> already raises for the
/// property-access shape of the identical mistake. A call through a genuinely
/// declared field whose value happens to be callable (<c>box.callback()</c>) must stay
/// permissive — the field-lookup-based fix, not a blanket rejection, is what
/// distinguishes the two;
/// <see cref="MethodCall_ThroughFunctionTypedField_StaysPermissiveNoErrors"/>
/// pins it at the type-checker level and
/// <c>Sprint6IncrementCTests.ClosureInField_Integration_CallsAfterReturn</c> pins it
/// end to end, unmodified by this change.
/// </para>
/// <para>
/// Finding 3: a method call on an already-errored receiver (an undefined identifier)
/// used to resolve <see cref="GrobType.Unknown"/> instead of
/// <see cref="GrobType.Error"/>, so the one root cause cascaded into up to three
/// diagnostics at the call's consumption sites (<c>return</c>, <c>throw</c>, a typed
/// argument) — <see cref="GrobType.Unknown"/> has no universal excusal the way
/// <see cref="GrobType.Error"/> does (D-300). Mirroring
/// <c>VisitMemberAccess</c>'s <c>Error</c> arm collapses this back to one diagnostic
/// without swallowing a genuinely separate, later mistake.
/// </para>
/// </remarks>
public sealed class TypeCheckerMemberAccessCallDiagnosticsTests {
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

    private static List<MemberAccessExpr> CollectMemberAccesses(CompilationUnit unit) {
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // =========================================================================
    // Finding 2 — unrecognised method call on a user struct / anon-struct receiver
    // =========================================================================

    [Fact]
    public void MethodCall_UnrecognisedNameOnUserStructReceiver_EmitsE1002() {
        DiagnosticBag bag = Check("""
            type Point {
            x: int
            y: int
            }
            p := Point { x: 1, y: 2 }
            p.compute()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal("Type 'Point' has no member 'compute'.", diag.Message);
        Assert.Equal(6, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    [Fact]
    public void MethodCall_UnrecognisedNameOnAnonStructReceiver_EmitsE1002() {
        DiagnosticBag bag = Check("""
            p := #{ x: 1, y: 2 }
            p.compute()
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, diag.Code);
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(1, diag.Range.Start.Column);
    }

    // Regression guard: field access on the same structs (a recognised field) must
    // still resolve correctly — the exact regression a careless "any call on a
    // Struct/AnonStruct receiver is E1002" fix would introduce.

    [Fact]
    public void FieldAccess_OnStructAlsoUsedForUnrecognisedMethodCall_StillResolves() {
        var (unit, bag) = TypeCheckSource("""
            type Point {
            x: int
            y: int
            }
            p := Point { x: 1, y: 2 }
            readonly xVal := p.x
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    [Fact]
    public void FieldAccess_OnAnonStructAlsoUsedForUnrecognisedMethodCall_StillResolves() {
        var (unit, bag) = TypeCheckSource("""
            p := #{ x: 1, y: 2 }
            readonly xVal := p.x
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
        MemberAccessExpr ma = Assert.Single(CollectMemberAccesses(unit));
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    // Preserved by construction (not broken): a call through a declared field whose
    // value happens to be callable — the one fixture the breaking-change sweep found
    // (Sprint6IncrementCTests.ClosureInField_Integration_CallsAfterReturn, which this
    // test does not duplicate — it pins the same shape at the type-checker layer).

    [Fact]
    public void MethodCall_ThroughFunctionTypedField_StaysPermissiveNoErrors() {
        DiagnosticBag bag = Check("""
            type Box {
            callback: fn(): int
            }
            fn makeBox(): Box {
            n := 42
            f := () => n
            return Box { callback: f }
            }
            box := makeBox()
            result := box.callback()
            print(result)
            """);

        Assert.False(bag.HasErrors, $"unexpected: {FormatErrors(bag)}");
    }

    // =========================================================================
    // Finding 3 — Error-receiver cascade suppression on the call path
    // =========================================================================

    [Fact]
    public void MethodCallOnErroredReceiver_InThrow_EmitsSingleDiagnostic() {
        // Before D-408: E1001 (undefined identifier) + a spurious E0014, because the
        // call resolved Unknown rather than Error and VisitThrow's cascade check only
        // excuses Error.
        DiagnosticBag bag = Check("throw undefinedThing.compute()\n");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
        // 'undefinedThing' starts at column 7 — after "throw " (six characters).
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(7, diag.Range.Start.Column);
    }

    [Fact]
    public void MethodCallOnErroredReceiver_InReturn_EmitsSingleDiagnostic() {
        // Before D-408: E1001 + a spurious E0005, because VisitReturn's cascade check
        // only excuses Error, not Unknown.
        DiagnosticBag bag = Check("""
            fn f(): int {
            return undefinedThing.compute()
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
        // 'undefinedThing' starts at column 8 — after "return " (seven characters).
        Assert.Equal(2, diag.Range.Start.Line);
        Assert.Equal(8, diag.Range.Start.Column);
    }

    [Fact]
    public void MethodCallOnErroredReceiver_ArgumentToTypedParameter_EmitsExactlyTwoDiagnostics_NotThree() {
        // The narrow-scoping proof (investigation §1c's last row): one mistake
        // (undefinedThing) must not multiply into a spurious E0004 on the FIRST
        // argument while the genuinely separate, unrelated mistake on the SECOND
        // argument still surfaces. Before D-408 this was three diagnostics
        // (E1001 + spurious E0004 on 'a' + genuine E0004 on 'b'); after, exactly two.
        DiagnosticBag bag = Check("""
            fn take(a: int, b: string): void {
            }
            take(undefinedThing.compute(), 42)
            """);

        List<Diagnostic> errors = bag.Errors.ToList();
        Assert.Equal(2, errors.Count);
        Assert.Equal(ErrorCatalog.E1001.Code, errors[0].Code);
        // 'undefinedThing' starts at column 6 — after "take(" (five characters).
        Assert.Equal(3, errors[0].Range.Start.Line);
        Assert.Equal(6, errors[0].Range.Start.Column);
        Assert.Equal(ErrorCatalog.E0004.Code, errors[1].Code);
        Assert.Contains("'b'", errors[1].Message);
        // The genuine second-argument mistake stays located on '42' itself (column 32 —
        // after "take(" plus "undefinedThing.compute()" plus ", "), not on the call or on
        // the suppressed first argument: cascade suppression must not move a real
        // diagnostic off its own source text.
        Assert.Equal(3, errors[1].Range.Start.Line);
        Assert.Equal(32, errors[1].Range.Start.Column);
    }

    [Fact]
    public void MethodCallOnErroredReceiver_TypedAssignment_EmitsSingleDiagnostic() {
        // Before D-408: E1001 + a spurious E0001, because the assignability guard
        // only excuses Error, not Unknown.
        DiagnosticBag bag = Check("x: int := undefinedThing.compute()\n");

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, diag.Code);
        // 'undefinedThing' starts at column 11 — after "x: int := " (ten characters).
        Assert.Equal(1, diag.Range.Start.Line);
        Assert.Equal(11, diag.Range.Start.Column);
    }

    // =========================================================================
    // Arm-diff regression pin — the two dispatch paths (VisitMemberAccess's
    // DispatchMemberAccessProperty vs ResolveMemberAccessCall's
    // DispatchMemberAccessCall) must handle an Error receiver and an unresolvable
    // struct/anon-struct member identically. Asserted directly (not by accident) so a
    // future divergence between the two paths fails here rather than being found a
    // fourth time (D-380 recorded three; this is the durable record of the diff).
    // Mutation-verified during development: each new arm was temporarily removed,
    // the corresponding test below confirmed to fail for the expected reason
    // (extra diagnostics for the Error-receiver case; a missing E1002 that instead
    // reaches the generic Unknown fallback for the struct-member case), then restored.
    // =========================================================================

    [Fact]
    public void ArmParity_ErrorReceiver_PropertyAndCallPathsBothSuppressCascade() {
        DiagnosticBag propertyBag = Check("""
            fn f(): int {
            return undefinedThing.length
            }
            """);
        DiagnosticBag callBag = Check("""
            fn f(): int {
            return undefinedThing.compute()
            }
            """);

        Diagnostic propertyDiag = Assert.Single(propertyBag.Errors);
        Diagnostic callDiag = Assert.Single(callBag.Errors);
        Assert.Equal(ErrorCatalog.E1001.Code, propertyDiag.Code);
        Assert.Equal(ErrorCatalog.E1001.Code, callDiag.Code);
        // Both paths locate the one root cause on 'undefinedThing' itself — column 8,
        // after "return " — not on the member or the call. Parity of location is part of
        // the parity this test pins, so the two are asserted individually and against
        // each other.
        Assert.Equal(2, propertyDiag.Range.Start.Line);
        Assert.Equal(8, propertyDiag.Range.Start.Column);
        Assert.Equal(2, callDiag.Range.Start.Line);
        Assert.Equal(8, callDiag.Range.Start.Column);
        Assert.Equal(propertyDiag.Range.Start, callDiag.Range.Start);
    }

    [Fact]
    public void ArmParity_UnresolvableStructMember_PropertyAndCallPathsBothEmitE1002() {
        const string typeDecl = """
            type Point {
            x: int
            y: int
            }
            p := Point { x: 1, y: 2 }
            """;

        DiagnosticBag propertyBag = Check(typeDecl + "\nreadonly v := p.nosuchmember\n");
        DiagnosticBag callBag = Check(typeDecl + "\np.nosuchmember()\n");

        Diagnostic propertyDiag = Assert.Single(propertyBag.Errors);
        Diagnostic callDiag = Assert.Single(callBag.Errors);
        Assert.Equal(ErrorCatalog.E1002.Code, propertyDiag.Code);
        Assert.Equal(ErrorCatalog.E1002.Code, callDiag.Code);
        Assert.Equal(propertyDiag.Message, callDiag.Message);
        // Both diagnostics span the whole member access, so each starts at its own
        // receiver 'p' — column 15 on 'readonly v := p.nosuchmember', column 1 on the
        // bare 'p.nosuchmember()' statement. The columns differ because the source does;
        // the shared line is the sixth in both, after the five-line type declaration.
        Assert.Equal(6, propertyDiag.Range.Start.Line);
        Assert.Equal(15, propertyDiag.Range.Start.Column);
        Assert.Equal(6, callDiag.Range.Start.Line);
        Assert.Equal(1, callDiag.Range.Start.Column);
    }
}
