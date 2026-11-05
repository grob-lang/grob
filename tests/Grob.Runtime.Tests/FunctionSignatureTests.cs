using Grob.Core;

using Xunit;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="FunctionSignature"/> (D-419, D-426 Decision 4): the inert
/// description of a native's name, positional parameters, return type and type
/// parameters. The constructor enforces the cross-parameter rules the hand tables
/// documented as conventions — one variadic tail and it is last, defaults trailing and
/// contiguous, never both on one signature, every type parameter declared once and used
/// at least once. Each rule has its own test.
/// </summary>
public sealed class FunctionSignatureTests {
    private static readonly SignatureType.Concrete Int = SignatureType.Of(GrobType.Int);
    private static readonly SignatureType.Concrete String = SignatureType.Of(GrobType.String);
    private static readonly SignatureType.Concrete Void = SignatureType.Of(GrobType.Nil);
    private static readonly SignatureType.Concrete Guid = SignatureType.Named(GrobType.Struct, "guid");

    private static SignatureParameter Required(string name) => new(name, String);

    private static SignatureParameter Defaulted(string name) => new(name, String, defaultValue: GrobValue.FromString(""));

    private static SignatureParameter Variadic(string name) => new(name, String, isVariadic: true);

    // ----- Happy paths -----

    [Fact]
    public void Constructor_NoParameters_IsNullaryAndNonGeneric() {
        var signature = new FunctionSignature("math.random", [], SignatureType.Of(GrobType.Float));

        Assert.Equal("math.random", signature.Name);
        Assert.Empty(signature.Parameters);
        Assert.Empty(signature.TypeParameters);
        Assert.Equal(GrobType.Float, ((SignatureType.Concrete)signature.ReturnType).Type);
        Assert.Equal(0, signature.RequiredArgumentCount);
        Assert.False(signature.IsVariadic);
        Assert.False(signature.IsGeneric);
    }

    [Fact]
    public void Constructor_VoidReturn_IsNil() {
        var signature = new FunctionSignature("log.info", [Required("message")], Void);

        Assert.Equal(Void, signature.ReturnType);
    }

    [Fact]
    public void Constructor_BareName_IsPermittedForInstanceMembers() {
        var signature = new FunctionSignature("addDays", [new SignatureParameter("days", Int)], Guid);

        Assert.Equal("addDays", signature.Name);
    }

    [Fact]
    public void Constructor_ThreeSegmentName_IsPermitted() =>
        Assert.Equal("guid.namespaces.dns", new FunctionSignature("guid.namespaces.dns", [], Guid).Name);

    [Fact]
    public void Constructor_FixedPrefixThenVariadicTail_CountsOnlyThePrefix() {
        var signature = new FunctionSignature(
            "guid.newV5", [new SignatureParameter("namespace", Guid), Variadic("name")], Guid);

        Assert.True(signature.IsVariadic);
        Assert.Equal(1, signature.RequiredArgumentCount);
    }

    [Fact]
    public void Constructor_VariadicOnly_RequiresNoFixedArguments() {
        var signature = new FunctionSignature("path.join", [Variadic("parts")], String);

        Assert.True(signature.IsVariadic);
        Assert.Equal(0, signature.RequiredArgumentCount);
    }

    [Fact]
    public void Constructor_TrailingDefaults_CountOnlyTheRequiredRun() {
        var signature = new FunctionSignature(
            "date.parse", [Required("value"), Defaulted("pattern")], SignatureType.Named(GrobType.Struct, "date"));

        Assert.Equal(1, signature.RequiredArgumentCount);
        Assert.False(signature.IsVariadic);
    }

    [Fact]
    public void Constructor_AllDefaulted_RequiresNothing() {
        var signature = new FunctionSignature("input", [Defaulted("prompt")], String);

        Assert.Equal(0, signature.RequiredArgumentCount);
    }

    [Fact]
    public void Constructor_GenericReturn_IsGeneric() {
        var signature = new FunctionSignature(
            "mapAs", [], new SignatureType.TypeParameterReference("T"),
            [new TypeParameter("T", TypeParameterConstraints.NamedStruct)]);

        Assert.True(signature.IsGeneric);
        TypeParameter declared = Assert.Single(signature.TypeParameters);
        Assert.Equal("T", declared.Name);
    }

    [Fact]
    public void Constructor_GenericArrayReturn_IsPermitted() {
        var signature = new FunctionSignature(
            "mapAs", [], new SignatureType.TypeParameterArray("T"),
            [new TypeParameter("T", TypeParameterConstraints.ArrayOfNamedStruct)]);

        Assert.True(signature.IsGeneric);
    }

