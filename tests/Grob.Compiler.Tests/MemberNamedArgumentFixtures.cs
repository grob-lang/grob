using Grob.Core;
using Grob.Runtime;

namespace Grob.Compiler.Tests;

/// <summary>
/// A named type registered by the tests of change group 4 of the native named arguments
/// increment, for the call shapes no first-party method has: two defaulted parameters
/// (skip and fill, either-order binding) and a float parameter (widening). It goes through
/// the public registrar, as a third-party plugin's would. The primitive receivers have no
/// such fixture: their registrar is internal and sealed off by a guard test (D-426
/// Decision 3), so skip-and-fill on that path is proven by a hand-built call instead.
/// </summary>
internal static class MemberNamedArgumentFixtures {
    /// <summary>
    /// A named type <c>box</c> made by <c>acmebox.make()</c>, with <c>box.fit(width: int,
    /// left: string = "&lt;", right: string = "&gt;")</c> and <c>box.scale(factor:
    /// float)</c>.
    /// </summary>
    private sealed class NamedTypePlugin : IGrobPlugin {
        public string Name => "acmebox";

        private static SignatureParameter Required(string name, GrobType type) =>
            new(name, SignatureType.Of(type));

        private static SignatureParameter Defaulted(string name, GrobType type, GrobValue value) =>
            new(name, SignatureType.Of(type), defaultValue: value);

        public void Register(IPluginRegistrar registrar) {
            registrar.RegisterNative(
                new FunctionSignature("acmebox.make", [], SignatureType.Named(GrobType.Struct, "box")),
                new NativeFunction("acmebox.make", 0, (_, _) => GrobValue.Nil));
            registrar.RegisterNamedType(new NamedTypeDefinition("box", [],
                [
                    new NamedTypeMethodBinding(
                        new FunctionSignature("fit",
                            [
                                Required("width", GrobType.Int),
                                Defaulted("left", GrobType.String, GrobValue.FromString("<")),
                                Defaulted("right", GrobType.String, GrobValue.FromString(">")),
                            ],
                            SignatureType.Of(GrobType.String)),
                        _ => new NativeFunction("fit", 3, (args, _) => args[0])),
                    new NamedTypeMethodBinding(
                        new FunctionSignature("scale", [Required("factor", GrobType.Float)], SignatureType.Of(GrobType.String)),
                        _ => new NativeFunction("scale", 1, (args, _) => args[0])),
                    // Two forms told apart by whether position 1 takes a function (D-443).
                    new NamedTypeMethodBinding(
                        new FunctionSignature("pick",
                            [Required("first", GrobType.String), Required("second", GrobType.String)],
                            SignatureType.Of(GrobType.String)),
                        _ => new NativeFunction("pick", 2, (args, _) => args[0])) {
                        Overloads = [
                            new FunctionSignature("pick",
                                [
                                    Required("first", GrobType.String),
                                    new SignatureParameter("picker", new SignatureType.FunctionShape(
                                        [SignatureType.Of(GrobType.String)], SignatureType.Of(GrobType.String))),
                                ],
                                SignatureType.Of(GrobType.String)),
                        ],
                    },
                ],
                _ => "box"));
        }
    }

    /// <summary>A catalogue holding the named type above and nothing else.</summary>
    internal static NativeCatalogue NamedType { get; } = new NativeCatalogueBuilder().Add(new NamedTypePlugin()).Build();
}
