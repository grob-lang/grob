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
}