    [Fact]
    public void Constructor_GenericParameter_IsPermitted() {
        var signature = new FunctionSignature(
            "accept", [new SignatureParameter("value", new SignatureType.TypeParameterReference("T"))], Void,
            [new TypeParameter("T")]);

        Assert.True(signature.IsGeneric);
    }

    [Fact]
    public void Constructor_ParameterListCopied_IsNotAliased() {
        var parameters = new List<SignatureParameter> { Required("a") };
        var signature = new FunctionSignature("f", parameters, Void);

        parameters.Add(Required("b"));

        Assert.Single(signature.Parameters);
    }

    [Fact]
    public void Constructor_TypeParameterListCopied_IsNotAliased() {
        var typeParameters = new List<TypeParameter> { new("T") };
        var signature = new FunctionSignature("f", [], new SignatureType.TypeParameterReference("T"), typeParameters);

        typeParameters.Clear();

        Assert.Single(signature.TypeParameters);
    }

    // ----- Rejected shapes: name -----

    [Fact]
    public void Constructor_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new FunctionSignature(null!, [], Void));

    [Theory]
    [InlineData("")]
    [InlineData("math.")]
    [InlineData(".sqrt")]
    [InlineData("math..sqrt")]
    [InlineData("1math.sqrt")]
    [InlineData("math sqrt")]
    public void Constructor_NameNotAQualifiedName_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionSignature(name, [], Void));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("identifier", ex.Message);
    }

    // ----- Rejected shapes: parameters -----

    [Fact]
    public void Constructor_NullParameters_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new FunctionSignature("f", null!, Void));

    [Fact]
    public void Constructor_NullParameterEntry_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionSignature("f", [Required("a"), null!], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("1", ex.Message);
    }

    [Fact]
    public void Constructor_NullReturnType_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new FunctionSignature("f", [], null!));

    [Fact]
    public void Constructor_DuplicateParameterName_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Required("a"), Required("b"), Required("a")], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("'a'", ex.Message);
    }

    [Fact]
    public void Constructor_VariadicNotLast_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Variadic("parts"), Required("sep")], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("last", ex.Message);
    }

    [Fact]
    public void Constructor_TwoVariadic_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Variadic("a"), Variadic("b")], Void));

        Assert.Equal("parameters", ex.ParamName);
    }

    [Fact]
    public void Constructor_VariadicTailWithDefault_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Defaulted("sep"), Variadic("parts")], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("default", ex.Message);
        Assert.Contains("variadic", ex.Message);
    }

    [Fact]
    public void Constructor_DefaultBeforeRequired_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Defaulted("a"), Required("b")], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("trailing", ex.Message);
    }

    [Fact]
    public void Constructor_DefaultRunInterrupted_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Required("a"), Defaulted("b"), Required("c"), Defaulted("d")], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("'c'", ex.Message);
    }

    // ----- Rejected shapes: type parameters -----

    [Fact]
    public void Constructor_NullTypeParameterEntry_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [], new SignatureType.TypeParameterReference("T"), [null!]));

        Assert.Equal("typeParameters", ex.ParamName);
    }

    [Fact]
    public void Constructor_DuplicateTypeParameter_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature(
                "f", [], new SignatureType.TypeParameterReference("T"), [new TypeParameter("T"), new TypeParameter("T")]));

        Assert.Equal("typeParameters", ex.ParamName);
        Assert.Contains("'T'", ex.Message);
    }

    [Fact]
    public void Constructor_UndeclaredTypeParameterInReturn_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [], new SignatureType.TypeParameterReference("T")));

        Assert.Equal("returnType", ex.ParamName);
        Assert.Contains("'T'", ex.Message);
    }

    [Fact]
    public void Constructor_UndeclaredTypeParameterArrayInReturn_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [], new SignatureType.TypeParameterArray("U"), [new TypeParameter("T")]));

        Assert.Equal("returnType", ex.ParamName);
        Assert.Contains("'U'", ex.Message);
    }

    [Fact]
    public void Constructor_UndeclaredTypeParameterInParameter_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature(
                "f", [new SignatureParameter("value", new SignatureType.TypeParameterArray("T"))], Void));

        Assert.Equal("parameters", ex.ParamName);
        Assert.Contains("'T'", ex.Message);
    }

    [Fact]
    public void Constructor_UnusedTypeParameter_Throws() {
        var ex = Assert.Throws<ArgumentException>(
            () => new FunctionSignature("f", [Required("a")], Void, [new TypeParameter("T")]));

        Assert.Equal("typeParameters", ex.ParamName);
        Assert.Contains("'T'", ex.Message);
    }
}
