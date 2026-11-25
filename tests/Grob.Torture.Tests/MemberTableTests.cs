using Grob.Core;
using Grob.Runtime;
using Grob.Torture.Generation;

using Xunit;

namespace Grob.Torture.Tests;

/// <summary>
/// The generator reads the natives it calls from the stdlib's catalogue (plan decision D2): every
/// primitive member it can write, and the namespace functions it names, with their signatures.
/// </summary>
public sealed class MemberTableTests {
    [Fact]
    public void Stdlib_EveryNamedNamespaceFunction_IsInTheCatalogue() {
        Assert.Empty(MemberTable.Stdlib.Missing);
        Assert.Equal(MemberTable.NamespaceFunctions, MemberTable.Stdlib.Natives.Where(n => n.Owner is null).Select(n => n.Name));
    }

    [Fact]
    public void Stdlib_StringMembers_ComeWithTheirSignatures() {
        IReadOnlyList<GenNative> natives = MemberTable.Stdlib.Natives;

        var upper = new GenNative(GenType.String, "upper", [], GenType.String, IsProperty: false);
        Assert.Contains(upper, natives);
        Assert.Equal(upper.GetHashCode(), natives.Single(n => n.Name == "upper" && n.Owner == GenType.String).GetHashCode());
        Assert.Contains(new GenNative(GenType.String, "length", [], GenType.Int, IsProperty: true), natives);
        Assert.Contains(natives, n => n.Owner == GenType.String && n.Name == "toInt" && n.Result == new GenType.Nullable(GenType.Int));
        Assert.Contains(natives, n => n.Owner == GenType.String && n.Name == "split" && n.Result == new GenType.ArrayOf(GenType.String)
            && n.Parameters.SequenceEqual([GenType.String]));
    }

    [Fact]
    public void Stdlib_NamespaceFunction_TakesItsCatalogueParameters() {
        GenNative pow = Assert.Single(MemberTable.Stdlib.Natives, n => n.Name == "math.pow");

        Assert.Equal([GenType.Float, GenType.Float], pow.Parameters);
        Assert.Equal(GenType.Float, pow.Result);
        Assert.Null(pow.Owner);
    }

    [Fact]
    public void Returning_AType_GivesOnlyTheNativesWithThatResult() {
        List<GenNative> returningBool = [.. MemberTable.Stdlib.Returning(GenType.Bool)];

        Assert.NotEmpty(returningBool);
        Assert.All(returningBool, n => Assert.Equal(GenType.Bool, n.Result));
    }

    [Fact]
    public void FromCatalogue_EmptyCatalogue_ReportsEveryNamespaceFunctionMissing() {
        MemberTable table = MemberTable.FromCatalogue(NativeCatalogue.Empty);

        Assert.Empty(table.Natives);
        Assert.Equal(MemberTable.NamespaceFunctions, table.Missing);
    }

    [Fact]
    public void FromCatalogue_SignaturesTheGeneratorCannotWrite_AreReportedMissing() {
        NativeCatalogue catalogue = new NativeCatalogueBuilder().Add(new MathLike()).Build();

        MemberTable table = MemberTable.FromCatalogue(catalogue);

        Assert.Equal(["math.log"], table.Natives.Select(n => n.Name));
        Assert.Contains("math.sqrt", table.Missing);
        Assert.Contains("math.pow", table.Missing);
        Assert.Contains("math.sin", table.Missing);
    }

    public static TheoryData<string> Unwritable => ["type parameter", "struct", "map", "array of a struct"];

    [Theory]
    [MemberData(nameof(Unwritable))]
    public void Map_ATypeTheGeneratorCannotWrite_IsNull(string name) {
        SignatureType type = name switch {
            "type parameter" => new SignatureType.TypeParameterReference("T"),
            "struct" => SignatureType.Named(GrobType.Struct, "date"),
            "map" => SignatureType.Of(GrobType.Map),
            _ => SignatureType.ArrayOf(SignatureType.Named(GrobType.Struct, "date")),
        };

        Assert.Null(MemberTable.Map(type));
    }

    // A math namespace whose sqrt is generic, whose pow takes a struct and whose sin is variadic:
    // none can be written, so only log joins the table.
    private sealed class MathLike : IGrobPlugin {
        public string Name => "math";

        public void Register(IPluginRegistrar registrar) {
            var echo = new NativeFunction("echo", 1, (args, _) => args[0]);
            SignatureType.Concrete number = SignatureType.Of(GrobType.Float);
            registrar.RegisterNative(new FunctionSignature("math.sqrt", [new SignatureParameter("n", new SignatureType.TypeParameterReference("T"))], number, [new TypeParameter("T")]), echo);
            registrar.RegisterNative(new FunctionSignature("math.pow", [new SignatureParameter("n", SignatureType.Named(GrobType.Struct, "date"))], number), echo);
            registrar.RegisterNative(new FunctionSignature("math.sin", [new SignatureParameter("n", number, isVariadic: true)], number), echo);
            registrar.RegisterNative(new FunctionSignature("math.log", [new SignatureParameter("n", number)], number), echo);
        }
    }
}
