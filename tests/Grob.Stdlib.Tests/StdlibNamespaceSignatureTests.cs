using System.Globalization;

using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Pins the signature every first-party namespace native registers (D-426 change group 5):
/// parameter names from the gate report's §8, types, defaults, variadic tails and return
/// types, read from the one catalogue the checker and the VM share. These facts lived in
/// <c>NamespaceRegistry</c>'s hand table until group 5 deleted it (gate report F6); the
/// agreement test that diffed that table against plugin registration went with it, since
/// a namespace native can no longer register without a signature.
/// </summary>
public sealed class StdlibNamespaceSignatureTests {
    private static readonly NativeCatalogue Catalogue = StdlibCatalogue.ForCompileOnly();

    private static readonly string[] PrimitiveReceiverNames = ["int", "float", "string", "bool"];

    public static TheoryData<string, string> Natives => new() {
        { "math.sqrt", "(n: float): float" },
        { "math.pow", "(base: float, exp: float): float" },
        { "math.log", "(n: float): float" },
        { "math.log10", "(n: float): float" },
        { "math.sin", "(radians: float): float" },
        { "math.cos", "(radians: float): float" },
        { "math.tan", "(radians: float): float" },
        { "math.asin", "(n: float): float" },
        { "math.acos", "(n: float): float" },
        { "math.atan", "(n: float): float" },
        { "math.atan2", "(y: float, x: float): float" },
        { "math.toRadians", "(degrees: float): float" },
        { "math.toDegrees", "(radians: float): float" },
        { "math.random", "(): float" },
        { "math.randomInt", "(min: int, max: int): int" },
        { "math.randomSeed", "(seed: int): nil" },
        { "path.join", "(...parts: string): string" },
        { "path.joinAll", "(parts: string[]): string" },
        { "path.extension", "(p: string): string" },
        { "path.filename", "(p: string): string" },
        { "path.stem", "(p: string): string" },
        { "path.directory", "(p: string): string" },
        { "path.resolve", "(p: string): string" },
        { "path.normalise", "(p: string): string" },
        { "path.isAbsolute", "(p: string): bool" },
        { "path.isRelative", "(p: string): bool" },
        { "path.changeExtension", "(p: string, ext: string): string" },
        { "strings.join", "(parts: string[], separator: string): string" },
        { "env.get", "(key: string): string?" },
        { "env.require", "(key: string): string" },
        { "env.has", "(key: string): bool" },
        { "env.set", "(key: string, value: string): nil" },
        { "env.all", "(): map" },
        { "log.debug", "(message: string): nil" },
        { "log.info", "(message: string): nil" },
        { "log.warning", "(message: string): nil" },
        { "log.error", "(message: string): nil" },
        { "log.setLevel", "(level: string): nil" },
        { "guid.newV4", "(): guid" },
        { "guid.newV7", "(): guid" },
        { "guid.newV5", "(namespace: guid, ...name: string): guid" },
        { "guid.parse", "(value: string): guid" },
        { "guid.tryParse", "(value: string): guid?" },
        { "date.now", "(): date" },
        { "date.today", "(): date" },
        { "date.of", "(year: int, month: int, day: int): date" },
        { "date.ofTime", "(year: int, month: int, day: int, hour: int, minute: int, second: int): date" },
        { "date.parse", "(value: string, pattern: string = \"\"): date" },
        { "date.fromUnixSeconds", "(seconds: int): date" },
        { "date.fromUnixMillis", "(millis: int): date" },
        { "int.min", "(a: int, b: int): int" },
        { "int.max", "(a: int, b: int): int" },
        { "int.clamp", "(v: int, lo: int, hi: int): int" },
        { "float.min", "(a: float, b: float): float" },
        { "float.max", "(a: float, b: float): float" },
        { "float.clamp", "(v: float, lo: float, hi: float): float" },
        { "formatAs.table", "(items: array, columns: array): string" },
        { "formatAs.list", "(item: struct, fields: array): string" },
        { "formatAs.csv", "(items: array, columns: array): string" },
    };

