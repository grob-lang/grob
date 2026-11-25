using Grob.Torture.Generation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>The generator's types write the annotation a Grob program spells them with.</summary>
public sealed class GenTypeTests {
    // Keyed by the expected spelling: GenType is internal, so a theory cannot take it as a parameter.
    private static readonly Dictionary<string, GenType> Types = new(StringComparer.Ordinal) {
        { "int", GenType.Int },
        { "float", GenType.Float },
        { "string", GenType.String },
        { "bool", GenType.Bool },
        { "int[]", new GenType.ArrayOf(GenType.Int) },
        { "string[][]", new GenType.ArrayOf(new GenType.ArrayOf(GenType.String)) },
        { "map<string, float>", new GenType.MapOf(GenType.Float) },
        { "int?", new GenType.Nullable(GenType.Int) },
        { "bool[]?", new GenType.Nullable(new GenType.ArrayOf(GenType.Bool)) },
        { "fn(int, string): bool", new GenType.Function([GenType.Int, GenType.String], GenType.Bool) },
        { "fn(): int", new GenType.Function([], GenType.Int) },
        { "Box", new GenType.Struct("Box", [new GenField("v", GenType.Int)]) },
        { "Box?", new GenType.Nullable(new GenType.Struct("Box", [])) },
    };

    public static TheoryData<string> Annotations => [.. Types.Keys];

    [Theory]
    [MemberData(nameof(Annotations))]
    public void Annotation_EachType_WritesItsGrobSpelling(string expected) {
        Assert.Equal(expected, Types[expected].Annotation);
    }

    [Fact]
    public void Annotation_AnonymousStruct_CannotBeWritten() {
        var anonymous = new GenType.Anonymous([new GenField("a", GenType.Int)]);

        Assert.False(anonymous.CanAnnotate);
        Assert.Throws<InvalidOperationException>(() => anonymous.Annotation);
    }

    [Fact]
    public void CanAnnotate_ATypeHoldingAnAnonymousStruct_IsFalse() {
        var anonymous = new GenType.Anonymous([new GenField("a", GenType.Int)]);

        Assert.False(new GenType.ArrayOf(anonymous).CanAnnotate);
        Assert.False(new GenType.MapOf(anonymous).CanAnnotate);
        Assert.False(new GenType.Nullable(anonymous).CanAnnotate);
        Assert.False(new GenType.Function([anonymous], GenType.Int).CanAnnotate);
        Assert.False(new GenType.Function([GenType.Int], anonymous).CanAnnotate);
        Assert.True(new GenType.ArrayOf(GenType.Int).CanAnnotate);
        Assert.True(new GenType.Function([GenType.Int], GenType.Int).CanAnnotate);
    }

    [Fact]
    public void GetHashCode_EqualTypes_HashAlike() {
        Assert.Equal(new GenType.Function([GenType.Int], GenType.Bool).GetHashCode(), new GenType.Function([GenType.Int], GenType.Bool).GetHashCode());
        Assert.Equal(new GenType.Struct("Box", []).GetHashCode(), new GenType.Struct("Box", [new GenField("v", GenType.Int)]).GetHashCode());
        Assert.Equal(new GenType.Anonymous([new GenField("a", GenType.Int)]).GetHashCode(), new GenType.Anonymous([new GenField("a", GenType.Int)]).GetHashCode());
        Assert.Equal(new GenType.Anonymous([new GenField("a", GenType.Int)]), new GenType.Anonymous([new GenField("a", GenType.Int)]));
        Assert.NotEqual(new GenType.Anonymous([new GenField("a", GenType.Int)]), new GenType.Anonymous([new GenField("b", GenType.Int)]));
    }

    [Fact]
    public void Equality_StructuralTypes_CompareByShape() {
        Assert.Equal(new GenType.ArrayOf(GenType.Int), new GenType.ArrayOf(GenType.Int));
        Assert.Equal(new GenType.Function([GenType.Int], GenType.Int), new GenType.Function([GenType.Int], GenType.Int));
        Assert.NotEqual(new GenType.Function([GenType.Int], GenType.Int), new GenType.Function([GenType.Float], GenType.Int));
        Assert.Equal(new GenType.Struct("Box", []), new GenType.Struct("Box", [new GenField("v", GenType.Int)]));
    }
}
