using System.Reflection;

using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-403 — pins the exact expression-node-kind set each side-channel type-resolution
/// helper (<c>ExpressionDescriptor</c>, <c>ArrayDescriptorOf</c>, <c>MapDescriptorOf</c>,
/// <c>GetStructTypeName</c>) currently handles, invoked directly via reflection against a
/// real, fully-checked <see cref="TypeChecker"/> instance so a future silent narrowing (an
/// arm quietly deleted) or a new helper added without the matching <c>GroupingExpr</c>/
/// <c>BinaryExpr {Operator: NilCoalesce}</c> convention fails a test here rather than
/// surfacing only as a user-facing regression. Deliberately does NOT assert every helper
/// handles the same set — <c>MapDescriptorOf</c>'s lack of a <c>CallExpr</c> arm is by
/// design (no map-returning native exists to carry a descriptor through), pinned as a
/// "not handled" case rather than treated as a gap.
/// </summary>
/// <remarks>
/// Mutation-verified during development (D-403): the <c>BinaryExpr {Operator: NilCoalesce}</c>
/// arm was temporarily deleted from <c>ArrayDescriptorOf</c>, confirmed
/// <see cref="ArrayDescriptorOf_NilCoalesce_ArrayLeft_ReturnsElementDescriptor"/> failed for
/// the expected reason (a <see langword="null"/> result where a descriptor was expected),
/// then the arm was restored and the test re-confirmed green — proving this pinning
/// mechanism actually catches the regression it claims to catch, not merely asserting
/// engineering intent.
/// </remarks>
public sealed class SideChannelHelperExhaustivenessTests {
    private static (TypeChecker Checker, CompilationUnit Unit, DiagnosticBag Diagnostics) CheckKeepInstance(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag);
        checker.Check(unit);
        return (checker, unit, bag);
    }

    private static string FormatErrors(DiagnosticBag bag) =>
        string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"));

    /// <summary>
    /// Invokes a private <see cref="TypeChecker"/> helper by name via reflection against
    /// an already-checked instance (so its backing dictionaries/symbol table are
    /// populated) — the only way to exercise these deliberately-private, non-mechanised
    /// side channels directly rather than only through their externally-visible effects.
    /// Throws (failing the calling test with a clear cause) if the named method no longer
    /// exists, so a rename silently breaking this pin is itself caught.
    /// </summary>
    private static object? Invoke(TypeChecker checker, string methodName, Expression arg) {
        MethodInfo method = typeof(TypeChecker).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"TypeChecker.{methodName} not found — this exhaustiveness pin is stale " +
                "(the helper was renamed or removed without updating this test).");
        return method.Invoke(checker, [arg]);
    }

    /// <summary>Collects every expression-node kind the four helpers under test switch on.</summary>
    private sealed class NodeCollector : AstWalker {
        public List<Expression> Nodes { get; } = [];
        public override Unit VisitBinary(BinaryExpr node) { Nodes.Add(node); return base.VisitBinary(node); }
        public override Unit VisitGrouping(GroupingExpr node) { Nodes.Add(node); return base.VisitGrouping(node); }
        public override Unit VisitCall(CallExpr node) { Nodes.Add(node); return base.VisitCall(node); }
        public override Unit VisitIdentifier(IdentifierExpr node) { Nodes.Add(node); return base.VisitIdentifier(node); }
        public override Unit VisitLambda(LambdaExpr node) { Nodes.Add(node); return base.VisitLambda(node); }
        public override Unit VisitArrayLiteral(ArrayLiteralExpr node) { Nodes.Add(node); return base.VisitArrayLiteral(node); }
        public override Unit VisitMemberAccess(MemberAccessExpr node) { Nodes.Add(node); return base.VisitMemberAccess(node); }
        public override Unit VisitIndex(IndexExpr node) { Nodes.Add(node); return base.VisitIndex(node); }
        public override Unit VisitMapLiteral(MapLiteralExpr node) { Nodes.Add(node); return base.VisitMapLiteral(node); }
        public override Unit VisitStructConstruction(StructConstructionExpr node) { Nodes.Add(node); return base.VisitStructConstruction(node); }
        public override Unit VisitAnonStruct(AnonStructExpr node) { Nodes.Add(node); return base.VisitAnonStruct(node); }
        public override Unit VisitIntLiteral(IntLiteralExpr node) { Nodes.Add(node); return base.VisitIntLiteral(node); }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static List<Expression> Collect(CompilationUnit unit) {
        NodeCollector collector = new();
        collector.Visit(unit);
        return collector.Nodes;
    }

    // =========================================================================
    // ExpressionDescriptor (TypeChecker.cs) — the function-descriptor side channel.
    // Handled: LambdaExpr, CallExpr, IdentifierExpr, GroupingExpr,
    // BinaryExpr{NilCoalesce}. Not handled: everything else (IntLiteralExpr pinned).
    // =========================================================================

    [Fact]
    public void ExpressionDescriptor_LambdaExpr_ReturnsDescriptor() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "f: fn(): int := () => 1\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        LambdaExpr lambda = Assert.IsType<LambdaExpr>(Collect(unit).Single(n => n is LambdaExpr));
        Assert.NotNull(Invoke(checker, "ExpressionDescriptor", lambda));
    }

    [Fact]
    public void ExpressionDescriptor_CallExpr_ReturnsDescriptor() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            fn makeAdder(): fn(): int {
                return () => 42
            }
            y := makeAdder()
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        CallExpr call = Assert.IsType<CallExpr>(Collect(unit).Single(n => n is CallExpr));
        Assert.NotNull(Invoke(checker, "ExpressionDescriptor", call));
    }

    [Fact]
    public void ExpressionDescriptor_IdentifierExpr_ReturnsDescriptor() {
        // ExpressionDescriptor's IdentifierExpr arm resolves purely via LookupSymbol,
        // which only searches the live scope stack — TypeChecker.Check pops every scope
        // (including the global one) before returning, so this arm cannot be pinned by
        // reflecting on the checker post-Check the way the dictionary-backed arms above
        // can. Proven instead through its own observable, real consumer (VisitCall's
        // fallback for a non-FnDecl callee, TypeChecker.Expressions.cs) — a call through
        // a bare function-typed identifier resolves its declared return type onto
        // CallExpr.ResolvedReturnType, a node-attached field that survives Check().
        (TypeChecker _, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "f: fn(): int := () => 1\ny := f()\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        CallExpr call = Assert.IsType<CallExpr>(Collect(unit).Single(n => n is CallExpr));
        Assert.Equal(GrobType.Int, call.ResolvedReturnType);
    }

    [Fact]
    public void ExpressionDescriptor_GroupingExpr_ReturnsDescriptor() {
        // Wraps a LambdaExpr directly (not an identifier) so this pin exercises only
        // GroupingExpr's own recursive unwrap, not the separately-pinned IdentifierExpr
        // arm's scope-dependent lookup.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "y := (() => 1)\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        GroupingExpr grouping = Assert.IsType<GroupingExpr>(Collect(unit).Single(n => n is GroupingExpr));
        Assert.NotNull(Invoke(checker, "ExpressionDescriptor", grouping));
    }

    [Fact]
    public void ExpressionDescriptor_NilCoalesce_ReturnsDescriptor() {
        // D-403's new arm. Both operands are bare lambdas (not identifiers) for the same
        // scope-independence reason as the GroupingExpr case above.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "y := ((() => 1) ?? (() => 2))\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        BinaryExpr binary = Assert.IsType<BinaryExpr>(
            Collect(unit).Single(n => n is BinaryExpr { Operator: BinaryOperator.NilCoalesce }));
        Assert.NotNull(Invoke(checker, "ExpressionDescriptor", binary));
    }

    [Fact]
    public void ExpressionDescriptor_IntLiteralExpr_IsNotHandled() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("n := 5\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        IntLiteralExpr literal = Assert.IsType<IntLiteralExpr>(Collect(unit).Single(n => n is IntLiteralExpr));
        Assert.Null(Invoke(checker, "ExpressionDescriptor", literal));
    }

    // =========================================================================
    // ArrayDescriptorOf (TypeChecker.cs) — the array-element-type side channel.
    // Handled: ArrayLiteralExpr, CallExpr, MemberAccessExpr, IdentifierExpr,
    // GroupingExpr, BinaryExpr{NilCoalesce}, IndexExpr.
    // =========================================================================

    [Fact]
    public void ArrayDescriptorOf_ArrayLiteralExpr_ReturnsDescriptor() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "xs := [1, 2, 3]\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        ArrayLiteralExpr literal = Assert.IsType<ArrayLiteralExpr>(Collect(unit).Single(n => n is ArrayLiteralExpr));
        Assert.NotNull(Invoke(checker, "ArrayDescriptorOf", literal));
    }

    [Fact]
    public void ArrayDescriptorOf_CallExpr_ReturnsDescriptor() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            fn makeArray(): int[] {
                return [9, 9, 9]
            }
            y := makeArray()
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        CallExpr call = Assert.IsType<CallExpr>(Collect(unit).Single(n => n is CallExpr));
        Assert.NotNull(Invoke(checker, "ArrayDescriptorOf", call));
    }

    [Fact]
    public void ArrayDescriptorOf_MemberAccessExpr_ReturnsDescriptor() {
        // Map 'keys'/'values' (C0b-2a, D-377) are the one property access whose own
        // result is array-typed.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            fn f(m: map<string, int>): void {
                ks := m.keys
            }
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        MemberAccessExpr access = Assert.IsType<MemberAccessExpr>(
            Collect(unit).Single(n => n is MemberAccessExpr { Member: "keys" }));
        Assert.NotNull(Invoke(checker, "ArrayDescriptorOf", access));
    }

    [Fact]
    public void ArrayDescriptorOf_IdentifierExpr_ReturnsDescriptor() {
        // ArrayDescriptorOf's IdentifierExpr arm resolves purely via LookupSymbol, which
        // only searches the live scope stack — Check() pops every scope before
        // returning, so this arm cannot be pinned by reflecting post-Check the way the
        // dictionary/node-field-backed arms above can (see the identical note on
        // ExpressionDescriptor's IdentifierExpr pin). Proven instead through VisitIndex's
        // own real consumption of it (TypeChecker.Expressions.cs): indexing an
        // identifier bound to an array threads the element type onto
        // IndexExpr.ElementType, a node-attached field that survives Check().
        (TypeChecker _, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "xs: int[] := [1, 2, 3]\nys := xs\nfirst := ys[0]\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        IndexExpr index = Assert.IsType<IndexExpr>(Collect(unit).Single(n => n is IndexExpr));
        Assert.Equal(GrobType.Int, index.ElementType);
    }

    [Fact]
    public void ArrayDescriptorOf_GroupingExpr_ReturnsDescriptor() {
        // Wraps an ArrayLiteralExpr directly (not an identifier) — same
        // scope-independence reason as ExpressionDescriptor's GroupingExpr pin.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "y := ([1, 2, 3])\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        GroupingExpr grouping = Assert.IsType<GroupingExpr>(Collect(unit).Single(n => n is GroupingExpr));
        Assert.NotNull(Invoke(checker, "ArrayDescriptorOf", grouping));
    }

    [Fact]
    public void ArrayDescriptorOf_NilCoalesce_ArrayLeft_ReturnsElementDescriptor() {
        // D-402's restored arm — the mutation-verified case (see class remarks). Both
        // operands are bare array literals (not identifiers), same reason as the
        // GroupingExpr case above.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "y := ([1, 2, 3] ?? [4, 5])\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        BinaryExpr binary = Assert.IsType<BinaryExpr>(
            Collect(unit).Single(n => n is BinaryExpr { Operator: BinaryOperator.NilCoalesce }));
        Assert.NotNull(Invoke(checker, "ArrayDescriptorOf", binary));
    }

    [Fact]
    public void ArrayDescriptorOf_IndexExpr_ReturnsDescriptor() {
        // The index target is a nested array literal directly (not an identifier) so
        // this pin exercises only IndexExpr's own recursive ElementArrayDescriptor
        // chase, not the separately-pinned IdentifierExpr arm's scope-dependent lookup.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "first := [[1, 2], [3, 4]][0]\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        IndexExpr index = Assert.IsType<IndexExpr>(Collect(unit).Single(n => n is IndexExpr));
        Assert.NotNull(Invoke(checker, "ArrayDescriptorOf", index));
    }

    [Fact]
    public void ArrayDescriptorOf_StructConstructionExpr_IsNotHandled() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            type Point {
                x: int
            }
            p := Point { x: 1 }
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(
            Collect(unit).Single(n => n is StructConstructionExpr));
        Assert.Null(Invoke(checker, "ArrayDescriptorOf", sc));
    }

    // =========================================================================
    // MapDescriptorOf (TypeChecker.cs) — the map-value-type side channel.
    // Handled: IdentifierExpr, MapLiteralExpr, GroupingExpr, BinaryExpr{NilCoalesce}.
    // Deliberately NOT handled: CallExpr — no map-returning native exists to carry a
    // value descriptor through, so no arm is registered (a design choice, not a gap).
    // =========================================================================

    [Fact]
    public void MapDescriptorOf_MapLiteralExpr_ReturnsDescriptor() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "m := map<string, int>{}\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        MapLiteralExpr literal = Assert.IsType<MapLiteralExpr>(Collect(unit).Single(n => n is MapLiteralExpr));
        Assert.NotNull(Invoke(checker, "MapDescriptorOf", literal));
    }

    [Fact]
    public void MapDescriptorOf_IdentifierExpr_ReturnsDescriptor() {
        // MapDescriptorOf's IdentifierExpr arm resolves purely via LookupSymbol, which
        // only searches the live scope stack — Check() pops every scope before
        // returning, so this arm cannot be pinned by reflecting post-Check the way the
        // dictionary/node-field-backed arms above can (see the identical note on
        // ExpressionDescriptor's IdentifierExpr pin). Proven instead through VisitIndex's
        // own real consumption of it: indexing an identifier bound to a map threads the
        // value type onto IndexExpr.ElementType, a node-attached field that survives
        // Check().
        (TypeChecker _, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "m: map<string, int> := map<string, int>{}\nn := m\nv := n[\"k\"]\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        IndexExpr index = Assert.IsType<IndexExpr>(Collect(unit).Single(n => n is IndexExpr));
        Assert.Equal(GrobType.Int, index.ElementType);
    }

    [Fact]
    public void MapDescriptorOf_GroupingExpr_ReturnsDescriptor() {
        // Wraps a MapLiteralExpr directly (not an identifier) — same
        // scope-independence reason as ExpressionDescriptor's GroupingExpr pin.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "y := (map<string, int>{})\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        GroupingExpr grouping = Assert.IsType<GroupingExpr>(Collect(unit).Single(n => n is GroupingExpr));
        Assert.NotNull(Invoke(checker, "MapDescriptorOf", grouping));
    }

    [Fact]
    public void MapDescriptorOf_NilCoalesce_ReturnsDescriptor() {
        // D-401's original arm. Both operands are bare map literals (not identifiers),
        // same reason as the GroupingExpr case above.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "y := (map<string, int>{} ?? map<string, int>{})\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        BinaryExpr binary = Assert.IsType<BinaryExpr>(
            Collect(unit).Single(n => n is BinaryExpr { Operator: BinaryOperator.NilCoalesce }));
        Assert.NotNull(Invoke(checker, "MapDescriptorOf", binary));
    }

    [Fact]
    public void MapDescriptorOf_CallExpr_IsNotHandledByDesign() {
        // The documented divergence: unlike ArrayDescriptorOf, MapDescriptorOf has no
        // CallExpr arm at all — pinned here as intentional, not a residual gap.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            fn one(): int {
                return 1
            }
            y := one()
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        CallExpr call = Assert.IsType<CallExpr>(Collect(unit).Single(n => n is CallExpr));
        Assert.Null(Invoke(checker, "MapDescriptorOf", call));
    }

    // =========================================================================
    // GetStructTypeName (TypeChecker.Expressions.cs) — the named-struct-type-identity
    // side channel. Handled: StructConstructionExpr, AnonStructExpr, IdentifierExpr,
    // MemberAccessExpr, CallExpr, GroupingExpr, BinaryExpr{NilCoalesce}.
    // =========================================================================

    [Fact]
    public void GetStructTypeName_StructConstructionExpr_ReturnsName() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            type Point {
                x: int
            }
            p := Point { x: 1 }
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        StructConstructionExpr sc = Assert.IsType<StructConstructionExpr>(
            Collect(unit).Single(n => n is StructConstructionExpr));
        Assert.Equal("Point", Invoke(checker, "GetStructTypeName", sc));
    }

    [Fact]
    public void GetStructTypeName_AnonStructExpr_ReturnsSynthesisedName() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "a := #{ label: \"hi\" }\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        AnonStructExpr anon = Assert.IsType<AnonStructExpr>(Collect(unit).Single(n => n is AnonStructExpr));
        Assert.NotNull(Invoke(checker, "GetStructTypeName", anon));
    }

    [Fact]
    public void GetStructTypeName_IdentifierExpr_ReturnsName() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            type Point {
                x: int
            }
            p := Point { x: 1 }
            p2 := p
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        IdentifierExpr id = Assert.IsType<IdentifierExpr>(
            Collect(unit).Single(n => n is IdentifierExpr { Name: "p" }));
        Assert.Equal("Point", Invoke(checker, "GetStructTypeName", id));
    }

    [Fact]
    public void GetStructTypeName_MemberAccessExpr_ReturnsName() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            type Point {
                x: int
            }
            type Box {
                inner: Point
            }
            b := Box { inner: Point { x: 1 } }
            nested := b.inner
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        MemberAccessExpr access = Assert.IsType<MemberAccessExpr>(
            Collect(unit).Single(n => n is MemberAccessExpr { Member: "inner" }));
        Assert.Equal("Point", Invoke(checker, "GetStructTypeName", access));
    }

    [Fact]
    public void GetStructTypeName_CallExpr_ReturnsName() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance(
            "id := guid.newV4()\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        CallExpr call = Assert.IsType<CallExpr>(Collect(unit).Single(n => n is CallExpr));
        Assert.Equal("guid", Invoke(checker, "GetStructTypeName", call));
    }

    [Fact]
    public void GetStructTypeName_GroupingExpr_ReturnsName() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            type Point {
                x: int
            }
            p := Point { x: 1 }
            g := (p)
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        GroupingExpr grouping = Assert.IsType<GroupingExpr>(Collect(unit).Single(n => n is GroupingExpr));
        Assert.Equal("Point", Invoke(checker, "GetStructTypeName", grouping));
    }

    [Fact]
    public void GetStructTypeName_NilCoalesce_ReturnsName() {
        // D-403's new arm.
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("""
            g1: guid? := guid.newV4()
            g2 := guid.newV4()
            y := (g1 ?? g2)
            """);
        Assert.False(bag.HasErrors, FormatErrors(bag));

        BinaryExpr binary = Assert.IsType<BinaryExpr>(
            Collect(unit).Single(n => n is BinaryExpr { Operator: BinaryOperator.NilCoalesce }));
        Assert.Equal("guid", Invoke(checker, "GetStructTypeName", binary));
    }

    [Fact]
    public void GetStructTypeName_IntLiteralExpr_IsNotHandled() {
        (TypeChecker checker, CompilationUnit unit, DiagnosticBag bag) = CheckKeepInstance("n := 5\n");
        Assert.False(bag.HasErrors, FormatErrors(bag));

        IntLiteralExpr literal = Assert.IsType<IntLiteralExpr>(Collect(unit).Single(n => n is IntLiteralExpr));
        Assert.Null(Invoke(checker, "GetStructTypeName", literal));
    }
}
