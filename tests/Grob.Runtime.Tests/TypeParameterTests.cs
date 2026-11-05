using Xunit;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="TypeParameter"/> and <see cref="TypeParameterConstraints"/>:
/// a type parameter a native signature declares, with the per-signature constraint
/// D-418 fixes (<c>json.Node.mapAs&lt;T&gt;</c> accepts a named struct or an array of
/// one; <c>csv.Table.mapAs&lt;T&gt;</c> accepts only an array of one). Substitution
/// and constraint checking arrive with the <c>mapAs&lt;T&gt;</c> increment; here the
/// shape is constructed and validated only (D-426 Decision 4).
/// </summary>
public sealed class TypeParameterTests {
    [Fact]
    public void Constructor_NameOnly_IsUnconstrained() {
        var parameter = new TypeParameter("T");

        Assert.Equal("T", parameter.Name);
        Assert.Equal(TypeParameterConstraints.None, parameter.Constraints);
    }

    [Fact]
    public void Constructor_SingleConstraint_IsCarried() {
        var parameter = new TypeParameter("T", TypeParameterConstraints.ArrayOfNamedStruct);

        Assert.Equal(TypeParameterConstraints.ArrayOfNamedStruct, parameter.Constraints);
    }

    [Fact]
    public void Constructor_CombinedConstraint_IsCarried() {
        const TypeParameterConstraints either =
            TypeParameterConstraints.NamedStruct | TypeParameterConstraints.ArrayOfNamedStruct;

        var parameter = new TypeParameter("T", either);

        Assert.Equal(either, parameter.Constraints);
    }

    [Fact]
    public void Equality_SameNameAndConstraint_AreEqual() =>
        Assert.Equal(
            new TypeParameter("T", TypeParameterConstraints.NamedStruct),
            new TypeParameter("T", TypeParameterConstraints.NamedStruct));

    [Fact]
    public void Constructor_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new TypeParameter(null!));

    [Theory]
    [InlineData("")]
    [InlineData("T U")]
    [InlineData("1T")]
    [InlineData("T.U")]
    public void Constructor_NameNotAnIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new TypeParameter(name));

        Assert.Equal("name", ex.ParamName);
        Assert.Contains("identifier", ex.Message);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(-1)]
    [InlineData(7)]
    public void Constructor_UndefinedConstraintBits_Throws(int bits) {
        var ex = Assert.Throws<ArgumentException>(() => new TypeParameter("T", (TypeParameterConstraints)bits));

        Assert.Equal("constraint", ex.ParamName);
    }
}
