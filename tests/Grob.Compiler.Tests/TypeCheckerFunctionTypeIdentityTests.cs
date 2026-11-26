using Grob.Compiler.Ast;
using Grob.Core;

using Xunit;

using static Grob.Compiler.Tests.ParserTestHelpers;

namespace Grob.Compiler.Tests;

/// <summary>
/// An array, a struct and a map in a function type's parameter or return, the element of an
/// array of functions and the value of a map of functions are compared invariantly by their
/// element, field and value types (D-326, D-463, R-98). <c>fn(int[]): int</c> is refused for
/// <c>fn(float[]): int</c>, <c>fn(A): int</c> for <c>fn(B): int</c>, <c>(fn(int): int)[]</c>
/// for <c>(fn(float): float)[]</c> and <c>map&lt;string, fn(int): int&gt;</c> for
/// <c>map&lt;string, fn(float): float&gt;</c>, and a function read out of an array or a map
/// carries its element's function type.
/// </summary>
public sealed class TypeCheckerFunctionTypeIdentityTests {
    private static (CompilationUnit Unit, DiagnosticBag Diagnostics) TypeCheckSource(string source) {
        DiagnosticBag bag = new();
        IReadOnlyList<Token> tokens = Lexer.Scan(source, bag);
        CompilationUnit unit = Parser.Parse(tokens, bag);
        TypeChecker checker = new(bag, SharedCatalogue.Stdlib);
        checker.Check(unit);
        return (unit, bag);
    }

    private static void AssertSingleError(string source, string code, int line, int column) {
        (_, DiagnosticBag bag) = TypeCheckSource(source);
        Diagnostic d = Assert.Single(bag.Errors);
        Assert.Equal(code, d.Code);
        Assert.Equal(line, d.Range.Start.Line);
        Assert.Equal(column, d.Range.Start.Column);
    }

    private static void AssertNoErrors(string source) {
        (_, DiagnosticBag bag) = TypeCheckSource(source);
        Assert.False(bag.HasErrors, FormatDiagnostics(bag));
    }

    private const string Structs = """
        type A { x: int }
        type B { y: string }

        """;

