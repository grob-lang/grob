using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for Sprint 7 Increment A — the <c>GrobError</c> hierarchy
/// registered as built-in nominal types. Construction is exercised through
/// <c>readonly x := Leaf { ... }</c> rather than <c>throw</c>, so these tests do
/// not depend on the throw-specific type-check landing first.
/// </summary>
public sealed class TypeCheckerExceptionHierarchyTests {
    private static DiagnosticBag Check(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return bag;
    }

    // -----------------------------------------------------------------------
    // Hierarchy names resolve by name — construction works for the root and
    // every leaf, exactly like a Sprint 6 user type.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("GrobError")]
    [InlineData("IoError")]
    [InlineData("NetworkError")]
    [InlineData("JsonError")]
    [InlineData("ProcessError")]
    [InlineData("NilError")]
    [InlineData("ArithmeticError")]
    [InlineData("IndexError")]
    [InlineData("ParseError")]
    [InlineData("LookupError")]
    [InlineData("RuntimeError")]
    public void HierarchyMember_ConstructsWithMessage_NoError(string typeName) {
        DiagnosticBag bag = Check($$"""
            readonly e := {{typeName}} { message: "x" }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void NetworkError_ConstructsWithStatusCode_NoError() {
        DiagnosticBag bag = Check("""
            readonly e := NetworkError { message: "timeout", statusCode: 504 }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // Construction reuses the Sprint 6B rules verbatim.
    // -----------------------------------------------------------------------

    [Fact]
    public void MissingRequiredMessage_EmitsE0103() {
        DiagnosticBag bag = Check("""
            readonly e := IoError { }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0103", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(15, d.Range.Start.Column);
    }

    [Fact]
    public void UnknownFieldName_EmitsE0012() {
        DiagnosticBag bag = Check("""
            readonly e := IoError { message: "x", typo: 1 }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0012", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(39, d.Range.Start.Column);
    }

    [Fact]
    public void UserSuppliedLocation_AcceptedAtCheckerButRuntimeOverwritten() {
        // The checker permits 'location' in the initialiser (nameable field) but
        // does not enforce or use the supplied value — the runtime stamps it
        // unconditionally when Throw executes (VirtualMachine.cs).
        DiagnosticBag bag = Check("""
            readonly e := IoError { message: "x", location: "wherever" }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    // -----------------------------------------------------------------------
    // §3.1.1 invariant — every identifier node carries a non-null ResolvedType
    // and Declaration.
    // -----------------------------------------------------------------------

    private sealed class IdentifierCollector : AstWalker {
        public List<IdentifierExpr> Identifiers { get; } = [];
        public override Unit VisitIdentifier(IdentifierExpr node) { Identifiers.Add(node); return default; }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    [Fact]
    public void HierarchyConstruction_IdentifierNodes_HaveNonNullResolvedTypeAndDeclaration() {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan("""
            readonly msg := "boom"
            readonly e := IoError { message: msg }
            """, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);

        IdentifierCollector collector = new();
        collector.Visit(unit);

        foreach (IdentifierExpr id in collector.Identifiers) {
            Assert.True(id.ResolvedType != GrobType.Unknown || bag.HasErrors,
                $"Identifier '{id.Name}' at {id.Range} has UnknownType after type-check.");
            Assert.NotSame(UnresolvedDecl.Instance, id.Declaration);
            Assert.NotNull(id.Declaration);
        }
    }

    // -----------------------------------------------------------------------
    // Collision safety — a user 'type' declaration reusing a hierarchy name is a
    // real E1102 redeclaration, not a silent shadow (the TypeDecl pass-1 guard
    // fix this increment adds).
    // -----------------------------------------------------------------------

    [Fact]
    public void UserTypeDecl_ReusingHierarchyName_EmitsE1102() {
        DiagnosticBag bag = Check("""
            type IoError {
            x: int
            }
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E1102", d.Code);
        Assert.Equal(1, d.Range.Start.Line);
        Assert.Equal(1, d.Range.Start.Column);
    }

    // -----------------------------------------------------------------------
    // Nominal identity — a hierarchy subtype (a leaf, or the root itself) is
    // assignable to any of its ancestors (fix/compiler-struct-nominal-identity),
    // mirroring the throw/catch subtype-matching semantics (D-284) that
    // TypeChecker.Statements.cs's throw check and TypeChecker.ControlFlow.cs's
    // catch check already implement directly via ExceptionHierarchy.IsSubtypeOf.
    // Nominal identity (IsStructNominalMismatch) must not reject this — it
    // applies only *across* the hierarchy (an unrelated struct), never *within*
    // it (a leaf assigned to its own root, or to itself).
    // -----------------------------------------------------------------------

    [Fact]
    public void Parameter_LeafAssignedToGrobErrorRoot_NoError() {
        DiagnosticBag bag = Check("""
            fn take(e: GrobError): void {}
            take(IoError { message: "x" })
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Binding_LeafAssignedToGrobErrorRoot_NoError() {
        DiagnosticBag bag = Check("""
            e: GrobError := IoError { message: "x" }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Return_LeafAssignedToGrobErrorRoot_NoError() {
        DiagnosticBag bag = Check("""
            fn f(): GrobError { return IoError { message: "x" } }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void FieldValue_LeafAssignedToGrobErrorRootField_NoError() {
        DiagnosticBag bag = Check("""
            type Wrapper { err: GrobError }
            readonly w := Wrapper { err: IoError { message: "x" } }
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Parameter_RootAssignedToItself_NoError() {
        // Reflexive case — a GrobError value assigned to a GrobError-typed slot is
        // the exact-name path, not the subtype path, but exercised here alongside
        // the subtype cases for completeness.
        DiagnosticBag bag = Check("""
            fn take(e: GrobError): void {}
            take(GrobError { message: "x" })
            """);

        Assert.False(bag.HasErrors,
            $"unexpected: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void Parameter_SiblingLeaf_NotSubtype_EmitsE0004() {
        // NetworkError and JsonError are both direct children of GrobError, not of
        // each other — sibling leaves, not root-vs-leaf. Must still be rejected;
        // hierarchy membership alone must not short-circuit the nominal check.
        DiagnosticBag bag = Check("""
            fn take(e: JsonError): void {}
            take(NetworkError { message: "x" })
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void Parameter_GrobErrorRootAssignedUnrelatedStruct_EmitsE0004() {
        // 'GrobError' is a hierarchy member but 'Config' is not — hierarchy
        // membership on only one side must not accidentally short-circuit the
        // mismatch check.
        DiagnosticBag bag = Check("""
            type Config { host: string }
            fn take(e: GrobError): void {}
            take(Config { host: "example.com" })
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(3, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }

    [Fact]
    public void Parameter_LeafParameterAssignedRoot_EmitsE0004() {
        // The relationship is directional: the root is NOT a subtype of a leaf,
        // so a GrobError value may not be passed where a specific leaf (IoError)
        // is declared — only the reverse (leaf-to-root) is permitted.
        DiagnosticBag bag = Check("""
            fn take(e: IoError): void {}
            take(GrobError { message: "x" })
            """);

        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal("E0004", d.Code);
        Assert.Equal(2, d.Range.Start.Line);
        Assert.Equal(6, d.Range.Start.Column);
    }
}
