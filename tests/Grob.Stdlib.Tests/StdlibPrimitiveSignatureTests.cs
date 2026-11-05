using System.Globalization;

using Grob.Core;
using Grob.Runtime;

using Xunit;

namespace Grob.Stdlib.Tests;

/// <summary>
/// Pins the signature every first-party primitive member and the <c>input</c> built-in
/// registers (D-426 change group 6): names from the gate report's §8, types, defaults and
/// return types, read from the one catalogue the checker and the VM share. These facts
/// lived in <c>PrimitiveMemberRegistry</c>'s hand table in <c>Grob.Core</c> until group 6
/// deleted it (gate report F6), and the agreement test that diffed that table against
/// plugin registration went with it.
/// </summary>
public sealed class StdlibPrimitiveSignatureTests {
    private static readonly NativeCatalogue Catalogue = StdlibCatalogue.ForCompileOnly();

    public static TheoryData<string, string, string> Methods => new() {
        { "string", "toInt", "(): int?" },
        { "string", "toFloat", "(): float?" },
        { "string", "trim", "(): string" },
        { "string", "trimStart", "(): string" },
        { "string", "trimEnd", "(): string" },
        { "string", "upper", "(): string" },
        { "string", "lower", "(): string" },
        { "string", "split", "(sep: string): string[]" },
        { "string", "contains", "(s: string): bool" },
        { "string", "startsWith", "(s: string): bool" },
        { "string", "endsWith", "(s: string): bool" },
        { "string", "replace", "(from: string, to: string): string" },
        { "string", "indexOf", "(s: string): int" },
        { "string", "lastIndexOf", "(s: string): int" },
        { "string", "substring", "(start: int, length: int): string" },
        { "string", "repeat", "(count: int): string" },
        { "string", "left", "(n: int): string" },
        { "string", "right", "(n: int): string" },
        { "string", "toString", "(): string" },
        { "string", "padLeft", "(width: int, char: string = \" \"): string" },
        { "string", "padRight", "(width: int, char: string = \" \"): string" },
        { "string", "truncate", "(maxLength: int, suffix: string = \"...\"): string" },
        { "int", "toString", "(): string" },
        { "int", "toFloat", "(): float" },
        { "int", "abs", "(): int" },
        { "int", "format", "(pattern: string): string" },
        { "float", "toString", "(): string" },
        { "float", "toInt", "(): int" },
        { "float", "round", "(): int" },
        { "float", "roundTo", "(decimals: int): float" },
        { "float", "floor", "(): int" },
        { "float", "ceil", "(): int" },
        { "float", "abs", "(): float" },
        { "float", "format", "(pattern: string): string" },
        { "bool", "toString", "(): string" },
    };

    public static TheoryData<string, string, string> Properties => new() {
        { "string", "length", "int" },
        { "string", "isEmpty", "bool" },
    };

    [Theory]
    [MemberData(nameof(Methods))]
    public void Method_RegistersItsSignature(string receiver, string name, string expected) {
        PrimitiveMemberSurface surface = SurfaceOf(receiver);

        Assert.True(surface.Methods.TryGetValue(name, out PrimitiveMethodEntry? entry), $"'{receiver}.{name}' is not a method.");
        Assert.Equal(name, entry.Signature.Name);
        Assert.Equal($"{receiver}.{name}", entry.QualifiedNativeName);
        Assert.Equal(expected, Describe(entry.Signature));
    }

    [Theory]
    [MemberData(nameof(Properties))]
    public void Property_RegistersItsType(string receiver, string name, string expected) {
        PrimitiveMemberSurface surface = SurfaceOf(receiver);

        Assert.True(surface.Properties.TryGetValue(name, out PrimitivePropertyEntry? entry), $"'{receiver}.{name}' is not a property.");
        Assert.Equal($"{receiver}.{name}", entry.QualifiedNativeName);
        Assert.Equal(expected, Describe(entry.Signature.Type));
    }

    [Theory]
    [InlineData("string", 2, 22)]
    [InlineData("int", 0, 4)]
    [InlineData("float", 0, 8)]
    [InlineData("bool", 0, 1)]
    public void Surface_HoldsExactlyTheDocumentedMembers(string receiver, int properties, int methods) {
        PrimitiveMemberSurface surface = SurfaceOf(receiver);

        Assert.Equal(properties, surface.Properties.Count);
        Assert.Equal(methods, surface.Methods.Count);
    }

    [Theory]
    [InlineData(GrobType.Array)]
    [InlineData(GrobType.Struct)]
    [InlineData(GrobType.NullableString)]
    public void NonPrimitiveReceiver_HasNoSurface(GrobType receiver) =>
        Assert.False(Catalogue.TryGetPrimitiveMembers(receiver, out _));

    [Fact]
    public void Input_RegistersAsABuiltinWithAnOptionalPrompt() {
        Assert.True(Catalogue.TryGetBuiltin("input", out NativeEntry? entry));
        Assert.Equal("(prompt: string = \"\"): string", Describe(entry.Signature));
    }

    [Fact]
    public void EveryFunctionGlobal_IsATypedRegistration() {
        // The catalogue-wide successor of group 5's namespace check: every callable the VM
        // loads is reachable through a signature, so nothing registers untyped.
        var untyped = Catalogue.Globals
            .Where(g => g.Value.Kind == GrobValueKind.Function)
            .Select(g => g.Key)
            .Where(name => !IsTyped(name))
            .ToList();

        Assert.Empty(untyped);
    }

    private static bool IsTyped(string name) {
        int dot = name.IndexOf('.');
        if (dot < 0) return Catalogue.TryGetBuiltin(name, out _);
        string prefix = name[..dot];
        string member = name[(dot + 1)..];
        if (Catalogue.TryGetNative(prefix, member, out _)) return true;
        return ReceiverOf(prefix) is GrobType receiver
            && Catalogue.TryGetPrimitiveMembers(receiver, out PrimitiveMemberSurface? surface)
            && (surface.Methods.ContainsKey(member) || surface.Properties.ContainsKey(member));
    }

    private static PrimitiveMemberSurface SurfaceOf(string receiver) {
        Assert.True(Catalogue.TryGetPrimitiveMembers(ReceiverOf(receiver)!.Value, out PrimitiveMemberSurface? surface));
        return surface;
    }

    private static GrobType? ReceiverOf(string name) => name switch {
        "string" => GrobType.String,
        "int" => GrobType.Int,
        "float" => GrobType.Float,
        "bool" => GrobType.Bool,
        _ => null,
    };

    private static string Describe(FunctionSignature signature) =>
        $"({string.Join(", ", signature.Parameters.Select(Describe))}): {Describe(signature.ReturnType)}";

    private static string Describe(SignatureParameter parameter) {
        string text = $"{parameter.Name}: {Describe(parameter.Type)}";
        return parameter.Default is GrobValue value
            ? $"{text} = \"{value.AsString().ToString(CultureInfo.InvariantCulture)}\""
            : text;
    }

    private static string Describe(SignatureType type) {
        var concrete = Assert.IsType<SignatureType.Concrete>(type);
        if (concrete.ElementType is { } element) return Describe(element) + "[]";
        return concrete.Type switch {
            GrobType.Int => "int",
            GrobType.NullableInt => "int?",
            GrobType.Float => "float",
            GrobType.NullableFloat => "float?",
            GrobType.String => "string",
            GrobType.Bool => "bool",
            _ => concrete.Type.ToString(),
        };
    }
}