    // -----------------------------------------------------------------------
    // An array in a function type's position
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayParameter_IntArrayForFloatArray_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(int[]): int): void {
                h: fn(float[]): int := g
            }
            """, "E0001", 2, 28);

    [Fact]
    public void ArrayParameter_FloatArrayForIntArray_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(float[]): int): void {
                h: fn(int[]): int := g
            }
            """, "E0001", 2, 26);

    [Fact]
    public void ArrayReturn_IntArrayForFloatArray_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(): int[]): void {
                h: fn(): float[] := g
            }
            """, "E0001", 2, 25);

    [Fact]
    public void ArrayParameter_NestedArrayElementDiffers_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(int[][]): int): void {
                h: fn(float[][]): int := g
            }
            """, "E0001", 2, 30);

    [Fact]
    public void ArrayParameter_StructElementDiffers_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(g: fn(A[]): int): void {
                h: fn(B[]): int := g
            }
            """, "E0001", 4, 24);

    [Fact]
    public void ArrayParameter_NullableArrayForArray_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(int[]?): int): void {
                h: fn(int[]): int := g
            }
            """, "E0001", 2, 26);

    [Fact]
    public void ArrayParameter_ArgumentToFunctionParameter_IsE0004() =>
        AssertSingleError("""
            fn take(f: fn(float[]): int): int {
                return f([2.5])
            }
            fn use(g: fn(int[]): int): int {
                return take(g)
            }
            """, "E0004", 5, 17);

    [Fact]
    public void ArrayReturn_ReturnedFunction_IsE0005() =>
        AssertSingleError("""
            fn use(g: fn(): int[]): fn(): float[] {
                return g
            }
            """, "E0005", 2, 12);

    [Fact]
    public void ArrayParameter_SameElementType_IsAccepted() =>
        AssertNoErrors("""
            fn use(g: fn(int[]): int): int {
                h: fn(int[]): int := g
                return h([1, 2])
            }
            """);

    // -----------------------------------------------------------------------
    // A struct in a function type's position
    // -----------------------------------------------------------------------

    [Fact]
    public void StructParameter_DifferentNamedTypes_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(g: fn(A): int): void {
                h: fn(B): int := g
            }
            """, "E0001", 4, 22);

    [Fact]
    public void StructParameter_ReverseDirection_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(g: fn(B): int): void {
                h: fn(A): int := g
            }
            """, "E0001", 4, 22);

    [Fact]
    public void StructReturn_DifferentNamedTypes_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(g: fn(): A): void {
                h: fn(): B := g
            }
            """, "E0001", 4, 19);

    [Fact]
    public void StructParameter_StructForInt_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(g: fn(A): int): void {
                h: fn(int): int := g
            }
            """, "E0001", 4, 24);

    [Fact]
    public void StructParameter_NullableStructForStruct_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(g: fn(A?): int): void {
                h: fn(A): int := g
            }
            """, "E0001", 4, 22);

    [Fact]
    public void StructParameter_ErrorSubtypeForRoot_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(IoError): int): void {
                h: fn(GrobError): int := g
            }
            """, "E0001", 2, 30);

    [Fact]
    public void StructParameter_ArgumentToFunctionParameter_IsE0004() =>
        AssertSingleError(Structs + """
            fn take(f: fn(B): int): int {
                return f(B { y: "s" })
            }
            fn use(g: fn(A): int): int {
                return take(g)
            }
            """, "E0004", 7, 17);

    [Fact]
    public void StructParameter_SameNamedType_IsAccepted() =>
        AssertNoErrors(Structs + """
            fn use(g: fn(A): int): int {
                h: fn(A): int := g
                return h(A { x: 1 })
            }
            """);

    [Fact]
    public void StructReturn_CallThroughFunctionValue_ResolvesTheField() =>
        AssertNoErrors(Structs + """
            fn use(g: fn(): A): int {
                return g().x
            }
            """);

    [Fact]
    public void StructReturn_CallThroughFunctionValue_UnknownFieldIsE1002() =>
        AssertSingleError(Structs + """
            fn use(g: fn(): A): int {
                return g().nope
            }
            """, "E1002", 4, 12);

    [Fact]
    public void StructParameter_CallWithDifferentStruct_IsE0004() =>
        AssertSingleError(Structs + """
            fn use(g: fn(A): int): int {
                return g(B { y: "s" })
            }
            """, "E0004", 4, 14);

    [Fact]
    public void StructParameter_CallWithInt_IsE0004() =>
        AssertSingleError(Structs + """
            fn use(g: fn(A): int): int {
                return g(1)
            }
            """, "E0004", 4, 14);

    [Fact]
    public void MapParameter_CallWithDifferentValueType_IsE0004() =>
        AssertSingleError("""
            fn use(g: fn(map<string, int>): int, m: map<string, string>): int {
                return g(m)
            }
            """, "E0004", 2, 14);

    [Fact]
    public void MapParameter_CallWithSameValueType_IsAccepted() =>
        AssertNoErrors("""
            fn use(g: fn(map<string, int>): int, m: map<string, int>): int {
                return g(m)
            }
            """);

    [Fact]
    public void ArrayParameter_CallWithDifferentElementType_IsE0004() =>
        AssertSingleError("""
            fn use(g: fn(int[]): int): int {
                return g([2.5])
            }
            """, "E0004", 2, 14);

    // -----------------------------------------------------------------------
    // A map in a function type's position
    // -----------------------------------------------------------------------

    [Fact]
    public void MapParameter_IntValueForFloatValue_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(map<string, int>): int): void {
                h: fn(map<string, float>): int := g
            }
            """, "E0001", 2, 39);

    [Fact]
    public void MapParameter_SameValueType_IsAccepted() =>
        AssertNoErrors("""
            fn use(g: fn(map<string, int>): int): void {
                h: fn(map<string, int>): int := g
            }
            """);

    // -----------------------------------------------------------------------
    // An array of functions
    // -----------------------------------------------------------------------

    [Fact]
    public void ArrayOfFunctions_IntFunctionsForFloatFunctions_IsE0001() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            hs: (fn(float): float)[] := fs
            """, "E0001", 2, 29);

    [Fact]
    public void ArrayOfFunctions_FloatFunctionsForIntFunctions_IsE0001() =>
        AssertSingleError("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            hs: (fn(int): int)[] := fs
            """, "E0001", 2, 25);

    [Fact]
    public void ArrayOfFunctions_LiteralOfMismatchedFunction_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            fs: (fn(float): float)[] := [g]
            """, "E0001", 2, 29);

    [Fact]
    public void ArrayOfFunctions_LiteralMixingShapes_IsE0001() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            h: fn(float): float := (n) => n * 2.0
            fs := [g, h]
            """, "E0001", 3, 11);

    [Fact]
    public void ArrayOfFunctions_InferredLambdaBesideTypedFunction_TakesItsShape() =>
        AssertSingleError("""
            g: fn(int): int := (n) => n + 1
            fs := [g, (n) => n * 2]
            h: fn(float): float := fs[1]
            """, "E0001", 3, 24);

    /// <summary>
    /// An element whose shape is not tracked was never checked against a typed element's
    /// shape, so the array claims none: a call through any element is unchecked, as it is
    /// for an array of untracked functions alone.
    /// </summary>
    [Theory]
    [InlineData("[box.op, g]")]
    [InlineData("[g, box.op]")]
    public void ArrayOfFunctions_UntrackedElementBesideTypedOne_LeavesTheArrayUntracked(string literal) =>
        AssertNoErrors($$"""
            box := #{ op: (s) => s + "!" }
            g: fn(int): int := (n) => n + 1
            fs := {{literal}}
            print(fs[0](2.5))
            """);

    [Fact]
    public void ArrayOfFunctions_LiteralOfUntrackedFunction_IsAccepted() =>
        AssertNoErrors("""
            box := #{ op: (n) => n + 1 }
            fs: (fn(float): float)[] := [box.op]
            """);

    [Fact]
    public void ArrayOfFunctions_LiteralOfInferredLambda_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            print(fs[0](2.5))
            """);

    [Fact]
    public void ArrayOfFunctions_EmptyLiteral_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(int): int)[] := []
            """);

    [Fact]
    public void ArrayOfFunctions_ElementBoundToItsFunctionType_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            h: fn(float): float := fs[0]
            print(h(2.5))
            """);

    [Fact]
    public void ArrayOfFunctions_ElementBoundToAnotherFunctionType_IsE0001() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            h: fn(float): float := fs[0]
            """, "E0001", 2, 24);

    [Fact]
    public void ArrayOfFunctions_InferredLocalFromElement_KeepsTheShape() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            g := fs[0]
            h: fn(float): float := g
            """, "E0001", 3, 24);

    [Fact]
    public void ArrayOfFunctions_CallThroughElementWithWrongArgument_IsE0004() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            print(fs[0](2.5))
            """, "E0004", 2, 13);

    [Fact]
    public void ArrayOfFunctions_CallThroughElementWithWrongCount_IsE0003() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            print(fs[0](1, 2))
            """, "E0003", 2, 7);

    [Fact]
    public void ArrayOfFunctions_CallThroughElementResult_TypesTheResult() =>
        AssertNoErrors("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            n: int := fs[0](1)
            """);

    [Fact]
    public void ArrayOfFunctions_IndexAssignmentOfMismatchedFunction_IsE0001() =>
        AssertSingleError("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            g: fn(int): int := (n) => n + 1
            fs[0] = g
            """, "E0001", 3, 9);

    [Fact]
    public void ArrayOfFunctions_AppendOfMismatchedFunction_IsE0004() =>
        AssertSingleError("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            g: fn(int): int := (n) => n + 1
            fs.append(g)
            """, "E0004", 3, 11);

    [Fact]
    public void ArrayOfFunctions_InsertOfMismatchedFunction_IsE0004() =>
        AssertSingleError("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            g: fn(int): int := (n) => n + 1
            fs.insert(0, g)
            """, "E0004", 3, 14);

    [Fact]
    public void ArrayOfFunctions_ContainsOfMismatchedFunction_IsE0004() =>
        AssertSingleError("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            g: fn(int): int := (n) => n + 1
            print(fs.contains(g))
            """, "E0004", 3, 19);

    [Fact]
    public void ArrayOfFunctions_AppendOfMatchingFunction_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            g: fn(float): float := (n) => n + 1.0
            fs.append(g)
            fs.append((n) => n / 2.0)
            fs[0] = g
            """);

    [Fact]
    public void ArrayOfFunctions_ForInItemCarriesTheShape_IsE0004OnAWrongArgument() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            for f in fs {
                print(f(2.5))
            }
            """, "E0004", 3, 13);

    [Fact]
    public void ArrayOfFunctions_ForInIndexFormItemCarriesTheShape() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            for i, f in fs {
                h: fn(float): float := f
            }
            """, "E0001", 3, 28);

    [Fact]
    public void ArrayOfFunctions_ForInMatchingCall_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            for f in fs {
                print(f(2.5))
            }
            """);

    [Fact]
    public void ArrayOfFunctions_FirstCarriesTheShape() =>
        AssertSingleError("""
            fs: (fn(int): int)[] := [(n) => n + 1]
            h: (fn(float): float)? := fs.first()
            """, "E0001", 2, 27);

    [Fact]
    public void ArrayOfFunctions_LastBoundToItsShape_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            h: (fn(float): float)? := fs.last()
            """);

    [Fact]
    public void ArrayOfFunctions_FilterResultBoundToTheSameShape_IsAccepted() =>
        AssertNoErrors("""
            fs: (fn(float): float)[] := [(n) => n * 2.0]
            hs: (fn(float): float)[] := fs.filter((f) => true)
            """);

    [Fact]
    public void ArrayOfFunctions_NestedArraysCompared_IsE0001() =>
        AssertSingleError("""
            fn use(fss: (fn(int): int)[][]): void {
                hss: (fn(float): float)[][] := fss
            }
            """, "E0001", 2, 36);

    [Fact]
    public void ArrayOfFunctions_NestedElementCarriesTheShape() =>
        AssertSingleError("""
            fn use(fss: (fn(int): int)[][]): void {
                h: fn(float): float := fss[0][0]
            }
            """, "E0001", 2, 28);

    [Fact]
    public void ArrayOfFunctions_NestedElementBoundToItsShape_IsAccepted() =>
        AssertNoErrors("""
            fn use(fss: (fn(int): int)[][]): int {
                h: fn(int): int := fss[0][0]
                return h(1)
            }
            """);

    [Fact]
    public void ArrayOfFunctions_NullableArraysCompared_IsE0001() =>
        AssertSingleError("""
            fn use(fs: (fn(int): int)[]?): void {
                hs: (fn(float): float)[]? := fs
            }
            """, "E0001", 2, 34);

    [Fact]
    public void ArrayOfFunctions_UnwrappedNullableArrayElementCarriesTheShape() =>
        AssertSingleError("""
            fn use(fs: (fn(int): int)[]?): void {
                h: fn(float): float := (fs ?? [])[0]
            }
            """, "E0001", 2, 28);

    [Fact]
    public void ArrayOfFunctions_FunctionReturningOne_ResultCompared() =>
        AssertSingleError("""
            fn mk(): (fn(int): int)[] {
                return [(n) => n + 1]
            }
            hs: (fn(float): float)[] := mk()
            """, "E0001", 4, 29);

    [Fact]
    public void ArrayOfFunctions_FunctionReturningOne_ElementCarriesTheShape() =>
        AssertSingleError("""
            fn mk(): (fn(int): int)[] {
                return [(n) => n + 1]
            }
            h: fn(float): float := mk()[0]
            """, "E0001", 4, 24);

    [Fact]
    public void ArrayOfFunctions_FunctionTypeReturningOne_ResultCompared() =>
        AssertSingleError("""
            fn use(g: fn(): (fn(int): int)[]): void {
                hs: (fn(float): float)[] := g()
            }
            """, "E0001", 2, 33);

    [Fact]
    public void ArrayOfFunctions_AsFunctionTypeReturn_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(): (fn(int): int)[]): void {
                h: fn(): (fn(float): float)[] := g
            }
            """, "E0001", 2, 38);

    [Fact]
    public void ArrayOfFunctions_AsFunctionTypeReturn_SameShape_IsAccepted() =>
        AssertNoErrors("""
            fn use(g: fn(): (fn(int): int)[]): int {
                h: fn(): (fn(int): int)[] := g
                return h()[0](1)
            }
            """);

    [Fact]
    public void ArrayOfFunctions_PassedToParameter_IsE0004() =>
        AssertSingleError("""
            fn take(hs: (fn(float): float)[]): void {
            }
            fs: (fn(int): int)[] := [(n) => n + 1]
            take(fs)
            """, "E0004", 4, 6);

    [Fact]
    public void ArrayOfFunctions_StructField_ConstructionCompared() =>
        AssertSingleError("""
            type T { fs: (fn(int): int)[] }
            hs: (fn(float): float)[] := [(n) => n * 2.0]
            t := T { fs: hs }
            """, "E0001", 3, 14);

    [Fact]
    public void ArrayOfFunctions_StructField_SameShape_IsAccepted() =>
        AssertNoErrors("""
            type T { fs: (fn(int): int)[] }
            hs: (fn(int): int)[] := [(n) => n + 1]
            t := T { fs: hs }
            """);

    [Fact]
    public void ArrayOfFunctions_TernaryOfMismatchedArrays_IsE0002() =>
        AssertSingleError("""
            fn use(fs: (fn(int): int)[], hs: (fn(float): float)[], c: bool): void {
                k := c ? fs : hs
            }
            """, "E0002", 2, 10);

    // -----------------------------------------------------------------------
    // A map of functions, by annotation (a map literal's type arguments do not
    // parse a function type)
    // -----------------------------------------------------------------------

    [Fact]
    public void MapOfFunctions_IntFunctionsForFloatFunctions_IsE0001() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>): void {
                k: map<string, fn(float): float> := m
            }
            """, "E0001", 2, 41);

    [Fact]
    public void MapOfFunctions_FloatFunctionsForIntFunctions_IsE0001() =>
        AssertSingleError("""
            fn use(m: map<string, fn(float): float>): void {
                k: map<string, fn(int): int> := m
            }
            """, "E0001", 2, 37);

    [Fact]
    public void MapOfFunctions_SameShape_IsAccepted() =>
        AssertNoErrors("""
            fn use(m: map<string, fn(int): int>): void {
                k: map<string, fn(int): int> := m
            }
            """);

    [Fact]
    public void MapOfFunctions_IndexCarriesTheShape() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>): void {
                h: (fn(float): float)? := m["k"]
            }
            """, "E0001", 2, 31);

    [Fact]
    public void MapOfFunctions_IndexBoundToItsShape_IsAccepted() =>
        AssertNoErrors("""
            fn use(m: map<string, fn(int): int>): void {
                h: (fn(int): int)? := m["k"]
                g: (fn(int): int)? := m.get("k")
            }
            """);

    [Fact]
    public void MapOfFunctions_GetCarriesTheShape() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>): void {
                h: (fn(float): float)? := m.get("k")
            }
            """, "E0001", 2, 31);

    [Fact]
    public void MapOfFunctions_ForInValueCarriesTheShape() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>): void {
                for k, v in m {
                    h: fn(float): float := v
                }
            }
            """, "E0001", 3, 32);

    [Fact]
    public void MapOfFunctions_ValuesCarryTheShape() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>): void {
                hs: (fn(float): float)[] := m.values
            }
            """, "E0001", 2, 33);

    [Fact]
    public void MapOfFunctions_SetOfMismatchedFunction_IsE0004() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>, g: fn(float): float): void {
                m.set("k", g)
            }
            """, "E0004", 2, 16);

    [Fact]
    public void MapOfFunctions_IndexAssignmentOfMismatchedFunction_IsE0001() =>
        AssertSingleError("""
            fn use(m: map<string, fn(int): int>, g: fn(float): float): void {
                m["k"] = g
            }
            """, "E0001", 2, 14);

    [Fact]
    public void MapOfFunctions_SetOfMatchingFunction_IsAccepted() =>
        AssertNoErrors("""
            fn use(m: map<string, fn(int): int>, g: fn(int): int): void {
                m.set("k", g)
                m["j"] = (n) => n * 2
            }
            """);

    [Fact]
    public void MapOfFunctions_InFunctionTypePosition_IsE0001() =>
        AssertSingleError("""
            fn use(g: fn(map<string, fn(int): int>): int): void {
                h: fn(map<string, fn(float): float>): int := g
            }
            """, "E0001", 2, 50);

    // -----------------------------------------------------------------------
    // A lambda's body keeps the value rules, identity included
    // -----------------------------------------------------------------------

    [Fact]
    public void LambdaReturningAnotherStruct_IsE0001() =>
        AssertSingleError(Structs + """
            g: fn(): A := () => B { y: "s" }
            """, "E0001", 3, 15);

    [Fact]
    public void LambdaReturningTheStruct_IsAccepted() =>
        AssertNoErrors(Structs + """
            g: fn(): A := () => A { x: 1 }
            print(g().x)
            """);

    [Fact]
    public void LambdaReturningAnAnonymousStructForANamedOne_IsE0001() =>
        AssertSingleError(Structs + """
            g: fn(): A := () => #{ x: 1 }
            """, "E0001", 3, 15);

    [Fact]
    public void LambdaReturningAnIntArrayForAFloatArray_IsE0001() =>
        AssertSingleError("""
            k: fn(): float[] := () => [1]
            """, "E0001", 1, 21);

    [Fact]
    public void LambdaReturningAMapOfAnotherValueType_IsE0001() =>
        AssertSingleError("""
            fn use(m: map<string, int>): void {
                k: fn(): map<string, float> := () => m
            }
            """, "E0001", 2, 36);

    [Fact]
    public void LambdaReturningAnErrorSubtypeForItsRoot_IsAccepted() =>
        AssertNoErrors("""
            g: fn(): GrobError := () => IoError { message: "m" }
            """);

    [Fact]
    public void BlockLambdaReturningAnotherStruct_IsE0001() =>
        AssertSingleError(Structs + """
            g: fn(): A := () => {
                return B { y: "s" }
            }
            """, "E0001", 3, 15);

    [Fact]
    public void BlockLambdaEndingInAnotherStruct_IsE0001() =>
        AssertSingleError(Structs + """
            g: fn(): A := () => {
                b := B { y: "s" }
                b
            }
            """, "E0001", 3, 15);

    [Fact]
    public void BlockLambdaReturningTheStructOrNil_IsAccepted() =>
        AssertNoErrors(Structs + """
            fn use(c: bool): void {
                g: fn(): A? := () => {
                    if (c) {
                        return nil
                    }
                    return A { x: 1 }
                }
            }
            """);

    /// <summary>
    /// A block ending in an <c>if</c> and <c>else</c> that both return has no final value
    /// of its own, so its result is the kind its returns agree on, not <c>nil</c>.
    /// </summary>
    [Fact]
    public void BlockLambdaEndingInIfElseReturningTheStruct_IsAccepted() =>
        AssertNoErrors(Structs + """
            fn use(c: bool): int {
                g: fn(): A := () => {
                    if (c) {
                        return A { x: 1 }
                    } else {
                        return A { x: 2 }
                    }
                }
                return g().x
            }
            """);

    [Fact]
    public void BlockLambdaEndingInIfElseReturningAnInt_IsAccepted() =>
        AssertNoErrors("""
            fn use(c: bool): int {
                g: fn(): int := () => {
                    if (c) {
                        return 1
                    } else {
                        return 2
                    }
                }
                return g()
            }
            """);

    [Fact]
    public void BlockLambdaEndingInIfElseReturningAnotherStruct_IsE0001() =>
        AssertSingleError(Structs + """
            fn use(c: bool): void {
                g: fn(): A := () => {
                    if (c) {
                        return B { y: "s" }
                    } else {
                        return B { y: "t" }
                    }
                }
            }
            """, "E0001", 4, 19);

    [Fact]
    public void BlockLambdaEndingInIfElseOfTwoKinds_KeepsTheBlockValue() =>
        AssertSingleError("""
            fn use(c: bool): void {
                g: fn(): int := () => {
                    if (c) {
                        return 1
                    } else {
                        return "s"
                    }
                }
            }
            """, "E0001", 2, 21);

    [Fact]
    public void BlockLambdaReturningAnArrayOrAnAnnotatedLocal_KeepsTheElementType() =>
        AssertSingleError("""
            fn use(c: bool): void {
                xs: int[] := [2]
                k: fn(): float[] := () => {
                    if (c) {
                        return [1]
                    }
                    return xs
                }
            }
            """, "E0001", 3, 25);

    [Fact]
    public void BlockLambdaWhoseResultsDisagree_CarriesNoIdentity() =>
        AssertNoErrors(Structs + """
            fn use(c: bool): void {
                g: fn(): A := () => {
                    if (c) {
                        return A { x: 1 }
                    }
                    return B { y: "s" }
                }
            }
            """);

    [Fact]
    public void LambdaReturningAnUntrackedArray_IsAccepted() =>
        AssertNoErrors("""
            k: fn(): float[] := () => [1.5, 2.5].filter((x) => true)
            """);

    [Fact]
    public void LambdaForABareArrayReturn_IsAccepted() =>
        AssertNoErrors("""
            k: fn(): array := () => [1]
            """);

    [Fact]
    public void LambdaReturningAnUntrackedMap_IsAccepted() =>
        AssertNoErrors("""
            fn mk(): map<string, int> {
                return map<string, int>{}
            }
            k: fn(): map<string, float> := () => mk()
            """);

    [Fact]
    public void BareArrayPosition_IsPermissive() =>
        AssertNoErrors("""
            fn use(g: fn(array): int): void {
                h: fn(int[]): int := g
            }
            """);

    [Fact]
    public void BareMapPosition_IsPermissive() =>
        AssertNoErrors("""
            fn use(g: fn(map): int): void {
                h: fn(map<string, int>): int := g
            }
            """);

    [Fact]
    public void ForInOverAnUntrackedArray_IsPermissive() =>
        AssertNoErrors("""
            for f in [1, 2].filter((x) => x > 1) {
                print(f)
            }
            """);

    [Fact]
    public void ForInOverABareMap_IsPermissive() =>
        AssertNoErrors("""
            fn use(m: map): void {
                for k, v in m {
                    print(v)
                }
            }
            """);

    [Fact]
    public void ForInOverAnUnknownSubject_IsPermissive() =>
        AssertNoErrors("""
            xs := [[1], [2]]
            xs.each((row) => {
                for v in row {
                    print(v)
                }
            })
            """);

    /// <summary>
    /// A lambda returning an annotated binding takes the binding's identity only for the
    /// kind it has, so it combines with a function type of the same return.
    /// </summary>
    [Theory]
    [InlineData("int[]", "[1]")]
    [InlineData("int", "1")]
    [InlineData("map<string, int>", "map<string, int>{}")]
    public void TernaryOfLambdaReturningAnnotatedBindingAndTypedFunction_IsAccepted(string type, string value) =>
        AssertNoErrors($$"""
            fn use(g: fn(): {{type}}, c: bool): void {
                xs: {{type}} := {{value}}
                k := c ? (() => xs) : g
            }
            """);

    [Fact]
    public void LambdaReturningTheArray_IsAccepted() =>
        AssertNoErrors("""
            k: fn(): float[] := () => [1.5]
            """);
}
