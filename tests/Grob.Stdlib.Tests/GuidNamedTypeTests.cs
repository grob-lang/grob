using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// The <c>guid</c> named type as <see cref="GuidPlugin"/> registers it (D-426 Decision 3,
/// change group 7): two properties, three parameterless methods and the canonical-form
/// renderer, read straight off the catalogue the plugin builds. Re-homes what
/// <c>NamedTypeRegistry</c>'s <c>guid</c> entry used to carry (gate report F6); the
/// end-to-end dispatch through a VM is <see cref="GuidPluginTests"/>' job.
/// </summary>
public sealed class GuidNamedTypeTests {
    private const string Sample = "01234567-89ab-4def-8123-456789abcdef";

    private static readonly NativeCatalogue Catalogue = new NativeCatalogueBuilder()
        .Add(new GuidPlugin(new TestRandomSource(1), new TestClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))))
        .Build();

    private static NamedTypeSurface Guid =>
        Catalogue.TryGetNamedType("guid", out NamedTypeSurface? surface)
            ? surface
            : throw new InvalidOperationException("GuidPlugin did not register the guid named type.");

    private static GrobStruct MakeGuid(string canonical) =>
        new("guid", [new KeyValuePair<string, GrobValue>("__value", GrobValue.FromString(canonical))]);

    private static GrobValue Call(string method, GrobStruct receiver) =>
        Guid.Methods[method].Bind(receiver).Implementation([], default);

    [Fact]
    public void Register_DeclaresTheGuidNamedType() {
        Assert.Equal(["guid"], Catalogue.NamedTypeNames);
        Assert.Equal("guid", Guid.Owner);
        Assert.Equal("guid", Guid.Definition.Name);
    }

    [Fact]
    public void Register_DeclaresTwoPropertiesAndThreeMethods() {
        Assert.Equal(2, Guid.Properties.Count);
        Assert.Equal(3, Guid.Methods.Count);
    }

    [Theory]
    [InlineData("version", GrobType.Int)]
    [InlineData("isEmpty", GrobType.Bool)]
    public void Property_HasItsDeclaredType(string name, GrobType expected) =>
        Assert.Equal(SignatureType.Of(expected), Guid.Properties[name].Signature.Type);

    [Theory]
    [InlineData("toString")]
    [InlineData("toUpperString")]
    [InlineData("toCompactString")]
    public void Method_TakesNothingAndReturnsString(string method) {
        FunctionSignature signature = Guid.Methods[method].Signature;

        Assert.Equal(method, signature.Name);
        Assert.Empty(signature.Parameters);
        Assert.Equal(SignatureType.Of(GrobType.String), signature.ReturnType);
        Assert.False(signature.IsGeneric);
    }

    [Fact]
    public void Version_ReadsTheUuidVersionNibble() =>
        Assert.Equal(4, Guid.Properties["version"].Get(MakeGuid(Sample)).AsInt());

    [Fact]
    public void IsEmpty_IsTrueOnlyForAllZeros() {
        Assert.True(Guid.Properties["isEmpty"].Get(MakeGuid("00000000-0000-0000-0000-000000000000")).AsBool());
        Assert.False(Guid.Properties["isEmpty"].Get(MakeGuid(Sample)).AsBool());
    }

    [Fact]
    public void ToString_ReturnsTheCanonicalLowercaseForm() =>
        Assert.Equal(Sample, Call("toString", MakeGuid(Sample)).AsString());

    [Fact]
    public void ToUpperString_UppercasesTheCanonicalForm() =>
        Assert.Equal("01234567-89AB-4DEF-8123-456789ABCDEF", Call("toUpperString", MakeGuid(Sample)).AsString());

    [Fact]
    public void ToCompactString_StripsTheHyphens() =>
        Assert.Equal("0123456789ab4def8123456789abcdef", Call("toCompactString", MakeGuid(Sample)).AsString());

    [Fact]
    public void Render_IsTheCanonicalForm() {
        GrobValue g = GrobValue.FromStruct(MakeGuid(Sample));

        Assert.Equal(Sample, Guid.Definition.Render(g));
        Assert.Equal(Sample, Catalogue.RenderValue(g));
    }
}
