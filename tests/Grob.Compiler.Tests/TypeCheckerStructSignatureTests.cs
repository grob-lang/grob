using Grob.Compiler.Ast;
using Grob.Core;
using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// Type-checker tests for the Sprint 6 close fix: a user-defined type name used as a
/// function parameter or return-type annotation now resolves to
/// <see cref="GrobType.Struct"/>/<see cref="GrobType.NullableStruct"/> instead of
/// <see cref="GrobType.Unknown"/>. Before this fix, field access on a struct-typed
/// parameter or on a <c>:=</c>-inferred binding from a struct-returning call silently
/// resolved to <c>Unknown</c> — permissive enough to pass through string interpolation,
/// but producing a false E0005 the moment the field value was returned or used in
/// arithmetic requiring a concrete type (surfaced by <c>types.grob</c>'s recursive-type
/// traversal).
/// </summary>
public sealed class TypeCheckerStructSignatureTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag).Check(unit);
        return (unit, bag);
    }

    private static DiagnosticBag Check(string source) => TypeCheckSource(source).Diagnostics;

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

    // -----------------------------------------------------------------------
    // Struct-typed parameter — field access
    // -----------------------------------------------------------------------

    [Fact]
    public void ParamFieldAccess_NonNullableStructParam_AnnotatesResolvedFieldType() {
        var (unit, bag) = TypeCheckSource("""
            type Person {
            name: string
            }
            fn greet(p: Person): string {
            return p.name
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        MemberAccessExpr ma = Assert.Single(collector.Nodes);
        Assert.Equal(GrobType.String, ma.ResolvedFieldType);
    }

    [Fact]
    public void ReturnFieldOfStructParam_NoFalseE0005() {
        // Regression: returning a struct parameter's field used to resolve to
        // GrobType.Unknown (the parameter's own type never resolved past Unknown),
        // which failed the declared-return-type check.
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            fn getPort(c: Config): int {
            return c.port
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void ArithmeticOverStructParamField_NoFalseE0005() {
        // The exact failure mode types.grob hit: a recursive function adding a
        // struct parameter's int field to its own recursive-call result.
        DiagnosticBag bag = Check("""
            type Node {
            value: int
            next: Node?
            }
            fn sumList(n: Node?): int {
            if (n != nil) {
            return n.value + sumList(n.next)
            }
            return 0
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }

    [Fact]
    public void NarrowedNullableStructParam_FieldAccess_AnnotatesResolvedFieldType() {
        var (unit, bag) = TypeCheckSource("""
            type Node {
            value: int
            next: Node?
            }
            fn getValue(n: Node?): int {
            if (n != nil) {
            return n.value
            }
            return 0
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        MemberAccessExpr ma = Assert.Single(collector.Nodes);
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    // -----------------------------------------------------------------------
    // D-403: '?.' field access on a nullable-struct receiver resolves the field's
    // nullable-widened type — mirroring D-402's identical fix for the method-call
    // path, now closed for property access too.
    // -----------------------------------------------------------------------

    [Fact]
    public void OptionalChainFieldAccess_OnNullableStructParam_ResolvesWidenedFieldType() {
        var (unit, bag) = TypeCheckSource("""
            type Point {
            x: int
            y: int
            }
            fn describe(p: Point?): void {
            v := p?.x
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        MemberAccessExpr ma = Assert.Single(collector.Nodes, m => m.Member == "x");
        Assert.Equal(GrobType.NullableInt, ma.ResolvedFieldType);
    }

    [Fact]
    public void OptionalChainFieldAccess_OnAlreadyNullableField_StaysSinglyNullable() {
        // PR #189 follow-up review (CodeRabbit): the boundary of D-403's widening. The
        // field is already 'int?', so ResolveNullableMemberAccess widens a value that is
        // nullable to begin with — GrobTypeHelpers.ToNullable's '_ => type' arm must
        // return it unchanged rather than reaching for a second-order nullable the type
        // system has no tag for. Characterisation, not a bug fix: this was already the
        // behaviour, it was simply never pinned.
        var (unit, bag) = TypeCheckSource("""
            type Box {
            opt: int?
            }
            fn describe(b: Box?): void {
            v := b?.opt
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        MemberAccessExpr ma = Assert.Single(collector.Nodes, m => m.Member == "opt");
        Assert.Equal(GrobType.NullableInt, ma.ResolvedFieldType);
    }

    [Fact]
    public void UndefinedFieldOnStructParam_EmitsE1002() {
        // The parameter now resolves to a real struct kind, so an undefined member
        // is caught (previously masked by the permissive Unknown fallback).
        DiagnosticBag bag = Check("""
            type Person {
            name: string
            }
            fn greet(p: Person): string {
            return p.missing
            }
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E1002", diag.Code);
        Assert.Equal(5, diag.Range.Start.Line);
        Assert.Equal(8, diag.Range.Start.Column);
    }

    [Fact]
    public void CallWithNonStructArgToStructParam_EmitsE0004() {
        // A user-defined struct used as a parameter annotation resolves to a concrete
        // struct kind, so passing a non-struct argument is now rejected by E0004
        // (previously masked by the permissive Unknown fallback in CheckBoundArgumentType).
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            fn takesConfig(c: Config): int {
            return 0
            }
            readonly x := takesConfig(1)
            """);

        Diagnostic diag = Assert.Single(bag.Errors);
        Assert.Equal("E0004", diag.Code);
    }

    // -----------------------------------------------------------------------
    // Struct-typed return — field access on the call-result binding
    // -----------------------------------------------------------------------

    [Fact]
    public void FieldAccessOnCallResult_StructReturnType_AnnotatesResolvedFieldType() {
        var (unit, bag) = TypeCheckSource("""
            type Config {
            port: int
            }
            fn makeConfig(): Config {
            return Config { port: 8080 }
            }
            readonly c := makeConfig()
            readonly p := c.port
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
        var collector = new MemberAccessCollector();
        collector.Visit(unit);
        MemberAccessExpr ma = Assert.Single(collector.Nodes);
        Assert.Equal(GrobType.Int, ma.ResolvedFieldType);
    }

    [Fact]
    public void ArithmeticOverCallResultField_StructReturnType_NoFalseE0005() {
        DiagnosticBag bag = Check("""
            type Config {
            port: int
            }
            fn makeConfig(): Config {
            return Config { port: 8080 }
            }
            fn getDoubledPort(): int {
            c := makeConfig()
            return c.port + c.port
            }
            """);

        Assert.False(bag.HasErrors,
            $"Unexpected errors: {string.Join("; ", bag.Errors.Select(d => $"[{d.Code}] {d.Message}"))}");
    }
}
