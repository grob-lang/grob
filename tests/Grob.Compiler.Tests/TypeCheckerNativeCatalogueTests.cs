using Grob.Compiler.Ast;
using Grob.Core;
using Grob.Runtime;

using Xunit;

using GrobCompiler = Grob.Compiler.Compiler;

namespace Grob.Compiler.Tests;

/// <summary>
/// D-426 change group 4: the type checker reads namespace natives and constants from the
/// <see cref="NativeCatalogue"/> it is constructed with, ahead of the hand table, and hands
/// the compiler what it needs through AST annotations (gate report F1).
/// </summary>
public sealed class TypeCheckerNativeCatalogueTests {
    private sealed class TestPlugin(string name, Action<IPluginRegistrar> register) : IGrobPlugin {
        public string Name { get; } = name;
        public void Register(IPluginRegistrar registrar) => register(registrar);
    }

    private static readonly NativeFunction Noop = new("noop", 0, (_, _) => GrobValue.Nil);

    private static NativeCatalogue CatalogueOf(Action<IPluginRegistrar> register) =>
        new NativeCatalogueBuilder().Add(new TestPlugin("acme", register)).Build();

    private static SignatureParameter Param(string name, GrobType type, GrobValue? defaultValue = null) =>
        new(name, SignatureType.Of(type), defaultValue: defaultValue);

    private static readonly NativeCatalogue Acme = CatalogueOf(registrar => {
        registrar.RegisterNative(
            new FunctionSignature("acme.twice", [Param("value", GrobType.Int)], SignatureType.Of(GrobType.Int)), Noop);
        registrar.RegisterNative(
            new FunctionSignature("acme.greet",
                [Param("name", GrobType.String), Param("greeting", GrobType.String, GrobValue.FromString("hi"))],
                SignatureType.Of(GrobType.String)),
            Noop);
        registrar.RegisterNative(
            new FunctionSignature("acme.make", [], new SignatureType.TypeParameterReference("T"), [new TypeParameter("T")]),
            Noop);
        registrar.RegisterConstant("acme.answer", GrobValue.FromInt(42));
    });

    private static (CompilationUnit Unit, DiagnosticBag Bag) Check(string source, NativeCatalogue catalogue) {
        var bag = new DiagnosticBag();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        new TypeChecker(bag, catalogue).Check(unit);
        return (unit, bag);
    }

    private static T Find<T>(CompilationUnit unit) where T : Expression {
        var finder = new Finder<T>();
        finder.Visit(unit);
        return finder.Found ?? throw new InvalidOperationException($"No {typeof(T).Name} in the unit.");
    }

    private sealed class Finder<T> : AstWalker where T : Expression {
        public T? Found { get; private set; }
        public override Unit VisitCall(CallExpr node) { Found ??= node as T; return base.VisitCall(node); }
        public override Unit VisitMemberAccess(MemberAccessExpr node) {
            // The outermost access is visited first; keep it.
            Found ??= node as T;
            return base.VisitMemberAccess(node);
        }
        public override Unit VisitErrorExpr(ErrorExpr node) => default;
        public override Unit VisitErrorStmt(ErrorStmt node) => default;
        public override Unit VisitErrorDecl(ErrorDecl node) => default;
    }

