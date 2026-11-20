using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-441: a named type's method may take a function (<see cref="SignatureType.FunctionShape"/>)
/// and carry overloads. A lambda passed where a shape is expected takes the shape's
/// parameter types, so its parameters resolve members nominally; the checker picks the
/// overload a call matches and checks a function argument's arity, parameters and return
/// against the shape. <c>token</c> exists only in this test catalogue, so every resolution
/// demonstrably comes from it.
/// </summary>
public sealed class TypeCheckerFunctionShapeTests {
    private sealed class TestPlugin(Action<IPluginRegistrar> register) : IGrobPlugin {
        public string Name => "tokens";
        public void Register(IPluginRegistrar registrar) => register(registrar);
    }

    private static readonly NativeFunction Noop = new("noop", 0, (_, _) => GrobValue.Nil);
    private static readonly SignatureType.Concrete TokenType = SignatureType.Named(GrobType.Struct, "token");
    private static readonly SignatureType.Concrete StringType = SignatureType.Of(GrobType.String);
    private static readonly SignatureType.Concrete IntType = SignatureType.Of(GrobType.Int);

    private static readonly NativeCatalogue Tokens = new NativeCatalogueBuilder().Add(new TestPlugin(registrar => {
        registrar.RegisterNative(new FunctionSignature("tokens.make", [], TokenType), Noop);
        registrar.RegisterNamedType(new NamedTypeDefinition(
            "token",
            [
                new NamedTypePropertyBinding(new PropertySignature("name", StringType), s => s.GetField("name")),
                new NamedTypePropertyBinding(new PropertySignature("secret", IntType), s => s.GetField("secret")),
            ],
            [
                new NamedTypeMethodBinding(
                    new FunctionSignature("apply",
                        [new SignatureParameter("input", StringType), new SignatureParameter("replacement", StringType)],
                        StringType),
                    _ => Noop) {
                    Overloads = [
                        new FunctionSignature("apply",
                            [new SignatureParameter("input", StringType),
                             new SignatureParameter("fn", new SignatureType.FunctionShape([TokenType], StringType))],
                            StringType),
                    ],
                },
                new NamedTypeMethodBinding(
                    new FunctionSignature("pairs",
                        [new SignatureParameter("fn", new SignatureType.FunctionShape(
                            [TokenType, new SignatureType.Concrete(GrobType.NullableArray, elementType: StringType)], StringType))],
                        IntType),
                    _ => Noop),
                new NamedTypeMethodBinding(
                    new FunctionSignature("each",
                        [new SignatureParameter("fn", new SignatureType.FunctionShape([TokenType], SignatureType.Of(GrobType.Nil)))],
                        IntType),
                    _ => Noop),
            ],
            v => "<token>"));
    })).Build();

