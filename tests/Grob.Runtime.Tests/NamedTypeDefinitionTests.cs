using Grob.Core;

using Xunit;

using static Grob.Runtime.Tests.TestPlugins;

namespace Grob.Runtime.Tests;

/// <summary>
/// Tests for <see cref="NamedTypeDefinition"/>: what a plugin hands
/// <c>IPluginRegistrar.RegisterNamedType</c> for a plugin-owned named type (D-426
/// Decision 3) — the type's name, its property and method bindings and its renderer.
/// The definition validates itself so the catalogue never holds a type with two members
/// of one name or a method whose signature name is qualified.
/// </summary>
public sealed class NamedTypeDefinitionTests {
    private static readonly NamedTypePropertyBinding Year =
        new(new PropertySignature("year", Int), s => GrobValue.FromInt(2026));

    private static readonly NamedTypeMethodBinding AddDays =
        new(Signature("addDays", new SignatureParameter("days", Int)), s => Native("addDays", 1));

    private static string Render(GrobValue value) => "date";

    [Fact]
    public void Constructor_ValidDefinition_CarriesEverything() {
        var definition = new NamedTypeDefinition("date", [Year], [AddDays], Render);

        Assert.Equal("date", definition.Name);
        Assert.Same(Year, Assert.Single(definition.Properties));
        Assert.Same(AddDays, Assert.Single(definition.Methods));
        Assert.Equal("date", definition.Render(GrobValue.Nil));
    }

    [Fact]
    public void Constructor_NoMembers_IsPermitted() {
        var definition = new NamedTypeDefinition("token", [], [], Render);

        Assert.Empty(definition.Properties);
        Assert.Empty(definition.Methods);
    }

    [Fact]
    public void Constructor_ListsCopied_AreNotAliased() {
        var properties = new List<NamedTypePropertyBinding> { Year };
        var definition = new NamedTypeDefinition("date", properties, [], Render);

        properties.Clear();

        Assert.Single(definition.Properties);
    }

    [Fact]
    public void Constructor_NullName_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new NamedTypeDefinition(null!, [], [], Render));

    [Theory]
    [InlineData("")]
    [InlineData("my type")]
    [InlineData("date.time")]
    public void Constructor_NameNotAnIdentifier_Throws(string name) {
        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition(name, [], [], Render));

        Assert.Equal("name", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullProperties_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new NamedTypeDefinition("date", null!, [], Render));

    [Fact]
    public void Constructor_NullMethods_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new NamedTypeDefinition("date", [], null!, Render));

    [Fact]
    public void Constructor_NullRender_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new NamedTypeDefinition("date", [], [], null!));

    [Fact]
    public void Constructor_NullPropertyEntry_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [null!], [], Render));

        Assert.Equal("properties", ex.ParamName);
    }

    [Fact]
    public void Constructor_NullMethodEntry_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [], [null!], Render));

        Assert.Equal("methods", ex.ParamName);
    }

    [Fact]
    public void Constructor_PropertyWithNullGetter_Throws() {
        var binding = new NamedTypePropertyBinding(new PropertySignature("year", Int), null!);

        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [binding], [], Render));

        Assert.Equal("properties", ex.ParamName);
        Assert.Contains("'year'", ex.Message);
    }

    [Fact]
    public void Constructor_MethodWithNullBinder_Throws() {
        var binding = new NamedTypeMethodBinding(Signature("addDays"), null!);

        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [], [binding], Render));

        Assert.Equal("methods", ex.ParamName);
        Assert.Contains("'addDays'", ex.Message);
    }

    [Fact]
    public void Constructor_MethodWithNullSignature_Throws() {
        var binding = new NamedTypeMethodBinding(null!, s => Native("x"));

        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [], [binding], Render));

        Assert.Equal("methods", ex.ParamName);
    }

    [Fact]
    public void Constructor_PropertyWithNullSignature_Throws() {
        var binding = new NamedTypePropertyBinding(null!, s => GrobValue.Nil);

        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [binding], [], Render));

        Assert.Equal("properties", ex.ParamName);
    }

    [Fact]
    public void Constructor_QualifiedMethodName_Throws() {
        var binding = new NamedTypeMethodBinding(Signature("date.addDays"), s => Native("addDays"));

        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [], [binding], Render));

        Assert.Equal("methods", ex.ParamName);
        Assert.Contains("bare", ex.Message);
    }

    [Fact]
    public void Constructor_DuplicatePropertyName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [Year, Year], [], Render));

        Assert.Equal("properties", ex.ParamName);
        Assert.Contains("'year'", ex.Message);
    }

    [Fact]
    public void Constructor_DuplicateMethodName_Throws() {
        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [], [AddDays, AddDays], Render));

        Assert.Equal("methods", ex.ParamName);
        Assert.Contains("'addDays'", ex.Message);
    }

    [Fact]
    public void Constructor_PropertyAndMethodShareName_Throws() {
        var method = new NamedTypeMethodBinding(Signature("year"), s => Native("year"));

        var ex = Assert.Throws<ArgumentException>(() => new NamedTypeDefinition("date", [Year], [method], Render));

        Assert.Equal("methods", ex.ParamName);
        Assert.Contains("'year'", ex.Message);
    }
}