    [Fact]
    public void Constructor_NullCatalogue_Throws() {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() => new TypeChecker(new DiagnosticBag(), null!));
        Assert.Equal("catalogue", ex.ParamName);
    }

    [Fact]
    public void CatalogueNative_WellTypedCall_ResolvesItsReturnType() {
        (_, DiagnosticBag bag) = Check("x: int := acme.twice(2)", Acme);
        Assert.False(bag.HasErrors, string.Join("; ", bag.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void CatalogueNative_WrongArgumentType_ReportsNamespaceNativeShape() {
        (_, DiagnosticBag bag) = Check("acme.twice(\"a\")", Acme);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0004", error.Code);
        Assert.Equal(
            "Argument 1 to 'acme.twice' has type 'string', which is not assignable to parameter of type 'int'.",
            error.Message);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(12, error.Range.Start.Column);
    }

    [Fact]
    public void CatalogueNative_WrongArity_ReportsNamespaceNativeShape() {
        (_, DiagnosticBag bag) = Check("acme.twice()", Acme);
        Diagnostic error = Assert.Single(bag.Errors);
        Assert.Equal("E0003", error.Code);
        Assert.Equal("'acme.twice' expects 1 argument, but 0 were supplied.", error.Message);
        Assert.Equal(1, error.Range.Start.Line);
        Assert.Equal(1, error.Range.Start.Column);
    }

    [Fact]
    public void CatalogueNative_WinsOverTheHandTable() {
        // The hand table types math.sqrt as (float): float; this catalogue's own math.sqrt
        // is (int): int, and the checker must read the catalogue's.
        NativeCatalogue catalogue = CatalogueOf(registrar => registrar.RegisterNative(
            new FunctionSignature("math.sqrt", [Param("value", GrobType.Int)], SignatureType.Of(GrobType.Int)), Noop));
        (_, DiagnosticBag bag) = Check("x: int := math.sqrt(4)", catalogue);
        Assert.False(bag.HasErrors, string.Join("; ", bag.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void CatalogueConstant_ResolvesItsDerivedType() {
        (_, DiagnosticBag bag) = Check("x: int := acme.answer", Acme);
        Assert.False(bag.HasErrors, string.Join("; ", bag.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void CatalogueNamespace_IsRegisteredAsANamespaceName() {
        (_, DiagnosticBag bag) = Check("acme := 5", Acme);
        Assert.Contains(bag.Errors, e => e.Code == "E1102");
    }

    [Fact]
    public void CatalogueNativeWithDefault_OmittedArgument_AnnotatesTheDefaults() {
        (CompilationUnit unit, DiagnosticBag bag) = Check("acme.greet(\"x\")", Acme);
        Assert.False(bag.HasErrors);
        CallExpr call = Find<CallExpr>(unit);
        Assert.Equal([null, GrobValue.FromString("hi")], call.ResolvedNativeParameterDefaults);
    }

    [Fact]
    public void NativeWithoutDefaults_LeavesTheDefaultsAnnotationNull() {
        (CompilationUnit unit, _) = Check("math.sqrt(4.0)", SharedCatalogue.Stdlib);
        Assert.Null(Find<CallExpr>(unit).ResolvedNativeParameterDefaults);
    }

    [Fact]
    public void HandTableNativeWithDefault_AnnotatesTheDefaults() {
        (CompilationUnit unit, _) = Check("date.parse(\"2026-04-05\")", SharedCatalogue.Stdlib);
        Assert.Equal([null, GrobValue.FromString("")], Find<CallExpr>(unit).ResolvedNativeParameterDefaults);
    }

    [Fact]
    public void NestedNamespaceChain_AnnotatesTheQualifiedName() {
        (CompilationUnit unit, _) = Check("guid.namespaces.dns", SharedCatalogue.Stdlib);
        Assert.Equal("guid.namespaces.dns", Find<MemberAccessExpr>(unit).ResolvedQualifiedNativeName);
    }

    [Fact]
    public void InstanceAccessOnANamespaceConstant_LeavesTheQualifiedNameNull() {
        (CompilationUnit unit, DiagnosticBag bag) = Check("guid.empty.isEmpty", SharedCatalogue.Stdlib);
        Assert.False(bag.HasErrors);
        Assert.Null(Find<MemberAccessExpr>(unit).ResolvedQualifiedNativeName);
    }

    [Fact]
    public void CatalogueNativeWithDefault_CompilesTheFilledDefault() {
        (CompilationUnit unit, DiagnosticBag bag) = Check("acme.greet(\"x\")", Acme);
        Chunk chunk = GrobCompiler.Compile(unit, bag);
        Assert.False(bag.HasErrors);
        Assert.Contains(GrobValue.FromString("hi"), Enumerable.Range(0, chunk.ConstantCount).Select(chunk.ReadConstant));
    }

    [Fact]
    public void GenericSignature_Call_ThrowsTheTypeParameterFence() {
        GrobInternalException ex = Assert.Throws<GrobInternalException>(() => Check("acme.make()", Acme));
        Assert.Equal(
            "FunctionSignature 'acme.make' declares type parameters; substitution and constraint checking arrive with the mapAs<T> increment.",
            ex.Message);
    }

    [Fact]
    public void GenericSignature_BareAccess_ThrowsTheTypeParameterFence() {
        GrobInternalException ex = Assert.Throws<GrobInternalException>(() => Check("f := acme.make", Acme));
        Assert.Contains("mapAs<T>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NamespaceSymbols_AreBuiltOncePerCatalogue() {
        Assert.Same(NamespaceSymbols.For(Acme), NamespaceSymbols.For(Acme));
        Assert.NotSame(NamespaceSymbols.For(Acme), NamespaceSymbols.For(SharedCatalogue.Stdlib));
    }
}