    public static TheoryData<string, string> Constants => new() {
        { "math.pi", "float" },
        { "math.e", "float" },
        { "math.tau", "float" },
        { "path.separator", "string" },
        { "guid.empty", "guid" },
        { "guid.namespaces.dns", "guid" },
        { "guid.namespaces.url", "guid" },
        { "guid.namespaces.oid", "guid" },
    };

    [Theory]
    [MemberData(nameof(Natives))]
    public void Native_RegistersItsSignature(string qualifiedName, string expected) {
        (string namespaceName, string memberName) = Split(qualifiedName);

        Assert.True(Catalogue.TryGetNative(namespaceName, memberName, out NativeEntry? entry),
            $"'{qualifiedName}' has no signature in the catalogue.");
        Assert.Equal(qualifiedName, entry.Signature.Name);
        Assert.Equal(expected, Describe(entry.Signature));
    }

    [Theory]
    [MemberData(nameof(Constants))]
    public void Constant_RegistersItsType(string qualifiedName, string expected) {
        (string namespaceName, string memberName) = Split(qualifiedName);

        Assert.True(Catalogue.TryGetConstant(namespaceName, memberName, out ConstantEntry? entry),
            $"'{qualifiedName}' is not a constant in the catalogue.");
        Assert.Equal(expected, Describe(entry.Type));
    }

    [Fact]
    public void EveryNamespaceGlobal_IsATypedMember() {
        // A namespace global that is neither a typed native nor a constant registered
        // without a signature: the gap the deleted agreement test used to catch. Primitive
        // receiver prefixes are skipped; their instance members migrate in group 6.
        var untyped = Catalogue.Globals.Keys
            .Where(name => name.Contains('.'))
            .Select(name => (Name: name, Parts: Split(name)))
            .Where(g => !PrimitiveReceiverNames.Contains(g.Parts.NamespaceName))
            .Where(g => !Catalogue.TryGetNative(g.Parts.NamespaceName, g.Parts.MemberName, out _)
                && !Catalogue.TryGetConstant(g.Parts.NamespaceName, g.Parts.MemberName, out _))
            .Select(g => g.Name)
            .ToList();

        Assert.Empty(untyped);
    }

    [Fact]
    public void NamespaceNames_AreExactlyTheStdlibNamespaces() =>
        Assert.Equal(
            ["date", "env", "float", "formatAs", "guid", "int", "log", "math", "path", "strings"],
            Catalogue.NamespaceNames.Order(StringComparer.Ordinal));

    private static (string NamespaceName, string MemberName) Split(string qualifiedName) {
        int dot = qualifiedName.IndexOf('.');
        return (qualifiedName[..dot], qualifiedName[(dot + 1)..]);
    }

    private static string Describe(FunctionSignature signature) =>
        $"({string.Join(", ", signature.Parameters.Select(Describe))}): {Describe(signature.ReturnType)}";

    private static string Describe(SignatureParameter parameter) {
        string text = $"{(parameter.IsVariadic ? "..." : "")}{parameter.Name}: {Describe(parameter.Type)}";
        return parameter.Default is GrobValue value ? $"{text} = {DescribeDefault(value)}" : text;
    }

    private static string DescribeDefault(GrobValue value) => value.Kind switch {
        GrobValueKind.String => $"\"{value.AsString()}\"",
        GrobValueKind.Int => value.AsInt().ToString(CultureInfo.InvariantCulture),
        _ => value.Kind.ToString(),
    };

    private static string Describe(SignatureType type) {
        var concrete = Assert.IsType<SignatureType.Concrete>(type);
        string nullable = concrete.IsNullable ? "?" : "";
        if (concrete.NamedTypeName is { } named) return named + nullable;
        if (concrete.ElementType is { } element) return Describe(element) + "[]" + nullable;
        return concrete.Type switch {
            GrobType.Int or GrobType.NullableInt => "int",
            GrobType.Float or GrobType.NullableFloat => "float",
            GrobType.String or GrobType.NullableString => "string",
            GrobType.Bool or GrobType.NullableBool => "bool",
            GrobType.Array or GrobType.NullableArray => "array",
            GrobType.Map or GrobType.NullableMap => "map",
            GrobType.Struct or GrobType.NullableStruct => "struct",
            GrobType.Nil => "nil",
            _ => concrete.Type.ToString(),
        } + nullable;
    }
}
