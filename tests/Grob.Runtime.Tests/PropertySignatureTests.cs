using Grob.Core;

using Xunit;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="PropertySignature"/>: a property on a receiver — a name and a
/// concrete type, no parameters, never called with parentheses. A property is its own
/// record rather than a zero-parameter <see cref="FunctionSignature"/> because the
/// distinction is data the checker reports on (<c>s.length()</c> is E1002 today and
/// the probe corpus pins it).
/// </summary>
public sealed class PropertySignatureTests {
    [Fact]
    public void Constructor_NameAndType_AreCarried() {
        var property = new PropertySignature("length", SignatureType.Of(GrobType.Int));

        Assert.Equal("length", property.Name);
        Assert.Equal(GrobType.Int, property.Type.Type);
    }

    [Fact]
    public void Constructor_NominalType_IsPermitted() {
        var property = new PropertySignature("utc", SignatureType.Named(GrobType.Struct, "date"));

        Assert.Equal("date", property.Type.NamedTypeName);
    }

    [Fact]
    public void Equality_SameShape_AreEqual() =>
        Assert.Equal(
            new PropertySignature("length", SignatureType.Of(GrobType.Int)),
            new PropertySignature("length", SignatureType.Of(GrobType.Int)));

    [Fact]
    public void Constructor_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new PropertySignature(null!, SignatureType.Of(GrobType.Int)));

    [Theory]
    [InlineData("")]
    [InlineData("is empty")]
    [InlineData("1st")]
    [InlineData("string.length")]
    public void Constructor_NameNotABareIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new PropertySignature(name, SignatureType.Of(GrobType.Int)));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("identifier", ex.Message);
    }

    [Fact]
    public void Constructor_NullType_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new PropertySignature("length", null!));

    [Fact]
    public void Constructor_NilType_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new PropertySignature("nothing", SignatureType.Of(GrobType.Nil)));

        Assert.Equal("type", ex.ParamName);
        Assert.Contains("nil", ex.Message);
    }
}
