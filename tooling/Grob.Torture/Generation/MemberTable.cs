using Grob.Core;
using Grob.Runtime;
using Grob.Stdlib;
using Grob.Torture.Fakes;

namespace Grob.Torture.Generation;

/// <summary>
/// The natives the generator calls, read from the stdlib's catalogue so a native added to a
/// primitive joins the generator without an edit here (prompt §2 group 5; plan decision D2).
/// <list type="bullet">
///   <item><description>Every member of <c>int</c>, <c>float</c>, <c>string</c> and <c>bool</c> whose parameters and result the generator can write: no type parameter, no function-typed parameter, no variadic. An optional parameter is left to its default.</description></item>
///   <item><description>The namespace functions in <see cref="NamespaceFunctions"/>, a list kept by hand because the catalogue lists a namespace's members only by lookup. Their signatures still come from the catalogue.</description></item>
/// </list>
/// The array and map members are part of the language's checker, not the catalogue, and the
/// expression generator writes them itself.
/// </summary>
internal sealed class MemberTable {
    /// <summary>The namespace functions the generator calls, by qualified name.</summary>
    public static IReadOnlyList<string> NamespaceFunctions { get; } =
        ["math.sqrt", "math.pow", "math.log", "math.sin", "math.cos", "math.toDegrees", "strings.join"];

    private static readonly GrobType[] Primitives = [GrobType.Int, GrobType.Float, GrobType.String, GrobType.Bool];

    private static readonly Lazy<MemberTable> StdlibTable = new(() => FromCatalogue(BuildCatalogue()));

    private MemberTable(IReadOnlyList<GenNative> natives, IReadOnlyList<string> missing) {
        Natives = natives;
        Missing = missing;
    }

    /// <summary>The table over the stdlib's catalogue, built once over fake capabilities.</summary>
    public static MemberTable Stdlib => StdlibTable.Value;

    /// <summary>Every native the generator may call, members first, each group in name order.</summary>
    public IReadOnlyList<GenNative> Natives { get; }

    /// <summary>Names in <see cref="NamespaceFunctions"/> the catalogue does not hold, or whose signature the generator cannot write.</summary>
    public IReadOnlyList<string> Missing { get; }

    /// <summary>The natives whose result is <paramref name="type"/>.</summary>
    public IEnumerable<GenNative> Returning(GenType type) => Natives.Where(n => n.Result == type);

    /// <summary>Reads the table from <paramref name="catalogue"/>.</summary>
    public static MemberTable FromCatalogue(NativeCatalogue catalogue) {
        var natives = new List<GenNative>();
        foreach (GrobType receiver in Primitives) {
            if (catalogue.TryGetPrimitiveMembers(receiver, out PrimitiveMemberSurface? surface)) {
                natives.AddRange(Members(surface));
            }
        }

        var missing = new List<string>();
        foreach (string qualified in NamespaceFunctions) {
            string[] parts = qualified.Split('.');
            GenNative? native = catalogue.TryGetNative(parts[0], parts[1], out NativeEntry? entry)
                ? FromSignature(null, qualified, entry.Signature)
                : null;
            if (native is null) missing.Add(qualified);
            else natives.Add(native);
        }
        return new MemberTable(natives, missing);
    }

    /// <summary>The generator's type for a signature type, or <see langword="null"/> for one it cannot write.</summary>
    public static GenType? Map(SignatureType type) => type is SignatureType.Concrete concrete ? Map(concrete) : null;

    private static IEnumerable<GenNative> Members(PrimitiveMemberSurface surface) {
        GenType owner = Map(SignatureType.Of(surface.Receiver))!;
        IEnumerable<GenNative> properties = surface.Properties.Values
            .Select(p => Map(p.Signature.Type) is { } result ? new GenNative(owner, p.Signature.Name, [], result, IsProperty: true) : null)
            .OfType<GenNative>();
        IEnumerable<GenNative> methods = surface.Methods.Values
            .Select(m => FromSignature(owner, m.Signature.Name, m.Signature))
            .OfType<GenNative>();
        return properties.Concat(methods).OrderBy(n => n.Name, StringComparer.Ordinal).ThenBy(n => n.IsProperty);
    }

    private static GenNative? FromSignature(GenType? owner, string name, FunctionSignature signature) {
        if (signature.IsGeneric || signature.IsVariadic) return null;
        GenType? result = Map(signature.ReturnType);
        List<GenType?> parameters = [.. signature.Parameters.Take(signature.RequiredArgumentCount).Select(p => Map(p.Type))];
        if (result is null || parameters.Contains(null)) return null;
        return new GenNative(owner, name, [.. parameters.OfType<GenType>()], result, IsProperty: false);
    }

    private static GenType? Map(SignatureType.Concrete concrete) => concrete.Type switch {
        GrobType.Int => GenType.Int,
        GrobType.Float => GenType.Float,
        GrobType.String => GenType.String,
        GrobType.Bool => GenType.Bool,
        GrobType.NullableInt => new GenType.Nullable(GenType.Int),
        GrobType.NullableFloat => new GenType.Nullable(GenType.Float),
        GrobType.NullableString => new GenType.Nullable(GenType.String),
        GrobType.NullableBool => new GenType.Nullable(GenType.Bool),
        GrobType.Array when concrete.ElementType is { } element && Map(element) is { } mapped => new GenType.ArrayOf(mapped),
        _ => null,
    };

    private static NativeCatalogue BuildCatalogue() {
        HostFakes fakes = HostFakes.Create();
        return StdlibCatalogue.Build(fakes.RandomSource, fakes.Environment, fakes.Streams, fakes.Clock, fakes.FileSystem, verbose: false);
    }
}