    private static (CompilationUnit Unit, DiagnosticBag Bag) Check(string source) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, Tokens).Check(unit);
        return (unit, bag);
    }

    private static string Messages(DiagnosticBag bag) => string.Join("; ", bag.Errors.Select(e => e.Message));

    private static List<IdentifierExpr> Identifiers(CompilationUnit unit, string name) {
        var finder = new IdentifierFinder(name);
        finder.Visit(unit);
        return finder.Found;
    }

    private sealed class IdentifierFinder(string name) : AstWalker {
        public List<IdentifierExpr> Found { get; } = [];

        public override Unit VisitIdentifier(IdentifierExpr node) {
            if (node.Name == name) Found.Add(node);
            return base.VisitIdentifier(node);
        }

        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    private static void AssertSingleError(DiagnosticBag bag, string code, int line, int column, string message) {
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Equal(line, error.Range.Start.Line);
        Assert.Equal(column, error.Range.Start.Column);
    }

    // ----- Contextual lambda typing -----

    [Fact]
    public void Lambda_ForAShape_TakesItsNominalParameterType() {
        (CompilationUnit unit, DiagnosticBag bag) = Check("t := tokens.make()\ns: string := t.apply(\"a\", k => k.name)");

        Assert.False(bag.HasErrors, Messages(bag));
        IdentifierExpr k = Assert.Single(Identifiers(unit, "k"));
        Assert.Equal(GrobType.Struct, k.ResolvedType);
        Assert.IsType<LambdaExpr>(k.Declaration);
    }

    [Fact]
    public void Lambda_ForAShape_UnknownMemberIsE1002() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nt.apply(\"a\", k => k.nope)");

        AssertSingleError(bag, "E1002", 2, 19, "Type 'token' has no member 'nope'.");
    }

    [Fact]
    public void Lambda_NestedInAShapedLambda_KeepsUnknownParameters() {
        (CompilationUnit unit, DiagnosticBag bag) = Check(
            "t := tokens.make()\nt.apply(\"a\", k => {\n    f := j => j.anything\n    k.name\n})");

        Assert.False(bag.HasErrors, Messages(bag));
        IdentifierExpr j = Assert.Single(Identifiers(unit, "j"));
        Assert.Equal(GrobType.Unknown, j.ResolvedType);
    }

    [Fact]
    public void Lambda_ThroughAnOptionalChain_TakesTheShapesType() {
        (CompilationUnit unit, DiagnosticBag bag) = Check(
            "t: token? := tokens.make()\ns := t?.apply(\"a\", k => k.name)");

        Assert.False(bag.HasErrors, Messages(bag));
        Assert.Equal(GrobType.Struct, Assert.Single(Identifiers(unit, "k")).ResolvedType);
    }

    // ----- Overload selection -----

    [Fact]
    public void StringArgument_SelectsTheStringForm() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\ns: string := t.apply(\"a\", \"b\")");

        Assert.False(bag.HasErrors, Messages(bag));
    }

    [Fact]
    public void NeitherForm_ReportsAgainstThePrimarySignature() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nt.apply(\"a\", 5)");

        AssertSingleError(bag, "E0004", 2, 14,
            "Argument to 'apply' has type 'int', which is not assignable to parameter of type 'string'.");
    }

    [Fact]
    public void UnknownArgument_IsAcceptedPermissively() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nxs := [1].select(q => t.apply(\"a\", q))");

        Assert.False(bag.HasErrors, Messages(bag));
    }

    // ----- Function-argument checks -----

    [Fact]
    public void Lambda_WrongArity_IsE0004() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nt.apply(\"a\", (j, k) => \"x\")");

        AssertSingleError(bag, "E0004", 2, 14,
            "Argument to 'apply' takes 2 parameters, but parameter 'fn' expects a function of type 'fn(token): string'.");
    }

    [Fact]
    public void Lambda_OneParameterForTwo_NamesTheFullShape() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nt.pairs(k => \"x\")");

        AssertSingleError(bag, "E0004", 2, 9,
            "Argument to 'pairs' takes 1 parameter, but parameter 'fn' expects a function of type 'fn(token, string[]?): string'.");
    }

    [Fact]
    public void Lambda_NonStringBody_IsE0004() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nt.apply(\"a\", k => k.secret)");

        AssertSingleError(bag, "E0004", 2, 14,
            "Argument to 'apply' returns 'int', but parameter 'fn' expects a function of type 'fn(token): string'.");
    }

    [Fact]
    public void NamedFunction_IsAcceptedPermissively() {
        // A top-level fn reference is typed Unknown (pass 1 and VisitFnDecl), so it is not
        // checked against the shape; the native tells the forms apart at run time.
        (_, DiagnosticBag bag) = Check(
            "fn label(n: int): string {\n    return \"x\"\n}\nt := tokens.make()\ns := t.apply(\"a\", label)");

        Assert.False(bag.HasErrors, Messages(bag));
    }

    [Fact]
    public void FunctionTypedBinding_MatchingTheShape_IsAccepted() {
        (_, DiagnosticBag bag) = Check(
            "f: fn(token): string := k => k.name\nt := tokens.make()\ns: string := t.apply(\"a\", f)");

        Assert.False(bag.HasErrors, Messages(bag));
    }

    [Fact]
    public void FunctionTypedBinding_WrongParameterType_IsE0004() {
        (_, DiagnosticBag bag) = Check(
            "f: fn(int): string := n => \"x\"\nt := tokens.make()\ns := t.apply(\"a\", f)");

        AssertSingleError(bag, "E0004", 3, 19,
            "Argument to 'apply' takes 'int' as parameter 1, but parameter 'fn' expects a function of type 'fn(token): string'.");
    }

    [Fact]
    public void FunctionTypedBinding_OneParameterTooFew_SaysParameterSingular() {
        (_, DiagnosticBag bag) = Check(
            "f: fn(): string := () => \"x\"\nt := tokens.make()\ns := t.apply(\"a\", f)");

        AssertSingleError(bag, "E0004", 3, 19,
            "Argument to 'apply' takes 0 parameters, but parameter 'fn' expects a function of type 'fn(token): string'.");
    }

    [Fact]
    public void NullableFunction_ToAShape_IsE0004() {
        (_, DiagnosticBag bag) = Check(
            "f: (fn(token): string)? := nil\nt := tokens.make()\ns := t.apply(\"a\", f)");

        AssertSingleError(bag, "E0004", 3, 19,
            "Argument to 'apply' has type 'fn?', which is not assignable to parameter of type 'fn(token): string'.");
    }

    [Fact]
    public void NilReturningShape_AcceptsAnyReturn() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nn: int := t.each(k => k.secret)");

        Assert.False(bag.HasErrors, Messages(bag));
    }

    [Fact]
    public void NonFunction_ToAShape_IsE0004() {
        (_, DiagnosticBag bag) = Check("t := tokens.make()\nt.each(5)");

        AssertSingleError(bag, "E0004", 2, 8,
            "Argument to 'each' has type 'int', which is not assignable to parameter of type 'fn(token): nil'.");
    }
}
